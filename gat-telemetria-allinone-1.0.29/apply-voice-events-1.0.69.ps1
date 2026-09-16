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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.68 incompleto para aplicar 1.0.69.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

function Replace-Between169([string]$text, [string]$startMarker, [string]$endMarker, [string]$replacement) {
    $s = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    if ($s -lt 0) { throw "Inicio nao encontrado 1.0.69: $startMarker" }
    $e = $text.IndexOf($endMarker, $s + $startMarker.Length, [StringComparison]::Ordinal)
    if ($e -lt 0) { throw "Fim nao encontrado 1.0.69: $endMarker" }
    return $text.Substring(0, $s) + $replacement.TrimEnd() + "`r`n`r`n" + $text.Substring($e)
}

# Versao 1.0.69 TESTE - hotfix de repeticao de fala aleatoria.
$mainText = $mainText.Replace('CurrentVersion = "1.0.68.0"', 'CurrentVersion = "1.0.69.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68"', 'Text = "Cliente 1.0.69"')
$mainText = $mainText.Replace('HUB 1.0.68:', 'HUB 1.0.69:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.68"', 'Text = "GAT Telemetria BETA 1.0.69"', 1)
$hubText = $hubText.Replace('Cliente 1.0.68 TESTE', 'Cliente 1.0.69 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.69')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.68 TESTE', 'Cliente: 1.0.69 TESTE')
}

# Estado adicional do contador real por carga. Nunca usa tempo para disparar fala.
$fieldNeedle = '    private double _voiceDamageBaseline168 = double.NaN;'
if ($voiceText.Contains($fieldNeedle) -and $voiceText -notlike '*_voiceJobTravelledKm169*') {
$extraFields = @'
    private double _voiceDamageBaseline168 = double.NaN;
    private double _voiceJobTravelledKm169 = 0.0;
    private double _voiceLastRemainingKm169 = double.NaN;
    private double _voiceLastOdometerKm169 = double.NaN;
    private double _voiceLastRandomAtKm169 = -500.0;
'@
    $voiceText = $voiceText.Replace($fieldNeedle, $extraFields.TrimEnd())
}

# Reset completo quando entrega/cancela e ao iniciar outra carga.
$resetStart = '    private void VoiceResetJob168()'
$resetEnd = '    private bool VoicePlayRandom168()'
$resetBlock = @'
    private void VoiceResetJob168()
    {
        _voiceJobKey168 = string.Empty;
        _voiceJobActive168 = false;
        _voiceJobStartOdometer168 = double.NaN;
        _voiceJobStartRemaining168 = double.NaN;
        _voiceJobPlanned168 = double.NaN;
        _voiceNextRandomKm168 = 500.0;
        _voiceJobTravelledKm169 = 0.0;
        _voiceLastRemainingKm169 = double.NaN;
        _voiceLastOdometerKm169 = double.NaN;
        _voiceLastRandomAtKm169 = -500.0;
    }
'@
$voiceText = Replace-Between169 $voiceText $resetStart $resetEnd $resetBlock

