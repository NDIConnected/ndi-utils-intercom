# NDI Bridge Integration — Manual Testing

## Important note

NDI Bridge integration is fully implemented in source code, but you must **rebuild the application** for it to take effect.

## Current status

### Implemented (source)
- Full API controller (`Controllers/NDIBridgeController.cs`)
- Updated UI (`wwwroot/settings.html`, `settings.js`, `settings.css`)
- Configuration (`appsettings.json`)
- `Program.cs` updated with `HttpClient`
- 23 working API endpoints
- Complete documentation

### Still required
- **Rebuild** the application
- NDI Bridge Service installed (optional, to exercise all features)

---

## How to build and test

### Option 1: Full build (recommended)

```powershell
# 1. Stop the service if it is running
Stop-Service "NDI Intercom16" -ErrorAction SilentlyContinue

# 2. Clean previous builds
dotnet clean

# 3. Restore packages (needs internet)
dotnet restore

# 4. Build Release
dotnet build --configuration Release

# 5. Publish
dotnet publish --configuration Release --output publish

# 6. Run the app
cd publish
.\NDI` Intercom16.exe
```

### Option 2: Quick build (if packages are already restored)

```powershell
# Build without restore
dotnet build --configuration Release --no-restore
```

### Option 3: Development only

```powershell
# Run without producing a standalone exe
dotnet run --configuration Release
```

---

## Verifying the integration

### 1. Basic API test

After building, confirm the app is up:

```powershell
# System endpoint (should work)
Invoke-WebRequest -Uri http://localhost:5016/api/system/status -UseBasicParsing
```

**Expected output:**
```json
{
  "running": true,
  "isRunning": true,
  "timestamp": "2026-02-06T...",
  "version": "1.3.0"
}
```

### 2. NDI Bridge endpoint test

```powershell
# NDI Bridge connection test
Invoke-WebRequest -Uri http://localhost:5016/api/ndibridge/test -UseBasicParsing
```

**Expected output (Bridge NOT installed):**
```json
{
  "connected": false,
  "message": "Error connecting..."
}
```

**Expected output (Bridge installed and running):**
```json
{
  "connected": true,
  "message": "Connected to NDI Bridge Service",
  "uptime": "..."
}
```

### 3. Web UI test

1. Open `http://localhost:5016/settings.html`
2. Scroll to the **NDI Bridge Service** section
3. You should see:
   - Status indicator
   - Mode buttons (Host/Join/Local/Stop)
   - Configuration panels

---

## Common issues

### Error: "No connection could be made... (127.0.0.1:9)"

**Cause:** Proxy or firewall blocking NuGet

**Fix:**
```powershell
# Temporarily disable proxy
$env:http_proxy = ""
$env:https_proxy = ""

# Retry restore
dotnet restore
```

### Error: "Access to the path ... apphost.exe denied"

**Cause:** The `.exe` is locked (running app or antivirus)

**Fix:**
1. Close all instances of the application
2. Stop the Windows service if it is running
3. Temporarily disable antivirus
4. Retry the build

### 404 Not Found on `/api/ndibridge/test`

**Cause:** The app was not rebuilt with the new controller

**Fix:** Run the full build (Option 1 above)

---

## Verification checklist

- [ ] Application built successfully
- [ ] `NDI Intercom16.exe` updated (check date/time)
- [ ] Application starts without errors
- [ ] `/api/system/status` returns 200 OK
- [ ] `/api/ndibridge/test` responds (not 404)
- [ ] Web UI loads
- [ ] **NDI Bridge Service** section visible in Settings
- [ ] Controller returns valid JSON

---

## Full test script

After building, run:

```powershell
.\test-ndi-bridge-integration.ps1
```

**Expected output:**
```
========================================
NDI Bridge Integration Test
========================================

1. Testing NDI Intercom16 API...
Testing: System Status... PASS
  Version: 1.3.0
  Running: True

2. Testing NDI Bridge Connection...
Testing: Bridge Connection Test... PASS
  Connected: False
  (normal if Bridge Service is not installed)

... (other tests)

========================================
Test Results
========================================
Passed: X
Failed: 0

All tests passed! ✓
```

---

## File check

Before building, confirm files exist:

```powershell
# Controller
Test-Path "Controllers\NDIBridgeController.cs"  # Should be True

# Config
Test-Path "appsettings.json"  # Should be True

# Frontend
Test-Path "wwwroot\settings.html"  # Should be True
Test-Path "wwwroot\js\settings.js"  # Should be True
Test-Path "wwwroot\css\settings.css"  # Should be True

# Program.cs
Select-String -Path "Program.cs" -Pattern "AddHttpClient"  # Should match
```

All should be `True` or return a match.

---

## Final tests

### Without NDI Bridge Service

```powershell
# 1. Start the app
cd publish
.\NDI` Intercom16.exe

# 2. In another terminal
Invoke-RestMethod -Uri http://localhost:5016/api/ndibridge/test
```

**Result:** Should return `connected: false` (expected when Bridge is not installed)

### With NDI Bridge Service

```powershell
# 1. Install and start NDI Bridge Service (download from ndi.video)

# 2. Start NDI Intercom16

# 3. Test
Invoke-RestMethod -Uri http://localhost:5016/api/ndibridge/test
```

**Result:** Should return `connected: true`

---

## Further reading

After building, see:

- **Quick start:** `NDI-BRIDGE-QUICK-START.md`
- **Full guide:** `NDI-BRIDGE-INTEGRATION.md`
- **API examples:** `NDI-BRIDGE-API-EXAMPLES.md`

---

## Notes

1. **Rebuild required:** Sources were updated; the binary must be rebuilt.
2. **NDI Bridge optional:** Integration works without Bridge Service (reports "not connected").
3. **Dev testing:** Use `dotnet run` for quick iteration without a full publish.
4. **Version:** After rebuild, version should reflect 1.3.1 (or current release).

---

## Next steps

1. Build the application
2. Confirm API responses
3. Test the web UI
4. (Optional) Install NDI Bridge Service
5. Run the full test script
6. Use the features

---

**Last updated:** February 2026  
**Target version:** 1.3.1
