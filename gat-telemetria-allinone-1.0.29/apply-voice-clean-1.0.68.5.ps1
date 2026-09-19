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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.68 incompleto para aplicar VOZ LIMPA 1.0.68.5.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

function Replace-Between1681([string]$text, [string]$startMarker, [string]$endMarker, [string]$replacement) {
    $s = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    if ($s -lt 0) { throw "Inicio nao encontrado 1.0.68.5: $startMarker" }
    $e = $text.IndexOf($endMarker, $s + $startMarker.Length, [StringComparison]::Ordinal)
    if ($e -lt 0) { throw "Fim nao encontrado 1.0.68.5: $endMarker" }
    return $text.Substring(0, $s) + $replacement.TrimEnd() + "`r`n`r`n" + $text.Substring($e)
}

# Mantem a linha 1.0.68 como base, mas cria uma revisao de teste separada.
$mainText = $mainText.Replace('CurrentVersion = "1.0.68.0"', 'CurrentVersion = "1.0.68.5"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68"', 'Text = "Cliente 1.0.68.5"')
$mainText = $mainText.Replace('HUB 1.0.68:', 'HUB 1.0.68.5:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.68"', 'Text = "GAT Telemetria BETA 1.0.68.5 VOZ LIMPA"', 1)
$hubText = $hubText.Replace('Cliente 1.0.68 TESTE', 'Cliente 1.0.68.5 VOZ LIMPA TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.68.5 VOZ LIMPA')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.68 TESTE', 'Cliente: 1.0.68.5 VOZ LIMPA TESTE')
}

# A preferencia de voz do DASH nao controla mais o motor de audio do Telemetria.
$voiceText = $voiceText.Replace('if (o["voice"] != null) _voiceMuted062 = !Convert.ToBoolean(o["voice"]);', '_voiceMuted062 = false; // 1.0.68.5: voz pertence ao Telemetria, nao ao DASH.')

