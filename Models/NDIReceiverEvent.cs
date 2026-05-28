namespace NDIIntercom.Models
{
    /// <summary>
    /// Represents an event received from an NDI receiver
    /// </summary>
    public class NDIReceiverEvent
    {
        /// <summary>
        /// UUID of the receiver that generated this event
        /// </summary>
        public string ReceiverUuid { get; set; } = string.Empty;

        /// <summary>
        /// Event name (e.g. "source-name", "connection-state", "video-resolution")
        /// </summary>
        public string EventName { get; set; } = string.Empty;

        /// <summary>
        /// Event value
        /// </summary>
        public string EventValue { get; set; } = string.Empty;
    }
}
