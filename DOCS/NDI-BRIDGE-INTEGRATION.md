# NDI Bridge Integration Guide

## Overview

NDI Intercom16 now includes integrated control of **NDI Bridge Service** directly from the Settings interface. This allows you to manage network routing for NDI sources across different networks or remote locations.

## Prerequisites

1. **NDI Bridge Service** must be installed and running on the same machine or accessible via network
2. Default NDI Bridge Service runs on `http://localhost:8080`
3. If SSL is enabled on NDI Bridge Service, you'll need an API key

## Configuration

### Basic Setup (No SSL)

If NDI Bridge Service is running locally without SSL, no configuration is needed. The integration will automatically connect to `http://localhost:8080`.

### Custom URL or SSL Setup

Edit `appsettings.json` in the application root directory:

```json
{
  "NDIBridge": {
    "Url": "http://localhost:8080",
    "ApiKey": ""
  }
}
```

**Parameters:**
- `Url`: The base URL of your NDI Bridge Service (default: `http://localhost:8080`)
- `ApiKey`: Your NDI Bridge API key (only required if SSL is enabled). Format: `NDI-xxxxx...`

### Example with SSL:

```json
{
  "NDIBridge": {
    "Url": "https://bridge.mydomain.com:443",
    "ApiKey": "NDI-your-api-key-here"
  }
}
```

## Features

### Available Modes

The NDI Bridge Service supports three operation modes:

#### 1. **Host Mode**
Shares local NDI sources to remote locations over WAN/Internet.

**Settings:**
- **Groups**: Comma-separated list of NDI groups to share (e.g., `public,studio1`)
- **Port**: Network port for incoming connections (default: 5990)

**Use Case:** You have NDI sources locally and want to make them available to remote locations.

#### 2. **Join Mode**
Connects to a remote Host to receive NDI sources from another network.

**Settings:**
- **Host IP Address**: IP address of the remote Host (e.g., `203.0.113.45`)
- **Port**: Port of the remote Host (default: 5990)
- **Groups**: Comma-separated list of groups to receive (e.g., `public,studio1`)

**Use Case:** You want to receive NDI sources from a remote location.

#### 3. **Local Mode**
Creates a local bridge for managing groups and bandwidth within the same network.

**Settings:**
- **Groups**: Comma-separated list of groups to manage locally

**Use Case:** Local network routing and group management.

## Using the Interface

### Accessing NDI Bridge Controls

1. Open the NDI Intercom16 web interface: `http://localhost:5016`
2. Navigate to **Settings**
3. Scroll to the **NDI Bridge Service** section

### Connection Status

The status indicator shows:
- **Green (●)**: Connected to NDI Bridge Service
- **Red (●)**: Not connected or service not running

Click the **🔄** button to refresh the connection status.

### Starting a Mode

1. Ensure NDI Bridge Service is connected (green indicator)
2. Configure the settings for your desired mode (Host/Join/Local)
3. Click the corresponding mode button (Host, Join, or Local)
4. The button will become highlighted when active
5. Current mode is displayed at the top: **Active Mode: HOST/JOIN/LOCAL/NONE**

### Stopping a Mode

Click the **Stop** button to stop the currently running mode. All modes must be stopped before switching to a different mode.

## Example Scenarios

### Scenario 1: Sharing Local Sources to Remote Studio

**Setup on Local Machine (Host):**
1. Go to Settings → NDI Bridge Service
2. Configure Host Mode:
   - Groups: `studio1`
   - Port: `5990`
3. Click **Host** button
4. Share your public IP and port with the remote location

**Setup on Remote Machine (Join):**
1. Go to Settings → NDI Bridge Service
2. Configure Join Mode:
   - Host IP Address: `[Your Public IP]`
   - Port: `5990`
   - Groups: `studio1`
3. Click **Join** button
4. NDI sources from the Host will appear in your local NDI source list

### Scenario 2: Bidirectional Communication

For bidirectional NDI communication, you need **two machines**:

**Machine A (sends to Machine B):**
- Mode: Host
- Port: 5990

