# NDI SDK - Sender Event Monitoring Implementation Guide

> Note: This guide applies to the NDI 6 SDK receiver/sender discovery and monitoring APIs.

## Overview

This document describes how to implement NDI Sender Event Monitoring using the NDI SDK (v6.3+). This feature allows monitoring real-time stream properties (codec, channels, sample rate, frames/bytes sent) from NDI senders registered on the Discovery Server.

## Prerequisites

- NDI SDK 6.3 or later
- Vendor credentials (VENDOR_NAME and VENDOR_CODE)
- Discovery Server running on the network

## Key Concepts

### 1. SendAdvertiser vs SendListener

**SendAdvertiser**:
- Registers local NDI senders on the Discovery Server for monitoring purposes
- Required to make senders visible to SendListener
- Separate from normal mDNS advertising
- Must call `NDIlib_send_advertiser_add_sender()` with `allow_monitoring = true`

**SendListener**:
- Discovers senders registered on the Discovery Server
- Subscribes to sender events
- Receives real-time stream property updates

### 2. Why Both Are Required

By default, NDI senders advertise via mDNS (legacy mode). To enable monitoring:
1. Create a SendAdvertiser instance
2. Register each sender with `NDIlib_send_advertiser_add_sender(advertiser, sender, true)`
3. Create a SendListener to discover these senders
4. Subscribe to events using `NDIlib_send_listener_subscribe_events()`
5. Poll events using `NDIlib_send_listener_get_events()`

## Implementation Steps

### Step 1: Add P/Invoke Declarations (NDIWrapper.cs)

```csharp
// SendAdvertiser structures
[StructLayout(LayoutKind.Sequential)]
public struct send_advertiser_create_t
{
    public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
}

// SendAdvertiser functions
[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_create")]
public static extern IntPtr NDIlib_send_advertiser_create(ref send_advertiser_create_t p_create_settings);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_destroy")]
public static extern void NDIlib_send_advertiser_destroy(IntPtr p_instance);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_add_sender")]
public static extern bool NDIlib_send_advertiser_add_sender(IntPtr p_instance, IntPtr p_sender, bool allow_monitoring);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_advertiser_del_sender")]
public static extern bool NDIlib_send_advertiser_del_sender(IntPtr p_instance, IntPtr p_sender);

// SendListener event subscription
[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_subscribe_events")]
public static extern void NDIlib_send_listener_subscribe_events(IntPtr p_instance, string p_sender_uuid);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_unsubscribe_events")]
public static extern void NDIlib_send_listener_unsubscribe_events(IntPtr p_instance, string p_sender_uuid);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_get_events")]
public static extern IntPtr NDIlib_send_listener_get_events(IntPtr p_instance, ref uint p_num_events, uint timeout_in_ms);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_send_listener_free_events")]
public static extern void NDIlib_send_listener_free_events(IntPtr p_instance, IntPtr p_events);

// Event structure (shared with RecvListener)
[StructLayout(LayoutKind.Sequential)]
public struct listener_event
{
    public IntPtr p_uuid;   // UUID of sender that generated the event
    public IntPtr p_name;   // Event name (e.g. "audio-codec", "audio-channels")
    public IntPtr p_value;  // Event value
}
```

### Step 2: Create SendAdvertiser in Manager

```csharp
public class NDIManager
{
    private IntPtr _sendAdvertiserInstance = IntPtr.Zero;

    public bool Initialize()
    {
        // ... other initialization ...

        // Create SendAdvertiser for Discovery Server registration
        var advertiserSettings = new NDIWrapper.send_advertiser_create_t
        {
            p_url_address = IntPtr.Zero  // Use default Discovery Server
        };
        _sendAdvertiserInstance = NDIWrapper.NDIlib_send_advertiser_create(ref advertiserSettings);

        // ... create channels ...
    }

    public void Dispose()
    {
        // ... dispose channels ...

        if (_sendAdvertiserInstance != IntPtr.Zero)
        {
            NDIWrapper.NDIlib_send_advertiser_destroy(_sendAdvertiserInstance);
            _sendAdvertiserInstance = IntPtr.Zero;
        }
    }
}
```

