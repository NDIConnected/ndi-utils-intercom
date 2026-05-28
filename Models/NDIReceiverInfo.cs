using System;

namespace NDIIntercom.Models
{
    /// <summary>
    /// Represents an NDI receiver registered on the Discovery Server
    /// </summary>
    public class NDIReceiverInfo
    {
        /// <summary>
        /// Receiver UUID (unique identifier)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Receiver name (e.g. "COMPUTER-NAME (NDI Studio Monitor 1)")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// UUID of connected source (if connected)
        /// </summary>
        public string? InputUuid { get; set; }

        /// <summary>
        /// Name of connected source (obtained from events!)
        /// Example: "COMPUTER-NAME (SENDER 001)"
        /// </summary>
        public string? CurrentSource { get; set; }

        /// <summary>
        /// Is the receiver connected to a source?
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Last time this receiver was seen
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Connection status string
        /// </summary>
        public string Status => IsConnected ? "Connected" : "Disconnected";
    }
}