# Nova carga usa uma chave estavel. Nao usa distancia planejada nem latch variavel na identidade.
$observeStart = '    private void VoiceObserveJobRandom168(JObject tele, DateTime now)'
$observeEnd = '    private void VoiceObserveDamage168(double damage, DateTime now)'
$observeBlock = @'
    private static string VoiceStableJobKey169(JObject tele)
    {
        string cargoId = VoiceText168(tele, "cargo_id", "job.cargoId", "Job.CargoId");
        string cargo = VoiceText168(tele, "cargo_name", "job.cargoName", "job.cargo");
        string source = VoiceText168(tele, "source_city_id", "source_city", "job.sourceCityId", "job.sourceCity");
        string dest = VoiceText168(tele, "destination_city_id", "destination_city", "job.destinationCityId", "job.destinationCity");
        string key = string.Join("|", new string[] { cargoId, cargo, source, dest });
        return string.IsNullOrWhiteSpace(key.Replace("|", string.Empty)) ? "active-job" : key;
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

            string key = VoiceStableJobKey169(tele);
            double remaining = VoiceMetric062(tele, "remaining_km", "remainingKm");
            double odometerKm = VoiceMetric062(tele, "odometerKm", "odometer_km");
            double speed = VoiceMetric062(tele, "speedKph", "speedKmh", "speed");

            if (!_voiceJobActive168 || !string.Equals(_voiceJobKey168, key, StringComparison.Ordinal))
            {
                _voiceJobActive168 = true;
                _voiceJobKey168 = key;
                _voiceJobStartRemaining168 = remaining;
                _voiceJobStartOdometer168 = odometerKm;
                _voiceJobTravelledKm169 = 0.0;
                _voiceNextRandomKm168 = 500.0;
                _voiceLastRandomAtKm169 = -500.0;
                _voiceLastRemainingKm169 = remaining;
                _voiceLastOdometerKm169 = odometerKm;
                ClientStore.Log("voz 1.0.69: nova carga, contador real zerado");
                return;
            }

            // Parado = contador congelado. Atualiza apenas as referencias para nao criar saltos falsos.
            bool moving = !double.IsNaN(speed) && Math.Abs(speed) >= 1.0;
            if (!moving)
            {
                if (!double.IsNaN(remaining)) _voiceLastRemainingKm169 = remaining;
                if (!double.IsNaN(odometerKm)) _voiceLastOdometerKm169 = odometerKm;
                return;
            }

            double deltaKm = 0.0;

            // Preferencia: distancia restante da rota, que ja vem em km.
            if (!double.IsNaN(remaining) && !double.IsNaN(_voiceLastRemainingKm169))
            {
                double d = _voiceLastRemainingKm169 - remaining;
                // Ignora rerota, teleporte e saltos de telemetria.
                if (d > 0.001 && d <= 10.0) deltaKm = d;
            }

            // Fallback somente para campos explicitamente identificados como odometro em km.
            if (deltaKm <= 0.0 && !double.IsNaN(odometerKm) && !double.IsNaN(_voiceLastOdometerKm169))
            {
                double d = odometerKm - _voiceLastOdometerKm169;
                if (d > 0.001 && d <= 10.0) deltaKm = d;
            }

            if (!double.IsNaN(remaining)) _voiceLastRemainingKm169 = remaining;
            if (!double.IsNaN(odometerKm)) _voiceLastOdometerKm169 = odometerKm;
            if (deltaKm <= 0.0) return;

            _voiceJobTravelledKm169 += deltaKm;
            if (_voiceJobTravelledKm169 + 0.01 < _voiceNextRandomKm168) return;

            // Trava definitiva do marco: uma fala por bloco completo de 500 km.
            if (_voiceJobTravelledKm169 - _voiceLastRandomAtKm169 < 499.0) return;

            if (VoicePlayRandom168())
            {
                _voiceLastRandomAtKm169 = _voiceJobTravelledKm169;
                _voiceNextRandomKm168 = (Math.Floor(_voiceJobTravelledKm169 / 500.0) + 1.0) * 500.0;
                ClientStore.Log("voz 1.0.69: marco concluido; proxima fala em " + _voiceNextRandomKm168.ToString("0", CultureInfo.InvariantCulture) + " km");
            }
        }
        catch { }
    }
'@
$voiceText = Replace-Between169 $voiceText $observeStart $observeEnd $observeBlock

# Validacoes: velocidade continua intocada; apenas o disparo aleatorio foi endurecido.
foreach ($m in @('CurrentVersion = "1.0.69.0"','Cliente 1.0.69')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.69 sem $m" }
}
foreach ($m in @('Cliente 1.0.69 TESTE','BuildVoiceSettings062(p);')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.69 sem $m" }
}
foreach ($m in @('_voiceJobTravelledKm169','VoiceStableJobKey169','Math.Abs(speed) >= 1.0','_voiceNextRandomKm168 = 500.0','_voiceLastRandomAtKm169','VoiceObserveDamage168','_voiceRandomTimer062.Enabled = false')) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.69 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }

Write-Host 'Patch 1.0.69 aplicado: fala aleatoria somente com movimento real e um unico disparo a cada 500 km da carga.'