### Step 3: Register Senders on Discovery Server

```csharp
public class NDIChannel
{
    private IntPtr _senderInstance = IntPtr.Zero;
    private IntPtr _sendAdvertiser = IntPtr.Zero;

    public NDIChannel(int channelNumber, IntPtr sendAdvertiser)
    {
        ChannelNumber = channelNumber;
        _sendAdvertiser = sendAdvertiser;
    }

    public void RecreateSender()
    {
        DestroySender();

        // Create sender with vendor credentials
        const string VENDOR_NAME = "YOUR_VENDOR_NAME";
        const string VENDOR_CODE = "YOUR_VENDOR_CODE";
        string configJson = $@"{{""ndi"":{{""vendor"":{{""name"":""{VENDOR_NAME}"",""id"":""{VENDOR_CODE}""}}}}}}";

        _senderInstance = NDIWrapper.NDIlib_send_create_v2(ref sendSettings, configJson);

        if (_senderInstance != IntPtr.Zero)
        {
            // Register on Discovery Server for monitoring (CRITICAL!)
            if (_sendAdvertiser != IntPtr.Zero)
            {
                bool added = NDIWrapper.NDIlib_send_advertiser_add_sender(
                    _sendAdvertiser,
                    _senderInstance,
                    true  // allow_monitoring = true
                );
                Console.WriteLine($"Registered on Discovery Server: {added}");
            }
        }
    }

    private void DestroySender()
    {
        if (_senderInstance != IntPtr.Zero)
        {
            // Remove from Discovery Server before destroying
            if (_sendAdvertiser != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_send_advertiser_del_sender(_sendAdvertiser, _senderInstance);
            }

            NDIWrapper.NDIlib_send_destroy(_senderInstance);
            _senderInstance = IntPtr.Zero;
        }
    }
}
```

### Step 4: Create SendListener with Event Support

