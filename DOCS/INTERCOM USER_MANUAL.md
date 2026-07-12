# User Manual — NDI Intercom16 & NDI Intercom2

> **Scope.** This manual describes **NDI Intercom16** (16 channels) in detail. **NDI Intercom2** (2 channels) uses the same web UI, tray workflow, and configuration model; differences are called out where they matter (default port **5017**, data folder `%ProgramData%\NDI Intercom2\`, no Stream Deck plugin in the installer).

> **Sample software.** NDI Intercom is MIT-licensed reference software, not an officially supported production product. The web UI has **no authentication** and defaults to **localhost only**. See [SECURITY.md](../SECURITY.md) before enabling remote access.

## Table of Contents
1. [Introduction](#introduction)
2. [System Requirements](#system-requirements)
3. [Installation](#installation)
4. [Quick Start Guide](#quick-start-guide) (includes System Tray)
5. [Web Interface](#web-interface)
6. [Channel Configuration](#channel-configuration)
7. [Intercom Groups](#intercom-groups) (includes Unified NDI+ASIO Groups)
8. [Stream Deck Integration](#stream-deck-integration)
9. [Audio and NDI Settings](#audio-and-ndi-settings)
10. [Application Identity](#application-identity)
11. [Bridge Service](#bridge-service)
12. [Daily Usage](#daily-usage)
13. [Troubleshooting](#troubleshooting)
14. [FAQ](#faq)

---

## Introduction

**NDI Intercom16** is a professional intercom communication system that uses NDI® (Network Device Interface) technology to transmit high-quality audio over local networks.

### Main Features

- **16 Full Duplex Channels**: Simultaneous bidirectional communication
- **Unified Intercom Groups**: NDI and ASIO channels can participate in the same group with cross-mode N-1 mixing
- **N-1 System**: Each channel sends its own mix excluding the recipient's own return audio
- **ASIO Support**: Professional audio routing with Dante Virtual Soundcard and ASIO devices
- **NDI Bridge Integration**: Control NDI Bridge Host/Join/Local modes directly from Settings
- **System Tray Application**: Runs silently in the notification area with quick-access menu
- **Web Interface**: Complete control via web browser with real-time sync
- **Stream Deck Integration**: Hardware control with Elgato Stream Deck
- **Self-Contained**: .NET 8 runtime included in the installer (NDI Tools / NDI runtime must be installed separately)
- **Persistent Configuration**: Settings are automatically saved
- **Advanced Audio Processing**: Microphone noise gate with independent operation from NDI receive streams

### Typical Use Cases

Ideal for:
- **Broadcast productions**: Communication between control room, studio, cameras
- **Live events**: Coordination between audio, video, lighting technicians
- **Fixed installations**: Theaters, TV studios, churches, auditoriums
- **Remote production**: Distributed production across multiple locations

---

## System Requirements

### Minimum Requirements

#### Windows desktop build

- **Operating System**: Windows 10 (64-bit) or Windows 11
- **Processor**: Intel Core i5 or equivalent AMD (i7 recommended)
- **RAM**: 8 GB minimum (16 GB recommended)
- **Network**: Gigabit Ethernet (1000 Mbps) for NDI streaming
- **Audio Card**: WASAPI compatible audio devices (microphone + headphones/speakers)

#### Linux headless build

- **Operating System**: Linux x64 with .NET 8 runtime
- **Audio backend**: PipeWire/PulseAudio with `parec`, `pacat`, and `pactl`
- **NDI runtime**: NDI SDK for Linux with `libndi.so.6` available to the application
- **Limitations**: no Windows tray app, no ASIO mode, and no Windows installer; control is via the web UI/API


### Optional Hardware

- **Elgato Stream Deck** (any model) for hardware control
- **Professional audio interface** for better audio quality
- **ASIO-compatible audio interface** for professional routing on Windows

### Network Requirements

- **Stable local network** with dedicated Gigabit switch
- **Bandwidth**: ~10 Mbps per NDI audio channel
- **Multicast enabled** (for automatic NDI discovery)

---

## Installation

1. Download `NDI-Intercom16-Setup-v1.7.5.exe` from the [releases page](https://github.com/NDIConnected/ndi-utils-intercom/releases)
2. Run the installer with administrator privileges
3. Follow the guided procedure:
   - Accept the license
   - Choose installation folder (default: `C:\Program Files\NDI\NDI Intercom16`)
   - Optionally install the Stream Deck plugin
   - Installer will automatically copy all necessary dependencies (including .NET runtime and NDI libraries)
4. At completion, click **Launch NDI Intercom16** or start it from the Start menu

> **Note:** The installer is self-contained for .NET. You must install **NDI Tools** (or the NDI runtime) separately on the target PC so NDI send/receive works.

### Installation Verification

After startup, the application icon appears in the **Windows system tray** (notification area near the clock). Right-click the icon to verify the application is running and access the web interface.

---

## Quick Start Guide

### 1. Start the Application

Launch NDI Intercom16 from:
- **Desktop shortcut**: Double-click "NDI Intercom16" on your desktop
- **Start Menu**: Start Menu → NDI Intercom16

The application starts silently and places an icon in the **Windows system tray** (notification area near the clock). No main window is displayed.

### 2. System Tray Icon

The system tray icon is your main access point to the application. **Right-click** the icon to open the context menu:

```
┌─────────────────────────────┐
│  Open Web Interface (5016)  │  ← Opens http://127.0.0.1:<port> in your browser
│  Web Server Settings…       │  ← Port + optional remote access on a network interface
│  ─────────────────────────  │
│  Exit                       │  ← Closes the application
└─────────────────────────────┘
```

- **Open Web Interface**: Opens `http://127.0.0.1:<port>` in your default browser (always works on the PC running Intercom)
- **Web Server Settings…**: Change the HTTP port and optionally allow remote control from other PCs on the LAN (see [Remote Control](#remote-control)). **Restart required** after saving.
- **Exit**: Completely shuts down the application

### 3. Access the Web Interface

Right-click the tray icon and select **Open Web Interface**, or open a browser manually on the **same PC**:

```
http://127.0.0.1:5016
```

(Use port **5017** for NDI Intercom2.)

**From another computer**, remote access is **disabled by default**. Enable it first via tray → **Web Server Settings…** → choose a network interface (recommended on multi-NIC machines) or all interfaces (advanced), then open:

```
http://<server-ip-address>:5016
```

Allow inbound TCP on that port in Windows Firewall if prompted.

### 4. Initial Configuration

#### Step 1: Configure Audio Devices

1. Click the **"Settings ⚙"** button in the top right
2. In the **"Audio Devices"** section:
   - **Input Device (Microphone)**: Select the microphone to use
   - **Output Device (Speakers)**: Select headphones or speakers
3. Click **Apply** at the bottom of the Settings page
4. Verify the **Input Level** bar in the header moves when you speak

#### Step 2: Configure Your First NDI Channel

For each channel you want to activate:

1. In **Settings → Channels Configuration**, open the card for that channel
2. Configure:
   - **Mode**: NDI (default) or ASIO (Windows only)
   - **NDI Send**: Name published on the network (e.g., `CONTROL-ROOM`, `CAMERA-1`)
   - **NDI Receive**: Select a remote NDI source from the dropdown, or **None**
   - For **ASIO** mode: pick **ASIO Input** and **ASIO Output** channel indices
3. Click **Apply** to save

Assign **intercom groups** on the main dashboard (see [Intercom Groups](#intercom-groups)).

#### Step 3: Test Communication (NDI mode)

1. Return to the main page (**Cancel** on Settings, or open `/index.html`)
2. Click **TALK** on Channel 1 (button turns **red** when active)
3. Click **LISTEN** on Channel 1 (button turns **blue** when active)
4. Speak into your microphone
5. You should hear yourself in the headphones (if configured for loopback testing)

---

## Web Interface

### Layout

The main dashboard has a **header** and a **grid of channel cards** (16 for Intercom16, 2 for Intercom2).

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ [NDI] Intercom16    Input Level [====VU====]    💾 Save  📂 Load  Settings ⚙ │ ← Header
├───────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ... (up to 16 cards)      │
│  │ Channel 1   │  │ Channel 2   │  │ Channel 3   │                            │
│  │  [ TALK ]   │  │  [ TALK ]   │  │  [ TALK ]   │                            │
│  │ [ LISTEN ]  │  │ [ LISTEN ]  │  │ [ LISTEN ]  │                            │
│  │ In / Out    │  │ In / Out    │  │ In / Out    │  ← level knobs + NDI VU    │
│  │ NONE 1 2 3 4│  │ NONE 1 2 3 4│  │ NONE 1 2 3 4│  ← intercom group          │
│  └─────────────┘  └─────────────┘  └─────────────┘                            │
└───────────────────────────────────────────────────────────────────────────────┘
```

There is **no** global “Reset all channels” button on this page. Use per-channel **TALK** / **LISTEN**, or a **Stream Deck “Reset All”** action if you use the plugin.

### Interface Elements

#### 1. Header Bar

- **NDI logo + product title** (e.g. Intercom16)
- **Input Level**: horizontal VU bar for the selected microphone (gradient green → yellow → red by level)
- **Save / Load**: save or restore named **configuration presets** (full settings snapshot)
- **Settings ⚙**: opens the Settings page

#### 2. Channel Card

Each channel card contains:

```
┌─────────────────────┐
│ Channel 1           │ ← click header to rename (dashboard label)
│ [      TALK      ]  │
│ [     LISTEN     ]  │
│ Input Level  [VU|◉] │ ← vertical NDI VU + input level knob
│ Output Level   [◉]  │ ← output level knob
│ Intercom Group      │
│ [NONE][1][2][3][4]  │
└─────────────────────┘
```

**TALK / LISTEN**

- **TALK**: dim when off; **bright red** when active (transmitting on that channel)
- **LISTEN**: dim when off; **bright blue** when active (hearing that channel’s mix)

**Levels**

- **Input / Output knobs**: drag vertically to set channel gain (0–100)
- **Vertical NDI VU** (beside input knob): shows incoming NDI level on that channel; drops to zero if the stream stops updating (no separate “NDI ACTIVE / INACTIVE” label)

**Intercom group**

- **NONE** plus **1–4**: only one active per channel; same as group 0–4 elsewhere in this manual

**Channel label**

- Click the channel title on the dashboard to edit the **display label** (browser prompt). This is separate from **NDI Send** in Settings, which controls the name on the NDI network.

### Real-Time Synchronization

The interface uses **SignalR** for real-time updates:
- Multiple browsers can connect simultaneously
- Changes made in one browser appear instantly in all others
- No page refresh needed
- Useful for multi-user control rooms

---

## Channel Configuration

### Channel Anatomy

Each channel can operate in two audio modes:

1. **NDI Mode** (default): Sends and receives audio via NDI over the network
2. **ASIO Mode**: Routes audio through a professional ASIO audio interface (e.g., Dante Virtual Soundcard, RME, Focusrite)

Both modes support N-1 intercom groups. Channels in different modes can be assigned to the same group for cross-mode communication.

### Configuration Parameters

For each channel in **Settings → Channels Configuration**:

#### NDI mode (default)

- **NDI Send**: Name published on the network (must be unique per sender on the LAN)
- **NDI Receive**: Dropdown of discovered sources, or **None** if this channel only transmits

NDI send/receive are configured by name and source selection — there are no separate “enable send/receive” toggles.

#### ASIO mode (Windows)

- **Mode**: ASIO
- **ASIO Input / ASIO Output**: channel indices on the selected ASIO device (configured under **Audio Devices → ASIO**)

#### Intercom group

- Assigned on the **main dashboard** with **NONE / 1 / 2 / 3 / 4** (not on the Settings page)
- **0 / NONE**: channel isolated
- **1–4**: group membership for N-1 mixing

### Example Configuration: Two Stations

**Station A (Control Room):**
```
Channel 1 (Settings):
- Mode: NDI
- NDI Send: CONTROL-ROOM
- NDI Receive: (source from Station B, e.g. CAMERA-1)
Dashboard: Intercom Group → 1
```

**Station B (Camera):**
```
Channel 1 (Settings):
- Mode: NDI
- NDI Send: CAMERA-1
- NDI Receive: CONTROL-ROOM
Dashboard: Intercom Group → 1
```

**Result:** Bidirectional communication between Control Room and Camera

---

## Intercom Groups

### What Are Intercom Groups?

Groups determine which channels can communicate with each other:
- Channels in the **same group** hear each other (N-1 mixing)
- Channels in **different groups** are isolated from each other
- **Group 0 (NONE)** means the channel is isolated

### Unified Groups (NDI + ASIO)

Starting from v1.5, intercom groups are **unified across NDI and ASIO modes**. This means:
- An NDI channel and an ASIO channel can be in the **same group**
- All channels in a group hear each other regardless of their audio mode
- N-1 mixing works transparently across modes (cross-mode mixing)

**Example:** Channel 1 (NDI), Channel 2 (ASIO), Channel 3 (ASIO) all in Group 1 -- all three users can communicate with each other seamlessly.

### Group Configuration

On the **main dashboard**, each channel card has **Intercom Group** buttons: **NONE**, **1**, **2**, **3**, **4**. The highlighted button is the active group.

**Available groups:**
- **Group 0**: NONE (channel is isolated)
- **Group 1**: First intercom group
- **Group 2**: Second intercom group
- **Group 3**: Third intercom group
- **Group 4**: Fourth intercom group

### Example Group Scenarios

#### Scenario 1: Single Production Group

```
All channels in Group 1:
- Channel 1: CONTROL-ROOM (Group 1)
- Channel 2: CAMERA-1 (Group 1)
- Channel 3: CAMERA-2 (Group 1)
- Channel 4: AUDIO-TECH (Group 1)
```

**Result:** All channels can communicate with each other

#### Scenario 2: Separated Teams

```
Video Team (Group 1):
- Channel 1: CONTROL-ROOM (Group 1)
- Channel 2: CAMERA-1 (Group 1)
- Channel 3: CAMERA-2 (Group 1)

Audio Team (Group 2):
- Channel 4: AUDIO-MIXER (Group 2)
- Channel 5: FOH-TECH (Group 2)

Isolated (Group 0):
- Channel 6: SPARE (Group 0 - not in use)
```

**Result:**
- Video team communicates internally (CH 1-3)
- Audio team communicates internally (CH 4-5)
- No cross-team communication
- Channel 6 is isolated

#### Scenario 3: Multi-Group Control Room

```
Control Room with multiple channels:
- Channel 1: CR-TO-VIDEO (Group 1) → talks to video team
- Channel 2: CR-TO-AUDIO (Group 2) → talks to audio team
- Channel 3: CR-TO-LIGHTS (Group 3) → talks to lighting team
```

**Result:** Control room can participate in multiple groups simultaneously

### Rotating Groups (Stream Deck Feature)

Using Stream Deck "Rotate Group" button:
- Press once: NONE → Group 1
- Press again: Group 1 → Group 2
- Press again: Group 2 → Group 3
- Press again: Group 3 → Group 4
- Press again: Group 4 → NONE (cycle repeats)

**Displayed as:**
- `NONE` = Group 0
- `GRP 1` = Group 1
- `GRP 2` = Group 2
- `GRP 3` = Group 3
- `GRP 4` = Group 4

---

## Stream Deck Integration

### Overview

NDI Intercom16 includes a plugin for **Elgato Stream Deck** that provides hardware control of intercom channels.

**Features:**
- Physical buttons for TALK/LISTEN control on all 16 channels
- Real-time visual feedback with colored buttons
- Automatic synchronization with web interface
- Support for all Stream Deck models

### Installation

#### Prerequisites
- Elgato Stream Deck software installed (v6.0 or later)
- NDI Intercom16 application running

#### Install Plugin

**Option A: From Installer**
1. Run `NDI-Intercom16-Setup.exe`
2. During installation, check "Install Stream Deck Plugin"
3. Restart Stream Deck software

**Option B: Manual Installation**
1. Copy `StreamDeck-Plugin\com.ndi.intercom16.sdPlugin` to `%APPDATA%\Elgato\StreamDeck\Plugins\`, or run `install-streamdeck-plugin.ps1` from the application install folder
2. Quit Stream Deck software completely (right-click tray → Quit)
3. Restart Stream Deck software
4. Look for "NDI Intercom16" in the actions list

### Plugin Actions

The plugin provides 4 action types:

#### 1. TALK Button
- **Function**: Toggle TALK for a specific channel
- **Visual Feedback:**
  - Dark Red background = OFF
  - Bright Red background = ON
  - Text shows: `TALK\n[CH] [ON/OFF]`

#### 2. LISTEN Button
- **Function**: Toggle LISTEN for a specific channel
- **Visual Feedback:**
  - Dark Gray background = OFF
  - Dark Blue background = ON
  - Text shows: `LISTEN\n[CH] [ON/OFF]`

#### 3. Rotate Group
- **Function**: Rotate through groups 0→1→2→3→4→0
- **Visual Feedback:**
  - Text shows: `CH [NUM]\n[GROUP NAME]`
  - Group names: NONE, GRP 1, GRP 2, GRP 3, GRP 4

#### 4. Reset All (Stream Deck only)

- **Function**: Disable TALK and LISTEN on **all** channels via the REST API
- **Where**: Stream Deck plugin action only — **not** on the web dashboard
- **Visual Feedback:** button text `RESET ALL`; state refresh follows the next poll from the server

### Configuration

For each TALK/LISTEN/Rotate button:

1. Drag action from "NDI Intercom16" category to Stream Deck key
2. In Property Inspector, configure:
   - **Channel**: Select channel number (1-16)
   - **Server URL**: Configure based on your setup:
     - **Local control**: `http://localhost:5016/api/intercom` (default)
     - **Remote control**: `http://192.168.1.100:5016/api/intercom` (replace with server IP)
     - **IMPORTANT**: Always include the port number (`:5016`) when using remote servers
3. Button will automatically show current state

#### Remote Server Configuration

You can now control NDI Intercom16 from a Stream Deck on a different computer:

**Requirements:**
- Server and Stream Deck computer must be on the same network
- Firewall must allow incoming connections on port 5016
- You need to know the server's IP address

**Setup Steps:**
1. Find server IP address:
   ```powershell
   # On the server computer, run:
   ipconfig
   # Look for "IPv4 Address" (e.g., 192.168.1.100)
   ```

2. Configure Stream Deck buttons:
   - In Property Inspector, change Server URL to: `http://<server-ip>:5016/api/intercom`
   - Example: `http://192.168.1.100:5016/api/intercom`
   - **Do not forget** the port number (`:5016`)!

3. Test connection:
   - Button should update with current state within 1-2 seconds
   - If you see "Connection Error", check firewall and IP address

**Troubleshooting Remote Connections:**
- Verify server is reachable: `ping <server-ip>` from Stream Deck computer
- Test web interface: Open `http://<server-ip>:5016` in browser on Stream Deck computer
- Check Windows Firewall allows port 5016 (see Troubleshooting section)
- Ensure you're using the correct format with port: `http://IP:5016/api/intercom`

### Auto-Sync with Web Interface

**How it works:**
- Stream Deck polls server every **500ms** for state updates
- When you change TALK/LISTEN from web interface, Stream Deck updates within ~0.5 seconds
- When you press Stream Deck button, web interface updates instantly via SignalR
- **Bidirectional sync** ensures all interfaces stay synchronized

**Visual Indicators:**
- Colors change automatically based on current state
- Text updates to show ON/OFF status
- No manual refresh needed

### Troubleshooting Stream Deck

#### Problem: Plugin doesn't appear in Stream Deck

**Solutions:**
1. Verify plugin is installed:
   - Check: `%APPDATA%\Elgato\StreamDeck\Plugins\com.ndi.intercom16.sdPlugin\`
2. Restart Stream Deck software completely (Quit → Reopen)
3. Reinstall the plugin using `install-streamdeck-plugin.ps1` from the application folder

#### Problem: Buttons don't update automatically

**Solutions:**
1. Verify NDI Intercom16 application is running
2. Check Server URL is correct in button properties
3. Restart Stream Deck software
4. Re-add buttons to Stream Deck

#### Problem: "Connection Error" on buttons

**Solutions:**
1. Verify application is running at `http://localhost:5016`
2. Check firewall isn't blocking port 5016
3. Test web interface works in browser
4. Verify Server URL in button properties matches application port

---

## Audio and NDI Settings

### Audio Devices

#### Input Device (Microphone)

Select input device:
- **USB Microphone** (recommended for quality)
- **Laptop integrated microphone** (testing only)
- **Professional audio card** (XLR via interface)

**Tips:**
- Use microphones with noise cancellation
- Test level with VU Meter in header
- Optimal level is -12 dB / -6 dB (green/yellow zone)
- Avoid red zone (clipping/distortion)

#### Output Device

Select output device:
- **Closed headphones** (highly recommended to avoid acoustic echo/feedback risk)
- **In-ear headphones**
- **Open speakers / monitor speakers**: use only in controlled environments

**Important:** Headphones are strongly recommended for intercom use. Speakers do not automatically cause feedback, but they can create acoustic echo or a feedback loop when the local microphone picks up audio coming from the speakers and sends it back to the remote channel. The risk increases with high speaker level, open microphones, and close speaker-to-mic placement.

### NDI Configuration

#### NDI Send

**Parameters:**
- **NDI Send Name**: Unique name visible on network
  - Examples: "CONTROL-ROOM-1", "CAM-A", "AUDIO-TECH"
  - ✅ Use descriptive names
  - ✅ Use hyphens instead of spaces
  - ❌ Don't duplicate names on same network

**Tips:**
- Use location-based names (e.g., "STUDIO-A-CAM1")
- Include role in name (e.g., "DIRECTOR-BOOTH")
- Avoid generic names (e.g., "NDI-1")

#### NDI Receive

**How discovery works:**

1. The app polls for NDI sources on the network
2. Sources appear in **Settings → Channels Configuration → NDI Receive**
3. Select the desired source (or **None**)

**Troubleshooting:**
- If no sources appear: verify machines are on same subnet
- Check firewall (port 5353 UDP for mDNS)
- Verify switch supports multicast (IGMP snooping)
- Use wired Ethernet, not WiFi

### Microphone Noise Gate

The microphone noise gate prevents background noise from being transmitted while preserving clear voice communication. It operates independently on the microphone input before NDI transmission.

**Key Features (v1.2):**
- **Independent Operation**: Works on microphone input only, unaffected by incoming NDI audio streams
- **No False Triggering**: Receiving audio from other channels won't interfere with your microphone gate
- **Professional Performance**: Smooth attack/release curves for natural-sounding gating

**Parameters:**

- **Enable**: Toggle noise gate on/off
- **Threshold**: Minimum level to open gate
  - `-60 dB`: Very sensitive (passes almost everything)
  - `-40 dB`: Medium (blocks light noise)
  - `-30 dB`: Standard (recommended for most use cases)
  - `-20 dB`: Aggressive (passes only loud voices)

- **Attack**: How quickly gate opens when signal exceeds threshold
  - `1-50 ms`: Shorter = faster response
  - Recommended: `15 ms`

- **Release**: How quickly gate closes when signal drops below threshold
  - `50-500 ms`: Longer = smoother transitions
  - Recommended: `150 ms`

- **Hold**: How long gate stays open after signal drops below threshold
  - `50-500 ms`: Prevents rapid open/close (chattering)
  - Recommended: `100 ms`

**Recommended Configuration:**
```
Enabled: ✅
Threshold: -30 dB
Attack: 15 ms
Release: 150 ms
Hold: 100 ms
```

**When to use:**
- Noisy environments (fans, air conditioning, ambient noise)
- Preventing open microphones from transmitting room noise
- Improving signal-to-noise ratio

**v1.2 Improvement:**
Previously, the noise gate could be affected by audio from incoming NDI streams, causing unwanted gate behavior. This has been fixed - the microphone noise gate now operates completely independently of NDI receive audio.

### Channels Feedback Gate

The feedback gate reduces the chance that remote users hear their own voice returning through your microphone when their audio is playing through local speakers.

**How it works:**
- Monitors audio received from each NDI channel
- When a channel is transmitting audio to you, temporarily reduces your microphone output back to that same channel
- Reduces acoustic echo and feedback risk in open-speaker environments
- Does NOT replace N-1 mixing and does NOT affect audio from other channels in the same group

**Parameters:**

- **Enable**: Toggle feedback gate on/off
- **Threshold**: Level at which to trigger ducking
  - Recommended: `-35 dB`

- **Reduction**: How much to reduce microphone when triggered
  - Recommended: `-60 dB`

**Recommended Configuration:**
```
Enabled: ✅ (especially if using speakers instead of headphones)
Threshold: -35 dB
Reduction: -60 dB
```

**When to use:**
- Using speakers instead of headphones
- Open microphone environments
- Preventing remote users from hearing themselves

**Important:** This gate is separate from the Microphone Noise Gate and serves a different purpose. It is a mitigation for speaker-based setups, not a substitute for proper monitoring with headphones.

---

## Application Identity

> **New in v1.7**: every NDI Intercom instance now carries a stable identity tag that lets external management software (e.g. a "studio dashboard" application) discover, group and remote-control your Intercom instances on the network.

You'll find this in the **Web Interface → Settings → Application Identity** panel.

### What you see in the UI

- **Application ID**: a free-text label, default `Intercom_A`. You can edit it freely.
  - Allowed characters: `A-Z`, `a-z`, `0-9`, underscore (`_`), dot (`.`), hyphen (`-`).
  - Length: 1 to 64 characters.
  - The Web UI rejects invalid values with an alert before saving.

- **Device ID**: not displayed. It is auto-generated the first time the app starts and stored permanently in `config.json`. Treat it as an opaque identifier; you should not change it manually.

- **NDI source names** always use the **Compact** format: `Channel 1 (Intercom_A)`. There is no UI option to change this; full identity metadata remains in the `<ndi_manager>` XML on each connection.

### How to set it

1. Open the Web UI → **Settings**.
2. Edit the **Application ID** field.
3. Click **Apply Settings**.

When you apply, NDI Intercom recreates all senders and receivers under the new identity (this takes a fraction of a second; existing TALK/LISTEN states are preserved).

### Why this matters

| Scenario                                                     | What to set                                                                                       |
|--------------------------------------------------------------|----------------------------------------------------------------------------------------------------|
| One Intercom on the studio network                           | Leave the default `Intercom_A`.                                                                   |
| Multiple Intercom16 instances on the same machine (rare)     | Each gets its own auto-generated `Device ID`; pick distinct **Application IDs** if you want them grouped differently in the management dashboard. |
| Multiple Intercom16 instances on different machines, same role (e.g. all "Director's stations") | Set the same **Application ID** on all of them (e.g. `Director`). They will appear as one logical group in management software. |
| Mixed Intercom16 + Intercom2 fleet                           | Use the **Application ID** to label the role (`Director`, `Talent_Booth`, `OB_Van`, …) regardless of model. |

### What you'll see in NDI tools

In NDI Studio Monitor or any other NDI-compatible tool, your channels appear with the compact identity suffix:

```
Channel 1 (Intercom_A)
Intercom RX ch-3 (Intercom_A)
```

NDI Studio Monitor also displays a **Web Control** link on each Intercom sender — clicking it opens the web UI for that specific Intercom instance when remote access is enabled. This works thanks to the `<ndi_capabilities web_control="…">` metadata that NDI Intercom publishes automatically.

### For developers / integrators

If you are building a management or aggregation application that needs to consume this identity, the full specification (NDI source name grammar, `<ndi_manager>` XML schema, persistent receiver lifecycle, recommended consumption strategy) is in [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

---

## NDI Bridge Service

Optional integration with an external **NDI Bridge Service** (separate Windows service with its own REST API, default `http://localhost:8080`).

Enable this section only if you use NDI Bridge to extend NDI across WAN or between network segments.

| Setting                                   | Description                                                  |
| ----------------------------------------- | ------------------------------------------------------------ |
| **Enable NDI Bridge Service integration** | When off, the app does not contact the Bridge Service        |
| **Bridge Service URL**                    | REST endpoint of the Bridge Service                          |
| **Auto-start Mode**                       | `None`, `Host`, `Join`, or `Local` — applied when the Bridge Service restarts |
| **Host**                                  | Expose local NDI sources to a remote bridge (name, port, groups, buffer, encryption key) |
| **Join**                                  | Connect to a remote bridge host (name, IP, port, groups, buffer, encryption key) |
| **Local**                                 | Local bridging configuration (name, groups)                  |

Click **Test Connection** to verify the Bridge Service URL before saving. Settings are pushed to the Bridge Service on save and again at application startup.



## Daily Usage

### Scenario 1: Point-to-Point Communication

**Goal:** Control Room talks with a Camera

1. On CONTROL ROOM station:
   - Open browser at `http://localhost:5016`
   - Or press Stream Deck TALK button for CAMERA channel
   - Press **TALK** on Channel configured for CAMERA (turns red)
   - Press **LISTEN** on same Channel (turns blue)
   - Speak into microphone

2. On CAMERA station:
   - Press **LISTEN** on Channel configured for CONTROL-ROOM
   - Press **TALK** on same Channel to respond

**Result:** Bidirectional communication active

### Scenario 2: Group Intercom (Multi-Party)

**Goal:** Control Room + 3 Cameras in communication

**Setup:**
- All channels configured in same group (e.g., Group 1)
- Each station has TALK and LISTEN active to relevant channels

**Usage:**
1. Each station presses TALK + LISTEN on desired channels
2. N-1 system automatically handles mixes
3. Nobody hears themselves in return (echo prevention)

**Voice Protocol Example:**
- "Control room to all cameras, stand by for test..." → everyone listens
- "Control room to CAM-1 only..." → only CAM-1 should respond
- "CAM-2 to Control room, I'm ready..." → direct communication

### Scenario 3: Separate Groups

**Goal:** Video Group + Audio Group separated

**Configuration:**
```
VIDEO Group (Group 1):
- CH1: CONTROL-ROOM-VIDEO
- CH2: CAMERA-1
- CH3: CAMERA-2

AUDIO Group (Group 2):
- CH4: AUDIO-MIXER
- CH5: FOH-TECH
```

**Usage:**
- Channels 1-3 communicate with each other (video crew)
- Channels 4-5 communicate with each other (audio crew)
- No interference between groups
- Director can monitor both using multiple channels

### Using Stream Deck

**Quick Actions:**
1. **Start talking to camera:**
   - Press TALK button for that channel
   - Button turns red
   - Speak normally

2. **Listen to camera:**
   - Press LISTEN button
   - Button turns blue
   - Hear camera operator

3. **Silence all channels (Stream Deck):**
   - Use a **Reset All** Stream Deck action (not available on the web dashboard)
   - Clears TALK and LISTEN on every channel via the API

4. **Change group:**
   - Press ROTATE GROUP button repeatedly
   - Cycles through: NONE → GRP 1 → GRP 2 → GRP 3 → GRP 4 → NONE

### Remote Control

By default the web UI listens on **127.0.0.1 only** on the PC running Intercom. To control it from other devices on the network:

1. **Enable remote access** (on the Intercom PC):
   - Right-click the tray icon → **Web Server Settings…**
   - Set the port if needed (default **5016** for Intercom16, **5017** for Intercom2)
   - Choose **Enabled on selected network interface** (recommended) or **all interfaces** (advanced)
   - Click **Save**, then **restart** the application
   - Allow inbound TCP on that port in Windows Firewall

2. **From another PC, tablet, or phone:**
   - Browser → `http://<server-ip>:5016` (or your custom port)
   - The interface is responsive and touch-friendly

3. **Multi-user access:**
   - Multiple browsers or Stream Decks can connect at once
   - All clients stay synchronized in real time via SignalR
   - **No login** — use only on trusted LANs or behind a VPN

> **Security:** Remote control exposes full intercom and settings access without authentication. Keep remote access disabled unless you need it. See [SECURITY.md](../SECURITY.md).

---

## Troubleshooting

### Audio

#### Problem: I can't hear anyone

**Solutions:**
1. Verify output device is correct (Settings → Audio Devices)
2. Check operating system volume (Windows sound mixer)
3. Verify LISTEN is active (**blue**) on the correct channel
4. Check the channel’s **vertical NDI VU** moves when the remote party talks, and **NDI Receive** is set in Settings
5. Verify the channel is in the correct intercom group

#### Problem: Nobody can hear me

**Solutions:**
1. Verify microphone is selected correctly (Settings)
2. Check VU Meter in header: should move when speaking
3. Verify TALK is active (**red**) on the channel
4. Confirm **NDI Send** is set in Settings for that channel
5. Verify microphone isn't muted in Windows

#### Problem: Feedback / Echo

**Solutions:**
1. **Use closed headphones instead of speakers** (most important!)
2. Lower headphone volume
3. Enable Feedback Gate (Settings)
4. Verify N-1 system is active
5. Check you're not in same physical room with open microphones

#### Problem: Distorted audio

**Solutions:**
1. Lower microphone gain in Windows
2. Check VU Meter doesn't go red (clipping)
3. Verify network connection quality (test with ping)
4. Reduce number of active NDI streams
5. Check CPU usage (Task Manager)

#### Problem: Audio dropouts

**Solutions:**
1. Use wired Ethernet, not WiFi
2. Check network cable quality (replace if old)
3. Verify switch is Gigabit
4. Reduce network traffic from other applications
5. Check CPU isn't overloaded

### NDI

#### Problem: NDI sources don't appear

**Solutions:**
1. Verify both machines are on same subnet:
   ```powershell
   ipconfig
   # Check they have similar IPs (e.g., 192.168.1.x)
   ```

2. Check Windows firewall:
   - Windows Defender Firewall → Allow an app
   - Add `NDI Intercom.exe`
   - Allow on Private networks

3. Verify NDI Discovery Service is running:
   ```powershell
   Get-Service "NDI Discovery Service"
   # Should show: Running
   
   # If stopped:
   Start-Service "NDI Discovery Service"
   ```

4. Restart application and refresh browser

5. Check switch supports multicast (IGMP snooping enabled)

#### Problem: Intermittent NDI stream

**Solutions:**
1. Use Ethernet cable, not WiFi
2. Verify switch is Gigabit (1000 Mbps)
3. Test network with ping:
   ```powershell
   ping <other-machine-ip> -t
   # Should show 0% packet loss
   ```

4. Check CPU load (Task Manager → Performance)
5. Reduce number of active NDI streams

#### Problem: High NDI latency

**Solutions:**
1. Use dedicated network switch (not shared with internet traffic)
2. Enable QoS on switch (prioritize multicast traffic)
3. Use Cat6 cables instead of Cat5
4. Reduce network segment hops
5. Check for network congestion

### Stream Deck

#### Problem: Stream Deck buttons don't update

**Solutions:**
1. Verify NDI Intercom is running
2. Check application shows: "Web interface available at: http://localhost:5016"
3. Test web interface in browser (should work)
4. Check Stream Deck logs (Help → Open logs folder)
5. Restart Stream Deck software
6. Remove and re-add buttons

#### Problem: Stream Deck shows old state

**Solutions:**
1. Wait 1-2 seconds for polling to update
2. Press button to force refresh
3. Check server URL in button properties is correct
4. Restart Stream Deck software

#### Problem: Connection error on Stream Deck

**Solutions:**
1. Verify port 5016 isn't blocked by firewall
2. Test connection:
   ```powershell
   curl http://localhost:5016/api/intercom/channels
   # Should return JSON data
   ```
3. Check application isn't running on different port
4. Verify Server URL in button properties

### Configuration

#### Problem: Configuration doesn't save

**Solutions:**
1. Check configuration folder exists:
   ```powershell
   Test-Path "$env:PROGRAMDATA\NDI Intercom16"
   # Should return: True
   ```

2. Create folder if missing:
   ```powershell
   New-Item -Path "$env:PROGRAMDATA\NDI Intercom16" -ItemType Directory -Force
   ```

3. Check file permissions (user must have write access to `%ProgramData%\NDI Intercom16\`)
4. For **web server** settings (port / remote access), use tray **Web Server Settings…** — do not edit `appsettings.json` under `Program Files` (that folder is read-only)

#### Problem: Browser doesn't connect

**Solutions:**
1. Verify application is running (check system tray icon is visible)
2. On the **same PC**, use `http://127.0.0.1:5016` (not only `localhost` if you have multiple network adapters)
3. From **another PC**, confirm remote access is enabled in tray → **Web Server Settings…** and the app was restarted
4. Test port 5016 isn't occupied:
   ```powershell
   netstat -ano | findstr :5016
   ```

5. Try clearing browser cache (Ctrl + Shift + Delete)
6. Try different browser (Chrome, Firefox, Edge)
7. Disable browser extensions

### Performance

#### Problem: High CPU usage

**Solutions:**
1. Reduce number of active NDI channels
2. Disable unused channels in Settings
3. Use audio devices with reliable native drivers for the active backend (WASAPI/ASIO on Windows, PipeWire/PulseAudio on Linux)
4. Close other applications
5. Upgrade CPU (i7 or better recommended)

#### Problem: High latency

**Causes and Solutions:**
1. **Slow network:**
   - Use Gigabit Ethernet
   - Replace old cables
   - Use quality network switch

2. **Overloaded CPU:**
   - Close unnecessary applications
   - Upgrade hardware

3. **Audio buffer:**
   - Uses adaptive buffers (300ms NDI, 200ms ASIO) for stability
   - Designed to eliminate dropouts while maintaining acceptable latency for intercom use

---

## FAQ

### General

**Q: How many intercom stations can I connect?**
A: Each instance supports 16 channels. You can run multiple instances on different computers for more channels.

**Q: Can I use NDI Intercom on Mac or Linux?**
A: Windows is the primary desktop/installer target. Linux is available as a headless build with web UI/API control and PipeWire/PulseAudio audio; ASIO, the Windows tray app, and the Windows installer are not available on Linux. macOS is not currently supported.

**Q: Do I need an NDI license?**
A: No, NDI SDK is free for broadcast use. Download from https://ndi.video/tools/

**Q: Can I use NDI Intercom commercially?**
A: Yes, respecting NDI SDK license terms.

**Q: Is Stream Deck required?**
A: No, Stream Deck is optional. The web interface provides full control.

### Audio

**Q: What is the total system latency?**
A: Adaptive buffering with 300ms NDI ring buffers and 200ms ASIO ring buffers for optimal stability and minimal dropouts. Total latency depends on mode: ~330ms for NDI mode, ~220ms for ASIO mode (including network latency).

**Q: Can I use professional audio interfaces?**
A: Yes. On Windows, the system supports WASAPI and ASIO audio interfaces. For professional routing with Dante Virtual Soundcard or other ASIO devices, use ASIO mode with N-1 routing and intercom groups. On Linux, local audio uses PipeWire/PulseAudio.

**Q: Does it support stereo or multi-channel audio?**
A: Currently mono only, which is standard for intercom systems.

**Q: Can I record conversations?**
A: Not directly. Use third-party NDI recording software like OBS Studio with NDI plugin.

**Q: What sample rate is used?**
A: 48kHz, 16-bit (broadcast standard).

### Network

**Q: Can I use WiFi instead of Ethernet?**
A: Not recommended. WiFi introduces variable latency and packet loss. Always use Ethernet for professional use.

**Q: Does it work over Internet (WAN)?**
A: No, NDI is designed for LAN only. For WAN, use NDI Bridge or dedicated VPN solutions.

**Q: How much bandwidth per channel?**
A: Approximately 8-10 Mbps per NDI audio stream.

**Q: Can I use VLAN for isolation?**
A: Yes, but ensure multicast routing is configured between VLANs.

### Intercom Groups

**Q: What does Group 0 (NONE) mean?**
A: Channel is not assigned to any group and is isolated.

**Q: Can a channel be in multiple groups?**
A: No, each channel can only be in one group at a time.

**Q: How do I change groups?**
A: Via Settings page (select from dropdown) or Stream Deck (use Rotate Group button).

**Q: What's the difference between groups and channels?**
A: Channels are communication endpoints. Groups determine which channels can talk to each other.

### Stream Deck

**Q: Which Stream Deck models are supported?**
A: All Elgato Stream Deck models (Mini, Standard, XL, Mobile, +, Neo).

**Q: Can multiple Stream Decks control the same instance?**
A: Yes, unlimited Stream Decks can connect and control simultaneously.

**Q: How fast do Stream Deck buttons update?**
A: Within 500ms (half a second) when changes are made from web interface.

**Q: Can I customize button colors?**
A: Button colors are automatic based on state (red=TALK ON, blue=LISTEN ON, dark=OFF).

**Q: Can I control NDI Intercom16 from a Stream Deck on a different computer?**
A: Yes! Configure the Server URL in Property Inspector to point to the server's IP address with port 5016. Example: `http://192.168.1.100:5016/api/intercom`. Make sure to include the port number.

**Q: Stream Deck shows "Connection Error" when using remote server**
A: Common causes:
- Missing port number in URL (must include `:5016`)
- Windows Firewall blocking port 5016 on server
- Server and Stream Deck not on same network
- Wrong IP address or server not running
Test by opening `http://<server-ip>:5016` in browser from Stream Deck computer.

**Q: Do I need to configure all buttons individually for remote control?**
A: Yes, each button needs its Server URL updated in Property Inspector. Set up one button first to test, then configure the rest once it's working.

### Configuration

**Q: Can I rename channels?**
A: Yes, via "NDI Send Name" field in channel settings.

**Q: Are settings lost on restart?**
A: No, automatically saved in `%PROGRAMDATA%\NDI Intercom16\config.json`.

**Q: Can I export/import configuration?**
A: Yes. Use the export/import actions in the web UI (files are stored under `%ProgramData%\NDI Intercom16\Exports\`). You can also back up `config.json` manually.

**Q: Can I have different saved configurations?**
A: Manually copy/restore `config.json` files with different names.

### Advanced

**Q: Can I change the web port (5016)?**
A: Yes. Right-click the system tray icon, choose **Web Server Settings…**, set the port and choose remote control: disabled, a **selected network interface**, or all interfaces (advanced). Local access via `http://127.0.0.1:{port}` remains available on the server PC. Click **Save** and restart. Settings are saved in `%ProgramData%\NDI Intercom16\appsettings.json` (writable without admin rights).

**Q: Can I customize the web interface?**
A: Yes, modify files in `wwwroot/` (HTML, CSS, JavaScript).

**Q: How do I update to a new version?**
A:
1. Backup `%PROGRAMDATA%\NDI Intercom16\config.json`
2. Download new installer
3. Run installer (will upgrade existing installation)
4. Configuration is preserved automatically

---

## Support

### Documentation

- **NDI Official Docs**: https://docs.ndi.video/
- **Stream Deck SDK**: https://docs.elgato.com/sdk
- **User Manual**: This document

### Logs and Diagnostics

To report issues, provide:

1. **Application version**: v1.7.5 (NDI Intercom16)
2. **Operating System**: (e.g., Windows 11 Pro 24H2)
3. **Configuration**:
   ```powershell
   type "$env:PROGRAMDATA\NDI Intercom16\config.json"
   ```
5. **Stream Deck logs** (if using Stream Deck):
   - Stream Deck → Help → Open logs folder
   - Attach `plugin-com.ndi.intercom16.log`
6. **Network configuration** (for remote Stream Deck issues):
   ```powershell
   ipconfig
   ping <server-ip>
   curl http://<server-ip>:5016/api/intercom/channels
   ```

### Useful Commands

```powershell
# Check .NET version
dotnet --version

# Check NDI library
Test-Path "C:\Program Files\NDI\NDI 6 SDK\Bin\x64\Processing.NDI.Lib.x64.dll"

# View current configuration
type "$env:PROGRAMDATA\NDI Intercom16\config.json"

# Backup configuration
copy "$env:PROGRAMDATA\NDI Intercom16\config.json" "$HOME\Desktop\intercom_backup.json"

# Restore configuration
copy "$HOME\Desktop\intercom_backup.json" "$env:PROGRAMDATA\NDI Intercom16\config.json"

# Check if port 5016 is open
netstat -ano | findstr :5016

# Test network connectivity
ping <other-machine-ip>
ping <other-machine-ip> -t  # Continuous ping

# Check NDI Discovery Service
Get-Service "NDI Discovery Service"
```

---

## Appendix

### Glossary

- **ASIO**: Audio Stream Input/Output, low-latency audio driver standard for professional audio
- **Cross-Mode Mixing**: Mixing audio between NDI and ASIO channels in the same intercom group
- **N-1 (Minus One)**: Mix-minus routing that excludes a destination's own return audio from the mix sent back to it
- **NDI (Network Device Interface)**: Protocol for video/audio streaming over IP
- **NDI Bridge**: NDI service for routing audio/video across network segments (Host/Join/Local modes)
- **WASAPI**: Windows Audio Session API, Windows low-latency audio system
- **PipeWire/PulseAudio**: Linux audio backend used by the headless build
- **SignalR**: Framework for real-time web communication
- **Full Duplex**: Simultaneous bidirectional communication
- **VU Meter**: Volume Unit Meter, audio level indicator
- **Feedback/Larsen**: Audio return effect that creates high-pitched whistle
- **Gate**: Device that cuts audio below certain threshold
- **Latency**: Delay between audio input and output
- **Buffer**: Temporary memory for audio processing
- **Ring Buffer**: Circular buffer for continuous audio streaming between threads
- **Multicast**: Network protocol for one-to-many communication
- **IGMP Snooping**: Switch feature for efficient multicast routing
- **Stream Deck**: Elgato hardware control device with programmable buttons
- **System Tray**: Windows notification area near the clock where the application icon resides

### Keyboard Shortcuts

Web Interface:
- `F5` or `Ctrl + R`: Refresh page
- `Ctrl + Shift + R`: Hard refresh (clear cache)
- `F12`: Open browser developer tools

### Network Ports

- **TCP 5016**: Web interface and REST API
- **UDP 5353**: mDNS for NDI discovery
- **TCP 5960-5980**: NDI video streams
- **TCP 5961-5981**: NDI audio streams (used by intercom)
- **UDP 5960-5961**: NDI multicast discovery

### File Locations

**User data (Intercom16):**
- `%ProgramData%\NDI Intercom16\config.json` — channel, audio, and identity settings
- `%ProgramData%\NDI Intercom16\appsettings.json` — web server port and remote bind (written by tray **Web Server Settings**)
- `%ProgramData%\NDI Intercom16\logs\` — rolling application logs
- `%ProgramData%\NDI Intercom16\Exports\` — config export/import folder

**User data (Intercom2):** same layout under `%ProgramData%\NDI Intercom2\` (default port **5017**).

**Installation (read-only after setup):**
- `C:\Program Files\NDI\NDI Intercom16\` — application files (default)
- `C:\Program Files\NDI\NDI Intercom2\` — Intercom2 install folder

**Stream Deck plugin (Intercom16 installer option):**
- `%APPDATA%\Elgato\StreamDeck\Plugins\com.ndi.intercom16.sdPlugin\`

**NDI runtime (separate install):**
- NDI Tools / NDI 6 runtime on the system (required for NDI audio on the network)

### Version History

See [CHANGELOG.md](../CHANGELOG.md) for v1.7.x release notes.

Earlier milestones (ASIO support, NDI Bridge, desktop/tray app, unified intercom groups) are described in the relevant sections of this manual.

---

**Manual Version**: 1.7.5  
**Date**: July 2026  
**Application**: NDI Intercom16 & NDI Intercom2 v1.7.5

---

Copyright (c) 2026 Vizrt NDI AB — MIT License (see LICENSE).
NDI® is a registered trademark of Vizrt NDI AB.
