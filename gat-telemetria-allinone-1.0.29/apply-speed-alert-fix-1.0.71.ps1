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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.70 incompleto para aplicar 1.0.71.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# ---------------------------------------------------------------------------
# 1.0.71 SPEED ALERT FIX
# - Alerta de excesso passa a ser detectado diretamente pela telemetria.
# - Usa truck.speed + navigation.speedLimit e a tolerancia salva no DASH.
# - Repete no maximo a cada 15 s enquanto continuar acima do limite+tolerancia.
# - Quando normaliza e excede novamente, alerta de imediato.
# - Instala/semeia automaticamente os 12 MP3s limite_020 ... limite_130.
# - Preserva MP3s personalizados existentes: so copia o padrao se estiver faltando.
# ---------------------------------------------------------------------------

$mainText = $mainText.Replace('CurrentVersion = "1.0.70.0"', 'CurrentVersion = "1.0.71.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.70"', 'Text = "Cliente 1.0.71"')
$mainText = $mainText.Replace('HUB 1.0.70:', 'HUB 1.0.71:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.70', 'GAT Telemetria BETA 1.0.71')
$hubText = $hubText.Replace('Cliente 1.0.70 TESTE', 'Cliente 1.0.71 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.70', 'GAT Telemetria BETA 1.0.71')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.70 TESTE', 'Cliente: 1.0.71 TESTE')
}

if ($voiceText -notlike '*VoiceSeedDefaultLimits171();*') {
    $seedNeedle = '        VoiceTryAutoImportSpeed065();'
    if ($voiceText.Contains($seedNeedle)) {
        $voiceText = $voiceText.Replace($seedNeedle, $seedNeedle + "`r`n        VoiceSeedDefaultLimits171();")
    } else {
        $seedFallback = '        VoiceTryAutoImport062();'
        if (-not $voiceText.Contains($seedFallback)) { throw 'Inicializacao de voz nao encontrada para 1.0.71.' }
        $voiceText = $voiceText.Replace($seedFallback, $seedFallback + "`r`n        VoiceSeedDefaultLimits171();")
    }
}

if ($voiceText -notlike '*_voiceLastRoadAlert171*') {
    $fieldNeedle = '    private DateTime _voiceLastObserve062 = DateTime.MinValue;'
    if (-not $voiceText.Contains($fieldNeedle)) { throw 'Campo _voiceLastObserve062 nao encontrado.' }
    $fields = @'
    private DateTime _voiceLastRoadAlert171 = DateTime.MinValue;
    private int _voiceLastRoadLimit171;
    private bool _voiceOverspeed171;
'@
    $voiceText = $voiceText.Replace($fieldNeedle, $fieldNeedle + "`r`n" + $fields.TrimEnd())
}

if ($voiceText -notlike '*VoiceObserveRoadSpeed171(tele, now);*') {
    $observeNeedle = '        _voiceSeenTelemetry062 = true;'
    if (-not $voiceText.Contains($observeNeedle)) { throw 'Marcador _voiceSeenTelemetry062 nao encontrado.' }
    $voiceText = $voiceText.Replace($observeNeedle, $observeNeedle + "`r`n        VoiceObserveRoadSpeed171(tele, now);")
}