**Machine B (receives from Machine A and sends back):**
- Mode: Host (on a different port, e.g., 5991)
- Mode: Join (pointing to Machine A:5990)

*Note: NDI Bridge Service can only run one mode at a time per instance. For bidirectional, install a second instance or use two separate machines.*

## Troubleshooting

### "Not connected" Message

**Possible causes:**
1. NDI Bridge Service is not running
   - Start "NDI Bridge Service" from Windows Services
2. Incorrect URL in `appsettings.json`
   - Verify the URL matches your NDI Bridge Service configuration
3. Firewall blocking connection
   - Check Windows Firewall settings
4. SSL/API key mismatch
   - Verify API key is correct if using SSL

### Mode Won't Start

**Possible causes:**
1. Another mode is already running
   - Stop the current mode first
2. Port already in use
   - Change to a different port
3. Invalid IP address (Join mode)
   - Verify the Host IP address is correct and reachable
4. Network/firewall issues
   - Ensure the specified port is open in your firewall

### Sources Not Appearing

**Check:**
1. Groups match on both Host and Join
   - Groups are case-sensitive
2. Firewall allows the specified port
   - Both Host and Join need firewall rules
3. Network connectivity
   - Ping the Host IP from the Join machine
4. NDI sources are active on the Host machine
   - Verify sources are running and sending

## API Endpoints

The integration exposes the following REST API endpoints for programmatic control:

### Status & Information
- `GET /api/ndibridge/test` - Test connection to Bridge Service
- `GET /api/ndibridge/status` - Get running status
- `GET /api/ndibridge/runmode` - Get current mode (HOST/JOIN/LOCAL/NONE)

### Host Mode
- `POST /api/ndibridge/host/start` - Start Host mode
- `POST /api/ndibridge/host/stop` - Stop Host mode
- `GET /api/ndibridge/host/groups` - Get Host groups
- `POST /api/ndibridge/host/groups` - Set Host groups
- `GET /api/ndibridge/host/port` - Get Host port
- `POST /api/ndibridge/host/port` - Set Host port

### Join Mode
- `POST /api/ndibridge/join/start` - Start Join mode
- `POST /api/ndibridge/join/stop` - Stop Join mode
- `GET /api/ndibridge/join/ip` - Get Join IP
- `POST /api/ndibridge/join/ip` - Set Join IP
- `GET /api/ndibridge/join/port` - Get Join port
- `POST /api/ndibridge/join/port` - Set Join port
- `GET /api/ndibridge/join/groups` - Get Join groups
- `POST /api/ndibridge/join/groups` - Set Join groups

### Local Mode
- `POST /api/ndibridge/local/start` - Start Local mode
- `POST /api/ndibridge/local/stop` - Stop Local mode
- `GET /api/ndibridge/local/groups` - Get Local groups
- `POST /api/ndibridge/local/groups` - Set Local groups

## Security Considerations

### SSL/HTTPS

When using NDI Bridge Service with SSL enabled:

1. **HTTPS URL**: Use `https://` in the URL configuration
2. **API Key**: Always use an API key for SSL connections
3. **Certificate**: Ensure valid SSL certificate is installed
4. **Firewall**: Open HTTPS port (typically 443)

### Network Security

1. **Firewall Rules**: Only open required ports
2. **Private Networks**: Use VPN when bridging over public internet
3. **Authentication**: Use SSL + API keys for remote access
4. **Monitoring**: Monitor active connections in NDI Bridge Service web UI

## Additional Resources

- **NDI Bridge Service Documentation**: See `DOCS/NDI Bridge Service Documentation.pdf`
- **NDI Bridge Service Web UI**: `http://localhost:8080` (or your configured URL)
- **NDI Official Website**: https://ndi.video

## Version History

### v1.7.4 (Current)
- NDI Bridge Service integration (Host/Join/Local modes)
- Settings UI for bridge control and real-time status monitoring
- Automatic configuration management
- See [CHANGELOG.md](../CHANGELOG.md) for full release history

---

For support or questions, contact NDI technical support.
