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

# Versao 1.0.70: somente alerta de velocidade/limite da rodovia.
$mainText = $mainText.Replace('CurrentVersion = "1.0.69.0"', 'CurrentVersion = "1.0.70.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.69"', 'Text = "Cliente 1.0.70"')
$mainText = $mainText.Replace('HUB 1.0.69:', 'HUB 1.0.70:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.69', 'GAT Telemetria BETA 1.0.70')
$hubText = $hubText.Replace('Cliente 1.0.69 TESTE', 'Cliente 1.0.70 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.69', 'GAT Telemetria BETA 1.0.70')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.69 TESTE', 'Cliente: 1.0.70 TESTE')
}

# Bloqueio 1: VoicePlayGroup062 so aceita o grupo speed.
$groupPattern = '(?s)(private bool VoicePlayGroup062\(string group\)\s*\{)'
if ([regex]::IsMatch($voiceText, $groupPattern)) {
    $voiceText = [regex]::Replace($voiceText, $groupPattern, '$1' + "`r`n        if (!string.Equals(group, \"speed\", StringComparison.OrdinalIgnoreCase)) return false;", 1)
} elseif ($voiceText -notlike '*StringComparison.OrdinalIgnoreCase)) return false;*') {
    throw 'VoicePlayGroup062 nao encontrado para bloqueio 1.0.70.'
}

# Bloqueio 2: IDs gerais so podem tocar os avisos de velocidade (016-022).
$idPattern = '(?s)(private bool VoicePlayId062\(int id, bool interrupt = true\)\s*\{)'
if ([regex]::IsMatch($voiceText, $idPattern)) {
    $voiceText = [regex]::Replace($voiceText, $idPattern, '$1' + "`r`n        if (id < 16 || id > 22) return false;", 1)
} elseif ($voiceText -notlike '*if (id < 16 || id > 22) return false;*') {
    throw 'VoicePlayId062 nao encontrado para bloqueio 1.0.70.'
}

# Desativa explicitamente os observadores que geravam falas nao relacionadas a limite.
$voiceText = [regex]::Replace($voiceText,
    '(?s)private void VoiceObserveJobRandom168\(JObject tele, DateTime now\)\s*\{.*?\n    \}',
    "private void VoiceObserveJobRandom168(JObject tele, DateTime now)`r`n    {`r`n        return;`r`n    }", 1)
$voiceText = [regex]::Replace($voiceText,
    '(?s)private void VoiceObserveDamage168\(double damage, DateTime now\)\s*\{.*?\n    \}',
    "private void VoiceObserveDamage168(double damage, DateTime now)`r`n    {`r`n        return;`r`n    }", 1)

# Mantem intacto o alerta numerico de limite e exige os marcadores principais.
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
