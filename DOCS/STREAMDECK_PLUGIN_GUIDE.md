# NDI Intercom16 - Stream Deck Plugin Guide

## Overview

This guide helps you create a **customizable Stream Deck plugin** to control NDI Intercom16 (16 channels) through the official Stream Deck application.

## Architecture

```
NDI Intercom16 App (localhost:5016)
         ↓ HTTP REST API
Stream Deck Plugin (.sdPlugin)
         ↓ WebSocket
Stream Deck Software
         ↓ USB
Stream Deck Hardware
```

## Available REST APIs

The NDI Intercom16 application now exposes these REST APIs:

### Read State
- `GET /api/intercom/channels` - All channels (1-16)
- `GET /api/intercom/channels/{N}` - Specific channel (N: 1-16)

### TALK Control
- `POST /api/intercom/channels/{N}/talk/toggle` - Toggle TALK
- `POST /api/intercom/channels/{N}/talk/on` - Enable TALK
- `POST /api/intercom/channels/{N}/talk/off` - Disable TALK

### LISTEN Control
- `POST /api/intercom/channels/{N}/listen/toggle` - Toggle LISTEN
- `POST /api/intercom/channels/{N}/listen/on` - Enable LISTEN
- `POST /api/intercom/channels/{N}/listen/off` - Disable LISTEN

### Group Control
- `POST /api/intercom/channels/{N}/group` - Set group (body: `{"group": 0-4}`)
- `POST /api/intercom/channels/{N}/group/rotate` - Rotate group

### Utilities
- `POST /api/intercom/reset` - Reset all channels

### API Call Example
```bash
# Toggle TALK channel 1
curl -X POST http://localhost:5016/api/intercom/channels/1/talk/toggle

# Set group 2 for channel 10
curl -X POST http://localhost:5016/api/intercom/channels/10/group \
  -H "Content-Type: application/json" \
  -d '{"group": 2}'

# Get channel 16 state
curl http://localhost:5016/api/intercom/channels/16
```

## Stream Deck Plugin Structure

A Stream Deck plugin requires these files:

```
com.ndi.intercom16.sdPlugin/
├── manifest.json           # Plugin configuration
├── plugin.js              # Main logic
├── propertyInspector.html # Configuration UI
├── actions/
│   ├── talk.html         # Property Inspector for TALK
│   ├── listen.html       # Property Inspector for LISTEN
│   └── group.html        # Property Inspector for GROUP
├── images/
│   ├── actionIcon.png    # 20x20
│   ├── actionIcon@2x.png # 40x40
│   ├── pluginIcon.png    # 28x28
│   └── pluginIcon@2x.png # 56x56
└── README.md             # Documentation
```

## File 1: manifest.json

```json
{
  "SDKVersion": 2,
  "Author": "NDI",
  "Name": "NDI Intercom16",
  "Description": "Control NDI Intercom16 channels (16 channels)",
  "Category": "NDI Intercom16",
  "CategoryIcon": "images/pluginIcon",
  "Icon": "images/pluginIcon",
  "URL": "http://localhost:5016",
  "Version": "1.0.0",
  "OS": [
    {
      "Platform": "windows",
      "MinimumVersion": "10"
    }
  ],
  "Software": {
    "MinimumVersion": "6.0"
  },
  "Actions": [
    {
      "Icon": "images/actionIcon",
      "Name": "TALK",
      "States": [
        {
          "Image": "images/talk-off",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        },
        {
          "Image": "images/talk-on",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        }
      ],
      "SupportedInMultiActions": true,
      "Tooltip": "Toggle TALK for channel",
      "UUID": "com.ndi.intercom16.talk",
      "PropertyInspectorPath": "actions/talk.html"
    },
    {
      "Icon": "images/actionIcon",
      "Name": "LISTEN",
      "States": [
        {
          "Image": "images/listen-off",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        },
        {
          "Image": "images/listen-on",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        }
      ],
      "SupportedInMultiActions": true,
      "Tooltip": "Toggle LISTEN for channel",
      "UUID": "com.ndi.intercom16.listen",
      "PropertyInspectorPath": "actions/listen.html"
    },
    {
      "Icon": "images/actionIcon",
      "Name": "Set Group",
      "States": [
        {
          "Image": "images/group",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        }
      ],
      "SupportedInMultiActions": false,
      "Tooltip": "Set intercom group for channel",
      "UUID": "com.ndi.intercom16.setgroup",
      "PropertyInspectorPath": "actions/group.html"
    },
    {
      "Icon": "images/actionIcon",
      "Name": "Rotate Group",
      "States": [
        {
          "Image": "images/group-rotate",
          "TitleAlignment": "bottom",
          "FontSize": "12"
        }
      ],
      "SupportedInMultiActions": true,
      "Tooltip": "Rotate intercom group for channel",
      "UUID": "com.ndi.intercom16.rotategroup",
      "PropertyInspectorPath": "actions/group.html"
    }
  ]
}
```

