; NDI Intercom16 Installer Script v1.7.4
; Inno Setup 6.x required
; Run build-intercom16-installer.cmd to publish and compile, or publish to .\publish first.

#define MyAppName "NDI Intercom16"
#define MyAppVersion "1.7.4"
#define MyAppPublisher "NDI"
#define MyAppURL "https://ndi.video"
#define MyAppExeName "NDI Intercom16.exe"
#define SourcePath "."
#define PublishPath ".\publish"

[Setup]
AppId={{7A5F2B9D-8E3C-4D1F-9B6A-2C8E4F7D1A3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\NDI\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile={#SourcePath}\LICENSE
OutputDir={#SourcePath}\Installer-Output
OutputBaseFilename=NDI-Intercom16-Setup-v{#MyAppVersion}
SetupIconFile={#SourcePath}\COM_icon_windows.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
InfoBeforeFile={#SourcePath}\INSTALLER-NOTICE.txt
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoCopyright=Copyright (C) 2026 Vizrt NDI AB
DisableProgramGroupPage=yes
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "streamdeckplugin"; Description: "Install Stream Deck Plugin"; GroupDescription: "Optional Components:"; Flags: unchecked

[Files]
Source: "{#PublishPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\COM_icon_windows.ico"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion
Source: "{#SourcePath}\wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\INSTALLER-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\THIRD-PARTY-LICENSES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\DOCS\INTERCOM USER_MANUAL.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#SourcePath}\DOCS\API_REST_GUIDE.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#SourcePath}\DOCS\STREAMDECK_PLUGIN_GUIDE.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#SourcePath}\DOCS\NDI-BRIDGE-INTEGRATION.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#SourcePath}\DOCS\NDI-BRIDGE-QUICK-START.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "{#SourcePath}\StreamDeck-Plugin\*"; DestDir: "{app}\StreamDeck-Plugin"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: streamdeckplugin
Source: "{#SourcePath}\install-streamdeck-plugin.ps1"; DestDir: "{app}"; Flags: ignoreversion; Tasks: streamdeckplugin

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Comment: "Launch NDI Intercom16"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Comment: "Launch NDI Intercom16"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\install-streamdeck-plugin.ps1"" -SourcePath ""{app}"""; Flags: runhidden; Tasks: streamdeckplugin; Description: "Installing Stream Deck Plugin..."
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{%APPDATA}\Elgato\StreamDeck\Plugins\com.ndi.intercom16.sdPlugin"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nNDI Intercom16 is a professional 16-channel intercom system using NDI technology for network audio communication.%n%nVersion 1.7.4 highlights:%n%n- Security: web UI defaults to localhost; optional remote control on a selected network interface (tray Web Server Settings)%n%n- FIX in 1.7.3: Settings page no longer leaves an empty band when sections have very different heights (CSS multicol layout)%n- FIX in 1.7.2: incoming NDI stereo audio now decoded correctly (FLTP planar). Previously, stereo flows played one octave higher and metallic - mono flows were unaffected.%n- 1.7.1: selectable NDI source name suffix mode (Full / Compact / Off) for legacy receiver compatibility - default Compact%n- 24/7 stability hardening: NDI native-handle race fixes, watchdog auto-restart of WASAPI/ASIO devices, atomic config save, persistent rolling log file%n- Identity-aware NDI routing (application_id / device_id) for management apps%n- Unified Intercom Groups: NDI and ASIO channels in the same group%n- Cross-mode N-1 mixing between NDI and ASIO%n- NDI Bridge Service integration (Host/Join/Local)%n- System tray application with port configuration%n- 16 full-duplex channels with ultra-low latency%n- ASIO support%n- Web-based control interface%n- Optional Stream Deck plugin%n%nThe application is fully self-contained - no additional software required.

FinishedHeadingLabel=Setup Completed Successfully!
FinishedLabel=[name] has been installed on your computer.%n%nThe application runs in the system tray (notification area near the clock).%n%nGetting started:%n- Launch from desktop shortcut or Start Menu%n- Right-click the tray icon to open the web interface%n- Local access: http://127.0.0.1:5016%n- Web Server Settings (tray): port and optional remote control on a selected network interface%n%nFor ASIO routing, configure your ASIO device in Settings.%nFor NDI Bridge, configure Host/Join/Local modes in Settings.%n%nSee INSTALLER-NOTICE.txt and THIRD-PARTY-LICENSES.txt for NDI SDK and trademark terms.

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "NDI Intercom16"; ValueData: "v2.26|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=5016|App={app}\{#MyAppExeName}|Name=NDI Intercom16|"; Flags: uninsdeletevalue
