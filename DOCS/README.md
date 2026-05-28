# Documentation

Guides and references for **NDI Intercom16** and **NDI Intercom2** live in this folder.

| Start here | Description |
|------------|-------------|
| [DOCUMENTATION-INDEX.md](DOCUMENTATION-INDEX.md) | Full catalog (User manual, API, NDI Bridge, deployment, testing) |
| [INTERCOM USER_MANUAL.md](INTERCOM%20USER_MANUAL.md) | End-user manual |
| [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md) | **v1.7+** — NDI identity & aggregation spec for management apps |
| [RELEASE-NOTES-v1.7.3.md](RELEASE-NOTES-v1.7.3.md) | **v1.7.3** — Settings page layout fix (CSS multicol; no more empty band between cards of different heights) |
| [RELEASE-NOTES-v1.7.2.md](RELEASE-NOTES-v1.7.2.md) | v1.7.2 — fix for incoming NDI stereo audio (FLTP planar decoding) |
| [RELEASE-NOTES-v1.7.1.md](RELEASE-NOTES-v1.7.1.md) | v1.7.1 — selectable NDI source name suffix mode (Full / Compact / Off) for legacy receiver compatibility |
| [RELEASE-NOTES-v1.7.0.md](RELEASE-NOTES-v1.7.0.md) | v1.7.0 — 24/7 stability hardening + identity routing |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Technical architecture and audio pipeline |
| [API_REST_GUIDE.md](API_REST_GUIDE.md) | HTTP / REST + SignalR API |

**Project overview** (build, installers, products): [README.md](../README.md) in the repository root.

### Linux development (experimental)

Cross-platform **`dotnet build`** on Linux uses **.NET 8** (`net8.0`), bundles **`libndi.so.6`** when the NDI SDK layout is found, and uses PipeWire/Pulse for audio (no ASIO). Full prerequisites, auto-detected SDK paths (`~/NDI SDK for Linux`, then `~/SDK/NDI_SDK_for_Linux`), environment overrides, and headless audio notes are in the root README section **“Linux build (experimental)”**: [README.md](../README.md). MSBuild logic: [Directory.Build.props](../Directory.Build.props). Helper script: [scripts/run-intercom-linux.sh](../scripts/run-intercom-linux.sh).

Also in this folder: `NDI Bridge Service Documentation.pdf`.
