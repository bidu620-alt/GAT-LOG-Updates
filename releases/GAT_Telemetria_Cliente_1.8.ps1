Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security
Add-Type -AssemblyName Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()
$ErrorActionPreference='SilentlyContinue'

$AppName='GAT Telemetria Cliente'
$AppVersion='1.8'
$DataDir=Join-Path $env:LOCALAPPDATA 'GAT Telemetria Cliente'
$ServersFile=Join-Path $DataDir 'servers.json'
$CredFile=Join-Path $DataDir 'credentials.json'
$SettingsFile=Join-Path $DataDir 'client_settings.json'
$TruckUrl='http://127.0.0.1:31377/api/ets2/telemetry'
$IconPath=Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'GAT_LOG.ico'

$TruckHealthUrl='http://127.0.0.1:31377/'
$VersionUrl='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/cliente_version.json'

function Test-TruckSimGps {
    try {
        $r=Invoke-WebRequest -UseBasicParsing -Uri $TruckHealthUrl -TimeoutSec 2
        return ($r.StatusCode -eq 200)
    } catch { return $false }
}

function Find-TruckSimGpsExe {
    try {
        $p=Get-Process -ErrorAction SilentlyContinue | Where-Object {
            $_.ProcessName -match '(?i)TruckSimGPS|TruckSim.*GPS'
        } | Select-Object -First 1
        if($p -and $p.Path -and (Test-Path $p.Path)){return $p.Path}
    } catch {}
    $candidates=@()
    try {
        $roots=@(
            (Join-Path $env:LOCALAPPDATA 'Programs'),
            (Join-Path $env:LOCALAPPDATA 'TruckSim GPS'),
            (Join-Path $env:ProgramFiles 'TruckSim GPS')
        )
        if(${env:ProgramFiles(x86)}){$roots+=(Join-Path ${env:ProgramFiles(x86)} 'TruckSim GPS')}
        foreach($r in $roots){
            if($r -and (Test-Path $r)){
                $cand=Get-ChildItem -Path $r -Filter 'TruckSimGPS_Server.exe' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
                if($cand){return $cand.FullName}
            }
        }
    } catch {}
    return ''
}

function Start-TruckSimGps {
    if(Test-TruckSimGps){ return $true }
    $exe=Find-TruckSimGpsExe
    if($exe){
        try{
            Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)|Out-Null
            Start-Sleep -Milliseconds 700
            return (Test-TruckSimGps)
        }catch{
            [System.Windows.Forms.MessageBox]::Show(
                "O TruckSim GPS esta instalado, mas nao foi possivel abri-lo automaticamente.`r`n`r`nAbra-o pelo Menu Iniciar.",
                'GAT Telemetria',
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Warning
            )|Out-Null
            return $false
        }
    }
    [System.Windows.Forms.MessageBox]::Show(
        "O TruckSim GPS nao esta instalado neste PC.`r`n`r`nA instalacao automatica foi removida do GAT Telemetria.`r`n`r`nUse o executavel separado: INSTALAR_TRUCKSIM_GPS_1.4.1.exe",
        'TruckSim GPS nao instalado',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
    return $false
}

try{New-Item -ItemType Directory -Path $DataDir -Force|Out-Null}catch{}

function Write-Utf8([string]$Path,[string]$Text){try{[System.IO.File]::WriteAllText($Path,$Text,(New-Object System.Text.UTF8Encoding($false)));return $true}catch{return $false}}
function Get-DeviceId {
    $raw=''
    try{$raw=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid -ErrorAction Stop).MachineGuid}catch{}
    if([string]::IsNullOrWhiteSpace($raw)){$raw=$env:COMPUTERNAME+'|'+$env:USERNAME}
    $sha=[System.Security.Cryptography.SHA256]::Create();try{$b=[System.Text.Encoding]::UTF8.GetBytes($raw);$h=$sha.ComputeHash($b);return ([BitConverter]::ToString($h).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()}
}
function Protect-Token([string]$Token){if(!$Token){return ''};$b=[System.Text.Encoding]::UTF8.GetBytes($Token);$p=[System.Security.Cryptography.ProtectedData]::Protect($b,$null,[System.Security.Cryptography.DataProtectionScope]::CurrentUser);return [Convert]::ToBase64String($p)}
function Unprotect-Token([string]$Value){if(!$Value){return ''};try{$p=[Convert]::FromBase64String($Value);$b=[System.Security.Cryptography.ProtectedData]::Unprotect($p,$null,[System.Security.Cryptography.DataProtectionScope]::CurrentUser);return [System.Text.Encoding]::UTF8.GetString($b)}catch{return ''}}
function Get-Servers {try{if(!(Test-Path $ServersFile)){return @()};$r=Get-Content $ServersFile -Raw|ConvertFrom-Json;return @($r)}catch{return @()}}
function Save-Servers($Items){try{Write-Utf8 $ServersFile (@($Items)|ConvertTo-Json -Depth 6)|Out-Null;return $true}catch{return $false}}
function Get-ClientSettings {
    try{
        if(Test-Path $SettingsFile){
            $o=Get-Content $SettingsFile -Raw|ConvertFrom-Json
            if($null-ne$o){return $o}
        }
    }catch{}
    return [pscustomobject]@{auto_connect=$true;last_server=''}
}
function Save-ClientSettings([bool]$AutoConnect,[string]$LastServer){
    $o=[pscustomobject]@{auto_connect=$AutoConnect;last_server=([string]$LastServer).TrimEnd('/');updated_at=(Get-Date).ToString('o')}
    [void](Write-Utf8 $SettingsFile ($o|ConvertTo-Json -Depth 4))
}
function Get-LastServerEndpoint { $o=Get-ClientSettings; try{return ([string]$o.last_server).TrimEnd('/')}catch{return ''} }
function Initialize-DefaultServers {
    if(Test-Path $ServersFile){ return }
    $defaults=@(
        [pscustomobject]@{name='BIDUZAO - DOUGLAS';endpoint='https://douglas.tail4577e8.ts.net'},
        [pscustomobject]@{name='JC - JEAN';endpoint='https://jean-jc.tailf14a00.ts.net'}
    )
    [void](Save-Servers $defaults)
}
Initialize-DefaultServers

function Get-Credentials {try{if(!(Test-Path $CredFile)){return @()};return @((Get-Content $CredFile -Raw|ConvertFrom-Json))}catch{return @()}}
function Get-SavedToken([string]$Endpoint,[string]$Driver){$e=$Endpoint.TrimEnd('/').ToLowerInvariant();foreach($x in @(Get-Credentials)){try{if(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq $e -and ([string]$x.driver)-eq $Driver){return (Unprotect-Token ([string]$x.token))}}catch{}};return ''}
function Save-Token([string]$Endpoint,[string]$Driver,[string]$Token){$items=@();$e=$Endpoint.TrimEnd('/');foreach($x in @(Get-Credentials)){try{if(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq $e.ToLowerInvariant() -and ([string]$x.driver)-eq $Driver){continue}}catch{};$items+=$x};$items+=[pscustomobject]@{endpoint=$e;driver=$Driver;token=(Protect-Token $Token);saved_at=(Get-Date).ToString('o')};Write-Utf8 $CredFile (@($items)|ConvertTo-Json -Depth 6)|Out-Null}
function Remove-Token([string]$Endpoint,[string]$Driver){$items=@();$e=$Endpoint.TrimEnd('/').ToLowerInvariant();foreach($x in @(Get-Credentials)){try{if(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq $e -and ([string]$x.driver)-eq $Driver){continue}}catch{};$items+=$x};Write-Utf8 $CredFile (@($items)|ConvertTo-Json -Depth 6)|Out-Null}

