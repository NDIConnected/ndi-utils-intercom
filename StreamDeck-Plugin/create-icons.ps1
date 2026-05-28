# Create placeholder icons for Stream Deck plugin
Add-Type -AssemblyName System.Drawing

$pluginPath = "C:\TEMP\NDI_INTERCOM\StreamDeck-Plugin\com.ndi.intercom.sdPlugin\images"

# Create key.png (72x72 - black)
$bmp = New-Object System.Drawing.Bitmap(72,72)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Black)
$bmp.Save("$pluginPath\key.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$g.Dispose()

# Create plugin.png (28x28 - blue)
$bmp = New-Object System.Drawing.Bitmap(28,28)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(0,112,243))
$bmp.Save("$pluginPath\plugin.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$g.Dispose()

# Create category.png (28x28 - blue)
$bmp = New-Object System.Drawing.Bitmap(28,28)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(0,112,243))
$bmp.Save("$pluginPath\category.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$g.Dispose()

# Create action.png (20x20 - blue)
$bmp = New-Object System.Drawing.Bitmap(20,20)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(0,112,243))
$bmp.Save("$pluginPath\action.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$g.Dispose()

Write-Host "Icons created successfully!" -ForegroundColor Green
