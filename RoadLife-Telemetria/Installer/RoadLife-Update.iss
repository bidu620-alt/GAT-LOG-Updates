#define AppVersion "1.0.68.17"

[Setup]
AppId=RoadLifeTelemetryUpdate
AppName=RoadLife Telemetria - Atualizacao
AppVersion={#AppVersion}
AppPublisher=RoadLife
DefaultDirName={code:ExistingAppDir}
CreateAppDir=yes
DisableProgramGroupPage=yes
Uninstallable=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
OutputDir=Output
OutputBaseFilename=ROADLIFE_TELEMETRIA_UPDATE_{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\app.ico
DisableReadyPage=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "payload\GAT_TELEMETRIA_APP.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\GAT_TELEMETRIA_APP.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\GAT_TELEMETRIA.exe"; Description: "Abrir RoadLife Telemetria"; Flags: nowait postinstall skipifsilent

[Code]
function ValidInstallation(Dir: String): Boolean;
begin
  Result := FileExists(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe') and
    FileExists(AddBackslash(Dir) + 'RoadLifeDash\RoadLifeDash.xaml');
end;

function ExistingAppDir(Param: String): String;
var
  Dir: String;
begin
  Dir := '';
  RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}_is1', 'Inno Setup: App Path', Dir);
  if ValidInstallation(Dir) then begin Result := Dir; Exit; end;
  RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}_is1', 'Inno Setup: App Path', Dir);
  if ValidInstallation(Dir) then begin Result := Dir; Exit; end;
  Dir := ExpandConstant('{autopf}\ROADLIFE TELEMETRIA');
  if ValidInstallation(Dir) then begin Result := Dir; Exit; end;
  Dir := ExpandConstant('{autopf}\GAT Telemetria');
  if ValidInstallation(Dir) then begin Result := Dir; Exit; end;
  Result := ExpandConstant('{autopf}\ROADLIFE TELEMETRIA');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Dir, Backup: String;
begin
  Result := '';
  Dir := ExpandConstant('{app}');
  Log('RoadLife update target: ' + Dir);
  if not ValidInstallation(Dir) then begin
    Result := 'Selecione a pasta da RoadLife Telemetria V5 instalada. Este pacote atualiza uma instalacao existente e preserva o Dashboard2 e os dados.';
    Exit;
  end;
  Backup := AddBackslash(Dir) + 'backup-update-{#AppVersion}-' + GetDateTimeString('yyyymmdd-hhnnss-zzz', '-', ':');
  if DirExists(Backup) then begin Result := 'Ja existe um backup com este nome. Tente novamente.'; Exit; end;
  if not ForceDirectories(Backup) then begin Result := 'Nao foi possivel criar o backup.'; Exit; end;
  if not FileCopy(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe', AddBackslash(Backup) + 'GAT_TELEMETRIA_APP.exe', False) then begin
    Result := 'Nao foi possivel salvar o aplicativo anterior. Feche a RoadLife e tente novamente.';
    Exit;
  end;
  if FileExists(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe.config') then
    if not FileCopy(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe.config', AddBackslash(Backup) + 'GAT_TELEMETRIA_APP.exe.config', False) then
      Result := 'Nao foi possivel salvar a configuracao anterior.';
end;
