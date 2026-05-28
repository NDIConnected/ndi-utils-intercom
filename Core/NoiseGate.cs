using System;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Professional expander/downward gate with smooth gain reduction instead of hard on/off
    /// </summary>
    public class NoiseGate
    {
        private float _currentGain = 1.0f;
        private readonly float _sampleRate;
        private float _currentLevelDb = -80.0f;       // Current RMS level in dB
        private int _blockSize = 480;                 // 10ms blocks @ 48kHz
        private int _blockCounter = 0;                // Sample counter for block processing
        private float _blockRmsSum = 0.0f;            // Sum for RMS calculation in current block

        // Parameters
        private float _thresholdDb = -40.0f;          // Threshold for opening
        private float _hysteresisDb = 6.0f;           // Hysteresis (difference between open/close)
        private float _attackCoefficient = 0.0f;      // Attack smoothing
        private float _releaseCoefficient = 0.0f;     // Release smoothing

        // Hold time
        private int _holdSamples = 0;                 // Hold time in samples
        private int _holdCounter = 0;                 // Current hold counter

        // Gate state
        private bool _gateOpen = false;               // Current gate state

        public bool Enabled { get; set; } = false;

        public NoiseGate(float sampleRate = 48000)
        {
            _sampleRate = sampleRate;
            _blockSize = (int)(sampleRate * 0.01f);   // 10ms blocks
            SetAttackTime(5);    // Default 5ms attack (fast)
            SetReleaseTime(150); // Default 150ms release (smooth)
        }

        /// <summary>
        /// Set threshold in dB (e.g., -40 dB)
        /// </summary>
        public void SetThreshold(float thresholdDb)
        {
            _thresholdDb = thresholdDb;
        }

        /// <summary>
        /// Set attack time in milliseconds
        /// </summary>
        public void SetAttackTime(float attackMs)
        {
            // Calculate coefficient for exponential smoothing
            // Smaller values = faster attack
            if (attackMs <= 0)
            {
                _attackCoefficient = 0.0f; // Instant attack
            }
            else
            {
                float attackSamples = (attackMs / 1000.0f) * _sampleRate;
                _attackCoefficient = (float)Math.Exp(-1.0f / attackSamples);
            }
        }

        /// <summary>
        /// Set release time in milliseconds
        /// </summary>
        public void SetReleaseTime(float releaseMs)
        {
            // Calculate coefficient for exponential smoothing
            float releaseSamples = (releaseMs / 1000.0f) * _sampleRate;
            _releaseCoefficient = (float)Math.Exp(-1.0f / releaseSamples);
        }

        /// <summary>
        /// Set hysteresis in dB (difference between open and close thresholds)
        /// </summary>
        public void SetHysteresis(float hysteresisDb)
        {
            _hysteresisDb = hysteresisDb;
        }

        /// <summary>
        /// Set hold time in milliseconds (gate stays open after signal drops)
        /// </summary>
        public void SetHoldTime(float holdMs)
        {
            _holdSamples = (int)((holdMs / 1000.0f) * _sampleRate);
        }

        /// <summary>
        /// Process audio buffer (32-bit float samples) using block-based detection with smooth expander
        /// </summary>
        public void Process(byte[] buffer, int length)
        {
            if (!Enabled || buffer == null || length == 0)
                return;

            for (int i = 0; i < length; i += 4)
            {
                // Read sample
                float sample = BitConverter.ToSingle(buffer, i);

                // Accumulate RMS for block processing
                _blockRmsSum += sample * sample;
                _blockCounter++;

                // Process block when full
                if (_blockCounter >= _blockSize)
                {
                    // Calculate RMS level for this block
                    float blockRms = (float)Math.Sqrt(_blockRmsSum / _blockCounter);
                    float rawLevelDb = 20.0f * (float)Math.Log10(blockRms + 1e-10f);
                    _currentLevelDb = rawLevelDb;

                    // Reset block
                    _blockRmsSum = 0.0f;
                    _blockCounter = 0;
                }

                // Calculate target gain using hard gate with hysteresis and hold time
                float targetGain;

                // Update gate state based on level and hysteresis
                if (!_gateOpen)
                {
                    // Gate is closed - check if should open
                    if (_currentLevelDb >= _thresholdDb)
                    {
                        _gateOpen = true;
                        _holdCounter = _holdSamples; // Reset hold timer
                    }
                }
                else
                {
                    // Gate is open - check if should close
                    float closeThreshold = _thresholdDb - _hysteresisDb;

                    // Decrement hold counter
                    if (_holdCounter > 0)
                        _holdCounter--;

                    // Close only if below close threshold AND hold time expired
                    if (_currentLevelDb < closeThreshold && _holdCounter <= 0)
                    {
                        _gateOpen = false;
                    }
                }

                // Set target gain based on gate state
                targetGain = _gateOpen ? 1.0f : 0.0f;

                // Smooth gain transition (attack/release envelope)
                float oldGain = _currentGain;
                if (targetGain > _currentGain)
                {
                    // Attack (gate opening)
                    _currentGain = targetGain + _attackCoefficient * (_currentGain - targetGain);
                }
                else
                {
                    // Release (gate closing)
                    _currentGain = targetGain + _releaseCoefficient * (_currentGain - targetGain);
                }

                // Apply gain to sample
                float processedSample = sample * _currentGain;

                // Write back to buffer
                BitConverter.GetBytes(processedSample).CopyTo(buffer, i);
            }
        }

        /// <summary>
        /// Get current gain envelope (0.0 to 1.0) for visualization
        /// </summary>
        public float CurrentGain => _currentGain;

        /// <summary>
        /// Reset gate state
        /// </summary>
        public void Reset()
        {
            _currentGain = 1.0f;
            _currentLevelDb = -80.0f;
            _blockCounter = 0;
            _blockRmsSum = 0.0f;
            _gateOpen = false;
            _holdCounter = 0;
        }
    }
}
