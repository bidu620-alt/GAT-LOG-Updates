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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.67 incompleto para aplicar 1.0.68.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

function Replace-Between168([string]$text, [string]$startMarker, [string]$endMarker, [string]$replacement) {
    $s = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    if ($s -lt 0) { throw "Inicio nao encontrado 1.0.68: $startMarker" }
    $e = $text.IndexOf($endMarker, $s + $startMarker.Length, [StringComparison]::Ordinal)
    if ($e -lt 0) { throw "Fim nao encontrado 1.0.68: $endMarker" }
    return $text.Substring(0, $s) + $replacement.TrimEnd() + "`r`n`r`n" + $text.Substring($e)
}

# Versao 1.0.68 TESTE - eventos de voz por carga/distancia.
$mainText = $mainText.Replace('CurrentVersion = "1.0.67.0"', 'CurrentVersion = "1.0.68.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.67"', 'Text = "Cliente 1.0.68"')
$mainText = $mainText.Replace('HUB 1.0.67:', 'HUB 1.0.68:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.67"', 'Text = "GAT Telemetria BETA 1.0.68"', 1)
$hubText = $hubText.Replace('Cliente 1.0.67 TESTE', 'Cliente 1.0.68 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.67', 'GAT Telemetria BETA 1.0.68')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.67 TESTE', 'Cliente: 1.0.68 TESTE')
}

# Desliga o timer antigo de falas aleatorias. A partir daqui o gatilho e por km da carga.
$timerStart = '        _voiceRandomTimer062.Start();'
if ($voiceText.Contains($timerStart)) {
    $voiceText = $voiceText.Replace($timerStart, "        // 1.0.68: fala aleatoria somente por distancia da carga.`r`n        _voiceRandomTimer062.Enabled = false;")
} elseif ($voiceText -notlike '*fala aleatoria somente por distancia da carga*') {
    throw 'Timer aleatorio 1.0.62 nao encontrado.'
}

# Estado novo: cada carga tem sua propria contagem e o dano acumula desde a ultima fala.
$fieldMarker = '    [DllImport("winmm.dll", CharSet = CharSet.Auto)]'
if ($voiceText.Contains($fieldMarker) -and $voiceText -notlike '*_voiceJobKey168*') {
$fields = @'
    private string _voiceJobKey168 = string.Empty;
    private bool _voiceJobActive168;
    private double _voiceJobStartOdometer168 = double.NaN;
    private double _voiceJobStartRemaining168 = double.NaN;
    private double _voiceJobPlanned168 = double.NaN;
    private double _voiceNextRandomKm168 = 500.0;
    private string _voiceLastRandomChoice168 = string.Empty;
    private double _voiceDamageBaseline168 = double.NaN;

'@
    $voiceText = $voiceText.Replace($fieldMarker, $fields + $fieldMarker)
}

# Substitui o detector antigo (mudancas minimas de dano + dano alto) por aumento acumulado de 1%.
$damageStart = '        double damage = VoiceDamage062(tele);'
$damageEnd = '        double fuel = VoiceMetric062(tele, "fuel", "fuelAmount", "fuelLiters", "fuelLevel");'
$damageBlock = @'
        double damage = VoiceTruckDamage168(tele);
        if (!double.IsNaN(damage) && damage >= 0)
        {
            VoiceObserveDamage168(damage, now);
            _voiceLastDamage062 = damage;
        }

        // A fala decorativa agora pertence a carga atual: 500, 1000, 1500 km...
        VoiceObserveJobRandom168(tele, now);
'@
$voiceText = Replace-Between168 $voiceText $damageStart $damageEnd $damageBlock

