#define AppVersion "1.0.68.35"
[Setup]
AppId=BiduTruckMemoryTrial135
AppName=BiduTruck Telemetria - Memoria de Viagens (TESTE)
AppVersion={#AppVersion}
AppPublisher=BiduTruck
DefaultDirName={code:ExistingAppDir}
CreateAppDir=yes
DisableProgramGroupPage=yes
Uninstallable=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=GAT_TELEMETRIA_CSHARP_SINGLE_INSTANCE
OutputDir=Output
OutputBaseFilename=BIDUTRUCK_TELEMETRIA_MEMORIA_1.0.68.35_TESTE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableReadyPage=yes
SetupIconFile=..\src\app.ico

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "payload\GAT_TELEMETRIA_APP.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "payload\GAT_TELEMETRIA_APP.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Code]
function ValidApp(Dir: String): Boolean;
begin
  Result := FileExists(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe');
end;

function ExistingAppDir(Param: String): String;
var Dir: String;
begin
  Dir := '';
  RegQueryStringValue(HKLM64,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}_is1',
    'Inno Setup: App Path', Dir);
  if ValidApp(Dir) then begin Result := Dir; Exit; end;
  RegQueryStringValue(HKLM32,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}_is1',
    'Inno Setup: App Path', Dir);
  if ValidApp(Dir) then begin Result := Dir; Exit; end;
  Dir := ExpandConstant('{autopf}\BiduTruck Telemetria');
  if ValidApp(Dir) then begin Result := Dir; Exit; end;
  Dir := ExpandConstant('{autopf}\ROADLIFE TELEMETRIA');
  if ValidApp(Dir) then begin Result := Dir; Exit; end;
  Dir := ExpandConstant('{autopf}\GAT Telemetria');
  if ValidApp(Dir) then begin Result := Dir; Exit; end;
  Result := ExpandConstant('{autopf}\BiduTruck Telemetria');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Dir, Backup: String;
begin
  Result := '';
  Dir := ExpandConstant('{app}');
  if not ValidApp(Dir) then begin
    Result := 'Selecione uma pasta que contenha a Telemetria 1.0.68.34 instalada.';
    Exit;
  end;
  Backup := AddBackslash(Dir) + 'backup-teste-memoria-{#AppVersion}-' +
    GetDateTimeString('yyyymmdd-hhnnss', '-', ':');
  if not ForceDirectories(Backup) then begin
    Result := 'Falha ao preparar backup dos arquivos atuais.';
    Exit;
  end;
  if not FileCopy(AddBackslash(Dir)+'GAT_TELEMETRIA_APP.exe',
      AddBackslash(Backup)+'GAT_TELEMETRIA_APP.exe', False) then begin
    Result := 'Nao foi possivel fazer backup. Feche a Telemetria.';
    Exit;
  end;
  if FileExists(AddBackslash(Dir)+'GAT_TELEMETRIA_APP.exe.config') then
    if not FileCopy(AddBackslash(Dir)+'GAT_TELEMETRIA_APP.exe.config',
      AddBackslash(Backup)+'GAT_TELEMETRIA_APP.exe.config', False) then begin
      Result := 'Nao foi possivel fazer backup da configuracao.';
      Exit;
    end;
  SaveStringToFile(AddBackslash(Backup)+'LEIA-ME.txt',
    'Teste 1.0.68.35. Restaurar os executaveis da pasta backup. Nao altera dados em AppData nem TruckSim GPS.', False);
end;
