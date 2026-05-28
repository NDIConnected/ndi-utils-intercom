# Install Stream Deck Plugin Script
# This script copies the NDI Intercom16 Stream Deck plugin to the user's Stream Deck plugins directory

param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath
)

try {
    # Get the AppData Roaming folder
    $appDataPath = [Environment]::GetFolderPath('ApplicationData')

    # Build the destination path
    $destinationPath = Join-Path $appDataPath "Elgato\StreamDeck\Plugins"

    # Create the destination directory if it doesn't exist
    if (-not (Test-Path $destinationPath)) {
        New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
        Write-Host "Created Stream Deck Plugins directory: $destinationPath"
    }

    # Copy the plugin
    $pluginSource = Join-Path $SourcePath "StreamDeck-Plugin\com.ndi.intercom16.sdPlugin"

    if (Test-Path $pluginSource) {
        Copy-Item -Path $pluginSource -Destination $destinationPath -Recurse -Force
        Write-Host "Stream Deck plugin installed successfully to: $destinationPath"
        exit 0
    } else {
        Write-Error "Plugin source not found: $pluginSource"
        exit 1
    }
} catch {
    Write-Error "Failed to install Stream Deck plugin: $_"
    exit 1
}
