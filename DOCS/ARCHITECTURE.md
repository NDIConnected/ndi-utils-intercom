# NDI Intercom16 - Technical Architecture

## System Overview

NDI Intercom16 is a multi-channel audio intercom system built on NDI technology with ASIO hardware integration. The application runs as a Windows service (or system-tray app) and provides 16 independent channels that can operate in different modes. A 2-channel variant (NDI Intercom2) shares the same codebase via a runtime product profile (`IntercomProductOptions`).

Each running process is **identity‑aware**: it carries an `(application_id, device_id)` pair that tags every NDI sender and receiver, so a management application can discover and aggregate Intercom instances on the network. See [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md) for the full specification.

On **Linux** the same solution builds a **headless** ASP.NET Core host (no system tray): `IntercomAppHost` listens on the configured HTTP port and the process runs until Ctrl+C. Configuration and presets live under `~/.local/share/NDI/<product data folder>/` via `ConfigManager`.

**NDI** uses P/Invoke against **`libndi.so.6`**, expected next to the published binary (MSBuild copies it from the NDI SDK for Linux when found; see `Directory.Build.props` and the root `README.md`). Runtime discovery for health-style checks mirrors that layout in `NDIWrapper.IsDllAccessible()`.

**Local audio** is implemented under `Core/Linux/` (not WASAPI): capture via **`parec`**, playback via **`pacat`**, targeting PipeWire’s PulseAudio compatibility layer (`LinuxPulseCompat`). **ASIO** exists only in **Windows** builds (`#if WINDOWS` in `IntercomEngine` and related SignalR methods): no ASIO types, processing paths, or settings UI are compiled or exposed on Linux. `GetProductInfo` reports `AsioAvailable: false` and `AudioBackendName: PipeWire / PulseAudio` on non-Windows.

For SSH or minimal sessions, use `scripts/run-intercom-linux.sh` so user PipeWire units and the Pulse socket under `$XDG_RUNTIME_DIR` are available (see root `README.md`).

## Core Components

### 1. IntercomEngine (`Core/IntercomEngine.cs`)
**Central orchestrator** that manages all subsystems.

**Responsibilities**:
- Initializes and coordinates all managers (NDI, ASIO, Audio)
- Manages microphone input capture
- Routes audio between NDI/ASIO channels and microphone
- Handles channel state updates and mode switching

**Key Methods**:
- `StartEngine()` - Initialize all subsystems
- `UpdateChannelStates()` - Propagate channel state changes
- `ProcessMicrophoneAudio()` - Route microphone to active channels

### 2. NDIManager (`Core/NDIManager.cs`)
**NDI network audio management** with Discovery Server integration.

**Features**:
- **N NDI Channels** (16 for Intercom16, 2 for Intercom2): each channel is a separate `NDIChannel` instance
- **Sender Creation**: creates an NDI sender per channel, with an identity‑tagged name and `<ndi_product>` / `<ndi_manager>` / `<ndi_capabilities>` connection metadata
- **Persistent Receivers**: each channel owns one long‑lived receiver, advertised on the Discovery Server with `allow_controlling=true, allow_monitoring=true`. Source changes use `NDIlib_recv_connect` (no destroy/recreate)
- **SendAdvertiser**: registers senders on the Discovery Server for remote monitoring
- **RecvAdvertiser**: registers all receivers on the Discovery Server for remote monitoring and control
- **Identity propagation**: `SetIdentity(applicationId, deviceId)` updates every channel's sender + receiver name and metadata
- **Metadata**: sends channel info as `<ndi_product>` (vendor/version/serial) and `<ndi_manager>` (`app_id`/`device_id`/`role`/`channel_index`/`schema`)

**Constants**:
```csharp
AUDIO_SAMPLE_RATE = 48000;           // NDI standard sample rate
NDI_FRAME_SAMPLES = 1920;            // 40ms frames @ 48kHz
RING_BUFFER_SAMPLES_300MS = 14400;   // 300ms buffering
AUDIO_BUFFER_SIZE = 7680;            // Buffer size in bytes
```

**Audio Flow**:
- **Send**: Microphone → NDI sender → Network
- **Receive**: Network → NDI receiver → Ring buffer → Audio output

**NDIChannel Class**:
- Manages individual sender/receiver instances
- Ping-pong buffer allocation (zero-copy)
- 16-bit audio format for NDI transmission
- Builds the identity-tagged NDI name (`<friendly> [app=…;device=…;role=…;ch=N]`) and the `<ndi_manager>` XML metadata (see [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md))
- Owns a persistent receiver for the channel's lifetime; `ConnectReceiver` / `DisconnectReceiver` switch sources without recreating it

### 3. AsioAudioEngine (`Core/AsioAudioEngine.cs`)
**ASIO hardware audio routing** with N-1 intercom group mixing.