# Helpers e nova logica de evento. Inseridos antes do detector visual 1.0.64.
$observeDamageMarker = '    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)'
if ($voiceText.Contains($observeDamageMarker) -and $voiceText -notlike '*private void VoiceObserveJobRandom168*') {
$eventMethods = @'
    private static double VoiceTruckDamage168(JObject tele)
    {
        if (tele == null) return double.NaN;
        double max = double.NaN;
        string[][] groups = new string[][]
        {
            new string[] { "wearEngine", "engineWear", "engineDamage", "engine_damage" },
            new string[] { "wearTransmission", "transmissionWear", "transmissionDamage", "transmission_damage" },
            new string[] { "wearCabin", "cabinWear", "cabinDamage", "cabin_damage" },
            new string[] { "wearChassis", "chassisWear", "chassisDamage", "chassis_damage" },
            new string[] { "wearWheels", "wheelsWear", "wheelsDamage", "wheels_damage" },
            new string[] { "trailerDamage", "trailer_damage" }
        };
        foreach (string[] names in groups)
        {
            double v = VoiceMetric062(tele, names);
            if (double.IsNaN(v) || v < 0) continue;
            if (v > 1.0 && v <= 100.0) v /= 100.0;
            if (v > 1.5) continue;
            if (double.IsNaN(max) || v > max) max = v;
        }
        return max;
    }

    private static string VoiceText168(JObject tele, params string[] paths)
    {
        if (tele == null) return string.Empty;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken t = tele.SelectToken(path, false);
                if (t != null && t.Type != JTokenType.Null)
                {
                    string s = (Convert.ToString(t, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                    if (s.Length > 0) return s;
                }
            }
            catch { }
        }
        return string.Empty;
    }

    private static bool VoiceBool168(JObject tele, params string[] paths)
    {
        string s = VoiceText168(tele, paths).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes" || s == "sim";
    }

    private static string VoiceJobKey168(JObject tele)
    {
        string latch = VoiceText168(tele, "job_latch_key");
        string cargoId = VoiceText168(tele, "cargo_id", "job.cargoId", "Job.CargoId");
        string cargo = VoiceText168(tele, "cargo_name", "job.cargoName", "job.cargo");
        string source = VoiceText168(tele, "source_city_id", "source_city", "job.sourceCityId", "job.sourceCity");
        string dest = VoiceText168(tele, "destination_city_id", "destination_city", "job.destinationCityId", "job.destinationCity");
        double planned = VoiceMetric062(tele, "planned_distance_km", "plannedDistanceKm");
        string p = double.IsNaN(planned) ? string.Empty : planned.ToString("0", CultureInfo.InvariantCulture);
        return string.Join("|", new string[] { latch, cargoId, cargo, source, dest, p });
    }

    private static bool VoiceJobEnded168(JObject tele)
    {
        string state = VoiceText168(tele, "gat_job_state").ToLowerInvariant();
        string ev = VoiceText168(tele, "gat_job_event").ToLowerInvariant();
        return state == "ended" || ev == "delivered" || ev == "cancelled" || ev == "canceled" ||
               VoiceBool168(tele, "gameplay.jobDelivered", "jobDelivered", "gameplay.jobCancelled", "jobCancelled", "gameplay.jobCanceled", "jobCanceled");
    }

    private static bool VoiceJobActiveNow168(JObject tele)
    {
        if (VoiceJobEnded168(tele)) return false;
        if (VoiceBool168(tele, "on_job", "job_latched", "gameplay.onJob")) return true;
        string state = VoiceText168(tele, "gat_job_state").ToLowerInvariant();
        if (state == "active") return true;
        string cargo = VoiceText168(tele, "cargo_name", "cargo_id", "job.cargoName", "job.cargoId", "job.cargo");
        return !string.IsNullOrWhiteSpace(cargo);
    }

    private void VoiceResetJob168()
    {
        _voiceJobKey168 = string.Empty;
        _voiceJobActive168 = false;
        _voiceJobStartOdometer168 = double.NaN;
        _voiceJobStartRemaining168 = double.NaN;
        _voiceJobPlanned168 = double.NaN;
        _voiceNextRandomKm168 = 500.0;
    }

    private bool VoicePlayRandom168()
    {
        try
        {
            if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
            DateTime now = DateTime.UtcNow;
            if (now < _voiceCrashProtectUntil063 || now < _voiceDynamicProtectUntil063) return false;
            if (!string.IsNullOrWhiteSpace(_voiceActiveAlias062)) return false;

            var choices = new List<string>();
            for (int id = 103; id <= 122; id++) choices.Add("id:" + id.ToString(CultureInfo.InvariantCulture));
            foreach (string f in VoiceExtraFiles167("random")) choices.Add("file:" + f);
            if (choices.Count == 0) return false;

            if (choices.Count > 1 && !string.IsNullOrWhiteSpace(_voiceLastRandomChoice168))
                choices.RemoveAll(x => string.Equals(x, _voiceLastRandomChoice168, StringComparison.OrdinalIgnoreCase));
            if (choices.Count == 0) return false;

            string pick = choices[_voiceRandom062.Next(choices.Count)];
            bool ok = false;
            if (pick.StartsWith("id:", StringComparison.Ordinal))
            {
                int id;
                if (int.TryParse(pick.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) ok = VoicePlayId062(id, false);
            }
            else if (pick.StartsWith("file:", StringComparison.Ordinal))
            {
                ok = VoicePlayFile166(pick.Substring(5), false);
            }
            if (ok)
            {
                _voiceLastRandomChoice168 = pick;
                ClientStore.Log("voz aleatoria 1.0.68 por distancia: " + pick);
            }
            return ok;
        }
        catch { return false; }
    }

    private void VoiceObserveJobRandom168(JObject tele, DateTime now)
    {
        try
        {
            if (tele == null) return;
            if (VoiceJobEnded168(tele))
            {
                VoiceResetJob168();
                return;
            }
            if (!VoiceJobActiveNow168(tele)) return;

            string key = VoiceJobKey168(tele);
            double odometer = VoiceMetric062(tele, "odometer", "odometerKm", "odometer_km");
            double remaining = VoiceMetric062(tele, "remaining_km", "remainingKm");
            double planned = VoiceMetric062(tele, "planned_distance_km", "plannedDistanceKm");

            if (!_voiceJobActive168 || !string.Equals(_voiceJobKey168, key, StringComparison.Ordinal))
            {
                _voiceJobActive168 = true;
                _voiceJobKey168 = key;
                _voiceJobStartOdometer168 = odometer;
                _voiceJobStartRemaining168 = remaining;
                _voiceJobPlanned168 = planned;
                _voiceNextRandomKm168 = 500.0;
                ClientStore.Log("voz 1.0.68: nova carga, contador aleatorio zerado");
                return;
            }

            double travelled = double.NaN;
            if (!double.IsNaN(odometer) && !double.IsNaN(_voiceJobStartOdometer168) && odometer >= _voiceJobStartOdometer168)
                travelled = odometer - _voiceJobStartOdometer168;
            else if (!double.IsNaN(remaining) && !double.IsNaN(_voiceJobStartRemaining168))
                travelled = _voiceJobStartRemaining168 - remaining;
            else if (!double.IsNaN(remaining) && !double.IsNaN(_voiceJobPlanned168))
                travelled = _voiceJobPlanned168 - remaining;

            if (double.IsNaN(travelled) || travelled < 0) return;
            if (travelled + 0.01 < _voiceNextRandomKm168) return;

            if (VoicePlayRandom168())
            {
                do { _voiceNextRandomKm168 += 500.0; }
                while (_voiceNextRandomKm168 <= travelled);
            }
        }
        catch { }
    }

    private void VoiceObserveDamage168(double damage, DateTime now)
    {
        if (double.IsNaN(damage) || damage < 0) return;
        if (damage > 1.0 && damage <= 100.0) damage /= 100.0;
        if (damage > 1.5) return;

        if (double.IsNaN(_voiceDamageBaseline168))
        {
            _voiceDamageBaseline168 = damage;
            return;
        }

        // Reparo ou reset de dano: apenas reposiciona a referencia, sem falar.
        if (damage + 0.0001 < _voiceDamageBaseline168)
        {
            _voiceDamageBaseline168 = damage;
            return;
        }

        double rise = damage - _voiceDamageBaseline168;
        if (rise < 0.01) return; // 1 ponto percentual acumulado.
        if ((now - _voiceLastCrash062).TotalSeconds < 5.0) return;

        _voiceDamageBaseline168 = damage;
        VoiceTriggerCrash064(now, rise * 100.0);
    }

'@
    $voiceText = $voiceText.Replace($observeDamageMarker, $eventMethods + $observeDamageMarker)
}

# O painel de danos usa a mesma referencia acumulada de 1%, evitando duas falas da mesma batida.
$displayReplacement = @'
    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)
    {
        try
        {
            // 1.0.68: carga nao dispara "batida"; somente dano do conjunto caminhao/reboque.
            double[] cur = new double[] {
                VoiceParsePercent064(engine), VoiceParsePercent064(transmission), VoiceParsePercent064(cabin),
                VoiceParsePercent064(chassis), VoiceParsePercent064(wheels), VoiceParsePercent064(trailer)
            };
            double max = double.NaN;
            foreach (double v in cur)
            {
                if (double.IsNaN(v)) continue;
                if (double.IsNaN(max) || v > max) max = v;
            }
            _voiceDisplayDamage064 = cur;
            _voiceDisplayDamageSeen064 = DateTime.UtcNow;
            if (!double.IsNaN(max)) VoiceObserveDamage168(max / 100.0, DateTime.UtcNow);
        }
        catch { }
    }
'@
$voiceText = Replace-Between168 $voiceText $observeDamageMarker '    private void VoiceTriggerCrash064(DateTime now, double rise)' $displayReplacement

foreach ($m in @('CurrentVersion = "1.0.68.0"','ObserveDamageDisplay064(d.CargoDamage')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.68 sem $m" }
}
foreach ($m in @('Cliente 1.0.68 TESTE','BuildVoiceSettings062(p);')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.68 sem $m" }
}
foreach ($m in @('_voiceJobKey168','_voiceNextRandomKm168 = 500.0','VoiceObserveJobRandom168','VoicePlayRandom168','VoiceTruckDamage168','VoiceObserveDamage168','rise < 0.01','_voiceRandomTimer062.Enabled = false')) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.68 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.68 TESTE: random a cada 500 km por carga, nova carga zera contador, dano somente apos +1% acumulado; velocidade preservada.'