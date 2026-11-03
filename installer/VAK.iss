; Inno Setup script for VAK. Build with:  powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
; It packs the folder made by tools\publish.ps1 into dist\VAK-Setup-<version>.exe (installs per user, no admin rights needed).
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6C1F8E0B-5D52-4D0E-9C61-7A4B6A0C1A11}
AppName=VAK
AppVersion={#AppVersion}
AppPublisher=Kartik Pagariya
AppPublisherURL=https://github.com/kartikpagariya25/VAK
DefaultDirName={localappdata}\Programs\VAK
DefaultGroupName=VAK
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=VAK-Setup-{#AppVersion}
SetupIconFile=..\src\VAK.App\Assets\Brand\vak.ico
UninstallDisplayIcon={app}\VAK.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start VAK when I sign in to Windows"; Flags: checkedonce

[Files]
Source: "..\dist\VAK-{#AppVersion}-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VAK"; Filename: "{app}\VAK.exe"
Name: "{autodesktop}\VAK"; Filename: "{app}\VAK.exe"; Tasks: desktopicon

[Registry]
; the same per-user Run entry VAK's own "Start with Windows" switch uses
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VAK"; ValueData: """{app}\VAK.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\VAK.exe"; Description: "Start VAK now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM VAK.exe /F"; Flags: runhidden; RunOnceId: "StopVAK"
