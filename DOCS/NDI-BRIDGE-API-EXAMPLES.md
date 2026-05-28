# NDI Bridge API Usage Examples

## C# Examples

### Basic Connection Test

```csharp
using System.Net.Http;
using System.Text.Json;

var client = new HttpClient();
var response = await client.GetAsync("http://localhost:5016/api/ndibridge/test");
var json = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<dynamic>(json);

Console.WriteLine($"Connected: {result.connected}");
```

### Start Host Mode

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

var client = new HttpClient();

// Set groups
var groupsJson = JsonSerializer.Serialize(new { groups = "public,studio1" });
await client.PostAsync(
    "http://localhost:5016/api/ndibridge/host/groups", 
    new StringContent(groupsJson, Encoding.UTF8, "application/json")
);

// Set port
var portJson = JsonSerializer.Serialize(new { port = 5990 });
await client.PostAsync(
    "http://localhost:5016/api/ndibridge/host/port", 
    new StringContent(portJson, Encoding.UTF8, "application/json")
);

// Start Host
var response = await client.PostAsync(
    "http://localhost:5016/api/ndibridge/host/start",
    new StringContent("{}", Encoding.UTF8, "application/json")
);

var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

### Start Join Mode

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

var client = new HttpClient();

// Set IP address
var ipJson = JsonSerializer.Serialize(new { ipAddress = "192.168.1.100" });
await client.PostAsync(
    "http://localhost:5016/api/ndibridge/join/ip", 
    new StringContent(ipJson, Encoding.UTF8, "application/json")
);

// Set port
var portJson = JsonSerializer.Serialize(new { port = 5990 });
await client.PostAsync(
    "http://localhost:5016/api/ndibridge/join/port", 
    new StringContent(portJson, Encoding.UTF8, "application/json")
);

// Set groups
var groupsJson = JsonSerializer.Serialize(new { groups = "public,studio1" });
await client.PostAsync(
    "http://localhost:5016/api/ndibridge/join/groups", 
    new StringContent(groupsJson, Encoding.UTF8, "application/json")
);

// Start Join
var response = await client.PostAsync(
    "http://localhost:5016/api/ndibridge/join/start",
    new StringContent("{}", Encoding.UTF8, "application/json")
);

var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

### Get Current Status

```csharp
using System.Net.Http;
using System.Text.Json;

var client = new HttpClient();

// Get running status
var statusResponse = await client.GetAsync("http://localhost:5016/api/ndibridge/status");
var statusJson = await statusResponse.Content.ReadAsStringAsync();
Console.WriteLine($"Status: {statusJson}");

// Get current mode
var modeResponse = await client.GetAsync("http://localhost:5016/api/ndibridge/runmode");
var modeJson = await modeResponse.Content.ReadAsStringAsync();
Console.WriteLine($"Mode: {modeJson}");
```

### Stop Current Mode

```csharp
using System.Net.Http;
using System.Text;

var client = new HttpClient();

// First, get current mode
var modeResponse = await client.GetAsync("http://localhost:5016/api/ndibridge/runmode");
var modeJson = await modeResponse.Content.ReadAsStringAsync();
var modeData = JsonSerializer.Deserialize<dynamic>(modeJson);
string currentMode = modeData.mode.ToString().ToLower();

// Stop based on current mode
string stopUrl = currentMode switch
{
    "host" => "http://localhost:5016/api/ndibridge/host/stop",
    "join" => "http://localhost:5016/api/ndibridge/join/stop",
    "local" => "http://localhost:5016/api/ndibridge/local/stop",
    _ => null
};

if (stopUrl != null)
{
    var response = await client.PostAsync(
        stopUrl,
        new StringContent("{}", Encoding.UTF8, "application/json")
    );
    var result = await response.Content.ReadAsStringAsync();
    Console.WriteLine(result);
}
```

## PowerShell Examples

### Test Connection

```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/test" -Method Get
Write-Host "Connected: $($response.connected)"
```

### Start Host Mode