```csharp
public class NDISendListener : IDisposable
{
    private IntPtr _listenerInstance = IntPtr.Zero;
    private Dictionary<string, Dictionary<string, string>> _senderEvents = new();
    private HashSet<string> _subscribedSenders = new();

    public bool Initialize()
    {
        var listenerSettings = new NDIWrapper.send_listener_create_t
        {
            p_url_address = IntPtr.Zero
        };

        _listenerInstance = NDIWrapper.NDIlib_send_listener_create(ref listenerSettings);
        return _listenerInstance != IntPtr.Zero;
    }

    public List<NDISenderStreamInfo> GetSenders()
    {
        var senderList = new List<NDISenderStreamInfo>();

        uint numSenders = 0;
        IntPtr sendersPtr = NDIWrapper.NDIlib_send_listener_get_senders(_listenerInstance, ref numSenders);

        if (sendersPtr == IntPtr.Zero || numSenders == 0)
            return senderList;

        int structSize = Marshal.SizeOf<NDIWrapper.sender_t>();

        for (int i = 0; i < numSenders; i++)
        {
            IntPtr currentPtr = IntPtr.Add(sendersPtr, i * structSize);
            var sender = Marshal.PtrToStructure<NDIWrapper.sender_t>(currentPtr);

            string uuid = Marshal.PtrToStringAnsi(sender.p_uuid) ?? "";

            // Subscribe to events if not already subscribed
            if (!sender.events_subscribed && !_subscribedSenders.Contains(uuid))
            {
                NDIWrapper.NDIlib_send_listener_subscribe_events(_listenerInstance, uuid);
                _subscribedSenders.Add(uuid);
            }

            var senderInfo = new NDISenderStreamInfo
            {
                Uuid = uuid,
                Name = Marshal.PtrToStringAnsi(sender.p_name) ?? "",
                Address = Marshal.PtrToStringAnsi(sender.p_address) ?? "",
                Port = sender.port,
                EventsSubscribed = sender.events_subscribed || _subscribedSenders.Contains(uuid)
            };

            // Populate event properties if we have them cached
            if (_senderEvents.TryGetValue(uuid, out var events))
            {
                PopulateEventProperties(senderInfo, events);
            }

            senderList.Add(senderInfo);
        }

        return senderList;
    }

    public void UpdateEvents(uint timeoutMs = 0)
    {
        uint numEvents = 0;
        IntPtr eventsPtr = NDIWrapper.NDIlib_send_listener_get_events(_listenerInstance, ref numEvents, timeoutMs);

        if (eventsPtr == IntPtr.Zero || numEvents == 0)
            return;

        try
        {
            int structSize = Marshal.SizeOf<NDIWrapper.listener_event>();

            for (int i = 0; i < numEvents; i++)
            {
                IntPtr currentPtr = IntPtr.Add(eventsPtr, i * structSize);
                var evt = Marshal.PtrToStructure<NDIWrapper.listener_event>(currentPtr);

                string uuid = Marshal.PtrToStringAnsi(evt.p_uuid) ?? "";
                string name = Marshal.PtrToStringAnsi(evt.p_name) ?? "";
                string value = Marshal.PtrToStringAnsi(evt.p_value) ?? "";

                if (!string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(name))
                {
                    if (!_senderEvents.ContainsKey(uuid))
                    {
                        _senderEvents[uuid] = new Dictionary<string, string>();
                    }
                    _senderEvents[uuid][name] = value;
                }
            }
        }
        finally
        {
            // CRITICAL: Free events memory
            NDIWrapper.NDIlib_send_listener_free_events(_listenerInstance, eventsPtr);
        }
    }

    private void PopulateEventProperties(NDISenderStreamInfo senderInfo, Dictionary<string, string> events)
    {
        // Audio properties
        if (events.TryGetValue("audio-codec", out var audioCodec))
            senderInfo.AudioCodec = audioCodec;

        if (events.TryGetValue("audio-channels", out var audioChannels) && int.TryParse(audioChannels, out var channels))
            senderInfo.AudioChannels = channels;

        if (events.TryGetValue("audio-samplerate", out var audioSampleRate) && int.TryParse(audioSampleRate, out var sampleRate))
            senderInfo.AudioSampleRate = sampleRate;

        if (events.TryGetValue("audio-frames-sent", out var audioFrames) && long.TryParse(audioFrames, out var aFrames))
            senderInfo.AudioFramesSent = aFrames;

        if (events.TryGetValue("audio-bytes-sent", out var audioBytes) && long.TryParse(audioBytes, out var aBytes))
            senderInfo.AudioBytesSent = aBytes;

        // Video properties
        if (events.TryGetValue("video-codec", out var videoCodec))
            senderInfo.VideoCodec = videoCodec;

        if (events.TryGetValue("video-resolution", out var videoResolution))
            senderInfo.VideoResolution = videoResolution;

        if (events.TryGetValue("video-framerate", out var videoFrameRate))
            senderInfo.VideoFrameRate = videoFrameRate;

        if (events.TryGetValue("video-frames-sent", out var videoFrames) && long.TryParse(videoFrames, out var vFrames))
            senderInfo.VideoFramesSent = vFrames;

        if (events.TryGetValue("video-bytes-sent", out var videoBytes) && long.TryParse(videoBytes, out var vBytes))
            senderInfo.VideoBytesSent = vBytes;

        // Connection count
        if (events.TryGetValue("connection-count", out var connCount) && int.TryParse(connCount, out var count))
            senderInfo.ConnectionCount = count;
    }
}
```

### Step 5: Create Background Service to Poll Events

