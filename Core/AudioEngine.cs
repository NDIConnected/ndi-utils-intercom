using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    public class AudioEngine : ILocalAudioEngine
    {
        // Static logger so watchdog retries are observable in the rolling log file.
        // NullLogger until IntercomAppHost wires it; never throws if unset.
        private static ILogger _logger = NullLogger.Instance;
        public static void SetLogger(ILogger logger) => _logger = logger ?? NullLogger.Instance;

        // Audio constants
        private const int SAMPLE_RATE = 48000;        // Standard audio sample rate
        private const int BUFFER_SIZE_MS = 120;        // Output buffer size (ultra-low latency)

        // Watchdog parameters: when capture/playback stops unexpectedly we retry with
        // exponential backoff capped at WATCHDOG_MAX_DELAY_MS, keeping retrying forever
        // (24/7 operation must self-heal across USB reconnects, driver restarts, etc.).
        private const int WATCHDOG_INITIAL_DELAY_MS = 1000;
        private const int WATCHDOG_MAX_DELAY_MS = 16000;

        private WasapiCapture _capture;
        private WasapiOut _output;
        private BufferedWaveProvider _outputBuffer;
        private MMDevice? _selectedMicrophone;
        private MMDevice? _selectedSpeaker;
        private int _bufferSizeMs = BUFFER_SIZE_MS;
        private int _captureChannels = 2; // Track number of channels being captured (default stereo)
        private NoiseGate _noiseGate;

        // Watchdog state
        private volatile bool _captureDesired;
        private volatile bool _playbackDesired;
        private int _captureRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS;
        private int _playbackRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS;
        private readonly object _captureLifecycleLock = new object();
        private readonly object _playbackLifecycleLock = new object();

        public event EventHandler<float> MicrophoneLevelUpdated;
        public event EventHandler<byte[]> AudioCaptured;

        public AudioEngine()
        {
            // Initialize noise gate with standard sample rate
            _noiseGate = new NoiseGate(SAMPLE_RATE);
        }

        public List<AudioDeviceInfo> GetInputDevices()
        {
            var devices = new List<AudioDeviceInfo>
            {
                // Empty id is the persisted "default" selection. Without this entry the
                // settings page has nothing to select and the browser shows the first
                // endpoint, which Apply then writes back as if the operator chose it.
                new AudioDeviceInfo
                {
                    DeviceId = "",
                    FriendlyName = "Default (Windows communications device)",
                    IsInput = true,
                    IsOutput = false
                }
            };
            var enumerator = new MMDeviceEnumerator();

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                devices.Add(new AudioDeviceInfo
                {
                    DeviceId = device.ID,
                    FriendlyName = device.FriendlyName,
                    IsInput = true,
                    IsOutput = false
                });
            }

            return devices;
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            var devices = new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo
                {
                    DeviceId = "",
                    FriendlyName = "Default (Windows communications device)",
                    IsInput = false,
                    IsOutput = true
                }
            };
            var enumerator = new MMDeviceEnumerator();

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add(new AudioDeviceInfo
                {
                    DeviceId = device.ID,
                    FriendlyName = device.FriendlyName,
                    IsInput = false,
                    IsOutput = true
                });
            }

            return devices;
        }

        public void SelectMicrophone(string deviceId)
        {
            StopCapture();

            try
            {
                // Dispose previous device handle to release the underlying COM RCW; without
                // this, switching device repeatedly leaks one MMDevice per call.
                _selectedMicrophone?.Dispose();
                var enumerator = new MMDeviceEnumerator();
                _selectedMicrophone = string.IsNullOrEmpty(deviceId)
                    ? null
                    : enumerator.GetDevice(deviceId);
            }
            catch (Exception)
            {
                _selectedMicrophone = null; // Force fallback to default
            }
        }

        public void SelectSpeaker(string deviceId)
        {
            StopPlayback();

            try
            {
                _selectedSpeaker?.Dispose();
                var enumerator = new MMDeviceEnumerator();
                _selectedSpeaker = string.IsNullOrEmpty(deviceId)
                    ? null
                    : enumerator.GetDevice(deviceId);
            }
            catch (Exception)
            {
                _selectedSpeaker = null; // Force fallback to default
            }
        }

        public void StartCapture()
        {
            _captureDesired = true;
            _captureRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS;
            StartCaptureInternal();
        }

        /// <summary>
        /// Actually create the WASAPI capture instance. Called from <see cref="StartCapture"/>
        /// and from the watchdog re-arm path. Idempotent: if capture is already running, no-op.
        /// </summary>
        private void StartCaptureInternal()
        {
            lock (_captureLifecycleLock)
            {
                if (!_captureDesired)
                    return;
                if (_capture != null)
                    return;

                // If no microphone selected, use default
                if (_selectedMicrophone == null)
                {
                    var enumerator = new MMDeviceEnumerator();
                    try
                    {
                        _selectedMicrophone = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    }
                    catch
                    {
                        // Default device not available — schedule retry; user may plug one back in.
                        ScheduleCaptureRetry();
                        return;
                    }
                }

                try
                {
                    var capture = new WasapiCapture(_selectedMicrophone, true, 50);
                    capture.DataAvailable += OnDataAvailable;
                    // Watchdog hook: if WASAPI stops unexpectedly (USB unplug, driver restart,
                    // session change), this fires with e.Exception != null. We re-arm the
                    // capture in that case so 24/7 operation self-heals.
                    capture.RecordingStopped += OnCaptureStopped;
                    capture.StartRecording();
                    _captureChannels = capture.WaveFormat.Channels;
                    _capture = capture;
                    _captureRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS; // reset on success
                    _logger.LogInformation("WASAPI capture started (device={Device}, channels={Channels})",
                        _selectedMicrophone.FriendlyName, _captureChannels);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WASAPI capture start failed; will retry in {DelayMs} ms", _captureRetryDelayMs);
                    ScheduleCaptureRetry();
                }
            }
        }

        public void StopCapture()
        {
            _captureDesired = false;
            DisposeCapture();
        }

        private void DisposeCapture()
        {
            WasapiCapture captureToDispose;
            lock (_captureLifecycleLock)
            {
                if (_capture == null)
                    return;
                captureToDispose = _capture;
                _capture = null;
            }

            try
            {
                captureToDispose.DataAvailable -= OnDataAvailable;
                captureToDispose.RecordingStopped -= OnCaptureStopped;
                try { captureToDispose.StopRecording(); } catch { }

                // Dispose in background thread with timeout to prevent UI deadlock.
                var disposeTask = System.Threading.Tasks.Task.Run(() =>
                {
                    try { captureToDispose.Dispose(); } catch { }
                });
                disposeTask.Wait(500);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private void OnCaptureStopped(object sender, StoppedEventArgs e)
        {
            // Drop the dead capture instance.
            DisposeCapture();

            if (_captureDesired)
            {
                if (e?.Exception != null)
                {
                    _logger.LogWarning(e.Exception, "WASAPI capture stopped unexpectedly; will retry in {DelayMs} ms", _captureRetryDelayMs);
                }
                else
                {
                    _logger.LogWarning("WASAPI capture stopped (no exception); will retry in {DelayMs} ms", _captureRetryDelayMs);
                }
                // A USB device may be reconnecting, a driver may be restarting, or a session
                // may have been ended by the OS. Bounded exponential backoff in retries.
                ScheduleCaptureRetry();
            }
        }

        private void ScheduleCaptureRetry()
        {
            int delay = _captureRetryDelayMs;
            // Exponential backoff capped at WATCHDOG_MAX_DELAY_MS so we don't pound a
            // permanently-unavailable device but still recover quickly from a brief glitch.
            _captureRetryDelayMs = Math.Min(_captureRetryDelayMs * 2, WATCHDOG_MAX_DELAY_MS);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay);
                    if (_captureDesired)
                    {
                        StartCaptureInternal();
                    }
                }
                catch
                {
                    // Best-effort retry; further retries will happen via the next StoppedEvent
                    // or the next ScheduleCaptureRetry() invocation.
                }
            });
        }

        public void StartPlayback()
        {
            _playbackDesired = true;
            _playbackRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS;
            StartPlaybackInternal();
        }

        private void StartPlaybackInternal()
        {
            lock (_playbackLifecycleLock)
            {
                if (!_playbackDesired)
                    return;
                if (_output != null)
                    return;

                // If no speaker selected, use default
                if (_selectedSpeaker == null)
                {
                    var enumerator = new MMDeviceEnumerator();
                    try
                    {
                        _selectedSpeaker = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
                    }
                    catch
                    {
                        SchedulePlaybackRetry();
                        return;
                    }
                }

                try
                {
                    var output = new WasapiOut(_selectedSpeaker, AudioClientShareMode.Shared, true, 50);

                    // Create buffer with appropriate format (mono, 32-bit float)
                    var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, 1);
                    var outputBuffer = new BufferedWaveProvider(waveFormat)
                    {
                        BufferDuration = TimeSpan.FromMilliseconds(_bufferSizeMs),
                        DiscardOnBufferOverflow = true
                    };

                    output.Init(outputBuffer);
                    // Watchdog hook for the playback path (mirrors capture).
                    output.PlaybackStopped += OnPlaybackStopped;
                    output.Play();

                    _outputBuffer = outputBuffer;
                    _output = output;
                    _playbackRetryDelayMs = WATCHDOG_INITIAL_DELAY_MS;
                    _logger.LogInformation("WASAPI playback started (device={Device})", _selectedSpeaker.FriendlyName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WASAPI playback start failed; will retry in {DelayMs} ms", _playbackRetryDelayMs);
                    SchedulePlaybackRetry();
                }
            }
        }

        public void StopPlayback()
        {
            _playbackDesired = false;
            DisposePlayback();
        }

        private void DisposePlayback()
        {
            WasapiOut outputToDispose;
            lock (_playbackLifecycleLock)
            {
                if (_output == null)
                    return;
                outputToDispose = _output;
                _output = null;
                _outputBuffer = null;
            }

            try
            {
                outputToDispose.PlaybackStopped -= OnPlaybackStopped;
                try { outputToDispose.Stop(); } catch { }

                var disposeTask = System.Threading.Tasks.Task.Run(() =>
                {
                    try { outputToDispose.Dispose(); } catch { }
                });
                disposeTask.Wait(500);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            DisposePlayback();
            if (_playbackDesired)
            {
                if (e?.Exception != null)
                    _logger.LogWarning(e.Exception, "WASAPI playback stopped unexpectedly; will retry in {DelayMs} ms", _playbackRetryDelayMs);
                else
                    _logger.LogWarning("WASAPI playback stopped (no exception); will retry in {DelayMs} ms", _playbackRetryDelayMs);
                SchedulePlaybackRetry();
            }
        }

        private void SchedulePlaybackRetry()
        {
            int delay = _playbackRetryDelayMs;
            _playbackRetryDelayMs = Math.Min(_playbackRetryDelayMs * 2, WATCHDOG_MAX_DELAY_MS);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay);
                    if (_playbackDesired)
                    {
                        StartPlaybackInternal();
                    }
                }
                catch
                {
                    // Best-effort retry
                }
            });
        }

        public void AddToOutputBuffer(byte[] audioData)
        {
            // Snapshot the field — the watchdog can null it out concurrently when
            // recreating the playback path. Catch any in-flight Dispose race.
            var buf = _outputBuffer;
            if (buf == null || audioData == null || audioData.Length == 0)
                return;
            try
            {
                buf.AddSamples(audioData, 0, audioData.Length);
            }
            catch
            {
                // Buffer was disposed concurrently; the next callback will use the new one.
            }
        }

        // Noise Gate Configuration
        public void ConfigureNoiseGate(bool enabled, float thresholdDb, float attackMs, float releaseMs, float holdMs)
        {
            _noiseGate.Enabled = enabled;
            _noiseGate.SetThreshold(thresholdDb);
            _noiseGate.SetAttackTime(attackMs);
            _noiseGate.SetReleaseTime(releaseMs);
            _noiseGate.SetHysteresis(6.0f); // 6dB hysteresis to prevent fluttering
            _noiseGate.SetHoldTime(holdMs);
        }

        public bool IsNoiseGateOpen => _noiseGate?.CurrentGain > 0.5f;

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            // Convert stereo to mono if needed (intercom is mono)
            byte[] audioData;
            if (_captureChannels == 2)
            {
                // Stereo to mono: mix L+R channels
                int stereoSamples = e.BytesRecorded / 4; // 32-bit float = 4 bytes
                int monoSamples = stereoSamples / 2;
                audioData = new byte[monoSamples * 4];

                for (int i = 0; i < monoSamples; i++)
                {
                    float left = BitConverter.ToSingle(e.Buffer, i * 8);      // L
                    float right = BitConverter.ToSingle(e.Buffer, i * 8 + 4); // R
                    float mono = (left + right) / 2.0f; // Mix and average
                    BitConverter.GetBytes(mono).CopyTo(audioData, i * 4);
                }
            }
            else
            {
                // Already mono, use as-is
                audioData = e.Buffer.Take(e.BytesRecorded).ToArray();
            }

            // Apply noise gate BEFORE sending to NDI
            _noiseGate.Process(audioData, audioData.Length);

            // Calculate RMS level for VU meter (use mono audio AFTER noise gate)
            float level = CalculateRmsLevel(audioData, audioData.Length);
            // Defensive Invoke: a misbehaving subscriber (e.g. SignalR client mid-disconnect)
            // must never propagate an exception to the WASAPI capture callback — that would
            // surface as RecordingStopped(e.Exception != null) and force a watchdog retry
            // even though audio is fine.
            try { MicrophoneLevelUpdated?.Invoke(this, level); } catch { }
            try { AudioCaptured?.Invoke(this, audioData); } catch { }
        }

        private float CalculateRmsLevel(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded == 0) return 0;

            float sum = 0;
            int sampleCount = bytesRecorded / 4; // 32-bit float samples

            for (int i = 0; i < bytesRecorded; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                sum += sample * sample;
            }

            float rms = (float)Math.Sqrt(sum / sampleCount);
            return 20 * (float)Math.Log10(rms + 1e-10f); // Convert to dB
        }

        public void Dispose()
        {
            StopCapture();
            StopPlayback();
            // Release MMDevice COM RCWs deterministically.
            try { _selectedMicrophone?.Dispose(); } catch { }
            try { _selectedSpeaker?.Dispose(); } catch { }
            _selectedMicrophone = null;
            _selectedSpeaker = null;
        }
    }
}
