using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Manages ASIO device initialization, channel discovery, and audio callbacks
    /// </summary>
    public class AsioManager : IDisposable
    {
        private AsioOut _asioDriver;
        private string _selectedDeviceName;
        private int _inputChannelCount;
        private int _outputChannelCount;
        private const int SAMPLE_RATE = 48000;

        // Events for audio data
        public event EventHandler<AsioAudioAvailableEventArgs> AudioAvailable;

        public bool IsInitialized => _asioDriver != null;
        public int InputChannelCount => _inputChannelCount;
        public int OutputChannelCount => _outputChannelCount;
        public string SelectedDevice => _selectedDeviceName;

        /// <summary>
        /// Get list of available ASIO drivers
        /// </summary>
        public List<string> GetAvailableDevices()
        {
            try
            {
                var drivers = AsioOut.GetDriverNames();
                return drivers?.ToList() ?? new List<string>();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Initialize ASIO device and scan channels
        /// ASIO requires STA thread - this method handles threading automatically
        /// </summary>
        // Last initialization error (for diagnostics)
        public string LastError { get; private set; }

        public bool Initialize(string deviceName)
        {
            bool success = false;
            LastError = null;

            // ASIO requires STA thread - create dedicated STA thread for initialization
            var staThread = new System.Threading.Thread(() =>
            {
                try
                {
                    // Dispose existing driver if any
                    if (_asioDriver != null)
                    {
                        _asioDriver.Dispose();
                        _asioDriver = null;
                    }

                    // Create new ASIO driver instance
                    _asioDriver = new AsioOut(deviceName);
                    _selectedDeviceName = deviceName;

                    // Query channel counts
                    _inputChannelCount = _asioDriver.DriverInputChannelCount;
                    _outputChannelCount = _asioDriver.DriverOutputChannelCount;

                    success = true;
                }
                catch (Exception ex)
                {
                    LastError = $"{ex.GetType().Name}: {ex.Message}";
                    _asioDriver?.Dispose();
                    _asioDriver = null;
                    success = false;
                }
            });

            staThread.SetApartmentState(System.Threading.ApartmentState.STA);
            staThread.Start();
            staThread.Join(); // Wait for initialization to complete

            return success;
        }

        /// <summary>
        /// Start ASIO playback and recording with custom provider
        /// </summary>
        public bool Start(IWaveProvider waveProvider)
        {
            try
            {
                if (_asioDriver == null)
                {
                    return false;
                }

                // Subscribe to audio available event
                _asioDriver.AudioAvailable += OnAsioAudioAvailable;

                // Initialize recording and playback
                _asioDriver.InitRecordAndPlayback(waveProvider, _inputChannelCount, SAMPLE_RATE);

                // Start playback
                _asioDriver.Play();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Stop ASIO playback and recording
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_asioDriver != null)
                {
                    _asioDriver.AudioAvailable -= OnAsioAudioAvailable;
                    _asioDriver.Stop();
                }
            }
            catch (Exception)
            {
                // Silently continue
            }
        }

        /// <summary>
        /// ASIO audio callback - forwards to subscribers
        /// </summary>
        private void OnAsioAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            // Forward event to subscribers (AsioAudioEngine)
            AudioAvailable?.Invoke(sender, e);
        }

        /// <summary>
        /// Get channel names from ASIO driver
        /// </summary>
        public List<string> GetInputChannelNames()
        {
            var names = new List<string>();
            if (_asioDriver != null)
            {
                for (int i = 0; i < _inputChannelCount; i++)
                {
                    names.Add($"Input {i + 1}");
                }
            }
            return names;
        }

        public List<string> GetOutputChannelNames()
        {
            var names = new List<string>();
            if (_asioDriver != null)
            {
                for (int i = 0; i < _outputChannelCount; i++)
                {
                    names.Add($"Output {i + 1}");
                }
            }
            return names;
        }

        public void Dispose()
        {
            Stop();
            _asioDriver?.Dispose();
            _asioDriver = null;
        }
    }
}