```csharp
public class NDISenderDiscoveryService : IHostedService, IDisposable
{
    private NDISendListener? _sendListener;
    private Timer? _pollTimer;
    private List<NDISenderStreamInfo> _cachedSenders = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sendListener = new NDISendListener();
        _sendListener.Initialize();

        // Poll every 2 seconds
        _pollTimer = new Timer(PollSenders, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        return Task.CompletedTask;
    }

    private void PollSenders(object? state)
    {
        if (_sendListener == null)
            return;

        // Update events from subscribed senders (non-blocking)
        _sendListener.UpdateEvents(0);

        // Get list of senders with populated event properties
        var senders = _sendListener.GetSenders();

        lock (_lock)
        {
            _cachedSenders = senders;
        }
    }

    public List<NDISenderStreamInfo> GetSenders()
    {
        lock (_lock)
        {
            return new List<NDISenderStreamInfo>(_cachedSenders);
        }
    }
}
```

## Data Model

```csharp
public class NDISenderStreamInfo
{
    // Basic sender info (from sender_t structure)
    public string Uuid { get; set; }
    public string Name { get; set; }
    public string Metadata { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public bool EventsSubscribed { get; set; }
    public List<string> Groups { get; set; }

    // Audio properties (from events)
    public string? AudioCodec { get; set; }              // "pcm", "aac", "opus"
    public int? AudioChannels { get; set; }              // 1, 2, 6, 8
    public int? AudioSampleRate { get; set; }            // 44100, 48000
    public long? AudioFramesSent { get; set; }
    public long? AudioBytesSent { get; set; }

    // Video properties (from events)
    public string? VideoCodec { get; set; }              // "shq0", "shq2", "h264", "h265"
    public string? VideoResolution { get; set; }         // "1920x1080", "1280x720"
    public string? VideoFrameRate { get; set; }          // "30/1", "60/1"
    public long? VideoFramesSent { get; set; }
    public long? VideoBytesSent { get; set; }

    // Connection info (from events)
    public int? ConnectionCount { get; set; }
}
```

## Expected Event Names

Based on NDI SDK documentation, sender events include:

### Audio Events
- `audio-codec` - Audio codec ("pcm", "aac", "opus")
- `audio-channels` - Number of channels (int)
- `audio-samplerate` - Sample rate (int)
- `audio-frames-sent` - Total frames sent (long)
- `audio-bytes-sent` - Total bytes sent (long)

### Video Events
- `video-codec` - Video codec ("shq0", "shq2", "shq7", "h264", "h265")
- `video-resolution` - Resolution ("WxH" format)
- `video-framerate` - Frame rate ("N/D" format)
- `video-frames-sent` - Total frames sent (long)
- `video-bytes-sent` - Total bytes sent (long)

### Connection Events
- `connection-count` - Number of active connections (int)

## Troubleshooting

### Senders Not Appearing in SendListener

**Problem**: `GetSenders()` returns empty list even though senders are running.

**Solution**:
- Ensure SendAdvertiser is created and initialized
- Verify `NDIlib_send_advertiser_add_sender()` is called with `allow_monitoring = true`
- Check senders are using V2 API with vendor credentials
- Confirm Discovery Server is running and accessible

### Events Not Being Received

**Problem**: `eventsSubscribed` is true but event properties are null.

**Solution**:
- Verify vendor credentials are correct
- Ensure `UpdateEvents()` is being called regularly (every 2-10 seconds)
- Check event names match exactly (case-sensitive)
- Events may take 5-10 seconds to start flowing
- Use non-zero timeout in `UpdateEvents()` for testing: `UpdateEvents(1000)`

### Memory Leaks

**Problem**: Memory usage increases over time.

**Solution**:
- ALWAYS call `NDIlib_send_listener_free_events()` after processing events
- Use try/finally block to ensure events are freed even on exceptions
- Properly dispose SendListener and SendAdvertiser instances

## Complete Flow Diagram

