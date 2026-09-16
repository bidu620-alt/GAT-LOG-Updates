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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.62 incompleto para aplicar 1.0.63.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# ---------------------------------------------------------------------------
# 1.0.63 TESTE - sincronizacao/prioridade de voz.
# 1) O texto dinamico de limite volta a ser falado exatamente como o DASH manda,
#    preservando o numero da via e o mesmo momento de aviso da versao anterior.
# 2) Batida usa a telemetria bruta e ganha prioridade maxima.
# 3) Enquanto uma batida fala, nenhum alerta menor pode cortar a frase.
# ---------------------------------------------------------------------------
$mainText = $mainText.Replace('CurrentVersion = "1.0.62.0"', 'CurrentVersion = "1.0.63.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.62"', 'Text = "Cliente 1.0.63"')
$mainText = $mainText.Replace('HUB 1.0.62:', 'HUB 1.0.63:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.62"', 'Text = "GAT Telemetria BETA 1.0.63"', 1)
$hubText = $hubText.Replace('Cliente 1.0.62 TESTE', 'Cliente 1.0.63 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.62', 'GAT Telemetria BETA 1.0.63')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.62 TESTE', 'Cliente: 1.0.63 TESTE')
}

# O handler do DASH deixa toda decisao de fala para VoiceHandleDashSpeak062.
# Assim o texto dinamico "Limite da via: 80 km/h" nao vira mais uma frase generica.
$oldSpeak = 'else if (type == "speak") { string t = (Convert.ToString(m["text"]) ?? "").Trim(); if (t.Length > 0 && t.Length < 240 && !VoiceHandleDashSpeak062(t)) { try { if (_voice == null) _voice = new System.Speech.Synthesis.SpeechSynthesizer(); _voice.Volume = VoiceVolume062Value(); _voice.SpeakAsyncCancelAll(); _voice.SpeakAsync(t); } catch { } } }'
$newSpeak = 'else if (type == "speak") { string t = (Convert.ToString(m["text"]) ?? "").Trim(); if (t.Length > 0 && t.Length < 240) VoiceHandleDashSpeak062(t); }'
if ($hubText.Contains($oldSpeak)) { $hubText = $hubText.Replace($oldSpeak, $newSpeak) }
elseif ($hubText -notlike '*VoiceHandleDashSpeak062(t);*') { throw 'Handler speak 1.0.62 nao encontrado.' }

# Batida deve observar a leitura crua, antes de conta/servidor/estabilizacao.
$rawNeedle = '_tripJournal.Observe(jObject);'
if ($mainText.Contains($rawNeedle) -and $mainText -notlike '*ObserveVoiceTelemetry062(jObject);*') {
    $mainText = $mainText.Replace($rawNeedle, 'ObserveVoiceTelemetry062(jObject);' + "`r`n`t`t`t`t" + $rawNeedle)
}

# Campos de prioridade e fila do alerta dinamico.
$fieldNeedle = '    private bool _voiceArriving062;'
if ($voiceText.Contains($fieldNeedle) -and $voiceText -notlike '*_voiceCrashProtectUntil063*') {
    $fields = @'
    private bool _voiceArriving062;
    private DateTime _voiceCrashProtectUntil063 = DateTime.MinValue;
    private DateTime _voiceDynamicProtectUntil063 = DateTime.MinValue;
    private int _voiceDynamicSerial063;
    private string _voicePendingDynamic063 = string.Empty;
'@.TrimEnd()
    $voiceText = $voiceText.Replace($fieldNeedle, $fields)
}

# Nao permite que piada/fuel/dano/etc cortem batida ou aviso dinamico de limite.
if ($voiceText -notlike '*GAT_VOICE_PRIORITY_063*') {
    $groupPattern = '(?s)(    private bool VoicePlayGroup062\(string group, bool interrupt = true\)\s*\{\s*)int first = 0, last = 0;'
    if (-not [regex]::IsMatch($voiceText, $groupPattern)) { throw 'VoicePlayGroup062 nao encontrado.' }
    $groupBody = @'
        // GAT_VOICE_PRIORITY_063
        string priorityGroup063 = (group ?? "").Trim().ToLowerInvariant();
        DateTime priorityNow063 = DateTime.UtcNow;
        if (priorityGroup063 != "crash" && priorityNow063 < _voiceCrashProtectUntil063) return false;
        if (priorityGroup063 != "crash" && priorityNow063 < _voiceDynamicProtectUntil063) return false;
        int first = 0, last = 0;
'@
    $voiceText = [regex]::Replace($voiceText, $groupPattern, ('$1' + $groupBody), 1)
}

