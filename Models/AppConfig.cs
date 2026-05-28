using System;
using System.Collections.Generic;

namespace NDIIntercom.Models
{
    public class AppConfig
    {
        public string ApplicationId { get; set; } = "Intercom_A";
        public string DeviceId { get; set; }

        /// <summary>
        /// Controls the suffix appended to every NDI sender / receiver name.
        /// Valid values (case-insensitive): "Full", "Compact", "Off".
        ///
        /// - <c>Full</c>:    "Channel 1 [app=Intercom_A;device=9f8c…;role=sender;ch=1]" — full
        ///                  parsable identity in the name. Legacy / embedded NDI receivers may
        ///                  reject names this complex (length, brackets, semicolons).
        /// - <c>Compact</c>: "Channel 1 (Intercom_A)" — application id only, parentheses style
        ///                  used by NDI tools. Maximum compatibility with all NDI clients while
        ///                  still hinting which Intercom owns the source. <b>Default since v1.7.1.</b>
        /// - <c>Off</c>:     "Channel 1" — pure friendly name, no identity hint. Use for legacy
        ///                  receivers that fail with any extra characters.
        ///
        /// In every mode the &lt;ndi_manager&gt; connection metadata XML is unchanged, so a
        /// management application that reads metadata can still aggregate instances by
        /// (application_id, device_id) regardless of this setting.
        /// </summary>
        public string IdentitySuffixMode { get; set; } = "Compact";

        public string SelectedMicrophone { get; set; }
        public string SelectedSpeaker { get; set; }

        // ASIO Device Configuration
        public string SelectedAsioDevice { get; set; }
        public int AsioInputChannelCount { get; set; } = 0;
        public int AsioOutputChannelCount { get; set; } = 0;

        // Microphone Noise Gate (input processing)
        public bool NoiseGateEnabled { get; set; } = false;
        public double NoiseGateThresholdDb { get; set; } = -45.0;  // Default -45dB (good for most environments)
        public int NoiseGateAttackMs { get; set; } = 1;            // 1ms attack (very fast, preserve consonants)
        public int NoiseGateReleaseMs { get; set; } = 100;         // 100ms release (natural sound)
        public int NoiseGateHoldMs { get; set; } = 100;            // 100ms hold time (prevents chopping during speech)

        // Channels Feedback Gate (channel mixing - anti-feedback)
        public bool FeedbackGateEnabled { get; set; } = false;
        public double GateThresholdDb { get; set; } = -40.0;
        public int GateAttackMs { get; set; } = 0;
        public int GateReleaseMs { get; set; } = 200;

        public List<ChannelConfig> Channels { get; set; } = new List<ChannelConfig>();

        /// <summary>
        /// Trim or pad <see cref="Channels"/> to exactly <paramref name="maxChannels"/> entries (numbers 1..max).
        /// </summary>
        public void EnsureChannelCount(int maxChannels)
        {
            if (maxChannels < 1)
                maxChannels = 1;

            while (Channels.Count > maxChannels)
            {
                Channels.RemoveAt(Channels.Count - 1);
            }

            for (int i = Channels.Count; i < maxChannels; i++)
            {
                int n = i + 1;
                Channels.Add(new ChannelConfig
                {
                    ChannelNumber = n,
                    Label = $"Channel {n}",
                    NdiSendName = $"Channel {n}",
                    InputLevel = 100,
                    OutputLevel = 100,
                    IntercomGroup = 0
                });
            }

            for (int i = 0; i < Channels.Count; i++)
            {
                int n = i + 1;
                Channels[i].ChannelNumber = n;
                if (string.IsNullOrWhiteSpace(Channels[i].Label))
                    Channels[i].Label = $"Channel {n}";
                if (string.IsNullOrWhiteSpace(Channels[i].NdiSendName))
                    Channels[i].NdiSendName = $"Channel {n}";
            }
        }
    }

    public class ChannelConfig
    {
        public int ChannelNumber { get; set; }
        public string Label { get; set; }
        public string NdiSendName { get; set; }
        public string NdiReceiveName { get; set; }
        public int InputLevel { get; set; }
        public int OutputLevel { get; set; }

        // Mode: NDI or ASIO
        public int Mode { get; set; } = 0; // 0=NDI, 1=ASIO (using int for JSON compatibility)

        // Unified Intercom Group (shared across NDI and ASIO modes)
        public int IntercomGroup { get; set; } = 0;

        // Backward-compat: old configs may have these separate fields.
        // On deserialization, if IntercomGroup is 0 but one of these is set, use the non-zero value.
        public int NdiIntercomGroup
        {
            get => IntercomGroup;
            set { if (value != 0 && IntercomGroup == 0) IntercomGroup = value; }
        }

        public int AsioIntercomGroup
        {
            get => IntercomGroup;
            set { if (value != 0 && IntercomGroup == 0) IntercomGroup = value; }
        }

        // ASIO Configuration
        public int AsioInputChannel { get; set; } = 0;
        public int AsioOutputChannel { get; set; } = 0;
    }
}
