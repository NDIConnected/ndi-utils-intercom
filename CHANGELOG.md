# Changelog

All notable changes to NDI Intercom16 and NDI Intercom2 are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
