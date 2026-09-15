param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.56 nao encontrado para versao 1.0.57.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# 1.0.57 altera somente a identificacao da versao no executavel.
# O layout geral do GAT Telemetria permanece exatamente o da 1.0.56.
$mainText = $mainText.Replace('CurrentVersion = "1.0.56.0"', 'CurrentVersion = "1.0.57.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.56"', 'Text = "Cliente 1.0.57"')
$mainText = $mainText.Replace('HUB 1.0.56:', 'HUB 1.0.57:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.56', 'GAT Telemetria BETA 1.0.57')
$hubText = $hubText.Replace('Cliente 1.0.56 TESTE', 'Cliente 1.0.57 TESTE')

if ($mainText -notlike '*CurrentVersion = "1.0.57.0"*') { throw 'Versao 1.0.57 nao aplicada.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.57: somente versao alterada; layout geral preservado da 1.0.56.'