# Toda fala solicitada pelo DASH e consumida sem reproduzir audio.
$dashHandler = @'
    private bool VoiceHandleDashSpeak062(string text)
    {
        // 1.0.68.5 VOZ LIMPA: o DASH e somente visual.
        // Nenhum "speak" vindo do HTML pode acionar MP3 ou TTS.
        return true;
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private bool VoiceHandleDashSpeak062(string text)' '    private static double VoiceParsePercent064(string text)' $dashHandler

# O DASH tambem nao pode mutar/desmutar a voz principal.
$dashSettings = @'
    private void VoiceSyncDashSetting062(JObject message)
    {
        // 1.0.68.5: configuracao de voz e local do GAT Telemetria.
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private void VoiceSyncDashSetting062(JObject message)' '    private static double VoiceMetric062' $dashSettings

# Impede o TTS antigo/legacy. Trabalho iniciado/finalizado agora vem somente da telemetria direta abaixo.
$legacyHandler = @'
    private bool VoiceHandleLegacySpeak062(string text, string logLabel)
    {
        // 1.0.68.5 VOZ LIMPA: evita qualquer segunda voz/TTS legado.
        return true;
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private bool VoiceHandleLegacySpeak062(string text, string logLabel)' '    private bool VoiceHandleDashSpeak062(string text)' $legacyHandler

# Filtro central: somente inicio/fim de trabalho usam grupos; velocidade usa o MP3 numerico direto.
$groupHead = @'
    private bool VoicePlayGroup062(string group, bool interrupt = true)
    {
'@
if ($voiceText.Contains($groupHead) -and $voiceText -notlike '*VOZ LIMPA: grupos permitidos*') {
    $groupHeadNew = @'
    private bool VoicePlayGroup062(string group, bool interrupt = true)
    {
        // VOZ LIMPA: grupos permitidos neste teste.
        string cleanGroup1681 = (group ?? string.Empty).ToLowerInvariant();
        if (cleanGroup1681 != "cargo_start" && cleanGroup1681 != "delivery") return true;
'@
    $voiceText = $voiceText.Replace($groupHead, $groupHeadNew)
}

# Estado unico do motor limpo.
$fieldMarker = '    [DllImport("winmm.dll", CharSet = CharSet.Auto)]'
if ($voiceText.Contains($fieldMarker) -and $voiceText -notlike '*_voiceCleanLastLimit1681*') {
$fields = @'
    private int _voiceCleanLastLimit1681;
    private DateTime _voiceCleanLastLimitAt1681 = DateTime.MinValue;
    private string _voiceCleanJobKey1681 = string.Empty;
    private bool _voiceCleanJobActive1681;
    private DateTime _voiceCleanLastJobStart1681 = DateTime.MinValue;
    private DateTime _voiceCleanLastJobEnd1681 = DateTime.MinValue;
    private DateTime _voiceCleanLastRoadAlert1681 = DateTime.MinValue;
    private int _voiceCleanLastRoadLimit1681;
    private bool _voiceCleanOverspeed1681;
    private System.Net.Http.HttpClient _voiceRoadHttp1683;
    private System.Windows.Forms.Timer _voiceRoadTimer1683;
    private bool _voiceRoadBusy1683;
    private bool _voiceRoadConnectedLogged1683;
    private DateTime _voiceRoadLastError1683 = DateTime.MinValue;

'@
    $voiceText = $voiceText.Replace($fieldMarker, $fields + $fieldMarker)
}

# Helpers do motor limpo: limite vem direto do TruckSim GPS/telemetria.
$cleanMarker = '    private void VoiceSyncDashSetting062(JObject message)'
if ($voiceText.Contains($cleanMarker) -and $voiceText -notlike '*private void VoiceCleanObserve1681*') {
$cleanMethods = @'
    private static double VoiceCleanPathNumber1681(JObject root, params string[] paths)
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

    private static int VoiceCleanSpeedLimit1681(double roadLimit)
    {
        if (double.IsNaN(roadLimit) || double.IsInfinity(roadLimit) || roadLimit <= 0) return 0;
        int rounded = (int)Math.Round(roadLimit, MidpointRounding.AwayFromZero);
        if (SpeedLimitValues065.Contains(rounded)) return rounded;

        int nearest = (int)Math.Round(roadLimit / 10.0, MidpointRounding.AwayFromZero) * 10;
        if (SpeedLimitValues065.Contains(nearest) && Math.Abs(roadLimit - nearest) <= 1.5) return nearest;
        return 0;
    }

    private void VoiceRoadInitialize1683()
    {
        if (_voiceRoadTimer1683 != null) return;

        _voiceRoadHttp1683 = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _voiceRoadTimer1683 = new System.Windows.Forms.Timer { Interval = 450 };
        _voiceRoadTimer1683.Tick += async delegate
        {
            if (_voiceRoadBusy1683) return;
            _voiceRoadBusy1683 = true;
            try
            {
                string json1683 = await _voiceRoadHttp1683.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry");
                JObject raw1683 = JObject.Parse(json1683);
                VoiceCleanObserveRoad1683(raw1683, DateTime.UtcNow);

                if (!_voiceRoadConnectedLogged1683)
                {
                    _voiceRoadConnectedLogged1683 = true;
                    ClientStore.Log("VOZ LIMPA 1.0.68.5 TruckSim direto conectado");
                }
            }
            catch (Exception ex1683)
            {
                if ((DateTime.UtcNow - _voiceRoadLastError1683).TotalSeconds >= 15)
                {
                    _voiceRoadLastError1683 = DateTime.UtcNow;
                    try { ClientStore.Log("VOZ LIMPA 1.0.68.5 TruckSim indisponivel: " + ex1683.Message); } catch { }
                }
            }
            finally
            {
                _voiceRoadBusy1683 = false;
            }
        };
        _voiceRoadTimer1683.Start();

        FormClosed += delegate
        {
            try { _voiceRoadTimer1683?.Stop(); _voiceRoadTimer1683?.Dispose(); } catch { }
            try { _voiceRoadHttp1683?.Dispose(); } catch { }
        };
    }

    private void VoiceCleanObserveRoad1683(JObject tele, DateTime now)
    {
        if (tele == null || _voiceMuted062 || _voiceVolume062 <= 0) return;

        double speed = VoiceCleanPathNumber1681(tele, "truck.speed", "Truck.Speed");
        double roadLimit = VoiceCleanPathNumber1681(tele, "navigation.speedLimit", "Navigation.SpeedLimit");

        if (double.IsNaN(speed) || double.IsNaN(roadLimit) || roadLimit <= 0)
        {
            _voiceCleanOverspeed1681 = false;
            return;
        }

        speed = Math.Abs(speed);
        int limit = VoiceCleanSpeedLimit1681(roadLimit);
        if (limit <= 0)
        {
            _voiceCleanOverspeed1681 = false;
            return;
        }

        bool over = speed > roadLimit;
        bool limitChanged = _voiceCleanLastRoadLimit1681 > 0 && limit != _voiceCleanLastRoadLimit1681;

        if (!over)
        {
            _voiceCleanOverspeed1681 = false;
            _voiceCleanLastRoadLimit1681 = limit;
            return;
        }

        if (limitChanged) _voiceCleanOverspeed1681 = false;

        bool due = !_voiceCleanOverspeed1681 || (now - _voiceCleanLastRoadAlert1681).TotalSeconds >= 15.0;
        if (due)
        {
            bool played = false;
            string direct = VoiceFindSpeed166(limit);
            if (!string.IsNullOrWhiteSpace(direct))
                played = VoicePlayFile166(direct, true);
            if (!played)
                played = VoicePlaySpeedLimit065(limit);

            if (played)
            {
                _voiceCleanLastRoadAlert1681 = now;
                _voiceCleanOverspeed1681 = true;
                ClientStore.Log(
                    "VOZ LIMPA 1.0.68.5 excesso direto TruckSim: limite " + limit.ToString(CultureInfo.InvariantCulture) +
                    " km/h | velocidade " + Math.Round(speed).ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                ClientStore.Log("VOZ LIMPA 1.0.68.5 sem MP3 para limite: " + limit + " km/h");
            }
        }

        _voiceCleanLastRoadLimit1681 = limit;
    }

    private void VoiceCleanObserve1681(JObject tele, DateTime now)
    {
        if (tele == null || _voiceMuted062 || _voiceVolume062 <= 0) return;

        // Excesso de velocidade: leitura direta do TruckSim GPS, sem depender do DASH.
        double speed = VoiceCleanPathNumber1681(tele, "truck.speed", "Truck.Speed");
        double roadLimit = VoiceCleanPathNumber1681(tele, "navigation.speedLimit", "Navigation.SpeedLimit");

        if (!double.IsNaN(speed) && !double.IsNaN(roadLimit) && roadLimit > 0)
        {
            speed = Math.Abs(speed);
            int limit = VoiceCleanSpeedLimit1681(roadLimit);

            if (limit > 0)
            {
                bool over = speed > roadLimit;
                bool limitChanged = _voiceCleanLastRoadLimit1681 > 0 && limit != _voiceCleanLastRoadLimit1681;

                if (!over)
                {
                    _voiceCleanOverspeed1681 = false;
                    _voiceCleanLastRoadLimit1681 = limit;
                }
                else
                {
                    if (limitChanged) _voiceCleanOverspeed1681 = false;

                    bool due = !_voiceCleanOverspeed1681 || (now - _voiceCleanLastRoadAlert1681).TotalSeconds >= 15.0;
                    if (due)
                    {
                        bool played = false;
                        string direct = VoiceFindSpeed166(limit);
                        if (!string.IsNullOrWhiteSpace(direct))
                            played = VoicePlayFile166(direct, true);
                        if (!played)
                            played = VoicePlaySpeedLimit065(limit);

                        if (played)
                        {
                            _voiceCleanLastRoadAlert1681 = now;
                            _voiceCleanOverspeed1681 = true;
                            ClientStore.Log(
                                "VOZ LIMPA 1.0.68.5 excesso: limite " + limit.ToString(CultureInfo.InvariantCulture) +
                                " km/h | velocidade " + Math.Round(speed).ToString(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            ClientStore.Log("VOZ LIMPA 1.0.68.5 sem MP3 para limite: " + limit + " km/h");
                        }
                    }

                    _voiceCleanLastRoadLimit1681 = limit;
                }
            }
            else
            {
                _voiceCleanOverspeed1681 = false;
            }
        }
        else
        {
            _voiceCleanOverspeed1681 = false;
        }

        // Inicio/fim de trabalho.
        bool ended = VoiceJobEnded168(tele);
        bool active = VoiceJobActiveNow168(tele);
        string key = VoiceJobKey168(tele);

        if (ended)
        {
            if (_voiceCleanJobActive1681 && (now - _voiceCleanLastJobEnd1681).TotalSeconds >= 4)
            {
                _voiceCleanLastJobEnd1681 = now;
                VoicePlayGroup062("delivery");
                ClientStore.Log("VOZ LIMPA 1.0.68.5 trabalho finalizado");
            }
            _voiceCleanJobActive1681 = false;
            _voiceCleanJobKey1681 = string.Empty;
            return;
        }

        if (active)
        {
            bool newJob = !_voiceCleanJobActive1681;
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(_voiceCleanJobKey1681) &&
                !string.Equals(key, _voiceCleanJobKey1681, StringComparison.OrdinalIgnoreCase))
                newJob = true;

            if (newJob && (now - _voiceCleanLastJobStart1681).TotalSeconds >= 4)
            {
                _voiceCleanLastJobStart1681 = now;
                VoicePlayGroup062("cargo_start");
                ClientStore.Log("VOZ LIMPA 1.0.68.5 trabalho iniciado");
            }

            _voiceCleanJobActive1681 = true;
            if (!string.IsNullOrWhiteSpace(key)) _voiceCleanJobKey1681 = key;
        }
    }

'@
    $voiceText = $voiceText.Replace($cleanMarker, $cleanMethods + $cleanMarker)
}

# 1.0.68.5: inicia um polling proprio do TruckSim GPS, sem depender da aba/janela do DASH.
$voiceInitStart1683 = '        _voiceRandomTimer062.Start();'
if ($voiceText.Contains($voiceInitStart1683) -and $voiceText -notlike '*_voiceRoadTimer1683 = new System.Windows.Forms.Timer*') {
$voiceInitBlock1683 = @'
        _voiceRandomTimer062.Start();

        _voiceRoadHttp1683 = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _voiceRoadTimer1683 = new System.Windows.Forms.Timer { Interval = 450 };
        _voiceRoadTimer1683.Tick += async delegate
        {
            if (_voiceRoadBusy1683) return;
            _voiceRoadBusy1683 = true;
            try
            {
                string json1683 = await _voiceRoadHttp1683.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry");
                JObject raw1683 = JObject.Parse(json1683);
                VoiceCleanObserveRoad1683(raw1683, DateTime.UtcNow);

                if (!_voiceRoadConnectedLogged1683)
                {
                    _voiceRoadConnectedLogged1683 = true;
                    ClientStore.Log("VOZ LIMPA 1.0.68.5 TruckSim direto conectado");
                }
            }
            catch (Exception ex1683)
            {
                if ((DateTime.UtcNow - _voiceRoadLastError1683).TotalSeconds >= 15)
                {
                    _voiceRoadLastError1683 = DateTime.UtcNow;
                    try { ClientStore.Log("VOZ LIMPA 1.0.68.5 TruckSim indisponivel: " + ex1683.Message); } catch { }
                }
            }
            finally
            {
                _voiceRoadBusy1683 = false;
            }
        };
        _voiceRoadTimer1683.Start();
'@
    $voiceText = $voiceText.Replace($voiceInitStart1683, $voiceInitBlock1683.TrimEnd())
}

# Encerra o polling proprio junto com o aplicativo.
$voiceClose1683 = '            try { _voiceRandomTimer062.Stop(); _voiceRandomTimer062.Dispose(); } catch { }'
if ($voiceText.Contains($voiceClose1683) -and $voiceText -notlike '*_voiceRoadHttp1683?.Dispose()*') {
    $voiceText = $voiceText.Replace(
        $voiceClose1683,
        $voiceClose1683 + "`r`n            try { _voiceRoadTimer1683?.Stop(); _voiceRoadTimer1683?.Dispose(); } catch { }`r`n            try { _voiceRoadHttp1683?.Dispose(); } catch { }"
    )
}

# Garante que o motor de velocidade inicie junto com o GAT Telemetria, mesmo sem abrir o DASH.
if ($hubText -notlike '*VoiceRoadInitialize1683();*') {
    $voiceInitHub1683 = '        VoiceInitialize062();'
    if (-not $hubText.Contains($voiceInitHub1683)) { throw 'VoiceInitialize062 do Hub nao encontrado para 1.0.68.5.' }
    $hubText = $hubText.Replace($voiceInitHub1683, $voiceInitHub1683 + "`r`n        VoiceRoadInitialize1683();")
}

# O observador antigo continua sendo o ponto de entrada, mas retorna apos o motor limpo.
$observeNeedle = '        _voiceSeenTelemetry062 = true;'
if ($voiceText.Contains($observeNeedle) -and $voiceText -notlike '*VoiceCleanObserve1681(tele, now);*') {
    $voiceText = $voiceText.Replace($observeNeedle, $observeNeedle + "`r`n        VoiceCleanObserve1681(tele, now);`r`n        return; // 1.0.68.5: bloqueia chuva/dano/random/fuel/arriving antigos.")
}

# O botao TESTAR VOZ usa a mesma funcao numerica do jogo: limite de 80 km/h.
$voiceText = [regex]::Replace(
    $voiceText,
    'test\.Click \+= delegate \{ if \(!VoicePlayGroup062\("startup"\)\) MessageBox\.Show\("Nenhuma voz pronta para teste\.", "Voz GAT", MessageBoxButtons\.OK, MessageBoxIcon\.Information\); \};',
    'test.Click += delegate { _voiceMuted062 = false; if (_voiceVolume062 <= 0) _voiceVolume062 = 80; if (!VoicePlaySpeedLimit065(80)) MessageBox.Show("Falha ao tocar C:\\Program Files\\GAT Telemetria\\voz\\limites\\limite_080.mp3. Verifique a instalacao.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); };',
    1
)
$voiceText = $voiceText.Replace('VOZ E ALERTAS • PACOTES DE VOZ', 'VOZ LOCAL • GAT TELEMETRIA')
$voiceText = $voiceText.Replace('Pacote ativo • " + general + " falas gerais • " + limits + " limites • " + extras + " extras. Substitua ou acrescente vozes sem atualizar o GAT.', 'Motor de voz local • " + limits + " limites instalados • DASH sem controle de audio • teste pelo limite 80 km/h.')


# 1.0.68.5: configuracao de volume tambem e limpa e independente das versoes antigas.
# Nao reutiliza voice-volume-1.0.62.txt.
$oldVolumeFile1685 = '    private static string VoiceVolumeFile062() { return Path.Combine(VoiceDataDir062(), "voice-volume-1.0.62.txt"); }'
$newVolumeFile1685 = '    private static string VoiceVolumeFile062() { return Path.Combine(VoiceDataDir062(), "voice-volume-clean-1.0.68.5.txt"); }'
if ($voiceText.Contains($oldVolumeFile1685)) {
    $voiceText = $voiceText.Replace($oldVolumeFile1685, $newVolumeFile1685)
}

# Depois de carregar configuracao limpa, garante volume audivel no primeiro uso.
$loadCall1685 = '        VoiceLoadSettings062();'
if ($voiceText.Contains($loadCall1685) -and $voiceText -notlike '*1.0.68.5: volume limpo*') {
    $voiceText = $voiceText.Replace(
        $loadCall1685,
        $loadCall1685 + "`r`n        _voiceMuted062 = false;`r`n        if (_voiceVolume062 <= 0) _voiceVolume062 = 80; // 1.0.68.5: volume limpo"
    )
}

# O player nao pode fingir sucesso quando esta silencioso.
$voiceText = $voiceText.Replace(
    '        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;' + "`r`n" + '        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return false;',
    '        if (_voiceMuted062 || _voiceVolume062 <= 0) return false;' + "`r`n" + '        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return false;'
)

# 1.0.68.5: pasta de voz oficial fica junto do aplicativo.
# Nao usa voicepack166/167 antigo para os alertas de velocidade.
$oldSpeedDir1684 = @'
    private static string VoiceSpeedDir166()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "limits");
        Directory.CreateDirectory(dir);
        return dir;
    }
'@
$newSpeedDir1684 = @'
    private static string VoiceSpeedDir166()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voz", "limites");
    }
'@
if ($voiceText.Contains($oldSpeedDir1684)) {
    $voiceText = $voiceText.Replace($oldSpeedDir1684, $newSpeedDir1684)
} elseif ($voiceText -notlike '*AppDomain.CurrentDomain.BaseDirectory, "voz", "limites"*') {
    throw 'VoiceSpeedDir166 nao encontrado para apontar para a pasta instalada.'
}

# Desativa importacoes automaticas legadas. Nenhum audio sera procurado em Downloads ou voicepack antigo.
$voiceText = $voiceText.Replace('        VoiceTryAutoImport062();', '        // 1.0.68.5: autoimport legado desativado.')
$voiceText = $voiceText.Replace('        VoiceTryAutoImportSpeed065();', '        // 1.0.68.5: autoimport de limite legado desativado.')

# Limites: usa SOMENTE voz\limites\limite_XXX.mp3.
$patternSpeed1684 = '(?s)    private bool VoicePlaySpeedLimit065\(int limit\)\s*\{.*?\r?\n    \}\r?\n\r?\n    private void VoiceTryAutoImportSpeed065\(\)'
$newSpeed1684 = @'
    private bool VoicePlaySpeedLimit065(int limit)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;

        string file = VoiceFindSpeed166(limit);
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            ClientStore.Log("VOZ 1.0.68.5 arquivo ausente: limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3");
            return false;
        }

        bool ok = VoicePlayFile166(file, true);
        if (ok)
            ClientStore.Log("VOZ 1.0.68.5 tocando: " + Path.GetFileName(file));
        return ok;
    }

    private void VoiceTryAutoImportSpeed065()
'@
$replacedSpeed1684 = [regex]::Replace($voiceText, $patternSpeed1684, $newSpeed1684, 1)
if ($replacedSpeed1684 -eq $voiceText) { throw 'VoicePlaySpeedLimit065 nao encontrado para limpeza 1.0.68.5.' }
$voiceText = $replacedSpeed1684

# Inicio/fim permanecem filtrados pela base 1.0.68; esta revisao limpa especificamente o alerta de velocidade.
# O observador interno fica apenas com inicio/fim; velocidade e lida exclusivamente no polling direto TruckSim.
$obsStart1684 = $voiceText.IndexOf('    private void VoiceCleanObserve1681(JObject tele, DateTime now)')
if ($obsStart1684 -ge 0) {
    $speedComment1684 = $voiceText.IndexOf('        // Excesso de velocidade:', $obsStart1684)
    $jobComment1684 = $voiceText.IndexOf('        // Inicio/fim de trabalho.', $obsStart1684)
    if ($speedComment1684 -ge 0 -and $jobComment1684 -gt $speedComment1684) {
        $nl1684 = [Environment]::NewLine
        $voiceText = $voiceText.Substring(0, $speedComment1684) +
            '        // 1.0.68.5: velocidade e processada apenas pelo polling direto do TruckSim GPS.' + $nl1684 + $nl1684 +
            $voiceText.Substring($jobComment1684)
    }
}

# Texto da tela de configuracoes deixa claro de onde vem o audio.
$voiceText = $voiceText.Replace(
    'Motor de voz local • " + limits + " limites instalados • DASH sem controle de audio • teste pelo limite 80 km/h.',
    'Motor de voz local • pasta oficial: C:\\Program Files\\GAT Telemetria\\voz\\limites • volume limpo independente • TESTAR VOZ = limite 80.'
)


foreach ($m in @('CurrentVersion = "1.0.68.5"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm VOZ LIMPA sem $m" }
}
foreach ($m in @('VoiceHandleDashSpeak062(t);','VoiceRoadInitialize1683();')) {
    if ($hubText -notlike "*$m*") { throw "Hub VOZ LIMPA sem $m" }
}
foreach ($m in @('VoiceRoadInitialize1683','VoiceCleanObserve1681','VoiceCleanObserveRoad1683','VoiceCleanPathNumber1681','VoiceCleanSpeedLimit1681','_voiceRoadTimer1683','127.0.0.1:31377','AppDomain.CurrentDomain.BaseDirectory, "voz", "limites"','voice-volume-clean-1.0.68.5.txt','1.0.68.5: volume limpo','VOZ 1.0.68.5 tocando','VoicePlaySpeedLimit065(limit)','VoicePlaySpeedLimit065(80)','VOZ LIMPA: grupos permitidos','trabalho iniciado','trabalho finalizado','bloqueia chuva/dano/random/fuel/arriving antigos')) {
    if ($voiceText -notlike "*$m*") { throw "Voice062 VOZ LIMPA sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.68.5 VOZ LIMPA: voz independente do DASH, somente trabalho + limites + finalizacao.'
