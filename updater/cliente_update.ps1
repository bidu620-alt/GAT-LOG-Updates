$ErrorActionPreference='Stop'
$ManifestUrl='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/manifests/cliente.json'

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao'){
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}

try{
    $m=Invoke-RestMethod -Uri ($ManifestUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -Headers @{'Cache-Control'='no-cache'} -TimeoutSec 10

    # Preferencia: patch pequeno e assinado. Evita baixar/regravar o cliente inteiro.
    $patchUrl=[string]$m.patch_url
    $patchExpected=([string]$m.patch_sha256).Trim().ToLowerInvariant()
    if(![string]::IsNullOrWhiteSpace($patchUrl) -and ![string]::IsNullOrWhiteSpace($patchExpected)){
        $tmp=Join-Path $env:TEMP ('gat_cliente_patch_'+[Guid]::NewGuid().ToString('N')+'.ps1')
        Invoke-WebRequest -UseBasicParsing -Uri ($patchUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -OutFile $tmp -TimeoutSec 20
        $hash=(Get-FileHash -Algorithm SHA256 -Path $tmp).Hash.ToLowerInvariant()
        if($hash -ne $patchExpected){Remove-Item $tmp -Force -ErrorAction SilentlyContinue;throw 'Falha de integridade no patch baixado do GitHub.'}
        Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$tmp+'"')) -WindowStyle Normal|Out-Null
        exit 0
    }

    # Compatibilidade: atualizacao pelo arquivo principal completo.
    $url=[string]$m.script_url
    $expected=([string]$m.script_sha256).Trim().ToLowerInvariant()
    if([string]::IsNullOrWhiteSpace($url) -or [string]::IsNullOrWhiteSpace($expected)){
        throw 'A nova versao ainda nao possui pacote de atualizacao publicado.'
    }

    $tmp=Join-Path $env:TEMP ('gat_cliente_'+[Guid]::NewGuid().ToString('N')+'.ps1')
    Invoke-WebRequest -UseBasicParsing -Uri ($url+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -OutFile $tmp -TimeoutSec 20
    $hash=(Get-FileHash -Algorithm SHA256 -Path $tmp).Hash.ToLowerInvariant()
    if($hash -ne $expected){Remove-Item $tmp -Force -ErrorAction SilentlyContinue;throw 'Falha de integridade: o arquivo baixado nao corresponde ao publicado no GitHub.'}

    $dir=Join-Path $env:LOCALAPPDATA 'GAT Telemetria Cliente'
    New-Item -ItemType Directory -Path $dir -Force|Out-Null
    $target=Join-Path $dir 'GAT_Telemetria_Cliente.ps1'
    $hashPath=Join-Path $dir 'client_integrity.sha256'
    $backup=Join-Path $dir ('GAT_Telemetria_Cliente.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.ps1')
    $hashBackup=Join-Path $dir ('client_integrity.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.sha256')

    Start-Sleep -Milliseconds 900
    if(Test-Path $target){Copy-Item $target $backup -Force}
    if(Test-Path $hashPath){Copy-Item $hashPath $hashBackup -Force}
    Move-Item $tmp $target -Force
    Set-Content -Path $hashPath -Value $expected -Encoding ASCII -Force

    $installed=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    if($installed -ne $expected){throw 'Falha de integridade depois da instalacao.'}

    $launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
    if(Test-Path $launcher){Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null}
    else{Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$target+'"')) -WorkingDirectory $dir|Out-Null}
}catch{
    Msg ("Nao foi possivel concluir a atualizacao.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha na atualizacao'
}
