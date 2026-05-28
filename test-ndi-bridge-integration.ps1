# NDI Bridge Integration Test Script
# This script tests the NDI Bridge API integration

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "NDI Bridge Integration Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5016"
$testsPassed = 0
$testsFailed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [object]$Body = $null
    )
    
    try {
        Write-Host "Testing: $Name..." -NoNewline
        
        $params = @{
            Uri = "$baseUrl$Url"
            Method = $Method
            ContentType = "application/json"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json)
        }
        
        $response = Invoke-RestMethod @params -ErrorAction Stop
        
        Write-Host " PASS" -ForegroundColor Green
        $script:testsPassed++
        return $response
    }
    catch {
        Write-Host " FAIL" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $script:testsFailed++
        return $null
    }
}

# Test 1: Connection to NDI Intercom16
Write-Host "`n1. Testing NDI Intercom16 API..." -ForegroundColor Yellow
$systemStatus = Test-Endpoint -Name "System Status" -Url "/api/system/status"

if ($systemStatus) {
    Write-Host "  Version: $($systemStatus.version)" -ForegroundColor Gray
    Write-Host "  Running: $($systemStatus.running)" -ForegroundColor Gray
}

# Test 2: NDI Bridge Connection Test
Write-Host "`n2. Testing NDI Bridge Connection..." -ForegroundColor Yellow
$bridgeTest = Test-Endpoint -Name "Bridge Connection Test" -Url "/api/ndibridge/test"

if ($bridgeTest) {
    Write-Host "  Connected: $($bridgeTest.connected)" -ForegroundColor Gray
    
    if ($bridgeTest.connected) {
        Write-Host "  Message: $($bridgeTest.message)" -ForegroundColor Gray
    }
    else {
        Write-Host "`n  WARNING: NDI Bridge Service is not connected!" -ForegroundColor Yellow
        Write-Host "  Make sure NDI Bridge Service is installed and running." -ForegroundColor Yellow
        Write-Host "  You can still test the endpoints, but they will fail." -ForegroundColor Yellow
    }
}

# Test 3: Get Bridge Status
Write-Host "`n3. Testing Bridge Status Endpoints..." -ForegroundColor Yellow
$bridgeStatus = Test-Endpoint -Name "Bridge Status" -Url "/api/ndibridge/status"

if ($bridgeStatus) {
    Write-Host "  Is Running: $($bridgeStatus.isRunning)" -ForegroundColor Gray
}

$bridgeMode = Test-Endpoint -Name "Bridge Run Mode" -Url "/api/ndibridge/runmode"

if ($bridgeMode) {
    Write-Host "  Current Mode: $($bridgeMode.mode)" -ForegroundColor Gray
}

# Test 4: Host Mode Endpoints
Write-Host "`n4. Testing Host Mode Endpoints..." -ForegroundColor Yellow
$hostGroups = Test-Endpoint -Name "Get Host Groups" -Url "/api/ndibridge/host/groups"
if ($hostGroups) {
    Write-Host "  Groups: $($hostGroups.groups)" -ForegroundColor Gray
}

$hostPort = Test-Endpoint -Name "Get Host Port" -Url "/api/ndibridge/host/port"
if ($hostPort) {
    Write-Host "  Port: $($hostPort.port)" -ForegroundColor Gray
}

# Test 5: Join Mode Endpoints
Write-Host "`n5. Testing Join Mode Endpoints..." -ForegroundColor Yellow
$joinIp = Test-Endpoint -Name "Get Join IP" -Url "/api/ndibridge/join/ip"
if ($joinIp) {
    Write-Host "  IP: $($joinIp.ip)" -ForegroundColor Gray
}

$joinPort = Test-Endpoint -Name "Get Join Port" -Url "/api/ndibridge/join/port"
if ($joinPort) {
    Write-Host "  Port: $($joinPort.port)" -ForegroundColor Gray
}

$joinGroups = Test-Endpoint -Name "Get Join Groups" -Url "/api/ndibridge/join/groups"
if ($joinGroups) {
    Write-Host "  Groups: $($joinGroups.groups)" -ForegroundColor Gray
}

# Test 6: Local Mode Endpoints
Write-Host "`n6. Testing Local Mode Endpoints..." -ForegroundColor Yellow
$localGroups = Test-Endpoint -Name "Get Local Groups" -Url "/api/ndibridge/local/groups"
if ($localGroups) {
    Write-Host "  Groups: $($localGroups.groups)" -ForegroundColor Gray
}

# Test 7: Configuration Endpoints (if Bridge is connected)
if ($bridgeTest.connected -eq $true) {
    Write-Host "`n7. Testing Configuration (Write Operations)..." -ForegroundColor Yellow
    
    # Test setting Host groups
    $setGroups = Test-Endpoint -Name "Set Host Groups" -Url "/api/ndibridge/host/groups" -Method "POST" -Body @{ groups = "test-group" }
    
    if ($setGroups.success) {
        Write-Host "  Successfully set Host groups to: test-group" -ForegroundColor Gray
        
        # Verify
        $verifyGroups = Test-Endpoint -Name "Verify Host Groups" -Url "/api/ndibridge/host/groups"
        if ($verifyGroups.groups -eq "test-group") {
            Write-Host "  Verified: Groups match!" -ForegroundColor Green
        }
        
        # Reset to empty
        Test-Endpoint -Name "Reset Host Groups" -Url "/api/ndibridge/host/groups" -Method "POST" -Body @{ groups = "" } | Out-Null
    }
}
else {
    Write-Host "`n7. Skipping Write Operations (Bridge not connected)" -ForegroundColor Yellow
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Passed: $testsPassed" -ForegroundColor Green
Write-Host "Failed: $testsFailed" -ForegroundColor Red
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "All tests passed! ✓" -ForegroundColor Green
}
else {
    Write-Host "Some tests failed. Check the output above." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Notes:" -ForegroundColor Cyan
Write-Host "- NDI Intercom16 must be running on port 5016" -ForegroundColor Gray
Write-Host "- NDI Bridge Service must be running on port 8080 for full functionality" -ForegroundColor Gray
Write-Host "- To test start/stop operations, use the web interface at http://localhost:5016/settings.html" -ForegroundColor Gray
Write-Host ""