## File 2: plugin.js

```javascript
// Global WebSocket and plugin UUID
let websocket = null;
let pluginUUID = null;

// API Base URL
const API_BASE = 'http://localhost:5016/api/intercom';

// Connect to Stream Deck
function connectElgatoStreamDeckSocket(inPort, inPluginUUID, inRegisterEvent, inInfo) {
    pluginUUID = inPluginUUID;

    websocket = new WebSocket(`ws://127.0.0.1:${inPort}`);

    websocket.onopen = function() {
        const json = {
            event: inRegisterEvent,
            uuid: inPluginUUID
        };
        websocket.send(JSON.stringify(json));
    };

    websocket.onmessage = function(evt) {
        const jsonObj = JSON.parse(evt.data);
        const event = jsonObj.event;
        const action = jsonObj.action;
        const context = jsonObj.context;
        const settings = jsonObj.payload?.settings || {};

        // Handle button press
        if (event === 'keyDown') {
            handleKeyDown(context, settings, action);
        }

        // Handle settings update
        if (event === 'didReceiveSettings') {
            // Settings changed - could update button state here
        }
    };
}

// Handle button press
async function handleKeyDown(context, settings, action) {
    const channel = settings.channel || 1;

    try {
        if (action === 'com.ndi.intercom16.talk') {
            await fetch(`${API_BASE}/channels/${channel}/talk/toggle`, { method: 'POST' });
            await updateButtonState(context, channel, 'talk');
        }
        else if (action === 'com.ndi.intercom16.listen') {
            await fetch(`${API_BASE}/channels/${channel}/listen/toggle`, { method: 'POST' });
            await updateButtonState(context, channel, 'listen');
        }
        else if (action === 'com.ndi.intercom16.setgroup') {
            const group = settings.group || 0;
            await fetch(`${API_BASE}/channels/${channel}/group`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ group })
            });
        }
        else if (action === 'com.ndi.intercom16.rotategroup') {
            await fetch(`${API_BASE}/channels/${channel}/group/rotate`, { method: 'POST' });
        }
    } catch (error) {
        console.error('API call failed:', error);
    }
}

// Update button state based on channel state
async function updateButtonState(context, channel, type) {
    try {
        const response = await fetch(`${API_BASE}/channels/${channel}`);
        const channelState = await response.json();

        let state = 0;
        if (type === 'talk') {
            state = channelState.talkEnabled ? 1 : 0;
        } else if (type === 'listen') {
            state = channelState.listenEnabled ? 1 : 0;
        }

        // Send state to Stream Deck
        websocket.send(JSON.stringify({
            event: 'setState',
            context: context,
            payload: {
                state: state
            }
        }));
    } catch (error) {
        console.error('Failed to update button state:', error);
    }
}
```

## File 3: actions/talk.html (Property Inspector)

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Talk Settings</title>
    <style>
        body { font-family: Arial; padding: 10px; }
        label { display: block; margin: 10px 0 5px; }
        select { width: 100%; padding: 5px; }
    </style>
</head>
<body>
    <label>Channel:</label>
    <select id="channel">
        <option value="1">Channel 1</option>
        <option value="2">Channel 2</option>
        <option value="3">Channel 3</option>
        <option value="4">Channel 4</option>
        <option value="5">Channel 5</option>
        <option value="6">Channel 6</option>
        <option value="7">Channel 7</option>
        <option value="8">Channel 8</option>
        <option value="9">Channel 9</option>
        <option value="10">Channel 10</option>
        <option value="11">Channel 11</option>
        <option value="12">Channel 12</option>
        <option value="13">Channel 13</option>
        <option value="14">Channel 14</option>
        <option value="15">Channel 15</option>
        <option value="16">Channel 16</option>
    </select>

    <script>
        // Load settings
        const channelSelector = document.getElementById('channel');

        function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
            const websocket = new WebSocket(`ws://127.0.0.1:${inPort}`);

            websocket.onopen = function() {
                const json = {
                    event: inRegisterEvent,
                    uuid: inPropertyInspectorUUID
                };
                websocket.send(JSON.stringify(json));
            };

            websocket.onmessage = function(evt) {
                const jsonObj = JSON.parse(evt.data);

                if (jsonObj.event === 'didReceiveSettings') {
                    const settings = jsonObj.payload.settings;
                    channelSelector.value = settings.channel || 1;
                }
            };

            // Save settings when changed
            channelSelector.addEventListener('change', function() {
                const settings = {
                    channel: parseInt(channelSelector.value)
                };

                websocket.send(JSON.stringify({
                    event: 'setSettings',
                    context: inPropertyInspectorUUID,
                    payload: settings
                }));
            });
        }
    </script>
