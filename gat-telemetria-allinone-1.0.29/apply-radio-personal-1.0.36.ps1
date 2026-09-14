param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar Radio Personal 1.0.36.' }

$mainText = Get-Content $main.FullName -Raw
$mainText = $mainText.Replace('CurrentVersion = "1.0.35.0"', 'CurrentVersion = "1.0.36.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.35"', 'Text = "Cliente 1.0.36"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.36\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.36.0.' }
if ($mainText -notlike '*radioForm.Show();*') { throw 'Radio independente da 1.0.35 nao encontrada.' }

$radioSource = Join-Path $PSScriptRoot 'RadioForm136.cs'
if (-not (Test-Path $radioSource)) { throw 'RadioForm136.cs nao encontrado.' }
Copy-Item $radioSource (Join-Path $main.Directory.FullName 'RadioForm.cs') -Force

$radioText = Get-Content (Join-Path $main.Directory.FullName 'RadioForm.cs') -Raw
foreach ($marker in @(
    '📡 CANAL GAT',
    '🎧 MINHA RÁDIO',
    'radio-personal-source.txt',
    'TryParseYoutubeSource',
    'query.TryGetValue("list"',
    'somente neste PC',
    'MODO JOGO • SOBREPOSTO',
    'TopMost = true;',
    'Radio-1.0.36',
    '/index.html?v=136',
    'gatLoadPlaylist',
    'player.nextVideo'
)) {
    if ($radioText -notlike "*$marker*") { throw "Radio GAT 1.0.36 incompleta: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.36: Canal GAT + Minha Radio local, com playlist/video YouTube e overlay.'