```
1. Application starts
   ↓
2. Create SendAdvertiser instance
   ↓
3. Create NDI sender instances
   ↓
4. Register each sender with SendAdvertiser (allow_monitoring = true)
   ↓
5. Create SendListener instance
   ↓
6. SendListener discovers senders on Discovery Server
   ↓
7. Subscribe to events for each sender
   ↓
8. Poll events every 2 seconds using get_events()
   ↓
9. Parse event name/value pairs
   ↓
10. Update cached sender properties
   ↓
11. Expose via API/UI
```

## Performance Considerations

- Event polling frequency: 2-5 seconds is optimal
- Use timeout = 0 in production (non-blocking)
- Cache events to avoid repeated allocations
- Free event memory immediately after processing
- Events are delta updates, maintain state in cache

## API Endpoint Example

```csharp
[HttpGet("api/ndi/senders")]
public ActionResult<List<NDISenderStreamInfo>> GetSenders()
{
    var senders = _senderDiscoveryService.GetSenders();
    return Ok(senders);
}
```

## Testing

```bash
# Test endpoint
curl http://localhost:5016/api/ndi/senders | jq

# Expected output (after events arrive):
[
  {
    "uuid": "6bf7befb-6a47-4722-a031-f04822d5997c",
    "name": "HOSTNAME (Channel 1)",
    "address": "192.168.1.100",
    "port": 5961,
    "eventsSubscribed": true,
    "audioCodec": "pcm",
    "audioChannels": 2,
    "audioSampleRate": 48000,
    "audioFramesSent": 123456,
    "audioBytesSent": 234567890
  }
]
```

## RecvAdvertiser & RecvListener Implementation

### Overview

Similar to SendAdvertiser/SendListener, the RecvAdvertiser/RecvListener APIs enable monitoring and control of NDI receivers registered on the Discovery Server.

### Key Concepts

**RecvAdvertiser**:
- Registers local NDI receivers on the Discovery Server
- Enables remote monitoring and control of receivers
- Must call `NDIlib_recv_advertiser_add_receiver()` with `allow_controlling` and `allow_monitoring` flags

**RecvListener**:
- Discovers receivers registered on the Discovery Server
- Subscribes to receiver events
- Can send control commands to receivers

### Step 1: Add RecvAdvertiser P/Invoke Declarations (NDIWrapper.cs)

```csharp
// RecvAdvertiser structures
[StructLayout(LayoutKind.Sequential)]
public struct recv_advertiser_create_t
{
    public IntPtr p_url_address;  // URL of Discovery Server (IntPtr.Zero for default)
}

// RecvAdvertiser functions
[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_create")]
public static extern IntPtr NDIlib_recv_advertiser_create(ref recv_advertiser_create_t p_create_settings);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_destroy")]
public static extern void NDIlib_recv_advertiser_destroy(IntPtr p_instance);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_add_receiver")]
public static extern bool NDIlib_recv_advertiser_add_receiver(
    IntPtr p_instance,
    IntPtr p_receiver,
    bool allow_controlling,      // Enable remote control commands
    bool allow_monitoring,       // Enable monitoring events
    string? p_input_group_name); // Optional input group filter (null = none)

[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_advertiser_del_receiver")]
public static extern bool NDIlib_recv_advertiser_del_receiver(IntPtr p_instance, IntPtr p_receiver);
```

### Step 2: Create RecvAdvertiser in Manager