# Restaura a fala dinamica de velocidade/limite exatamente como o DASH envia.
$methodPattern = '(?s)    private bool VoiceHandleDashSpeak062\(string text\)\s*\{.*?\r?\n    \}\s*\r?\n\s*    private void VoiceSyncDashSetting062'
if (-not [regex]::IsMatch($voiceText, $methodPattern)) { throw 'VoiceHandleDashSpeak062 nao encontrado.' }
$methodReplacement = @'
    private bool VoiceHandleDashSpeak062(string text)
    {
        if (_voiceMuted062) return true;
        string t = (text ?? "").Trim();
        if (t.Length == 0) return true;
        string s = t.ToLowerInvariant();

        // Velocidade/limite e texto dinamico: preserva exatamente a frase antiga do DASH,
        // inclusive o numero do limite da via e o instante/tolerancia em que ele pediu a fala.
        bool dynamicSpeed063 = s.Contains("veloc") || s.Contains("limite") || s.Contains("reduza") ||
                               s.Contains("reduzir") || s.Contains("quilômetro") || s.Contains("km/h");
        if (dynamicSpeed063)
        {
            VoiceSpeakDynamic063(t);
            return true;
        }

        if (s.Contains("radar") || s.Contains("fiscaliza"))
        {
            if (!VoicePlayGroup062("radar")) VoiceSpeakDynamic063(t);
            return true;
        }

        VoiceSpeakDynamic063(t);
        return true;
    }

    private void VoiceSpeakDynamic063(string text)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0 || string.IsNullOrWhiteSpace(text)) return;
        DateTime now063 = DateTime.UtcNow;

        // Batida sempre termina primeiro; guarda somente o aviso dinamico mais recente.
        if (now063 < _voiceCrashProtectUntil063)
        {
            VoiceQueueDynamic063(text, _voiceCrashProtectUntil063);
            return;
        }

        try { VoiceStop062(); } catch { }
        try
        {
            System.Speech.Synthesis.SpeechSynthesizer v063 = EnsureVoice();
            v063.Volume = VoiceVolume062Value();
            v063.SpeakAsyncCancelAll();
            v063.SpeakAsync(text);
            // Durante alguns segundos falas decorativas nao podem sobrepor o limite.
            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(3600);
            ClientStore.Log("voz limite dinamica 1.0.63: " + text);
        }
        catch (Exception ex)
        {
            ClientStore.Log("voz limite dinamica indisponivel: " + ex.Message);
        }
    }

    private void VoiceQueueDynamic063(string text, DateTime after063)
    {
        _voicePendingDynamic063 = text ?? string.Empty;
        int serial063 = Interlocked.Increment(ref _voiceDynamicSerial063);
        int delay063 = Math.Max(100, (int)Math.Min(6000, (after063 - DateTime.UtcNow).TotalMilliseconds + 120));
        Task.Run(async delegate
        {
            await Task.Delay(delay063);
            try
            {
                if (IsDisposed || Disposing) return;
                BeginInvoke((Action)delegate
                {
                    if (serial063 != _voiceDynamicSerial063 || _voiceMuted062) return;
                    string pending063 = _voicePendingDynamic063;
                    _voicePendingDynamic063 = string.Empty;
                    if (!string.IsNullOrWhiteSpace(pending063)) VoiceSpeakDynamic063(pending063);
                });
            }
            catch { }
        });
    }

    private void VoiceSyncDashSetting062
'@
$voiceText = [regex]::Replace($voiceText, $methodPattern, $methodReplacement, 1)

# Batida: sensibilidade maior, cancela fala de limite em andamento e protege a frase inteira.
$crashOld = @'
            if (_voiceLastDamage062 >= 0 && damage - _voiceLastDamage062 >= 0.004 && (now - _voiceLastCrash062).TotalSeconds >= 7)
            {
                _voiceLastCrash062 = now;
                VoicePlayGroup062("crash");
            }
'@
$crashNew = @'
            if (_voiceLastDamage062 >= 0 && damage - _voiceLastDamage062 >= 0.0015 && (now - _voiceLastCrash062).TotalSeconds >= 4)
            {
                _voiceLastCrash062 = now;
                _voiceCrashProtectUntil063 = now.AddMilliseconds(3900);
                _voiceDynamicProtectUntil063 = DateTime.MinValue;
                _voicePendingDynamic063 = string.Empty;
                Interlocked.Increment(ref _voiceDynamicSerial063);
                try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
                VoicePlayGroup062("crash");
            }
'@
if ($voiceText.Contains($crashOld)) { $voiceText = $voiceText.Replace($crashOld, $crashNew) }
else { throw 'Detector de batida 1.0.62 nao encontrado.' }

# Dano alto nao pode substituir/cortar uma fala de batida que acabou de iniciar.
$damageCallOld = @'
                _voiceLastCrash062 = now;
                VoicePlayGroup062("damage");
'@
$damageCallNew = @'
                _voiceLastCrash062 = now;
                if (now >= _voiceCrashProtectUntil063) VoicePlayGroup062("damage");
'@
$voiceText = $voiceText.Replace($damageCallOld, $damageCallNew)

# UI/status deixa claro que o limite numerico voltou ao comportamento antigo.
$voiceText = $voiceText.Replace('Pacote de voz pronto • 122 falas • piadas e reações automáticas.', 'Pacote de voz pronto • 122 falas • limite dinâmico sincronizado • reações automáticas.')

foreach ($m in @('CurrentVersion = "1.0.63.0"','ObserveVoiceTelemetry062(jObject);')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.63 sem $m" }
}
foreach ($m in @('VoiceHandleDashSpeak062(t);','Cliente 1.0.63 TESTE')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.63 sem $m" }
}
foreach ($m in @('GAT_VOICE_PRIORITY_063','VoiceSpeakDynamic063','VoiceQueueDynamic063','_voiceCrashProtectUntil063','damage - _voiceLastDamage062 >= 0.0015')) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.63 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.63 TESTE: limite dinamico restaurado, batida imediata e prioridade de fala corrigida.'
