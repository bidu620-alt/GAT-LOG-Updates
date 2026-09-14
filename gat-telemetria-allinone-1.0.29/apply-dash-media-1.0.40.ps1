param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $radio -or -not $project) { throw 'Fonte GAT Telemetria 1.0.39 incompleto.' }

# Reaproveita o terceiro modo WEB que ja existiu na 1.0.38, adaptado sobre a 1.0.39.
$legacy = Join-Path $env:TEMP 'gat-radio-web-legacy.ps1'
$adapted = Join-Path $env:TEMP 'gat-radio-web-140.ps1'
Invoke-WebRequest 'https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/00136c82583806cac5cb0d643a26d5e30399b3cc/gat-telemetria-allinone-1.0.29/apply-radio-web-1.0.38.ps1' -OutFile $legacy
$patch = Get-Content $legacy -Raw
$patch = $patch.Replace('1.0.37','1.0.39').Replace('1.0.38','1.0.40')
Set-Content $adapted $patch -Encoding UTF8
& $adapted -Root $rootPath
if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar base Canal Web 1.0.40.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

# Nomes finais combinados: Canal GAT / Meu Video / Canal Web.
$radioText = $radioText.Replace('🎧 MINHA RÁDIO','🎬 MEU VÍDEO')
$radioText = $radioText.Replace('MINHA RÁDIO','MEU VÍDEO').Replace('Minha Rádio','Meu Vídeo')
$radioText = $radioText.Replace('🌐 SITE / WEB','🌐 CANAL WEB')
$radioText = $radioText.Replace('SITE / WEB','CANAL WEB')
$radioText = $radioText.Replace('sua rádio pessoal','seu vídeo pessoal')
$radioText = $radioText.Replace('Sua fonte: YouTube ou URL direta de Rádio Online MP3/AAC (somente neste PC):','Seu vídeo/playlist do YouTube (fica disponível também no GAT DASH):')

# Ponte local: o DASH Android/PC le as tres fontes pelo mesmo computador do Telemetria.
$bridgeSource = Join-Path $PSScriptRoot 'DashMediaBridge.cs'
$bridgeTarget = Join-Path (Split-Path $main.FullName -Parent) 'DashMediaBridge.cs'
Copy-Item $bridgeSource $bridgeTarget -Force

if ($mainText -notlike '*DashMediaBridge.Start();*') {
    $needle = '        ClientStore.Ensure();'
    if (-not $mainText.Contains($needle)) { throw 'Ponto ClientStore.Ensure nao encontrado.' }
    $mainText = $mainText.Replace($needle, $needle + "`r`n        DashMediaBridge.Start();")
}
if ($mainText -notlike '*DashMediaBridge.Stop();*') {
    $needle = '            _timer.Stop();'
    if (-not $mainText.Contains($needle)) { throw 'Ponto FormClosed nao encontrado.' }
    $mainText = $mainText.Replace($needle, "            DashMediaBridge.Stop();`r`n" + $needle)
}

foreach($m in @('CurrentVersion = "1.0.40.0"','DashMediaBridge.Start();','DashMediaBridge.Stop();')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.40 sem $m" }
}
foreach($m in @('CANAL GAT','MEU VÍDEO','CANAL WEB','radio-web-url.txt','TryParseWebUrl','NavigateWebAsync','Radio-1.0.40','/index.html?v=140')) {
    if ($radioText -notlike "*$m*") { throw "Radio/TV 1.0.40 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.40: Canal GAT + Meu Video + Canal Web + ponte DASH aplicado.'
