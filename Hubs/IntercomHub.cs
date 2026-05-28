using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR;
using NDIIntercom.Core;
using NDIIntercom.Models;

namespace NDIIntercom.Hubs
{
    public class IntercomHub : Hub
    {
        private readonly IntercomEngine _intercomEngine;

        public IntercomHub(IntercomEngine intercomEngine)
        {
            _intercomEngine = intercomEngine;
        }

        public async Task<List<ChannelState>> GetChannels()
        {
            return _intercomEngine.GetChannels();
        }

        public async Task ToggleTalk(int channelNumber)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.TalkEnabled = !channel.TalkEnabled;
                _intercomEngine.UpdateAsioChannelStates();
                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        public async Task ToggleListen(int channelNumber)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.ListenEnabled = !channel.ListenEnabled;
                _intercomEngine.UpdateAsioChannelStates();
                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        public async Task SetInputLevel(int channelNumber, int level)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.InputLevel = level;

                // Save configuration to persist the change
                _intercomEngine.SaveCurrentConfiguration();

                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        public async Task SetOutputLevel(int channelNumber, int level)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.OutputLevel = level;

                // Save configuration to persist the change
                _intercomEngine.SaveCurrentConfiguration();

                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        public async Task SetIntercomGroup(int channelNumber, int group)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.IntercomGroup = group;

                // Save configuration to persist the change
                _intercomEngine.SaveCurrentConfiguration();

                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        public async Task UpdateChannelLabel(int channelNumber, string label)
        {
            _intercomEngine.UpdateChannelLabel(channelNumber, label);
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

#if WINDOWS
        public async Task<List<string>> GetAsioDevices()
        {
            return _intercomEngine.GetAvailableAsioDevices();
        }

        public async Task<bool> InitializeAsioDevice(string deviceName)
        {
            bool success = _intercomEngine.InitializeAsioDevice(deviceName);
            if (!success)
            {
                string error = _intercomEngine.GetAsioLastError() ?? "Unknown error";
                throw new HubException($"ASIO init failed: {error}");
            }
            return true;
        }

        public async Task<Dictionary<string, int>> GetAsioChannelCounts()
        {
            return new Dictionary<string, int>
            {
                { "inputChannels", _intercomEngine.GetAsioInputChannelCount() },
                { "outputChannels", _intercomEngine.GetAsioOutputChannelCount() }
            };
        }

        public async Task SetAsioChannels(int channelNumber, int inputChannel, int outputChannel)
        {
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.AsioInputChannel = inputChannel;
                channel.AsioOutputChannel = outputChannel;
                _intercomEngine.SaveCurrentConfiguration();
                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }
#endif

        public async Task SetChannelMode(int channelNumber, int mode)
        {
#if !WINDOWS
            if (mode == (int)ChannelMode.ASIO)
            {
                mode = (int)ChannelMode.NDI;
            }
#endif
            var channels = _intercomEngine.GetChannels();
            var channel = channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
            if (channel != null)
            {
                channel.Mode = (ChannelMode)mode;
                _intercomEngine.SaveCurrentConfiguration();
                await Clients.All.SendAsync("ChannelUpdated", channel);
            }
        }

        // Backward-compat aliases: both map to the unified SetIntercomGroup
        public async Task SetAsioIntercomGroup(int channelNumber, int group)
        {
            await SetIntercomGroup(channelNumber, group);
        }

        public async Task SetNdiIntercomGroup(int channelNumber, int group)
        {
            await SetIntercomGroup(channelNumber, group);
        }

        public Task<AppConfig> GetConfiguration()
        {
            return Task.FromResult(ConfigManager.LoadConfig());
        }

        public Task<IntercomProductInfoMessage> GetProductInfo()
        {
            var p = IntercomRuntime.Product;
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return Task.FromResult(new IntercomProductInfoMessage
            {
                MaxChannels = p.MaxChannels,
                ProductDisplayName = p.ProductDisplayName,
                UiTitleShort = p.UiTitleShort,
                AsioAvailable = windows,
                AudioBackendName = windows ? "WASAPI" : "PipeWire / PulseAudio"
            });
        }

        public async Task<List<AudioDeviceInfo>> GetInputDevices()
        {
            return _intercomEngine.GetInputDevices();
        }

        public async Task<List<AudioDeviceInfo>> GetOutputDevices()
        {
            return _intercomEngine.GetOutputDevices();
        }

        public async Task<List<string>> GetNDISources()
        {
            return _intercomEngine.GetAvailableNDISources();
        }

        public async Task ApplyConfiguration(AppConfig config)
        {
            config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
            ConfigManager.SaveConfig(config);
            _intercomEngine.ApplyConfig(config);
            await Clients.All.SendAsync("ConfigurationApplied");
        }

        // Export/Import Configuration
        public async Task ExportConfiguration(string filePath)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                ConfigManager.ExportConfig(config, filePath);
                await Clients.Caller.SendAsync("ConfigurationExported", filePath);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ExportError", ex.Message);
            }
        }

