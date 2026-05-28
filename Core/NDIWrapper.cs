using System;
using System.Runtime.InteropServices;

namespace NDIIntercom.Core
{
    // P/Invoke wrapper for NDI SDK (Free)
    public static class NDIWrapper
    {
#if WINDOWS
        private const string NDI_LIB = "Processing.NDI.Lib.x64.dll";
#else
        private const string NDI_LIB = "libndi.so.6";
#endif

        // Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_source_t
        {
            public IntPtr p_ndi_name;
            public IntPtr p_url_address;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_send_create_t
        {
            public IntPtr p_ndi_name;
            public IntPtr p_groups;
            public bool clock_video;
            public bool clock_audio;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_recv_create_v3_t
        {
            public NDIlib_source_t source_to_connect_to;
            public int color_format;
            public int bandwidth;
            public bool allow_video_fields;
            public IntPtr p_ndi_recv_name;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_audio_frame_v3_t
        {
            public int sample_rate;
            public int no_channels;
            public int no_samples;
            public long timecode;
            public int FourCC; // Must be NDIlib_FourCC_audio_type_FLTP = 0
            public IntPtr p_data;
            public int channel_stride_in_bytes;
            public IntPtr p_metadata;
            public long timestamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_video_frame_v2_t
        {
            public int xres;
            public int yres;
            public int FourCC;
            public int frame_rate_N;
            public int frame_rate_D;
            public float picture_aspect_ratio;
            public int frame_format_type;
            public long timecode;
            public IntPtr p_data;
            public int line_stride_in_bytes;
            public IntPtr p_metadata;
            public long timestamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_find_create_t
        {
            public bool show_local_sources;
            public IntPtr p_groups;
            public IntPtr p_extra_ips;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_metadata_frame_t
        {
            public int length;           // Length of the string in p_data (UTF-8 encoded)
            public long timecode;        // Timecode (synthesize = long.MaxValue)
            public IntPtr p_data;        // Metadata XML string (UTF-8 encoded, null terminated)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_tally_t
        {
            public bool on_program;      // Is this source on program output?
            public bool on_preview;      // Is this source on preview output?
        }

        // Constants
        public const int NDIlib_recv_bandwidth_metadata_only = -10;
        public const int NDIlib_recv_bandwidth_audio_only = 10;
        public const int NDIlib_recv_bandwidth_lowest = 0;
        public const int NDIlib_recv_bandwidth_highest = 100;
        public const int NDIlib_recv_color_format_fastest = 1;
        public const long NDIlib_send_timecode_synthesize = long.MaxValue;

        // Video FourCC codes (calculated as: 'B' | ('G' << 8) | ('R' << 16) | ('A' << 24))
        public const int NDIlib_FourCC_video_type_BGRA = 0x41524742;
        public const int NDIlib_FourCC_video_type_UYVY = 0x59565955;

        // Audio FourCC codes
        public const int NDIlib_FourCC_audio_type_FLTP = 0; // 32-bit float planar
        public const int NDIlib_FourCC_audio_type_FLTp = 1; // 32-bit float interleaved (lowercase p)

        // Calculate FourCC for 16-bit signed PCM: 'I' | ('N' << 8) | ('T' << 16) | ('S' << 24) = 0x53544E49
        public const int NDIlib_FourCC_audio_type_INTS = 0x53544E49; // 16-bit signed integer (most compatible)

        // Frame format types
        public const int NDIlib_frame_format_type_progressive = 1;

        public enum NDIlib_frame_type_e
        {
            NDIlib_frame_type_none = 0,
            NDIlib_frame_type_video = 1,
            NDIlib_frame_type_audio = 2,
            NDIlib_frame_type_metadata = 3,
            NDIlib_frame_type_error = 4,
            NDIlib_frame_type_status_change = 100
        }

        // Core Functions
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_initialize();

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_destroy();

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_is_supported_CPU", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_is_supported_CPU();

        // Find (Discovery)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_find_create_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_find_create_v2(ref NDIlib_find_create_t p_create_settings);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_find_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_find_destroy(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_find_get_current_sources", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_find_get_current_sources(IntPtr p_instance, ref uint p_no_sources);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_find_wait_for_sources", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_find_wait_for_sources(IntPtr p_instance, uint timeout_in_ms);

        // Send
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_create(ref NDIlib_send_create_t p_create_settings);

        // Send V2 - with vendor credentials
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_create_v2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr NDIlib_send_create_v2(ref NDIlib_send_create_t p_create_settings, string? p_config_data);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_destroy(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_send_audio_v3", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_send_audio_v3(IntPtr p_instance, ref NDIlib_audio_frame_v3_t p_audio_data);

        // Send monitoring functions (Advanced SDK)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_get_no_connections", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NDIlib_send_get_no_connections(IntPtr p_instance, uint timeout_in_ms);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_get_tally", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_send_get_tally(IntPtr p_instance, ref NDIlib_tally_t p_tally, uint timeout_in_ms);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_send_video_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_send_video_v2(IntPtr p_instance, ref NDIlib_video_frame_v2_t p_video_data);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_add_connection_metadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_add_connection_metadata(IntPtr p_instance, ref NDIlib_metadata_frame_t p_metadata);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_clear_connection_metadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_clear_connection_metadata(IntPtr p_instance);

        // Receive
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_create_v3", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_create_v3(ref NDIlib_recv_create_v3_t p_create_settings);

        // Receive V4 - with vendor credentials
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_create_v4", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr NDIlib_recv_create_v4(ref NDIlib_recv_create_v3_t p_create_settings, string? p_config_data);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_destroy(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_connect", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_connect(IntPtr p_instance, ref NDIlib_source_t p_src);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_capture_v3", CallingConvention = CallingConvention.Cdecl)]
        public static extern NDIlib_frame_type_e NDIlib_recv_capture_v3(
            IntPtr p_instance,
            IntPtr p_video_data,
            ref NDIlib_audio_frame_v3_t p_audio_data,
            IntPtr p_metadata,
            uint timeout_in_ms);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_free_audio_v3", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_free_audio_v3(IntPtr p_instance, ref NDIlib_audio_frame_v3_t p_audio_data);

        // Utility function for sending 16-bit interleaved audio (easier and more compatible!)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_util_send_send_audio_interleaved_16s", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_util_send_send_audio_interleaved_16s(IntPtr p_instance, ref NDIlib_audio_frame_interleaved_16s_t p_audio_data);

        // ===== Receiver connection metadata =====
        // Register metadata that is sent to senders each time a connection is established
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_add_connection_metadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_add_connection_metadata(IntPtr p_instance, ref NDIlib_metadata_frame_t p_metadata);

        // Clear all registered connection metadata
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_clear_connection_metadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_clear_connection_metadata(IntPtr p_instance);

        // Connect receiver to a source (or disconnect with NULL)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_connect", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_connect(IntPtr p_instance, IntPtr p_source);

        [StructLayout(LayoutKind.Sequential)]
        public struct NDIlib_audio_frame_interleaved_16s_t
        {
            public int sample_rate;
            public int no_channels;
            public int no_samples;       // Samples PER channel
            public long timecode;
            public int reference_level;  // in dB (0 = default)
            public IntPtr p_data;        // int16_t* interleaved (LRLRLR...)
        }

        // ===== SendListener API - NDI 6.3 Advanced SDK =====

        [StructLayout(LayoutKind.Sequential)]
        public struct send_listener_create_t
        {
            public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct sender_t
        {
            public IntPtr p_uuid;        // Unique identifier
            public IntPtr p_name;        // Human-readable name
            public IntPtr p_metadata;    // Source metadata
            public IntPtr p_address;     // IP address
            public int port;             // Port number
            public IntPtr p_groups;      // Array of group strings
            public uint num_groups;      // Number of groups
            public bool events_subscribed;  // Events subscription status
        }

        // SendListener creation
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_listener_create(ref send_listener_create_t p_create_settings);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_listener_destroy(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_is_connected", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_send_listener_is_connected(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_get_server_url", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_listener_get_server_url(IntPtr p_instance);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_get_senders", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_listener_get_senders(IntPtr p_instance, ref uint p_num_senders);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_wait_for_senders", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_send_listener_wait_for_senders(IntPtr p_instance, uint timeout_in_ms);

        // SendListener event subscription (Advanced SDK)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_subscribe_events", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void NDIlib_send_listener_subscribe_events(IntPtr p_instance, string p_sender_uuid);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_unsubscribe_events", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void NDIlib_send_listener_unsubscribe_events(IntPtr p_instance, string p_sender_uuid);

        // Get events from subscribed senders
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_get_events", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_listener_get_events(IntPtr p_instance, ref uint p_num_events, uint timeout_in_ms);

        // Free events memory
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_free_events", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_listener_free_events(IntPtr p_instance, IntPtr p_events);

        // ===== SendAdvertiser API - NDI 6.3 Advanced SDK =====
        // Required to register senders on Discovery Server for monitoring!

        [StructLayout(LayoutKind.Sequential)]
        public struct send_advertiser_create_t
        {
            public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
        }

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_send_advertiser_create(ref send_advertiser_create_t p_create_settings);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_send_advertiser_destroy(IntPtr p_instance);

        // Add sender to Discovery Server for monitoring
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_add_sender", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_send_advertiser_add_sender(IntPtr p_instance, IntPtr p_sender, bool allow_monitoring);

        // Remove sender from Discovery Server
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_del_sender", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_send_advertiser_del_sender(IntPtr p_instance, IntPtr p_sender);

        // ===== RecvAdvertiser API - NDI 6.3 Advanced SDK =====
        // Required to register receivers on Discovery Server for monitoring and control!

        [StructLayout(LayoutKind.Sequential)]
        public struct recv_advertiser_create_t
        {
            public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
        }

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_advertiser_create(ref recv_advertiser_create_t p_create_settings);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_advertiser_destroy(IntPtr p_instance);

        // Add receiver to Discovery Server for monitoring and control
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_add_receiver", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool NDIlib_recv_advertiser_add_receiver(
            IntPtr p_instance,
            IntPtr p_receiver,
            bool allow_controlling,
            bool allow_monitoring,
            string? p_input_group_name
        );

        // Remove receiver from Discovery Server
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_del_receiver", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool NDIlib_recv_advertiser_del_receiver(IntPtr p_instance, IntPtr p_receiver);

        // ===== RecvListener API - NDI 6.3 Advanced SDK =====

        [StructLayout(LayoutKind.Sequential)]
        public struct recv_listener_create_t
        {
            public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct receiver_t
        {
            public IntPtr p_uuid;          // Receiver UUID
            public IntPtr p_name;          // Receiver name
            public IntPtr p_input_uuid;    // Input group UUID
            public IntPtr p_input_name;    // Input group name
            public IntPtr p_address;       // IP address of receiver
            public IntPtr p_streams;       // Array of NDIlib_receiver_type_e
            public uint num_streams;       // Number of streams
            public IntPtr p_commands;      // Array of NDIlib_receiver_command_e
            public uint num_commands;      // Number of commands

            [MarshalAs(UnmanagedType.U1)]
            public byte events_subscribed; // 1 if subscribed to events, 0 otherwise
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct listener_event
        {
            public IntPtr p_uuid;   // UUID of receiver that generated the event
            public IntPtr p_name;   // Event name (e.g. "source-name", "connection-state")
            public IntPtr p_value;  // Event value
        }

        // RecvListener creation (standard)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_listener_create(ref recv_listener_create_t p_create_settings);

        // RecvListener creation (with vendor credentials) - REQUIRED FOR EVENTS!
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_create_ex", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr NDIlib_recv_listener_create_ex(ref recv_listener_create_t p_create_settings, string? p_config_data);

        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_listener_destroy(IntPtr p_instance);

        // Get list of receivers registered on Discovery Server
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_get_receivers", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_listener_get_receivers(IntPtr p_instance, ref uint p_num_receivers);

        // Subscribe to events from a receiver
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_subscribe_events", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void NDIlib_recv_listener_subscribe_events(IntPtr p_instance, string p_receiver_uuid);

        // Unsubscribe from events
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_unsubscribe_events", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void NDIlib_recv_listener_unsubscribe_events(IntPtr p_instance, string p_receiver_uuid);

        // Get pending events from subscribed receivers
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_get_events", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_listener_get_events(IntPtr p_instance, ref uint p_num_events, uint timeout_in_ms);

        // Free events memory (CRITICAL!)
        [DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_free_events", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_listener_free_events(IntPtr p_instance, IntPtr p_events);

        // ===== End RecvListener API =====

        // Utility to check if DLL is accessible
        public static bool IsDllAccessible()
        {
            try
            {
                string? appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(appPath))
                {
                    return false;
                }

                string dllPath = System.IO.Path.Combine(appPath, NDI_LIB);
                if (System.IO.File.Exists(dllPath))
                {
                    return true;
                }

#if WINDOWS
                string ndiPath = @"C:\Program Files\NDI\NDI 6 SDK\Bin\x64";
                string sdkDllPath = System.IO.Path.Combine(ndiPath, NDI_LIB);
                return System.IO.File.Exists(sdkDllPath);
#else
                string? fromEnv = System.Environment.GetEnvironmentVariable("NDI_SDK_LINUX_LIB");
                if (!string.IsNullOrEmpty(fromEnv) && System.IO.File.Exists(fromEnv))
                {
                    return true;
                }

                string arch = System.Environment.GetEnvironmentVariable("NDI_SDK_LINUX_ARCH") ?? "x86_64-linux-gnu";
                string home = System.Environment.GetEnvironmentVariable("HOME") ?? "";

                string? envRoot = System.Environment.GetEnvironmentVariable("NDI_SDK_LINUX_ROOT");
                if (!string.IsNullOrEmpty(envRoot))
                {
                    string sdkLib = System.IO.Path.Combine(envRoot, "lib", arch, NDI_LIB);
                    if (System.IO.File.Exists(sdkLib))
                    {
                        return true;
                    }
                }

                // Same discovery order as Directory.Build.props when NDI_SDK_LINUX_ROOT is unset.
                string classic = System.IO.Path.Combine(home, "NDI SDK for Linux", "lib", arch, NDI_LIB);
                if (System.IO.File.Exists(classic))
                {
                    return true;
                }

                string sdkTree = System.IO.Path.Combine(home, "SDK", "NDI_SDK_for_Linux", "lib", arch, NDI_LIB);
                return System.IO.File.Exists(sdkTree);
#endif
            }
            catch
            {
                return false;
            }
        }

        // Detect optional Advanced SDK exports (not available in Free SDK)
        public static bool HasAdvancedFeatures()
        {
            try
            {
                if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(NDI_LIB, out var handle))
                {
                    return false;
                }

                try
                {
                    return System.Runtime.InteropServices.NativeLibrary.TryGetExport(handle, "NDIlib_send_advertiser_create", out _);
                }
                finally
                {
                    System.Runtime.InteropServices.NativeLibrary.Free(handle);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