**Zero-Allocation Design**:
- Uses `ArrayPool<float>` and `ArrayPool<byte>` for audio buffers
- Prevents GC pressure in real-time audio path
- All buffers rented/returned in try-finally blocks

**Key Features**:

**N-1 Mixing Logic**:
```
For each ASIO channel in an intercom group:
  Output = Microphone + Mix(all other channels in group EXCEPT self)
```

**Ring Buffers**:
- `_microphoneRingBuffers`: Per-output-channel buffers for microphone audio
- `_asioInputRingBuffers`: Per-input-channel buffers for received audio
- `_lastAsioInputCache`: Cached frames for non-blocking reads

**Audio Processing**:
- `ProcessAsioInput()`: ASIO callback, receives input and writes to ring buffers
- `Read()`: IWaveProvider implementation, generates output with N-1 mix
- `GetAsioInputAudio()`: Non-blocking read for NDI drain (uses TryEnter)

**Buffer Sizes**:
- Ring buffers: 400ms (19200 samples @ 48kHz)
- Designed to handle ASIO callback jitter

### 4. AudioEngine (Windows: `Core/AudioEngine.cs`, Linux: `Core/Linux/AudioEngine.cs`)
**Microphone capture and management**.

**Windows**:
- Microphone input via WaveIn/WASAPI
- Audio format conversion (mono, 48kHz, float32)
- Level monitoring and VU meter calculation
- Audio callback to IntercomEngine

**Linux**: Pulse-compatible I/O via `parec` / `pacat` (see Linux section above).

### 5. AsioManager (`Core/AsioManager.cs`)
**ASIO driver initialization and management**.

**Features**:
- ASIO driver enumeration
- Driver initialization with channel mapping
- Integration with AsioAudioEngine for audio callbacks

### 6. Discovery Server Integration

**SendAdvertiser** (`Core/NDIManager.cs:58-62`):
- Registers NDI senders on Discovery Server for monitoring

**RecvAdvertiser** (`Core/NDIManager.cs:65-69`):
- Registers as an NDI receiver on Discovery Server for monitoring

**NDISendListener** (`Core/NDISendListener.cs`):
- Monitors senders registered on Discovery Server
- Subscribes to sender events
- Provides sender statistics

**NDIRecvListener** (`Core/NDIRecvListener.cs`):
- Monitors receivers registered on Discovery Server
- Subscribes to receiver events
- Tracks receiver connection status

**Vendor Credentials**:
- Not required for NDI 6 SDK receiver/sender discovery and monitoring.

### 7. Identity & Discovery (v1.7+)

Every NDI endpoint published by Intercom carries a stable identity so external **management / aggregation applications** can group the N senders + N receivers belonging to the same instance, distinguish multiple Intercoms on the network, and deep-link to the per-instance Web UI.

**Two identity fields** persisted in `AppConfig` (`%PROGRAMDATA%\NDI Intercom16\config.json`):

- `ApplicationId` — logical label, default `"Intercom_A"`, regex `^[A-Za-z0-9_.-]{1,64}$`. Editable from the Web UI.
- `DeviceId` — stable instance UID, auto-generated on first run as a GUID and persisted.

**Three propagation channels** (redundant, pick the one that fits):

1. **NDI source name suffix** — every sender / receiver name ends with `[app=<APP_ID>;device=<DEVICE_ID>;role=(sender|receiver);ch=<N>]`. Parseable without opening a connection.
2. **`<ndi_manager>` connection metadata** on senders:
   ```xml
   <ndi_manager app_id="…" device_id="…" channel_id="ch-N" channel_index="N" role="sender" schema="1" />
   ```
3. **`<ndi_capabilities web_control="http://%IP%:<port>/" />`** on senders, so NDI Studio Monitor (and similar) display a *Web Control* link that opens the Intercom's Settings UI.

**Persistent receiver lifecycle**: each channel creates its receiver during `NDIManager.Initialize` and disposes it only on shutdown. Source changes use `NDIlib_recv_connect`. Receivers are advertised with `allow_controlling=true, allow_monitoring=true`, so a Discovery Server-aware controller can connect / disconnect them remotely.

**NDI groups are not used** for routing or aggregation; senders are created with `p_groups = IntPtr.Zero` and the send listener ignores the `Groups` field.

