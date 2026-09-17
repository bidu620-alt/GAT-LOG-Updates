param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$voice = Get-ChildItem $rootPath -Filter 'Voice062.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.69 incompleto para aplicar 1.0.70.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# 1.0.70: somente alerta de velocidade/limite da rodovia.
$mainText = $mainText.Replace('CurrentVersion = "1.0.69.0"', 'CurrentVersion = "1.0.70.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.69"', 'Text = "Cliente 1.0.70"')
$mainText = $mainText.Replace('HUB 1.0.69:', 'HUB 1.0.70:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.69', 'GAT Telemetria BETA 1.0.70')
$hubText = $hubText.Replace('Cliente 1.0.69 TESTE', 'Cliente 1.0.70 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.69', 'GAT Telemetria BETA 1.0.70')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.69 TESTE', 'Cliente: 1.0.70 TESTE')
}

# Bloqueio central: nenhum grupo de voz toca, exceto speed.
$groupGuard = 'if (!string.Equals(group, "speed", StringComparison.OrdinalIgnoreCase)) return false;'
if ($voiceText -notlike "*$groupGuard*") {
    $groupPattern = '(private bool VoicePlayGroup062\(string group, bool interrupt = true\)\s*\{)'
    if (-not [regex]::IsMatch($voiceText, $groupPattern)) { throw 'VoicePlayGroup062 nao encontrado.' }
    $groupReplacement = @'
$1
        if (!string.Equals(group, "speed", StringComparison.OrdinalIgnoreCase)) return false;
'@
    $voiceText = [regex]::Replace($voiceText, $groupPattern, $groupReplacement.TrimEnd(), 1)
}

# Bloqueio secundario: falas gerais somente IDs de velocidade 016-022.
$idGuard = 'if (id < 16 || id > 22) return false;'
if ($voiceText -notlike "*$idGuard*") {
    $idPattern = '(private bool VoicePlayId062\(int id, bool interrupt = true\)\s*\{)'
    if (-not [regex]::IsMatch($voiceText, $idPattern)) { throw 'VoicePlayId062 nao encontrado.' }
    $idReplacement = @'
$1
        if (id < 16 || id > 22) return false;
'@
    $voiceText = [regex]::Replace($voiceText, $idPattern, $idReplacement.TrimEnd(), 1)
}

# O alerta numerico de limite continua direto pelos MP3 limite_020 ... limite_130.
foreach ($m in @('CurrentVersion = "1.0.70.0"','Cliente 1.0.70')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.70 sem $m" }
}
foreach ($m in @('SpeedLimitPackName065','VoicePlaySpeedLimit065','VoiceFindSpeed166','limite_')) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.70 sem $m" }
}
if ($voiceText -notlike '*if (id < 16 || id > 22) return false;*') { throw 'Bloqueio por ID nao aplicado.' }
if ($voiceText -notlike '*string.Equals(group, "speed", StringComparison.OrdinalIgnoreCase)*') { throw 'Bloqueio por grupo nao aplicado.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }

Write-Host 'GAT Telemetria 1.0.70: somente alertas de limite/velocidade da rodovia permanecem ativos.'
