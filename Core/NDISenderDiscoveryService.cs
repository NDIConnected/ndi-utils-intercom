using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Background service that monitors NDI senders registered on the Discovery Server
    /// </summary>
    public class NDISenderDiscoveryService : IHostedService, IDisposable
    {
        private readonly ILogger<NDISenderDiscoveryService> _logger;
        private NDISendListener? _sendListener;
        private System.Threading.Timer? _pollTimer;
        private List<NDISenderStreamInfo> _cachedSenders = new();
        private readonly object _lock = new();
        private readonly TimeSpan _pollInterval;

        public NDISenderDiscoveryService(ILogger<NDISenderDiscoveryService> logger, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        {
            _logger = logger;
            // Override via appsettings.json: "NDIDiscovery": { "PollIntervalSeconds": 2 }.
            // Same key as NDIDiscoveryService so a single setting governs both poll loops.
            int pollSeconds = configuration?.GetValue<int?>("NDIDiscovery:PollIntervalSeconds") ?? 2;
            if (pollSeconds < 1) pollSeconds = 1;
            if (pollSeconds > 60) pollSeconds = 60;
            _pollInterval = TimeSpan.FromSeconds(pollSeconds);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NDISenderDiscoveryService] Starting...");

            _sendListener = new NDISendListener();
            if (!_sendListener.Initialize())
            {
                _logger.LogError("[NDISenderDiscoveryService] Failed to initialize SendListener");
                return Task.CompletedTask;
            }

            _logger.LogInformation("[NDISenderDiscoveryService] SendListener initialized. IsConnected: {IsConnected}, ServerUrl: {ServerUrl}",
                _sendListener.IsConnected, _sendListener.ServerUrl);

            // Poll using the configured interval (default 2s).
            _pollTimer = new System.Threading.Timer(PollSenders, null, TimeSpan.Zero, _pollInterval);

            return Task.CompletedTask;
        }

        private void PollSenders(object? state)
        {
            try
            {
                if (_sendListener == null)
                    return;

                // Update events from subscribed senders (non-blocking)
                _sendListener.UpdateEvents(0);

                // Get list of senders with populated event properties
                var senders = _sendListener.GetSenders();

                lock (_lock)
                {
                    _cachedSenders = senders;
                }

                if (senders.Count > 0)
                {
                    _logger.LogDebug("[NDISenderDiscoveryService] Found {Count} senders on Discovery Server", senders.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NDISenderDiscoveryService] Error polling senders");
            }
        }

        /// <summary>
        /// Get all senders currently registered on the Discovery Server
        /// </summary>
        public List<NDISenderStreamInfo> GetSenders()
        {
            lock (_lock)
            {
                return new List<NDISenderStreamInfo>(_cachedSenders);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NDISenderDiscoveryService] Stopping...");

            _pollTimer?.Change(Timeout.Infinite, 0);
            _pollTimer?.Dispose();
            _pollTimer = null;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _sendListener?.Dispose();
            _sendListener = null;
        }
    }
}
