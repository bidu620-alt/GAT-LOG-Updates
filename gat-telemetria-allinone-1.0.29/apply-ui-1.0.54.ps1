param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.53 incompleto para aplicar 1.0.54.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.53.0"', 'CurrentVersion = "1.0.54.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.53"', 'Text = "Cliente 1.0.54"')
$mainText = $mainText.Replace('HUB 1.0.53:', 'HUB 1.0.54:')

# Corrige tambem o titulo da janela e o rodape, que podiam ficar mostrando uma versao antiga.
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+";', 'Text = "GAT Telemetria BETA 1.0.54";', 1)
$hubText = [regex]::Replace($hubText, 'Cliente\s+1\.0\.\d+\s+TESTE', 'Cliente 1.0.54 TESTE')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.53', 'GAT Telemetria BETA 1.0.54')

if ($mainText -notlike '*CurrentVersion = "1.0.54.0"*') { throw 'Versao 1.0.54 nao aplicada no MainForm.' }
if ($hubText -notlike '*GAT Telemetria BETA 1.0.54*' -or $hubText -notlike '*Cliente 1.0.54 TESTE*') { throw 'Titulo/rodape 1.0.54 nao aplicados.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.54: versao, titulo e rodape atualizados.'