```csharp
public class NDIManager
{
    private IntPtr _sendAdvertiserInstance = IntPtr.Zero;
    private IntPtr _recvAdvertiserInstance = IntPtr.Zero;

    public bool Initialize()
    {
        // ... SendAdvertiser creation ...

        // Create RecvAdvertiser for Discovery Server registration
        var recvAdvertiserSettings = new NDIWrapper.recv_advertiser_create_t
        {
            p_url_address = IntPtr.Zero  // Use default Discovery Server
        };
        _recvAdvertiserInstance = NDIWrapper.NDIlib_recv_advertiser_create(ref recvAdvertiserSettings);
        Console.WriteLine($"[NDIManager] RecvAdvertiser created: {(_recvAdvertiserInstance != IntPtr.Zero ? "Success" : "Failed")}");

        // Pass both advertisers to channels
        for (int i = 1; i <= 16; i++)
        {
            _channels[i] = new NDIChannel(i, _sendAdvertiserInstance, _recvAdvertiserInstance);
        }

        return true;
    }

    public void Dispose()
    {
        // ... dispose channels ...

        if (_sendAdvertiserInstance != IntPtr.Zero)
        {
            NDIWrapper.NDIlib_send_advertiser_destroy(_sendAdvertiserInstance);
            _sendAdvertiserInstance = IntPtr.Zero;
        }

        if (_recvAdvertiserInstance != IntPtr.Zero)
        {
            NDIWrapper.NDIlib_recv_advertiser_destroy(_recvAdvertiserInstance);
            _recvAdvertiserInstance = IntPtr.Zero;
        }
    }
}
```

### Step 3: Register Receivers on Discovery Server

```csharp
public class NDIChannel
{
    private IntPtr _receiverInstance = IntPtr.Zero;
    private IntPtr _recvAdvertiser = IntPtr.Zero;

    public NDIChannel(int channelNumber, IntPtr sendAdvertiser, IntPtr recvAdvertiser)
    {
        ChannelNumber = channelNumber;
        _sendAdvertiser = sendAdvertiser;
        _recvAdvertiser = recvAdvertiser;
    }

    public void RecreateReceiver(NDIWrapper.NDIlib_source_t? source)
    {
        DestroyReceiver();

        if (!source.HasValue)
            return;

        // Create receiver with vendor credentials
        const string VENDOR_NAME = "YOUR_VENDOR_NAME";
        const string VENDOR_CODE = "YOUR_VENDOR_CODE";
        string configJson = $@"{{""ndi"":{{""vendor"":{{""name"":""{VENDOR_NAME}"",""id"":""{VENDOR_CODE}""}}}}}}";

        _receiverInstance = NDIWrapper.NDIlib_recv_create_v4(ref recvSettings, configJson);

        if (_receiverInstance != IntPtr.Zero)
        {
            // Register receiver on Discovery Server for monitoring and control
            if (_recvAdvertiser != IntPtr.Zero)
            {
                bool added = NDIWrapper.NDIlib_recv_advertiser_add_receiver(
                    _recvAdvertiser,
                    _receiverInstance,
                    true,  // allow_controlling = enable remote control
                    true,  // allow_monitoring = enable monitoring events
                    null   // p_input_group_name = no input group filter
                );
                Console.WriteLine($"[Channel {ChannelNumber}] Receiver registered on Discovery Server: {(added ? "Success" : "Failed")}");
            }
        }
    }

    private void DestroyReceiver()
    {
        if (_receiverInstance != IntPtr.Zero)
        {
            // Remove from Discovery Server before destroying
            if (_recvAdvertiser != IntPtr.Zero)
            {
                NDIWrapper.NDIlib_recv_advertiser_del_receiver(_recvAdvertiser, _receiverInstance);
            }

            NDIWrapper.NDIlib_recv_destroy(_receiverInstance);
            _receiverInstance = IntPtr.Zero;
        }
    }
}
```

### Receiver Monitoring Events

Expected receiver event names (from NDI SDK documentation):

#### Status Events
- `status` - Receiver status ("connected", "disconnected", "connecting")
- `source-name` - Currently connected source name
- `source-uuid` - Currently connected source UUID

#### Statistics Events
- `audio-frames-received` - Total audio frames received (long)
- `video-frames-received` - Total video frames received (long)
- `audio-bytes-received` - Total audio bytes received (long)
- `video-bytes-received` - Total video bytes received (long)

#### Stream Properties Events
- `current-audio-codec` - Current audio codec being received
- `current-video-codec` - Current video codec being received
- `current-audio-channels` - Current audio channel count
- `current-audio-samplerate` - Current audio sample rate

### RecvListener Implementation Notes

