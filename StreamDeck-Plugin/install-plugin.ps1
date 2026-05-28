# Install NDI Intercom Stream Deck Plugin

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "NDI Intercom Plugin Installer" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Get paths
$pluginSource = "C:\TEMP\NDI_INTERCOM\StreamDeck-Plugin\com.ndi.intercom.sdPlugin"
$pluginDest = "$env:APPDATA\Elgato\StreamDeck\Plugins\com.ndi.intercom.sdPlugin"

Write-Host "Source: $pluginSource" -ForegroundColor Yellow
Write-Host "Destination: $pluginDest" -ForegroundColor Yellow
Write-Host ""

# Check if Stream Deck is installed
if (!(Test-Path "$env:APPDATA\Elgato\StreamDeck")) {
    Write-Host "ERROR: Stream Deck software not found!" -ForegroundColor Red
    Write-Host "Please install Stream Deck software first." -ForegroundColor Red
    Write-Host "Download from: https://www.elgato.com/downloads" -ForegroundColor Yellow
    pause
    exit 1
}

# Create Plugins folder if it doesn't exist
$pluginsFolder = "$env:APPDATA\Elgato\StreamDeck\Plugins"
if (!(Test-Path $pluginsFolder)) {
    Write-Host "Creating Plugins folder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $pluginsFolder -Force | Out-Null
}

# Check if plugin source exists
if (!(Test-Path $pluginSource)) {
    Write-Host "ERROR: Plugin source not found at $pluginSource" -ForegroundColor Red
    pause
    exit 1
}

# Remove old version if exists
if (Test-Path $pluginDest) {
    Write-Host "Removing old version..." -ForegroundColor Yellow
    Remove-Item -Path $pluginDest -Recurse -Force
}

# Copy plugin
Write-Host "Installing plugin..." -ForegroundColor Green
Copy-Item -Path $pluginSource -Destination $pluginDest -Recurse -Force

# Verify installation
if (Test-Path $pluginDest) {
    Write-Host ""
    Write-Host "SUCCESS! Plugin installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Close Stream Deck software completely (right-click tray icon -> Quit)" -ForegroundColor White
    Write-Host "2. Restart Stream Deck software" -ForegroundColor White
    Write-Host "3. Look for 'NDI Intercom' in the actions list" -ForegroundColor White
    Write-Host ""
    Write-Host "Plugin location: $pluginDest" -ForegroundColor Gray
} else {
    Write-Host "ERROR: Plugin installation failed!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
