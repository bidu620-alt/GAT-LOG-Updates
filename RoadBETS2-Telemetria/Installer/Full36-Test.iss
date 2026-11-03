#define AppVersion "1.0.68.36"
[Setup]
AppId={code:InstallerIdentity}
UsePreviousLanguage=no
AppName=BiduTruck Telemetria
AppVersion={#AppVersion}
AppPublisher=BiduTruck
DefaultDirName={code:InstallFolder}
DefaultGroupName=BiduTruck Telemetria
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=GAT_TELEMETRIA_CSHARP_SINGLE_INSTANCE
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\GAT_TELEMETRIA.exe
#ifdef TestPackage
Uninstallable=no
OutputDir=.
OutputBaseFilename=BIDUTRUCK_FULL35_VALIDACAO_ISOLADA
#else
OutputDir=Output
OutputBaseFilename=BIDUTRUCK_TELEMETRIA_COMPLETO_1.0.68.36_TESTE
#endif
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=payload\ROADLIFE_TELEMETRIA.ico
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "managed-files.txt"; Flags: dontcopy
[Icons]
Name: "{group}\BiduTruck Telemetria"; Filename: "{app}\GAT_TELEMETRIA.exe"; Check: NotIsolated
Name: "{autodesktop}\BiduTruck Telemetria"; Filename: "{app}\GAT_TELEMETRIA.exe"; Check: NotIsolated
[Run]
Filename: "{app}\GAT_TELEMETRIA.exe"; Description: "Abrir BiduTruck Telemetria"; Flags: nowait postinstall skipifsilent; Check: NotIsolated
[Code]
var BackupDir: String;
function Isolated: Boolean;
begin
  Result := ExpandConstant('{param:ISOLATEDTEST|0}') = '1';
end;
function NotIsolated: Boolean;
begin
  Result := not Isolated;
end;
function InstallerIdentity(Param: String): String;
begin
  if Isolated then Result := 'BiduTruckFull36IsolatedTest'
  else Result := '{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}';
end;
function InstallFolder(Param: String): String;
var Dir: String;
begin
  Dir := '';
  if not Isolated then RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}_is1', 'Inno Setup: App Path', Dir);
  if (Dir <> '') and FileExists(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe') then Result := Dir
  else Result := ExpandConstant('{autopf}\BiduTruck Telemetria');
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Dir, Existing, Destination, Relative: String; Paths: TArrayOfString; I: Integer; Release: Cardinal;
begin
  Result := ''; Dir := ExpandConstant('{app}');
  if not Isolated then begin
    if not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) or (Release < 528040) then begin
      Result := 'Instale ou ative o Microsoft .NET Framework 4.8 antes de continuar: https://dotnet.microsoft.com/download/dotnet-framework/net48'; Exit;
    end;
  end;
  { Complete install: no minimum telemetry version, no deletion of AppData or journal. }
  if not FileExists(AddBackslash(Dir) + 'GAT_TELEMETRIA_APP.exe') then Exit;
  ExtractTemporaryFile('managed-files.txt');
  if not LoadStringsFromFile(ExpandConstant('{tmp}\managed-files.txt'), Paths) then begin Result := 'Falha ao conferir lista de arquivos.'; Exit; end;
  BackupDir := AddBackslash(Dir) + 'backup-completa-1.0.68.36-' + GetDateTimeString('yyyymmdd-hhnnss-zzz', '-', ':');
  if DirExists(BackupDir) or not ForceDirectories(BackupDir) then begin Result := 'Nao foi possivel criar backup exclusivo.'; Exit; end;
  for I := 0 to GetArrayLength(Paths)-1 do begin
    Relative := Paths[I]; Existing := AddBackslash(Dir) + Relative;
    if FileExists(Existing) then begin
      Destination := AddBackslash(BackupDir) + Relative;
      if not ForceDirectories(ExtractFileDir(Destination)) or not FileCopy(Existing, Destination, True) then begin Result := 'Falha no backup. Feche o aplicativo e tente novamente.'; Exit; end;
      if GetSHA256OfFile(Existing) <> GetSHA256OfFile(Destination) then begin Result := 'Backup nao confere. Instalacao interrompida.'; Exit; end;
    end;
  end;
  SaveStringToFile(AddBackslash(BackupDir)+'LEIA-ME.txt','Backup dos arquivos de programa substituidos pela instalacao completa. Perfil, credenciais, saves, fila e diario em AppData nao foram alterados.',False);
end;

