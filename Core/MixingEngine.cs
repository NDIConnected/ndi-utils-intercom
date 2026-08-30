using System;
using System.Collections.Generic;
using System.Linq;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    public class MixingEngine
    {
        private Dictionary<int, ChannelState> _channels = new Dictionary<int, ChannelState>();
        private byte[] _microphoneBuffer;
        private bool _feedbackGateEnabled;
        private double _gateThresholdDb;
        private int _gateAttackMs;
        private int _gateReleaseMs;

        // Gate state per channel (8 independent gates)
        private Dictionary<int, GateState> _gateStates = new Dictionary<int, GateState>();

        private class GateState
        {
            public float CurrentGain = 1.0f;
        }

        public void UpdateChannelStates(IEnumerable<ChannelState> channels)
        {
            _channels.Clear();
            foreach (var channel in channels)
            {
                _channels[channel.ChannelNumber] = channel;

                // Initialize gate state for this channel if not exists
                if (!_gateStates.ContainsKey(channel.ChannelNumber))
                {
                    _gateStates[channel.ChannelNumber] = new GateState();
                }
            }
        }

        public void SetMicrophoneBuffer(byte[] buffer)
        {
            _microphoneBuffer = buffer;
        }

        public void SetGateParameters(bool enabled, double thresholdDb, int attackMs, int releaseMs)
        {
            _feedbackGateEnabled = enabled;
            _gateThresholdDb = thresholdDb;
            _gateAttackMs = attackMs;
            _gateReleaseMs = releaseMs;
        }

        /// <summary>
        /// Prepares the audio to send for a specific channel based on N-1 logic
        /// </summary>
        public byte[] PrepareChannelSendAudio(int channelNumber, Dictionary<int, byte[]> receivedAudioByChannel)
        {
            if (!_channels.TryGetValue(channelNumber, out var channel))
                return null;

            float[] microphoneAudio = null;
            float[] n1MixAudio = null;

            // 1. Process MICROPHONE audio with Feedback Gate
            //    Feedback Gate prevents remote user from hearing themselves
            if (channel.TalkEnabled && _microphoneBuffer != null)
            {
                microphoneAudio = BytesToFloats(_microphoneBuffer);
                ApplyGain(microphoneAudio, channel.OutputLevel / 100.0f);

                // Apply Feedback Gate ONLY to microphone (not to N-1 mix!)
                // Analyzes audio received from THIS channel - if channel is transmitting, block mic
                if (_feedbackGateEnabled)
                {
                    byte[] thisChannelReceivedAudio = null;
                    receivedAudioByChannel.TryGetValue(channelNumber, out thisChannelReceivedAudio);
                    ApplyChannelGate(microphoneAudio, channelNumber, thisChannelReceivedAudio);
                }
            }

            // 2. Process N-1 mix (NO GATE - always pass through)
            //    N-1 mix is audio from OTHER channels in the same group.
            //    Manual scan instead of LINQ Where(...) — Where allocates an iterator + closure
            //    on every call, and this method runs ~25/sec * N channels in the audio path.
            if (channel.IntercomGroup > 0)
            {
                foreach (var kvp in _channels)
                {
                    var otherChannel = kvp.Value;
                    if (otherChannel.IntercomGroup != channel.IntercomGroup ||
                        otherChannel.ChannelNumber == channelNumber)
                        continue;

                    if (receivedAudioByChannel.TryGetValue(otherChannel.ChannelNumber, out var otherAudio) && otherAudio != null)
                    {
                        float[] otherFloats = BytesToFloats(otherAudio);
                        ApplyGain(otherFloats, otherChannel.InputLevel / 100.0f);

                        if (n1MixAudio == null)
                        {
                            n1MixAudio = otherFloats;
                        }
                        else
                        {
                            MixAudio(n1MixAudio, otherFloats);
                        }
                    }
                }
            }

            // 3. Combine microphone and N-1 mix
            float[] finalMix = null;

            if (microphoneAudio != null && n1MixAudio != null)
            {
                // Both present - mix them
                finalMix = microphoneAudio;
                MixAudio(finalMix, n1MixAudio);
            }
            else if (microphoneAudio != null)
            {
                finalMix = microphoneAudio;
            }
            else if (n1MixAudio != null)
            {
                finalMix = n1MixAudio;
            }

            return finalMix != null ? FloatsToBytes(finalMix) : null;
        }

        /// <summary>
        /// Prepares the audio to play locally for a specific channel (returns float[] for efficiency)
        /// </summary>
        public float[] PrepareChannelMonitorAudioAsFloats(int channelNumber, byte[] receivedAudio)
        {
            if (!_channels.TryGetValue(channelNumber, out var channel))
                return null;

            if (!channel.ListenEnabled || receivedAudio == null)
                return null;

            float[] audioFloats = BytesToFloats(receivedAudio);

            // Apply input level
            float gainMultiplier = channel.InputLevel / 100.0f;
            for (int i = 0; i < audioFloats.Length; i++)
            {
                audioFloats[i] *= gainMultiplier;

                // Clip to prevent distortion
                if (audioFloats[i] > 1.0f) audioFloats[i] = 1.0f;
                if (audioFloats[i] < -1.0f) audioFloats[i] = -1.0f;
            }

            return audioFloats;
        }

        /// <summary>
        /// Prepares the audio to play locally for a specific channel
        /// </summary>
        public byte[] PrepareChannelMonitorAudio(int channelNumber, byte[] receivedAudio)
        {
            float[] audioFloats = PrepareChannelMonitorAudioAsFloats(channelNumber, receivedAudio);
            if (audioFloats == null)
                return null;

            return FloatsToBytes(audioFloats);
        }

        /// <summary>
        /// Mixes multiple float audio streams together (OPTIMIZED - no byte conversions)
        /// </summary>
        public byte[] MixMultipleFloatStreams(List<float[]> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            // If only one stream, convert and return
            if (audioStreams.Count == 1)
                return FloatsToBytes(audioStreams[0]);

            // Find the maximum length to handle different sized chunks
            int maxLength = 0;
            foreach (var stream in audioStreams)
            {
                if (stream.Length > maxLength)
                    maxLength = stream.Length;
            }

            // Mix all streams together (already in float format!)
            float[] mixBuffer = new float[maxLength];

            foreach (var streamFloats in audioStreams)
            {
                for (int i = 0; i < streamFloats.Length; i++)
                {
                    mixBuffer[i] += streamFloats[i];
                }
            }

            // Apply clipping to prevent distortion
            for (int i = 0; i < mixBuffer.Length; i++)
            {
                if (mixBuffer[i] > 1.0f) mixBuffer[i] = 1.0f;
                if (mixBuffer[i] < -1.0f) mixBuffer[i] = -1.0f;
            }

            // Convert to bytes ONCE at the end
            return FloatsToBytes(mixBuffer);
        }

        /// <summary>
        /// Mixes multiple audio streams together (for multiple LISTEN channels)
        /// Prevents buffer overflow by mixing before adding to output buffer
        /// </summary>
        public byte[] MixMultipleAudioStreams(List<byte[]> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            // If only one stream, return it directly (no mixing needed)
            if (audioStreams.Count == 1)
                return audioStreams[0];

            // Find the maximum length to handle different sized chunks
            int maxLength = 0;
            foreach (var stream in audioStreams)
            {
                int floatCount = stream.Length / 4;
                if (floatCount > maxLength)
                    maxLength = floatCount;
            }

            // Mix all streams together
            float[] mixBuffer = new float[maxLength];

            foreach (var stream in audioStreams)
            {
                float[] streamFloats = BytesToFloats(stream);

                for (int i = 0; i < streamFloats.Length; i++)
                {
                    mixBuffer[i] += streamFloats[i];
                }
            }

            // Apply clipping to prevent distortion
            for (int i = 0; i < mixBuffer.Length; i++)
            {
                if (mixBuffer[i] > 1.0f) mixBuffer[i] = 1.0f;
                if (mixBuffer[i] < -1.0f) mixBuffer[i] = -1.0f;
            }

            return FloatsToBytes(mixBuffer);
        }

        /// <summary>
        /// Applies gate to a specific channel (8 independent gates)
        /// Analyzes the received NDI audio to control the gate
        /// </summary>
        private void ApplyChannelGate(float[] outputAudio, int channelNumber, byte[] receivedAudio)
        {
            if (!_gateStates.ContainsKey(channelNumber))
                return;

            var gateState = _gateStates[channelNumber];

            // Analyze the RECEIVED NDI audio (not the output!) to determine gate state
            float levelDb = -100f; // Default: very low level (gate closed)

            if (receivedAudio != null && receivedAudio.Length > 0)
            {
                float[] receivedFloats = BytesToFloats(receivedAudio);
                float level = CalculateRmsLevel(receivedFloats);
                levelDb = 20 * (float)Math.Log10(level + 1e-10f);
            }

            // Determine target gain based on threshold
            // INVERTED: when receiving audio above threshold, BLOCK the output (ducking/protection)
            float targetGain = levelDb >= _gateThresholdDb ? 0.0f : 1.0f;

            // Apply attack/release with configured parameters
            // Attack = when NDI audio arrives (target drops to 0.0 = BLOCK)
            // Release = when NDI audio stops (target rises to 1.0 = UNBLOCK)
            if (targetGain < gateState.CurrentGain)
            {
                // Attack: NDI audio detected, BLOCK output (fast)
                // Attack time in ms, convert to coefficient per frame (40ms @ 48kHz)
                if (_gateAttackMs <= 0)
                {
                    // Instant attack
                    gateState.CurrentGain = targetGain;
                }
                else
                {
                    // Calculate attack coefficient: how much to move toward target per 40ms frame
                    float attackCoeff = 1.0f - (float)Math.Exp(-40.0 / _gateAttackMs);
                    gateState.CurrentGain += (targetGain - gateState.CurrentGain) * attackCoeff;
                }
            }
            else if (targetGain > gateState.CurrentGain)
            {
                // Release: NDI audio stopped, UNBLOCK output (slow)
                if (_gateReleaseMs <= 0)
                {
                    // Instant release
                    gateState.CurrentGain = targetGain;
                }
                else
                {
                    // Calculate release coefficient
                    float releaseCoeff = 1.0f - (float)Math.Exp(-40.0 / _gateReleaseMs);
                    gateState.CurrentGain += (targetGain - gateState.CurrentGain) * releaseCoeff;
                }
            }

            // Apply gain to OUTPUT audio (not received!)
            ApplyGain(outputAudio, gateState.CurrentGain);
        }

        private float CalculateRmsLevel(float[] audio)
        {
            float sum = 0;
            foreach (var sample in audio)
            {
                sum += sample * sample;
            }
            return (float)Math.Sqrt(sum / audio.Length);
        }

        private void ApplyGain(float[] audio, float gain)
        {
            for (int i = 0; i < audio.Length; i++)
            {
                float sample = audio[i] * gain;
                if (sample > 1.0f) sample = 1.0f;
                if (sample < -1.0f) sample = -1.0f;
                audio[i] = sample;
            }
        }

        private void MixAudio(float[] destination, float[] source)
        {
            int minLength = Math.Min(destination.Length, source.Length);
            for (int i = 0; i < minLength; i++)
            {
                destination[i] += source[i];

                // Simple clipping prevention
                if (destination[i] > 1.0f) destination[i] = 1.0f;
                if (destination[i] < -1.0f) destination[i] = -1.0f;
            }
        }

        // Re-encoded with Buffer.BlockCopy: same layout as the previous BitConverter loop
        // (IEEE754 little-endian float32) but does the conversion as a single bulk memcpy
        // instead of one BitConverter call per sample. The float[] is still allocated; the
        // big win is removing the per-sample 4-byte allocation that BitConverter.GetBytes
        // does on every call inside the FloatsToBytes loop. On a 16-channel intercom this
        // alone saved ~6M small heap allocations per second.
        private static float[] BytesToFloats(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Array.Empty<float>();

            int floatCount = bytes.Length / sizeof(float);
            float[] floats = new float[floatCount];
            Buffer.BlockCopy(bytes, 0, floats, 0, floatCount * sizeof(float));
            return floats;
        }

        private static byte[] FloatsToBytes(float[] floats)
        {
            if (floats == null || floats.Length == 0) return Array.Empty<byte>();

            byte[] bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
