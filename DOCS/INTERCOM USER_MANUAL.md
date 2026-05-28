# User Manual - NDI Intercom16

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
11. [Daily Usage](#daily-usage)
12. [Troubleshooting](#troubleshooting)
13. [FAQ](#faq)

---

## Introduction

**NDI Intercom16** is a professional intercom communication system that uses NDI® (Network Device Interface) technology to transmit high-quality audio over local networks.

### Main Features

- **16 Full Duplex Channels**: Simultaneous bidirectional communication
- **Unified Intercom Groups**: NDI and ASIO channels can participate in the same group with cross-mode N-1 mixing
- **N-1 System**: Each channel sends its own mix excluding recipient return (prevents echo and feedback)
- **ASIO Support**: Professional audio routing with Dante Virtual Soundcard and ASIO devices
- **NDI Bridge Integration**: Control NDI Bridge Host/Join/Local modes directly from Settings
- **System Tray Application**: Runs silently in the notification area with quick-access menu
- **Web Interface**: Complete control via web browser with real-time sync
- **Stream Deck Integration**: Hardware control with Elgato Stream Deck
- **Self-Contained**: No additional software installation required (.NET runtime included)
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

- **Operating System**: Windows 10 (64-bit) or Windows 11
- **Processor**: Intel Core i5 or equivalent AMD (i7 recommended)
- **RAM**: 8 GB minimum (16 GB recommended)
- **Network**: Gigabit Ethernet (1000 Mbps) for NDI streaming
- **Audio Card**: WASAPI compatible audio devices (microphone + headphones/speakers)


### Optional Hardware

- **Elgato Stream Deck** (any model) for hardware control
- **Professional audio interface** for better audio quality
- **ASIO-compatible audio interface** (Dante Virtual Soundcard, RME, Focusrite, etc.) for professional routing

### Network Requirements

- **Stable local network** with dedicated Gigabit switch
- **Bandwidth**: ~10 Mbps per NDI audio channel
- **Multicast enabled** (for automatic NDI discovery)

---

## Installation

1. Download the `NDI-Intercom16-Setup-v1.5.0.exe` file
2. Run the installer with administrator privileges
3. Follow the guided procedure:
   - Accept the license
   - Choose installation folder (default: `C:\Program Files\NDI\NDI Intercom16`)
   - Optionally install the Stream Deck plugin
   - Installer will automatically copy all necessary dependencies (including .NET runtime and NDI libraries)
4. At completion, click "Launch NDI Intercom16" or start it from the Start menu

> **Note:** The application is fully self-contained. No additional software installation (such as .NET Runtime) is required.

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
│  Open Web Interface         │  ← Opens the control page in your default browser
│  ─────────────────────────  │
│  Port: 5016          [Set]  │  ← Change the web server port
│  ─────────────────────────  │
│  Exit                       │  ← Closes the application
└─────────────────────────────┘
```

- **Open Web Interface**: Opens `http://localhost:<port>` in your default browser
- **Port configuration**: Change the listening port (default: 5016). After changing, restart the application for the new port to take effect.
- **Exit**: Completely shuts down the application

### 3. Access the Web Interface

Right-click the tray icon and select **"Open Web Interface"**, or open a browser manually:
```
http://localhost:5016
```

From another computer on the same network:
```
http://<server-ip-address>:5016
```

### 4. Initial Configuration

#### Step 1: Configure Audio Devices

1. Click the **"Settings ⚙"** button in the top right
2. In the **"Audio Devices"** section:
   - **Input Device (Microphone)**: Select the microphone to use
   - **Output Device (Speakers)**: Select headphones or speakers
3. Click **"Apply Audio Settings"**
4. Verify the **VU Meter** in the header moves when you speak

#### Step 2: Configure Your First NDI Channel

For each channel you want to activate:

1. Expand the **"Channel X Configuration"** section
2. Configure:
   - **NDI Send Name**: Output channel name (e.g., "CONTROL-ROOM", "CAMERA-1")
   - **Enable NDI Send**: ✅ to enable NDI sending
   - **NDI Receive Source**: Select NDI source to receive (will appear in dropdown)
   - **Enable NDI Receive**: ✅ to enable NDI reception
3. Click **"Apply"** to save

#### Step 3: Test Communication (NDI mode)

1. Return to main page (click "NDI Intercom" logo)
2. Click **TALK** button on Channel 1 (turns green)
3. Click **LISTEN** button on Channel 1 (turns blue)
4. Speak into your microphone
5. You should hear yourself in the headphones (if configured for loopback testing)

---

## Web Interface

### Layout

The interface is divided into three main areas:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  [NDI Logo] Intercom16   [VU Meter]   [Settings ⚙]                         │ ← Header
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │
│  │ CH 1 │ │ CH 2 │ │ CH 3 │ │ CH 4 │ │ CH 5 │ │ CH 6 │ │ CH 7 │ │ CH 8 │  │
│  │GRP:1 │ │GRP:1 │ │GRP:2 │ │GRP:0 │ │GRP:2 │ │GRP:3 │ │GRP:0 │ │GRP:4 │  │
│  │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │  │
│  │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│  │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │
│  │ CH 9 │ │ CH10 │ │ CH11 │ │ CH12 │ │ CH13 │ │ CH14 │ │ CH15 │ │ CH16 │  │
│  │GRP:1 │ │GRP:2 │ │GRP:0 │ │GRP:3 │ │GRP:1 │ │GRP:4 │ │GRP:2 │ │GRP:0 │  │
│  │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │ │ TALK │  │
│  │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│ │LISTEN│  │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │
│                                                                              │
│  [RESET ALL CHANNELS]                                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Interface Elements

#### 1. Header Bar
- **NDI Logo + Title**: Application identification
- **VU Meter**: Real-time input microphone audio level
  - Green: Good level (-12 dB to -6 dB)
  - Yellow: Moderate level (-6 dB to -3 dB)
  - Red: Clipping/too loud (> -3 dB) - reduce gain!
- **Settings Button**: Access to configuration page

#### 2. Channel Card

Each channel displays:

```
┌─────────────────────┐
│ Channel 1           │ ← Channel number
│ CONTROL-ROOM        │ ← Custom name (from NDI Send Name)
│ Group: 1            │ ← Intercom group assignment
│                     │
│ ┌─────────────────┐ │
│ │  TALK  │ LISTEN │ │ ← Control buttons
│ └─────────────────┘ │
│                     │
│ 🟢 NDI ACTIVE       │ ← NDI stream status
└─────────────────────┘
```

**Button States:**

- **TALK (Red/Dark Red)**: Transmitting audio to channel
  - Bright Red: Active ON
  - Dark Red: Inactive OFF
- **LISTEN (Blue/Dark Blue)**: Receiving audio from channel
  - Bright Blue: Active ON
  - Dark Blue: Inactive OFF
- **NDI Status:**
  - 🟢 Green "NDI ACTIVE": Stream active and healthy
  - 🔴 Red "NDI INACTIVE": Stream not active

#### 3. RESET ALL Button

- Click to disable TALK and LISTEN on all channels simultaneously
- Useful for quick "silence" or panic button
- Shows confirmation checkmark when successful

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

For each channel, you can configure:

#### NDI Send Configuration
- **NDI Send Name**: Unique name visible on the network
  - ✅ Use descriptive names (e.g., "CONTROL-ROOM", "CAM-1")
  - ✅ Use hyphens instead of spaces
  - ❌ Don't duplicate names on the same network
- **Enable NDI Send**: Toggle to enable/disable sending

#### NDI Receive Configuration
- **NDI Receive Source**: Select from available NDI sources on network
- **Enable NDI Receive**: Toggle to enable/disable receiving

#### Intercom Group
- **Group Assignment**: Select 0-4
  - **0 = NONE**: Channel is isolated, not part of any group
  - **1-4**: Assign to one of four intercom groups

### Example Configuration: Two Stations

**Station A (Control Room):**
```
Channel 1:
- NDI Send Name: "CONTROL-ROOM"
- Enable NDI Send: ✅
- NDI Receive Source: "CAMERA-1" (from station B)
- Enable NDI Receive: ✅
- Intercom Group: 1
```

**Station B (Camera):**
```
Channel 1:
- NDI Send Name: "CAMERA-1"
- Enable NDI Send: ✅
- NDI Receive Source: "CONTROL-ROOM" (from station A)
- Enable NDI Receive: ✅
- Intercom Group: 1
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

**Available Groups:**
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
1. Navigate to: `C:\TEMP\NDI_INTERCOM _16\StreamDeck-Plugin\`
2. Run: `install-plugin.ps1`
3. Quit Stream Deck software completely (right-click tray → Quit)
4. Restart Stream Deck software
5. Look for "NDI Intercom16" in the actions list

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

#### 4. Reset All
- **Function**: Disable TALK and LISTEN on all channels
- **Visual Feedback:**
  - Shows checkmark (OK) when executed
  - Text shows: `RESET\nALL`

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

### Typical Stream Deck Layout

Example layout for control room operator:

```
┌──────┬──────┬──────┬──────┐
│TALK  │TALK  │TALK  │RESET │
│CH 1  │CH 2  │CH 3  │ ALL  │
├──────┼──────┼──────┼──────┤
│LISTEN│LISTEN│LISTEN│ROTATE│
│CH 1  │CH 2  │CH 3  │GRP 1 │
├──────┼──────┼──────┼──────┤
│TALK  │TALK  │      │      │
│CH 4  │CH 5  │      │      │
├──────┼──────┼──────┼──────┤
│LISTEN│LISTEN│      │      │
│CH 4  │CH 5  │      │      │
└──────┴──────┴──────┴──────┘
```

### Troubleshooting Stream Deck

#### Problem: Plugin doesn't appear in Stream Deck

**Solutions:**
1. Verify plugin is installed:
   - Check: `%APPDATA%\Elgato\StreamDeck\Plugins\com.ndi.intercom16.sdPlugin\`
2. Restart Stream Deck software completely (Quit → Reopen)
3. Reinstall plugin using `install-plugin.ps1`

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

#### Output Device (Speakers)

Select output device:
- **Closed headphones** (highly recommended to avoid feedback)
- **In-ear headphones**
- ❌ **Open speakers** (not recommended: causes feedback)

**Important:** Always use headphones in intercom systems to prevent audio feedback loops!

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

**How Discovery Works:**
1. Application automatically searches for NDI sources on network
2. Sources appear in "NDI Receive Source" dropdown
3. Select desired source
4. Enable "Enable NDI Receive"

**NDI Troubleshooting:**
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

The feedback gate prevents remote users from hearing themselves through your microphone when their audio is playing through your speakers.

**How it works:**
- Monitors audio received from each NDI channel
- When a channel is transmitting audio to you, temporarily reduces your microphone output to that channel
- Prevents acoustic feedback loops in open speaker environments
- Does NOT affect N-1 mix (audio from other channels in the same group)

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

**Important:** This gate is separate from the Microphone Noise Gate and serves a different purpose.

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

In NDI Studio Monitor or any other NDI-compatible tool, your channels appear with the identity tag appended:

```
Channel 1 [app=Intercom_A;device=9f8c1d3a…;role=sender;ch=1]
Intercom RX ch-3 [app=Intercom_A;device=9f8c1d3a…;role=receiver;ch=3]
```

NDI Studio Monitor also displays a **Web Control** link on each Intercom sender — clicking it opens the Settings UI for that specific Intercom instance, even from another machine on the network. This works thanks to the `<ndi_capabilities web_control="…">` metadata that NDI Intercom publishes automatically.

### For developers / integrators

If you are building a management or aggregation application that needs to consume this identity, the full specification (NDI source name grammar, `<ndi_manager>` XML schema, persistent receiver lifecycle, recommended consumption strategy) is in [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

---

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

3. **Emergency silence:**
   - Press RESET ALL button
   - All TALK/LISTEN disabled
   - All buttons turn dark

4. **Change group:**
   - Press ROTATE GROUP button repeatedly
   - Cycles through: NONE → GRP 1 → GRP 2 → GRP 3 → GRP 4 → NONE

### Remote Control

You can control intercom from any device on network:

1. **From network PC:**
   - Browser → `http://<server-ip>:5016`

2. **From tablet/smartphone:**
   - Mobile browser → `http://<server-ip>:5016`
   - Interface is responsive and touch-friendly

3. **Multi-User Access:**
   - Multiple browsers/Stream Decks can connect simultaneously
   - All interfaces stay synchronized in real-time
   - Perfect for distributed control rooms

---

## Troubleshooting

### Audio

#### Problem: I can't hear anyone

**Solutions:**
1. Verify output device is correct (Settings → Audio Devices)
2. Check operating system volume (Windows sound mixer)
3. Verify LISTEN is active (blue) on correct channel
4. Check NDI source is active (green indicator on channel)
5. Verify channel is in correct intercom group

#### Problem: Nobody can hear me

**Solutions:**
1. Verify microphone is selected correctly (Settings)
2. Check VU Meter in header: should move when speaking
3. Verify TALK is active (red) on channel
4. Check NDI Send is enabled (Settings)
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

3. Check file permissions (user must have write access)
4. Run application as Administrator (temporarily)

#### Problem: Browser doesn't connect

**Solutions:**
1. Verify application is running (check system tray icon is visible)
2. Test port 5016 isn't occupied:
   ```powershell
   netstat -ano | findstr :5016
   ```

3. Try clearing browser cache (Ctrl + Shift + Delete)
4. Try different browser (Chrome, Firefox, Edge)
5. Disable browser extensions

### Performance

#### Problem: High CPU usage

**Solutions:**
1. Reduce number of active NDI channels
2. Disable unused channels in Settings
3. Use audio devices with native WASAPI drivers
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
A: No, currently Windows only (requires WASAPI and .NET 8.0 Windows-specific features).

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
A: Yes! The system supports both WASAPI and ASIO audio interfaces. For professional routing with Dante Virtual Soundcard or other ASIO devices, use ASIO mode with N-1 routing and intercom groups.

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
A: Yes, copy the `config.json` file to another computer.

**Q: Can I have different saved configurations?**
A: Manually copy/restore `config.json` files with different names.

### Advanced

**Q: Can I change the web port (5016)?**
A: Yes, right-click the system tray icon and enter a new port number in the "Port" field. Click "Set" and restart the application for the change to take effect. The port setting is saved in `appsettings.json`.

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

1. **Application version**: v1.5.0 (NDI Intercom16)
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
- **N-1 (Minus One)**: System that excludes return audio to prevent feedback
- **NDI (Network Device Interface)**: Protocol for video/audio streaming over IP
- **NDI Bridge**: NDI service for routing audio/video across network segments (Host/Join/Local modes)
- **WASAPI**: Windows Audio Session API, Windows low-latency audio system
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

**Configuration:**
- `%PROGRAMDATA%\NDI Intercom16\config.json` - User configuration

**Installation:**
- `C:\Program Files\NDI\NDI Intercom16\` - Application files (default)

**Stream Deck Plugin:**
- `%APPDATA%\Elgato\StreamDeck\Plugins\com.ndi.intercom16.sdPlugin\` - Plugin files

**NDI Runtime:**
- `C:\Program Files\NDI\NDI 6 SDK\` - NDI libraries

### Version History

**v1.5.0** (February 2026)
- **Unified Intercom Groups**: NDI and ASIO channels can now participate in the same group
  - Cross-mode N-1 mixing: NDI and ASIO users hear each other seamlessly
  - Ring-buffered audio pipeline for glitch-free cross-mode streaming
  - Single "Intercom Group" setting per channel (replaces separate NDI/ASIO group fields)
- **NDI Bridge Service Integration**: Control NDI Bridge directly from Settings
  - Host, Join, and Local mode configuration
  - Encoder/decoder settings (HX quality, compression, GPU selection, encryption)
  - Real-time Bridge status monitoring
- **Codebase Cleanup**: Deep audit and refactoring for stability and maintainability

**v1.4.0** (January 2026)
- **Desktop Application**: Converted from Windows Service to desktop application
  - System tray icon with right-click menu
  - Configure web server port from tray menu
  - Quick access link to web interface
- **ASIO Hot-Start**: ASIO devices can be initialized after application startup
- **Improved Error Reporting**: Detailed ASIO error messages displayed in the UI

**v1.3.0** (December 2025)
- **NDI Bridge Service**: Initial integration with NDI Bridge API
  - Host/Join/Local mode control from Settings page
  - Bridge connection status display

**v1.2.0** (November 2025)
- **ASIO Support**: Professional audio routing with ASIO-compatible devices
  - 16 input and 16 output channels for ASIO devices
  - N-1 routing for ASIO intercom groups
  - Dedicated ASIO audio engine with lock-free processing
- **Adaptive Audio Buffers**: 300ms NDI / 400ms ASIO ring buffers for stability

**v1.0.0** (January 2025)
- **Initial Release**: 16-channel NDI intercom system
  - Full duplex communication, 4 intercom groups, N-1 mixing
  - Web-based control interface on port 5016
  - Stream Deck plugin for hardware control

---

**Manual Version**: 5.0
**Date**: February 2026
**Application**: NDI Intercom16 v1.5.0

---

Copyright 2026 NDI Intercom16 - Powered by NDI Technology
