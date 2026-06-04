# NDI Intercom16 - REST API Guide

## Overview

NDI Intercom16 exposes a **complete REST API** for remote control of all 16 channels. This API allows you to integrate NDI Intercom16 with:

- **Stream Deck** - Hardware control via buttons
- **Control Surfaces** - Hardware control panels
- **Automation** - Scripts, macros, automation systems
- **Custom Applications** - Custom integrations
- **Web Dashboards** - Remote web interfaces
- **Mobile Apps** - iOS/Android applications

> Note: Discovery Server monitoring endpoints (`/api/ndi/senders` and `/api/ndi/receivers`) require a configured NDI Discovery Server.

> **v1.7+**: REST endpoints in this guide control **per‑channel state** (TALK / LISTEN / group / level). For instance‑level identity (`applicationId` / `deviceId`), use the SignalR hub method `GetConfiguration` / `ApplyConfiguration` (see [§ Instance identity (v1.7+)](#instance-identity-v17)). External management apps that need to **aggregate Intercom instances over the NDI network** should read [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md) and consume the identity directly from NDI source names + `<ndi_manager>` connection metadata, not from this REST API.

## Base URL

```
http://localhost:5016/api/intercom
```

For remote access, replace `localhost` with the IP address of the computer running NDI Intercom16:
```
http://192.168.1.100:5016/api/intercom
```

## Authentication

**No authentication** — the control API is open to any client that can reach the web server.

**Default:** remote control is **off**; only this PC can reach the web UI over the network (`WebServer:RemoteAccess`: `Off`). **Localhost remains available on the server machine** (`http://127.0.0.1:5016`). On Windows, enable remote access from the tray: **Web Server Settings…** → select a network interface (recommended) or all interfaces. See [SECURITY.md](../SECURITY.md).

---

## Available Endpoints

### 📊 Read Channel State

#### `GET /api/intercom/channels`
Gets the state of all 16 channels.

**Request:**
```bash
curl http://localhost:5016/api/intercom/channels
```

**Response:**
```json
[
  {
    "channelNumber": 1,
    "label": "Channel 1",
    "talkEnabled": false,
    "listenEnabled": false,
    "intercomGroup": 0,
    "ndiSendName": "Channel 1",
    "inputLevel": 100,
    "outputLevel": 100
  },
  {
    "channelNumber": 2,
    "label": "Channel 2",
    "talkEnabled": true,
    "listenEnabled": false,
    "intercomGroup": 1,
    "ndiSendName": "Channel 2",
    "inputLevel": 100,
    "outputLevel": 100
  },
  // ... channels 3-16
]
```

**Response fields:**
- `channelNumber` (int): Channel number (1-16)
- `label` (string): Channel label
- `talkEnabled` (bool): TALK active (microphone transmits)
- `listenEnabled` (bool): LISTEN active (monitor audio)
- `intercomGroup` (int): Intercom group (0=none, 1-4=groups)
- `ndiSendName` (string): NDI sender name
- `inputLevel` (int): Input level (0-100)
- `outputLevel` (int): Output level (0-100)

---

#### `GET /api/intercom/channels/{N}`
Gets the state of a specific channel.

**Parameters:**
- `N` (path, int): Channel number (1-16)

**Request:**
```bash
curl http://localhost:5016/api/intercom/channels/5
```

**Response:**
```json
{
  "channelNumber": 5,
  "label": "Channel 5",
  "talkEnabled": true,
  "listenEnabled": false,
  "intercomGroup": 2,
  "ndiSendName": "Channel 5",
  "inputLevel": 100,
  "outputLevel": 100
}
```

**Errors:**
- `404 Not Found` - Channel not found (N must be 1-16)

---

### 🎤 TALK Control (Transmission)

#### `POST /api/intercom/channels/{N}/talk/toggle`
Toggles TALK for a channel.

**Parameters:**
- `N` (path, int): Channel number (1-16)

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/3/talk/toggle
```

**Response:**
```json
{
  "channelNumber": 3,
  "talkEnabled": true
}
```

**Behavior:**
- If TALK is OFF → becomes ON
- If TALK is ON → becomes OFF

---

#### `POST /api/intercom/channels/{N}/talk/on`
Enables TALK for a channel (force ON).

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/7/talk/on
```

**Response:**
```json
{
  "channelNumber": 7,
  "talkEnabled": true
}
```

**Behavior:**
- Forces TALK ON (even if already ON)

---

#### `POST /api/intercom/channels/{N}/talk/off`
Disables TALK for a channel (force OFF).

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/7/talk/off
```

**Response:**
```json
{
  "channelNumber": 7,
  "talkEnabled": false
}
```

**Behavior:**
- Forces TALK OFF (even if already OFF)

---

### 🎧 LISTEN Control (Monitoring)

#### `POST /api/intercom/channels/{N}/listen/toggle`
Toggles LISTEN for a channel.

**Parameters:**
- `N` (path, int): Channel number (1-16)

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/12/listen/toggle
```

**Response:**
```json
{
  "channelNumber": 12,
  "listenEnabled": true
}
```

**Behavior:**
- If LISTEN is OFF → becomes ON
- If LISTEN is ON → becomes OFF

---

#### `POST /api/intercom/channels/{N}/listen/on`
Enables LISTEN for a channel (force ON).

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/9/listen/on
```

**Response:**
```json
{
  "channelNumber": 9,
  "listenEnabled": true
}
```

---

#### `POST /api/intercom/channels/{N}/listen/off`
Disables LISTEN for a channel (force OFF).

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/9/listen/off
```

**Response:**
```json
{
  "channelNumber": 9,
  "listenEnabled": false
}
```

---

### 👥 Intercom Group Management

#### `POST /api/intercom/channels/{N}/group`
Sets the intercom group for a channel.

**Parameters:**
- `N` (path, int): Channel number (1-16)
- `group` (body, int): Group to assign (0-4)
  - `0` = No group
  - `1-4` = Intercom groups

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/4/group \
  -H "Content-Type: application/json" \
  -d '{"group": 2}'
```

**Response:**
```json
{
  "channelNumber": 4,
  "group": 2
}
```

**Errors:**
- `400 Bad Request` - Invalid group (must be 0-4)
- `404 Not Found` - Channel not found

**Notes:**
- Group configuration is saved automatically
- Channels in the same group communicate with each other (N-1 mixing)
- This `intercomGroup` is an **internal mixing concept** of NDI Intercom (used to build N-1 mixes across NDI and ASIO channels of the same instance). It is **unrelated to NDI groups** (`p_groups`), which are not used by NDI Intercom v1.7+ for routing or aggregation. To group endpoints across the NDI network, see [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md)

---

#### `POST /api/intercom/channels/{N}/group/rotate`
Rotates the intercom group of a channel (0→1→2→3→4→0).

**Parameters:**
- `N` (path, int): Channel number (1-16)

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/15/group/rotate
```

**Response:**
```json
{
  "channelNumber": 15,
  "group": 3
}
```

**Behavior:**
- If current group is 0 → becomes 1
- If current group is 1 → becomes 2
- If current group is 2 → becomes 3
- If current group is 3 → becomes 4
- If current group is 4 → becomes 0

**Notes:**
- Useful for Stream Deck or hardware buttons
- Configuration is saved automatically

---

### ⚠️ Utility Functions

#### `POST /api/intercom/reset`
Disables TALK and LISTEN on all 16 channels.

**Request:**
```bash
curl -X POST http://localhost:5016/api/intercom/reset
```

**Response:**
```json
{
  "message": "All channels reset"
}
```

**Behavior:**
- Disables TALK on channels 1-16
- Disables LISTEN on channels 1-16
- Does NOT modify intercom groups
- Does NOT modify configurations (labels, levels, etc.)

**Use Cases:**
- Emergency "silence all" button
- End of broadcast
- Quick reset before new show

---

## Usage Examples

### Scenario 1: Single Channel Control

**Enable TALK on channel 5:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/5/talk/on
```

**Enable LISTEN on channel 8:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/8/listen/on
```

**Disable both:**
```bash
curl -X POST http://localhost:5016/api/intercom/channels/5/talk/off
curl -X POST http://localhost:5016/api/intercom/channels/8/listen/off
```

---

### Scenario 2: Intercom Group Setup

Create a group of 4 people (channels 1-4) to talk to each other:

```bash
# Assign all to group 1
curl -X POST http://localhost:5016/api/intercom/channels/1/group \
  -H "Content-Type: application/json" -d '{"group": 1}'
curl -X POST http://localhost:5016/api/intercom/channels/2/group \
  -H "Content-Type: application/json" -d '{"group": 1}'
curl -X POST http://localhost:5016/api/intercom/channels/3/group \
  -H "Content-Type: application/json" -d '{"group": 1}'
curl -X POST http://localhost:5016/api/intercom/channels/4/group \
  -H "Content-Type: application/json" -d '{"group": 1}'

# Enable TALK for all
curl -X POST http://localhost:5016/api/intercom/channels/1/talk/on
curl -X POST http://localhost:5016/api/intercom/channels/2/talk/on
curl -X POST http://localhost:5016/api/intercom/channels/3/talk/on
curl -X POST http://localhost:5016/api/intercom/channels/4/talk/on
```

Now the 4 channels can talk to each other with N-1 mixing!

---

### Scenario 3: Director Monitoring

Director wants to listen to 8 talents (channels 1-8) without transmitting:

```bash
# Enable LISTEN on channels 1-8
for i in {1..8}; do
  curl -X POST http://localhost:5016/api/intercom/channels/$i/listen/on
done

# Director's channel (e.g., 9) has NO TALK active
curl -X POST http://localhost:5016/api/intercom/channels/9/talk/off
```

---

### Scenario 4: System State Check

Check state of all channels:
```bash
curl http://localhost:5016/api/intercom/channels | jq
```

Check only channel 10:
```bash
curl http://localhost:5016/api/intercom/channels/10 | jq
```

Extract only channels with TALK active:
```bash
curl http://localhost:5016/api/intercom/channels | jq '.[] | select(.talkEnabled == true)'
```

---

### Scenario 5: Automation Script

Bash script to enable TALK on odd channels (1,3,5,7,9,11,13,15):

```bash
#!/bin/bash
for i in 1 3 5 7 9 11 13 15; do
  curl -X POST http://localhost:5016/api/intercom/channels/$i/talk/toggle
  echo "Toggled channel $i"
done
```

PowerShell script for Windows:
```powershell
1,3,5,7,9,11,13,15 | ForEach-Object {
    Invoke-RestMethod -Method Post -Uri "http://localhost:5016/api/intercom/channels/$_/talk/toggle"
    Write-Host "Toggled channel $_"
}
```

---

### Scenario 6: Python Integration

```python
import requests

API_BASE = "http://localhost:5016/api/intercom"

# Get all channels
response = requests.get(f"{API_BASE}/channels")
channels = response.json()
print(f"Found {len(channels)} channels")

# Enable TALK on channel 1
requests.post(f"{API_BASE}/channels/1/talk/on")

# Set group 2 for channel 5
requests.post(
    f"{API_BASE}/channels/5/group",
    json={"group": 2}
)

# Reset all
requests.post(f"{API_BASE}/reset")
```

---

### Scenario 7: JavaScript/Node.js

```javascript
const API_BASE = 'http://localhost:5016/api/intercom';

// Toggle TALK channel 3
fetch(`${API_BASE}/channels/3/talk/toggle`, { method: 'POST' })
  .then(res => res.json())
  .then(data => console.log('TALK toggled:', data));

// Set group
fetch(`${API_BASE}/channels/7/group`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ group: 4 })
})
  .then(res => res.json())
  .then(data => console.log('Group set:', data));
```

---

## CORS and Remote Access

### Browser Access (CORS)

The API supports **CORS** for browser calls. If you encounter CORS issues, verify:

1. URL is correct (http://localhost:5016)
2. Browser allows local requests
3. No proxy/firewall blocking

### Local Network Access

To control NDI Intercom16 from another computer:

1. **Find IP of computer** running NDI Intercom16:
   ```bash
   ipconfig  # Windows
   ifconfig  # Linux/Mac
   ```

2. **Configure firewall** to allow port 5016

3. **Use IP instead of localhost**:
   ```bash
   curl http://192.168.1.100:5016/api/intercom/channels
   ```

---

## Error Handling

### HTTP Codes

- `200 OK` - Request completed successfully
- `400 Bad Request` - Invalid parameters (e.g., group out of range)
- `404 Not Found` - Channel not found (N must be 1-16)
- `500 Internal Server Error` - Server error

### Error Handling in Scripts

**Bash:**
```bash
if curl -f -X POST http://localhost:5016/api/intercom/channels/1/talk/on; then
  echo "Success"
else
  echo "Failed"
  exit 1
fi
```

**Python:**
```python
try:
    response = requests.post(f"{API_BASE}/channels/1/talk/on")
    response.raise_for_status()
    print("Success:", response.json())
except requests.exceptions.RequestException as e:
    print("Error:", e)
```

---

## Testing and Debugging

### Quick Test with curl

```bash
# 1. Verify API is available
curl http://localhost:5016/api/intercom/channels

# 2. Toggle TALK channel 1
curl -X POST http://localhost:5016/api/intercom/channels/1/talk/toggle

# 3. Verify state changed
curl http://localhost:5016/api/intercom/channels/1
```

### Test with Postman

1. Import collection from base URL: `http://localhost:5016/api/intercom`
2. Create requests for each endpoint
3. Save environment with `baseUrl` variable

### Real-Time Monitoring

The API doesn't support WebSocket/SSE for real-time updates, but you can:

1. **Polling**: Call `GET /api/intercom/channels` every N seconds
2. **SignalR**: Connect to SignalR hub (see advanced section)

---

## Limits and Notes

- **Rate Limiting**: No limit implemented. Use moderately.
- **Concurrency**: API safely handles concurrent requests
- **Persistence**: Group changes are saved automatically
- **Restart**: TALK/LISTEN don't persist after restart (reset to OFF)
- **Groups**: Only 4 groups available (1-4), plus group 0 (no group)

---

## Security

⚠️ **IMPORTANT**: The API does not require authentication.

### Best Practices:

1. **Don't expose on Internet** - Use only on local network/VPN
2. **Firewall**: Block port 5016 from untrusted networks
3. **VPN**: If remote access needed, use VPN
4. **Reverse Proxy**: Use NGINX/Apache to add authentication

### Example NGINX with Basic Auth:

```nginx
location /api/intercom/ {
    auth_basic "NDI Intercom API";
    auth_basic_user_file /etc/nginx/.htpasswd;
    proxy_pass http://localhost:5016/api/intercom/;
}
```

---

## Instance identity (v1.7+)

Per‑channel REST endpoints described above don't expose the instance‑level identity (`applicationId` / `deviceId`). To read or update them programmatically, use the **SignalR hub** at `/intercomHub` and invoke:

| Hub method                   | Direction      | Purpose                                                                          |
|------------------------------|----------------|----------------------------------------------------------------------------------|
| `GetConfiguration()`         | client → server | Returns the full `AppConfig` JSON, including `applicationId` and `deviceId`.    |
| `ApplyConfiguration(config)` | client → server | Saves and applies a full `AppConfig` (including `applicationId`).               |
| `GetProductInfo()`           | client → server | Returns `MaxChannels`, `ProductDisplayName`, `UiTitleShort`, `AsioAvailable`, `AudioBackendName`. |

> `deviceId` is **read‑only by design**. It is auto‑generated on first run and persisted to `config.json`. Any value you submit via `ApplyConfiguration` for `deviceId` is currently accepted, but you should treat the field as opaque and never overwrite it from a management app.

**Validation of `applicationId`** (server side, mirrored in the Web UI):

- Regex: `^[A-Za-z0-9_.-]{1,64}$` (1–64 chars; ASCII alphanum, underscore, dot, hyphen).
- Empty or invalid input is silently coerced to the default `Intercom_A`.

For the full identity propagation model (NDI source name suffix, `<ndi_manager>` connection metadata, persistent receivers and Discovery Server registration), read [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md).

### Browser example (SignalR client)

```javascript
const conn = new signalR.HubConnectionBuilder()
    .withUrl("/intercomHub")
    .build();

await conn.start();
const config = await conn.invoke("GetConfiguration");
console.log(config.applicationId, config.deviceId);

config.applicationId = "Intercom_B";
await conn.invoke("ApplyConfiguration", config);
```

---

## Common Integrations

### Stream Deck Plugin
See: [STREAMDECK_PLUGIN_GUIDE.md](STREAMDECK_PLUGIN_GUIDE.md)

### Home Assistant
```yaml
rest_command:
  ndi_channel_1_talk_on:
    url: http://192.168.1.100:5016/api/intercom/channels/1/talk/on
    method: POST
```

### Node-RED
Use HTTP Request node with:
- Method: POST
- URL: `http://localhost:5016/api/intercom/channels/1/talk/toggle`

### TouchOSC / MIDI Controllers
Map MIDI/OSC buttons to HTTP calls via middleware like:
- **OSC2HTTP**: Converts OSC → HTTP
- **MIDI2HTTP**: Converts MIDI → HTTP

---

## FAQ

**Q: Can I control multiple NDI Intercom16 instances?**
A: Yes. Each instance binds its own port (default `5016` for Intercom16, `5017` for Intercom2; configurable via `appsettings.json` → `WebServer:Port`). To **aggregate** instances on the NDI network rather than poll each REST endpoint individually, use the identity scheme described in [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md): every sender carries `<ndi_capabilities web_control="http://%IP%:<port>/" />` and `<ndi_manager app_id=… device_id=… />`, so a discovery scan on the NDI network is enough to find all instances and their Web UIs.

**Q: Do TALK/LISTEN states persist after restart?**
A: No, they reset to OFF. Only groups are saved.

**Q: Can I use HTTPS?**
A: NDI Intercom16 uses HTTP. Use reverse proxy (NGINX) for HTTPS.

**Q: How fast is the API?**
A: Typical latency < 10ms on local network.

**Q: Does it support WebSocket for real-time updates?**
A: Use SignalR hub for real-time (see advanced section).

---

## Support and Resources

- **Source Code**: IntercomController.cs
- **Stream Deck Plugin**: STREAMDECK_PLUGIN_GUIDE.md
- **Examples**: See "Usage Examples" section

For questions and support, consult the project's main documentation.

---

## API Changelog

**v1.7.0** (24/7 stability hardening + identity-aware NDI routing)
- New `GET /healthz` endpoint: returns 200 once the engine is initialized and the audio thread is running, 503 otherwise. Suitable for external monitoring loops.
- Added `applicationId` / `deviceId` to `AppConfig` (read/write via SignalR `GetConfiguration` / `ApplyConfiguration`; **not exposed via REST** — see [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md))
- Endpoint count is now product-dependent (16 for Intercom16, 2 for Intercom2); paths are unchanged but `N` is bounded by `MaxChannels`
- NDI groups (`p_groups`) are no longer used for routing; `intercomGroup` (0-4) is an internal N-1 mixing concept and remains exposed by REST
- The web port is configurable (`5016` Intercom16 / `5017` Intercom2 by default, override via `appsettings.json` → `WebServer:Port`)

**v1.0.0** (NDI Intercom16)
- Full support for 16 channels (1-16)
- Port 5016 (was 5000 for 8-channel version)
- 4 intercom groups (0-4)
- TALK endpoints: toggle, on, off
- LISTEN endpoints: toggle, on, off
- GROUP endpoints: set, rotate
- RESET endpoint: reset all
- CORS enabled for browser calls
