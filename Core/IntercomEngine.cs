using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Main engine that coordinates audio, NDI, and mixing
    /// </summary>
    public class IntercomEngine : IDisposable
    {
        private const string DefaultApplicationId = "Intercom_A";
        private static readonly Regex ApplicationIdRegex = new Regex("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);

        private ILocalAudioEngine _audioEngine;
        private NDIManager _ndiManager;
        private MixingEngine _mixingEngine;
#if WINDOWS
        private AsioManager _asioManager;
        private AsioAudioEngine _asioAudioEngine;
        private Dictionary<int, float> _asioAudioLevels = new Dictionary<int, float>();
        private readonly object _asioLevelsLock = new object();
#endif
        private List<ChannelState> _channels;
        private AppConfig _config;
        private CancellationTokenSource _processingCancellation;
        private Task _processingTask;

        // Captured Task.CurrentId of the audio thread; used by Stop() to avoid
        // self-waiting (which would deadlock).
        private int? _audioThreadTaskId;

        private bool _audioDeviceSelectionAppliedFromConfig;
        private string _lastAppliedMic = "";
        private string _lastAppliedSpk = "";

        // RING BUFFER: Accumulates variable-rate mic audio for fixed-rate NDI transmission
        private AudioRingBuffer _microphoneBuffer;
        private const int RING_BUFFER_CAPACITY = 5760; // 120ms @ 48kHz (ultra-low latency!)

        /// <summary>
        /// Mono float32 silence for one NDI frame (40ms @ 48kHz). Keeps senders streaming so receivers
        /// (e.g. NDI Studio Monitor) decay VU instead of freezing when mic ring underruns or mix is empty.
        /// </summary>
        private static readonly byte[] SilentMonoNdiFrame = new byte[NDIManager.NDI_FRAME_SAMPLES * sizeof(float)];

        private readonly ILogger _logger;

        // Default constructor (parameterless): used by tests / legacy callers; routes to the
        // logger-aware ctor with a NullLogger so existing call sites keep compiling.
        public IntercomEngine() : this(NullLogger<IntercomEngine>.Instance) { }

        public IntercomEngine(ILogger<IntercomEngine> logger)
        {
            _logger = logger ?? NullLogger<IntercomEngine>.Instance;

            _audioEngine = AudioEngineFactory.CreateDefault();
            _ndiManager = new NDIManager();
            _mixingEngine = new MixingEngine();
#if WINDOWS
            _asioManager = new AsioManager();
#endif
            _channels = new List<ChannelState>();

            // Initialize ring buffer for microphone audio
            _microphoneBuffer = new AudioRingBuffer(RING_BUFFER_CAPACITY);

            int max = IntercomRuntime.Product.MaxChannels;
            for (int i = 1; i <= max; i++)
            {
                _channels.Add(new ChannelState { ChannelNumber = i, Label = $"Channel {i}" });
            }

            // Connect audio engine events
            _audioEngine.AudioCaptured += OnAudioCaptured;
        }

        private void OnAudioCaptured(object sender, byte[] audioData)
        {
            // Write captured audio to ring buffer (variable-rate input)
            _microphoneBuffer.Write(audioData);
        }

        // Initialization state used by the /healthz endpoint and by management tooling.
        // Set to true only when NDI initialization succeeded; consumers must treat false
        // as "audio path is non-functional even though the web UI is up".
        private volatile bool _isInitialized;
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Normalizes a config's identity fields in place; returns true when something
        /// changed and the caller should persist it.
        /// </summary>
        /// <remarks>
        /// Shared by <see cref="Initialize"/> and <see cref="ApplyConfig"/> so the NDI names
        /// chosen while creating senders/receivers match the ones the first ApplyConfig would
        /// produce. Otherwise every endpoint is created with a placeholder identity and then
        /// destroyed and recreated moments later.
        /// </remarks>
        public static bool NormalizeIdentity(AppConfig config)
        {
            if (config == null)
            {
                return false;
            }

            bool changed = false;

            string normalizedApplicationId = SanitizeApplicationId(config.ApplicationId);
            if (!string.Equals(config.ApplicationId, normalizedApplicationId, StringComparison.Ordinal))
            {
                config.ApplicationId = normalizedApplicationId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.DeviceId))
            {
                config.DeviceId = Guid.NewGuid().ToString("N");
                changed = true;
            }

            // NDI source names always use Compact suffix (not user-configurable).
            if (!string.Equals(config.IdentitySuffixMode, NDIManager.SuffixModeCompact, StringComparison.Ordinal))
            {
                config.IdentitySuffixMode = NDIManager.SuffixModeCompact;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Initializes NDI. Pass <paramref name="initialConfig"/> (the persisted config) so
        /// channels are created with their final identity straight away.
        /// </summary>
        public bool Initialize(int webPort = 5016, AppConfig initialConfig = null)
        {
            if (initialConfig != null && NormalizeIdentity(initialConfig))
            {
                ConfigManager.SaveConfig(initialConfig);
            }

            bool ok = _ndiManager.Initialize(
                webPort,
                IntercomRuntime.Product.MaxChannels,
                initialConfig?.ApplicationId,
                initialConfig?.DeviceId,
                NDIManager.SuffixModeCompact);
            if (!ok)
            {
                _logger.LogCritical("IntercomEngine.Initialize: NDIManager.Initialize returned false. Audio engine will not start. Web UI will be available but the intercom is non-functional.");
            }
            else
            {
                _isInitialized = true;
                _logger.LogInformation("IntercomEngine.Initialize: NDI ready on web port {WebPort}, channels={Channels}",
                    webPort, IntercomRuntime.Product.MaxChannels);
            }
            return ok;
        }

        public void ConnectMicrophoneLevelEvent(EventHandler<float> handler)
        {
            _audioEngine.MicrophoneLevelUpdated += handler;
        }

        public void DisconnectMicrophoneLevelEvent(EventHandler<float> handler)
        {
            _audioEngine.MicrophoneLevelUpdated -= handler;
        }

        public float GetChannelReceiveLevel(int channelNumber)
        {
#if WINDOWS
            var channel = _channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null && channel.Mode == ChannelMode.ASIO)
            {
                lock (_asioLevelsLock)
                {
                    if (_asioAudioLevels.TryGetValue(channelNumber, out float level))
                    {
                        return level;
                    }
                }
                return -60f;
            }
#endif
            return _ndiManager.GetChannelReceiveLevel(channelNumber);
        }

        public List<ChannelState> GetChannels()
        {
            RefreshNdiConnectionFlags();
            return _channels;
        }

        /// <summary>
        /// Per-channel NDI receiver status (configured vs actually connected).
        /// </summary>
        public Dictionary<int, NDIReceiverStatus> GetReceiverStatuses()
        {
            return _ndiManager.GetReceiverStatuses();
        }

        /// <summary>
        /// Number of NDI channels that have a source configured, and how many of those are
        /// actually connected. Used by <c>/healthz</c> so an all-silent intercom is not
        /// reported as healthy.
        /// </summary>
        public (int Configured, int Connected) GetNdiReceiverHealth()
        {
            int configured = 0;
            int connected = 0;

            foreach (var status in _ndiManager.GetReceiverStatuses().Values)
            {
                if (string.IsNullOrEmpty(status.ConfiguredSource))
                    continue;

                configured++;
                if (status.IsConnected)
                    connected++;
            }

            return (configured, connected);
        }

        /// <summary>
        /// Mirrors the real receiver state onto <see cref="ChannelState.IsConnected"/> so the
        /// web UI can distinguish "source selected and receiving" from "source selected but
        /// nothing arriving".
        /// </summary>
        private void RefreshNdiConnectionFlags()
        {
            try
            {
                var statuses = _ndiManager.GetReceiverStatuses();
                foreach (var channel in _channels)
                {
                    if (channel.Mode != ChannelMode.NDI)
                        continue;

                    channel.IsConnected = statuses.TryGetValue(channel.ChannelNumber, out var status)
                                          && status.IsConnected;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RefreshNdiConnectionFlags failed");
            }
        }

        public void UpdateAsioChannelStates()
        {
#if WINDOWS
            if (_asioAudioEngine != null)
            {
                _asioAudioEngine.UpdateChannelStates(_channels);
            }
#endif
        }

        public List<AudioDeviceInfo> GetInputDevices()
        {
            return _audioEngine.GetInputDevices();
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            return _audioEngine.GetOutputDevices();
        }

        public List<string> GetAvailableNDISources()
        {
            return _ndiManager.DiscoverSources();
        }

#if WINDOWS
        public List<string> GetAvailableAsioDevices()
        {
            return _asioManager.GetAvailableDevices();
        }

        public bool InitializeAsioDevice(string deviceName)
        {
            // Stop existing ASIO engine if switching devices.
            // We MUST also unsubscribe the previous AudioAvailable handler from the
            // _asioManager event, otherwise the lambda keeps the old _asioAudioEngine
            // alive (memory leak) and a future ASIO callback fires on a stale instance
            // (potential crash). See StartAsioEngine() for the matching subscribe.
            if (_asioAudioEngine != null && _asioManager.IsInitialized)
            {
                DetachAsioAudioHandler();
                _asioManager.Stop();
                _asioAudioEngine = null;
            }

            bool success = _asioManager.Initialize(deviceName);
            if (success)
            {
                if (_config != null)
                {
                    _config.SelectedAsioDevice = deviceName;
                    _config.AsioInputChannelCount = _asioManager.InputChannelCount;
                    _config.AsioOutputChannelCount = _asioManager.OutputChannelCount;
                    SaveCurrentConfiguration();
                }

                if (IsRunning)
                {
                    StartAsioEngine();
                }
            }
            return success;
        }

        // Captured handler reference so we can unsubscribe symmetrically when switching
        // ASIO device — preventing the leak described in InitializeAsioDevice().
        // AsioAudioAvailableEventArgs lives in NAudio.Wave (not NAudio.Wave.Asio).
        private EventHandler<NAudio.Wave.AsioAudioAvailableEventArgs>? _asioAudioHandler;

        private void DetachAsioAudioHandler()
        {
            if (_asioAudioHandler != null)
            {
                _asioManager.AudioAvailable -= _asioAudioHandler;
                _asioAudioHandler = null;
            }
        }

        /// <summary>
        /// Creates and starts the ASIO audio engine (called from Start() or InitializeAsioDevice())
        /// </summary>
        private void StartAsioEngine()
        {
            if (!_asioManager.IsInitialized || _asioAudioEngine != null)
                return;

            _asioAudioEngine = new AsioAudioEngine(_asioManager.OutputChannelCount);
            _asioAudioEngine.UpdateChannelStates(_channels);

            // Connect ASIO audio input callback.
            // Defensive: if a previous handler is still attached for any reason, drop it first.
            DetachAsioAudioHandler();
            // Use the engine reference captured at this moment; the handler reads it via
            // a local copy so it stays valid even if _asioAudioEngine is replaced before
            // the unsubscribe happens (we still unsubscribe in InitializeAsioDevice).
            var engineSnapshot = _asioAudioEngine;
            _asioAudioHandler = (sender, e) => engineSnapshot.ProcessAsioInput(e);
            _asioManager.AudioAvailable += _asioAudioHandler;

            _asioManager.Start(_asioAudioEngine);
        }

        public string GetAsioLastError()
        {
            return _asioManager.LastError;
        }

        public int GetAsioInputChannelCount()
        {
            return _asioManager.InputChannelCount;
        }

        public int GetAsioOutputChannelCount()
        {
            return _asioManager.OutputChannelCount;
        }
#endif

        public bool IsRunning
        {
            get
            {
                return _processingTask != null &&
                       _processingCancellation != null &&
                       !_processingCancellation.Token.IsCancellationRequested;
            }
        }

        private bool _sendersCreated = false;

        // The legacy `isWindowsService` parameter is kept for binary compatibility (callers
        // outside this assembly may pass it) but is now ignored — ASIO retry runs the same
        // way in tray-app and (former) service modes. Bound to 3 attempts with exponential
        // backoff so an autostart at boot still succeeds when the ASIO driver is slow to
        // come up after the audio interface enumerates.
        public void ApplyConfig(AppConfig config, bool isWindowsService = false)
        {
            _ = isWindowsService; // intentionally unused (see comment above)

            _config = config ?? new AppConfig();
            _config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);

            bool shouldPersistConfig = NormalizeIdentity(_config);

            _ndiManager.SetIdentity(_config.ApplicationId, _config.DeviceId);
            _ndiManager.SetSuffixMode(NDIManager.SuffixModeCompact);
            config = _config;

            // Apply WDM/Pulse microphone & speaker (empty string = default device).
            // Previously empty fields were skipped, so switching back to "Default" in the UI never updated the engine.
            string mic = config.SelectedMicrophone ?? "";
            string spk = config.SelectedSpeaker ?? "";
            bool audioDevicesChanged = !_audioDeviceSelectionAppliedFromConfig
                || !string.Equals(mic, _lastAppliedMic, StringComparison.Ordinal)
                || !string.Equals(spk, _lastAppliedSpk, StringComparison.Ordinal);

            if (audioDevicesChanged)
            {
                _audioDeviceSelectionAppliedFromConfig = true;
                _lastAppliedMic = mic;
                _lastAppliedSpk = spk;
                _audioEngine.SelectMicrophone(mic);
                _audioEngine.SelectSpeaker(spk);

                if (_processingTask != null)
                {
                    Task.Run(() =>
                    {
                        _audioEngine.StopCapture();
                        _audioEngine.StopPlayback();
                        Thread.Sleep(100);
                        _audioEngine.StartCapture();
                        _audioEngine.StartPlayback();
                    });
                }
            }

#if WINDOWS
            // Apply ASIO device configuration with retry. Useful at autostart-on-boot when
            // the ASIO driver may not be ready until a few seconds after the audio interface
            // enumerates. 3 attempts × exponential backoff (2s → 4s → 8s) is enough in
            // practice without making config-apply feel sluggish on a healthy system.
            if (!string.IsNullOrEmpty(config.SelectedAsioDevice))
            {
                if (!_asioManager.IsInitialized || _asioManager.SelectedDevice != config.SelectedAsioDevice)
                {
                    bool asioInitialized = false;

                    // The backoff exists for autostart-on-boot, where the ASIO driver can lag
                    // behind the interface enumerating. An interactive Apply runs on a SignalR
                    // hub thread, so blocking it for up to 14s there would freeze the settings
                    // page; give the running case a single attempt.
                    int maxRetries = IsRunning ? 1 : 3;
                    int retryDelayMs = 2000;

                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        asioInitialized = _asioManager.Initialize(config.SelectedAsioDevice);

                        if (asioInitialized)
                        {
                            if (attempt > 1)
                                _logger.LogInformation("ASIO device '{Device}' initialized after {Attempts} attempts", config.SelectedAsioDevice, attempt);
                            break;
                        }
                        else if (attempt < maxRetries)
                        {
                            _logger.LogWarning("ASIO device '{Device}' init attempt {Attempt}/{Max} failed: {Err}. Retrying in {Delay} ms.",
                                config.SelectedAsioDevice, attempt, maxRetries, _asioManager.LastError ?? "unknown", retryDelayMs);
                            System.Threading.Thread.Sleep(retryDelayMs);
                            retryDelayMs *= 2; // Exponential backoff: 2s, 4s, 8s
                        }
                        else
                        {
                            _logger.LogError("ASIO device '{Device}' init failed after {Max} attempts: {Err}",
                                config.SelectedAsioDevice, maxRetries, _asioManager.LastError ?? "unknown");
                        }
                    }
                }
            }
#endif

            // Apply channel configuration.
            // The apply window lets source resolution block briefly so a cold start (finder
            // created moments ago, nothing discovered yet) still connects immediately instead
            // of waiting for the first reconcile pass. The budget is shared across all
            // channels, so an all-offline setup costs it once, not once per channel.
            _ndiManager.BeginConfigApply();
            try
            {
                for (int i = 0; i < config.Channels.Count && i < _channels.Count; i++)
                {
                    var channelConfig = config.Channels[i];
                    var channelState = _channels[i];

                    channelState.Label = channelConfig.Label;
                    channelState.InputLevel = Math.Clamp(channelConfig.InputLevel, 0, 300);
                    channelState.OutputLevel = Math.Clamp(channelConfig.OutputLevel, 0, 100);
                    channelState.Mode = (ChannelMode)channelConfig.Mode;
#if !WINDOWS
                    if (channelState.Mode == ChannelMode.ASIO)
                    {
                        channelState.Mode = ChannelMode.NDI;
                    }
#endif
                    channelState.IntercomGroup = channelConfig.IntercomGroup;
                    channelState.AsioInputChannel = channelConfig.AsioInputChannel;
                    channelState.AsioOutputChannel = channelConfig.AsioOutputChannel;
                    channelState.NdiSendName = channelConfig.NdiSendName;
                    channelState.NdiReceiveName = channelConfig.NdiReceiveName;

                    // Only create senders once at startup
                    if (!_sendersCreated)
                    {
                        _ndiManager.SetChannelSendName(channelState.ChannelNumber, channelState.NdiSendName);
                    }

                    // Always update receiver sources (in case they change)
                    // This can be done while running - NDI handles it
                    _ndiManager.SetChannelReceiveSource(channelState.ChannelNumber, channelState.NdiReceiveName);
                }
            }
            finally
            {
                _ndiManager.EndConfigApply();
            }

            _sendersCreated = true;

            // Apply noise gate settings to AudioEngine (input microphone - blocks background noise)
            _audioEngine.ConfigureNoiseGate(
                config.NoiseGateEnabled,
                (float)config.NoiseGateThresholdDb,
                config.NoiseGateAttackMs,
                config.NoiseGateReleaseMs,
                config.NoiseGateHoldMs
            );

            // Apply feedback gate settings to MixingEngine (channels - prevents feedback)
            _mixingEngine.SetGateParameters(
                config.FeedbackGateEnabled,
                config.GateThresholdDb,
                config.GateAttackMs,
                config.GateReleaseMs
            );

            _mixingEngine.UpdateChannelStates(_channels);

#if WINDOWS
            // Initialize and start ASIO if a device is already selected
            StartAsioEngine();
#endif

            if (shouldPersistConfig)
            {
                ConfigManager.SaveConfig(_config);
            }
        }

        private static string SanitizeApplicationId(string applicationId)
        {
            string trimmed = (applicationId ?? string.Empty).Trim();
            if (ApplicationIdRegex.IsMatch(trimmed))
            {
                return trimmed;
            }

            return DefaultApplicationId;
        }

        public void Start()
        {
            _audioEngine.StartCapture();
            _audioEngine.StartPlayback();

#if WINDOWS
            StartAsioEngine();
#endif

            // Start DEDICATED AUDIO THREAD with PRECISE TIMING (like NDI_Test_Simple)
            _processingCancellation = new CancellationTokenSource();
            _processingTask = Task.Run(() => DedicatedAudioThread(_processingCancellation.Token));
        }

        public void Stop()
        {
            // Signal cancellation
            _processingCancellation?.Cancel();

            // Wait for the audio thread to finish — but ONLY if Stop() is not being
            // called from the audio thread itself (would deadlock). The task captures
            // its own id, so we can guard cleanly.
            try
            {
                var task = _processingTask;
                if (task != null && task.Id != _audioThreadTaskId)
                {
                    // Bounded wait: if the thread is genuinely stuck we don't want to
                    // hang the host on shutdown.
                    task.Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (AggregateException)
            {
                // Cancellation surfaces as TaskCanceledException inside AggregateException;
                // ignore — that's the normal exit path.
            }
            catch (Exception)
            {
                // Don't let shutdown be blocked by audio thread errors.
            }

            // Stop audio immediately
            _audioEngine.StopCapture();
            _audioEngine.StopPlayback();

#if WINDOWS
            if (_asioManager.IsInitialized)
            {
                _asioManager.Stop();
            }
#endif
        }

        /// <summary>
        /// Dedicated audio thread with PRECISE TIMING (based on NDI_Test_Simple pattern)
        /// Uses Stopwatch + frame number scheduling to eliminate timing jitter
        /// </summary>
        private void DedicatedAudioThread(CancellationToken cancellationToken)
        {
            // Record this task's id so Stop() can detect self-wait and skip blocking.
            _audioThreadTaskId = Task.CurrentId;

            // Audio parameters (standard NDI)
            const int sampleRate = 48000;
            const int samplesPerChannel = 1920; // Standard NDI frame: 40ms @ 48kHz
            const double msPerFrame = (double)samplesPerChannel / sampleRate * 1000.0; // 40ms exactly

            // Periodically rebase the timer/frame counter so frameNumber * msPerFrame stays
            // representable as a long without losing precision. Without this, after
            // ~2.85 years of continuous uptime the cast `(long)(frameNumber * msPerFrame)`
            // starts skipping milliseconds (double mantissa exhaustion); after ~292 million
            // years it overflows entirely. Rebasing every hour is cheap and keeps the math
            // bounded for any realistic 24/7 uptime.
            const long rebaseIntervalFrames = 3600L * 1000L / 40L; // 90 000 frames = 1 hour at 40 ms/frame

            var audioTimer = System.Diagnostics.Stopwatch.StartNew();
            long frameNumber = 0;

            // Scratch collections reused across frames so the per-frame loop allocates only
            // when the inner data really grows. The loop runs ~25/sec; previously each
            // iteration allocated 2 dictionaries + 1 list + N float[] just for bookkeeping.
            // Over 24/7 that turned into noticeable Gen1 GC pressure.
            var receivedAudio = new Dictionary<int, byte[]>(IntercomRuntime.Product.MaxChannels);
            var listenAudioPerChannel = new Dictionary<int, List<float[]>>(IntercomRuntime.Product.MaxChannels);
            var channelBuffers = new List<float[]>(IntercomRuntime.Product.MaxChannels);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // PRECISE TIMING: Calculate exact time for this frame.
                    long targetTimeMs = (long)(frameNumber * msPerFrame);
                    long currentTimeMs = audioTimer.ElapsedMilliseconds;
                    long waitMs = targetTimeMs - currentTimeMs;

                    // If we're ahead of schedule, wait (precise timing!). Clamp to 1s so a
                    // catastrophic timer drift can never put the audio loop to sleep for an
                    // arbitrarily long time (defensive against the L5 overflow scenario).
                    if (waitMs > 0)
                    {
                        if (waitMs > 1000) waitMs = 1000;
                        Thread.Sleep((int)waitMs);
                    }

                    // Periodic rebase: reset frameNumber and audioTimer once per hour so
                    // long-uptime arithmetic never loses precision. The schedule is
                    // monotonic (we always sleep up to the target), so the rebase is
                    // visible only as a one-frame timing reset and never as audio skew.
                    if (frameNumber >= rebaseIntervalFrames)
                    {
                        frameNumber = 0;
                        audioTimer.Restart();
                    }

                    // Receive ALL available audio from NDI channels (may have multiple chunks buffered)
                    // NDI source sends 1024 sample chunks (~21ms) but our thread runs at 40ms
                    // STEP 1: Drain ALL available NDI chunks into ring buffers (FAST, non-blocking)
                    // This decouples burst arrivals from constant consumption
                    int totalNdiChunksDrained = 0;
                    foreach (var channel in _channels)
                    {
                        if (channel.Mode == ChannelMode.NDI)
                        {
                            int drained = _ndiManager.DrainAllToBuffer(channel.ChannelNumber);
                            totalNdiChunksDrained += drained;
                        }
                    }

                    // STEP 2: Process audio from buffers (constant rate: 1920 samples per channel).
                    // Reuse scratch dictionaries — clear is O(N) but cheap; allocating fresh
                    // ones every frame was the GC-pressure culprit.
                    receivedAudio.Clear();
                    listenAudioPerChannel.Clear();

                    foreach (var channel in _channels)
                    {
                        byte[] audioForChannel = null;

                        // Handle NDI channels FIRST (read from buffer, not from network)
                        if (channel.Mode == ChannelMode.NDI)
                        {
                            // Read exactly 1920 samples from ring buffer (constant consumption rate)
                            audioForChannel = _ndiManager.ReceiveAudio(channel.ChannelNumber);

                            if (audioForChannel != null)
                            {
                                // Store for N-1 mixing
                                receivedAudio[channel.ChannelNumber] = audioForChannel;

                                // If LISTEN is enabled, process audio
                                if (channel.ListenEnabled)
                                {
                                    var processedAudio = _mixingEngine.PrepareChannelMonitorAudioAsFloats(channel.ChannelNumber, audioForChannel);
                                    if (processedAudio != null)
                                    {
                                        if (!listenAudioPerChannel.ContainsKey(channel.ChannelNumber))
                                            listenAudioPerChannel[channel.ChannelNumber] = new List<float[]>();
                                        listenAudioPerChannel[channel.ChannelNumber].Add(processedAudio);
                                    }
                                }
                            }
                        }
#if WINDOWS
                        else if (channel.Mode == ChannelMode.ASIO && _asioAudioEngine != null)
                        {
                            audioForChannel = _asioAudioEngine.GetAsioInputAudio(channel.AsioInputChannel);

                            if (audioForChannel != null)
                            {
                                float level = CalculateAudioLevel(audioForChannel);
                                lock (_asioLevelsLock)
                                {
                                    _asioAudioLevels[channel.ChannelNumber] = level;
                                }

                                if (channel.ListenEnabled)
                                {
                                    var processedAudio = _mixingEngine.PrepareChannelMonitorAudioAsFloats(channel.ChannelNumber, audioForChannel);
                                    if (processedAudio != null)
                                    {
                                        if (!listenAudioPerChannel.ContainsKey(channel.ChannelNumber))
                                            listenAudioPerChannel[channel.ChannelNumber] = new List<float[]>();
                                        listenAudioPerChannel[channel.ChannelNumber].Add(processedAudio);
                                    }
                                }

                                receivedAudio[channel.ChannelNumber] = audioForChannel;
                            }
                        }
#endif
                    }

                    // Concatenate each channel's chunks, then mix channels together.
                    if (listenAudioPerChannel.Count > 0)
                    {
                        channelBuffers.Clear();
                        foreach (var kvp in listenAudioPerChannel)
                        {
                            if (kvp.Value.Count > 0)
                            {
                                // Concatenate all float[] chunks from this channel.
                                // Manual sum avoids the LINQ Sum() allocation for the iterator.
                                int totalSamples = 0;
                                for (int ci = 0; ci < kvp.Value.Count; ci++)
                                    totalSamples += kvp.Value[ci].Length;

                                float[] concatenated = new float[totalSamples];
                                int offset = 0;
                                for (int ci = 0; ci < kvp.Value.Count; ci++)
                                {
                                    var chunk = kvp.Value[ci];
                                    Array.Copy(chunk, 0, concatenated, offset, chunk.Length);
                                    offset += chunk.Length;
                                }
                                channelBuffers.Add(concatenated);
                            }
                        }

                        if (channelBuffers.Count > 0)
                        {
                            byte[] mixedAudio = _mixingEngine.MixMultipleFloatStreams(channelBuffers);
                            if (mixedAudio != null)
                            {
                                _audioEngine.AddToOutputBuffer(mixedAudio);
                            }
                        }
                    }

                    // Check if any channel needs audio (TALK enabled). Manual scan avoids
                    // the LINQ closure allocation on the per-frame audio path.
                    bool anyTalkEnabled = false;
                    for (int ci = 0; ci < _channels.Count; ci++)
                    {
                        if (_channels[ci].TalkEnabled)
                        {
                            anyTalkEnabled = true;
                            break;
                        }
                    }

                    byte[] microphoneChunk = null;

                    // Read from ring buffer ONLY if at least one channel has TALK active
                    if (anyTalkEnabled)
                    {
                        microphoneChunk = _microphoneBuffer.Read(samplesPerChannel);
                    }

                    // Pass microphone buffer to mixing engine for N-1 processing
                    _mixingEngine.SetMicrophoneBuffer(microphoneChunk);

#if WINDOWS
                    if (_asioAudioEngine != null)
                    {
                        _asioAudioEngine.SetMicrophoneBuffer(microphoneChunk);
                        _asioAudioEngine.SetCrossModeAudio(receivedAudio, _channels);
                    }
#endif

                    // Process each channel with N-1 routing
                    foreach (var channel in _channels)
                    {
                        // Prepare audio to send (includes N-1 mix if in a group)
                        byte[] audioToSend = _mixingEngine.PrepareChannelSendAudio(channel.ChannelNumber, receivedAudio);

                        // TALK: always emit one frame per tick so NDI receivers keep getting audio packets
                        // (silence on underrun / empty mix). Otherwise Studio Monitor VU "freezes" on last sample.
                        if (channel.TalkEnabled && (audioToSend == null || audioToSend.Length == 0))
                        {
                            audioToSend = SilentMonoNdiFrame;
                        }

                        if (audioToSend != null && audioToSend.Length > 0)
                        {
                            // Send mixed audio (microphone + N-1 group audio)
                            // Always send as MONO (stereo is converted to mono in AudioEngine)
                            _ndiManager.SendAudio(channel.ChannelNumber, audioToSend, sampleRate, 1);
                        }

                        // NDI audio for LISTEN is now added directly in the receive loop above
                        // (all chunks are drained and added immediately to prevent buffer starvation)
                    }

                    frameNumber++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Silently continue
                }
            }
        }

        /// <summary>
        /// Calculate RMS level in dB from audio buffer (Float32 format)
        /// </summary>
        private float CalculateAudioLevel(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length == 0)
                return -60f;

            // Convert bytes to floats (IEEE Float32)
            int sampleCount = audioBytes.Length / 4;
            float sumSquares = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(audioBytes, i * 4);
                sumSquares += sample * sample;
            }

            float rms = (float)Math.Sqrt(sumSquares / sampleCount);

            // Convert to dB (20 * log10(rms))
            if (rms < 0.000001f) // -120dB threshold
                return -60f;

            float dB = 20f * (float)Math.Log10(rms);
            return Math.Max(dB, -60f); // Clamp to -60dB minimum
        }

        public void UpdateChannelLabel(int channelNumber, string label)
        {
            var channel = _channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.Label = label;
                channel.NdiSendName = label;
                _ndiManager.SetChannelSendName(channelNumber, label);

                // Save configuration to persist the change
                SaveCurrentConfiguration();
            }
        }

        public void SaveCurrentConfiguration()
        {
            if (_config == null)
            {
                _config = new AppConfig();
            }

            // Update channel configs from current state
            _config.Channels.Clear();
            foreach (var channel in _channels)
            {
                _config.Channels.Add(new ChannelConfig
                {
                    ChannelNumber = channel.ChannelNumber,
                    Label = channel.Label,
                    InputLevel = channel.InputLevel,
                    OutputLevel = channel.OutputLevel,
                    Mode = (int)channel.Mode,
                    IntercomGroup = channel.IntercomGroup,
                    AsioInputChannel = channel.AsioInputChannel,
                    AsioOutputChannel = channel.AsioOutputChannel,
                    NdiSendName = channel.NdiSendName ?? string.Empty,
                    NdiReceiveName = channel.NdiReceiveName ?? string.Empty
                });
            }

            ConfigManager.SaveConfig(_config);
        }

        public List<Models.NDISenderInfo> GetAllSendersInfo()
        {
            return _ndiManager.GetAllSendersInfo();
        }

        private bool _disposed;
        private readonly object _disposeLock = new object();

        public void Dispose()
        {
            // Idempotent: callable from Stop()-then-Dispose() chains and from the DI
            // container's own teardown without double-freeing native NDI handles.
            lock (_disposeLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            // 1. Stop() blocks until the audio thread has actually exited (see Stop()).
            //    Crucial: must complete before disposing the NDI manager or audio engines,
            //    otherwise the audio thread may dereference a freed native pointer.
            Stop();

            // 2. Dispose audio I/O first (microphone, speakers, ASIO) so callbacks can't
            //    fire after the NDI manager is gone.
            try { _audioEngine?.Dispose(); } catch { }
#if WINDOWS
            try { _asioManager?.Dispose(); } catch { }
#endif

            // 3. Finally dispose NDI: senders, receivers, advertisers, finder, NDIlib_destroy.
            try { _ndiManager?.Dispose(); } catch { }

            // Release the cancellation source last.
            try { _processingCancellation?.Dispose(); } catch { }
        }
    }
}
