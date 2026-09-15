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

# Versao 1.0.54 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.53.0"', 'CurrentVersion = "1.0.54.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.53"', 'Text = "Cliente 1.0.54"')
$mainText = $mainText.Replace('HUB 1.0.53:', 'HUB 1.0.54:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.53', 'GAT Telemetria BETA 1.0.54')
$hubText = $hubText.Replace('Cliente 1.0.53 TESTE', 'Cliente 1.0.54 TESTE')

# GAT_OVERLAY_INDEPENDENT_054
# As sobreposicoes eram abertas com Show(this), ficando como janelas "owned" pelo
# GAT Telemetria. No Windows, ao minimizar a janela proprietaria, as janelas owned
# tambem somem. Elas agora sao janelas independentes TopMost e continuam visiveis.
$oldTruck = 'if (!_truckOverlay041.Visible) _truckOverlay041.Show(this);'
$newTruck = 'if (!_truckOverlay041.Visible) _truckOverlay041.Show();'
$oldVideo = 'if (!_videoOverlay041.Visible) _videoOverlay041.Show(this);'
$newVideo = 'if (!_videoOverlay041.Visible) _videoOverlay041.Show();'

if (-not $hubText.Contains($oldTruck)) { throw 'Abertura da sobreposicao do caminhao nao encontrada.' }
if (-not $hubText.Contains($oldVideo)) { throw 'Abertura da sobreposicao de video nao encontrada.' }
$hubText = $hubText.Replace($oldTruck, $newTruck)
$hubText = $hubText.Replace($oldVideo, $newVideo)

# Reforca TopMost ao abrir/reabrir e mantem sem botao na barra de tarefas.
$hubText = $hubText.Replace(
    'ApplyOverlayOpacity041(); if (!_truckOverlay041.Visible) _truckOverlay041.Show(); _truckOverlay041.BringToFront();',
    'ApplyOverlayOpacity041(); _truckOverlay041.TopMost = true; _truckOverlay041.ShowInTaskbar = false; if (!_truckOverlay041.Visible) _truckOverlay041.Show(); _truckOverlay041.BringToFront();'
)
$hubText = $hubText.Replace(
    'ApplyOverlayOpacity041(); if (!_videoOverlay041.Visible) _videoOverlay041.Show(); _videoOverlay041.BringToFront();',
    'ApplyOverlayOpacity041(); _videoOverlay041.TopMost = true; _videoOverlay041.ShowInTaskbar = false; if (!_videoOverlay041.Visible) _videoOverlay041.Show(); _videoOverlay041.BringToFront();'
)

if ($mainText -notlike '*CurrentVersion = "1.0.54.0"*') { throw 'Versao 1.0.54 nao aplicada.' }
if ($hubText -like '*_truckOverlay041.Show(this)*' -or $hubText -like '*_videoOverlay041.Show(this)*') { throw 'Ainda existe overlay vinculado a janela principal.' }
foreach ($m in @('_truckOverlay041.Show();','_videoOverlay041.Show();','_truckOverlay041.TopMost = true','_videoOverlay041.TopMost = true')) {
    if ($hubText -notlike "*$m*") { throw "Overlay 1.0.54 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.54: overlays independentes da janela principal; minimizar o app nao esconde as sobreposicoes.'
