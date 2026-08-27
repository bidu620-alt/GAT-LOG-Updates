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

    # Aceita qualquer 1.8.x anterior e tambem reaplicacao segura da 1.8.3.
    $verLine=[regex]::Match($raw,'(?m)^\s*\$AppVersion\s*=\s*[''"]([^''"]+)[''"]\s*$')
    if(!$verLine.Success){throw 'Linha $AppVersion nao encontrada no cliente.'}
    $installed=[string]$verLine.Groups[1].Value
    if($installed -notmatch '^1\.8(?:\.\d+)?$'){throw ("Versao instalada nao pertence a base 1.8.x: "+$installed)}
    $raw=[regex]::Replace($raw,'(?m)^\s*\$AppVersion\s*=\s*[''"]1\.8(?:\.\d+)?[''"]\s*$',"`$AppVersion='1.8.3'",1)

    # A partir desta versao o processo auxiliar de atualizacao roda oculto.
    $raw=$raw.Replace(
        "Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"')) | Out-Null",
        "Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"')) -WindowStyle Hidden | Out-Null"
    )

    # Mantem a correcao da 1.8.2 mesmo para quem estiver vindo direto da 1.8/1.8.1.
    if($raw -notmatch 'Tenta primeiro os motoristas ja vinculados neste PC'){
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

        # Tenta primeiro os motoristas ja vinculados neste PC para este servidor.
        # O proprio GAT LOG confirma se o motorista esta na sessao agora.
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
        if($raw.Contains($oldConnect)){
            $raw=$raw.Replace($oldConnect,$newConnect)
        } else {
            $connPattern="(?ms)^\s*\`$lblLogin\.Text='ETS2 aberto\. Aguardando voce entrar na sessao selecionada\.\.\.'\s*\r?\n\s*\`$btnEnter\.Text='AGUARDANDO ENTRAR NA SESSAO\.\.\.'\s*\r?\n\s*\`$players=@\(Get-ServerPlayers \`$ep\)\s*\r?\n\s*if\(\`$players\.Count-eq0\)\{return\}\s*\r?\n\s*\`$driver=Resolve-AutomaticDriver \`$ep \`$players"
            if([regex]::IsMatch($raw,$connPattern)){
                $raw=[regex]::Replace($raw,$connPattern,$newConnect.TrimEnd(),1)
            }
        }
    }

    $massBlock=@'
$script:Ets2SaveCachePath=''
$script:Ets2SaveCacheChecked=[datetime]::MinValue
$script:Ets2ActiveProfilePath=''

function Convert-GatMassKg($Value){
    if($null-eq$Value){return $null}
    try{
        $txt=([string]$Value).Trim()
        if([string]::IsNullOrWhiteSpace($txt)){return $null}
        $txt=$txt.Replace(',','.')
        $n=[double]::Parse($txt,[System.Globalization.CultureInfo]::InvariantCulture)
        if($n-lt0){$n=[Math]::Abs($n)}
        if($n-le0){return $null}
        return $n
    }catch{
        try{$n=[double]$Value;if($n-gt0){return $n}}catch{}
        return $null
    }
}

function Get-Ets2ActiveSaveFile {
    try{
        if($script:Ets2SaveCachePath -and (Test-Path $script:Ets2SaveCachePath) -and ((Get-Date)-$script:Ets2SaveCacheChecked).TotalSeconds -lt 8){
            return $script:Ets2SaveCachePath
        }
        $script:Ets2SaveCacheChecked=Get-Date
        $docs=[Environment]::GetFolderPath('MyDocuments')
        if([string]::IsNullOrWhiteSpace($docs)){return ''}
        $ets=Join-Path $docs 'Euro Truck Simulator 2'
        $roots=@((Join-Path $ets 'profiles'),(Join-Path $ets 'steam_profiles'))
        $candidates=@()
        foreach($root in $roots){
            if(!(Test-Path $root)){continue}
            try{
                $files=Get-ChildItem -Path $root -Filter 'game.sii' -File -Recurse -ErrorAction SilentlyContinue
                foreach($f in $files){
                    # Saves reais ficam abaixo de \save\. Evita arquivos estranhos com o mesmo nome.
                    if($f.FullName -notmatch '[\\/]+save[\\/]+'){continue}
                    $candidates+=$f
                }
            }catch{}
        }
        if($candidates.Count-eq0){$script:Ets2SaveCachePath='';$script:Ets2ActiveProfilePath='';return ''}
        $best=$candidates|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
        if($null-eq$best){return ''}
        $script:Ets2SaveCachePath=$best.FullName

        # Guarda internamente o caminho do perfil detectado.
        $p=$best.Directory
        while($null-ne$p -and $p.Parent -and $p.Parent.Name -ne 'profiles' -and $p.Parent.Name -ne 'steam_profiles'){$p=$p.Parent}
        if($null-ne$p -and $p.Parent -and ($p.Parent.Name -eq 'profiles' -or $p.Parent.Name -eq 'steam_profiles')){
            $script:Ets2ActiveProfilePath=$p.FullName
        }
        return $best.FullName
    }catch{return ''}
}

