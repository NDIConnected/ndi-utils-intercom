# Security Policy

## Sample software notice

NDI Intercom16 / NDI Intercom2 is **reference sample software** provided under the MIT license. It is **not officially supported** as a production product. Use at your own risk.

## Supported versions

Security fixes are applied on the `main` branch. There is no separate long-term support channel for older releases.

## Reporting a vulnerability

If you believe you have found a security issue, please **do not** open a public GitHub issue with exploit details.

Instead, report it through one of:

- [GitHub Security Advisories](https://github.com/NDIConnected/ndi-utils-intercom/security/advisories/new) for this repository (preferred)
- Contact the maintainers via the channels listed in [CONTRIBUTING.md](CONTRIBUTING.md)

Include steps to reproduce, affected version, and impact if known.

## Known security posture

This sample ships with **no authentication** on its embedded web server and SignalR hub. By default the server binds to **localhost only** (`WebServer:BindLocalhostOnly` in `appsettings.json`).

On Windows, operators can enable remote control from the system tray: **Web Server Settings…** → choose a network interface or all interfaces (restart required). This keeps **localhost always enabled** on the server PC and adds a separate bind on the selected interface. Settings map to `WebServer:RemoteAccess` (`Off`, `Interface`, `All`) and optional `WebServer:BindAddress`.

**Do not** enable LAN access on untrusted networks: anyone who can reach the port can control talk/listen, rewrite configuration, manage presets, and proxy commands to NDI Bridge.

Configuration export/import is confined to the per-product `Exports` folder under the application data directory; arbitrary path read/write is not permitted.

For deployment guidance see [README.md](README.md).
