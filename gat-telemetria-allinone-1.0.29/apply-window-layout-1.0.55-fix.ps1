param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
Get-ChildItem $rootPath -Filter '*.cs' -Recurse | ForEach-Object {
    $text = Get-Content $_.FullName -Raw
    if ($text.Contains('`r`n')) {
        $text = $text.Replace('`r`n', [Environment]::NewLine)
        Set-Content $_.FullName $text -Encoding UTF8
    }
}

$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $hub -or -not $truck -or -not $video) { throw 'Arquivos 1.0.55 nao encontrados apos hotfix.' }
$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw
if ($hubText.Contains('`r`n') -or $truckText.Contains('`r`n') -or $videoText.Contains('`r`n')) { throw 'Ainda existem quebras literais apos hotfix 1.0.55.' }
Write-Host 'Hotfix 1.0.55: quebras de linha normalizadas.'
