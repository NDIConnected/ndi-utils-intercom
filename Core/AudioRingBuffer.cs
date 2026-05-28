using System;
using System.Buffers;
using System.Threading;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Thread-safe circular buffer for audio samples.
    /// Decouples variable-rate audio capture from fixed-rate NDI transmission.
    /// Uses ArrayPool to eliminate allocations in audio path.
    /// </summary>
    public class AudioRingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private int _writePosition;
        private int _readPosition;
        private int _availableSamples;
        private readonly object _lock = new object();

        public AudioRingBuffer(int capacityInSamples)
        {
            _capacity = capacityInSamples;
            _buffer = new float[capacityInSamples];
            _writePosition = 0;
            _readPosition = 0;
            _availableSamples = 0;
        }

        /// <summary>
        /// Write audio samples to the ring buffer (called by audio capture thread).
        /// Zero-allocation using reusable buffer.
        /// </summary>
        public void Write(byte[] audioData)
        {
            Write(audioData, audioData?.Length ?? 0);
        }

        /// <summary>
        /// Write audio samples to the ring buffer with explicit byte count.
        /// Use this when the audioData buffer is larger than the valid data (e.g. from ArrayPool).
        /// Truly zero-alloc on the hot path: a single (or two, on wrap) Buffer.BlockCopy
        /// reinterprets the byte[] as float[] directly into the ring buffer.
        /// </summary>
        public void Write(byte[] audioData, int byteCount)
        {
            if (audioData == null || byteCount <= 0)
                return;

            int sampleCount = byteCount / sizeof(float);
            if (sampleCount <= 0)
                return;

            lock (_lock)
            {
                // Clamp to capacity. If the caller writes more samples than the buffer holds
                // we keep only the most recent _capacity samples (matches the original
                // "drop oldest" behavior). This avoids a needless multi-wrap copy.
                int srcByteOffset = 0;
                if (sampleCount > _capacity)
                {
                    srcByteOffset = (sampleCount - _capacity) * sizeof(float);
                    sampleCount = _capacity;
                    byteCount = _capacity * sizeof(float);
                }

                // Bulk copy with wrap-around — at most two memcpy operations total.
                if (_writePosition + sampleCount <= _capacity)
                {
                    Buffer.BlockCopy(audioData, srcByteOffset,
                                     _buffer, _writePosition * sizeof(float),
                                     sampleCount * sizeof(float));
                }
                else
                {
                    int head = _capacity - _writePosition;
                    Buffer.BlockCopy(audioData, srcByteOffset,
                                     _buffer, _writePosition * sizeof(float),
                                     head * sizeof(float));
                    Buffer.BlockCopy(audioData, srcByteOffset + head * sizeof(float),
                                     _buffer, 0,
                                     (sampleCount - head) * sizeof(float));
                }

                _writePosition = (_writePosition + sampleCount) % _capacity;

                // Update availability + advance read position if we just overran (drop oldest).
                int newAvail = _availableSamples + sampleCount;
                if (newAvail > _capacity)
                {
                    int dropped = newAvail - _capacity;
                    _readPosition = (_readPosition + dropped) % _capacity;
                    _availableSamples = _capacity;
                }
                else
                {
                    _availableSamples = newAvail;
                }
            }
        }

        /// <summary>
        /// Read exactly 'sampleCount' samples from the ring buffer (called by NDI thread).
        /// Returns null if not enough samples available.
        /// The result array IS allocated each call (callers expect to own it). For an
        /// allocation-free fast path, use <see cref="TryRead"/> with a caller-owned buffer.
        /// </summary>
        public byte[] Read(int sampleCount)
        {
            int byteCount = sampleCount * sizeof(float);
            byte[] result = new byte[byteCount];
            return TryRead(result, sampleCount) ? result : null;
        }

        /// <summary>
        /// Allocation-free read: copies <paramref name="sampleCount"/> float samples (as bytes)
        /// into the caller-owned <paramref name="destination"/> buffer. Returns true on
        /// success, false on underrun (destination is left untouched in that case so callers
        /// can keep using their previous frame as a graceful fallback).
        /// On a 16-channel intercom this is the difference between 0 byte[] allocations per
        /// audio frame and 16+ allocations of ~7.6 KB each per frame.
        /// </summary>
        public bool TryRead(byte[] destination, int sampleCount)
        {
            if (destination == null) return false;
            int byteCount = sampleCount * sizeof(float);
            if (destination.Length < byteCount) return false;

            lock (_lock)
            {
                if (_availableSamples < sampleCount)
                    return false;

                // Two-pass blockcopy that handles wrap-around without an intermediate
                // float[] copy. _buffer is float[]; destination is byte[]; both are
                // blittable so Buffer.BlockCopy reinterprets the float bits directly.
                if (_readPosition + sampleCount <= _capacity)
                {
                    Buffer.BlockCopy(_buffer, _readPosition * sizeof(float),
                                     destination, 0, byteCount);
                }
                else
                {
                    int tail = _capacity - _readPosition;
                    Buffer.BlockCopy(_buffer, _readPosition * sizeof(float),
                                     destination, 0, tail * sizeof(float));
                    Buffer.BlockCopy(_buffer, 0,
                                     destination, tail * sizeof(float),
                                     (sampleCount - tail) * sizeof(float));
                }

                _readPosition = (_readPosition + sampleCount) % _capacity;
                _availableSamples -= sampleCount;
                return true;
            }
        }

        /// <summary>
        /// Get the number of samples currently available in the buffer.
        /// </summary>
        public int AvailableSamples
        {
            get
            {
                lock (_lock)
                {
                    return _availableSamples;
                }
            }
        }

        /// <summary>
        /// Clear the buffer (reset to empty state).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _writePosition = 0;
                _readPosition = 0;
                _availableSamples = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }

        /// <summary>
        /// Get buffer fill percentage (0.0 to 1.0).
        /// </summary>
        public float FillPercentage
        {
            get
            {
                lock (_lock)
                {
                    return (float)_availableSamples / _capacity;
                }
            }
        }
    }
}
