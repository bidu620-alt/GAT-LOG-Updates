#define MyAppName "RoadLife ETS2 Dashboard2 TESTE"
#define MyAppVersion "0.1.0"
#define MyAppExeName "ROADLIFE_DASHBOARD2_TESTE.exe"

[Setup]
AppId={{7D2E815E-1DD3-4DD5-86FA-8C1110C05355}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\RoadLife ETS2\Dashboard2 Teste
DefaultGroupName=RoadLife ETS2
OutputDir=Output
OutputBaseFilename=ROADLIFE_DASHBOARD2_TESTE_SETUP_0.1.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RoadLife Dashboard2 TESTE"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\RoadLife Dashboard2 TESTE"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Dashboard2 TESTE"; Flags: nowait postinstall skipifsilent
