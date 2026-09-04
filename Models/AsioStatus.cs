namespace NDIIntercom.Models
{
    /// <summary>
    /// Snapshot of the ASIO path, distinguishing "a device is selected" from "the driver is
    /// actually playing". A channel can be configured and still be silent, so the difference
    /// between <see cref="IsInitialized"/>, <see cref="IsStarted"/> and <see cref="IsPlaying"/>
    /// is what tells an operator whether the problem is the config or the driver.
    /// </summary>
    public class AsioStatus
    {
        /// <summary>Device the engine currently holds, empty when none is selected.</summary>
        public string SelectedDevice { get; set; } = string.Empty;

        /// <summary>A driver instance exists and reported its channel counts.</summary>
        public bool IsInitialized { get; set; }

        /// <summary>Buffers were created and playback was started without throwing.</summary>
        public bool IsStarted { get; set; }

        /// <summary>The driver still reports the transport as running.</summary>
        public bool IsPlaying { get; set; }

        /// <summary>The mixing engine was built against the current driver.</summary>
        public bool EngineBuilt { get; set; }

        /// <summary>Inputs the driver exposes; routing above this is dropped by the mixer.</summary>
        public int InputChannelCount { get; set; }

        /// <summary>Outputs the driver exposes; routing above this is dropped by the mixer.</summary>
        public int OutputChannelCount { get; set; }

        /// <summary>Last initialization or start failure, empty when none.</summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>True when a device is configured but audio is not flowing.</summary>
        public bool IsFaulted => !string.IsNullOrEmpty(SelectedDevice) && !(IsPlaying && EngineBuilt);
    }
}
