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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.64 incompleto para aplicar 1.0.65.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# ---------------------------------------------------------------------------
# 1.0.65 TESTE
# - Limites 20..130 passam a usar as 12 frases Ayres gravadas pelo usuario.
# - Mantem exatamente o disparo/tolerancia que vem do GAT DASH.
# - Nao usa TTS antigo para limite quando o pacote numerico esta disponivel.
# - Detector de batida pelo painel de danos fica um pouco mais sensivel.
# ---------------------------------------------------------------------------
$mainText = $mainText.Replace('CurrentVersion = "1.0.64.0"', 'CurrentVersion = "1.0.65.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.64"', 'Text = "Cliente 1.0.65"')
$mainText = $mainText.Replace('HUB 1.0.64:', 'HUB 1.0.65:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.64"', 'Text = "GAT Telemetria BETA 1.0.65"', 1)
$hubText = $hubText.Replace('Cliente 1.0.64 TESTE', 'Cliente 1.0.65 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.64', 'GAT Telemetria BETA 1.0.65')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.64 TESTE', 'Cliente: 1.0.65 TESTE')
}

# Inicializacao: tenta localizar automaticamente o arquivo curto de limites.
$initNeedle = '        VoiceTryAutoImport062();'
if ($voiceText.Contains($initNeedle) -and $voiceText -notlike '*VoiceTryAutoImportSpeed065();*') {
    $voiceText = $voiceText.Replace($initNeedle, $initNeedle + "`r`n        VoiceTryAutoImportSpeed065();")
}

# Troca o aviso generico de velocidade da 1.0.64 pela frase Ayres numerica correta.
$oldDynamic = @'
        if (dynamicSpeed063)
        {
            try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(3200);
            if (!VoicePlayGroup062("speed")) VoiceSpeakDynamic063(t);
            ClientStore.Log("voz limite Ayres 1.0.64: " + t);
            return true;
        }
'@
$newDynamic = @'
        if (dynamicSpeed063)
        {
            try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
            int limit065 = VoiceExtractSpeedLimit065(t);
            if (limit065 > 0 && VoicePlaySpeedLimit065(limit065))
            {
                ClientStore.Log("voz limite Ayres 1.0.65: " + limit065 + " km/h");
                return true;
            }

            // Sem o pacote numerico, ainda usa a Ayres generica. Nao volta para a voz antiga.
            if (VoicePlayGroup062("speed"))
                ClientStore.Log("voz limite Ayres generica 1.0.65: " + t);
            return true;
        }
'@
if ($voiceText.Contains($oldDynamic)) { $voiceText = $voiceText.Replace($oldDynamic, $newDynamic) }
elseif ($voiceText -notlike '*voz limite Ayres 1.0.65*') { throw 'Bloco de velocidade 1.0.64 nao encontrado.' }

