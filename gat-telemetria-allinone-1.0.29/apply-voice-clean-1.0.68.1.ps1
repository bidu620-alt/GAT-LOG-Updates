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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.68 incompleto para aplicar VOZ LIMPA 1.0.68.1.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

function Replace-Between1681([string]$text, [string]$startMarker, [string]$endMarker, [string]$replacement) {
    $s = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    if ($s -lt 0) { throw "Inicio nao encontrado 1.0.68.1: $startMarker" }
    $e = $text.IndexOf($endMarker, $s + $startMarker.Length, [StringComparison]::Ordinal)
    if ($e -lt 0) { throw "Fim nao encontrado 1.0.68.1: $endMarker" }
    return $text.Substring(0, $s) + $replacement.TrimEnd() + "`r`n`r`n" + $text.Substring($e)
}

# Mantem a linha 1.0.68 como base, mas cria uma revisao de teste separada.
$mainText = $mainText.Replace('CurrentVersion = "1.0.68.0"', 'CurrentVersion = "1.0.68.1"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68"', 'Text = "Cliente 1.0.68.1"')
$mainText = $mainText.Replace('HUB 1.0.68:', 'HUB 1.0.68.1:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.68"', 'Text = "GAT Telemetria BETA 1.0.68.1 VOZ LIMPA"', 1)
$hubText = $hubText.Replace('Cliente 1.0.68 TESTE', 'Cliente 1.0.68.1 VOZ LIMPA TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.68', 'GAT Telemetria BETA 1.0.68.1 VOZ LIMPA')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.68 TESTE', 'Cliente: 1.0.68.1 VOZ LIMPA TESTE')
}

# A preferencia de voz do DASH nao controla mais o motor de audio do Telemetria.
$voiceText = $voiceText.Replace('if (o["voice"] != null) _voiceMuted062 = !Convert.ToBoolean(o["voice"]);', '_voiceMuted062 = false; // 1.0.68.1: voz pertence ao Telemetria, nao ao DASH.')