function Decode-ServerCode([string]$Code){
    $c=$Code.Trim()
    if($c -match '^https://') { return [pscustomobject]@{name='Servidor GAT';endpoint=$c.TrimEnd('/')} }
    if(!$c.StartsWith('GAT1:')){return $null}
    try{$s=$c.Substring(5).Replace('-','+').Replace('_','/');while(($s.Length%4)-ne 0){$s+='='};$json=[System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($s));$o=$json|ConvertFrom-Json;if(!$o.endpoint){return $null};return [pscustomobject]@{name=([string]$o.name);endpoint=([string]$o.endpoint).TrimEnd('/')}}catch{return $null}
}

function Invoke-Gat([string]$Method,[string]$Uri,$Body=$null,[int]$Timeout=8){
    try{
        $args=@{UseBasicParsing=$true;Method=$Method;Uri=$Uri;TimeoutSec=$Timeout}
        if($null-ne $Body){$args.ContentType='application/json';$args.Body=($Body|ConvertTo-Json -Depth 30 -Compress)}
        $r=Invoke-WebRequest @args
        $obj=$null;try{$obj=$r.Content|ConvertFrom-Json}catch{}
        return [pscustomobject]@{Status=[int]$r.StatusCode;Json=$obj;Text=[string]$r.Content;Error=''}
    }catch{
        $status=0;$txt='';try{$status=[int]$_.Exception.Response.StatusCode}catch{}
        try{$sr=New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream());$txt=$sr.ReadToEnd();$sr.Dispose()}catch{}
        $obj=$null;try{$obj=$txt|ConvertFrom-Json}catch{}
        return [pscustomobject]@{Status=$status;Json=$obj;Text=$txt;Error=$_.Exception.Message}
    }
}

function Get-CredentialsForEndpoint([string]$Endpoint){
    $out=@();$e=$Endpoint.TrimEnd('/').ToLowerInvariant()
    foreach($x in @(Get-Credentials)){
        try{if(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq$e){$out+=$x}}catch{}
    }
    return @($out)
}
function Remove-OtherEndpointTokens([string]$Endpoint,[string]$KeepDriver){
    $items=@();$e=$Endpoint.TrimEnd('/').ToLowerInvariant()
    foreach($x in @(Get-Credentials)){
        try{
            $sameEp=(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq$e)
            if($sameEp -and ([string]$x.driver)-ne$KeepDriver){continue}
        }catch{}
        $items+=$x
    }
    Write-Utf8 $CredFile (@($items)|ConvertTo-Json -Depth 6)|Out-Null
}
function Get-ServerPlayers([string]$Endpoint){
    $r=Invoke-Gat GET ($Endpoint.TrimEnd('/')+'/api/client/players') $null 5
    if($r.Status-ne200 -or $null-eq$r.Json -or !$r.Json.ok){return @()}
    $out=@();foreach($p in @($r.Json.players)){$s=([string]$p).Trim();if($s -and $out-notcontains$s){$out+=$s}}
    return @($out)
}
function Get-ServerInfo([string]$Endpoint){
    $ep=$Endpoint.TrimEnd('/')
    $r=Invoke-Gat GET ($ep+'/api/client/server-info') $null 5
    if($r.Status-eq200 -and $null-ne$r.Json -and $r.Json.ok){
        $on=$false;try{$on=[bool]$r.Json.online}catch{}
        $n='';try{$n=[string]$r.Json.server_name}catch{}
        $sid='';try{$sid=[string]$r.Json.session_id}catch{}
        $p=0;try{$p=[int]$r.Json.players}catch{}
        $max=0;try{$max=[int]$r.Json.max_players}catch{}
        return [pscustomobject]@{reachable=$true;supported=$true;online=$on;server_name=$n;session_id=$sid;players=$p;max_players=$max}
    }
    $h=Invoke-Gat GET ($ep+'/health') $null 4
    if($h.Status-eq200){return [pscustomobject]@{reachable=$true;supported=$false;online=$null;server_name='';session_id='';players=0;max_players=0}}
    return [pscustomobject]@{reachable=$false;supported=$false;online=$false;server_name='';session_id='';players=0;max_players=0}
}

function Test-Ets2Running {
    try { return ($null -ne (Get-Process -Name 'eurotrucks2' -ErrorAction SilentlyContinue | Select-Object -First 1)) } catch { return $false }
}
function Get-ObjectPathValue($Obj,[string[]]$Paths){
    foreach($path in $Paths){
        $cur=$Obj;$ok=$true
        foreach($part in $path.Split('.')){
            if($null-eq$cur){$ok=$false;break}
            try{$prop=$cur.PSObject.Properties[$part]}catch{$prop=$null}
            if($null-eq$prop){$cur=$prop.Value;continue}
            $ok=$false;break
        }
        if($ok -and $null-ne$cur -and ![string]::IsNullOrWhiteSpace([string]$cur)){return $cur}
    }
    return $null
}
function Get-TelemetryMassKg($Telemetry){
    if($null-eq$Telemetry){return $null}
    $v=Get-ObjectPathValue $Telemetry @(
        'mass_kg','cargoMass','cargo_mass','cargoMassKg','cargo_mass_kg','cargoWeight','cargo_weight','weight_kg',
        'job.mass','job.mass_kg','job.cargoMass','job.cargo_mass','job.cargoMassKg','job.cargo_mass_kg','job.cargoWeight','job.weight',
        'job.cargo.mass','job.cargo.mass_kg','job.cargo.massKg','job.cargo.weight',
        'cargo.mass','cargo.mass_kg','cargo.massKg','cargo.weight',
        'trailer.cargoMass','trailer.cargo_mass','game.job.cargoMass','game.job.mass'
    )
    if($null-eq$v){return $null}
    try{
        $txt=([string]$v).Trim().Replace(',','.')
        $n=[double]::Parse($txt,[System.Globalization.CultureInfo]::InvariantCulture)
        if($n-lt0){$n=[Math]::Abs($n)}
        return $n
    }catch{
        try{return [double]$v}catch{return $null}
    }
}
function Add-NormalizedTelemetryFields($Telemetry){
    if($null-eq$Telemetry){return $Telemetry}
    $m=Get-TelemetryMassKg $Telemetry
    if($null-ne$m){
        foreach($name in @('mass_kg','cargoMass','cargo_mass')){
            try{$Telemetry|Add-Member -NotePropertyName $name -NotePropertyValue ([double]$m) -Force}catch{}
        }
    }
    return $Telemetry
}
function Format-MassKg($Mass){
    if($null-eq$Mass){return '-'}
    try{
        $n=[double]$Mass
        if($n-ge1000){return ([Math]::Round($n/1000,2).ToString('0.##')+' t')}
        return ([Math]::Round($n,0).ToString('0')+' kg')
    }catch{return '-'}
}

