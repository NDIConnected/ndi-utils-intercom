using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Background service that monitors NDI receivers on the Discovery Server
    /// and tracks which sources are connected to which receivers via events
    /// </summary>
    public class NDIDiscoveryService : BackgroundService
    {
        private readonly ILogger<NDIDiscoveryService> _logger;
        private readonly NDIRecvListener _recvListener;
        private readonly ConcurrentDictionary<string, NDIReceiverInfo> _receivers;
        private readonly TimeSpan _pollInterval;

        // CACHE: Persistent cache of receiver UUID -> source name from events
        // This is necessary because events are edge-triggered (only sent on change)
        private readonly ConcurrentDictionary<string, string> _receiverSourceNames;

        private System.Threading.Timer? _discoveryTimer;

        public NDIDiscoveryService(ILogger<NDIDiscoveryService> logger, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        {
            _logger = logger;
            _receivers = new ConcurrentDictionary<string, NDIReceiverInfo>();
            _receiverSourceNames = new ConcurrentDictionary<string, string>();
            _recvListener = new NDIRecvListener();

            // Override via appsettings.json: "NDIDiscovery": { "PollIntervalSeconds": 2 }.
            // Clamped to [1, 60] so a misconfigured value can't disable discovery or
            // burn CPU. Default 2s preserves prior behavior.
            int pollSeconds = configuration?.GetValue<int?>("NDIDiscovery:PollIntervalSeconds") ?? 2;
            if (pollSeconds < 1) pollSeconds = 1;
            if (pollSeconds > 60) pollSeconds = 60;
            _pollInterval = TimeSpan.FromSeconds(pollSeconds);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NDIDiscoveryService] Starting NDI Discovery Service...");

            // Initialize RecvListener with vendor credentials
            if (!_recvListener.Initialize())
            {
                _logger.LogError("[NDIDiscoveryService] Failed to initialize RecvListener. Events will not be available.");
                return Task.CompletedTask;
            }

            _logger.LogInformation("[NDIDiscoveryService] RecvListener initialized successfully with vendor credentials");

            // Start discovery timer with configured interval (default 2s; events are
            // edge-triggered, so polling only refreshes the device list).
            _discoveryTimer = new System.Threading.Timer(DiscoverDevices, null, TimeSpan.Zero, _pollInterval);

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NDIDiscoveryService] Stopping NDI Discovery Service...");

            _discoveryTimer?.Dispose();
            _recvListener?.Dispose();

            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Background service keeps running while timer executes
            return Task.CompletedTask;
        }

        private void DiscoverDevices(object? state)
        {
            try
            {
                // Get receivers from NDI Discovery Server
                var receivers = _recvListener.GetReceivers();

                // Poll for events and update persistent cache
                var events = _recvListener.GetEvents(0);  // 0 = non-blocking
                foreach (var evt in events)
                {
                    // Per-event detail at Debug only — Information-level spam can fill the log
                    // file in minutes on a busy NDI network with many devices/connections.
                    _logger.LogDebug("Event UUID={Uuid} Name={Name} Value={Value}", evt.ReceiverUuid, evt.EventName, evt.EventValue);

                    // Extract source-name events and save to persistent cache
                    if (evt.EventName == "source-name")
                    {
                        if (!string.IsNullOrEmpty(evt.EventValue))
                        {
                            // Connected to a source
                            _receiverSourceNames[evt.ReceiverUuid] = evt.EventValue;
                            _logger.LogInformation("Receiver {Uuid} connected to source: {Source}", evt.ReceiverUuid, evt.EventValue);
                        }
                        else
                        {
                            // Disconnected (empty source-name)
                            _receiverSourceNames.TryRemove(evt.ReceiverUuid, out _);
                            _logger.LogInformation("Receiver {Uuid} disconnected", evt.ReceiverUuid);
                        }
                    }
                }

                // Update receivers and populate CurrentSource from cache
                _receivers.Clear();
                foreach (var receiver in receivers)
                {
                    // Use cached source name from events
                    if (_receiverSourceNames.TryGetValue(receiver.Id, out var sourceName))
                    {
                        receiver.CurrentSource = sourceName;
                        receiver.IsConnected = true;
                    }

                    _receivers[receiver.Id] = receiver;
                }

                // Prune the source-name cache: receivers that are no longer present on the
                // Discovery Server (host crashed, USB unplug, app exit without a clean
                // disconnect event) leak entries into _receiverSourceNames otherwise. Over
                // 24/7 operation this grows linearly with churn.
                var liveIds = new HashSet<string>(receivers.Count, StringComparer.Ordinal);
                foreach (var r in receivers)
                {
                    liveIds.Add(r.Id);
                }
                foreach (var key in _receiverSourceNames.Keys)
                {
                    if (!liveIds.Contains(key))
                    {
                        _receiverSourceNames.TryRemove(key, out _);
                    }
                }

                // Log summary
                if (receivers.Count > 0)
                {
                    _logger.LogDebug($"[NDIDiscoveryService] Found {receivers.Count} receivers on Discovery Server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NDIDiscoveryService] Error during NDI discovery");
            }
        }

        /// <summary>
        /// Get all receivers currently registered on the Discovery Server
        /// </summary>
        /// <returns>List of receiver information with current source</returns>
        public List<NDIReceiverInfo> GetReceivers()
        {
            return _receivers.Values.ToList();
        }

        /// <summary>
        /// Get a specific receiver by UUID
        /// </summary>
        /// <param name="receiverId">Receiver UUID</param>
        /// <returns>Receiver information or null if not found</returns>
        public NDIReceiverInfo? GetReceiverById(string receiverId)
        {
            _receivers.TryGetValue(receiverId, out var receiver);
            return receiver;
        }
    }
}
