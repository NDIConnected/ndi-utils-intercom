using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    public class NDIManager : IDisposable
    {
        private const string DefaultApplicationId = "Intercom_A";
        private static readonly Regex ApplicationIdRegex = new Regex("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);

        // Allowed values for the NDI source name suffix mode. Kept as plain strings (rather
        // than an enum) to preserve forward / backward compatibility with the JSON config:
        // an unknown or missing value silently falls back to the default.
        public const string SuffixModeFull = "Full";
        public const string SuffixModeCompact = "Compact";
        public const string SuffixModeOff = "Off";
        public const string DefaultSuffixMode = SuffixModeCompact;

        public static string SanitizeSuffixMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return DefaultSuffixMode;
            // Case-insensitive match, normalize back to canonical form.
            if (string.Equals(mode, SuffixModeFull,    StringComparison.OrdinalIgnoreCase)) return SuffixModeFull;
            if (string.Equals(mode, SuffixModeCompact, StringComparison.OrdinalIgnoreCase)) return SuffixModeCompact;
            if (string.Equals(mode, SuffixModeOff,     StringComparison.OrdinalIgnoreCase)) return SuffixModeOff;
            return DefaultSuffixMode;
        }

        // Audio/NDI Constants (internal so NDIChannel can reference them)
        internal const int AUDIO_SAMPLE_RATE = 48000;           // Standard NDI audio sample rate
        internal const int NDI_FRAME_SAMPLES = 1920;            // NDI frame size: 40ms @ 48kHz
        internal const int RING_BUFFER_SAMPLES_300MS = 14400;   // 300ms buffer @ 48kHz
        internal const int AUDIO_BUFFER_SIZE = 7680;            // Audio buffer size in bytes
        internal const int MAX_AUDIO_CHANNELS = 2;              // Max audio channels (stereo)

        private Dictionary<int, NDIChannel> _channels = new Dictionary<int, NDIChannel>();
        private IntPtr _pFinder = IntPtr.Zero;
        private IntPtr _sendAdvertiserInstance = IntPtr.Zero;
        private IntPtr _recvAdvertiserInstance = IntPtr.Zero;
        private bool _isInitialized = false;
        private int _maxChannels = 16;
        private int _webPort = 5016;
        private string _applicationId = DefaultApplicationId;
        private string _deviceId = "unknown-device";
        private string _suffixMode = DefaultSuffixMode;

        /// <summary>
        /// Initialize NDI SDK, create finder, advertisers, and <paramref name="maxChannels"/> NDI channels.
        /// Each channel creates a persistent receiver registered on the Discovery Server.
        /// </summary>
        public bool Initialize(int webPort = 5016, int maxChannels = 16)
        {
            if (!NDIWrapper.IsDllAccessible())
            {
                LogWarning("NDI runtime DLL not accessible. Is NDI Tools / NDI runtime installed?");
                return false;
            }

            try
            {
                // Check CPU support
                if (!NDIWrapper.NDIlib_is_supported_CPU())
                {
                    LogWarning("NDIlib_is_supported_CPU returned false: CPU lacks required SIMD support.");
                    return false;
                }

                // Initialize NDI
                if (!NDIWrapper.NDIlib_initialize())
                {
                    LogWarning("NDIlib_initialize returned false: NDI runtime did not start.");
                    return false;
                }

                // Create finder for discovery
                var findSettings = new NDIWrapper.NDIlib_find_create_t
                {
                    show_local_sources = true,
                    p_groups = IntPtr.Zero,
                    p_extra_ips = IntPtr.Zero
                };

                _pFinder = NDIWrapper.NDIlib_find_create_v2(ref findSettings);
                if (_pFinder == IntPtr.Zero)
                {
                    return false;
                }

                // Create SendAdvertiser for Discovery Server registration
                var advertiserSettings = new NDIWrapper.send_advertiser_create_t
                {
                    p_url_address = IntPtr.Zero  // Use default Discovery Server
                };
                _sendAdvertiserInstance = NDIWrapper.NDIlib_send_advertiser_create(ref advertiserSettings);

                // Create RecvAdvertiser for Discovery Server registration
                var recvAdvertiserSettings = new NDIWrapper.recv_advertiser_create_t
                {
                    p_url_address = IntPtr.Zero  // Use default Discovery Server
                };
                _recvAdvertiserInstance = NDIWrapper.NDIlib_recv_advertiser_create(ref recvAdvertiserSettings);

                _webPort = webPort;
                _maxChannels = maxChannels < 1 ? 1 : maxChannels;

                // Create N channels - each creates a persistent receiver on Discovery Server
                for (int i = 1; i <= _maxChannels; i++)
                {
                    _channels[i] = new NDIChannel(i, _sendAdvertiserInstance, _recvAdvertiserInstance, _applicationId, _deviceId, _webPort, _suffixMode);
                }

                _isInitialized = true;
                LogInfo($"NDIManager initialized: {_maxChannels} channels, web_port={_webPort}, app_id='{_applicationId}', device_id='{_deviceId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "NDIManager.Initialize failed");
                // Rollback any partially-allocated native resources. Without this, an
                // initialization failure (e.g. an NDIChannel ctor throws on AllocHGlobal)
                // would leak the finder + advertisers + the previously-created channels
                // because Dispose() short-circuits when _isInitialized stays false.
                RollbackPartialInitialize();
                return false;
            }
        }

        private void RollbackPartialInitialize()
        {
            // Best-effort rollback: ignore secondary failures — the goal is to release
            // native handles that were created before the throwing step.
            try
            {
                foreach (var ch in _channels.Values)
                {
                    try { ch.Dispose(); } catch { }
                }
                _channels.Clear();
            }
            catch { }

            if (_sendAdvertiserInstance != IntPtr.Zero)
            {
                try { NDIWrapper.NDIlib_send_advertiser_destroy(_sendAdvertiserInstance); } catch { }
                _sendAdvertiserInstance = IntPtr.Zero;
            }
            if (_recvAdvertiserInstance != IntPtr.Zero)
            {
                try { NDIWrapper.NDIlib_recv_advertiser_destroy(_recvAdvertiserInstance); } catch { }
                _recvAdvertiserInstance = IntPtr.Zero;
            }
            if (_pFinder != IntPtr.Zero)
            {
                try { NDIWrapper.NDIlib_find_destroy(_pFinder); } catch { }
                _pFinder = IntPtr.Zero;
            }
            // NDIlib_initialize was matched by NDIlib_destroy; only call destroy if we got
            // past the initialize check (which is the only way a partial state is possible).
            try { NDIWrapper.NDIlib_destroy(); } catch { }
        }

        public void SetChannelSendName(int channelNumber, string name)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                // Use default name if empty
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"Channel {channelNumber}";
                }

                channel.SendName = name;
                channel.RecreateSender();
                channel.RefreshReceiverName();

                if (string.IsNullOrEmpty(channel.ReceiveSource))
                {
                    channel.DisconnectReceiver();
                    return;
                }

                var source = GetSourceByName(channel.ReceiveSource);
                if (source.HasValue)
                {
                    channel.ConnectReceiver(source.Value);
                }
                else
                {
                    channel.DisconnectReceiver();
                }
            }
        }

        public void SetChannelReceiveSource(int channelNumber, string sourceName)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                channel.ReceiveSource = sourceName;

                if (string.IsNullOrEmpty(sourceName))
                {
                    // Disconnect receiver (keep it alive for Discovery Server visibility)
                    channel.DisconnectReceiver();
                }
                else
                {
                    // Connect receiver to the specified source
                    var source = GetSourceByName(sourceName);
                    if (source.HasValue)
                    {
                        channel.ConnectReceiver(source.Value);
                    }
                    else
                    {
                        // Requested source not found: keep receiver alive but disconnected.
                        channel.DisconnectReceiver();
                    }
                }
            }
        }

        public void SetIdentity(string applicationId, string deviceId)
        {
            string sanitizedApplicationId = SanitizeApplicationId(applicationId);
            string sanitizedDeviceId = SanitizeDeviceId(deviceId);
            if (string.Equals(_applicationId, sanitizedApplicationId, StringComparison.Ordinal) &&
                string.Equals(_deviceId, sanitizedDeviceId, StringComparison.Ordinal))
            {
                return;
            }

            _applicationId = sanitizedApplicationId;
            _deviceId = sanitizedDeviceId;

            foreach (var channel in _channels.Values)
            {
                channel.SetIdentity(_applicationId, _deviceId);

                if (string.IsNullOrEmpty(channel.ReceiveSource))
                {
                    channel.DisconnectReceiver();
                    continue;
                }

                var source = GetSourceByName(channel.ReceiveSource);
                if (source.HasValue)
                {
                    channel.ConnectReceiver(source.Value);
                }
                else
                {
                    channel.DisconnectReceiver();
                }
            }
        }

        public void SetApplicationId(string applicationId)
        {
            SetIdentity(applicationId, _deviceId);
        }

        /// <summary>
        /// Sets the NDI source name suffix mode (Full / Compact / Off) and propagates it to
        /// every channel, recreating senders + receivers under each channel's lifetime lock
        /// so the change is visible on the network immediately. Re-connects each receiver to
        /// its previous source after the recreate (mirrors <see cref="SetIdentity"/>).
        /// No-op if the requested mode equals the current one.
        /// </summary>
        public void SetSuffixMode(string mode)
        {
            string sanitized = SanitizeSuffixMode(mode);
            if (string.Equals(_suffixMode, sanitized, StringComparison.Ordinal))
            {
                return;
            }

            _suffixMode = sanitized;
            LogInfo($"NDI source name suffix mode changed to '{sanitized}'");

            foreach (var channel in _channels.Values)
            {
                channel.SetSuffixMode(_suffixMode);

                if (string.IsNullOrEmpty(channel.ReceiveSource))
                {
                    channel.DisconnectReceiver();
                    continue;
                }

                var source = GetSourceByName(channel.ReceiveSource);
                if (source.HasValue)
                {
                    channel.ConnectReceiver(source.Value);
                }
                else
                {
                    channel.DisconnectReceiver();
                }
            }
        }

        public static string SanitizeApplicationId(string applicationId)
        {
            string trimmed = (applicationId ?? string.Empty).Trim();
            if (ApplicationIdRegex.IsMatch(trimmed))
            {
                return trimmed;
            }

            return DefaultApplicationId;
        }

        private static string SanitizeDeviceId(string deviceId)
        {
            string trimmed = (deviceId ?? string.Empty).Trim();
            if (ApplicationIdRegex.IsMatch(trimmed))
            {
                return trimmed;
            }

            return "unknown-device";
        }

        // Static logger so NDIChannel (instantiated internally) can log without changing
        // every constructor signature. IntercomAppHost sets this at startup; until then a
        // NullLogger is used so unit tests / early init paths don't throw.
        private static ILogger _logger = NullLogger.Instance;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        internal static void LogInfo(string message) => _logger.LogInformation("{Message}", message);
        internal static void LogWarning(string message) => _logger.LogWarning("{Message}", message);
        internal static void LogError(Exception ex, string message) => _logger.LogError(ex, "{Message}", message);
        internal static void LogDebug(string message) => _logger.LogDebug("{Message}", message);

        public void SendAudio(int channelNumber, byte[] audioData, int sampleRate, int channels)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                channel.SendAudio(audioData, sampleRate, channels);
            }
        }


        public byte[] ReceiveAudio(int channelNumber)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                return channel.ReceiveAudio();
            }
            return null;
        }

        public int DrainAllToBuffer(int channelNumber)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                return channel.DrainAllToBuffer();
            }
            return 0;
        }

        public float GetChannelReceiveLevel(int channelNumber)
        {
            if (_channels.TryGetValue(channelNumber, out var channel))
            {
                return channel.ReceiveAudioLevel;
            }
            return -60f;
        }

        public List<Models.NDISenderInfo> GetAllSendersInfo()
        {
            var sendersInfo = new List<Models.NDISenderInfo>();

            foreach (var kvp in _channels.OrderBy(x => x.Key))
            {
                var channel = kvp.Value;
                var info = channel.GetSenderInfo();
                sendersInfo.Add(info);
            }

            return sendersInfo;
        }

        public List<string> DiscoverSources()
        {
            var sources = new List<string>();

            if (_pFinder == IntPtr.Zero)
                return sources;

            try
            {
                // Wait for sources
                NDIWrapper.NDIlib_find_wait_for_sources(_pFinder, 1000);

                // Get current sources
                uint numSources = 0;
                IntPtr pSources = NDIWrapper.NDIlib_find_get_current_sources(_pFinder, ref numSources);

                if (pSources != IntPtr.Zero && numSources > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(NDIWrapper.NDIlib_source_t));

                    for (int i = 0; i < numSources; i++)
                    {
                        IntPtr pSource = IntPtr.Add(pSources, i * structSize);
                        var source = Marshal.PtrToStructure<NDIWrapper.NDIlib_source_t>(pSource);

                        if (source.p_ndi_name != IntPtr.Zero)
                        {
                            string sourceName = NdiNativeStrings.PtrToStringUtf8(source.p_ndi_name);
                            if (!string.IsNullOrEmpty(sourceName))
                            {
                                sources.Add(sourceName);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently continue
            }

            return sources;
        }

        private NDIWrapper.NDIlib_source_t? GetSourceByName(string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName) || _pFinder == IntPtr.Zero)
                return null;

            try
            {
                uint numSources = 0;
                IntPtr pSources = NDIWrapper.NDIlib_find_get_current_sources(_pFinder, ref numSources);

                if (pSources != IntPtr.Zero && numSources > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(NDIWrapper.NDIlib_source_t));

                    for (int i = 0; i < numSources; i++)
                    {
                        IntPtr pSource = IntPtr.Add(pSources, i * structSize);
                        var source = Marshal.PtrToStructure<NDIWrapper.NDIlib_source_t>(pSource);

                        if (source.p_ndi_name != IntPtr.Zero)
                        {
                            string name = NdiNativeStrings.PtrToStringUtf8(source.p_ndi_name);
                            if (name == sourceName)
                            {
                                return source;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently continue
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }
            _channels.Clear();

            if (_sendAdvertiserInstance != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_send_advertiser_destroy(_sendAdvertiserInstance);
                _sendAdvertiserInstance = IntPtr.Zero;
            }

            if (_recvAdvertiserInstance != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_recv_advertiser_destroy(_recvAdvertiserInstance);
                _recvAdvertiserInstance = IntPtr.Zero;
            }

            if (_pFinder != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_find_destroy(_pFinder);
                _pFinder = IntPtr.Zero;
            }

            if (_isInitialized)
            {
                NDIWrapper.NDIlib_destroy();
                _isInitialized = false;
            }
        }
    }

    internal class NDIChannel : IDisposable
    {
        // Stripping regex: matches BOTH the Full-mode bracket suffix
        //   "<friendly> [app=...;device=...;role=sender|receiver;ch=N]"
        // and the Compact-mode parens suffix
        //   "<friendly> (Intercom_A)"
        // Stripping is necessary so a runtime mode change doesn't accumulate suffixes on the
        // friendly name. The Compact regex is intentionally tight: it only matches a trailing
        // " (...)" whose content matches the ApplicationId regex, so user-friendly names that
        // legitimately end with parens (e.g. "Channel 1 (Studio A)") are NOT stripped.
        private static readonly Regex IdentitySuffixRegex = new Regex(
            @"\s*(\[app=[^;\]]+;device=[^;\]]+;role=(sender|receiver);ch=\d+\]|\([A-Za-z0-9_.\-]{1,64}\))\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public int ChannelNumber { get; }
        public string SendName { get; set; }
        public string ReceiveSource { get; set; }
        private string _applicationId;
        private string _deviceId;
        private string _suffixMode;

        private IntPtr _senderInstance = IntPtr.Zero;
        private IntPtr _receiverInstance = IntPtr.Zero;
        private IntPtr _sendAdvertiser = IntPtr.Zero;
        private IntPtr _recvAdvertiser = IntPtr.Zero;

        // Synchronizes lifecycle (create/destroy) vs usage (send/recv/connect) of native NDI handles.
        // Audio threads take this lock for the duration of native calls; reconfigure/recreate paths
        // also take it so they cannot free a pointer that another thread is currently dereferencing.
        // The lock is per-channel so different channels never contend with each other.
        private readonly object _ndiLifetimeLock = new object();

        // Web server port for ndi_capabilities web_control URL
        private readonly int _webPort;

        // PING-PONG BUFFERS: Pre-allocated persistent buffers to avoid continuous alloc/free
        private IntPtr _audioBuffer1 = IntPtr.Zero;
        private IntPtr _audioBuffer2 = IntPtr.Zero;
        private bool _useBuffer1 = true;
        private int _audioBufferSize = 0;

        // NDI receive audio level (RMS in dB)
        private float _receiveAudioLevel = -60f;
        public float ReceiveAudioLevel => _receiveAudioLevel;

        // Ring buffer for NDI receiver (300ms = 14400 samples @ 48kHz mono)
        // This absorbs burst arrivals and provides smooth consumption
        private AudioRingBuffer _receiveRingBuffer;
        private readonly object _receiveBufferLock = new object();

        // Receiver name allocated once (freed on Dispose)
        private IntPtr _pRecvName = IntPtr.Zero;

        public NDIChannel(int channelNumber, IntPtr sendAdvertiser, IntPtr recvAdvertiser, string applicationId, string deviceId, int webPort = 5016, string suffixMode = NDIManager.DefaultSuffixMode)
        {
            ChannelNumber = channelNumber;
            SendName = $"Channel {channelNumber}";
            _sendAdvertiser = sendAdvertiser;
            _recvAdvertiser = recvAdvertiser;
            _applicationId = NDIManager.SanitizeApplicationId(applicationId);
            _deviceId = SanitizeDeviceId(deviceId);
            _webPort = webPort;
            _suffixMode = NDIManager.SanitizeSuffixMode(suffixMode);

            // Pre-allocate ping-pong buffers for audio (NDI standard frame)
            _audioBufferSize = NDIManager.NDI_FRAME_SAMPLES * NDIManager.MAX_AUDIO_CHANNELS * sizeof(short);

            _audioBuffer1 = Marshal.AllocHGlobal(_audioBufferSize);
            _audioBuffer2 = Marshal.AllocHGlobal(_audioBufferSize);

            // Initialize receive ring buffer (300ms @ 48kHz mono)
            _receiveRingBuffer = new AudioRingBuffer(NDIManager.RING_BUFFER_SAMPLES_300MS);

            // Create the receiver immediately (unconnected) so it is always visible
            // on the Discovery Server for monitoring and remote control
            CreatePersistentReceiver();
        }

        public void SetIdentity(string applicationId, string deviceId)
        {
            string sanitizedApplicationId = NDIManager.SanitizeApplicationId(applicationId);
            string sanitizedDeviceId = SanitizeDeviceId(deviceId);
            lock (_ndiLifetimeLock)
            {
                if (string.Equals(_applicationId, sanitizedApplicationId, StringComparison.Ordinal) &&
                    string.Equals(_deviceId, sanitizedDeviceId, StringComparison.Ordinal))
                {
                    return;
                }

                _applicationId = sanitizedApplicationId;
                _deviceId = sanitizedDeviceId;
                RecreateSenderLocked();
                RecreateReceiverLocked();
            }
        }

        /// <summary>
        /// Updates the suffix mode for this channel and recreates the native sender +
        /// receiver under the lifetime lock so the new name is published immediately.
        /// </summary>
        public void SetSuffixMode(string suffixMode)
        {
            string sanitized = NDIManager.SanitizeSuffixMode(suffixMode);
            lock (_ndiLifetimeLock)
            {
                if (string.Equals(_suffixMode, sanitized, StringComparison.Ordinal))
                {
                    return;
                }
                _suffixMode = sanitized;
                RecreateSenderLocked();
                RecreateReceiverLocked();
            }
        }

        /// <summary>
        /// Creates an unconnected NDI receiver with a descriptive name and registers it
        /// on the Discovery Server via RecvAdvertiser. The receiver persists for the
        /// lifetime of this channel and uses NDIlib_recv_connect to change sources.
        /// Caller must hold <see cref="_ndiLifetimeLock"/> (or be in the constructor before
        /// any other thread can observe this instance).
        /// </summary>
        private void CreatePersistentReceiver()
        {
            try
            {
                // Free any previous receiver name to avoid leaking unmanaged memory
                // if this method is invoked more than once during the channel's life.
                if (_pRecvName != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_pRecvName);
                    _pRecvName = IntPtr.Zero;
                }

                // Allocate receiver name (persists until Dispose)
                string recvName = BuildReceiverName();
                _pRecvName = Marshal.StringToHGlobalAnsi(recvName);

                // Create receiver without connecting to any source
                var emptySource = new NDIWrapper.NDIlib_source_t
                {
                    p_ndi_name = IntPtr.Zero,
                    p_url_address = IntPtr.Zero
                };

                var recvSettings = new NDIWrapper.NDIlib_recv_create_v3_t
                {
                    source_to_connect_to = emptySource,
                    color_format = NDIWrapper.NDIlib_recv_color_format_fastest,
                    bandwidth = NDIWrapper.NDIlib_recv_bandwidth_audio_only,
                    allow_video_fields = false,
                    p_ndi_recv_name = _pRecvName
                };

                _receiverInstance = NDIWrapper.NDIlib_recv_create_v3(ref recvSettings);

                if (_receiverInstance != IntPtr.Zero)
                {
                    // Add product metadata to receiver
                    AddReceiverConnectionMetadata();

                    // Register on Discovery Server with monitoring + control enabled.
                    // p_input_group_name is intentionally null to keep global discoverability.
                    if (_recvAdvertiser != IntPtr.Zero)
                    {
                        NDIWrapper.NDIlib_recv_advertiser_add_receiver(
                            _recvAdvertiser, _receiverInstance,
                            true,   // allow_controlling: NDI apps can connect/disconnect this receiver
                            true,   // allow_monitoring: NDI apps can monitor events
                            null);
                    }

                    NDIManager.LogInfo(
                        $"Receiver created: name='{recvName}', app_id='{_applicationId}', device_id='{_deviceId}', channel='ch-{ChannelNumber}', groups disabled");
                }
                else
                {
                    NDIManager.LogWarning($"Receiver creation failed: NDIlib_recv_create_v3 returned NULL for channel {ChannelNumber}");
                }
            }
            catch (Exception ex)
            {
                NDIManager.LogError(ex, $"CreatePersistentReceiver failed for channel {ChannelNumber}");
            }
        }

        /// <summary>
        /// Connect the persistent receiver to an NDI source.
        /// Does NOT destroy/recreate the receiver, preserving Discovery Server registration.
        /// </summary>
        public void ConnectReceiver(NDIWrapper.NDIlib_source_t source)
        {
            lock (_ndiLifetimeLock)
            {
                if (_receiverInstance == IntPtr.Zero)
                    return;

                try
                {
                    NDIWrapper.NDIlib_recv_connect(_receiverInstance, ref source);
                }
                catch (Exception)
                {
                    // Silently continue
                }
            }
        }

        /// <summary>
        /// Disconnect the persistent receiver from its current source.
        /// The receiver remains alive and visible on the Discovery Server.
        /// </summary>
        public void DisconnectReceiver()
        {
            lock (_ndiLifetimeLock)
            {
                if (_receiverInstance == IntPtr.Zero)
                    return;

                try
                {
                    NDIWrapper.NDIlib_recv_connect(_receiverInstance, IntPtr.Zero);
                }
                catch (Exception)
                {
                    // Silently continue
                }
            }

            // Reset the cached level so the VU meter doesn't keep showing the last RMS
            // value of the previous source after the operator hits "disconnect" — UX bug
            // that made it look like audio was still flowing.
            _receiveAudioLevel = -60f;
        }

        public void RecreateSender()
        {
            lock (_ndiLifetimeLock)
            {
                RecreateSenderLocked();
            }
        }

        private void RecreateSenderLocked()
        {
            DestroySenderLocked();

            // Ensure SendName is not empty
            if (string.IsNullOrWhiteSpace(SendName))
            {
                SendName = $"Channel {ChannelNumber}";
            }

            try
            {
                string senderNdiName = BuildSenderName();
                IntPtr pName = IntPtr.Zero;
                try
                {
                    pName = Marshal.StringToHGlobalAnsi(senderNdiName);

                    var sendSettings = new NDIWrapper.NDIlib_send_create_t
                    {
                        p_ndi_name = pName,
                        p_groups = IntPtr.Zero,
                        clock_video = false,
                        clock_audio = true  // Use audio as timing reference for better sync
                    };

                    _senderInstance = NDIWrapper.NDIlib_send_create(ref sendSettings);
                }
                finally
                {
                    if (pName != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pName);
                    }
                }

                if (_senderInstance != IntPtr.Zero)
                {
                    // Add product metadata + capabilities to sender
                    AddSenderConnectionMetadata();
                    AddSenderManagerMetadata();
                    AddSenderCapabilities();

                    // Register sender on Discovery Server for monitoring
                    if (_sendAdvertiser != IntPtr.Zero)
                    {
                        NDIWrapper.NDIlib_send_advertiser_add_sender(_sendAdvertiser, _senderInstance, true);
                    }

                    NDIManager.LogInfo(
                        $"Sender created: ndi_name='{senderNdiName}', app_id='{_applicationId}', device_id='{_deviceId}', channel='ch-{ChannelNumber}', groups disabled");
                }
                else
                {
                    NDIManager.LogWarning($"Sender creation failed: NDIlib_send_create returned NULL for channel {ChannelNumber}");
                }
            }
            catch (Exception ex)
            {
                NDIManager.LogError(ex, $"RecreateSenderLocked failed for channel {ChannelNumber}");
            }
        }

        private void RecreateReceiverLocked()
        {
            DestroyReceiverLocked();
            CreatePersistentReceiver();
        }

        public void RefreshReceiverName()
        {
            lock (_ndiLifetimeLock)
            {
                RecreateReceiverLocked();
            }
        }

        private string BuildNdiProductMetadataXml(string sessionName)
        {
            var p = IntercomRuntime.Product;
            string longName = EscapeXml(p.NdiProductLongName);
            string shortName = EscapeXml(p.NdiProductShortName);
            string version = EscapeXml(p.NdiProductVersion);
            string model = EscapeXml(p.NdiModelName);
            string session = EscapeXml(sessionName);
            return "<ndi_product " +
                $"long_name=\"{longName}\" " +
                $"short_name=\"{shortName}\" " +
                "manufacturer=\"NDI\" " +
                $"version=\"{version}\" " +
                $"model_name=\"{model}\" " +
                $"serial=\"IC-{ChannelNumber.ToString("D2")}\" " +
                $"session_name=\"{session}\" />";
        }

        /// <summary>
        /// Adds ndi_product connection metadata to the sender.
        /// This identifies the product to any connected receivers.
        /// </summary>
        private void AddSenderConnectionMetadata()
        {
            if (_senderInstance == IntPtr.Zero)
                return;

            try
            {
                string metadataXml = BuildNdiProductMetadataXml(BuildSenderName());
                RegisterMetadata(_senderInstance, metadataXml, isSender: true);
            }
            catch (Exception)
            {
                // Silently continue
            }
        }

        private void AddSenderManagerMetadata()
        {
            if (_senderInstance == IntPtr.Zero)
            {
                NDIManager.LogWarning($"Sender metadata unavailable: channel={ChannelNumber}, reason=sender_instance_null");
                return;
            }

            string metadataXml = BuildSenderManagerMetadataXml();
            if (string.IsNullOrWhiteSpace(metadataXml))
            {
                NDIManager.LogWarning($"Sender metadata unavailable: channel={ChannelNumber}, reason=empty_metadata_xml");
                return;
            }

            try
            {
                RegisterMetadata(_senderInstance, metadataXml, isSender: true);
            }
            catch (Exception ex)
            {
                NDIManager.LogWarning($"Sender metadata registration failed: channel={ChannelNumber}, error={ex.Message}");
                return;
            }

            NDIManager.LogInfo(
                $"Sender metadata: ndi_name='{BuildSenderName()}', xml='{metadataXml}', app_id='{_applicationId}', device_id='{_deviceId}', channel='ch-{ChannelNumber}', groups disabled");
        }

        /// <summary>
        /// Adds ndi_capabilities to the sender so NDI apps (e.g. Studio Monitor)
        /// can offer a link to the intercom's web interface.
        /// The %IP% token is replaced by the SDK with the correct local IP.
        /// </summary>
        private void AddSenderCapabilities()
        {
            if (_senderInstance == IntPtr.Zero)
                return;

            try
            {
                string capabilitiesXml = $"<ndi_capabilities web_control=\"http://%IP%:{_webPort}/\" />";
                RegisterMetadata(_senderInstance, capabilitiesXml, isSender: true);
            }
            catch (Exception)
            {
                // Silently continue
            }
        }

        /// <summary>
        /// Adds ndi_product connection metadata to the receiver.
        /// This identifies the product to any connected senders.
        /// </summary>
        private void AddReceiverConnectionMetadata()
        {
            if (_receiverInstance == IntPtr.Zero)
                return;

            try
            {
                string metadataXml = BuildNdiProductMetadataXml(BuildReceiverName());
                RegisterMetadata(_receiverInstance, metadataXml, isSender: false);
            }
            catch (Exception)
            {
                // Silently continue
            }
        }

        private string BuildSenderManagerMetadataXml()
        {
            string appId = EscapeXml(_applicationId);
            string deviceId = EscapeXml(_deviceId);
            return $"<ndi_manager app_id=\"{appId}\" device_id=\"{deviceId}\" channel_id=\"ch-{ChannelNumber}\" channel_index=\"{ChannelNumber}\" role=\"sender\" schema=\"1\" />";
        }

        private string BuildReceiverName()
        {
            return BuildEndpointName(SendName, "receiver");
        }

        private string BuildSenderName()
        {
            return BuildEndpointName(SendName, "sender");
        }

        private string BuildEndpointName(string friendlyName, string role)
        {
            string normalizedFriendlyName = (friendlyName ?? string.Empty).Trim();
            // Strip any previous suffix (Full or Compact) so a runtime mode change can't
            // accumulate "[…] (App)" tails on the friendly name.
            normalizedFriendlyName = IdentitySuffixRegex.Replace(normalizedFriendlyName, string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedFriendlyName))
            {
                normalizedFriendlyName = string.Equals(role, "receiver", StringComparison.OrdinalIgnoreCase)
                    ? $"Intercom RX ch-{ChannelNumber}"
                    : $"Channel {ChannelNumber}";
            }

            // Three suffix modes (see AppConfig.IdentitySuffixMode for full rationale):
            //   Full    → maximum identity in the name; can be parsed by management apps
            //              that don't read connection metadata, but legacy / embedded NDI
            //              receivers may reject this complex name.
            //   Compact → "(application_id)" parens hint, NDI-tool-style. Works with all
            //              clients while still disambiguating multiple Intercom instances.
            //   Off     → pure friendly name; for receivers that fail with any extra text.
            //
            // Note: in every mode the <ndi_manager> connection metadata XML is unchanged,
            // so a management application that reads metadata can still aggregate by
            // (application_id, device_id) regardless of the suffix mode.
            return _suffixMode switch
            {
                NDIManager.SuffixModeOff     => normalizedFriendlyName,
                NDIManager.SuffixModeCompact => $"{normalizedFriendlyName} ({_applicationId})",
                _ /* Full */                 => $"{normalizedFriendlyName} [app={_applicationId};device={_deviceId};role={role};ch={ChannelNumber}]"
            };
        }

        private static string EscapeXml(string value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }

        private static string SanitizeDeviceId(string deviceId)
        {
            string trimmed = (deviceId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "unknown-device";
            }

            return trimmed.Length > 64 ? trimmed.Substring(0, 64) : trimmed;
        }

        /// <summary>
        /// Helper to register an XML metadata string on a sender or receiver instance.
        /// </summary>
        private void RegisterMetadata(IntPtr instance, string metadataXml, bool isSender)
        {
            byte[] metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataXml + "\0");
            IntPtr pMetadata = Marshal.AllocHGlobal(metadataBytes.Length);
            try
            {
                Marshal.Copy(metadataBytes, 0, pMetadata, metadataBytes.Length);

                var metadataFrame = new NDIWrapper.NDIlib_metadata_frame_t
                {
                    length = metadataBytes.Length - 1,
                    timecode = NDIWrapper.NDIlib_send_timecode_synthesize,
                    p_data = pMetadata
                };

                if (isSender)
                    NDIWrapper.NDIlib_send_add_connection_metadata(instance, ref metadataFrame);
                else
                    NDIWrapper.NDIlib_recv_add_connection_metadata(instance, ref metadataFrame);
            }
            finally
            {
                Marshal.FreeHGlobal(pMetadata);
            }

        }

        public void SendAudio(byte[] audioData, int sampleRate, int channels)
        {
            if (audioData == null || audioData.Length == 0)
            {
                return;
            }

            // Acquire the lifetime lock for the duration of the native send call so the
            // sender pointer cannot be destroyed (e.g. by a concurrent RecreateSender)
            // while the SDK is dereferencing it. The lock is per-channel; ASIO/NDI
            // threads on different channels never contend.
            lock (_ndiLifetimeLock)
            {
                if (_senderInstance == IntPtr.Zero)
                {
                    return;
                }

            try
            {
                // OPTIMIZED: Convert byte[] (float32) to short[] (int16) in one pass
                int floatSampleCount = audioData.Length / 4;
                short[] samples16 = new short[floatSampleCount];

                // Use unsafe code for FAST direct memory access (eliminates intermediate array)
                unsafe
                {
                    fixed (byte* pAudioData = audioData)
                    fixed (short* pSamples16 = samples16)
                    {
                        float* pFloat = (float*)pAudioData;

                        // Single-pass conversion with direct pointer access
                        for (int i = 0; i < floatSampleCount; i++)
                        {
                            float value = pFloat[i];
                            // Clamp to [-1.0, 1.0] then convert to [-32768, 32767]
                            value = Math.Max(-1.0f, Math.Min(1.0f, value));
                            pSamples16[i] = (short)(value * 32767f);
                        }
                    }
                }

                // USE PING-PONG BUFFERS (pre-allocated, no alloc/free overhead!)
                IntPtr pCurrentBuffer = _useBuffer1 ? _audioBuffer1 : _audioBuffer2;
                _useBuffer1 = !_useBuffer1; // Alternate for next frame

                int dataSize = samples16.Length * sizeof(short);

                // Check buffer size (should always fit, but safety check)
                if (dataSize > _audioBufferSize)
                {
                    return;
                }

                // Copy to persistent buffer (no allocation needed!)
                Marshal.Copy(samples16, 0, pCurrentBuffer, samples16.Length);

                int samplesPerChannel = floatSampleCount / channels;

                // Create NDI audio frame using 16-bit utility function
                var audioFrame = new NDIWrapper.NDIlib_audio_frame_interleaved_16s_t
                {
                    sample_rate = sampleRate,
                    no_channels = channels,
                    no_samples = samplesPerChannel,  // Samples PER channel
                    timecode = NDIWrapper.NDIlib_send_timecode_synthesize,
                    reference_level = 0,  // 0 dB reference
                    p_data = pCurrentBuffer  // Use persistent ping-pong buffer!
                };

                // Send audio using 16-bit utility function
                NDIWrapper.NDIlib_util_send_send_audio_interleaved_16s(_senderInstance, ref audioFrame);

                // NO FREE! Buffer is persistent and reused (ping-pong pattern)
            }
            catch (Exception)
            {
                // Silently continue
            }
            } // _ndiLifetimeLock
        }

        /// <summary>
        /// Reads audio from the ring buffer (BUFFERED MODE)
        /// Call DrainAllToBuffer() before this to fill the buffer
        /// </summary>
        public byte[] ReceiveAudio(int sampleCount = NDIManager.NDI_FRAME_SAMPLES)
        {
            // ReceiveAudio reads only from the managed ring buffer (filled by DrainAllToBuffer);
            // it does not touch native NDI handles, so no lifetime lock is required here.
            try
            {
                byte[] audioData = null;

                // Read from ring buffer
                lock (_receiveBufferLock)
                {
                    audioData = _receiveRingBuffer.Read(sampleCount);
                }

                if (audioData == null)
                {
                    // Buffer underrun - no audio available
                    _receiveAudioLevel = -60f;
                }

                return audioData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Drains ALL available NDI audio chunks into the ring buffer (called before ReceiveAudio)
        /// This absorbs burst arrivals and prevents packet drops
        /// </summary>
        public int DrainAllToBuffer()
        {
            int chunksDrained = 0;

            // Acquire the lifetime lock for the duration of the native receive calls so the
            // receiver pointer cannot be destroyed (e.g. by a concurrent RecreateReceiver) while
            // the SDK is dereferencing it.
            lock (_ndiLifetimeLock)
            {
                if (_receiverInstance == IntPtr.Zero)
                    return 0;

            try
            {
                // Drain all available chunks (burst mode)
                while (true)
                {
                    var audioFrame = new NDIWrapper.NDIlib_audio_frame_v3_t();

                    // Try to capture audio (non-blocking, 0ms timeout)
                    var frameType = NDIWrapper.NDIlib_recv_capture_v3(
                        _receiverInstance,
                        IntPtr.Zero,
                        ref audioFrame,
                        IntPtr.Zero,
                        0);

                    if (frameType != NDIWrapper.NDIlib_frame_type_e.NDIlib_frame_type_audio)
                        break; // No more audio available

                    if (audioFrame.p_data == IntPtr.Zero || audioFrame.no_samples == 0)
                    {
                        NDIWrapper.NDIlib_recv_free_audio_v3(_receiverInstance, ref audioFrame);
                        break;
                    }

                    // NDI v3 audio is delivered in PLANAR layout by default (FLTP, FourCC=0):
                    //   buffer = [L0,L1,...,Ln-1] padding [R0,R1,...,Rn-1] padding ...
                    // The byte distance between the start of channel k and channel k+1 is
                    // `channel_stride_in_bytes` (NOT `no_samples * sizeof(float)`, because the
                    // SDK may insert padding for SIMD alignment). The previous code assumed
                    // INTERLEAVED layout and indexed `samples[i*2] / samples[i*2+1]`, which on
                    // a stereo planar frame produced a 2:1 decimation of the L channel
                    // followed by a 2:1 decimation of the R channel, played back at the same
                    // sample rate — symptoms: pitch shifted up one octave + metallic aliasing.
                    int channels = audioFrame.no_channels;
                    int frames   = audioFrame.no_samples;

                    // Snapshot the FourCC so we know whether the frame is planar or interleaved.
                    // Default in NDI v3 (and what every modern NDI sender we tested produces) is
                    // FLTP (planar). The NDIWrapper struct already documents this requirement
                    // ("Must be NDIlib_FourCC_audio_type_FLTP = 0"). We treat any value other
                    // than the explicit interleaved code as planar.
                    bool isInterleaved = audioFrame.FourCC == NDIWrapper.NDIlib_FourCC_audio_type_FLTp;

                    // Stride in floats between two channel planes, computed BEFORE freeing the
                    // frame (afterwards the struct fields are stale).
                    int strideFloats = isInterleaved
                        ? 0
                        : Math.Max(frames, audioFrame.channel_stride_in_bytes / sizeof(float));

                    // Copy enough float data to cover the whole logical buffer:
                    // - interleaved: frames * channels floats
                    // - planar:      strideFloats * (channels - 1) + frames floats
                    //   (i.e. up to the last sample of the last channel plane)
                    int totalFloatsToCopy = isInterleaved
                        ? frames * channels
                        : strideFloats * (channels - 1) + frames;

                    float[] samples = new float[totalFloatsToCopy];
                    Marshal.Copy(audioFrame.p_data, samples, 0, totalFloatsToCopy);

                    // Free the audio frame ASAP
                    NDIWrapper.NDIlib_recv_free_audio_v3(_receiverInstance, ref audioFrame);

                    // Convert to MONO. In every branch we produce exactly `frames` mono samples,
                    // which is the actual sample count per channel — what the ring buffer must
                    // ingest at the original sample_rate, with NO pitch / time distortion.
                    float[] monoSamples;
                    if (channels == 1)
                    {
                        monoSamples = samples;
                    }
                    else if (channels == 2)
                    {
                        monoSamples = new float[frames];
                        if (isInterleaved)
                        {
                            // [L0,R0,L1,R1,...]
                            for (int i = 0; i < frames; i++)
                            {
                                monoSamples[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
                            }
                        }
                        else
                        {
                            // Planar: L plane at [0..frames), R plane at [strideFloats..)
                            for (int i = 0; i < frames; i++)
                            {
                                monoSamples[i] = (samples[i] + samples[i + strideFloats]) * 0.5f;
                            }
                        }
                    }
                    else
                    {
                        // > 2 channels: take channel 0 (matches the previous behavior — for an
                        // intercom path averaging 5.1 / 7.1 into mono is rarely what the user
                        // wants; channel 0 / front-left is a safe pick).
                        monoSamples = new float[frames];
                        if (isInterleaved)
                        {
                            for (int i = 0; i < frames; i++)
                            {
                                monoSamples[i] = samples[i * channels];
                            }
                        }
                        else
                        {
                            // Planar: channel 0 lives at [0..frames)
                            Array.Copy(samples, 0, monoSamples, 0, frames);
                        }
                    }

                    // Calculate RMS level
                    float sum = 0;
                    for (int i = 0; i < monoSamples.Length; i++)
                    {
                        sum += monoSamples[i] * monoSamples[i];
                    }
                    float rms = (float)Math.Sqrt(sum / monoSamples.Length);
                    _receiveAudioLevel = rms > 0.00001f ? (float)(20 * Math.Log10(rms)) : -60f;

                    // Convert to byte[]
                    byte[] audioData = new byte[monoSamples.Length * sizeof(float)];
                    Buffer.BlockCopy(monoSamples, 0, audioData, 0, audioData.Length);

                    // Write to ring buffer
                    lock (_receiveBufferLock)
                    {
                        _receiveRingBuffer.Write(audioData);
                    }

                    chunksDrained++;
                }
            }
            catch (Exception)
            {
                // Silently continue
            }
            } // _ndiLifetimeLock

            return chunksDrained;
        }

        // Caller must hold _ndiLifetimeLock.
        private void DestroySenderLocked()
        {
            if (_senderInstance != IntPtr.Zero)
            {
                try
                {
                    // Remove from Discovery Server before destroying
                    if (_sendAdvertiser != IntPtr.Zero)
                    {
                        NDIWrapper.NDIlib_send_advertiser_del_sender(_sendAdvertiser, _senderInstance);
                    }

                    NDIWrapper.NDIlib_send_destroy(_senderInstance);
                    _senderInstance = IntPtr.Zero;
                }
                catch (Exception)
                {
                    // Silently continue
                }
            }
        }

        /// <summary>
        /// Destroys the persistent receiver and removes it from Discovery Server.
        /// Caller must hold <see cref="_ndiLifetimeLock"/>.
        /// </summary>
        private void DestroyReceiverLocked()
        {
            if (_receiverInstance != IntPtr.Zero)
            {
                try
                {
                    // Remove from Discovery Server before destroying
                    if (_recvAdvertiser != IntPtr.Zero)
                    {
                        NDIWrapper.NDIlib_recv_advertiser_del_receiver(_recvAdvertiser, _receiverInstance);
                    }

                    NDIWrapper.NDIlib_recv_destroy(_receiverInstance);
                    _receiverInstance = IntPtr.Zero;
                }
                catch (Exception)
                {
                    // Silently continue
                }
            }

            // Free the receiver name
            if (_pRecvName != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pRecvName);
                _pRecvName = IntPtr.Zero;
            }
        }

        public Models.NDISenderInfo GetSenderInfo()
        {
            var info = new Models.NDISenderInfo
            {
                ChannelNumber = ChannelNumber,
                SenderName = SendName ?? $"Channel {ChannelNumber}",
                ConnectionCount = 0,
                OnProgram = false,
                OnPreview = false,
                IntercomGroup = null
            };

            // Acquire the lifetime lock so the sender pointer can't be freed while we
            // call NDIlib_send_get_* on it.
            lock (_ndiLifetimeLock)
            {
                info.IsActive = _senderInstance != IntPtr.Zero;

                if (_senderInstance != IntPtr.Zero)
                {
                    try
                    {
                        // Get number of active connections
                        info.ConnectionCount = NDIWrapper.NDIlib_send_get_no_connections(_senderInstance, 0);

                        // Get tally status
                        var tally = new NDIWrapper.NDIlib_tally_t();
                        if (NDIWrapper.NDIlib_send_get_tally(_senderInstance, ref tally, 0))
                        {
                            info.OnProgram = tally.on_program;
                            info.OnPreview = tally.on_preview;
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }

            return info;
        }

        public void Dispose()
        {
            lock (_ndiLifetimeLock)
            {
                DestroySenderLocked();
                DestroyReceiverLocked();

                // Free ping-pong buffers
                if (_audioBuffer1 != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_audioBuffer1);
                    _audioBuffer1 = IntPtr.Zero;
                }

                if (_audioBuffer2 != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_audioBuffer2);
                    _audioBuffer2 = IntPtr.Zero;
                }
            }
        }
    }
}
