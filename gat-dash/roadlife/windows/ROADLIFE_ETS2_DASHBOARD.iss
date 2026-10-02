#define MyAppName "RoadLife ETS2 Dashboard"
#define MyAppVersion "0.1.1"
#define MyAppExeName "ROADLIFE_ETS2_DASHBOARD.exe"

[Setup]
AppId={{B9073A20-5331-4AD6-98CD-562AE5E54764}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\RoadLife ETS2\RoadLife Dashboard
DefaultGroupName=RoadLife ETS2
OutputDir=Output
OutputBaseFilename=ROADLIFE_ETS2_DASHBOARD_SETUP_0.1.1
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RoadLife ETS2 Dashboard"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\RoadLife ETS2 Dashboard"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir RoadLife ETS2 Dashboard"; Flags: nowait postinstall skipifsilent
