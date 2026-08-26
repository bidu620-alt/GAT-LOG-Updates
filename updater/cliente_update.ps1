$ErrorActionPreference='Stop'
$ManifestUrl='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/manifests/cliente.json'

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao'){
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}

try{
    $m=Invoke-RestMethod -Uri ($ManifestUrl+'?t='+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -Headers @{'Cache-Control'='no-cache'} -TimeoutSec 12
    if(!$m.patch_url -or !$m.patch_sha256){throw 'A versao foi anunciada, mas o pacote de atualizacao ainda nao esta publicado.'}
    $tmp=Join-Path $env:TEMP ('gatlog_patch_cliente_'+[Guid]::NewGuid().ToString('N')+'.ps1')
    Invoke-WebRequest -UseBasicParsing -Uri ([string]$m.patch_url) -OutFile $tmp -TimeoutSec 20
    $hash=(Get-FileHash -Algorithm SHA256 -Path $tmp).Hash.ToLowerInvariant()
    if($hash -ne ([string]$m.patch_sha256).ToLowerInvariant()){Remove-Item $tmp -Force -ErrorAction SilentlyContinue;throw 'Falha de integridade no pacote de atualizacao.'}
    Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$tmp+'"')) -WindowStyle Hidden|Out-Null
}catch{Msg ("Nao foi possivel iniciar a atualizacao.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha na atualizacao'}
