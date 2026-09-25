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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.68 incompleta para aplicar 1.0.68.8 VOZ NOVA LIMPA.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

$mainText = $mainText.Replace('CurrentVersion = "1.0.68.0"', 'CurrentVersion = "1.0.68.8"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68"', 'Text = "Cliente 1.0.68.8"')
$mainText = $mainText.Replace('HUB 1.0.68:', 'HUB 1.0.68.8:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.68.8')
$hubText = $hubText.Replace('Cliente 1.0.68 TESTE', 'Cliente 1.0.68.8 VOZ NOVA LIMPA')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.68.8')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.68 TESTE', 'Cliente: 1.0.68.8 VOZ NOVA LIMPA')
}

$nl = [Environment]::NewLine

$hubText = [regex]::Replace(
    $hubText,
    'else if \(type == "speak"\) \{.*?\}\s*else if \(type == "setSettings"\)',
    'else if (type == "speak") { /* 1.0.68.8: voz pertence ao GAT Telemetria; DASH nao fala */ }' + $nl + '            else if (type == "setSettings")',
    [System.Text.RegularExpressions.RegexOptions]::Singleline
)
$hubText = $hubText.Replace('VoiceSyncDashSetting062(m); SaveDashSettings045(m);', 'SaveDashSettings045(m);')

$oldSpeed = @'
    private static string VoiceSpeedDir166()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "limits");
        Directory.CreateDirectory(dir);
        return dir;
    }
'@
$newSpeed = @'
    private static string VoiceSpeedDir166()
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voz", "limites");
        Directory.CreateDirectory(dir);
        return dir;
    }
'@
if ($voiceText.Contains($oldSpeed.Trim())) {
    $voiceText = $voiceText.Replace($oldSpeed.Trim(), $newSpeed.Trim())
} else {
    $voiceText = [regex]::Replace(
        $voiceText,
        'private static string VoiceSpeedDir166\(\)\s*\{.*?\}',
        $newSpeed.Trim(),
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
}

$voiceText = [regex]::Replace(
    $voiceText,
    'try\s*\{\s*JObject o = JObject\.Parse\(DashSettingsJson045\(\)\);\s*if \(o\["voice"\] != null\) _voiceMuted062 = !Convert\.ToBoolean\(o\["voice"\]\);\s*\}\s*catch \{ \}',
    '_voiceMuted062 = false;',
    [System.Text.RegularExpressions.RegexOptions]::Singleline
)

$initNeedle = '        VoiceLoadSettings062();'
if ($voiceText.Contains($initNeedle) -and $voiceText -notlike '*VOZ_LIMPA_1688*') {
    $voiceText = $voiceText.Replace($initNeedle, $initNeedle + $nl + '        // VOZ_LIMPA_1688' + $nl + '        try { VoiceSpeedDir166(); } catch { }')
}

$voiceText = $voiceText.Replace('Voz Ayres 1.0.66 • MP3 individual por evento • limites individuais • volume independente.', 'Voz limpa do GAT Telemetria • limites em voz\\limites • independente do GAT DASH.')

if ($mainText -notlike '*CurrentVersion = "1.0.68.8"*') { throw 'MainForm nao ficou em 1.0.68.8.' }
if ($hubText -notlike '*voz pertence ao GAT Telemetria*') { throw 'DASH ainda nao foi isolado da voz.' }
if ($voiceText -notlike '*AppDomain.CurrentDomain.BaseDirectory*' -or $voiceText -notlike '*"voz", "limites"*') { throw 'Pasta voz\\limites limpa nao aplicada.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }

Write-Host 'GAT Telemetria 1.0.68.8: VOZ NOVA LIMPA aplicada; DASH sem voz e pasta voz\\limites independente.'
