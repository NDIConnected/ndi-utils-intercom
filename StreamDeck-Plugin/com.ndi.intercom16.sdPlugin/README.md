# NDI Intercom16 — Stream Deck Plugin

Optional Elgato Stream Deck plugin for **16-channel** NDI Intercom16 control (TALK, LISTEN, group rotation, reset all).

## Requirements

- Elgato Stream Deck software 6.0+
- NDI Intercom16 running (default web UI: http://localhost:5016)

## Installation

**From the Intercom16 installer:** check *Install Stream Deck Plugin* during setup.

**Manual:** copy `com.ndi.intercom16.sdPlugin` to:

```
%APPDATA%\Elgato\StreamDeck\Plugins\
```

Or run `install-streamdeck-plugin.ps1` from the application install folder (included when the optional task is selected).

Restart Stream Deck after installing.

## Documentation

Full setup and remote-control notes: [DOCS/STREAMDECK_PLUGIN_GUIDE.md](../../DOCS/STREAMDECK_PLUGIN_GUIDE.md).

Distributed with NDI Intercom under the MIT License (Copyright (c) 2026 Vizrt NDI AB). See [LICENSE](../../LICENSE) in the repository root.