RecvListener is used to discover and monitor receivers registered by OTHER applications on the Discovery Server. For monitoring your own local receivers, the event data comes through the same event polling mechanism as SendListener.

#### CRITICAL: Correct receiver_t Structure

The `receiver_t` structure MUST match the C++ definition exactly. Missing fields will cause `AccessViolationException` crashes:

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct receiver_t
{
    public IntPtr p_uuid;          // Receiver UUID
    public IntPtr p_name;          // Receiver name
    public IntPtr p_input_uuid;    // Input group UUID
    public IntPtr p_input_name;    // Input group name
    public IntPtr p_address;       // IP address of receiver (CRITICAL - don't omit!)
    public IntPtr p_streams;       // Array of NDIlib_receiver_type_e (CRITICAL - don't omit!)
    public uint num_streams;       // Number of streams (CRITICAL - don't omit!)
    public IntPtr p_commands;      // Array of NDIlib_receiver_command_e (CRITICAL - don't omit!)
    public uint num_commands;      // Number of commands (CRITICAL - don't omit!)

    [MarshalAs(UnmanagedType.U1)]
    public byte events_subscribed; // 1 if subscribed to events, 0 otherwise
}
```

**Common Mistake**: Omitting `p_address`, `p_streams`, `num_streams`, `p_commands`, `num_commands` fields causes memory alignment issues and crashes during marshaling.

#### RecvListener API

If you need to implement RecvListener for discovering external receivers:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct recv_listener_create_t
{
    public IntPtr p_url_address;  // URL of Discovery Server
}

[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_create")]
public static extern IntPtr NDIlib_recv_listener_create(ref recv_listener_create_t p_create_settings);

[DllImport(NDI_LIB, EntryPoint = "NDIlib_recv_listener_get_receivers")]
public static extern IntPtr NDIlib_recv_listener_get_receivers(IntPtr p_instance, ref uint p_num_receivers);

// Event subscription works similarly to SendListener
```

### Complete Architecture Diagram

```
Local Application:
  ├─ NDI Sender 1 ──> SendAdvertiser ──> Discovery Server
  ├─ NDI Sender 2 ──> SendAdvertiser ──> Discovery Server
  ├─ NDI Sender N ──> SendAdvertiser ──> Discovery Server
  ├─ NDI Receiver 1 ──> RecvAdvertiser ──> Discovery Server
  ├─ NDI Receiver 2 ──> RecvAdvertiser ──> Discovery Server
  └─ NDI Receiver N ──> RecvAdvertiser ──> Discovery Server

Remote Monitoring Application:
  ├─ SendListener <──┐
  └─ RecvListener <──┤
                     │
              Discovery Server
                (192.168.15.60:5959)
```

### Testing RecvAdvertiser

```bash
# Check console output during startup:
[NDIManager] SendAdvertiser created: Success
[NDIManager] RecvAdvertiser created: Success

# When a receiver connects to a source:
[NDIChannel 1] Receiver registered on Discovery Server: Success
[NDIChannel 2] Receiver registered on Discovery Server: Success
```

### Use Cases

**RecvAdvertiser** enables:
1. **Remote Monitoring**: View receiver status and statistics from remote applications
2. **Remote Control**: Control receiver operations (connect/disconnect, change source)
3. **Centralized Management**: Monitor all receivers in your facility from a single dashboard
4. **Load Balancing**: Distribute sources across receivers based on load statistics

### Security Considerations

- `allow_controlling = true` enables remote control of receivers
- Only set `allow_controlling = true` if remote control is desired
- Discovery Server should be on a trusted network
- Consider firewall rules to restrict Discovery Server access

## References

- NDI SDK Documentation: Processing.NDI.SendAdvertiser.h
- NDI SDK Documentation: Processing.NDI.SendListener.h
- NDI SDK Documentation: Processing.NDI.RecvAdvertiser.h
- NDI SDK Documentation: Processing.NDI.RecvListener.h
- NDI Events Implementation Guide (vendor documentation)
