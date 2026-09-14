#define MyAppName "GAT DASH"
#define MyAppVersion "1.1.0"
#define MyAppExeName "GAT_DASH.exe"
[Setup]
AppId={{8A7D57F2-6B9A-4D0E-8E5D-14F6520DB251}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\GAT-LOG\GAT DASH
DefaultGroupName=GAT LOG
OutputDir=Output
OutputBaseFilename=GAT_DASH_PC_SETUP_1.1.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
[Files]
Source: "bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\GAT DASH"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\GAT DASH"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir GAT DASH"; Flags: nowait postinstall skipifsilent