function Get-LocalPlayerHint($Tele,[string[]]$Players){
    if($null-eq$Tele -or $null-eq$Players -or $Players.Count-eq0){return ''}
    $candidates=@()
    function Walk-Names($Obj,[int]$Depth){
        if($null-eq$Obj -or $Depth-gt5){return}
        try{
            foreach($pr in $Obj.PSObject.Properties){
                $n=[string]$pr.Name;$v=$pr.Value
                if($n -match '^(?i:playerName|profileName|steamName|userName|username|multiplayerName|player_name|profile_name)$'){
                    $s=([string]$v).Trim();if($s){$script:__gatCandidates+=$s}
                }
                if($null-ne$v -and !($v -is [string]) -and !($v.GetType().IsPrimitive)){Walk-Names $v ($Depth+1)}
            }
        }catch{}
    }
    $script:__gatCandidates=@();Walk-Names $Tele 0;$candidates=@($script:__gatCandidates);$script:__gatCandidates=$null
    foreach($c in $candidates){foreach($p in $Players){if(([string]$p).Equals([string]$c,[StringComparison]::OrdinalIgnoreCase)){return [string]$p}}}
    return ''
}

function Normalize-DriverName([string]$Name){
    if([string]::IsNullOrWhiteSpace($Name)){return ''}
    return (($Name.Trim() -replace '\s+',' ').ToLowerInvariant())
}
function Find-PlayerFromHint([string]$Hint,[string[]]$Players){
    if([string]::IsNullOrWhiteSpace($Hint) -or $null-eq$Players){return ''}
    $n=Normalize-DriverName $Hint
    foreach($p in $Players){if((Normalize-DriverName ([string]$p)) -eq $n){return [string]$p}}
    $compact=($n -replace '[^\p{L}\p{Nd}]','')
    if($compact.Length-ge3){
        $matches=@()
        foreach($p in $Players){
            $pc=((Normalize-DriverName ([string]$p)) -replace '[^\p{L}\p{Nd}]','')
            if($pc -eq $compact){$matches+=[string]$p}
        }
        if($matches.Count-eq1){return [string]$matches[0]}
    }
    return ''
}
function Get-SteamPersonaHint {
    $paths=@()
    try{
        $sp=[string](Get-ItemProperty 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction Stop).SteamPath
        if($sp){$paths+=(Join-Path $sp 'config\loginusers.vdf')}
    }catch{}
    $paths+='C:\Program Files (x86)\Steam\config\loginusers.vdf'
    $paths+='C:\Program Files\Steam\config\loginusers.vdf'
    foreach($path in @($paths|Select-Object -Unique)){
        if(!(Test-Path $path)){continue}
        try{
            $raw=Get-Content $path -Raw -ErrorAction Stop
            $blocks=[regex]::Matches($raw,'"(?<id>\d{15,20})"\s*\{(?<body>.*?)\}',[System.Text.RegularExpressions.RegexOptions]::Singleline)
            $fallback=''
            foreach($b in $blocks){
                $body=$b.Groups['body'].Value
                $pm=[regex]::Match($body,'"PersonaName"\s*"(?<name>[^"]+)"',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
                if(!$pm.Success){continue}
                $name=$pm.Groups['name'].Value.Trim();if(!$name){continue}
                if(!$fallback){$fallback=$name}
                if([regex]::IsMatch($body,'"MostRecent"\s*"1"',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)){return $name}
            }
            if($fallback){return $fallback}
        }catch{}
    }
    return ''
}
function Get-Ets2PlayerHints {
    $out=@()
    $docs=[Environment]::GetFolderPath('MyDocuments')
    if([string]::IsNullOrWhiteSpace($docs)){$docs=Join-Path $env:USERPROFILE 'Documents'}
    $log=Join-Path $docs 'Euro Truck Simulator 2\game.log.txt'
    if(Test-Path $log){
        try{
            $txt=(Get-Content $log -Tail 5000 -ErrorAction Stop)-join "`n"
            $patterns=@(
                '(?im)(?:player|steam|persona|profile)[ _-]*(?:name)?\s*[:=]\s*["'']?([^"'',\r\n\[\]]{2,64})',
                '(?im)profile\s+["'']([^"'']{2,64})["'']'
            )
            foreach($pat in $patterns){
                foreach($m in [regex]::Matches($txt,$pat)){
                    $v=$m.Groups[1].Value.Trim();if($v -and $out -notcontains $v){$out+=$v}
                }
            }
        }catch{}
    }
    return @($out)
}
function Resolve-AutomaticDriver([string]$Endpoint,[string[]]$Players){
    if($null-eq$Players -or $Players.Count-eq0){return ''}
    foreach($cred in @(Get-CredentialsForEndpoint $Endpoint)){
        $m=Find-PlayerFromHint ([string]$cred.driver) $Players
        if($m){return $m}
    }
    $tele=$null;try{$tele=Invoke-RestMethod -Uri $TruckUrl -TimeoutSec 1}catch{}
    $m=Get-LocalPlayerHint $tele $Players
    if($m){return $m}
    $steam=Get-SteamPersonaHint
    $m=Find-PlayerFromHint $steam $Players
    if($m){return $m}
    foreach($h in @(Get-Ets2PlayerHints)){
        $m=Find-PlayerFromHint ([string]$h) $Players
        if($m){return $m}
    }
    if($Players.Count-eq1){return [string]$Players[0]}
    return ''
}

function Get-RemoteClientVersion {
    try{
        $u=$VersionUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        return Invoke-RestMethod -Uri $u -Headers @{'Cache-Control'='no-cache'} -TimeoutSec 5
    }catch{return $null}
}
function Start-GitHubClientUpdate($VersionInfo){
    try{
        $manifestUrl=[string]$VersionInfo.manifest
        if([string]::IsNullOrWhiteSpace($manifestUrl)){throw 'Manifesto de atualizacao nao informado.'}
        $m=Invoke-RestMethod -Uri ($manifestUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -Headers @{'Cache-Control'='no-cache'} -TimeoutSec 6
        $updaterUrl=[string]$m.updater_url
        if([string]::IsNullOrWhiteSpace($updaterUrl)){throw 'Atualizador nao publicado no GitHub.'}
        $tmp=Join-Path $env:TEMP ('gat_cliente_updater_'+[Guid]::NewGuid().ToString('N')+'.ps1')
        Invoke-WebRequest -UseBasicParsing -Uri ($updaterUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -OutFile $tmp -TimeoutSec 10
        $script:UpdateLaunching=$true
        Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$tmp+'"')) | Out-Null
        $form.Close()
        return $true
    }catch{
        [System.Windows.Forms.MessageBox]::Show("Nao foi possivel iniciar a atualizacao.`r`n`r`n"+$_.Exception.Message,'GAT Telemetria | Atualizacao',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Warning)|Out-Null
        return $false
    }
}
function Check-GitHubClientUpdate([bool]$Silent=$true){
    $v=Get-RemoteClientVersion
    if($null-eq$v){
        if(!$Silent){[System.Windows.Forms.MessageBox]::Show('Nao foi possivel consultar o GitHub agora. O cliente continuara funcionando normalmente.','GAT Telemetria | Atualizacao')|Out-Null}
        return
    }
    try{$local=[version]$AppVersion;$remote=[version]([string]$v.version)}catch{return}
    if($remote -gt $local){
        $notes='';try{$notes=[string]$v.notas}catch{}
        $msg="Nova versao disponivel: $remote`r`nVersao instalada: $local"
        if($notes){$msg+="`r`n`r`n$notes"}
        $msg+="`r`n`r`nDeseja atualizar agora pelo GitHub?"
        $ans=[System.Windows.Forms.MessageBox]::Show($msg,'GAT Telemetria | Atualizacao',[System.Windows.Forms.MessageBoxButtons]::YesNo,[System.Windows.Forms.MessageBoxIcon]::Information)
        if($ans-eq[System.Windows.Forms.DialogResult]::Yes){[void](Start-GitHubClientUpdate $v)}
    }elseif(!$Silent){
        [System.Windows.Forms.MessageBox]::Show("Voce ja esta usando a versao mais recente ($AppVersion).",'GAT Telemetria | Atualizacao')|Out-Null
    }
}

function Select-ServerPlayer([string[]]$Players){return ''}

$script:DeviceId=Get-DeviceId
$script:Endpoint='';$script:Driver='';$script:Token='';$script:InSession=$false;$script:LastHeartbeat=[datetime]::MinValue;$script:AutoBusy=$false;$script:LastInfoCheck=[datetime]::MinValue;$script:SelectedInfo=$null;$script:UpdateLaunching=$false;$script:WaitingForSession=$false;$script:LastAutoAttempt=[datetime]::MinValue;$script:AutoConnectEnabled=$true;$script:Initializing=$true

$form=New-Object System.Windows.Forms.Form
$form.Text="$AppName $AppVersion"
$form.Size=New-Object System.Drawing.Size(560,590)
$form.StartPosition='CenterScreen'
$form.BackColor=[System.Drawing.Color]::FromArgb(16,28,42)
$form.ForeColor=[System.Drawing.Color]::White
$form.Font=New-Object System.Drawing.Font('Segoe UI',10)
$form.FormBorderStyle='FixedSingle';$form.MaximizeBox=$false
if(Test-Path $IconPath){try{$form.Icon=New-Object System.Drawing.Icon($IconPath)}catch{}}

$title=New-Object System.Windows.Forms.Label;$title.Text='GAT TELEMETRIA';$title.Font=New-Object System.Drawing.Font('Segoe UI',24,[System.Drawing.FontStyle]::Bold);$title.Location=New-Object System.Drawing.Point(28,22);$title.AutoSize=$true;$form.Controls.Add($title)
$sub=New-Object System.Windows.Forms.Label;$sub.Text='Conexao automatica: abra o cliente antes ou depois do ETS2';$sub.Location=New-Object System.Drawing.Point(31,67);$sub.AutoSize=$true;$sub.ForeColor=[System.Drawing.Color]::FromArgb(160,185,210);$form.Controls.Add($sub)
$lblVersion=New-Object System.Windows.Forms.Label;$lblVersion.Text='Versao '+$AppVersion;$lblVersion.Location=New-Object System.Drawing.Point(31,515);$lblVersion.AutoSize=$true;$lblVersion.ForeColor=[System.Drawing.Color]::FromArgb(150,175,198);$form.Controls.Add($lblVersion)
$btnUpdate=New-Object System.Windows.Forms.Button;$btnUpdate.Text='VERIFICAR ATUALIZACAO';$btnUpdate.Location=New-Object System.Drawing.Point(315,508);$btnUpdate.Size=New-Object System.Drawing.Size(198,34);$btnUpdate.BackColor=[System.Drawing.Color]::FromArgb(42,103,172);$btnUpdate.ForeColor=[System.Drawing.Color]::White;$btnUpdate.FlatStyle='Flat';$form.Controls.Add($btnUpdate)

$loginPanel=New-Object System.Windows.Forms.Panel;$loginPanel.Location=New-Object System.Drawing.Point(28,105);$loginPanel.Size=New-Object System.Drawing.Size(485,395);$loginPanel.BackColor=[System.Drawing.Color]::FromArgb(24,40,58);$form.Controls.Add($loginPanel)
$l2=New-Object System.Windows.Forms.Label;$l2.Text='Servidor';$l2.Location=New-Object System.Drawing.Point(22,16);$l2.AutoSize=$true;$loginPanel.Controls.Add($l2)
$cmbServer=New-Object System.Windows.Forms.ComboBox;$cmbServer.Location=New-Object System.Drawing.Point(22,40);$cmbServer.Size=New-Object System.Drawing.Size(440,31);$cmbServer.DropDownStyle='DropDownList';$loginPanel.Controls.Add($cmbServer)
$lblServerState=New-Object System.Windows.Forms.Label;$lblServerState.Text='Status: CONSULTANDO...';$lblServerState.Location=New-Object System.Drawing.Point(22,83);$lblServerState.Size=New-Object System.Drawing.Size(440,26);$lblServerState.Font=New-Object System.Drawing.Font('Segoe UI',10,[System.Drawing.FontStyle]::Bold);$loginPanel.Controls.Add($lblServerState)
$lblPlayersInfo=New-Object System.Windows.Forms.Label;$lblPlayersInfo.Text='Jogadores: -';$lblPlayersInfo.Location=New-Object System.Drawing.Point(22,111);$lblPlayersInfo.Size=New-Object System.Drawing.Size(220,25);$lblPlayersInfo.ForeColor=[System.Drawing.Color]::FromArgb(180,205,225);$loginPanel.Controls.Add($lblPlayersInfo)
$lblId=New-Object System.Windows.Forms.Label;$lblId.Text='ID da sala';$lblId.Location=New-Object System.Drawing.Point(22,143);$lblId.AutoSize=$true;$loginPanel.Controls.Add($lblId)
$txtRoomId=New-Object System.Windows.Forms.TextBox;$txtRoomId.Location=New-Object System.Drawing.Point(22,166);$txtRoomId.Size=New-Object System.Drawing.Size(290,29);$txtRoomId.ReadOnly=$true;$txtRoomId.Text='Aguardando...';$loginPanel.Controls.Add($txtRoomId)
$btnCopyId=New-Object System.Windows.Forms.Button;$btnCopyId.Text='COPIAR ID';$btnCopyId.Location=New-Object System.Drawing.Point(320,164);$btnCopyId.Size=New-Object System.Drawing.Size(142,34);$btnCopyId.BackColor=[System.Drawing.Color]::FromArgb(42,103,172);$btnCopyId.ForeColor=[System.Drawing.Color]::White;$btnCopyId.FlatStyle='Flat';$btnCopyId.Enabled=$false;$loginPanel.Controls.Add($btnCopyId)
$btnAdd=New-Object System.Windows.Forms.Button;$btnAdd.Text='ADICIONAR';$btnAdd.Location=New-Object System.Drawing.Point(22,215);$btnAdd.Size=New-Object System.Drawing.Size(145,38);$btnAdd.BackColor=[System.Drawing.Color]::FromArgb(42,103,172);$btnAdd.ForeColor=[System.Drawing.Color]::White;$btnAdd.FlatStyle='Flat';$loginPanel.Controls.Add($btnAdd)
$btnRemove=New-Object System.Windows.Forms.Button;$btnRemove.Text='REMOVER';$btnRemove.Location=New-Object System.Drawing.Point(177,215);$btnRemove.Size=New-Object System.Drawing.Size(110,38);$btnRemove.BackColor=[System.Drawing.Color]::FromArgb(75,86,98);$btnRemove.ForeColor=[System.Drawing.Color]::White;$btnRemove.FlatStyle='Flat';$loginPanel.Controls.Add($btnRemove)
$btnRefreshInfo=New-Object System.Windows.Forms.Button;$btnRefreshInfo.Text='ATUALIZAR STATUS';$btnRefreshInfo.Location=New-Object System.Drawing.Point(297,215);$btnRefreshInfo.Size=New-Object System.Drawing.Size(165,38);$btnRefreshInfo.BackColor=[System.Drawing.Color]::FromArgb(70,82,96);$btnRefreshInfo.ForeColor=[System.Drawing.Color]::White;$btnRefreshInfo.FlatStyle='Flat';$loginPanel.Controls.Add($btnRefreshInfo)
$chkAuto=New-Object System.Windows.Forms.CheckBox;$chkAuto.Text='Entrar automaticamente ao abrir o cliente';$chkAuto.Location=New-Object System.Drawing.Point(22,258);$chkAuto.Size=New-Object System.Drawing.Size(350,25);$chkAuto.ForeColor=[System.Drawing.Color]::FromArgb(190,210,230);$loginPanel.Controls.Add($chkAuto)
$btnEnter=New-Object System.Windows.Forms.Button;$btnEnter.Text='ENTRAR / AGUARDAR SESSAO';$btnEnter.Location=New-Object System.Drawing.Point(22,286);$btnEnter.Size=New-Object System.Drawing.Size(440,47);$btnEnter.BackColor=[System.Drawing.Color]::FromArgb(34,135,83);$btnEnter.ForeColor=[System.Drawing.Color]::White;$btnEnter.FlatStyle='Flat';$btnEnter.Font=New-Object System.Drawing.Font('Segoe UI',11,[System.Drawing.FontStyle]::Bold);$loginPanel.Controls.Add($btnEnter)
$lblLogin=New-Object System.Windows.Forms.Label;$lblLogin.Text='Selecione o servidor. O cliente pode ficar aguardando mesmo com o ETS2 fechado.';$lblLogin.Location=New-Object System.Drawing.Point(22,339);$lblLogin.Size=New-Object System.Drawing.Size(440,48);$lblLogin.ForeColor=[System.Drawing.Color]::FromArgb(235,190,80);$loginPanel.Controls.Add($lblLogin)

$sessionPanel=New-Object System.Windows.Forms.Panel;$sessionPanel.Location=$loginPanel.Location;$sessionPanel.Size=New-Object System.Drawing.Size(485,385);$sessionPanel.BackColor=$loginPanel.BackColor;$sessionPanel.Visible=$false;$form.Controls.Add($sessionPanel)
$srv=New-Object System.Windows.Forms.Label;$srv.Location=New-Object System.Drawing.Point(22,22);$srv.Size=New-Object System.Drawing.Size(440,28);$srv.Font=New-Object System.Drawing.Font('Segoe UI',12,[System.Drawing.FontStyle]::Bold);$sessionPanel.Controls.Add($srv)
$drv=New-Object System.Windows.Forms.Label;$drv.Location=New-Object System.Drawing.Point(22,55);$drv.Size=New-Object System.Drawing.Size(440,28);$sessionPanel.Controls.Add($drv)
$stTruck=New-Object System.Windows.Forms.Label;$stTruck.Location=New-Object System.Drawing.Point(22,105);$stTruck.Size=New-Object System.Drawing.Size(440,28);$sessionPanel.Controls.Add($stTruck)
$stGat=New-Object System.Windows.Forms.Label;$stGat.Location=New-Object System.Drawing.Point(22,140);$stGat.Size=New-Object System.Drawing.Size(440,28);$sessionPanel.Controls.Add($stGat)
$stTel=New-Object System.Windows.Forms.Label;$stTel.Location=New-Object System.Drawing.Point(22,175);$stTel.Size=New-Object System.Drawing.Size(440,28);$sessionPanel.Controls.Add($stTel)
$cargo=New-Object System.Windows.Forms.Label;$cargo.Location=New-Object System.Drawing.Point(22,210);$cargo.Size=New-Object System.Drawing.Size(440,68);$cargo.ForeColor=[System.Drawing.Color]::FromArgb(190,210,230);$sessionPanel.Controls.Add($cargo)
$btnTruck=New-Object System.Windows.Forms.Button;$btnTruck.Text='ABRIR TRUCKSIM GPS';$btnTruck.Location=New-Object System.Drawing.Point(22,278);$btnTruck.Size=New-Object System.Drawing.Size(330,38);$btnTruck.BackColor=[System.Drawing.Color]::FromArgb(42,103,172);$btnTruck.ForeColor=[System.Drawing.Color]::White;$btnTruck.FlatStyle='Flat';$sessionPanel.Controls.Add($btnTruck)
$btnSwitch=New-Object System.Windows.Forms.Button;$btnSwitch.Text='TROCAR SERVIDOR';$btnSwitch.Location=New-Object System.Drawing.Point(22,323);$btnSwitch.Size=New-Object System.Drawing.Size(205,45);$btnSwitch.BackColor=[System.Drawing.Color]::FromArgb(70,82,96);$btnSwitch.ForeColor=[System.Drawing.Color]::White;$btnSwitch.FlatStyle='Flat';$sessionPanel.Controls.Add($btnSwitch)
$btnExit=New-Object System.Windows.Forms.Button;$btnExit.Text='SAIR';$btnExit.Location=New-Object System.Drawing.Point(237,323);$btnExit.Size=New-Object System.Drawing.Size(115,45);$btnExit.BackColor=[System.Drawing.Color]::FromArgb(135,55,55);$btnExit.ForeColor=[System.Drawing.Color]::White;$btnExit.FlatStyle='Flat';$sessionPanel.Controls.Add($btnExit)

function Refresh-SelectedServerInfo {
    if($script:InSession){return}
    if($cmbServer.SelectedIndex-lt0){
        $lblServerState.Text='Status: NENHUM SERVIDOR'
        $lblServerState.ForeColor=[System.Drawing.Color]::Khaki
        $lblPlayersInfo.Text='Jogadores: -'
        $txtRoomId.Text='-'
        $btnCopyId.Enabled=$false
        return
    }
    $sel=$cmbServer.SelectedItem
    $ep=([string]$sel.endpoint).TrimEnd('/')
    $lblServerState.Text='Status: CONSULTANDO...'
    $lblServerState.ForeColor=[System.Drawing.Color]::Khaki
    $form.Refresh()
    $info=Get-ServerInfo $ep
    $script:SelectedInfo=$info
    $script:LastInfoCheck=Get-Date
    if(!$info.reachable){
        $lblServerState.Text='Status: GAT LOG INACESSIVEL'
        $lblServerState.ForeColor=[System.Drawing.Color]::Salmon
        $lblPlayersInfo.Text='Jogadores: -';$txtRoomId.Text='-';$btnCopyId.Enabled=$false;return
    }
    if(!$info.supported){
        $lblServerState.Text='Status: GAT LOG ONLINE | ATUALIZE O SERVIDOR PARA 1.9'
        $lblServerState.ForeColor=[System.Drawing.Color]::Khaki
        $lblPlayersInfo.Text='Jogadores: -';$txtRoomId.Text='ID indisponivel';$btnCopyId.Enabled=$false;return
    }
    if($info.online){
        $lblServerState.Text='Status: SERVIDOR ONLINE';$lblServerState.ForeColor=[System.Drawing.Color]::LightGreen
        if($info.max_players-gt0){$lblPlayersInfo.Text="Jogadores: $($info.players) / $($info.max_players)"}else{$lblPlayersInfo.Text="Jogadores: $($info.players)"}
        if([string]::IsNullOrWhiteSpace([string]$info.session_id)){$txtRoomId.Text='Aguardando ID...';$btnCopyId.Enabled=$false}else{$txtRoomId.Text=[string]$info.session_id;$btnCopyId.Enabled=$true}
    }else{
        $lblServerState.Text='Status: SERVIDOR OFFLINE';$lblServerState.ForeColor=[System.Drawing.Color]::Salmon
        $lblPlayersInfo.Text='Jogadores: 0';$txtRoomId.Text='-';$btnCopyId.Enabled=$false
    }
}

function Reload-ServerCombo([string]$SelectEndpoint=''){
    $cmbServer.Items.Clear();$servers=@(Get-Servers)
    foreach($s in $servers){[void]$cmbServer.Items.Add(([pscustomobject]@{name=[string]$s.name;endpoint=([string]$s.endpoint).TrimEnd('/')}))}
    $cmbServer.DisplayMember='name'
    if($cmbServer.Items.Count-gt0){
        $wanted=$SelectEndpoint
        if([string]::IsNullOrWhiteSpace($wanted)){$wanted=Get-LastServerEndpoint}
        $idx=0
        if($wanted){for($i=0;$i-lt$cmbServer.Items.Count;$i++){if(([string]$cmbServer.Items[$i].endpoint).TrimEnd('/').ToLowerInvariant()-eq$wanted.TrimEnd('/').ToLowerInvariant()){$idx=$i;break}}}
        $cmbServer.SelectedIndex=$idx
    }
}

$btnAdd.Add_Click({
    $d=New-Object System.Windows.Forms.Form;$d.Text='Adicionar servidor GAT';$d.Size=New-Object System.Drawing.Size(530,280);$d.StartPosition='CenterParent';$d.BackColor=$form.BackColor;$d.ForeColor=[System.Drawing.Color]::White;$d.Font=$form.Font;$d.FormBorderStyle='FixedDialog';$d.MaximizeBox=$false
    $la=New-Object System.Windows.Forms.Label;$la.Text='Cole o CODIGO DO SERVIDOR copiado no GAT LOG:';$la.Location=New-Object System.Drawing.Point(20,20);$la.AutoSize=$true;$d.Controls.Add($la)
    $tb=New-Object System.Windows.Forms.TextBox;$tb.Location=New-Object System.Drawing.Point(20,50);$tb.Size=New-Object System.Drawing.Size(470,70);$tb.Multiline=$true;$d.Controls.Add($tb)
    $info=New-Object System.Windows.Forms.Label;$info.Text='Tambem aceita um endereco https://...ts.net';$info.Location=New-Object System.Drawing.Point(20,125);$info.AutoSize=$true;$info.ForeColor=[System.Drawing.Color]::FromArgb(155,180,205);$d.Controls.Add($info)
    $ok=New-Object System.Windows.Forms.Button;$ok.Text='ADICIONAR';$ok.Location=New-Object System.Drawing.Point(20,165);$ok.Size=New-Object System.Drawing.Size(160,42);$ok.BackColor=[System.Drawing.Color]::FromArgb(34,135,83);$ok.ForeColor=[System.Drawing.Color]::White;$ok.FlatStyle='Flat';$d.Controls.Add($ok)
    $cancel=New-Object System.Windows.Forms.Button;$cancel.Text='CANCELAR';$cancel.Location=New-Object System.Drawing.Point(190,165);$cancel.Size=New-Object System.Drawing.Size(140,42);$cancel.BackColor=[System.Drawing.Color]::FromArgb(70,82,96);$cancel.ForeColor=[System.Drawing.Color]::White;$cancel.FlatStyle='Flat';$d.Controls.Add($cancel)
    $ok.Add_Click({$s=Decode-ServerCode $tb.Text;if($null-eq$s){[System.Windows.Forms.MessageBox]::Show('Codigo/endereco invalido.','GAT Telemetria')|Out-Null;return};if($s.name-eq'Servidor GAT'){$n=[Microsoft.VisualBasic.Interaction]::InputBox('Nome para este servidor:','GAT Telemetria','GAT AMIGOS');if($n){$s.name=$n}};$items=@();$exists=$false;foreach($x in @(Get-Servers)){if(([string]$x.endpoint).TrimEnd('/').ToLowerInvariant()-eq$s.endpoint.ToLowerInvariant()){$items+=$s;$exists=$true}else{$items+=$x}};if(!$exists){$items+=$s};Save-Servers $items|Out-Null;$d.Tag=$s.endpoint;$d.Close()})
    $cancel.Add_Click({$d.Close()});[void]$d.ShowDialog();if($d.Tag){Reload-ServerCombo ([string]$d.Tag)}
})
$btnRemove.Add_Click({if($cmbServer.SelectedIndex-lt0){return};$sel=$cmbServer.SelectedItem;if([System.Windows.Forms.MessageBox]::Show("Remover o servidor $($sel.name) desta lista?",'GAT Telemetria',[System.Windows.Forms.MessageBoxButtons]::YesNo)-ne[System.Windows.Forms.DialogResult]::Yes){return};$items=@();foreach($x in @(Get-Servers)){if(([string]$x.endpoint).TrimEnd('/')-ne$sel.endpoint){$items+=$x}};Save-Servers $items|Out-Null;Reload-ServerCombo})
$btnTruck.Add_Click({[void](Start-TruckSimGps)})
$btnCopyId.Add_Click({
    if(!$btnCopyId.Enabled -or [string]::IsNullOrWhiteSpace($txtRoomId.Text)){return}
    try{[System.Windows.Forms.Clipboard]::SetText($txtRoomId.Text.Trim());$lblLogin.Text='ID da sala copiado. Agora abra o ETS2 e entre no comboio.'}catch{$lblLogin.Text='Nao foi possivel copiar o ID.'}
})
$btnRefreshInfo.Add_Click({Refresh-SelectedServerInfo})

function Renew-GatCredential {
    if([string]::IsNullOrWhiteSpace($script:Endpoint) -or [string]::IsNullOrWhiteSpace($script:Driver)){return $false}
    $rr=Invoke-Gat POST ($script:Endpoint.TrimEnd('/')+'/api/client/login') ([pscustomobject]@{
        driver=$script:Driver
        device_id=$script:DeviceId
        token=''
    }) 8
    if($rr.Status-eq200 -and $rr.Json.ok){
        $canonical=$script:Driver
        try{if($rr.Json.driver){$canonical=[string]$rr.Json.driver}}catch{}
        $newToken=''
        try{$newToken=[string]$rr.Json.token}catch{}
        if(![string]::IsNullOrWhiteSpace($newToken)){
            $script:Driver=$canonical
            $script:Token=$newToken
            Save-Token $script:Endpoint $canonical $newToken
            Remove-OtherEndpointTokens $script:Endpoint $canonical
            $drv.Text='Motorista: '+$canonical+'  (detectado automaticamente)'
            return $true
        }
    }
    return $false
}

function Send-GatTelemetryWithRenew($Telemetry){
    $Telemetry=Add-NormalizedTelemetryFields $Telemetry
    $body=[pscustomobject]@{driver=$script:Driver;device_id=$script:DeviceId;token=$script:Token;telemetry=$Telemetry}
    $r=Invoke-Gat POST ($script:Endpoint+'/api/client/telemetry') $body 8
    $err='';try{$err=[string]$r.Json.error}catch{}
    if($r.Status-eq401 -or $err-eq'token_required'){
        if(Renew-GatCredential){
            $body=[pscustomobject]@{driver=$script:Driver;device_id=$script:DeviceId;token=$script:Token;telemetry=$Telemetry}
            $r=Invoke-Gat POST ($script:Endpoint+'/api/client/telemetry') $body 8
        }
    }
    return $r
}

function Send-GatHeartbeatWithRenew {
    $body=[pscustomobject]@{driver=$script:Driver;device_id=$script:DeviceId;token=$script:Token}
    $r=Invoke-Gat POST ($script:Endpoint+'/api/client/heartbeat') $body 6
    $err='';try{$err=[string]$r.Json.error}catch{}
    if($r.Status-eq401 -or $err-eq'token_required'){
        if(Renew-GatCredential){
            $body=[pscustomobject]@{driver=$script:Driver;device_id=$script:DeviceId;token=$script:Token}
            $r=Invoke-Gat POST ($script:Endpoint+'/api/client/heartbeat') $body 6
        }
    }
    return $r
}

function Start-DetectedSession([string]$Driver,$Server){
    if([string]::IsNullOrWhiteSpace($Driver)){return $false}
    $ep=([string]$Server.endpoint).TrimEnd('/');$token=Get-SavedToken $ep $Driver
    $r=Invoke-Gat POST ($ep+'/api/client/login') ([pscustomobject]@{driver=$Driver;device_id=$script:DeviceId;token=$token}) 10
    if($r.Status-eq200 -and $r.Json.ok){
        $canonical=$Driver;try{if($r.Json.driver){$canonical=[string]$r.Json.driver}}catch{}
        if($r.Json.token){$token=[string]$r.Json.token;Save-Token $ep $canonical $token}
        Remove-OtherEndpointTokens $ep $canonical
        $script:Endpoint=$ep;$script:Driver=$canonical;$script:Token=$token;$script:InSession=$true;$script:WaitingForSession=$false;$script:LastHeartbeat=[datetime]::MinValue
        $srv.Text='Servidor: '+[string]$Server.name;$drv.Text='Motorista: '+$canonical+'  (detectado automaticamente)';$loginPanel.Visible=$false;$sessionPanel.Visible=$true;$sessionPanel.BringToFront();return $true
    }
    $err='';try{$err=[string]$r.Json.error}catch{}
    switch($err){
        'device_mismatch'{$lblLogin.Text='Seu nome ja esta vinculado a outro PC. Use DESVINCULAR PC no GAT LOG.'}
        'token_required'{$lblLogin.Text='Credencial desatualizada. Tentando renovar automaticamente...'}
        'blocked'{$lblLogin.Text='Motorista bloqueado pelo administrador.'}
        'registration_closed'{$lblLogin.Text='Novos vinculos estao bloqueados neste servidor.'}
        'not_in_server'{$lblLogin.Text='ETS2 aberto. Aguardando voce entrar na sessao...'}
        'disconnected_by_admin'{$lblLogin.Text='Desconectado pelo administrador. Aguarde e tente novamente.'}
        default{if($r.Status-eq0){$lblLogin.Text='Servidor inacessivel. Verifique internet/Funnel.'}else{$lblLogin.Text="Falha ao conectar (HTTP $($r.Status))."}}
    }
    return $false
}

function Try-Connect([bool]$Interactive=$true){
    if($script:InSession -or $script:AutoBusy){return}
    if($cmbServer.SelectedIndex-lt0){$lblLogin.Text='Escolha um servidor para iniciar o modo automatico.';return}
    $script:WaitingForSession=$true
    $script:AutoBusy=$true
    try{
        $sel=$cmbServer.SelectedItem;$ep=([string]$sel.endpoint).TrimEnd('/')
        Save-ClientSettings $script:AutoConnectEnabled $ep
        if(!(Test-Ets2Running)){
            $lblLogin.Text='AGUARDANDO ETS2... Pode abrir o jogo quando quiser. Depois entre na sessao e a telemetria conecta sozinha.'
            $btnEnter.Text='AGUARDANDO ETS2...'
            return
        }
        $lblLogin.Text='ETS2 aberto. Aguardando voce entrar na sessao selecionada...'
        $btnEnter.Text='AGUARDANDO ENTRAR NA SESSAO...'
        $players=@(Get-ServerPlayers $ep)
        if($players.Count-eq0){return}
        $driver=Resolve-AutomaticDriver $ep $players
        if($driver){
            if(Start-DetectedSession $driver $sel){return}
            return
        }
        $lblLogin.Text="Sessao detectada com $($players.Count) jogador(es). Aguardando reconhecer automaticamente seu motorista..."
    } finally {$script:AutoBusy=$false}
}

$btnEnter.Add_Click({
    if($cmbServer.SelectedIndex-lt0){$lblLogin.Text='Escolha um servidor primeiro.';return}
    $script:WaitingForSession=$true
    $script:LastAutoAttempt=[datetime]::MinValue
    Try-Connect $true
})
$btnUpdate.Add_Click({Check-GitHubClientUpdate $false})
$chkAuto.Add_CheckedChanged({
    $script:AutoConnectEnabled=[bool]$chkAuto.Checked
    if($script:Initializing){return}
    $ep='';if($cmbServer.SelectedIndex-ge0){$ep=([string]$cmbServer.SelectedItem.endpoint).TrimEnd('/')}
    Save-ClientSettings $script:AutoConnectEnabled $ep
    if($script:AutoConnectEnabled -and $cmbServer.SelectedIndex-ge0 -and !$script:InSession){
        $script:WaitingForSession=$true;$script:LastAutoAttempt=[datetime]::MinValue
    }elseif(!$script:InSession){
        $script:WaitingForSession=$false;$btnEnter.Text='ENTRAR / AGUARDAR SESSAO';$lblLogin.Text='Modo automatico desativado. Clique ENTRAR quando quiser ficar aguardando a sessao.'
    }
})
$cmbServer.Add_SelectedIndexChanged({
    if($script:Initializing){return}
    if(!$script:InSession){
        Refresh-SelectedServerInfo
        if($cmbServer.SelectedIndex-ge0){
            $ep=([string]$cmbServer.SelectedItem.endpoint).TrimEnd('/')
            Save-ClientSettings $script:AutoConnectEnabled $ep
            $script:WaitingForSession=$script:AutoConnectEnabled
            $script:LastAutoAttempt=[datetime]::MinValue
            if($script:AutoConnectEnabled){$lblLogin.Text='Servidor selecionado. Modo automatico ativo: aguardando ETS2/sessao...'}
            else{$lblLogin.Text='Servidor selecionado. Clique ENTRAR para aguardar a sessao.'}
        }
    }
})
function End-Session([string]$Message=''){
    $script:InSession=$false
    $sessionPanel.Visible=$false;$loginPanel.Visible=$true;$loginPanel.BringToFront()
    $btnEnter.Text='ENTRAR / AGUARDAR SESSAO'
    if($Message){$lblLogin.Text=$Message}else{$lblLogin.Text='Aguardando servidor/ETS2.'}
    Refresh-SelectedServerInfo
}
$btnSwitch.Add_Click({
    $script:InSession=$false;$script:WaitingForSession=$false
    $sessionPanel.Visible=$false;$loginPanel.Visible=$true;$loginPanel.BringToFront()
    $cmbServer.SelectedIndex=-1
    $btnEnter.Text='ENTRAR / AGUARDAR SESSAO'
    $lblLogin.Text='Escolha outro servidor. Ao selecionar, ele vira o servidor padrao.'
    Refresh-SelectedServerInfo
})
$btnExit.Add_Click({$form.Close()})

$timer=New-Object System.Windows.Forms.Timer;$timer.Interval=1200
$timer.Add_Tick({
    if(!$script:InSession){
        if(((Get-Date)-$script:LastInfoCheck).TotalSeconds-ge3){Refresh-SelectedServerInfo}
        if($script:WaitingForSession -and !$script:AutoBusy -and $cmbServer.SelectedIndex-ge0 -and ((Get-Date)-$script:LastAutoAttempt).TotalSeconds-ge2.5){
            $script:LastAutoAttempt=Get-Date
            Try-Connect $false
        }
        return
    }
    $tele=$null;$truckOk=$false
    try{$tele=Invoke-RestMethod -Uri $TruckUrl -TimeoutSec 1;$truckOk=($null-ne$tele)}catch{}
    if($truckOk){
        $tele=Add-NormalizedTelemetryFields $tele
        $stTruck.Text='TruckSim GPS       ● CONECTADO';$stTruck.ForeColor=[System.Drawing.Color]::LightGreen;$btnTruck.Text='ABRIR TRUCKSIM GPS'
        $r=Send-GatTelemetryWithRenew $tele
        if($r.Status-eq200 -and $r.Json.ok){
            $stGat.Text='GAT LOG            ● CONECTADO';$stGat.ForeColor=[System.Drawing.Color]::LightGreen
            $stTel.Text='Telemetria         ● ENVIANDO';$stTel.ForeColor=[System.Drawing.Color]::LightGreen
            $c='Sem carga';try{if($r.Json.cargo){$c=[string]$r.Json.cargo}}catch{}
            $km='-';try{$km=[Math]::Round(([double]$r.Json.distance_m/1000),1).ToString('0.#')+' km'}catch{}
            $vel='-';try{$vel=[Math]::Round([Math]::Abs([double]$r.Json.speed_kmh),0).ToString('0')+' km/h'}catch{}
            $mass=$null
            try{$mass=[double]$r.Json.mass_kg}catch{}
            if($null-eq$mass){try{$mass=[double]$r.Json.cargo_mass}catch{}}
            if($null-eq$mass){$mass=Get-TelemetryMassKg $tele}
            $peso=Format-MassKg $mass
            $cargo.Text="Carga: $c   |   Peso: $peso`r`nKm restantes: $km`r`nVelocidade: $vel"
        }else{
            $err='';try{$err=[string]$r.Json.error}catch{}
            if($err-eq'blocked'){End-Session 'Motorista bloqueado pelo administrador.';return}
            if($err-eq'device_mismatch'){End-Session 'PC nao autorizado. Use DESVINCULAR PC no servidor.';return}
            if($err-eq'disconnected_by_admin'){End-Session 'Voce foi desconectado pelo administrador.';return}
            if($r.Status-eq0){$stGat.Text='GAT LOG            ● SERVIDOR INACESSIVEL'}else{$stGat.Text="GAT LOG            ● ERRO HTTP $($r.Status)"}
            $stGat.ForeColor=[System.Drawing.Color]::Salmon;$stTel.Text='Telemetria         ● NAO ENVIANDO';$stTel.ForeColor=[System.Drawing.Color]::Salmon
        }
    }else{
        $truckExe=Find-TruckSimGpsExe
        if($truckExe){$stTruck.Text='TruckSim GPS       ● INSTALADO / FECHADO';$btnTruck.Text='ABRIR TRUCKSIM GPS'}else{$stTruck.Text='TruckSim GPS       ● NAO INSTALADO';$btnTruck.Text='TRUCKSIM GPS NAO INSTALADO'}
        $stTruck.ForeColor=[System.Drawing.Color]::Khaki;$stTel.Text='Telemetria         ● AGUARDANDO ETS2';$stTel.ForeColor=[System.Drawing.Color]::Khaki;$cargo.Text='Aguardando o ETS2/TruckSim GPS para iniciar a telemetria.'
        if(((Get-Date)-$script:LastHeartbeat).TotalSeconds-ge5){
            $script:LastHeartbeat=Get-Date;$r=Send-GatHeartbeatWithRenew
            if($r.Status-eq200){$stGat.Text='GAT LOG            ● CONECTADO';$stGat.ForeColor=[System.Drawing.Color]::LightGreen}
            else{$err='';try{$err=[string]$r.Json.error}catch{};if($err-eq'blocked'){End-Session 'Motorista bloqueado pelo administrador.';return};if($err-eq'disconnected_by_admin'){End-Session 'Voce foi desconectado pelo administrador.';return};$stGat.Text='GAT LOG            ● SEM CONEXAO';$stGat.ForeColor=[System.Drawing.Color]::Salmon}
        }
    }
})
$timer.Start()
$updateTimer=New-Object System.Windows.Forms.Timer;$updateTimer.Interval=1600
$updateTimer.Add_Tick({$updateTimer.Stop();Check-GitHubClientUpdate $true})
$form.Add_Shown({$updateTimer.Start()})
$form.Add_FormClosing({$timer.Stop();$updateTimer.Stop()})
$cfg=Get-ClientSettings
try{$script:AutoConnectEnabled=[bool]$cfg.auto_connect}catch{$script:AutoConnectEnabled=$true}
$chkAuto.Checked=$script:AutoConnectEnabled
$lastServer='';try{$lastServer=([string]$cfg.last_server).TrimEnd('/')}catch{}
Reload-ServerCombo $lastServer
$script:Initializing=$false
Refresh-SelectedServerInfo
if($script:AutoConnectEnabled -and $cmbServer.SelectedIndex-ge0){
    $script:WaitingForSession=$true
    $lblLogin.Text='Modo automatico ativo. AGUARDANDO ETS2...'
    $btnEnter.Text='AGUARDANDO ETS2...'
}else{
    $lblLogin.Text='Escolha o servidor e clique ENTRAR para aguardar a sessao.'
}
[void]$form.ShowDialog()