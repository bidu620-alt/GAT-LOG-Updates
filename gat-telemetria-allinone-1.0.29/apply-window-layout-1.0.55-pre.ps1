param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $hub -or -not $truck -or -not $video) { throw 'Arquivos nao encontrados para prepatch 1.0.55.' }

$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# Normaliza os estilos para que o patch principal seja deterministico.
$truckText = [regex]::Replace($truckText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.None;', 1)
$videoText = [regex]::Replace($videoText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.SizableToolWindow;', 1)

# A 1.0.54 pode ter gravado o ShowInTaskbar com espacamento diferente; normaliza antes da troca.
$hubText = [regex]::Replace($hubText, '_truckOverlay041\.ShowInTaskbar\s*=\s*false\s*;', '_truckOverlay041.ShowInTaskbar = true;')
$hubText = [regex]::Replace($hubText, '_videoOverlay041\.ShowInTaskbar\s*=\s*false\s*;', '_videoOverlay041.ShowInTaskbar = true;')

Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'Prepatch 1.0.55: estilos de borda e barra de tarefas dos overlays normalizados.'
