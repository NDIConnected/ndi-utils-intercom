# NDI Intercom — Technical Architecture

## Overview

NDI Intercom is a multi-channel audio intercom built on NDI with optional ASIO hardware I/O (Windows only). Two products share one codebase:

| Product | Channels | Entry point | Default port |
|---------|----------|-------------|--------------|
| NDI Intercom16 | 16 | `Program.cs` | 5016 |
| NDI Intercom2 | 2 | `Program.Light.cs` | 5017 |

On **Windows**, each product runs as a **system-tray desktop app** with an embedded ASP.NET Core host (Kestrel + SignalR). On **Linux**, the same projects build a headless web host (no tray, no ASIO).

Every instance is **identity-aware**: an `(application_id, device_id)` pair tags all NDI endpoints so management tools can aggregate senders and receivers. Full spec: [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

## Startup

`IntercomAppHost.cs` bootstraps both products:

- Registers controllers, SignalR, static files (`wwwroot/`)
- Starts `IntercomEngine` (audio + NDI)
- On Windows, shows `TrayApplicationContext` for tray menu, port, and LAN access settings

Configuration and presets: `%ProgramData%\NDI Intercom16\` or `%ProgramData%\NDI Intercom2\` (Windows); `~/.local/share/NDI/…` (Linux).

## Core components

```
IntercomEngine          Central orchestrator (mic, channels, mode switching)
├── NDIManager          NDI send/receive per channel, Discovery Server adverts
├── AudioEngine         Microphone capture (WASAPI on Windows, parec on Linux)
├── AsioAudioEngine     ASIO I/O + N-1 group mixing (Windows only)
├── AsioManager         ASIO driver enumeration and init
├── ConfigManager       Atomic JSON config persistence
├── PresetManager       Channel preset load/save
└── MixingEngine        Float/byte conversions, cross-mode mixing helpers
```

**Discovery listeners** (`NDISendListener`, `NDIRecvListener`, `NDIDiscoveryService`, `NDISenderDiscoveryService`) poll the NDI Discovery Server for remote monitoring and source lists.

**NDIWrapper** — P/Invoke to `Processing.NDI.Lib.x64.dll` (Windows) or `libndi.so.6` (Linux).

## Audio paths

### NDI — Listen
```
NDI source → receiver → ring buffer → output
```

### NDI — Talk
```
Microphone → sender → network
```

### ASIO — Intercom group (N-1)
```
Each channel output = mic + sum(other group members, excluding self)
```

NDI and ASIO channels can share the same intercom group (cross-mode N-1 mixing).

**Real-time constraints:** no LINQ or per-sample allocations in ASIO/NDI callbacks; `ArrayPool` and ring buffers in the hot path.

## Web layer

| Layer | Role |
|-------|------|
| `Controllers/` | REST API (channels, NDI, ASIO, presets, bridge) |
| `Hubs/IntercomHub` | SignalR — VU meters, product info, live updates |
| `wwwroot/` | Web UI (shared by both products) |

API reference: [API_REST_GUIDE.md](API_REST_GUIDE.md).

## Threading

- **ASP.NET thread pool** — HTTP / SignalR
- **Dedicated audio thread** — microphone processing in `IntercomEngine`
- **ASIO callback thread** — real-time I/O (Windows)
- **NDI SDK internal threads** — send/receive

Per-channel NDI lifetime locks (v1.7+) serialize native handle changes against audio I/O.

## Health & logging

- `GET /healthz` — returns 200 when the engine and audio thread are running
- Rolling log files via `FileLoggerProvider` (14-day retention by default)

## Build layout

```
Program.cs / Program.Light.cs     Product entry points
Program.Linux.cs / Program.Linux.Light.cs   Linux entry points
Directory.Build.props             Separate bin/obj per product; Linux NDI lib copy
installer-script-intercom16.iss   Windows installer (16 ch)
installer-script-intercom2.iss      Windows installer (2 ch)
StreamDeck-Plugin/                  Optional Elgato plugin (Intercom16 only)
```

## Related docs

- [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md) — NDI identity spec
- [NDI-BRIDGE-INTEGRATION.md](NDI-BRIDGE-INTEGRATION.md) — Bridge controls
- [CHANGELOG.md](../CHANGELOG.md) — release history
