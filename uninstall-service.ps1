# Uninstall NDI Intercom16
# Run as Administrator!

param(
    [string]$InstallPath = "C:\Program Files\NDI\NDI Intercom16",
    [switch]$KeepConfig = $false
)

# Check for Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "NDI Intercom16 - Uninstaller" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Stop running application if present
Write-Host "Stopping NDI Intercom16 if running..." -ForegroundColor Yellow
$processes = Get-Process -Name "NDI Intercom16" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "  ✓ Application stopped" -ForegroundColor Green
} else {
    Write-Host "  - Application not running" -ForegroundColor Gray
}

# Remove legacy Windows Service if it exists (from previous versions)
$serviceName = "NDI Intercom16"
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host ""
    Write-Host "Removing legacy Windows Service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "  ✓ Legacy service removed" -ForegroundColor Green
}

# Remove firewall rule
Write-Host ""
Write-Host "Removing firewall rules..." -ForegroundColor Yellow
$ruleName = "NDI Intercom16 Web Server"
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($rule) {
    Remove-NetFirewallRule -DisplayName $ruleName
    Write-Host "  ✓ Firewall rule removed" -ForegroundColor Green
} else {
    Write-Host "  - No firewall rule found" -ForegroundColor Gray
}

# Remove installation directory
Write-Host ""
if (Test-Path $InstallPath) {
    Write-Host "Removing installation directory..." -ForegroundColor Yellow
    Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Installation directory removed" -ForegroundColor Green
} else {
    Write-Host "Installation directory not found" -ForegroundColor Gray
}

# Remove config (optional)
$appDataPath = "$env:PROGRAMDATA\NDI Intercom16"
if (Test-Path $appDataPath) {
    if ($KeepConfig) {
        Write-Host ""
        Write-Host "Configuration preserved at: $appDataPath" -ForegroundColor Yellow
    } else {
        Write-Host ""
        $removeConfig = Read-Host "Remove configuration files? (Y/n)"
        if ($removeConfig -ne "n" -and $removeConfig -ne "N") {
            Remove-Item -Path $appDataPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Configuration removed" -ForegroundColor Green
        } else {
            Write-Host "  - Configuration preserved at: $appDataPath" -ForegroundColor Yellow
        }
    }
}

# Remove desktop shortcut
Write-Host ""
Write-Host "Removing shortcuts..." -ForegroundColor Yellow
$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcutPath = "$desktopPath\NDI Intercom16.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "  ✓ Desktop shortcut removed" -ForegroundColor Green
}

# Final status
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "UNINSTALLATION COMPLETED!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "NDI Intercom16 has been removed from your system." -ForegroundColor White
Write-Host ""

pause
