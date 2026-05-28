# NDI Intercom - Stream Deck Plugin

Control your NDI Intercom system directly from your Elgato Stream Deck!

## Features

- **TALK Button** - Toggle transmission for any channel (1-8)
- **LISTEN Button** - Monitor audio from any channel (1-8)
- **Set Group** - Assign a channel to an intercom group (0-4)
- **Rotate Group** - Cycle through groups for quick changes
- **Reset All** - Turn off all TALK/LISTEN states

## Requirements

- Elgato Stream Deck (any model)
- Stream Deck software 6.0 or later
- NDI Intercom application running on http://localhost:5000
- Windows 10/11

## Installation

### Method 1: Double-Click Install
1. Download the `.streamDeckPlugin` file
2. Double-click the file
3. Stream Deck software will install it automatically
4. The plugin actions will appear in the Stream Deck actions list

### Method 2: Manual Install (Development)
1. Copy the `com.ndi.intercom.sdPlugin` folder to:
   ```
   %appdata%\Elgato\StreamDeck\Plugins\
   ```
2. Restart Stream Deck software
3. The plugin will appear in the actions list

## Usage

### Step 1: Start NDI Intercom
Make sure NDI Intercom is running on http://localhost:5000

### Step 2: Add Actions to Stream Deck
1. Open Stream Deck software
2. Find "NDI Intercom" in the actions list
3. Drag desired action (TALK, LISTEN, etc.) to a button
4. Configure the action in the Property Inspector

### Step 3: Configure Each Button
For each button you add:
- **Channel**: Select which channel (1-8) to control
- **Server URL** (optional): Leave empty for localhost:5000

### Action Types

#### TALK Button
- **Function**: Toggle microphone transmission for selected channel
- **Display**: Shows "TALK {channel}" and current state (ON/OFF)
- **Use Case**: Press to start/stop talking on a channel

#### LISTEN Button
- **Function**: Toggle audio monitoring for selected channel
- **Display**: Shows "LISTEN {channel}" and current state (ON/OFF)
- **Use Case**: Press to start/stop listening to a channel

#### Set Group Button
- **Function**: Assign channel to specific intercom group
- **Configuration**: Select channel (1-8) and group (0-4)
- **Display**: Shows "SET GRP {group} CH {channel}"
- **Use Case**: Set up intercom routing before broadcast

#### Rotate Group Button
- **Function**: Cycle through groups 0→1→2→3→4→0
- **Display**: Shows current group for selected channel
- **Use Case**: Quick group changes during operation

#### Reset All Button
- **Function**: Turn off all TALK and LISTEN for all channels
- **Display**: Shows "RESET ALL"
- **Use Case**: Emergency silence or end of show

## Example Configurations

### Typical Setup (15-key Stream Deck)
```
[T1] [T2] [T3] [T4] [T5]
[L1] [L2] [L3] [L4] [L5]
[T6] [T7] [T8] [GRP][RST]
```
- T = TALK, L = LISTEN, GRP = Rotate Group, RST = Reset

### Director Setup
```
[T1] [L1] [L2] [L3] [L4]
[L5] [L6] [L7] [L8] [RST]
[G1] [G2] [G3] [G4] [---]
```
- One TALK for director, LISTEN for all talent, group presets

### Broadcast Crew Setup
```
[T1] [L1] [R1] [---] [---]
[T2] [L2] [R2] [---] [---]
[T3] [L3] [R3] [---] [---]
```
- T = TALK, L = LISTEN, R = Rotate Group (one set per crew member)

## Troubleshooting

### Plugin doesn't appear
- Restart Stream Deck software
- Check plugin is in correct folder
- Verify `manifest.json` is valid JSON

### Buttons show alert (❗)
- NDI Intercom app is not running
- Check NDI Intercom is on http://localhost:5000
- Test API with: `curl http://localhost:5000/api/intercom/channels`

### Buttons don't update state
- Check internet/network connection
- Verify NDI Intercom API is responding
- Look at Stream Deck Remote Debugger (F12)

### Wrong channel responds
- Check Property Inspector configuration
- Verify correct channel is selected (1-8)
- Save settings and re-test

## Customization

### Change Button Appearance
1. Right-click button in Stream Deck software
2. Select "Edit" or properties
3. Customize:
   - Title (text shown)
   - Icon (upload custom image)
   - Background color
   - Font size/style

### Multi-Actions
Combine multiple NDI Intercom actions:
1. Create Multi-Action
2. Add sequence: Reset All → Set Group → TALK on
3. Trigger complex operations with one button

### Profiles
Create different profiles for different shows:
- Profile 1: 8-person talk show
- Profile 2: 4-person podcast
- Profile 3: Director monitor mode

## Advanced Configuration

### Remote Server
If NDI Intercom runs on another computer:
1. In Property Inspector, set Server URL to:
   ```
   http://192.168.1.100:5000/api/intercom
   ```
2. Replace `192.168.1.100` with actual IP
3. Ensure firewall allows port 5000

### Multiple Instances
Run multiple NDI Intercom instances:
1. Configure each on different port
2. Set Server URL for each button group:
   - Buttons 1-5: http://localhost:5000/api/intercom
   - Buttons 6-10: http://localhost:5001/api/intercom

## API Reference

The plugin calls these NDI Intercom APIs:

- `GET /api/intercom/channels/{N}` - Get channel state
- `POST /api/intercom/channels/{N}/talk/toggle` - Toggle TALK
- `POST /api/intercom/channels/{N}/listen/toggle` - Toggle LISTEN
- `POST /api/intercom/channels/{N}/group` - Set group
- `POST /api/intercom/channels/{N}/group/rotate` - Rotate group
- `POST /api/intercom/reset` - Reset all

See [STREAMDECK_PLUGIN_GUIDE.md](../../DOCS/STREAMDECK_PLUGIN_GUIDE.md) for full API documentation.

## Development

### Debug Mode
1. Open Stream Deck software
2. Enable Developer Mode in settings
3. Right-click plugin → "Inspect"
4. Use Chrome DevTools to debug

### Log Output
Check logs in:
```
%appdata%\Elgato\StreamDeck\logs\
```

### Testing Changes
After modifying plugin files:
1. Restart Stream Deck software
2. Or use "Reload" in developer tools

## Support

For issues or questions:
- Check NDI Intercom documentation
- Test APIs with curl/Postman first
- Enable Stream Deck developer mode for debugging

## Version

**v1.0.0** - Initial release
- TALK/LISTEN toggle actions
- Group assignment and rotation
- Reset all function
- Automatic state synchronization

## License

Distributed with NDI Intercom under the MIT License (Copyright (c) 2026 Vizrt NDI AB).
See LICENSE in the repository root.

Compatible with NDI Intercom v1.0.12+