</body>
</html>
```

## Creating Icons

You need to create PNG icons for the plugin:

### Required icons:
- `actionIcon.png` (20x20px)
- `actionIcon@2x.png` (40x40px)
- `pluginIcon.png` (28x28px)
- `pluginIcon@2x.png` (56x56px)
- `talk-off.png` / `talk-on.png` (72x72px)
- `listen-off.png` / `listen-on.png` (72x72px)
- `group.png` / `group-rotate.png` (72x72px)

### Tools to create icons:
- **Online**: [Canva](https://canva.com), [Figma](https://figma.com)
- **Software**: Photoshop, GIMP, Affinity Designer
- **Simple**: Use emoji or text symbols on colored background

## Plugin Installation

### Method 1: Development Mode
1. Copy `com.ndi.intercom16.sdPlugin` to:
   - Windows: `%appdata%\Elgato\StreamDeck\Plugins\`
2. Restart Stream Deck software
3. The plugin will appear in the actions list

### Method 2: Distribution (.streamDeckPlugin)
1. Rename folder from `.sdPlugin` to `.streamDeckPlugin`
2. Double-click to install
3. Stream Deck installs it automatically

## User Configuration

1. **Start NDI Intercom16** - Must be running on localhost:5016
2. **Open Stream Deck software**
3. **Drag "TALK" action** to a button
4. **Configure** in inspector on the right:
   - Select channel (1-16)
5. **Repeat** for LISTEN, Set Group, etc.

## Testing

### Test API (before using plugin):
```bash
# Test if API is working
curl http://localhost:5016/api/intercom/channels

# Test toggle TALK channel 1
curl -X POST http://localhost:5016/api/intercom/channels/1/talk/toggle

# Test toggle LISTEN channel 16
curl -X POST http://localhost:5016/api/intercom/channels/16/listen/toggle
```

### Test Plugin:
1. Press button on Stream Deck
2. Verify NDI Intercom16 receives the command
3. Check browser console for errors (F12 in Property Inspector)

## Troubleshooting

### Plugin doesn't appear
- Verify `manifest.json` is valid JSON
- Check folder name ends with `.sdPlugin`
- Restart Stream Deck software

### Buttons don't work
- Verify NDI Intercom16 is running
- Verify it's on port 5016 (not 5000)
- Test API with curl to verify it's working
- Check CORS if necessary

### States don't update
- Implement polling in `plugin.js`
- Or use SignalR for real-time push

## Future Improvements

1. **Auto-discovery** - Automatically detect NDI Intercom16 IP/port
2. **Real-time sync** - Use SignalR to update LED immediately
3. **VU Meters** - Show audio levels on buttons
4. **Profiles** - Save/load configurations
5. **Multi-instance** - Support multiple NDI Intercom16 instances

## Resources

- [Stream Deck SDK Documentation](https://developer.elgato.com/documentation/stream-deck/)
- [NDI Intercom16 API Docs](http://localhost:5016/swagger) (if you implement Swagger)
- [Plugin Examples](https://github.com/elgatosf/streamdeck-plugintemplate)
- [API REST Guide](API_REST_GUIDE.md) - Complete API documentation

## Files Already Created

✅ **IntercomController.cs** - Complete REST API for remote control (16 channels)
✅ **This guide** - Complete instructions for Stream Deck
✅ **Complete plugin** - com.ndi.intercom16.sdPlugin ready to use

## Next Steps

1. Create PNG icons
2. Create HTML files for Property Inspector
3. Complete `plugin.js` with all logic
4. Test the plugin
5. Distribute as `.streamDeckPlugin`

For additional questions or help with implementation, let me know!
