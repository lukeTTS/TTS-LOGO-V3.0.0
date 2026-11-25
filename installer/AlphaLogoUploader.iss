#define AppName "TTS Printer Logo Uploader"
#define AppVersion "3.0.0"
#define AppPublisher "Test & Tag Supplies"
#define AppExeName "AlphaLogoUploader.exe"

[Setup]
AppId={{5170E7A2-315B-4BB6-915F-4AE80E527D54}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Test & Tag Supplies\Alpha Logo Uploader
DefaultGroupName={#AppPublisher}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=AlphaLogoUploader-{#AppVersion}-Setup
SetupIconFile=..\AlphaLogoUploader\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\AlphaLogoUploader.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppPublisher}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
