param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $truck -or -not $video) { throw 'Fonte 1.0.53 incompleto para aplicar 1.0.54.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# Versao 1.0.54 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.53.0"', 'CurrentVersion = "1.0.54.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.53"', 'Text = "Cliente 1.0.54"')
$mainText = $mainText.Replace('HUB 1.0.53:', 'HUB 1.0.54:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.53', 'GAT Telemetria BETA 1.0.54')
$hubText = $hubText.Replace('Cliente 1.0.53 TESTE', 'Cliente 1.0.54 TESTE')

# GAT_OVERLAY_INDEPENDENT_054
$oldTruck = 'if (!_truckOverlay041.Visible) _truckOverlay041.Show(this);'
$newTruck = 'if (!_truckOverlay041.Visible) _truckOverlay041.Show();'
$oldVideo = 'if (!_videoOverlay041.Visible) _videoOverlay041.Show(this);'
$newVideo = 'if (!_videoOverlay041.Visible) _videoOverlay041.Show();'

if (-not $hubText.Contains($oldTruck)) { throw 'Abertura da sobreposicao do caminhao nao encontrada.' }
if (-not $hubText.Contains($oldVideo)) { throw 'Abertura da sobreposicao de video nao encontrada.' }
$hubText = $hubText.Replace($oldTruck, $newTruck)
$hubText = $hubText.Replace($oldVideo, $newVideo)

$hubText = $hubText.Replace(
    'ApplyOverlayOpacity041(); if (!_truckOverlay041.Visible) _truckOverlay041.Show(); _truckOverlay041.BringToFront();',
    'ApplyOverlayOpacity041(); _truckOverlay041.TopMost = true; _truckOverlay041.ShowInTaskbar = false; if (!_truckOverlay041.Visible) _truckOverlay041.Show(); _truckOverlay041.BringToFront();'
)
$hubText = $hubText.Replace(
    'ApplyOverlayOpacity041(); if (!_videoOverlay041.Visible) _videoOverlay041.Show(); _videoOverlay041.BringToFront();',
    'ApplyOverlayOpacity041(); _videoOverlay041.TopMost = true; _videoOverlay041.ShowInTaskbar = false; if (!_videoOverlay041.Visible) _videoOverlay041.Show(); _videoOverlay041.BringToFront();'
)

# Base canonica para o patch 1.0.55. Alguns patches antigos podem deixar estilos
# diferentes; normalizar aqui torna a transformacao para janelas normais deterministica.
$truckText = [regex]::Replace($truckText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.None;', 1)
$videoText = [regex]::Replace($videoText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.SizableToolWindow;', 1)
$truckText = [regex]::Replace($truckText, 'ShowInTaskbar\s*=\s*(true|false)\s*;', 'ShowInTaskbar = false;', 1)
$videoText = [regex]::Replace($videoText, 'ShowInTaskbar\s*=\s*(true|false)\s*;', 'ShowInTaskbar = false;', 1)

if ($mainText -notlike '*CurrentVersion = "1.0.54.0"*') { throw 'Versao 1.0.54 nao aplicada.' }
if ($hubText -like '*_truckOverlay041.Show(this)*' -or $hubText -like '*_videoOverlay041.Show(this)*') { throw 'Ainda existe overlay vinculado a janela principal.' }
foreach ($m in @('_truckOverlay041.Show();','_videoOverlay041.Show();','_truckOverlay041.TopMost = true','_videoOverlay041.TopMost = true')) {
    if ($hubText -notlike "*$m*") { throw "Overlay 1.0.54 sem $m" }
}
if ($truckText -notlike '*FormBorderStyle = FormBorderStyle.None*' -or $videoText -notlike '*FormBorderStyle = FormBorderStyle.SizableToolWindow*') { throw 'Estilos-base dos overlays nao foram normalizados.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.54: overlays independentes e estilos-base normalizados para a 1.0.55.'
