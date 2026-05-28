using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Handles ASIO audio routing with N-1 logic for unified Intercom Groups.
    /// Supports cross-mode mixing: NDI channels in the same group are included in the N-1 mix.
    /// Uses ArrayPool for zero-allocation audio processing.
    /// </summary>
    public class AsioAudioEngine : IWaveProvider
    {
        // Audio constants
        private const int SAMPLE_RATE = 48000;                    // Standard audio sample rate
        private const int NDI_FRAME_SAMPLES = 1920;               // NDI frame size: 40ms @ 48kHz
        private const int RING_BUFFER_SAMPLES_400MS = 19200;      // 400ms buffer @ 48kHz

        private List<ChannelState> _channels;
        private byte[] _microphoneBuffer;
        private readonly int _outputChannelCount;

        // LOCK-FREE DOUBLE BUFFERING: Two sets of buffers for zero-contention audio processing
        // ProcessAsioInput writes to one buffer, Read reads from the other, then atomic swap
        private readonly Dictionary<int, float[]> _asioInputBuffers0 = new Dictionary<int, float[]>();
        private readonly Dictionary<int, float[]> _asioInputBuffers1 = new Dictionary<int, float[]>();
        private int _currentReadBufferIndex = 0; // Atomic: 0 or 1, indicates which buffer Read should use

        private readonly Dictionary<int, AudioRingBuffer> _microphoneRingBuffers = new Dictionary<int, AudioRingBuffer>(); // Ring buffer per channel (output)
        private readonly Dictionary<int, AudioRingBuffer> _asioInputRingBuffers = new Dictionary<int, AudioRingBuffer>(); // Ring buffer per ASIO input channel

        // Lightweight lock ONLY for ring buffer management (channel add/remove), NOT for audio processing
        private readonly object _ringBufferManagementLock = new object();

        private readonly Dictionary<int, byte[]> _lastAsioInputCache = new Dictionary<int, byte[]>(); // Cache last frame for non-blocking read

        // CROSS-MODE AUDIO: NDI channel audio for unified group mixing
        // Written by NDI thread (every 40ms), read by ASIO callback thread.
        // Uses ring buffers to properly stream NDI frames (1920 samples/40ms) to ASIO callbacks (small buffers every ~3-5ms).
        // Key = channelNumber, Value = AudioRingBuffer for that NDI channel
        private readonly Dictionary<int, AudioRingBuffer> _crossModeRingBuffers = new Dictionary<int, AudioRingBuffer>();

        // ArrayPools for zero-allocation processing
        private static readonly ArrayPool<float> _floatPool = ArrayPool<float>.Shared;
        private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;

        // Wave format: Stereo, 48kHz, Float32
        public WaveFormat WaveFormat { get; private set; }

        public AsioAudioEngine(int outputChannelCount)
        {
            _outputChannelCount = outputChannelCount;
            _channels = new List<ChannelState>();

            // ASIO uses multi-channel interleaved format
            // We'll use stereo as base format (ASIO will handle channel mapping)
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, outputChannelCount);
        }

        public void UpdateChannelStates(IEnumerable<ChannelState> channels)
        {
            _channels = channels.ToList();

            // Clean up ring buffers for channels that no longer have TALK enabled or are no longer ASIO
            // Note: This is configuration change, not audio processing path, so lightweight lock is OK
            lock (_ringBufferManagementLock)
            {
                var activeOutputChannels = _channels
                    .Where(c => c.Mode == Models.ChannelMode.ASIO && c.TalkEnabled)
                    .Select(c => c.AsioOutputChannel)
                    .ToHashSet();

                var toRemove = _microphoneRingBuffers.Keys
                    .Where(outputCh => !activeOutputChannels.Contains(outputCh))
                    .ToList();

                foreach (var outputCh in toRemove)
                {
                    _microphoneRingBuffers.Remove(outputCh);
                }
            }
        }

        public void SetMicrophoneBuffer(byte[] buffer)
        {
            _microphoneBuffer = buffer;

            // Write microphone audio to ring buffers for all ASIO channels with TALK enabled.
            // Manual filter loop instead of LINQ Where — this is the audio hot path; the
            // Where iterator + closure used to allocate ~25/sec * N channels.
            lock (_ringBufferManagementLock)
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    var channel = _channels[i];
                    if (channel.Mode != Models.ChannelMode.ASIO || !channel.TalkEnabled)
                        continue;

                    int outputCh = channel.AsioOutputChannel;

                    // Create ring buffer if it doesn't exist (400ms @ 48kHz)
                    if (!_microphoneRingBuffers.TryGetValue(outputCh, out var rb))
                    {
                        rb = new AudioRingBuffer(RING_BUFFER_SAMPLES_400MS);
                        _microphoneRingBuffers[outputCh] = rb;
                    }

                    // Write audio to ring buffer (AudioRingBuffer has internal locking)
                    rb.Write(buffer);
                }
            }
        }

        /// <summary>
        /// Called by NDI thread (every 40ms) to provide NDI channel audio for cross-mode N-1 mixing.
        /// Writes NDI audio into per-channel ring buffers so ASIO callbacks can consume it progressively.
        /// Without ring buffers, the ASIO callback (every ~3-5ms) would re-read the same 40ms NDI frame,
        /// creating a short loop artifact ("martian" effect).
        /// </summary>
        public void SetCrossModeAudio(Dictionary<int, byte[]> receivedAudio, List<ChannelState> allChannels)
        {
            try
            {
                foreach (var channel in allChannels)
                {
                    // Only include NDI channels that have audio and are in a group
                    if (channel.Mode != Models.ChannelMode.NDI || channel.IntercomGroup <= 0)
                        continue;

                    if (receivedAudio.TryGetValue(channel.ChannelNumber, out var audioBytes) && audioBytes != null && audioBytes.Length > 0)
                    {
                        AudioRingBuffer ringBuffer;
                        lock (_ringBufferManagementLock)
                        {
                            // Ensure ring buffer exists for this channel
                            if (!_crossModeRingBuffers.TryGetValue(channel.ChannelNumber, out ringBuffer))
                            {
                                ringBuffer = new AudioRingBuffer(RING_BUFFER_SAMPLES_400MS);
                                _crossModeRingBuffers[channel.ChannelNumber] = ringBuffer;
                            }
                        }

                        // Write audio bytes to ring buffer (AudioRingBuffer has internal locking)
                        // audioBytes is already mono float32 from the NDI receive pipeline
                        ringBuffer.Write(audioBytes);
                    }
                }
            }
            catch (Exception)
            {
                // Silently continue - don't disrupt NDI thread
            }
        }

        /// <summary>
        /// ASIO callback - receives audio from ASIO input channels AND writes to output channels
        /// LOCK-FREE using double buffering with atomic swap
        /// Zero-allocation using ArrayPool
        /// </summary>
        public void ProcessAsioInput(AsioAudioAvailableEventArgs e)
        {
            float[]? interleavedSamples = null;
            float[]? channelSamples = null;
            byte[]? audioBytes = null;

            try
            {
                int samplesPerBuffer = e.SamplesPerBuffer;

                // ===== PROCESS INPUTS =====
                if (e.InputBuffers != null && e.InputBuffers.Length > 0)
                {
                    int channelCount = e.InputBuffers.Length;
                    int totalSamples = channelCount * samplesPerBuffer;

                    // Rent buffer from pool instead of allocating
                    interleavedSamples = _floatPool.Rent(totalSamples);
                    e.GetAsInterleavedSamples(interleavedSamples);

                    // Rent buffers for channel processing
                    channelSamples = _floatPool.Rent(samplesPerBuffer);
                    audioBytes = _bytePool.Rent(samplesPerBuffer * sizeof(float));

                    // LOCK-FREE: Determine which buffer to write to (opposite of current read buffer)
                    int currentReadIndex = _currentReadBufferIndex; // Snapshot current state
                    int writeIndex = 1 - currentReadIndex; // Write to the OTHER buffer

                    // Select write buffer set based on index
                    var writeBuffers = writeIndex == 0 ? _asioInputBuffers0 : _asioInputBuffers1;

                    // De-interleave and store per-channel buffers WITHOUT LOCK
                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        // Create ring buffer if it doesn't exist (400ms @ 48kHz)
                        // Ring buffers need brief lock for dictionary access (not audio data)
                        if (!_asioInputRingBuffers.ContainsKey(ch))
                        {
                            lock (_ringBufferManagementLock)
                            {
                                if (!_asioInputRingBuffers.ContainsKey(ch))
                                {
                                    _asioInputRingBuffers[ch] = new AudioRingBuffer(RING_BUFFER_SAMPLES_400MS);
                                }
                            }
                        }

                        // Extract this channel's samples
                        for (int i = 0; i < samplesPerBuffer; i++)
                        {
                            channelSamples[i] = interleavedSamples[i * channelCount + ch];
                        }

                        // Convert to bytes and write to ring buffer (AudioRingBuffer has internal locking)
                        // IMPORTANT: pass exact byte count because audioBytes from ArrayPool may be oversized
                        int exactByteCount = samplesPerBuffer * sizeof(float);
                        Buffer.BlockCopy(channelSamples, 0, audioBytes, 0, exactByteCount);
                        _asioInputRingBuffers[ch].Write(audioBytes, exactByteCount);

                        // Store in write buffer for N-1 mixing (used in PrepareAsioChannelAudio)
                        // This write is LOCK-FREE because Read is reading from the OTHER buffer
                        if (!writeBuffers.ContainsKey(ch))
                        {
                            writeBuffers[ch] = new float[samplesPerBuffer];
                        }
                        Array.Copy(channelSamples, writeBuffers[ch], samplesPerBuffer);
                    }

                    // ATOMIC SWAP: Make the newly written buffer available for reading
                    // This is a single atomic operation - no lock needed!
                    // Memory barrier ensures all writes above are visible before swap
                    System.Threading.Thread.MemoryBarrier();
                    System.Threading.Interlocked.Exchange(ref _currentReadBufferIndex, writeIndex);
                }

                // NOTE: ASIO output is handled in the Read() method, not here
                // NAudio ignores e.WrittenToOutputBuffers and always calls Read()
            }
            catch (Exception)
            {
                // Silently continue
            }
            finally
            {
                // CRITICAL: Return rented buffers to pool
                if (interleavedSamples != null)
                    _floatPool.Return(interleavedSamples, clearArray: false);
                if (channelSamples != null)
                    _floatPool.Return(channelSamples, clearArray: false);
                if (audioBytes != null)
                    _bytePool.Return(audioBytes, clearArray: false);
            }
        }

        /// <summary>
        /// IWaveProvider.Read - Called by ASIO for output data
        /// Implements N-1 routing for ASIO groups
        /// LOCK-FREE using double buffering
        /// Zero-allocation using ArrayPool
        /// </summary>
        public int Read(byte[] buffer, int offset, int count)
        {
            float[]? outputSamples = null;

            // Track every buffer we rent from the pool (cross-mode NDI snapshots and
            // per-channel mixes) so the finally block can return them ALL — including
            // rented arrays whose Length matches samplesNeeded exactly, which the previous
            // implementation silently leaked into the GC heap (~1 leak per ASIO callback
            // per active channel, accumulating GC pressure over 24/7 uptime).
            var rentedToReturn = _rentedScratch;
            rentedToReturn.Clear();

            try
            {
                // Clear buffer (silence by default)
                Array.Clear(buffer, offset, count);

                // Calculate samples needed
                int bytesPerSample = 4; // Float32
                int samplesNeeded = count / (bytesPerSample * _outputChannelCount);

                // Rent output buffer from pool (interleaved multi-channel)
                int totalSamples = samplesNeeded * _outputChannelCount;
                outputSamples = _floatPool.Rent(totalSamples);
                Array.Clear(outputSamples, 0, totalSamples);

                // LOCK-FREE: Snapshot current read buffer index at the START
                // This ensures we read from a consistent buffer throughout this call.
                int readIndex = _currentReadBufferIndex;
                var liveReadBuffers = readIndex == 0 ? _asioInputBuffers0 : _asioInputBuffers1;

                // Defensive ownership snapshot: copy each live buffer into a pool-rented
                // private array BEFORE any per-channel processing reads from it. NAudio
                // currently serializes ProcessAsioInput and Read on the same callback, but
                // a future framework change (or a third-party host) might overlap them; in
                // that case the original "lock-free" design would let ProcessAsioInput
                // overwrite a buffer mid-Read. The snapshot makes the read window
                // self-contained at the cost of one short Buffer.BlockCopy per channel.
                var readBuffers = _readBuffersSnapshotScratch;
                readBuffers.Clear();
                foreach (var kvp in liveReadBuffers)
                {
                    int len = kvp.Value.Length;
                    float[] copy = _floatPool.Rent(len);
                    rentedToReturn.Add(copy);
                    Buffer.BlockCopy(kvp.Value, 0, copy, 0, len * sizeof(float));
                    readBuffers[kvp.Key] = copy;
                }

                // CRITICAL: Read NDI cross-mode audio from ring buffers ONCE per ASIO callback.
                // Each NDI channel's ring buffer must be read exactly once, then shared across
                // all ASIO channels in the group. Without this, N ASIO channels would each consume
                // from the same ring buffer, playing NDI audio at Nx speed (pitch shift!).
                // Reuse the per-call snapshot dictionary to avoid an allocation per ASIO callback.
                var crossModeSnapshot = _crossModeSnapshotScratch;
                crossModeSnapshot.Clear();
                foreach (var ch in _channels)
                {
                    if (ch.Mode != Models.ChannelMode.NDI || ch.IntercomGroup <= 0)
                        continue;

                    AudioRingBuffer ringBuffer = null;
                    lock (_ringBufferManagementLock)
                    {
                        _crossModeRingBuffers.TryGetValue(ch.ChannelNumber, out ringBuffer);
                    }

                    if (ringBuffer != null)
                    {
                        // Allocation-free read into a per-channel scratch byte[]; the scratch
                        // is sized once and reused across ASIO callbacks.
                        int ndiByteCount = samplesNeeded * sizeof(float);
                        if (!_crossModeRingReadScratch.TryGetValue(ch.ChannelNumber, out var ndiScratch)
                            || ndiScratch.Length < ndiByteCount)
                        {
                            ndiScratch = new byte[ndiByteCount];
                            _crossModeRingReadScratch[ch.ChannelNumber] = ndiScratch;
                        }

                        if (ringBuffer.TryRead(ndiScratch, samplesNeeded))
                        {
                            float[] ndiFloats = _floatPool.Rent(samplesNeeded);
                            rentedToReturn.Add(ndiFloats);
                            Buffer.BlockCopy(ndiScratch, 0, ndiFloats, 0, ndiByteCount);
                            crossModeSnapshot[ch.ChannelNumber] = ndiFloats;
                        }
                    }
                }

                // Process each ASIO channel WITHOUT LOCK
                foreach (var channel in _channels)
                {
                    if (channel.Mode != Models.ChannelMode.ASIO)
                        continue;

                    int outputCh = channel.AsioOutputChannel;

                    if (outputCh < 0 || outputCh >= _outputChannelCount)
                        continue;

                    // Prepare audio for this channel with N-1 logic (LOCK-FREE).
                    // The returned buffer is ALWAYS rented from _floatPool (PrepareAsioChannelAudio
                    // never returns a caller-owned reference), so we can return it unconditionally.
                    float[] channelAudio = PrepareAsioChannelAudio(channel, samplesNeeded, readBuffers, crossModeSnapshot);

                    if (channelAudio != null)
                    {
                        rentedToReturn.Add(channelAudio);

                        if (channelAudio.Length > 0)
                        {
                            // Interleave into output buffer
                            int copyCount = Math.Min(samplesNeeded, channelAudio.Length);
                            for (int i = 0; i < copyCount; i++)
                            {
                                int pos = i * _outputChannelCount + outputCh;
                                outputSamples[pos] += channelAudio[i];
                            }
                        }
                    }
                }

                // Convert float[] to byte[] and copy to output buffer
                Buffer.BlockCopy(outputSamples, 0, buffer, offset, Math.Min(count, totalSamples * bytesPerSample));

                return count;
            }
            catch (Exception)
            {
                return count; // Return silence
            }
            finally
            {
                // CRITICAL: Return ALL rented buffers to pool, regardless of Length.
                // Any throw above also falls through here so we never leak a rented array.
                if (outputSamples != null)
                    _floatPool.Return(outputSamples, clearArray: false);
                for (int i = 0; i < rentedToReturn.Count; i++)
                {
                    _floatPool.Return(rentedToReturn[i], clearArray: false);
                }
                rentedToReturn.Clear();
            }
        }

        // Per-call scratch lists for Read() — reused to avoid a per-callback List/Dictionary
        // allocation. Read() is single-threaded (NAudio invokes IWaveProvider.Read serially
        // from its output thread), so no synchronization is required here.
        private readonly List<float[]> _rentedScratch = new List<float[]>(32);
        private readonly Dictionary<int, float[]> _crossModeSnapshotScratch = new Dictionary<int, float[]>(16);
        private readonly Dictionary<int, float[]> _readBuffersSnapshotScratch = new Dictionary<int, float[]>(16);

        // Per-channel byte[] scratches reused across ring-buffer reads to avoid per-frame
        // allocations from AudioRingBuffer.Read. Keyed by ASIO output channel for the
        // microphone path and by intercom channel number for the cross-mode (NDI) path.
        private readonly Dictionary<int, byte[]> _micRingReadScratch = new Dictionary<int, byte[]>(16);
        private readonly Dictionary<int, byte[]> _crossModeRingReadScratch = new Dictionary<int, byte[]>(16);

        /// <summary>
        /// Prepare audio for a specific ASIO channel with N-1 logic
        /// LOCK-FREE: Uses buffer snapshot passed from caller
        /// Zero-allocation using ArrayPool
        /// </summary>
        private float[] PrepareAsioChannelAudio(ChannelState channel, int samplesNeeded, Dictionary<int, float[]> readBuffers, Dictionary<int, float[]> crossModeSnapshot)
        {
            float[]? micAudio = null;
            float[]? n1MixAudio = null;
            float[]? otherAudioCopy = null;
            bool micAudioFromPool = false;
            bool n1MixAudioFromPool = false;

            try
            {
                // 1. TALK: Send microphone audio from ring buffer.
                if (channel.TalkEnabled)
                {
                    int outputCh = channel.AsioOutputChannel;
                    if (_microphoneRingBuffers.TryGetValue(outputCh, out var micRing))
                    {
                        // Allocation-free read: reuse a per-channel scratch instead of
                        // letting AudioRingBuffer.Read allocate a fresh byte[] each call.
                        int byteCount = samplesNeeded * sizeof(float);
                        if (!_micRingReadScratch.TryGetValue(outputCh, out var scratch) || scratch.Length < byteCount)
                        {
                            scratch = new byte[byteCount];
                            _micRingReadScratch[outputCh] = scratch;
                        }

                        if (micRing.TryRead(scratch, samplesNeeded))
                        {
                            micAudio = _floatPool.Rent(samplesNeeded);
                            micAudioFromPool = true;
                            Buffer.BlockCopy(scratch, 0, micAudio, 0, byteCount);
                            ApplyGain(micAudio, channel.OutputLevel / 100.0f, samplesNeeded);
                        }
                        // Underrun → send silence (we simply don't allocate micAudio).
                    }
                }

                // 2. N-1 Mix: Audio from ALL channels in the same unified intercom group
                // Includes both ASIO channels (from readBuffers) and NDI channels (from _crossModeAudio)
                // LOCK-FREE: Uses buffer snapshots
                if (channel.IntercomGroup > 0)
                {
                    // Avoid LINQ allocations - use foreach with manual filtering
                    foreach (var otherChannel in _channels)
                    {
                        // N-1: exclude self and filter by unified group
                        if (otherChannel.IntercomGroup != channel.IntercomGroup ||
                            otherChannel.ChannelNumber == channel.ChannelNumber)
                            continue;

                        float[] otherAudio = null;
                        int otherAudioLength = 0;

                        if (otherChannel.Mode == Models.ChannelMode.ASIO)
                        {
                            // Get audio from ASIO input buffer (LOCK-FREE read from snapshot)
                            if (readBuffers.TryGetValue(otherChannel.AsioInputChannel, out var asioAudio))
                            {
                                otherAudio = asioAudio;
                                otherAudioLength = asioAudio.Length;
                            }
                        }
                        else if (otherChannel.Mode == Models.ChannelMode.NDI)
                        {
                            // Get audio from pre-read cross-mode snapshot (read once in Read(), shared across channels)
                            // This prevents multiple ASIO channels from consuming the same ring buffer independently,
                            // which would cause Nx speed playback (pitch shift) with N ASIO channels in the group
                            if (crossModeSnapshot.TryGetValue(otherChannel.ChannelNumber, out var ndiAudio))
                            {
                                otherAudio = ndiAudio;
                                otherAudioLength = ndiAudio.Length;
                            }
                        }

                        if (otherAudio != null && otherAudioLength > 0)
                        {
                            // Rent temporary buffer for copy (don't modify the source!)
                            otherAudioCopy = _floatPool.Rent(otherAudioLength);
                            Array.Copy(otherAudio, otherAudioCopy, otherAudioLength);

                            // Apply input level
                            ApplyGain(otherAudioCopy, otherChannel.InputLevel / 100.0f, otherAudioLength);

                            if (n1MixAudio == null)
                            {
                                n1MixAudio = otherAudioCopy;
                                n1MixAudioFromPool = true;
                                otherAudioCopy = null; // Transfer ownership
                            }
                            else
                            {
                                MixAudio(n1MixAudio, otherAudioCopy, Math.Min(n1MixAudio.Length, otherAudioLength));
                                _floatPool.Return(otherAudioCopy, clearArray: false);
                                otherAudioCopy = null;
                            }
                        }
                    }
                }

                // 3. Combine microphone and N-1 mix
                float[] finalMix;

                if (micAudio != null && n1MixAudio != null)
                {
                    MixAudio(micAudio, n1MixAudio, samplesNeeded);
                    finalMix = micAudio;
                    // Return n1Mix to pool since we used micAudio as the result
                    if (n1MixAudioFromPool && n1MixAudio != null)
                    {
                        _floatPool.Return(n1MixAudio, clearArray: false);
                        n1MixAudioFromPool = false;
                        n1MixAudio = null;
                    }
                    micAudioFromPool = false; // Caller will return this
                    return finalMix;
                }
                else if (micAudio != null)
                {
                    micAudioFromPool = false; // Caller will return this
                    return micAudio;
                }
                else if (n1MixAudio != null)
                {
                    n1MixAudioFromPool = false; // Caller will return this
                    return n1MixAudio;
                }

                // Return silence (rent a buffer for it)
                finalMix = _floatPool.Rent(samplesNeeded);
                Array.Clear(finalMix, 0, samplesNeeded);
                return finalMix;
            }
            finally
            {
                // Clean up any buffers we still own
                if (micAudioFromPool && micAudio != null)
                    _floatPool.Return(micAudio, clearArray: false);
                if (n1MixAudioFromPool && n1MixAudio != null)
                    _floatPool.Return(n1MixAudio, clearArray: false);
                if (otherAudioCopy != null)
                    _floatPool.Return(otherAudioCopy, clearArray: false);
            }
        }

        /// <summary>
        /// Get audio received from a specific ASIO input channel (for LISTEN functionality).
        /// COMPLETELY LOCK-FREE: reads from ring buffer (which has internal locking).
        /// Returns the cached frame if the ring buffer underruns (graceful fallback).
        ///
        /// Allocation-free fast path: <see cref="_lastAsioInputCache"/> doubles as a per-channel
        /// scratch buffer reused across calls. Only the first call per channel allocates the
        /// scratch; subsequent calls write into the same byte[] via <see cref="AudioRingBuffer.TryRead"/>.
        /// </summary>
        public byte[] GetAsioInputAudio(int inputChannel, int samplesNeeded = NDI_FRAME_SAMPLES)
        {
            int byteCount = samplesNeeded * sizeof(float);

            if (_asioInputRingBuffers.TryGetValue(inputChannel, out var ringBuffer))
            {
                // Get-or-create the per-channel scratch. Allocates only the first time, or
                // if samplesNeeded grew beyond the existing buffer size.
                if (!_lastAsioInputCache.TryGetValue(inputChannel, out var scratch) || scratch.Length < byteCount)
                {
                    scratch = new byte[byteCount];
                    _lastAsioInputCache[inputChannel] = scratch;
                }

                if (ringBuffer.TryRead(scratch, samplesNeeded))
                {
                    return scratch; // fresh data
                }

                // Underrun: scratch still holds the last successful frame (TryRead leaves
                // the destination untouched on failure). Returning it gives the caller a
                // brief audible repeat instead of silence — same UX as before.
                return scratch;
            }

            // Channel not found — return cached frame if any was ever produced.
            _lastAsioInputCache.TryGetValue(inputChannel, out var fallback);
            return fallback;
        }

        private void ApplyGain(float[] audio, float gain, int length)
        {
            for (int i = 0; i < length; i++)
            {
                audio[i] *= gain;
            }
        }

        private void MixAudio(float[] destination, float[] source, int length)
        {
            for (int i = 0; i < length; i++)
            {
                destination[i] += source[i];

                // Simple clipping prevention
                if (destination[i] > 1.0f) destination[i] = 1.0f;
                if (destination[i] < -1.0f) destination[i] = -1.0f;
            }
        }
    }
}