```powershell
# Set groups
$groupsBody = @{ groups = "public,studio1" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/host/groups" `
    -Method Post -Body $groupsBody -ContentType "application/json"

# Set port
$portBody = @{ port = 5990 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/host/port" `
    -Method Post -Body $portBody -ContentType "application/json"

# Start Host
$response = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/host/start" `
    -Method Post -Body "{}" -ContentType "application/json"
Write-Host "Result: $($response.message)"
```

### Start Join Mode

```powershell
# Set IP
$ipBody = @{ ipAddress = "192.168.1.100" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/join/ip" `
    -Method Post -Body $ipBody -ContentType "application/json"

# Set port
$portBody = @{ port = 5990 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/join/port" `
    -Method Post -Body $portBody -ContentType "application/json"

# Set groups
$groupsBody = @{ groups = "public,studio1" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/join/groups" `
    -Method Post -Body $groupsBody -ContentType "application/json"

# Start Join
$response = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/join/start" `
    -Method Post -Body "{}" -ContentType "application/json"
Write-Host "Result: $($response.message)"
```

### Get Status

```powershell
# Get running status
$status = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/status" -Method Get
Write-Host "Is Running: $($status.isRunning)"

# Get current mode
$mode = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/runmode" -Method Get
Write-Host "Current Mode: $($mode.mode)"
```

### Stop Current Mode

```powershell
# Get current mode
$mode = Invoke-RestMethod -Uri "http://localhost:5016/api/ndibridge/runmode" -Method Get
$currentMode = $mode.mode.ToLower()

# Stop based on mode
$stopUrl = switch ($currentMode) {
    "host" { "http://localhost:5016/api/ndibridge/host/stop" }
    "join" { "http://localhost:5016/api/ndibridge/join/stop" }
    "local" { "http://localhost:5016/api/ndibridge/local/stop" }
    default { $null }
}

if ($stopUrl) {
    $response = Invoke-RestMethod -Uri $stopUrl -Method Post -Body "{}" -ContentType "application/json"
    Write-Host "Result: $($response.message)"
}
```

## Python Examples

### Test Connection

```python
import requests
import json

response = requests.get('http://localhost:5016/api/ndibridge/test')
data = response.json()
print(f"Connected: {data['connected']}")
```

### Start Host Mode

```python
import requests

# Set groups
requests.post(
    'http://localhost:5016/api/ndibridge/host/groups',
    json={'groups': 'public,studio1'}
)

# Set port
requests.post(
    'http://localhost:5016/api/ndibridge/host/port',
    json={'port': 5990}
)

# Start Host
response = requests.post('http://localhost:5016/api/ndibridge/host/start', json={})
result = response.json()
print(f"Success: {result['success']}, Message: {result['message']}")
```

### Start Join Mode

```python
import requests

# Set IP
requests.post(
    'http://localhost:5016/api/ndibridge/join/ip',
    json={'ipAddress': '192.168.1.100'}
)

# Set port
requests.post(
    'http://localhost:5016/api/ndibridge/join/port',
    json={'port': 5990}
)

# Set groups
requests.post(
    'http://localhost:5016/api/ndibridge/join/groups',
    json={'groups': 'public,studio1'}
)

# Start Join
response = requests.post('http://localhost:5016/api/ndibridge/join/start', json={})
result = response.json()
print(f"Success: {result['success']}, Message: {result['message']}")
```

### Get Status

```python
import requests

# Get running status
status = requests.get('http://localhost:5016/api/ndibridge/status').json()
print(f"Is Running: {status['isRunning']}")

# Get current mode
mode = requests.get('http://localhost:5016/api/ndibridge/runmode').json()
print(f"Current Mode: {mode['mode']}")
```

## JavaScript/Node.js Examples

### Test Connection

```javascript
const fetch = require('node-fetch');

async function testConnection() {
    const response = await fetch('http://localhost:5016/api/ndibridge/test');
    const data = await response.json();
    console.log(`Connected: ${data.connected}`);
}

testConnection();
```

### Start Host Mode

```javascript
const fetch = require('node-fetch');

async function startHost() {
    // Set groups
    await fetch('http://localhost:5016/api/ndibridge/host/groups', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ groups: 'public,studio1' })
    });
    
    // Set port
    await fetch('http://localhost:5016/api/ndibridge/host/port', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ port: 5990 })
    });
    
    // Start Host
    const response = await fetch('http://localhost:5016/api/ndibridge/host/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: '{}'
    });
    
    const result = await response.json();
    console.log(`Success: ${result.success}, Message: ${result.message}`);
}

