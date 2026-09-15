param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.57 nao encontrado para versao 1.0.58.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# 1.0.58 continua preservando o layout geral do GAT Telemetria.
# A unica mudanca funcional nova fica no HTML/CSS do GAT DASH empacotado.
$mainText = $mainText.Replace('CurrentVersion = "1.0.57.0"', 'CurrentVersion = "1.0.58.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.57"', 'Text = "Cliente 1.0.58"')
$mainText = $mainText.Replace('HUB 1.0.57:', 'HUB 1.0.58:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.57', 'GAT Telemetria BETA 1.0.58')
$hubText = $hubText.Replace('Cliente 1.0.57 TESTE', 'Cliente 1.0.58 TESTE')

if ($mainText -notlike '*CurrentVersion = "1.0.58.0"*') { throw 'Versao 1.0.58 nao aplicada.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.58: somente identificacao da versao; layout geral preservado.'
