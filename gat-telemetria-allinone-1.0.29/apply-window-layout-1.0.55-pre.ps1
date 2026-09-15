param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $truck -or -not $video) { throw 'Overlays nao encontrados para prepatch 1.0.55.' }

$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw
$truckText = [regex]::Replace($truckText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.None;', 1)
$videoText = [regex]::Replace($videoText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.SizableToolWindow;', 1)
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'Prepatch 1.0.55: estilos de borda dos overlays normalizados.'
