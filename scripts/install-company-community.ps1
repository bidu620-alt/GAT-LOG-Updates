$ErrorActionPreference='Stop'
$runtime='C:\ProgramData\GAT-LOG Server\central'
$worker=Join-Path $runtime 'worker.js'
$package=Join-Path $PSScriptRoot 'empresas-central'
$sourcePackage=Join-Path (Split-Path $PSScriptRoot -Parent) 'central-economy'
if(!(Test-Path -LiteralPath $package) -and (Test-Path -LiteralPath $sourcePackage)){$package=$sourcePackage}
$taskWork=Join-Path $PSScriptRoot 'empresas-backups'
New-Item -ItemType Directory -Path $taskWork -Force | Out-Null
$backup=Join-Path $taskWork ('worker-before-empresas-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.js')
$node=Join-Path $runtime 'node.exe'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Abra o PowerShell como administrador e execute novamente.'}
$source=[IO.File]::ReadAllText($worker)
Copy-Item -LiteralPath $worker -Destination $backup
foreach($name in @('company-community.js','company-community.sql')){Copy-Item -LiteralPath (Join-Path $package $name) -Destination (Join-Path $runtime $name) -Force}
$import="import {ensureCompanies,companyPublic,companyAction} from './company-community.js';"
if(!$source.Contains($import)){$source=$import+"`n"+$source}
$anchor=" if(p.startsWith('/api/economy/')&&m==='POST'){"
if(!$source.Contains(" if(p.startsWith('/api/companies/')){")){
 if(!$source.Contains($anchor)){throw 'A estrutura da central mudou. Nenhuma substituicao foi feita.'}
 $routesPath=Join-Path $package 'company-routes.txt'
 if(Test-Path -LiteralPath $routesPath){$routes=[IO.File]::ReadAllText($routesPath)}else{
  $feature=[IO.File]::ReadAllText((Join-Path $package 'worker.js'))
  $start=$feature.IndexOf(" if(p.startsWith('/api/companies/')){")
  $end=$feature.IndexOf(" if(p.startsWith('/api/economy/')",$start)
  if($start -lt 0 -or $end -le $start){throw 'Rotas da comunidade nao encontradas no pacote.'}
  $routes=$feature.Substring($start,$end-$start)
 }
 $source=$source.Replace($anchor,$routes+$anchor)
}
$source=$source.Replace("try{return json(req,economyAction(env.DB,session,p.slice('/api/economy/'.length),b))}","try{ensureCompanies(env.DB);return json(req,economyAction(env.DB,session,p.slice('/api/economy/'.length),b))}")
$staged=Join-Path $runtime 'worker-company-staged.js'
[IO.File]::WriteAllText($staged,$source,[Text.UTF8Encoding]::new($false))
& $node --check $staged
if($LASTEXITCODE -ne 0){throw 'A central nao passou na validacao. A versao anterior foi preservada.'}
[IO.File]::WriteAllText($worker,$source,[Text.UTF8Encoding]::new($false))
$listener=Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
if($listener){
 $centralProcess=Get-Process -Id $listener.OwningProcess -ErrorAction Stop
 if($centralProcess.Path -ne $node){Copy-Item -LiteralPath $backup -Destination $worker -Force;throw 'A porta 5056 pertence a outro processo. A versao anterior foi restaurada.'}
}
try {
 if($listener){Stop-Process -Id $listener.OwningProcess -Force}
 Start-Process -FilePath $node -ArgumentList 'host.mjs' -WorkingDirectory $runtime -WindowStyle Hidden
 $confirmed=$false
 for($attempt=0;$attempt -lt 20;$attempt++){
  Start-Sleep -Seconds 2
  try{$result=Invoke-RestMethod 'http://127.0.0.1:5056/api/companies/directory?scope=total' -TimeoutSec 5;if($result.ok){$confirmed=$true;break}}catch{}
 }
 if(!$confirmed){throw 'As novas rotas da central nao responderam.'}
 Write-Host 'SUCESSO! Empresas, ranking, comunidade e RH ativados.' -ForegroundColor Green
 Write-Host 'Abra https://gatlogets2.com.br/empresas.html e atualize com Ctrl+F5.'
}catch{
 $problem=$_.Exception.Message
 $current=Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
 if($current){$process=Get-Process -Id $current.OwningProcess -ErrorAction SilentlyContinue;if($process.Path -eq $node){Stop-Process -Id $current.OwningProcess -Force}}
 Copy-Item -LiteralPath $backup -Destination $worker -Force
 Start-Process -FilePath $node -ArgumentList 'host.mjs' -WorkingDirectory $runtime -WindowStyle Hidden
 throw ('Nao foi possivel ativar. A versao anterior da central foi restaurada: '+$problem)
}
