# BiduLog - instalador seguro do cargo Streamer (Central local Windows).
# Executar em PowerShell como administrador. Nao baixa nem substitui central.sqlite.
$ErrorActionPreference='Stop'
$runtime='C:\ProgramData\GAT-LOG Server\central'
$node=Join-Path $runtime 'node.exe'
$hostScript=Join-Path $runtime 'host.mjs'
$files=@('worker.js','economy-official.js','economy-schema.sql')
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Abra o PowerShell COMO ADMINISTRADOR.'}
if(-not (Test-Path -LiteralPath $node)){throw "Node da Central nao encontrado: $node"}
if(-not (Test-Path -LiteralPath $hostScript)){throw "host.mjs da Central nao encontrado: $hostScript"}
foreach($name in $files){if(-not (Test-Path -LiteralPath (Join-Path $runtime $name))){throw "Arquivo da Central ausente: $name"}}
$dbfile=Join-Path $env:LOCALAPPDATA 'GAT-LOG\Central\central.sqlite'
if(-not (Test-Path -LiteralPath $dbfile)){throw "Banco da Central nao encontrado: $dbfile"}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backup=Join-Path $env:LOCALAPPDATA ("GAT-LOG\Backups\streamer-"+$stamp)
$stage=Join-Path $env:TEMP ("bidulog-streamer-"+$stamp)
New-Item -ItemType Directory -Path $stage,$backup -Force | Out-Null
$patch=Join-Path $stage 'apply-streamer-central.mjs'
$url='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/642d1e5a0ec9fa3366844d49b309229dc3081afb/scripts/apply-streamer-central.mjs'
Write-Host 'Preparando pacote Streamer sem substituir os arquivos existentes...' -ForegroundColor Cyan
Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $patch -TimeoutSec 35
& $node --check $patch
if($LASTEXITCODE -ne 0){throw 'O atualizador baixado nao passou na verificacao de sintaxe.'}
& $node $patch $runtime $stage
if($LASTEXITCODE -ne 0){throw 'Os arquivos nao sao compativeis. Nenhuma alteracao foi aplicada.'}
foreach($name in @('worker.js','economy-official.js')){
 & $node --check (Join-Path $stage $name)
 if($LASTEXITCODE -ne 0){throw "Erro de sintaxe no arquivo $name. Nada foi alterado."}
}
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $runtime $name) -Destination (Join-Path $backup $name) -Force}
$listener=Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
$running=$false
if($listener){
 $proc=Get-Process -Id $listener.OwningProcess -ErrorAction Stop
 if([IO.Path]::GetFullPath($proc.Path) -ine [IO.Path]::GetFullPath($node)){
   throw 'A porta 5056 nao pertence ao node.exe desta Central. Nada foi alterado.'
 }
 $running=$true
}
$copied=$false
try{
 if($running){
   Write-Host 'Parando temporariamente a Central para backup seguro...' -ForegroundColor Cyan
   Stop-Process -Id $listener.OwningProcess -Force -ErrorAction Stop
   Start-Sleep -Seconds 2
 }
 if(Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue){throw 'Porta 5056 ainda ocupada. Nada foi alterado.'}
 Write-Host 'Fazendo backup SQLite oficial da Central...' -ForegroundColor Cyan
 $dbBackup=(& $node $hostScript 'backup' 2>&1)
 if($LASTEXITCODE -ne 0){throw ('Backup do banco falhou: '+($dbBackup -join ' '))}
 Write-Host ('Backup do banco: '+($dbBackup -join ' ')) -ForegroundColor Green
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $runtime $name) -Force}
 $copied=$true
 Write-Host 'Reiniciando a Central...' -ForegroundColor Cyan
 Start-Process -FilePath $node -ArgumentList 'host.mjs' -WorkingDirectory $runtime -WindowStyle Hidden | Out-Null
 $healthy=$false
 for($attempt=0;$attempt -lt 20;$attempt++){
   Start-Sleep -Seconds 2
   try{
     $r=Invoke-RestMethod -Uri 'http://127.0.0.1:5056/health' -TimeoutSec 5
     if($r.ok -eq $true){$healthy=$true;break}
   }catch{}
 }
 if(-not $healthy){throw 'A Central nao respondeu na porta 5056 apos a atualizacao.'}
 Write-Host 'SUCESSO: Central atualizada e funcionando. Cargo Streamer liberado.' -ForegroundColor Green
 Write-Host ('Backup dos arquivos anteriores: '+$backup)
 Write-Host 'Abra https://bidulog.com.br/admin.html e pressione Ctrl+F5.'
}catch{
 $problem=$_.Exception.Message
 $current=Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
 if($current){
   $p=Get-Process -Id $current.OwningProcess -ErrorAction SilentlyContinue
   if($p -and $p.Path -and [IO.Path]::GetFullPath($p.Path) -ieq [IO.Path]::GetFullPath($node)){
     Stop-Process -Id $current.OwningProcess -Force -ErrorAction SilentlyContinue
     Start-Sleep -Seconds 2
   }
 }
 if($copied){foreach($name in $files){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $runtime $name) -Force}}
 if($running){Start-Process -FilePath $node -ArgumentList 'host.mjs' -WorkingDirectory $runtime -WindowStyle Hidden | Out-Null}
 throw ('Falha ao aplicar Streamer. Arquivos originais restaurados: '+$problem)
}
