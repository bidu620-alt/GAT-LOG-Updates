param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub44 = Get-ChildItem $rootPath -Filter 'MainForm.Hub044.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub41 -or -not $hub44 -or -not $hub45) {
    throw 'Fonte 1.0.45 incompleto para aplicar a atualizacao 1.0.46.'
}

$mainText = Get-Content $main.FullName -Raw
$hub41Text = Get-Content $hub41.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$hub45Text = Get-Content $hub45.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.45.0"', 'CurrentVersion = "1.0.46.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.45"', 'Text = "Cliente 1.0.46"')
$mainText = $mainText.Replace('HUB 1.0.45:', 'HUB 1.0.46:')
$hub44Text = $hub44Text.Replace('GAT Telemetria BETA 1.0.45', 'GAT Telemetria BETA 1.0.46')
$hub44Text = $hub44Text.Replace('Cliente 1.0.45 TESTE', 'Cliente 1.0.46 TESTE')
$hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.45', 'GAT Telemetria BETA 1.0.46')
$hub45Text = $hub45Text.Replace('Cliente: 1.0.45 TESTE', 'Cliente: 1.0.46 TESTE')

# Permite que o botao de mute interrompa imediatamente qualquer fala em andamento.
$settingsNeedle = 'else if (type == "setSettings") { SaveDashSettings045(m); await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")"); }'
if ($hub41Text -notlike '*type == "stopSpeech"*') {
    if (-not $hub41Text.Contains($settingsNeedle)) { throw 'Handler setSettings nao encontrado para inserir stopSpeech.' }
    $stopHandler = 'else if (type == "stopSpeech") { try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { } }' + "`r`n            " + $settingsNeedle
    $hub41Text = $hub41Text.Replace($settingsNeedle, $stopHandler)
}

foreach ($m in @('CurrentVersion = "1.0.46.0"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.46 sem $m" }
}
foreach ($m in @('type == "stopSpeech"','SaveDashSettings045(m)')) {
    if ($hub41Text -notlike "*$m*") { throw "Mute 1.0.46 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hub41Text -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $hub45.FullName $hub45Text -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.46: suporte a mutar/desmutar alertas de velocidade aplicado.'