The full specification (regex grammar, schema versioning policy, aggregator recipe, migration from v1.x) lives in [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

## Data Flow

### NDI Mode - Listen
```
NDI Source → NDI Receiver → Ring Buffer → Audio Output
                                ↓
                         Discovery Server monitoring (requires Discovery Server configuration)
```

### NDI Mode - Talk
```
Microphone → NDI Sender → Network → All connected receivers
                ↓
         Discovery Server monitoring (requires Discovery Server configuration)
```

### ASIO Mode - Intercom Group
```
Channel 1 ASIO Input ──┐
Channel 2 ASIO Input ──┼─→ N-1 Mixer ──→ Channel 1 Output (hears 2+3)
Channel 3 ASIO Input ──┘                → Channel 2 Output (hears 1+3)
                                        → Channel 3 Output (hears 1+2)
```

### Combined Mode (ASIO Talk + Listen)
```
Microphone ──→ ASIO Output Channel (with N-1 mix)
ASIO Input ──→ NDI Sender → Network
```

## Configuration Storage

### ConfigManager (`Core/ConfigManager.cs`)
- Stores configuration in `%PROGRAMDATA%\NDI Intercom16\config.json` (Intercom16) or `…\NDI Intercom2\config.json` (Intercom2)
- Auto-saves on changes
- JSON serialization of all channel states
- Persists identity fields (`applicationId`, `deviceId`); `deviceId` is auto-generated on first boot if missing

### PresetManager (`Core/PresetManager.cs`)
- Presets stored in `%PROGRAMDATA%\NDI Intercom16\Presets\`
- Each preset is a JSON file with all channel configurations
- Load/save via API

## Models

### ChannelState (`Models/ChannelState.cs`)
Complete channel configuration:
- Channel number, name, color
- Mode (NDI or ASIO)
- NDI source, ASIO input/output channels
- Listen/Talk enabled flags
- Input/output levels
- ASIO intercom group

### Channel Modes
```csharp
enum ChannelMode
{
    NDI,   // Network audio via NDI
    ASIO   // Hardware audio via ASIO
}
```

## API Layer

### IntercomController (`Controllers/IntercomController.cs`)
RESTful API endpoints for:
- Channel status and configuration
- NDI source discovery
- Discovery Server monitoring (senders/receivers)
- ASIO driver management
- Preset management

## Performance Optimizations

### Zero-Allocation Audio Path
- ArrayPool usage in AsioAudioEngine
- Ping-pong buffers in NDIChannel
- No LINQ in audio callbacks
- Pre-allocated ring buffers

### Non-Blocking Design
- `TryEnter` locks in GetAsioInputAudio
- Cached frames for lock contention
- Prevents ASIO callback blocking NDI drain

### Buffer Management
- Ring buffers: 300-400ms capacity
- Handles timing jitter between ASIO/NDI
- Graceful underrun handling (silence)

## Error Handling

- Silent error handling in audio path (prevents service crashes)
- Try-catch blocks in all callbacks
- Graceful degradation (silence on buffer underrun)
- Finally blocks ensure buffer return to pools

## Threading Model

- **Main Thread**: ASP.NET Core HTTP server
- **Audio Thread**: Microphone capture callback
- **ASIO Thread**: ASIO driver callback (real-time priority)
- **NDI Threads**: NDI SDK internal threads
- **Lock Coordination**: `_bufferLock` for ring buffer access

## Native Interop

### NDIWrapper (`Core/NDIWrapper.cs`)
P/Invoke wrapper for NDI SDK DLL.

**Key Functions**:
- Sender: `NDIlib_send_create_ex`, `NDIlib_util_send_send_audio_interleaved_16s`, `NDIlib_send_add_connection_metadata`
- Receiver: `NDIlib_recv_create_v3`, `NDIlib_recv_capture_v3`, `NDIlib_recv_connect`, `NDIlib_recv_add_connection_metadata`, `NDIlib_recv_clear_connection_metadata`
- Discovery: `NDIlib_find_create_v2`, `NDIlib_find_get_current_sources`
- Advertiser: `NDIlib_send_advertiser_create`, `NDIlib_send_advertiser_add_sender`, `NDIlib_recv_advertiser_create`, `NDIlib_recv_advertiser_add_receiver`
- Listener: `NDIlib_send_listener_create`, `NDIlib_recv_listener_create_ex`

**Native library**:
- **Windows**: `Processing.NDI.Lib.x64.dll` (NDI 6.x); standard SDK / install paths
- **Linux**: `libndi.so.6` next to the app or under the NDI SDK for Linux tree (`lib/<triplet>/`), same conventions as `Directory.Build.props`

## Service Management

### Program.cs / WindowsServiceHelpers.cs
- Runs as Windows service via `Microsoft.Extensions.Hosting.WindowsServices`
- Service name: "NDI Intercom16"
- Automatic startup configuration
- Graceful shutdown with cleanup

## Installer

Uses Inno Setup to create Windows installer:
- Service installation and configuration
- File deployment
- License acceptance
- Uninstall with service stop

## Future Considerations

- Multi-instance support (multiple services)
- External mixer control integration
- Advanced routing matrix
- Authentication/authorization for API
- WebSocket for real-time updates