function Get-Ets2ProfileCargoMassKg {
    try{
        $save=Get-Ets2ActiveSaveFile
        if([string]::IsNullOrWhiteSpace($save) -or !(Test-Path $save)){return $null}

        # game.sii criptografado/compactado da SCS nao e texto; nesse caso apenas
        # deixamos o TruckSim como fonte, sem quebrar a telemetria.
        $fs=[System.IO.File]::Open($save,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::ReadWrite)
        try{
            $buf=New-Object byte[] 4
            [void]$fs.Read($buf,0,4)
            $sig=[System.Text.Encoding]::ASCII.GetString($buf)
        }finally{$fs.Dispose()}
        if($sig -eq 'ScsC'){return $null}

        $text=[System.IO.File]::ReadAllText($save)
        $matches=[regex]::Matches($text,'(?im)^\s*cargo_mass\s*:\s*([0-9]+(?:[\.,][0-9]+)?)\s*$')
        if($matches.Count-eq0){return $null}

        # Se houver mais de um cargo_mass no save, usa o ultimo valor positivo.
        for($i=$matches.Count-1;$i-ge0;$i--){
            $m=Convert-GatMassKg $matches[$i].Groups[1].Value
            if($null-ne$m -and $m-gt0){return $m}
        }
        return $null
    }catch{return $null}
}

function Get-TelemetryMassKg($Telemetry){
    # Funbit/TruckSim GPS: trailer.mass e a massa da carga em kg.
    if($null-ne$Telemetry){
        $paths=@(
            'trailer.mass',
            'mass_kg','cargoMass','cargo_mass','cargoMassKg','cargo_mass_kg','cargoWeight','cargo_weight','weight_kg',
            'job.mass','job.mass_kg','job.cargoMass','job.cargo_mass','job.cargoMassKg','job.cargo_mass_kg','job.cargoWeight','job.weight',
            'job.cargo.mass','job.cargo.mass_kg','job.cargo.massKg','job.cargo.weight',
            'cargo.mass','cargo.mass_kg','cargo.massKg','cargo.weight',
            'trailer.cargoMass','trailer.cargo_mass','game.job.cargoMass','game.job.mass'
        )
        foreach($path in $paths){
            $v=Get-ObjectPathValue $Telemetry @($path)
            $n=Convert-GatMassKg $v
            if($null-ne$n -and $n-gt0){return $n}
        }

        # Algumas APIs trazem peso unitario + quantidade.
        $unit=Get-ObjectPathValue $Telemetry @('cargo.unitMass','cargo.unit_mass','job.cargo.unitMass','job.cargo.unit_mass')
        $count=Get-ObjectPathValue $Telemetry @('cargo.units','cargo.unitCount','cargo.unit_count','job.cargo.units','job.cargo.unitCount')
        $un=Convert-GatMassKg $unit
        try{$ct=[double]$count}catch{$ct=0}
        if($null-ne$un -and $un-gt0 -and $ct-gt0){return ($un*$ct)}
    }

    # Fallback: perfil/save ativo do ETS2 quando a telemetria retorna zero.
    $profileMass=Get-Ets2ProfileCargoMassKg
    if($null-ne$profileMass -and $profileMass-gt0){return $profileMass}
    return $null
}

function Add-NormalizedTelemetryFields
'@

    $massPattern='(?ms)^function Get-TelemetryMassKg\(\$Telemetry\)\{.*?^function Add-NormalizedTelemetryFields'
    if([regex]::IsMatch($raw,$massPattern)){
        $raw=[regex]::Replace($raw,$massPattern,$massBlock,1)
    } elseif($raw -notmatch 'function Get-Ets2ProfileCargoMassKg') {
        throw 'Funcao de peso do cliente nao foi localizada. O arquivo original foi preservado.'
    }

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $check=Get-Content $target -Raw -Encoding UTF8
    if($check -notmatch "(?m)^\s*\`$AppVersion\s*=\s*['\"]1\.8\.3['\"]\s*$"){throw 'Falha ao validar a versao 1.8.3.'}
    if($check -notmatch 'trailer\.mass'){throw 'Falha ao validar a leitura trailer.mass.'}
    if($check -notmatch 'Get-Ets2ProfileCargoMassKg'){throw 'Falha ao validar o fallback do perfil ETS2.'}

    $launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
    if(Test-Path $launcher){Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null}
    Msg "Atualizacao 1.8.3 instalada.`r`n`r`n- PowerShell auxiliar oculto.`r`n- Peso tenta trailer.mass em tempo real.`r`n- Se vier zero, tenta o game.sii do perfil/save ativo."
}catch{
    try{
        if(Test-Path $backup){
            Copy-Item $backup $target -Force
            $h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
            Set-Content $hashPath $h -Encoding ASCII -Force
        }
    }catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.3.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
