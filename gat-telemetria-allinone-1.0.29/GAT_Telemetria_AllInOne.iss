#define AppVersion "1.0.29"

[Setup]
AppId={{C82013A7-2F6D-46B4-9B3D-7CC8EA349029}
AppName=GAT Telemetria
AppVersion={#AppVersion}
AppPublisher=GAT-LOG
DefaultDirName={autopf}\GAT Telemetria
DefaultGroupName=GAT Telemetria
PrivilegesRequired=admin
UsedUserAreasWarning=no
OutputDir=Output
OutputBaseFilename=GAT_TELEMETRIA_SETUP_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\GAT_TELEMETRIA.exe

[Files]
Source: "staging\GAT_TELEMETRIA.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "staging\GAT_TELEMETRIA_APP.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "staging\GAT_TELEMETRIA_APP.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "staging\gat\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "staging\trucksim\*"; DestDir: "{app}\TruckSimGPS"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "staging\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist

[Icons]
Name: "{group}\GAT Telemetria"; Filename: "{app}\GAT_TELEMETRIA.exe"
Name: "{group}\Desinstalar GAT Telemetria"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GAT Telemetria"; Filename: "{app}\GAT_TELEMETRIA.exe"

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Instalando componentes necessários..."; Check: NeedsVCRedist; Flags: waituntilterminated
Filename: "{app}\GAT_TELEMETRIA.exe"; Description: "Abrir GAT Telemetria"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM GAT_TELEMETRIA.exe /T"; Flags: runhidden; RunOnceId: "KillGATLauncher"
Filename: "taskkill"; Parameters: "/F /IM GAT_TELEMETRIA_APP.exe /T"; Flags: runhidden; RunOnceId: "KillGATApp"
Filename: "taskkill"; Parameters: "/F /IM TruckSimGPS_Server.exe /T"; Flags: runhidden; RunOnceId: "KillTruckSim"
Filename: "netsh"; Parameters: "http delete urlacl url=http://+:31377/"; Flags: runhidden; RunOnceId: "RemoveUrlAcl"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function NeedsVCRedist: Boolean;
var
  Installed: Cardinal;
begin
  Result := True;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
    Result := Installed <> 1;
end;

procedure KillOldProcesses;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM GAT_TELEMETRIA.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM GAT_TELEMETRIA_APP.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM TruckSimGPS_Server.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(700);
end;

procedure RemoveOldShortcuts;
begin
  DeleteFile(ExpandConstant('{autodesktop}\GAT Telemetria.lnk'));
  DeleteFile(ExpandConstant('{autodesktop}\GAT TELEMETRIA.lnk'));
  DeleteFile(ExpandConstant('{autodesktop}\TruckSim GPS Telemetry Server.lnk'));
  DeleteFile(ExpandConstant('{autodesktop}\TruckSim GPS GAT.lnk'));
  DelTree(ExpandConstant('{group}\GAT Telemetria'), True, True, True);
end;

procedure RemoveOldProgramFolders;
begin
  { Limpeza somente de arquivos de programa. Dados/vinculação da Conta GAT em AppData são preservados. }
  DelTree('C:\TruckSimGPS_GAT', True, True, True);
  DelTree('C:\TruckSimGPS', True, True, True);
  DelTree('C:\GAT_TELEMETRIA', True, True, True);
  DelTree('C:\GAT Telemetria', True, True, True);
  DelTree(ExpandConstant('{autopf}\TruckSim GPS Telemetry Server'), True, True, True);
  DelTree(ExpandConstant('{autopf}\TruckSim GPS GAT'), True, True, True);
  DelTree(ExpandConstant('{localappdata}\Programs\GAT Telemetria'), True, True, True);
  DelTree(ExpandConstant('{localappdata}\TruckSim GPS Telemetry Server'), True, True, True);
end;

procedure RemoveOldStartup;
begin
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'TruckSimGPS');
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GAT Telemetria');
end;

procedure ConfigureFirewall;
var
  ResultCode: Integer;
  NetshPath, ExePath: String;
begin
  NetshPath := ExpandConstant('{sys}\netsh.exe');
  ExePath := ExpandConstant('{app}\TruckSimGPS\TruckSimGPS_Server.exe');
  Exec(NetshPath, 'advfirewall firewall delete rule name="TruckSim GPS Telemetry Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(NetshPath, 'advfirewall firewall delete rule name="TruckSim GPS Telemetry Server (TCP Port)"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(NetshPath, 'advfirewall firewall add rule name="TruckSim GPS Telemetry Server" dir=in action=allow program="' + ExePath + '" profile=any enable=yes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(NetshPath, 'advfirewall firewall add rule name="TruckSim GPS Telemetry Server (TCP Port)" dir=in action=allow protocol=TCP localport=31377 profile=any enable=yes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  KillOldProcesses;
  RemoveOldStartup;
  RemoveOldShortcuts;
  RemoveOldProgramFolders;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    ConfigureFirewall;
end;
