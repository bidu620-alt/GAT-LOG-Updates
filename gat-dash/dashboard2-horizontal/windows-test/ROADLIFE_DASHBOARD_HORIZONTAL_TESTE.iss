#define MyAppName "RoadLife ETS2 Dashboard Horizontal TESTE"
#define MyAppVersion "0.5.0"
#define MyAppExeName "ROADLIFE_DASHBOARD_HORIZONTAL_TESTE.exe"

[Setup]
AppId={{D802A2B6-6F34-47D8-88F8-524C6A5905A5}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\RoadLife ETS2\Dashboard Horizontal Teste
DefaultGroupName=RoadLife ETS2
OutputDir=Output
OutputBaseFilename=ROADLIFE_DASHBOARD_HORIZONTAL_TESTE_SETUP_0.5.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RoadLife Dashboard Horizontal TESTE"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\RoadLife Dashboard Horizontal TESTE"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Dashboard Horizontal TESTE"; Flags: nowait postinstall skipifsilent
