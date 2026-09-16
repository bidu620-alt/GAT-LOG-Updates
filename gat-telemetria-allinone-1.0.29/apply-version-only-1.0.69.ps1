param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.68 nao encontrado para versao 1.0.69.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# 1.0.69 preserva integralmente a logica validada da 1.0.68.
# Esta etapa altera somente a identificacao da versao.
$mainText = $mainText.Replace('CurrentVersion = "1.0.68.0"', 'CurrentVersion = "1.0.69.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68"', 'Text = "Cliente 1.0.69"')
$mainText = $mainText.Replace('HUB 1.0.68:', 'HUB 1.0.69:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.69')
$hubText = $hubText.Replace('Cliente 1.0.68 TESTE', 'Cliente 1.0.69 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.69')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.68 TESTE', 'Cliente: 1.0.69 TESTE')
}

if ($mainText -notlike '*CurrentVersion = "1.0.69.0"*') { throw 'Versao 1.0.69 nao aplicada.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.69: somente identificacao da versao; logica 1.0.68 preservada.'
