$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao 1.8.2'){
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

    # Aceita variacoes normais de formatacao da linha de versao.
    $versionPattern='(?m)^\s*\$AppVersion\s*=\s*[''"]1\.8(?:\.0|\.1)?[''"]\s*$'
    $alreadyPattern='(?m)^\s*\$AppVersion\s*=\s*[''"]1\.8\.2[''"]\s*$'
    if($raw -match $alreadyPattern){
        $raw2=$raw
    } elseif($raw -match $versionPattern){
        $raw2=[regex]::Replace($raw,$versionPattern,"`$AppVersion='1.8.2'",1)
    } else {
        $found=[regex]::Match($raw,'(?m)^\s*\$AppVersion\s*=\s*.+$')
        $detail=if($found.Success){$found.Value.Trim()}else{'linha $AppVersion nao encontrada'}
        throw ("Base do cliente nao reconhecida. Encontrado: "+$detail)
    }
    $raw=$raw2

    $old=@'
        $lblLogin.Text='ETS2 aberto. Aguardando voce entrar na sessao selecionada...'
        $btnEnter.Text='AGUARDANDO ENTRAR NA SESSAO...'
        $players=@(Get-ServerPlayers $ep)
        if($players.Count-eq0){return}
        $driver=Resolve-AutomaticDriver $ep $players
'@

    $new=@'
        $lblLogin.Text='ETS2 aberto. Verificando se voce ja esta na sessao selecionada...'
        $btnEnter.Text='VERIFICANDO SESSAO...'

        # Tenta primeiro os motoristas ja vinculados neste PC para este servidor.
        # O proprio GAT LOG confirma se o motorista esta na sessao agora. Assim o
        # cliente conecta mesmo quando foi aberto depois de voce ja entrar no comboio.
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

    if($raw.Contains($old)){
        $raw=$raw.Replace($old,$new)
    } elseif($raw -notmatch 'Tenta primeiro os motoristas ja vinculados neste PC') {
        # Segunda tentativa tolerando espacos/indentacao diferentes.
        $connPattern="(?ms)^\s*\`$lblLogin\.Text='ETS2 aberto\. Aguardando voce entrar na sessao selecionada\.\.\.'\s*\r?\n\s*\`$btnEnter\.Text='AGUARDANDO ENTRAR NA SESSAO\.\.\.'\s*\r?\n\s*\`$players=@\(Get-ServerPlayers \`$ep\)\s*\r?\n\s*if\(\`$players\.Count-eq0\)\{return\}\s*\r?\n\s*\`$driver=Resolve-AutomaticDriver \`$ep \`$players"
        if([regex]::IsMatch($raw,$connPattern)){
            $raw=[regex]::Replace($raw,$connPattern,$new.TrimEnd(),1)
        } else {
            throw 'Trecho de conexao automatica esperado nao foi encontrado. O arquivo foi preservado.'
        }
    }

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $check=Get-Content $target -Raw -Encoding UTF8
    if($check -notmatch $alreadyPattern){throw 'Falha ao validar a versao depois da atualizacao.'}

    $launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
    if(Test-Path $launcher){Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null}
    Msg 'Atualizacao 1.8.2 instalada. Agora o cliente reconhece tambem o motorista que ja estava dentro da sessao antes de o aplicativo abrir.'
}catch{
    try{if(Test-Path $backup){Copy-Item $backup $target -Force;$h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant();Set-Content $hashPath $h -Encoding ASCII -Force}}catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.2.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
