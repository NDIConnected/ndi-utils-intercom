; NDI Intercom2 (2-channel) - Inno Setup 6
; Run build-intercom2-installer.cmd to publish and compile, or publish to publish-intercom2 first.

[Setup]
AppId={{C4D5E6F7-A8B9-0123-CDEF-123456789ABC}
AppName=NDI Intercom2
AppVersion=1.8.2
AppPublisher=NDI
AppPublisherURL=https://ndi.video
AppSupportURL=https://ndi.video
AppUpdatesURL=https://ndi.video
DefaultDirName={autopf}\NDI\NDI Intercom2
DefaultGroupName=NDI Intercom2
LicenseFile=LICENSE
OutputDir=Installer-Output
OutputBaseFilename=NDI-Intercom2-Setup-v1.8.2
SetupIconFile=COM_icon_windows.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
InfoBeforeFile=INSTALLER-NOTICE.txt
UninstallDisplayIcon={app}\NDI Intercom2.exe
UninstallDisplayName=NDI Intercom2 v1.8.2
VersionInfoVersion=1.8.2
VersionInfoCompany=NDI
VersionInfoDescription=NDI Intercom2 Installer
VersionInfoCopyright=Copyright (C) 2026 Vizrt NDI AB
DisableProgramGroupPage=yes
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish-intercom2\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "COM_icon_windows.ico"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion
Source: "wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "INSTALLER-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-LICENSES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "Processing.NDI.Lib.Licenses.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "DOCS\INTERCOM USER_MANUAL.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "DOCS\API_REST_GUIDE.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "DOCS\NDI-BRIDGE-INTEGRATION.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "DOCS\NDI-BRIDGE-QUICK-START.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion

[Icons]
Name: "{group}\NDI Intercom2"; Filename: "{app}\NDI Intercom2.exe"; IconFilename: "{app}\app.ico"; Comment: "Launch NDI Intercom2"
Name: "{group}\{cm:UninstallProgram,NDI Intercom2}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\NDI Intercom2"; Filename: "{app}\NDI Intercom2.exe"; IconFilename: "{app}\app.ico"; Comment: "Launch NDI Intercom2"; Tasks: desktopicon

[Run]
Filename: "{app}\NDI Intercom2.exe"; Description: "Launch NDI Intercom2"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nNDI Intercom2 is a compact 2-channel professional intercom using NDI for network audio.%n%nIt includes:%n%n- Two full-duplex channels (NDI and optional ASIO)%n- Self-contained .NET 8 runtime (no separate runtime install)%n- Web control interface (default port 5017)%n- Separate settings from NDI Intercom16 (%ProgramData%\NDI Intercom2)%n%nNDI Runtime must be installed separately (NDI Tools).%n%nThe Stream Deck plugin for 16-channel mode is not included with this product.

FinishedHeadingLabel=Setup Completed Successfully!
FinishedLabel=[name] has been installed on your computer.%n%nThe application runs in the system tray.%n%nGetting started:%n- Open the web UI from the tray icon or browser: http://127.0.0.1:5017%n- Web Server Settings (tray): port and optional remote control on a selected network interface%n%nConfiguration is stored under:%n%ProgramData%\NDI Intercom2\%n%nSee INSTALLER-NOTICE.txt, THIRD-PARTY-LICENSES.txt, and Processing.NDI.Lib.Licenses.txt for NDI SDK and trademark terms.

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "NDI Intercom2 Web"; ValueData: "v2.26|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=5017|App={app}\NDI Intercom2.exe|Name=NDI Intercom2|"; Flags: uninsdeletevalue
