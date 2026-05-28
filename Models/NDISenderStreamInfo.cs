namespace NDIIntercom.Models
{
    /// <summary>
    /// Information about an NDI sender stream registered on the Discovery Server
    /// </summary>
    public class NDISenderStreamInfo
    {
        /// <summary>
        /// Unique identifier for the sender
        /// </summary>
        public string Uuid { get; set; } = string.Empty;

        /// <summary>
        /// NDI source name of the sender
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Metadata associated with the sender
        /// </summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>
        /// IP address of the sender
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Port number the sender is using
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Whether the sender has events subscribed
        /// </summary>
        public bool EventsSubscribed { get; set; }

        /// <summary>
        /// List of groups the sender belongs to
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();

        // === Audio Properties (from events) ===

        /// <summary>
        /// Audio codec in use (e.g., "pcm", "aac", "opus")
        /// </summary>
        public string? AudioCodec { get; set; }

        /// <summary>
        /// Number of audio channels
        /// </summary>
        public int? AudioChannels { get; set; }

        /// <summary>
        /// Audio sample rate (e.g., 44100, 48000)
        /// </summary>
        public int? AudioSampleRate { get; set; }

        /// <summary>
        /// Total audio frames sent
        /// </summary>
        public long? AudioFramesSent { get; set; }

        /// <summary>
        /// Total audio bytes sent
        /// </summary>
        public long? AudioBytesSent { get; set; }

        // === Video Properties (from events) ===

        /// <summary>
        /// Video codec in use (e.g., "shq0", "shq2", "shq7", "h264", "h265")
        /// </summary>
        public string? VideoCodec { get; set; }

        /// <summary>
        /// Video resolution (e.g., "1920x1080")
        /// </summary>
        public string? VideoResolution { get; set; }

        /// <summary>
        /// Video frame rate (e.g., "30/1", "60/1")
        /// </summary>
        public string? VideoFrameRate { get; set; }

        /// <summary>
        /// Total video frames sent
        /// </summary>
        public long? VideoFramesSent { get; set; }

        /// <summary>
        /// Total video bytes sent
        /// </summary>
        public long? VideoBytesSent { get; set; }

        /// <summary>
        /// Number of active connections to this sender
        /// </summary>
        public int? ConnectionCount { get; set; }
    }
}
