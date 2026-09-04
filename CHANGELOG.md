# Changelog

All notable changes to NDI Intercom16 and NDI Intercom2 are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.8.0] — 2026-09

### Fixed
- ASIO channels no longer stay dead when the driver is not ready at startup: a watchdog re-initializes and restarts the device every 5s until it plays, instead of giving up after the one-shot init.
- ASIO start failures are logged with the driver error and the `AsioOut` instance is discarded, so a failed attempt can be retried instead of leaving a half-wired engine.
- Changing the ASIO device from Settings → Apply now rebuilds the audio engine together with the driver; previously the engine kept the old output channel count and audio was routed to the wrong outputs until a restart.
- Microphone ring buffers are keyed by intercom channel instead of ASIO output channel, so two channels sharing one output no longer mix into the same buffer.
- Ring buffer dictionaries are concurrent, removing a startup race between the ASIO callback thread and configuration apply.
- ASIO input buffers are re-allocated when the driver changes its buffer size.

### Added
- Tray menu: **Open Logs Folder** and **Export Diagnostics…**. The export produces a zip with the log tail (max 4 MB per file), `config.json`, `appsettings.json`, and a summary of system, engine, ASIO, and per-channel routing state.
- ASIO routing indexes are validated against the driver's real channel counts and out-of-range channels are logged as warnings.

### Changed
- Settings: the ASIO input/output dropdowns list only the channels the selected device actually exposes; a configured channel beyond that range is shown as "not available on this device" instead of being silently muted.

## [1.7.9] — 2026-08

### Added
- Persist **Talk** and **Listen** per channel in `config.json` and restore them on restart (UI, REST, and Stream Deck toggles).
- NDI receive warning in the web UI: channel card background `#673131` when a configured NDI source is not connected (normal `#333333`).

## [1.7.8] — 2026-08

### Fixed
- NDI receive after restart: load identity/config before creating receivers, and reconnect unresolved sources via a watchdog (uses `NDIlib_recv_get_no_connections` when available) so audio no longer stays silent until Settings → Apply.
- ASIO init retries: multiple attempts on cold start only; a single attempt on interactive Apply so the UI does not freeze for ~14s.

### Added
- `GET /healthz` reports `configuredReceivers` / `connectedReceivers` and returns 503 when configured sources are not connected.
- `GetReceiverStatuses` / `NDIReceiverStatus` for per-channel configured vs connected receive state.

## [1.7.7] — 2026-08

### Changed
- **Input Level** knob: center = unity (100%), left = mute (0%), right = boost up to 300% (3×). Hard clip after gain to avoid overflow.

## [1.7.6] — 2026-07

### Added
- `Processing.NDI.Lib.Licenses.txt` (NDI SDK third-party notices: RapidJSON, SpeexDSP, RapidXML, etc.) shipped with both Windows installers alongside the NDI runtime DLL.

### Changed
- `THIRD-PARTY-LICENSES.txt` and `INSTALLER-NOTICE.txt` reference the NDI SDK license URL (`http://ndi.link/ndisdk_license`) and `Processing.NDI.Lib.Licenses.txt`.

## [1.7.5] — 2026-07

### Fixed
- Web UI input/output volume knobs now work on touch screens (Pointer Events + `touch-action: none`).

## [1.7.4] — 2026-06

### Added
- Tray **Web Server Settings**: port plus optional remote control on a **selected network interface** (localhost always available on the server PC).
- `SECURITY.md`, `CODE_OF_CONDUCT.md`, GitHub issue/PR templates, and installer NDI SDK notice.

### Security
- Web UI defaults to **localhost only**; config export/import confined to app data; CORS and `AllowedHosts` restricted by default.
- NDI P/Invoke bool marshaling and UTF-8 string decoding fixes; XSS hardening in web UI; Linux audio subprocess argument validation.

### Fixed
- Tray **Web Server Settings** saves to `%ProgramData%\…\appsettings.json` (writable without admin); install-dir defaults remain read-only.
- Installer completion page: shorter welcome/finish text so all content and the launch checkbox are visible.

### Changed
- Installers use Inno `x64` architecture identifier and ship `INSTALLER-NOTICE.txt`.
- Settings **Application Identity**: only **Application ID** in the UI; NDI source names always use **Compact** format (`Channel 1 (Intercom_A)`).

## [1.7.3] — 2026-05

### Fixed
- Settings page layout: replaced CSS Grid with multi-column layout so cards of different heights no longer leave empty gaps between columns.

## [1.7.2] — 2026-05

### Fixed
- Incoming NDI stereo audio: receive path now decodes planar (FLTP) layout correctly. Previously, stereo flows played one octave higher with a metallic edge; mono was unaffected.

## [1.7.1] — 2026-05

### Added
- **NDI source name suffix mode** (`Full` / `Compact` / `Off`) in Settings → Application Identity. Default is `Compact` for legacy receiver compatibility.
- Hot-swap of suffix mode without restart.

### Changed
- Default published NDI name shape changed from full identity suffix to compact form (`Channel 1 (Intercom_A)`). Set mode to `Full` to restore v1.7.0 naming.

### Notes
- `<ndi_manager>` connection metadata is unchanged in every mode. See [DOCS/IDENTITY-AWARE-ROUTING.md](DOCS/IDENTITY-AWARE-ROUTING.md).

## [1.7.0] — 2026-05

### Added
- **Identity-aware NDI routing**: `application_id` and `device_id` on every sender/receiver, with `<ndi_manager>` metadata and Web Control deep links.
- **24/7 stability**: per-channel NDI lifetime locks, graceful shutdown, ASIO subscription cleanup, ArrayPool leak fixes, audio device watchdog (WASAPI / PipeWire), atomic config save.
- **Persistent rolling log files** under `%ProgramData%` (Windows) or `~/.local/share` (Linux).
- **`GET /healthz`** endpoint for external monitoring.

### Changed
- NDI groups no longer used for routing; use identity fields instead. See [DOCS/IDENTITY-AWARE-ROUTING.md](DOCS/IDENTITY-AWARE-ROUTING.md).

### Fixed
- GC pressure in audio path (`MixingEngine`, `AudioRingBuffer`, `IntercomEngine`).
- Unbounded growth in NDI discovery listener caches.
- VU meter SignalR pump hardening.

---

Earlier history (v1.0–v1.5) predates this public repository. See [DOCS/INTERCOM USER_MANUAL.md](DOCS/INTERCOM%20USER_MANUAL.md) appendix for a high-level summary.
