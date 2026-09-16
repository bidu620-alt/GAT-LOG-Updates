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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.63 incompleto para aplicar 1.0.64.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

$mainText = $mainText.Replace('CurrentVersion = "1.0.63.0"', 'CurrentVersion = "1.0.64.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.63"', 'Text = "Cliente 1.0.64"')
$mainText = $mainText.Replace('HUB 1.0.63:', 'HUB 1.0.64:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.63"', 'Text = "GAT Telemetria BETA 1.0.64"', 1)
$hubText = $hubText.Replace('Cliente 1.0.63 TESTE', 'Cliente 1.0.64 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.63', 'GAT Telemetria BETA 1.0.64')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.63 TESTE', 'Cliente: 1.0.64 TESTE')
}

$damageNeedle = 'SetDamageValue(lblDamageTrailer, d.TrailerDamage);'
if ($mainText.Contains($damageNeedle) -and $mainText -notlike '*ObserveDamageDisplay064(d.CargoDamage*') {
    $mainText = $mainText.Replace($damageNeedle, $damageNeedle + "`r`n`t`tObserveDamageDisplay064(d.CargoDamage, d.EngineDamage, d.TransmissionDamage, d.CabinDamage, d.ChassisDamage, d.WheelsDamage, d.TrailerDamage, d.Speed);")
}

$fieldNeedle = '    private string _voicePendingDynamic063 = string.Empty;'
if ($voiceText.Contains($fieldNeedle) -and $voiceText -notlike '*_voiceDisplayDamage064*') {
$fields = @'
    private string _voicePendingDynamic063 = string.Empty;
    private double[] _voiceDisplayDamage064;
    private DateTime _voiceDisplayDamageSeen064 = DateTime.MinValue;
'@.TrimEnd()
    $voiceText = $voiceText.Replace($fieldNeedle, $fields)
}

$oldDynamic = @'
        if (dynamicSpeed063)
        {
            VoiceSpeakDynamic063(t);
            return true;
        }
'@
$newDynamic = @'
        if (dynamicSpeed063)
        {
            try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(3200);
            if (!VoicePlayGroup062("speed")) VoiceSpeakDynamic063(t);
            ClientStore.Log("voz limite Ayres 1.0.64: " + t);
            return true;
        }
'@
if ($voiceText.Contains($oldDynamic)) { $voiceText = $voiceText.Replace($oldDynamic, $newDynamic) }
elseif ($voiceText -notlike '*voz limite Ayres 1.0.64*') { throw 'Bloco de velocidade 1.0.63 nao encontrado.' }

$insertBefore = '    private void VoiceSyncDashSetting062(JObject message)'
if ($voiceText.Contains($insertBefore) -and $voiceText -notlike '*private void ObserveDamageDisplay064*') {
$methods = @'
    private static double VoiceParsePercent064(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return double.NaN;
        string s = text.Trim();
        if (s == "—" || s == "-") return double.NaN;
        var chars = s.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray();
        if (chars.Length == 0) return double.NaN;
        s = new string(chars).Replace(',', '.');
        double v;
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return double.NaN;
        return v;
    }

    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)
    {
        try
        {
            double[] cur = new double[] {
                VoiceParsePercent064(cargo), VoiceParsePercent064(engine), VoiceParsePercent064(transmission),
                VoiceParsePercent064(cabin), VoiceParsePercent064(chassis), VoiceParsePercent064(wheels),
                VoiceParsePercent064(trailer)
            };
            DateTime now = DateTime.UtcNow;
            if (_voiceDisplayDamage064 == null || _voiceDisplayDamage064.Length != cur.Length)
            {
                _voiceDisplayDamage064 = cur;
                _voiceDisplayDamageSeen064 = now;
                return;
            }

            double maxRise = 0.0;
            for (int i = 0; i < cur.Length; i++)
            {
                if (double.IsNaN(cur[i]) || double.IsNaN(_voiceDisplayDamage064[i])) continue;
                maxRise = Math.Max(maxRise, cur[i] - _voiceDisplayDamage064[i]);
            }
            _voiceDisplayDamage064 = cur;
            _voiceDisplayDamageSeen064 = now;

            double speed = VoiceParsePercent064(speedText);
            bool moving = double.IsNaN(speed) || Math.Abs(speed) >= 2.0;
            if (maxRise >= 0.05 && moving && (now - _voiceLastCrash062).TotalSeconds >= 3.0)
                VoiceTriggerCrash064(now, maxRise);
        }
        catch { }
    }

    private void VoiceTriggerCrash064(DateTime now, double rise)
    {
        _voiceLastCrash062 = now;
        _voiceCrashProtectUntil063 = now.AddMilliseconds(5200);
        _voiceDynamicProtectUntil063 = DateTime.MinValue;
        _voicePendingDynamic063 = string.Empty;
        Interlocked.Increment(ref _voiceDynamicSerial063);
        try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
        try { VoiceStop062(); } catch { }
        if (VoicePlayGroup062("crash"))
            ClientStore.Log("voz batida 1.0.64: aumento de dano " + rise.ToString("0.###", CultureInfo.InvariantCulture) + "%");
    }

'@
    $voiceText = $voiceText.Replace($insertBefore, $methods + $insertBefore)
}

$oldCrashBody = @'
                _voiceLastCrash062 = now;
                _voiceCrashProtectUntil063 = now.AddMilliseconds(3900);
                _voiceDynamicProtectUntil063 = DateTime.MinValue;
                _voicePendingDynamic063 = string.Empty;
                Interlocked.Increment(ref _voiceDynamicSerial063);
                try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
                VoicePlayGroup062("crash");
'@
$newCrashBody = @'
                VoiceTriggerCrash064(now, damage - _voiceLastDamage062);
'@
if ($voiceText.Contains($oldCrashBody)) { $voiceText = $voiceText.Replace($oldCrashBody, $newCrashBody) }

$voiceText = $voiceText.Replace('Pacote de voz pronto • 122 falas • limite dinâmico sincronizado • reações automáticas.', 'Pacote de voz pronto • Ayres imediata no limite • batida por dano real • reações automáticas.')

foreach ($m in @('CurrentVersion = "1.0.64.0"','ObserveDamageDisplay064(d.CargoDamage')) { if ($mainText -notlike "*$m*") { throw "MainForm 1.0.64 sem $m" } }
foreach ($m in @('Cliente 1.0.64 TESTE','VoiceHandleDashSpeak062(t);')) { if ($hubText -notlike "*$m*") { throw "Hub 1.0.64 sem $m" } }
foreach ($m in @('voz limite Ayres 1.0.64','ObserveDamageDisplay064','VoiceTriggerCrash064','maxRise >= 0.05')) { if ($voiceText -notlike "*$m*") { throw "Voice 1.0.64 sem $m" } }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.64 TESTE: Ayres imediata para limite e detector de batida baseado no painel de danos.'