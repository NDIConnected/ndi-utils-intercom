# NDI Intercom16 & NDI Intercom2

> **Sample / reference software.** NDI Intercom is an MIT-licensed community sample, **not officially supported** as a production product. The embedded control API has **no authentication**; the default configuration binds the web UI to **localhost only**. See [SECURITY.md](SECURITY.md) before exposing the service on a network.

Professional NDI-based intercom for **Windows** (desktop + installer) and **Linux** (headless server + web UI): **16-channel** full product and **2-channel** compact build, sharing one codebase (NDI + optional ASIO on Windows).

## Products

| | **NDI Intercom16** | **NDI Intercom2** |
|---|-------------------|-------------------|
| Channels | 16 | 2 |
| Executable | `NDI Intercom16.exe` | `NDI Intercom2.exe` |
| Default web UI | [http://localhost:5016](http://localhost:5016) | [http://localhost:5017](http://localhost:5017) |
| Settings / presets | `%ProgramData%\NDI Intercom16\` | `%ProgramData%\NDI Intercom2\` |
| NDI model name | Intercom-16CH | Intercom-2CH |

Both Windows builds are **self-contained** .NET 8 desktop apps (system tray + embedded Kestrel + SignalR). On **Linux**, the same projects build a **headless web host** (no tray, no ASIO, no installer) controlled via browser — see [Linux headless server build](#linux-headless-server-build) below.

**NDI Tools / NDI runtime** must be installed separately on target machines. **Processing.NDI.Lib.x64.dll** (Windows) or **`libndi.so.6`** (Linux) is deployed next to the executable (from the NDI SDK on the build machine).

The **Stream Deck** plugin in this repo targets 16-channel workflows; the Intercom2 installer does not bundle it.

## Key features

- **NDI**: send/receive audio, discovery, optional NDI Bridge controls in Settings (see [NDI-BRIDGE-INTEGRATION.md](DOCS/NDI-BRIDGE-INTEGRATION.md))
- **Identity-aware NDI routing (v1.7+)**: every sender / receiver is tagged with `(application_id, device_id)` plus `<ndi_manager>` connection metadata and a `<ndi_capabilities web_control>` link, so a management application can aggregate the 16+16 endpoints of an instance, distinguish multiple Intercoms on the network, and deep-link to the per-instance Web UI. Receivers are persistent and can be controlled remotely via the Discovery Server. Full spec: [DOCS/IDENTITY-AWARE-ROUTING.md](DOCS/IDENTITY-AWARE-ROUTING.md).
- **Stereo NDI receive fix (v1.7.2)**: incoming stereo NDI flows (FLTP planar) are decoded correctly.
- **NDI source name suffix mode (v1.7.1)**: `Full` / `Compact` / `Off` in Settings for legacy receiver compatibility. Default `Compact`.
- **24/7 stability + identity routing (v1.7.0)**: watchdog, atomic config, `/healthz`, `(application_id, device_id)` tagging.

See [CHANGELOG.md](CHANGELOG.md) for full release notes.
- **ASIO** (Windows): hardware I/O, N-1 group mixing, low-allocation audio path
- **Web UI**: channel control, VU meters, presets, REST API
- **Two entry points**: `Program.cs` (16ch) and `Program.Light.cs` (2ch) via `IntercomAppHost`

## Technology stack

- .NET 8, ASP.NET Core, SignalR
- **Windows:** NAudio (WASAPI + ASIO), system tray, Inno Setup installers
- **Linux:** PipeWire / PulseAudio (`parec`, `pacat`, `pactl`), headless host, `libndi.so.6`
- NDI SDK 6 (Free)

## Requirements

### Windows (desktop / installer)

- Windows 10/11 **x64**
- **NDI runtime** (e.g. NDI Tools) on machines that run the app
- **Visual C++ Redistributable (x64)** on clean PCs (native NDI dependency)
- For **building:** .NET 8 SDK, **NDI 6 SDK** under `C:\Program Files\NDI\NDI 6 SDK\`, optional Inno Setup 6

### Linux (headless server; build from source)

- Linux **x64** with .NET 8 SDK or runtime
- **NDI SDK for Linux** with `libndi.so.6`
- PipeWire/PulseAudio and `pulseaudio-utils` (`parec`, `pacat`, `pactl`)
- No Windows installer — build and run via scripts (see below)

## Building (development)

The solution includes both apps:

```bash
dotnet build "NDI-Intercom.sln" -c Release
```

Or build one project:

```bash
dotnet build "NDI Intercom16.csproj" -c Release
dotnet build "NDI Intercom2.csproj"  -c Release
```

**Note:** Both `.csproj` files live in the **same folder**. [Directory.Build.props](Directory.Build.props) gives each product its own `bin\` / `obj\` subtree and excludes local `publish*` folders from SDK globs so builds stay clean.

### Linux headless server build

Same **Intercom16** and **Intercom2** codebase on Linux: headless ASP.NET Core host, **same web UI and REST API** as Windows, native NDI via **`libndi.so.6`**, local audio via **PipeWire / PulseAudio**. There is **no Linux installer** — build from source and use the helper scripts below. ASIO, system tray, and Stream Deck remain **Windows-only**.

On a Linux machine with **.NET 8 SDK**, `dotnet build` uses `net8.0` and copies **`libndi.so.6`** next to the app when the SDK layout `<root>/lib/<triplet>/libndi.so.6` is found ([Directory.Build.props](Directory.Build.props)). If **`NDI_SDK_LINUX_ROOT`** is unset, the build picks the first path that contains that file: **`~/NDI SDK for Linux`** (official installer layout), then **`~/SDK/NDI_SDK_for_Linux`**. Override with **`NDI_SDK_LINUX_ROOT`**, **`NDI_SDK_LINUX_ARCH`** (e.g. `aarch64-rpi4-linux-gnueabi` on some ARM64 trees), or **`NDI_LINUX_LIB`** pointing at any `libndi.so*`. **Runtime packages** (typical Ubuntu): `sudo apt install libavahi-client3 libavahi-common3 libpulse0 pipewire-pulse pulseaudio-utils`. The **`pulseaudio-utils`** package provides **`parec`**, **`pacat`**, and **`pactl`**, which the app invokes for capture/playback and device listing; without them local audio will not start.

**SSH / headless:** The USB microphone is captured **on the server**. PipeWire’s Pulse socket lives under `$XDG_RUNTIME_DIR/pulse/native` and is created by **user** services (`pipewire`, `pipewire-pulse`, `wireplumber`). If that directory is empty, `pactl` fails with *Connection refused* and the VU meter stays flat. From the same user as the login session, start the app with **[scripts/run-intercom-linux.sh](scripts/run-intercom-linux.sh)** (16-channel, port 5016) or **[scripts/run-intercom2-linux.sh](scripts/run-intercom2-linux.sh)** (2-channel, port 5017); each script runs `systemctl --user start …` before launching. For a server **without** a GUI login, enable lingering once: `sudo loginctl enable-linger $(whoami)`, then SSH in and run the script (or enable user units: `systemctl --user enable pipewire.socket pipewire-pulse.socket wireplumber.service`).

## Publishing & installers

### NDI Intercom16

1. Publish (self-contained **win-x64** matches the project RID):

```bash
dotnet publish "NDI Intercom16.csproj" -c Release -r win-x64 --self-contained true -o publish ^
  /p:PublishSingleFile=false /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true
```

2. Ensure `publish\` contains `Processing.NDI.Lib.x64.dll` (copied from the SDK path in the `.csproj`, or copy manually).

3. Compile **Inno Setup** with [installer-script-intercom16.iss](installer-script-intercom16.iss) (expects output under `publish\`).  
   Or run [build-intercom16-installer.cmd](build-intercom16-installer.cmd) to publish and compile in one step.  
   Result: `Installer-Output\NDI-Intercom16-Setup-v1.7.4.exe` (version follows `#define` in the `.iss`).

### NDI Intercom2

1. Run [build-intercom2-installer.cmd](build-intercom2-installer.cmd) (publishes to `publish-intercom2\`, copies NDI DLL if the SDK path exists, then runs `ISCC` if Inno is installed).

   Or manually:

```bash
dotnet publish "NDI Intercom2.csproj" -c Release -r win-x64 --self-contained true -o publish-intercom2 ^
  /p:PublishSingleFile=false /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true
```

2. Compile [installer-script-intercom2.iss](installer-script-intercom2.iss) from the repo root (Inno `Source` paths are relative to the `.iss` file). **You must publish first** so `publish-intercom2\` is populated; otherwise Inno reports no files for `publish-intercom2\*`.

   Result: `Installer-Output\NDI-Intercom2-Setup-v1.7.4.exe` (version in the `.iss` / `.csproj` should be kept in sync).

## Installation (end users)

- **Intercom16**: installs under `C:\Program Files\NDI\NDI Intercom16` (per installer script).
- **Intercom2**: installs under `C:\Program Files\NDI\NDI Intercom2`.

After install, use the **system tray** icon (hidden icons area) to open the web UI or open **Web Server Settings…** to change the HTTP port and allow control from other computers on the LAN (writes `%ProgramData%\NDI Intercom16\appsettings.json`; restart required). The web UI listens on **http://127.0.0.1:{port}** by default.

### Network exposure

The embedded Kestrel server defaults to **localhost only** for remote clients. **Localhost on this PC is always available** (`http://127.0.0.1:{port}`). To allow other PCs on the network, use the tray menu **Web Server Settings…** and choose a **selected network interface** (recommended on multi-NIC machines) or all interfaces (advanced). Alternatively set `WebServer:RemoteAccess` to `Interface` or `All` in `%ProgramData%\NDI Intercom16\appsettings.json` (or `%ProgramData%\NDI Intercom2\appsettings.json`). There is no authentication — restrict access with firewall rules or VPN on untrusted networks. See [SECURITY.md](SECURITY.md).

## Configuration

- **Intercom16**: `%ProgramData%\NDI Intercom16\config.json`, presets under `Presets\`
- **Intercom2**: `%ProgramData%\NDI Intercom2\config.json`, presets under `Presets\`

### Channel modes (summary)

- **NDI**: listen to an NDI source, talk sends mic audio to NDI receivers; each channel exposes an NDI sender on the network.
- **ASIO**: map hardware inputs/outputs and intercom groups with N-1 mixing (see [ARCHITECTURE.md](DOCS/ARCHITECTURE.md)).

## API endpoints

REST base path: `/api/intercom/…` — full reference in [API_REST_GUIDE.md](DOCS/API_REST_GUIDE.md).

Common routes:

- `GET /api/intercom/channels`, `GET /api/intercom/channels/{id}`, `POST /api/intercom/channels/{id}/talk`, `POST /api/intercom/channels/{id}/listen`
- `GET /api/system/status`, `GET /healthz`
- `GET /api/ndi/senders`, `GET /api/ndi/receivers` (Discovery Server)
- `GET/POST /api/intercom/presets/*`, `POST /api/intercom/config/export`, `POST /api/intercom/config/import`

Details: [API_REST_GUIDE.md](DOCS/API_REST_GUIDE.md), [INTERCOM USER_MANUAL.md](DOCS/INTERCOM%20USER_MANUAL.md).  
Documentation index: [DOCS/README.md](DOCS/README.md). Release history: [CHANGELOG.md](CHANGELOG.md).

## Architecture & project layout

See [ARCHITECTURE.md](DOCS/ARCHITECTURE.md) for components, audio pipeline, and threading.

```
├── Core/                    # Engine, NDI, ASIO, config, discovery listeners
├── DOCS/                    # User manual, API guide, NDI Bridge guides, architecture
├── Models/                  # Channel state, IntercomProductOptions, AppConfig, …
├── Hubs/                    # SignalR (incl. GetProductInfo, VU updates)
├── Controllers/             # REST API
├── wwwroot/                 # Web UI (shared by both products)
├── IntercomAppHost.cs       # Shared web + tray startup
├── Program.cs               # Entry: NDI Intercom16
├── Program.Light.cs         # Entry: NDI Intercom2
├── TrayApplicationContext.cs
├── NDI Intercom16.csproj
├── NDI Intercom2.csproj
├── NDI-Intercom.sln
├── Directory.Build.props    # Per-product output dirs + safe DefaultItemExcludes
├── CHANGELOG.md
├── CONTRIBUTING.md
├── installer-script-intercom16.iss
├── installer-script-intercom2.iss
├── build-intercom16-installer.cmd
└── build-intercom2-installer.cmd
```

## License

MIT License — Copyright (c) 2026 Vizrt NDI AB. See [LICENSE](LICENSE).

Third-party components are listed in [THIRD-PARTY-LICENSES.txt](THIRD-PARTY-LICENSES.txt). When redistributing the NDI runtime DLL, also include [Processing.NDI.Lib.Licenses.txt](Processing.NDI.Lib.Licenses.txt) (NDI SDK third-party notices). The NDI logo and NDI® trademark are **not** covered by MIT; see those files for redistribution and trademark notes.

Contributing: [CONTRIBUTING.md](CONTRIBUTING.md). Code of conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Security: [SECURITY.md](SECURITY.md).
