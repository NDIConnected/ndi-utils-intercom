# Stream Deck Plugin — Quick Install

## Install in 3 steps

### Step 1: Make sure NDI Intercom is running
```bash
cd C:\TEMP\NDI_INTERCOM
dotnet run
```
Confirm it responds at: http://localhost:5000

### Step 2: Install the Stream Deck plugin

**Option A — Automatic (recommended):**
1. Go to `C:\TEMP\NDI_INTERCOM\StreamDeck-Plugin\`
2. Rename the folder from `com.ndi.intercom.sdPlugin` to `com.ndi.intercom.streamDeckPlugin`
3. Double-click the renamed folder
4. Stream Deck installs it automatically

**Option B — Manual:**
1. Copy the `com.ndi.intercom.sdPlugin` folder
2. Paste it into: `%appdata%\Elgato\StreamDeck\Plugins\`
3. Restart the Stream Deck app

### Step 3: Configure the keys
1. Open the Stream Deck app
2. Search for "NDI Intercom" in the actions list
3. Drag actions (TALK, LISTEN, etc.) onto keys
4. Set the channel for each key (1–8)

## Plugin layout

```
com.ndi.intercom.sdPlugin/
├── manifest.json          ✅ Plugin configuration
├── plugin.js              ✅ Main logic
├── pi-talk.html           ✅ TALK property inspector
├── pi-listen.html         ✅ LISTEN property inspector
├── pi-group.html          ✅ GROUP property inspector
├── images/
│   ├── key.png           ✅ Key icon
│   ├── plugin.png        ✅ Plugin icon
│   ├── category.png      ✅ Category icon
│   └── action.png        ✅ Action icon
└── README.md             ✅ Full documentation
```

## Available actions

### 1. TALK
- **Function:** Toggle microphone transmission
- **Configuration:** Select channel (1–8)
- **Example:** Drag onto a key → Configure → Channel 1

### 2. LISTEN
- **Function:** Toggle audio monitoring
- **Configuration:** Select channel (1–8)
- **Example:** Drag onto a key → Configure → Channel 2

### 3. Set Group
- **Function:** Assign channel to a specific group
- **Configuration:** Channel (1–8) + Group (0–4)
- **Example:** Channel 3 → Group 2

### 4. Rotate Group
- **Function:** Cycle group 0→1→2→3→4→0
- **Configuration:** Select channel (1–8)
- **Example:** Press to change group on the fly

### 5. Reset All
- **Function:** Turn off TALK/LISTEN on all channels
- **Configuration:** None
- **Example:** Emergency key

## Customization

You can customize **everything** from the Stream Deck app:
- Key title
- Custom icon (upload image)
- Background color
- Font and text size

**The plugin only handles logic** — you do not need fancy custom icons in the bundle.

## Testing the plugin

### Test 1: Verify API
```bash
# NDI Intercom must respond
curl http://localhost:5000/api/intercom/channels
```

### Test 2: Verify plugin loaded
1. Open the Stream Deck app
2. Look for "NDI Intercom" in the action list
3. If it does not appear, restart the Stream Deck app

### Test 3: Key test
1. Drag "TALK" onto a key
2. Set Channel 1
3. Press the key
4. On the web UI (http://localhost:5000), confirm TALK turns on

## Troubleshooting

### Plugin does not appear
```bash
# Check it is in the correct folder
dir "%appdata%\Elgato\StreamDeck\Plugins\com.ndi.intercom.sdPlugin"

# Restart Stream Deck
taskkill /F /IM StreamDeck.exe
# Reopen Stream Deck from the Start menu
```

### Keys show ❗ (alert)
- NDI Intercom is not running
- Check http://localhost:5000 in the browser
- Start NDI Intercom with `dotnet run`

### Status does not update
- The plugin refreshes every 2 seconds
- Check the network connection
- Check the Stream Deck console (F12 in developer mode)

## Development and debug

### Enable developer mode
1. Stream Deck app → Settings → Developer
2. Enable "Show Developer Tools"
3. Right-click the plugin → "Inspect"

### Log files
```
%appdata%\Elgato\StreamDeck\logs\
```

### Edit the plugin
1. Edit files under `com.ndi.intercom.sdPlugin\`
2. Restart the Stream Deck app
3. Or: Developer Tools → Console → `location.reload()`

## Full documentation

- **README.md** — User guide inside the plugin
- **[DOCS/STREAMDECK_PLUGIN_GUIDE.md](../DOCS/STREAMDECK_PLUGIN_GUIDE.md)** — Developer guide in the repository

## Quick start example

```bash
# 1. Start NDI Intercom
cd C:\TEMP\NDI_INTERCOM
dotnet run

# 2. In another terminal, verify API
curl http://localhost:5000/api/intercom/channels

# 3. Install plugin (copy folder or double-click)
# 4. Configure Stream Deck from the app

# Typical 15-key Stream Deck layout:
# Row 1: TALK 1–5
# Row 2: LISTEN 1–5
# Row 3: TALK 6–8, Rotate Group, Reset All
```

## Done

The plugin is ready. You can:
1. Control TALK/LISTEN from Stream Deck
2. Change groups on the fly
3. Customize each key in the app
4. Create different profiles for different shows

Enjoy.
