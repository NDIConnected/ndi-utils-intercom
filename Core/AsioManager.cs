using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Manages ASIO device initialization, channel discovery, and audio callbacks
    /// </summary>
    public class AsioManager : IDisposable
    {
        // Static logger so a wedged ASIO path is visible in the rolling log file.
        // Previously every failure here was swallowed, which made a silent ASIO
        // indistinguishable from a misconfigured one.
        private static ILogger _logger = NullLogger.Instance;
        public static void SetLogger(ILogger logger) => _logger = logger ?? NullLogger.Instance;

        private AsioOut _asioDriver;
        private string _selectedDeviceName;
        private int _inputChannelCount;
        private int _outputChannelCount;
        private const int SAMPLE_RATE = 48000;

        // Events for audio data
        public event EventHandler<AsioAudioAvailableEventArgs> AudioAvailable;

        /// <summary>
        /// Raised when the driver asks the host to reset (user changed buffer size or sample
        /// rate in the ASIO control panel). Channel counts and buffer size can change, so the
        /// audio engine must be rebuilt — ignoring this leaves the engine interleaving output
        /// with a stale channel stride, which puts audio on the wrong physical outputs.
        /// </summary>
        public event EventHandler DriverResetRequested;

        public bool IsInitialized => _asioDriver != null;

        /// <summary>
        /// True only after <see cref="Start"/> has completed <c>InitRecordAndPlayback</c> and
        /// <c>Play()</c> without throwing. An <see cref="AsioOut"/> instance can only be
        /// initialised once, so a failed Start requires a full <see cref="Initialize"/>.
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>Driver-reported transport state; false once the driver stops on its own.</summary>
        public bool IsPlaying
        {
            get
            {
                var driver = _asioDriver;
                if (driver == null || !_isStarted)
                    return false;

                try
                {
                    return driver.PlaybackState == PlaybackState.Playing;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private volatile bool _isStarted;

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

            // A driver instance can only be initialised once (NAudio throws on a second
            // InitRecordAndPlayback), so recovery always goes through a fresh instance.
            _isStarted = false;

            // ASIO requires STA thread - create dedicated STA thread for initialization
            var staThread = new System.Threading.Thread(() =>
            {
                try
                {
                    // Dispose existing driver if any
                    if (_asioDriver != null)
                    {
                        try { _asioDriver.DriverResetRequest -= OnDriverResetRequest; } catch { }
                        _asioDriver.Dispose();
                        _asioDriver = null;
                    }

                    // Create new ASIO driver instance
                    _asioDriver = new AsioOut(deviceName);
                    _selectedDeviceName = deviceName;

                    // Query channel counts
                    _inputChannelCount = _asioDriver.DriverInputChannelCount;
                    _outputChannelCount = _asioDriver.DriverOutputChannelCount;

                    _asioDriver.DriverResetRequest += OnDriverResetRequest;

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

            if (success)
            {
                bool rateOk;
                try { rateOk = _asioDriver.IsSampleRateSupported(SAMPLE_RATE); }
                catch (Exception) { rateOk = false; }

                // The intercom pipeline is hard-wired to 48 kHz. A driver locked to another
                // rate by its own control panel (Dante Virtual Soundcard does this) makes
                // InitRecordAndPlayback throw later; say so now instead of at Start().
                if (!rateOk)
                {
                    _logger.LogWarning(
                        "ASIO device '{Device}' reports {Rate} Hz as unsupported. Set the driver to that rate in its control panel, otherwise playback cannot start.",
                        deviceName, SAMPLE_RATE);
                }

                _logger.LogInformation(
                    "ASIO device '{Device}' initialized: inputs={Inputs}, outputs={Outputs}, sample_rate_48k_supported={RateOk}",
                    deviceName, _inputChannelCount, _outputChannelCount, rateOk);
            }
            else
            {
                _logger.LogWarning("ASIO device '{Device}' initialization failed: {Err}", deviceName, LastError ?? "unknown");
            }

            return success;
        }

        private void OnDriverResetRequest(object sender, EventArgs e)
        {
            _logger.LogWarning("ASIO device '{Device}' requested a driver reset; the audio engine will be rebuilt.", _selectedDeviceName);
            // The driver is no longer usable in its current configuration. Marking it stopped
            // makes the reconcile pass in IntercomEngine tear down and rebuild the engine.
            _isStarted = false;
            try { DriverResetRequested?.Invoke(this, EventArgs.Empty); } catch { }
        }

        /// <summary>
        /// Start ASIO playback and recording with custom provider
        /// </summary>
        public bool Start(IWaveProvider waveProvider)
        {
            var driver = _asioDriver;
            if (driver == null)
            {
                LastError = "ASIO driver not initialized";
                return false;
            }

            try
            {
                // Subscribe to audio available event
                driver.AudioAvailable += OnAsioAudioAvailable;

                // Initialize recording and playback
                driver.InitRecordAndPlayback(waveProvider, _inputChannelCount, SAMPLE_RATE);

                // Start playback
                driver.Play();

                _isStarted = true;
                _logger.LogInformation(
                    "ASIO device '{Device}' started: inputs={Inputs}, outputs={Outputs}, frames_per_buffer={Frames}",
                    _selectedDeviceName, driver.NumberOfInputChannels, driver.NumberOfOutputChannels, driver.FramesPerBuffer);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"{ex.GetType().Name}: {ex.Message}";
                _isStarted = false;

                // Leave nothing half-wired: the handler was attached before the init call, and
                // this instance can never be initialised again, so drop it entirely. The caller
                // recovers by calling Initialize() for a fresh instance.
                try { driver.AudioAvailable -= OnAsioAudioAvailable; } catch { }
                try { driver.DriverResetRequest -= OnDriverResetRequest; } catch { }
                try { driver.Dispose(); } catch { }
                _asioDriver = null;

                _logger.LogError(ex, "ASIO device '{Device}' failed to start; driver discarded so it can be re-initialized.", _selectedDeviceName);
                return false;
            }
        }

        /// <summary>
        /// Stop ASIO playback and recording
        /// </summary>
        public void Stop()
        {
            // NAudio keeps its `isInitialized` flag set after Stop(), so a stopped instance can
            // never be re-initialised. Clearing _isStarted makes callers go through
            // Initialize() again rather than silently believing ASIO is still live.
            _isStarted = false;

            try
            {
                if (_asioDriver != null)
                {
                    _asioDriver.AudioAvailable -= OnAsioAudioAvailable;
                    _asioDriver.Stop();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ASIO device '{Device}' stop failed", _selectedDeviceName);
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

        /// <summary>
        /// Stops and releases the driver instance. Required before any restart: NAudio keeps
        /// its internal <c>isInitialized</c> flag set for the lifetime of an
        /// <see cref="AsioOut"/>, so a stopped instance can never be started again.
        /// </summary>
        public void ReleaseDriver()
        {
            Stop();

            var driver = _asioDriver;
            _asioDriver = null;
            if (driver != null)
            {
                try { driver.DriverResetRequest -= OnDriverResetRequest; } catch { }
                try { driver.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            ReleaseDriver();
        }
    }
}
