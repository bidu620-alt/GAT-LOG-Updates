param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.58 nao encontrado para versao 1.0.59.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# 1.0.59 preserva o layout geral do GAT Telemetria.
# A alteracao funcional fica somente nos assets HTML/CSS do GAT DASH.
$mainText = $mainText.Replace('CurrentVersion = "1.0.58.0"', 'CurrentVersion = "1.0.59.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.58"', 'Text = "Cliente 1.0.59"')
$mainText = $mainText.Replace('HUB 1.0.58:', 'HUB 1.0.59:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.58', 'GAT Telemetria BETA 1.0.59')
$hubText = $hubText.Replace('Cliente 1.0.58 TESTE', 'Cliente 1.0.59 TESTE')

if ($mainText -notlike '*CurrentVersion = "1.0.59.0"*') { throw 'Versao 1.0.59 nao aplicada.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.59: somente identificacao da versao; layout geral preservado.'