# Insere o segundo pacote: 12 frases curtas, uma para cada limite 20..130.
$insertBefore = '    private void VoiceSyncDashSetting062(JObject message)'
if ($voiceText.Contains($insertBefore) -and $voiceText -notlike '*SpeedLimitPackName065*') {
$methods = @'
    private const string SpeedLimitPackName065 = "gat-speed-limits-1.0.65.mp3";
    private static readonly int[] SpeedLimitValues065 = new int[] { 20,30,40,50,60,70,80,90,100,110,120,130 };
    // Cortes medidos no audio ElevenLabs de 47.49 s enviado pelo usuario.
    private static readonly int[] SpeedLimitCue065 = new int[] { 0,3920,7840,12520,16480,20340,24180,28020,31900,35620,39720,43980,47490 };

    private static string SpeedLimitPackFile065()
    {
        return Path.Combine(VoiceDataDir062(), SpeedLimitPackName065);
    }

    private static int VoiceExtractSpeedLimit065(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        try
        {
            string digits = new string(text.Where(char.IsDigit).ToArray());
            int n;
            if (int.TryParse(digits, out n) && SpeedLimitValues065.Contains(n)) return n;
        }
        catch { }

        string s = (text ?? "").ToLowerInvariant();
        if (s.Contains("cento e trinta")) return 130;
        if (s.Contains("cento e vinte")) return 120;
        if (s.Contains("cento e dez")) return 110;
        if (s.Contains("cem")) return 100;
        if (s.Contains("noventa")) return 90;
        if (s.Contains("oitenta")) return 80;
        if (s.Contains("setenta")) return 70;
        if (s.Contains("sessenta")) return 60;
        if (s.Contains("cinquenta")) return 50;
        if (s.Contains("quarenta")) return 40;
        if (s.Contains("trinta")) return 30;
        if (s.Contains("vinte")) return 20;
        return 0;
    }

    private bool VoicePlaySpeedLimit065(int limit)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
        if (DateTime.UtcNow < _voiceCrashProtectUntil063) return false;

        int idx = Array.IndexOf(SpeedLimitValues065, limit);
        string file = SpeedLimitPackFile065();
        if (idx < 0 || idx + 1 >= SpeedLimitCue065.Length || !File.Exists(file)) return false;

        try { VoiceStop062(); } catch { }
        int serial = Interlocked.Increment(ref _voiceSerial062);
        string alias = "gatspeed065_" + serial.ToString(CultureInfo.InvariantCulture);
        string safe = file.Replace("\"", "");
        int start = SpeedLimitCue065[idx];
        int end = SpeedLimitCue065[idx + 1];
        try
        {
            if (mciSendString("open \"" + safe + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero) != 0) return false;
            mciSendString("set " + alias + " time format milliseconds", null, 0, IntPtr.Zero);
            mciSendString("setaudio " + alias + " volume to " + (_voiceVolume062 * 10), null, 0, IntPtr.Zero);
            _voiceActiveAlias062 = alias;
            if (mciSendString("play " + alias + " from " + start + " to " + end, null, 0, IntPtr.Zero) != 0)
            {
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
                if (_voiceActiveAlias062 == alias) _voiceActiveAlias062 = string.Empty;
                return false;
            }

            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(Math.Max(2600, end - start + 250));
            Task.Run(async delegate
            {
                await Task.Delay(Math.Max(500, end - start + 350));
                try { mciSendString("close " + alias, null, 0, IntPtr.Zero); } catch { }
                if (_voiceActiveAlias062 == alias) _voiceActiveAlias062 = string.Empty;
            });
            return true;
        }
        catch
        {
            try { mciSendString("close " + alias, null, 0, IntPtr.Zero); } catch { }
            return false;
        }
    }

    private void VoiceTryAutoImportSpeed065()
    {
        try
        {
            if (File.Exists(SpeedLimitPackFile065())) return;
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] dirs = new[] { Path.Combine(user, "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            var candidates = new List<FileInfo>();
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                try { candidates.AddRange(new DirectoryInfo(d).GetFiles("ElevenLabs_*Ayres*.mp3")); } catch { }
            }
            foreach (FileInfo f in candidates.OrderByDescending(x => x.LastWriteTimeUtc))
            {
                if (!VoiceValidateSpeedPack065(f.FullName)) continue;
                File.Copy(f.FullName, SpeedLimitPackFile065(), true);
                ClientStore.Log("pacote Ayres de limites 1.0.65 importado automaticamente");
                break;
            }
        }
        catch { }
    }

    private bool VoiceValidateSpeedPack065(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        string alias = "gatspeedcheck065" + Environment.TickCount.ToString("x");
        try
        {
            string safe = path.Replace("\"", "");
            if (mciSendString("open \"" + safe + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero) != 0) return false;
            mciSendString("set " + alias + " time format milliseconds", null, 0, IntPtr.Zero);
            var sb = new StringBuilder(64);
            if (mciSendString("status " + alias + " length", sb, sb.Capacity, IntPtr.Zero) != 0) return false;
            int ms;
            if (!int.TryParse(sb.ToString().Trim(), out ms)) return false;
            return ms >= 45000 && ms <= 50000;
        }
        catch { return false; }
        finally { try { mciSendString("close " + alias, null, 0, IntPtr.Zero); } catch { } }
    }

'@
    $voiceText = $voiceText.Replace($insertBefore, $methods + $insertBefore)
}

# Mais sensivel para batidas pequenas, mantendo cooldown para evitar repeticao.
$voiceText = $voiceText.Replace('if (maxRise >= 0.05 && moving && (now - _voiceLastCrash062).TotalSeconds >= 3.0)', 'if (maxRise >= 0.01 && moving && (now - _voiceLastCrash062).TotalSeconds >= 3.5)')

# Status visual.
$voiceText = $voiceText.Replace('Pacote de voz pronto • Ayres imediata no limite • batida por dano real • reações automáticas.', 'Pacote Ayres pronto • limites 20–130 km/h • batida por dano real • volume independente.')

foreach ($m in @('CurrentVersion = "1.0.65.0"','ObserveDamageDisplay064(d.CargoDamage')) { if ($mainText -notlike "*$m*") { throw "MainForm 1.0.65 sem $m" } }
foreach ($m in @('Cliente 1.0.65 TESTE','VoiceHandleDashSpeak062(t);')) { if ($hubText -notlike "*$m*") { throw "Hub 1.0.65 sem $m" } }
foreach ($m in @('SpeedLimitPackName065','SpeedLimitCue065','VoicePlaySpeedLimit065','VoiceExtractSpeedLimit065','VoiceTryAutoImportSpeed065','voz limite Ayres 1.0.65','maxRise >= 0.01')) { if ($voiceText -notlike "*$m*") { throw "Voice 1.0.65 sem $m" } }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.65 TESTE: 12 limites Ayres curtos integrados, mesmo timing do DASH e batida mais sensivel.'
