namespace NDIIntercom.Models
{
    public enum ChannelMode
    {
        NDI = 0,
        ASIO = 1
    }

    /// <summary>
    /// Mutable per-channel state. Plain POCO with public properties — no INotifyPropertyChanged
    /// because nothing in the codebase subscribes to PropertyChanged. The previous setters
    /// allocated a <c>PropertyChangedEventArgs</c> each call, which was a per-frame waste
    /// when the audio path updated levels for 16 channels at ~50 Hz.
    /// State is observed instead via SignalR push (see <c>VUMeterBackgroundService</c>).
    /// </summary>
    public class ChannelState
    {
        public int ChannelNumber { get; set; }
        public string Label { get; set; }
        public bool TalkEnabled { get; set; }
        public bool ListenEnabled { get; set; }
        public int InputLevel { get; set; } = 100;
        public int OutputLevel { get; set; } = 100;

        // Mode: NDI or ASIO
        public ChannelMode Mode { get; set; } = ChannelMode.NDI;

        // Unified Intercom Group (shared across NDI and ASIO).
        // 0 = none, 1-4 = group number.
        public int IntercomGroup { get; set; } = 0;

        // NDI Configuration
        public string NdiSendName { get; set; }
        public string NdiReceiveName { get; set; }

        // ASIO Configuration (0-based indices into the active ASIO driver)
        public int AsioInputChannel { get; set; } = 0;
        public int AsioOutputChannel { get; set; } = 0;

        // Backward-compat aliases preserved so legacy serialized payloads still bind.
        // Both forms map to IntercomGroup.
        public int NdiIntercomGroup
        {
            get => IntercomGroup;
            set => IntercomGroup = value;
        }

        public int AsioIntercomGroup
        {
            get => IntercomGroup;
            set => IntercomGroup = value;
        }

        public bool IsConnected { get; set; }
        public float InputMeterLevel { get; set; }
    }
}
