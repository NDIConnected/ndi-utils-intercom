namespace NDIIntercom.Models
{
    /// <summary>
    /// What a channel's NDI receiver is actually doing, as opposed to what it was configured
    /// to do. Surfaced to the web UI and <c>/healthz</c> so a channel whose source is
    /// selected but not connected is visible instead of looking healthy while silent.
    /// </summary>
    public class NDIReceiverStatus
    {
        public int ChannelNumber { get; set; }

        /// <summary>Source the operator selected (persisted in config).</summary>
        public string ConfiguredSource { get; set; } = string.Empty;

        /// <summary>Source the receiver was last pointed at, empty when idle.</summary>
        public string ConnectedSource { get; set; } = string.Empty;

        /// <summary>True only when the SDK reports a live connection to a sender.</summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// A source is configured but no live connection exists — the operator selected a
        /// source and hears nothing.
        /// </summary>
        public bool IsWaitingForSource => !string.IsNullOrEmpty(ConfiguredSource) && !IsConnected;
    }
}
