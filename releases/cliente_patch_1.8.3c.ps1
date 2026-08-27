$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao 1.8.3'){
    [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}

$dir=Join-Path $env:LOCALAPPDATA 'GAT Telemetria Cliente'
$target=Join-Path $dir 'GAT_Telemetria_Cliente.ps1'
$hashPath=Join-Path $dir 'client_integrity.sha256'
$backup=Join-Path $dir ('GAT_Telemetria_Cliente.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.ps1')

try{
    if(!(Test-Path $target)){throw 'Arquivo principal do cliente nao encontrado.'}
    Copy-Item $target $backup -Force
    $raw=Get-Content $target -Raw -Encoding UTF8

    $versionPattern='(?m)^\s*\$AppVersion\s*=\s*[''"]1\.8(?:\.\d+)?[''"]\s*$'
    if($raw -notmatch $versionPattern){throw 'A versao instalada nao pertence a base 1.8.x.'}
    $raw=[regex]::Replace($raw,$versionPattern,'$AppVersion=''1.8.3''',1)

    # Oculta o PowerShell que inicia as proximas atualizacoes.
    $oldUpdater=@'
Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$tmp+'"')) | Out-Null
'@.Trim()
    $newUpdater=@'
Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('"'+$tmp+'"')) -WindowStyle Hidden | Out-Null
'@.Trim()
    if($raw.Contains($oldUpdater)){$raw=$raw.Replace($oldUpdater,$newUpdater)}

    # Mantem a correcao de reconhecer quem ja estava na sessao.
    if($raw -notmatch 'Verificando se voce ja esta na sessao selecionada'){
        $oldConnect=@'
        $lblLogin.Text='ETS2 aberto. Aguardando voce entrar na sessao selecionada...'
        $btnEnter.Text='AGUARDANDO ENTRAR NA SESSAO...'
        $players=@(Get-ServerPlayers $ep)
        if($players.Count-eq0){return}
        $driver=Resolve-AutomaticDriver $ep $players
'@
        $newConnect=@'
        $lblLogin.Text='ETS2 aberto. Verificando se voce ja esta na sessao selecionada...'
        $btnEnter.Text='VERIFICANDO SESSAO...'
        foreach($cred in @(Get-CredentialsForEndpoint $ep)){
            $savedDriver=([string]$cred.driver).Trim()
            if([string]::IsNullOrWhiteSpace($savedDriver)){continue}
            if(Start-DetectedSession $savedDriver $sel){return}
        }
        $lblLogin.Text='ETS2 aberto. Aguardando voce entrar na sessao selecionada...'
        $btnEnter.Text='AGUARDANDO ENTRAR NA SESSAO...'
        $players=@(Get-ServerPlayers $ep)
        if($players.Count-eq0){return}
        $driver=Resolve-AutomaticDriver $ep $players
'@
        if($raw.Contains($oldConnect)){$raw=$raw.Replace($oldConnect,$newConnect)}
    }

    $massStart=$raw.IndexOf('function Convert-TelemetryMassNumber($Value){')
    $massEnd=$raw.IndexOf('function Add-NormalizedTelemetryFields',$massStart)
    if($massStart-lt0 -or $massEnd-le$massStart){throw 'Bloco de leitura de peso nao encontrado.'}

    $newMass=@'
function Convert-TelemetryMassNumber($Value){
    if($null-eq$Value){return $null}
    try{
        $txt=([string]$Value).Trim().Replace(',','.')
        $n=[double]::Parse($txt,[System.Globalization.CultureInfo]::InvariantCulture)
        if($n-lt0){$n=[Math]::Abs($n)}
        if($n-gt0){return $n}
    }catch{
        try{$n=[Math]::Abs([double]$Value);if($n-gt0){return $n}}catch{}
    }
    return $null
}

$script:Ets2SaveCachePath=''
$script:Ets2SaveCacheChecked=[datetime]::MinValue
$script:Ets2ActiveProfilePath=''

