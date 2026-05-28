namespace NDIIntercom.Models
{
    /// <summary>Client-facing product limits (SignalR / UI).</summary>
    public class IntercomProductInfoMessage
    {
        public int MaxChannels { get; set; }
        public string ProductDisplayName { get; set; } = "";
        public string UiTitleShort { get; set; } = "";

        /// <summary>False on Linux: ASIO UI and per-channel ASIO mode are hidden.</summary>
        public bool AsioAvailable { get; set; }

        /// <summary>Short label for settings (e.g. WASAPI vs PipeWire).</summary>
        public string AudioBackendName { get; set; } = "";
    }
}
