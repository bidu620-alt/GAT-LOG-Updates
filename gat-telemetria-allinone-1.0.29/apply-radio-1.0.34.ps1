param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar Radio GAT 1.0.34.' }

$mainText = Get-Content $main.FullName -Raw
$mainText = $mainText.Replace('CurrentVersion = "1.0.33.0"', 'CurrentVersion = "1.0.34.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.33"', 'Text = "Cliente 1.0.34"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.34\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.34.0.' }

$radioSource = Join-Path $PSScriptRoot 'RadioForm134.cs'
if (-not (Test-Path $radioSource)) { throw 'RadioForm134.cs nao encontrado.' }
Copy-Item $radioSource (Join-Path $main.Directory.FullName 'RadioForm.cs') -Force

$radioText = Get-Content (Join-Path $main.Directory.FullName 'RadioForm.cs') -Raw
foreach ($marker in @('RÁDIO / TV GAT','gatLoadVideo','gatLoadPlaylist','ABRIR NO YOUTUBE','TELA CHEIA','Radio-1.0.34','e.data===101','player.nextVideo')) {
    if ($radioText -notlike "*$marker*") { throw "Radio GAT 1.0.34 incompleta: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.34: video individual + playlist + fullscreen + fallback YouTube + WebView2 limpo.'
