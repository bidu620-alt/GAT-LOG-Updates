$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao 1.8.4'){
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
    $raw=[regex]::Replace($raw,$versionPattern,'$AppVersion=''1.8.4''',1)

    $start=$raw.IndexOf('function Get-TelemetryMassKg($Telemetry){')
    if($start-lt0){throw 'Funcao Get-TelemetryMassKg nao encontrada.'}
    $end=$raw.IndexOf('function Add-NormalizedTelemetryFields',$start)
    if($end-le$start){throw 'Fim da funcao de peso nao encontrado.'}

    $newMass=@'
function Get-TelemetryMassKg($Telemetry){
    if($null-eq$Telemetry){return $null}

    # TruckSim GPS Server v5: este e o campo real da massa da carga.
    try{
        $direct=Convert-TelemetryMassNumber $Telemetry.job.cargoMass
        if($null-ne$direct -and $direct-gt0){return $direct}
    }catch{}

    # Compatibilidade com nomes/caminhos de outras versoes de telemetria.
    $paths=@(
        'job.cargoMass','Job.CargoMass',
        'mass_kg','cargoMass','cargo_mass','cargoMassKg','cargo_mass_kg','cargoWeight','cargo_weight','weight_kg',
        'job.mass','job.mass_kg','job.cargo_mass','job.cargoMassKg','job.cargo_mass_kg','job.cargoWeight','job.weight',
        'job.cargo.mass','job.cargo.mass_kg','job.cargo.massKg','job.cargo.weight',
        'cargo.mass','cargo.mass_kg','cargo.massKg','cargo.weight',
        'trailer.mass','trailerMass','trailer.cargoMass','trailer.cargo_mass','game.job.cargoMass','game.job.mass'
    )
    foreach($p in $paths){
        try{
            $v=Get-ObjectPathValue $Telemetry @($p)
            $n=Convert-TelemetryMassNumber $v
            if($null-ne$n -and $n-gt0){return $n}
        }catch{}
    }

    try{
        $profileMass=Get-Ets2ProfileCargoMassKg
        if($null-ne$profileMass -and $profileMass-gt0){return $profileMass}
    }catch{}
    return $null
}

'@
    $raw=$raw.Substring(0,$start)+$newMass+$raw.Substring($end)

    # Corrige a exibicao: peso local positivo tem prioridade; zero do servidor e ignorado.
    $oldDisplay=@'
            $mass=$null
            try{$mass=[double]$r.Json.mass_kg}catch{}
            if($null-eq$mass){try{$mass=[double]$r.Json.cargo_mass}catch{}}
            if($null-eq$mass){$mass=Get-TelemetryMassKg $tele}
            $peso=Format-MassKg $mass
'@
    $newDisplay=@'
            $mass=Get-TelemetryMassKg $tele
            if($null-eq$mass -or [double]$mass-le0){
                $mass=$null
                try{$sm=[double]$r.Json.mass_kg;if($sm-gt0){$mass=$sm}}catch{}
                if($null-eq$mass){try{$sm=[double]$r.Json.cargo_mass;if($sm-gt0){$mass=$sm}}catch{}}
            }
            $peso=Format-MassKg $mass
'@
    if($raw.Contains($oldDisplay)){
        $raw=$raw.Replace($oldDisplay,$newDisplay)
    } else {
        $displayPattern='(?ms)^\s*\$mass=\$null\s*\r?\n\s*try\{\$mass=\[double\]\$r\.Json\.mass_kg\}catch\{\}\s*\r?\n\s*if\(\$null-eq\$mass\)\{try\{\$mass=\[double\]\$r\.Json\.cargo_mass\}catch\{\}\}\s*\r?\n\s*if\(\$null-eq\$mass\)\{\$mass=Get-TelemetryMassKg \$tele\}\s*\r?\n\s*\$peso=Format-MassKg \$mass'
        if([regex]::IsMatch($raw,$displayPattern)){
            $raw=[regex]::Replace($raw,$displayPattern,$newDisplay.TrimEnd(),1)
        }
    }

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $check=Get-Content $target -Raw -Encoding UTF8
    if(!$check.Contains('$AppVersion=''1.8.4''')){throw 'Falha ao gravar a versao 1.8.4.'}
    if(!$check.Contains('$Telemetry.job.cargoMass')){throw 'Falha ao instalar leitura direta de job.cargoMass.'}

    $launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
    if(Test-Path $launcher){Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null}
    Msg 'Atualizacao 1.8.4 instalada. O peso agora usa diretamente job.cargoMass do TruckSim GPS e ignora valor zero do servidor.'
}catch{
    try{
        if(Test-Path $backup){
            Copy-Item $backup $target -Force
            $h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
            Set-Content -Path $hashPath -Value $h -Encoding ASCII -Force
        }
    }catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.4.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