        public async Task ImportConfiguration(string filePath)
        {
            try
            {
                var config = ConfigManager.ImportConfig(filePath);
                config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
                ConfigManager.SaveConfig(config);
                _intercomEngine.ApplyConfig(config);
                await Clients.All.SendAsync("ConfigurationImported");
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ImportError", ex.Message);
            }
        }

        // Preset Management
        public async Task SavePreset(string presetName)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                PresetManager.SavePreset(presetName, config);
                await Clients.All.SendAsync("PresetSaved", presetName);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("PresetError", ex.Message);
            }
        }

        public async Task LoadPreset(string presetName)
        {
            try
            {
                var config = PresetManager.LoadPreset(presetName);
                ConfigManager.SaveConfig(config);
                _intercomEngine.ApplyConfig(config);
                await Clients.All.SendAsync("PresetLoaded", presetName);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("PresetError", ex.Message);
            }
        }

        public async Task<List<string>> GetPresets()
        {
            return PresetManager.ListPresets();
        }

        public async Task DeletePreset(string presetName)
        {
            try
            {
                PresetManager.DeletePreset(presetName);
                await Clients.All.SendAsync("PresetDeleted", presetName);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("PresetError", ex.Message);
            }
        }
    }

    // Background service to push VU meter updates
    public class VUMeterBackgroundService : BackgroundService
    {
        private readonly IHubContext<IntercomHub> _hubContext;
        private readonly IntercomEngine _intercomEngine;

        // Throttle: the microphone callback fires ~50/sec; SignalR can't usefully push
        // that fast to every client and it generates one Task allocation per call. Limit
        // to ~10/sec, which is plenty for a VU meter and slashes 24/7 GC pressure.
        private long _lastVuMeterEmitTicks;
        private const long MIN_VU_INTERVAL_TICKS = TimeSpan.TicksPerMillisecond * 100; // 100ms

        public VUMeterBackgroundService(IHubContext<IntercomHub> hubContext, IntercomEngine intercomEngine)
        {
            _hubContext = hubContext;
            _intercomEngine = intercomEngine;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _intercomEngine.ConnectMicrophoneLevelEvent(OnMicrophoneLevel);

            // Send NDI receive levels for all channels periodically
            var loopTask = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        for (int ch = 1; ch <= IntercomRuntime.Product.MaxChannels; ch++)
                        {
                            float level = _intercomEngine.GetChannelReceiveLevel(ch);
                            try
                            {
                                await _hubContext.Clients.All.SendAsync("ChannelNDILevelUpdated", ch, level, stoppingToken);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch
                            {
                                // A misbehaving SignalR client must NOT take down the level
                                // pump. Skip this channel for this tick and try again next iteration.
                            }
                        }

                        await Task.Delay(100, stoppingToken); // Update every 100ms
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        try { await Task.Delay(1000, stoppingToken); } catch { }
                    }
                }
            }, stoppingToken);

            // Keep the service running until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { }

            // Detach the microphone handler so we don't keep firing into a torn-down hub
            // (and to release the closure capture, eliminating the leak when the service
            // is restarted by the host).
            _intercomEngine.DisconnectMicrophoneLevelEvent(OnMicrophoneLevel);

            try { await loopTask; } catch { }
        }

        private void OnMicrophoneLevel(object? sender, float level)
        {
            // Throttle to ~10 Hz.
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastVuMeterEmitTicks);
            if (now - last < MIN_VU_INTERVAL_TICKS)
                return;
            Interlocked.Exchange(ref _lastVuMeterEmitTicks, now);

            // Fire-and-forget: SignalR SendAsync is async, but we're called from the WASAPI
            // capture callback. Wrap in a try/catch so a transient SignalR failure (slow
            // client, mid-disconnect) never propagates back into the audio path.
            try
            {
                _ = _hubContext.Clients.All.SendAsync("VUMeterUpdate", level)
                    .ContinueWith(t =>
                    {
                        // Observe any faulted task so it doesn't surface as
                        // TaskScheduler.UnobservedTaskException over 24/7 operation.
                        if (t.IsFaulted) { _ = t.Exception; }
                    }, TaskScheduler.Default);
            }
            catch { }
        }
    }
}
