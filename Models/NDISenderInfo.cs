namespace NDIIntercom.Models
{
    /// <summary>
    /// Information about a local NDI sender
    /// </summary>
    public class NDISenderInfo
    {
        /// <summary>
        /// Channel number (1-16)
        /// </summary>
        public int ChannelNumber { get; set; }

        /// <summary>
        /// NDI sender name
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Number of active connections (receivers connected to this sender)
        /// </summary>
        public int ConnectionCount { get; set; }

        /// <summary>
        /// Active sender (created and running)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Tally Program (sender is on program)
        /// </summary>
        public bool OnProgram { get; set; }

        /// <summary>
        /// Tally Preview (sender is on preview)
        /// </summary>
        public bool OnPreview { get; set; }

        /// <summary>
        /// NDI intercom group
        /// </summary>
        public string? IntercomGroup { get; set; }
    }
}