if ($voiceText -notlike '*private void VoiceObserveRoadSpeed171*') {
    $methodNeedle = '    private void ObserveVoiceTelemetry062(JObject tele)'
    if (-not $voiceText.Contains($methodNeedle)) { throw 'ObserveVoiceTelemetry062 nao encontrado.' }

    $methods = @'
    private void VoiceSeedDefaultLimits171()
    {
        try
        {
            string src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice-defaults", "limits");
            if (!Directory.Exists(src)) return;

            string dest = VoiceSpeedDir166();
            int copied = 0;
            foreach (int limit in new int[] {20,30,40,50,60,70,80,90,100,110,120,130})
            {
                string name = "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3";
                string from = Path.Combine(src, name);
                string to = Path.Combine(dest, name);
                if (!File.Exists(from) || File.Exists(to)) continue;
                File.Copy(from, to, false);
                copied++;
            }
            if (copied > 0) ClientStore.Log("voz 1.0.71: " + copied + " limites padrao instalados");
        }
        catch (Exception ex)
        {
            try { ClientStore.Log("voz 1.0.71: falha ao instalar limites padrao: " + ex.Message); } catch { }
        }
    }

    private static double VoicePathNumber171(JObject root, params string[] paths)
    {
        if (root == null) return double.NaN;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken token = root.SelectToken(path, false);
                if (token == null) continue;
                double value;
                if (double.TryParse(Convert.ToString(token), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    return value;
            }
            catch { }
        }
        return double.NaN;
    }

    private double VoiceTolerance171()
    {
        double tolerance = 3.0;
        try
        {
            JObject settings = JObject.Parse(DashSettingsJson045());
            JToken token = settings["tolerance"];
            double parsed;
            if (token != null && double.TryParse(Convert.ToString(token), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                tolerance = parsed;
        }
        catch { }
        return Math.Max(0.0, Math.Min(20.0, tolerance));
    }

    private static int VoiceNormalizeRoadLimit171(double limit)
    {
        if (double.IsNaN(limit) || double.IsInfinity(limit) || limit <= 0) return 0;
        int rounded = (int)Math.Round(limit, MidpointRounding.AwayFromZero);
        int[] allowed = new int[] {20,30,40,50,60,70,80,90,100,110,120,130};
        if (allowed.Contains(rounded)) return rounded;

        int nearest = (int)Math.Round(limit / 10.0, MidpointRounding.AwayFromZero) * 10;
        if (allowed.Contains(nearest) && Math.Abs(limit - nearest) <= 1.5) return nearest;
        return 0;
    }

    private void VoiceObserveRoadSpeed171(JObject tele, DateTime now)
    {
        try
        {
            double speed = VoicePathNumber171(tele, "truck.speed", "Truck.Speed");
            double roadLimit = VoicePathNumber171(tele, "navigation.speedLimit", "Navigation.SpeedLimit");
            if (double.IsNaN(speed) || double.IsNaN(roadLimit) || roadLimit <= 0)
            {
                _voiceOverspeed171 = false;
                return;
            }

            speed = Math.Abs(speed);
            int announcedLimit = VoiceNormalizeRoadLimit171(roadLimit);
            if (announcedLimit <= 0)
            {
                _voiceOverspeed171 = false;
                return;
            }

            double tolerance = VoiceTolerance171();
            bool over = speed > roadLimit + tolerance;
            bool limitChanged = _voiceLastRoadLimit171 > 0 && announcedLimit != _voiceLastRoadLimit171;

            if (!over)
            {
                _voiceOverspeed171 = false;
                _voiceLastRoadLimit171 = announcedLimit;
                return;
            }

            if (limitChanged) _voiceOverspeed171 = false;

            bool due = !_voiceOverspeed171 || (now - _voiceLastRoadAlert171).TotalSeconds >= 15.0;
            if (due)
            {
                bool played = false;
                string direct = VoiceFindSpeed166(announcedLimit);
                if (!string.IsNullOrWhiteSpace(direct))
                    played = VoicePlayFile166(direct, true);
                if (!played)
                    played = VoicePlaySpeedLimit065(announcedLimit);

                if (played)
                {
                    _voiceLastRoadAlert171 = now;
                    _voiceOverspeed171 = true;
                    ClientStore.Log(
                        "voz limite direta 1.0.71: " + announcedLimit.ToString(CultureInfo.InvariantCulture) +
                        " km/h | velocidade " + Math.Round(speed).ToString(CultureInfo.InvariantCulture) +
                        " | tolerancia " + tolerance.ToString("0.#", CultureInfo.InvariantCulture));
                }
            }

            _voiceLastRoadLimit171 = announcedLimit;
        }
        catch (Exception ex)
        {
            try { ClientStore.Log("voz limite direta 1.0.71 indisponivel: " + ex.Message); } catch { }
        }
    }

'@
    $voiceText = $voiceText.Replace($methodNeedle, $methods + $methodNeedle)
}

foreach ($m in @('CurrentVersion = "1.0.71.0"','Cliente 1.0.71')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.71 sem $m" }
}
foreach ($m in @(
    'VoiceSeedDefaultLimits171',
    'VoiceObserveRoadSpeed171',
    'VoicePathNumber171',
    'VoiceTolerance171',
    'VoiceNormalizeRoadLimit171',
    'VoiceFindSpeed166',
    'VoicePlayFile166',
    'navigation.speedLimit',
    'truck.speed'
)) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.71 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }

Write-Host 'GAT Telemetria 1.0.71: alerta direto limite+tolerancia + 12 vozes padrao aplicado.'
