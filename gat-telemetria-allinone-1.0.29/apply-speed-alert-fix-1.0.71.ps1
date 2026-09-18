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
# - Usa truck.speed + navigation.speedLimit com tolerancia fixa ZERO.
# - Regra exata: limite 40 permite 40; a partir de 41 dispara o alerta.
# - Repete no maximo a cada 15 s enquanto continuar acima; ao normalizar e exceder novamente, alerta de imediato.
# - A versao 1.0.71 NAO embute audio: o motorista importa os MP3s pelas Configuracoes.
# - Aceita 1 ou varios arquivos limite_020.mp3 ... limite_130.mp3.
# - Reimportar o mesmo limite substitui somente aquela fala, permitindo voz personalizada.
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

# Ajusta a interface de voz para importacao manual dos limites.
$voiceText = $voiceText.Replace('var replace = HubButton041("SUBSTITUIR VOZ", 135);', 'var replace = HubButton041("IMPORTAR LIMITES", 135);')
$voiceText = $voiceText.Replace('replace.Click += delegate { VoiceReplacePackage167(); };', 'replace.Click += delegate { VoiceImportLimits171(); };')
$voiceText = $voiceText.Replace('test.Click += delegate { if (!VoicePlayGroup062("startup")) MessageBox.Show("Nenhuma voz pronta para teste.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information); };', 'test.Click += delegate { VoiceTestLimit171(); };')

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
    private void VoiceTestLimit171()
    {
        try
        {
            foreach (int limit in new int[] {80,60,40,30,50,70,90,100,110,120,130,20})
            {
                string file = VoiceFindSpeed166(limit);
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) continue;
                if (!VoicePlaySpeedLimit065(limit))
                    MessageBox.Show("Nao foi possivel tocar a voz de " + limit + " km/h.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show("Nenhuma voz de limite instalada. Clique em IMPORTAR LIMITES e selecione os MP3s.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nao foi possivel testar a voz: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void VoiceImportLimits171()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Selecione as falas de limite de velocidade";
            dlg.Filter = "Arquivos MP3 (*.mp3)|*.mp3";
            dlg.Multiselect = true;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int imported = 0;
            int ignored = 0;
            int firstLimit = 0;
            try
            {
                VoiceStop062();
                VoiceBackupCurrent167();
                string dest = VoiceSpeedDir166();

                foreach (string src in dlg.FileNames ?? new string[0])
                {
                    int limit = VoiceLimitId167(src);
                    if (limit <= 0)
                    {
                        ignored++;
                        continue;
                    }

                    string name = "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3";
                    File.Copy(src, Path.Combine(dest, name), true);
                    if (firstLimit == 0) firstLimit = limit;
                    imported++;
                }

                VoiceRefreshUi062();

                if (imported <= 0)
                {
                    MessageBox.Show(
                        "Nenhum MP3 reconhecido. Use nomes como limite_020.mp3, limite_040.mp3, limite_080.mp3 ate limite_130.mp3.",
                        "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ClientStore.Log("voz 1.0.71: " + imported + " limite(s) importado(s), " + ignored + " ignorado(s)");
                MessageBox.Show(
                    imported + " fala(s) de limite importada(s)." +
                    (ignored > 0 ? "\n" + ignored + " arquivo(s) ignorado(s) por nome invalido." : "") +
                    "\n\nPara trocar uma fala depois, importe outro MP3 com o mesmo nome.",
                    "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (firstLimit > 0) VoicePlaySpeedLimit065(firstLimit);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nao foi possivel importar as falas: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

    private static double VoiceTolerance171()
    {
        // 1.0.71: tolerancia fixa ZERO. O limite e exato:
        // 40 km/h = normal; 41 km/h = excesso.
        return 0.0;
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
    'VoiceImportLimits171',
    'VoiceTestLimit171',
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

Write-Host 'GAT Telemetria 1.0.71: alerta direto, tolerancia ZERO e importacao manual de vozes aplicada.'
