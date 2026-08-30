; NDI Intercom16 Installer Script v1.7.7
; Inno Setup 6.x required
; Run build-intercom16-installer.cmd to publish and compile, or publish to .\publish first.

#define MyAppName "NDI Intercom16"
#define MyAppVersion "1.7.7"
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
Source: "{#SourcePath}\Processing.NDI.Lib.Licenses.txt"; DestDir: "{app}"; Flags: ignoreversion
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
WelcomeLabel2=This will install [name/ver] on your computer.%n%nNDI Intercom16 is a 16-channel NDI intercom with web control, optional ASIO, and NDI Bridge integration.%n%nVersion 1.7.7:%n- Input Level knob: center = unity, left = mute, right = up to 3x boost%n%nSee CHANGELOG.md in the repo or Documentation after install for full release notes.%n%nSelf-contained .NET 8 app — NDI Runtime must be installed separately.

FinishedHeadingLabel=Setup Completed Successfully!
FinishedLabel=[name] is installed and runs in the system tray.%n%nRight-click the tray icon to open the web UI.%n%nLocal access: http://127.0.0.1:5016%nRemote control: tray → Web Server Settings%n%nConfiguration: %ProgramData%\NDI Intercom16\%nDocumentation and licenses (including Processing.NDI.Lib.Licenses.txt) are in the install folder.

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "NDI Intercom16"; ValueData: "v2.26|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=5016|App={app}\{#MyAppExeName}|Name=NDI Intercom16|"; Flags: uninsdeletevalue
