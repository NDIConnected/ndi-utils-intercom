# NDI Bridge Quick Start Guide

## 🚀 5-Minute Setup

### Prerequisites Checklist
- [ ] NDI Intercom16 installed and running
- [ ] NDI Bridge Service installed (download from ndi.video)
- [ ] NDI Bridge Service running (check Windows Services)
- [ ] Network connectivity between locations (for Host/Join)

---

## Scenario 1: Share Local Sources (Host Mode)

**Use Case**: You have NDI sources locally and want to share them with a remote location.

### Steps:

1. **Open Settings**
   ```
   http://localhost:5016/settings.html
   ```

2. **Check Connection**
   - Scroll to "NDI Bridge Service" section
   - Look for green indicator: ● Connected

3. **Configure Host**
   - Groups: `public` (or your group name)
   - Port: `5990` (default)

4. **Start Host**
   - Click **Host** button
   - Wait for confirmation message

5. **Share Your Info**
   - Give remote location your **public IP address** and **port 5990**
   - Example: "Connect to 203.0.113.45:5990, group 'public'"

**Done!** Your NDI sources are now available remotely.

---

## Scenario 2: Receive Remote Sources (Join Mode)

**Use Case**: You want to receive NDI sources from a remote Host.

### Steps:

1. **Get Host Information**
   - IP Address: (from remote Host)
   - Port: (from remote Host, usually 5990)
   - Groups: (from remote Host)

2. **Open Settings**
   ```
   http://localhost:5016/settings.html
   ```

3. **Check Connection**
   - Scroll to "NDI Bridge Service" section
   - Look for green indicator: ● Connected

4. **Configure Join**
   - Host IP Address: `203.0.113.45` (example)
   - Port: `5990`
   - Groups: `public`

5. **Start Join**
   - Click **Join** button
   - Wait for confirmation message

**Done!** Remote NDI sources will appear in your NDI source list.

---

## Scenario 3: Local Network Management (Local Mode)

**Use Case**: Manage NDI groups within your local network.

### Steps:

1. **Open Settings**
   ```
   http://localhost:5016/settings.html
   ```

2. **Check Connection**
   - Scroll to "NDI Bridge Service" section
   - Look for green indicator: ● Connected

3. **Configure Local**
   - Groups: `public,studio1`

4. **Start Local**
   - Click **Local** button
   - Wait for confirmation message

**Done!** Local group management is active.

---

## 🔧 Troubleshooting

### Problem: Red Indicator (Not Connected)

**Solution**:
1. Check if NDI Bridge Service is running:
   - Open Windows Services (Win+R → `services.msc`)
   - Find "NDI Bridge Service"
   - Status should be "Running"
   - If not, click "Start"

2. Check if URL is correct:
   - Default: `http://localhost:8080`
   - Edit `appsettings.json` if different

### Problem: Mode Won't Start

**Solution**:
1. Stop current mode first (click **Stop** button)
2. Check if port is available (not used by another application)
3. Verify firewall allows the port

### Problem: Sources Not Appearing

**Solution**:
1. Check groups match on Host and Join
2. Verify network connectivity (ping Host IP)
3. Check firewall allows the port
4. Restart NDI Bridge Service

---

## 🎯 Best Practices

### Groups
- Use **descriptive names**: `studio1`, `production`, `intercom`
- Keep it **simple**: `public` works for most cases
- **Match exactly**: Groups are case-sensitive

### Ports
- **Default 5990** works for most setups
- Change only if port is in use
- **Remember to open** in firewall

### Network
- **Local network**: Works out of the box
- **Over internet**: Use VPN for security
- **Firewall**: Allow the port on both Host and Join

---

## 📋 Command Cheat Sheet

### PowerShell

```powershell
# Test connection
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/test"

# Start Host
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/host/start" -Method Post -Body "{}" -ContentType "application/json"

# Start Join
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/join/start" -Method Post -Body "{}" -ContentType "application/json"

# Get current mode
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/runmode"

# Stop current mode
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/host/stop" -Method Post -Body "{}" -ContentType "application/json"
```

### cURL

```bash
# Test connection
curl http://localhost:5016/api/ndibridge/test

# Start Host
curl -X POST http://localhost:5016/api/ndibridge/host/start -H "Content-Type: application/json" -d '{}'

# Start Join
curl -X POST http://localhost:5016/api/ndibridge/join/start -H "Content-Type: application/json" -d '{}'

# Get current mode
curl http://localhost:5016/api/ndibridge/runmode

# Stop Host
curl -X POST http://localhost:5016/api/ndibridge/host/stop -H "Content-Type: application/json" -d '{}'
```

---

## 🔗 Useful Links

- **Settings Interface**: http://localhost:5016/settings.html
- **Main Interface**: http://localhost:5016
- **NDI Bridge Web UI**: http://localhost:8080 (if enabled)
- **NDI Website**: https://ndi.video

---

## 📞 Need More Help?

- **Complete Guide**: See `NDI-BRIDGE-INTEGRATION.md`
- **API Examples**: See `NDI-BRIDGE-API-EXAMPLES.md`
- **Release notes**: See [CHANGELOG.md](../CHANGELOG.md)

---

## ⚡ Quick Reference

| Action | URL | Method |
|--------|-----|--------|
| Test Connection | `/api/ndibridge/test` | GET |
| Get Mode | `/api/ndibridge/runmode` | GET |
| Start Host | `/api/ndibridge/host/start` | POST |
| Start Join | `/api/ndibridge/join/start` | POST |
| Start Local | `/api/ndibridge/local/start` | POST |
| Stop Host | `/api/ndibridge/host/stop` | POST |
| Stop Join | `/api/ndibridge/join/stop` | POST |
| Stop Local | `/api/ndibridge/local/stop` | POST |

---

**That's it!** You're ready to use NDI Bridge integration. For advanced features and detailed documentation, see the complete guide.

**Version**: 1.7.4 | **Updated**: June 2026