function Get-Ets2ActiveSaveFile {
    try{
        if($script:Ets2SaveCachePath -and (Test-Path $script:Ets2SaveCachePath) -and ((Get-Date)-$script:Ets2SaveCacheChecked).TotalSeconds-lt8){return $script:Ets2SaveCachePath}
        $script:Ets2SaveCacheChecked=Get-Date
        $docs=[Environment]::GetFolderPath('MyDocuments')
        if([string]::IsNullOrWhiteSpace($docs)){return ''}
        $ets=Join-Path $docs 'Euro Truck Simulator 2'
        $roots=@((Join-Path $ets 'profiles'),(Join-Path $ets 'steam_profiles'))
        $files=@()
        foreach($root in $roots){
            if(!(Test-Path $root)){continue}
            foreach($f in @(Get-ChildItem -Path $root -Filter 'game.sii' -File -Recurse -ErrorAction SilentlyContinue)){
                if($f.FullName -match '[\\/]+save[\\/]+'){$files+=$f}
            }
        }
        if($files.Count-eq0){return ''}
        $best=$files|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
        $script:Ets2SaveCachePath=$best.FullName
        return $best.FullName
    }catch{return ''}
}

function Get-Ets2ProfileCargoMassKg {
    try{
        $save=Get-Ets2ActiveSaveFile
        if(!$save -or !(Test-Path $save)){return $null}
        $fs=[System.IO.File]::Open($save,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::ReadWrite)
        try{$buf=New-Object byte[] 4;[void]$fs.Read($buf,0,4);$sig=[System.Text.Encoding]::ASCII.GetString($buf)}finally{$fs.Dispose()}
        if($sig-eq'ScsC'){return $null}
        $text=[System.IO.File]::ReadAllText($save)
        $matches=[regex]::Matches($text,'(?im)^\s*cargo_mass\s*:\s*([0-9]+(?:[\.,][0-9]+)?)\s*$')
        for($i=$matches.Count-1;$i-ge0;$i--){
            $n=Convert-TelemetryMassNumber $matches[$i].Groups[1].Value
            if($null-ne$n){return $n}
        }
    }catch{}
    return $null
}

function Get-TelemetryMassKg($Telemetry){
    if($null-ne$Telemetry){
        $paths=@(
            'trailer.mass','trailerMass','trailer.cargoMass','trailer.cargo_mass',
            'mass_kg','cargoMass','cargo_mass','cargoMassKg','cargo_mass_kg','cargoWeight','cargo_weight','weight_kg',
            'job.mass','job.mass_kg','job.cargoMass','job.cargo_mass','job.cargoMassKg','job.cargo_mass_kg','job.cargoWeight','job.weight',
            'job.cargo.mass','job.cargo.mass_kg','job.cargo.massKg','job.cargo.weight',
            'cargo.mass','cargo.mass_kg','cargo.massKg','cargo.weight','game.job.cargoMass','game.job.mass'
        )
        foreach($p in $paths){
            $n=Convert-TelemetryMassNumber (Get-ObjectPathValue $Telemetry @($p))
            if($null-ne$n){return $n}
        }
        $um=Convert-TelemetryMassNumber (Get-ObjectPathValue $Telemetry @('job.cargo.unit.mass','job.cargo.unitMass','cargo.unit.mass','cargoUnitMass'))
        $uc=Convert-TelemetryMassNumber (Get-ObjectPathValue $Telemetry @('job.cargo.unit.count','job.cargo.unitCount','cargo.unit.count','cargoUnitCount'))
        if($null-ne$um -and $null-ne$uc){return ($um*$uc)}
    }
    $profileMass=Get-Ets2ProfileCargoMassKg
    if($null-ne$profileMass){return $profileMass}
    return $null
}
'@

    $raw=$raw.Substring(0,$massStart)+$newMass+"`r`n"+$raw.Substring($massEnd)

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $check=Get-Content $target -Raw -Encoding UTF8
    if(!$check.Contains('$AppVersion=''1.8.3''')){throw 'Falha ao gravar a versao 1.8.3.'}
    if(!$check.Contains('Get-Ets2ProfileCargoMassKg')){throw 'Falha ao instalar leitura do perfil ETS2.'}
    if(!$check.Contains('trailer.mass')){throw 'Falha ao instalar leitura trailer.mass.'}

    $launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
    if(Test-Path $launcher){Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null}
    Msg 'Atualizacao 1.8.3 instalada com sucesso.'
}catch{
    try{
        if(Test-Path $backup){
            Copy-Item $backup $target -Force
            $h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
            Set-Content -Path $hashPath -Value $h -Encoding ASCII -Force
        }
    }catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.3.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