# Toda fala solicitada pelo DASH e consumida sem reproduzir audio.
$dashHandler = @'
    private bool VoiceHandleDashSpeak062(string text)
    {
        // 1.0.68.1 VOZ LIMPA: o DASH e somente visual.
        // Nenhum "speak" vindo do HTML pode acionar MP3 ou TTS.
        return true;
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private bool VoiceHandleDashSpeak062(string text)' '    private void VoiceSyncDashSetting062(JObject message)' $dashHandler

# O DASH tambem nao pode mutar/desmutar a voz principal.
$dashSettings = @'
    private void VoiceSyncDashSetting062(JObject message)
    {
        // 1.0.68.1: configuracao de voz e local do GAT Telemetria.
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private void VoiceSyncDashSetting062(JObject message)' '    private static double VoiceMetric062' $dashSettings

# Impede o TTS antigo/legacy. Trabalho iniciado/finalizado agora vem somente da telemetria direta abaixo.
$legacyHandler = @'
    private bool VoiceHandleLegacySpeak062(string text, string logLabel)
    {
        // 1.0.68.1 VOZ LIMPA: evita qualquer segunda voz/TTS legado.
        return true;
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private bool VoiceHandleLegacySpeak062(string text, string logLabel)' '    private bool VoiceHandleDashSpeak062(string text)' $legacyHandler

# Desliga o detector visual antigo de batida e qualquer protecao associada a ele.
$damageNoop = @'
    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)
    {
        // 1.0.68.1 VOZ LIMPA: eventos de dano ficam desativados neste teste.
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)' '    private void VoiceTriggerCrash064(DateTime now, double rise)' $damageNoop

$crashNoop = @'
    private void VoiceTriggerCrash064(DateTime now, double rise)
    {
        // 1.0.68.1 VOZ LIMPA: nenhuma fala de batida nesta revisao.
    }
'@
$voiceText = Replace-Between1681 $voiceText '    private void VoiceTriggerCrash064(DateTime now, double rise)' '    private void VoiceSyncDashSetting062(JObject message)' $crashNoop

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

'@
    $voiceText = $voiceText.Replace($fieldMarker, $fields + $fieldMarker)
}

# Helpers do motor limpo: limite vem direto do TruckSim GPS/telemetria.
$cleanMarker = '    private void ObserveDamageDisplay064(string cargo, string engine, string transmission, string cabin, string chassis, string wheels, string trailer, string speedText)'
if ($voiceText.Contains($cleanMarker) -and $voiceText -notlike '*private void VoiceCleanObserve1681*') {
$cleanMethods = @'
    private static int VoiceCleanSpeedLimit1681(JObject tele)
    {
        double raw = VoiceMetric062(tele, "speedLimit", "speed_limit", "navigationSpeedLimit");
        if (double.IsNaN(raw) || raw <= 0) return 0;

        foreach (int supported in SpeedLimitValues065)
            if (Math.Abs(raw - supported) <= 1.5) return supported;

        // Alguns provedores de telemetria podem entregar o limite em m/s.
        if (raw >= 5.0 && raw <= 40.0)
        {
            double kmh = raw * 3.6;
            int near = (int)(Math.Round(kmh / 10.0) * 10.0);
            if (SpeedLimitValues065.Contains(near) && Math.Abs(kmh - near) <= 3.5) return near;
        }

        int rounded = (int)(Math.Round(raw / 10.0) * 10.0);
        if (SpeedLimitValues065.Contains(rounded) && Math.Abs(raw - rounded) <= 2.0) return rounded;
        return 0;
    }

    private void VoiceCleanObserve1681(JObject tele, DateTime now)
    {
        if (tele == null || _voiceMuted062 || _voiceVolume062 <= 0) return;

        // 1) Limite de velocidade: fala pelo proprio Telemetria, sem depender do DASH.
        int limit = VoiceCleanSpeedLimit1681(tele);
        if (limit > 0 && limit != _voiceCleanLastLimit1681)
        {
            _voiceCleanLastLimit1681 = limit;
            if ((now - _voiceCleanLastLimitAt1681).TotalMilliseconds >= 900)
            {
                _voiceCleanLastLimitAt1681 = now;
                if (VoicePlaySpeedLimit065(limit))
                    ClientStore.Log("VOZ LIMPA 1.0.68.1 limite direto: " + limit + " km/h");
                else
                    ClientStore.Log("VOZ LIMPA 1.0.68.1 sem MP3 para limite: " + limit + " km/h");
            }
        }

        // 2) Inicio/fim de trabalho: um unico gatilho, diretamente pelo estado da carga.
        bool ended = VoiceJobEnded168(tele);
        bool active = VoiceJobActiveNow168(tele);
        string key = VoiceJobKey168(tele);

        if (ended)
        {
            if (_voiceCleanJobActive1681 && (now - _voiceCleanLastJobEnd1681).TotalSeconds >= 4)
            {
                _voiceCleanLastJobEnd1681 = now;
                VoicePlayGroup062("delivery");
                ClientStore.Log("VOZ LIMPA 1.0.68.1 trabalho finalizado");
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
                ClientStore.Log("VOZ LIMPA 1.0.68.1 trabalho iniciado");
            }

            _voiceCleanJobActive1681 = true;
            if (!string.IsNullOrWhiteSpace(key)) _voiceCleanJobKey1681 = key;
        }
    }

'@
    $voiceText = $voiceText.Replace($cleanMarker, $cleanMethods + $cleanMarker)
}

# O observador antigo continua sendo o ponto de entrada, mas retorna apos o motor limpo.
$observeNeedle = '        _voiceSeenTelemetry062 = true;'
if ($voiceText.Contains($observeNeedle) -and $voiceText -notlike '*VoiceCleanObserve1681(tele, now);*') {
    $voiceText = $voiceText.Replace($observeNeedle, $observeNeedle + "`r`n        VoiceCleanObserve1681(tele, now);`r`n        return; // 1.0.68.1: bloqueia chuva/dano/random/fuel/arriving antigos.")
}

# O botao TESTAR VOZ usa a mesma funcao numerica do jogo: limite de 80 km/h.
$voiceText = [regex]::Replace(
    $voiceText,
    'test\.Click \+= delegate \{ if \(!VoicePlayGroup062\("startup"\)\) MessageBox\.Show\("Nenhuma voz pronta para teste\.", "Voz GAT", MessageBoxButtons\.OK, MessageBoxIcon\.Information\); \};',
    'test.Click += delegate { if (!VoicePlaySpeedLimit065(80)) MessageBox.Show("MP3 do limite 80 km/h nao encontrado. Importe/substitua o pacote de voz.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information); };',
    1
)
$voiceText = $voiceText.Replace('VOZ E ALERTAS • PACOTES DE VOZ', 'VOZ LOCAL • GAT TELEMETRIA')
$voiceText = $voiceText.Replace('Pacote ativo • " + general + " falas gerais • " + limits + " limites • " + extras + " extras. Substitua ou acrescente vozes sem atualizar o GAT.', 'Motor de voz local • " + limits + " limites instalados • DASH sem controle de audio • teste pelo limite 80 km/h.')

foreach ($m in @('CurrentVersion = "1.0.68.1"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm VOZ LIMPA sem $m" }
}
foreach ($m in @('VoiceHandleDashSpeak062(t);')) {
    if ($hubText -notlike "*$m*") { throw "Hub VOZ LIMPA sem $m" }
}
foreach ($m in @('VoiceCleanObserve1681','VoiceCleanSpeedLimit1681','return true;','VoicePlaySpeedLimit065(limit)','trabalho iniciado','trabalho finalizado','bloqueia chuva/dano/random/fuel/arriving antigos')) {
    if ($voiceText -notlike "*$m*") { throw "Voice062 VOZ LIMPA sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.68.1 VOZ LIMPA: voz independente do DASH, somente trabalho + limites + finalizacao.'