startHost();
```

### Browser (Vanilla JavaScript)

```javascript
// Test connection
async function testConnection() {
    try {
        const response = await fetch('http://localhost:5016/api/ndibridge/test');
        const data = await response.json();
        console.log('Connected:', data.connected);
    } catch (err) {
        console.error('Error:', err);
    }
}

// Start Host mode
async function startHost() {
    try {
        // Set groups
        await fetch('http://localhost:5016/api/ndibridge/host/groups', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ groups: 'public,studio1' })
        });
        
        // Set port
        await fetch('http://localhost:5016/api/ndibridge/host/port', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ port: 5990 })
        });
        
        // Start
        const response = await fetch('http://localhost:5016/api/ndibridge/host/start', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: '{}'
        });
        
        const result = await response.json();
        alert(`Success: ${result.success}\nMessage: ${result.message}`);
    } catch (err) {
        alert('Error: ' + err.message);
    }
}
```

## cURL Examples

### Test Connection

```bash
curl -X GET http://localhost:5016/api/ndibridge/test
```

### Start Host Mode

```bash
# Set groups
curl -X POST http://localhost:5016/api/ndibridge/host/groups \
  -H "Content-Type: application/json" \
  -d '{"groups":"public,studio1"}'

# Set port
curl -X POST http://localhost:5016/api/ndibridge/host/port \
  -H "Content-Type: application/json" \
  -d '{"port":5990}'

# Start Host
curl -X POST http://localhost:5016/api/ndibridge/host/start \
  -H "Content-Type: application/json" \
  -d '{}'
```

### Start Join Mode

```bash
# Set IP
curl -X POST http://localhost:5016/api/ndibridge/join/ip \
  -H "Content-Type: application/json" \
  -d '{"ipAddress":"192.168.1.100"}'

# Set port
curl -X POST http://localhost:5016/api/ndibridge/join/port \
  -H "Content-Type: application/json" \
  -d '{"port":5990}'

# Set groups
curl -X POST http://localhost:5016/api/ndibridge/join/groups \
  -H "Content-Type: application/json" \
  -d '{"groups":"public,studio1"}'

# Start Join
curl -X POST http://localhost:5016/api/ndibridge/join/start \
  -H "Content-Type: application/json" \
  -d '{}'
```

### Get Status

```bash
# Get running status
curl -X GET http://localhost:5016/api/ndibridge/status

# Get current mode
curl -X GET http://localhost:5016/api/ndibridge/runmode
```

### Stop Mode

```bash
# Stop Host
curl -X POST http://localhost:5016/api/ndibridge/host/stop \
  -H "Content-Type: application/json" \
  -d '{}'

# Stop Join
curl -X POST http://localhost:5016/api/ndibridge/join/stop \
  -H "Content-Type: application/json" \
  -d '{}'

# Stop Local
curl -X POST http://localhost:5016/api/ndibridge/local/stop \
  -H "Content-Type: application/json" \
  -d '{}'
```

## Response Format

All API endpoints return JSON responses in the following formats:

### Success Response

```json
{
  "success": true,
  "message": "Host started",
  "connected": true
}
```

### Error Response

```json
{
  "success": false,
  "error": "Connection failed",
  "connected": false
}
```

### Status Response

```json
{
  "isRunning": true,
  "connected": true
}
```

### Mode Response

```json
{
  "mode": "HOST",
  "connected": true
}
```
