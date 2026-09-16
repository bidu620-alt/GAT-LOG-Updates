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
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.65 incompleto para aplicar 1.0.66.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# 1.0.66 TESTE: MP3 individual por evento, limites individuais e batida mais sensivel.
$mainText = $mainText.Replace('CurrentVersion = "1.0.65.0"', 'CurrentVersion = "1.0.66.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.65"', 'Text = "Cliente 1.0.66"')
$mainText = $mainText.Replace('HUB 1.0.65:', 'HUB 1.0.66:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.65"', 'Text = "GAT Telemetria BETA 1.0.66"', 1)
$hubText = $hubText.Replace('Cliente 1.0.65 TESTE', 'Cliente 1.0.66 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.65', 'GAT Telemetria BETA 1.0.66')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.65 TESTE', 'Cliente: 1.0.66 TESTE')
}

$playIdNeedle = '    private bool VoicePlayId062(int id, bool interrupt = true)'
if ($voiceText.Contains($playIdNeedle) -and $voiceText -notlike '*VoiceIndividualDir166*') {
$helpers = @'
    private static string VoiceIndividualRoot166()
    {
        string dir = Path.Combine(VoiceDataDir062(), "voicepack166");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoiceIndividualDir166()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "general");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoiceSpeedDir166()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "limits");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoiceFindIndividual166(int id)
    {
        try
        {
            if (id < 1 || id > 122) return string.Empty;
            string dir = VoiceIndividualDir166();
            string prefix = id.ToString("D3", CultureInfo.InvariantCulture) + "_";
            return Directory.GetFiles(dir, prefix + "*.mp3").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string VoiceFindSpeed166(int limit)
    {
        try
        {
            string file = Path.Combine(VoiceSpeedDir166(), "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3");
            return File.Exists(file) ? file : string.Empty;
        }
        catch { return string.Empty; }
    }

    private bool VoicePlayFile166(string file, bool interrupt = true)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return false;
        if (!interrupt && !string.IsNullOrWhiteSpace(_voiceActiveAlias062)) return false;
        if (interrupt) VoiceStop062();

        int serial = Interlocked.Increment(ref _voiceSerial062);
        string alias = "gatvoice166_" + serial.ToString(CultureInfo.InvariantCulture);
        string safe = file.Replace("\"", "");
        try
        {
            if (mciSendString("open \"" + safe + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero) != 0) return false;
            mciSendString("set " + alias + " time format milliseconds", null, 0, IntPtr.Zero);
            var sb = new StringBuilder(64);
            int lengthMs = 3500;
            if (mciSendString("status " + alias + " length", sb, sb.Capacity, IntPtr.Zero) == 0)
            {
                int parsed;
                if (int.TryParse(sb.ToString().Trim(), out parsed) && parsed > 0) lengthMs = parsed;
            }
            mciSendString("setaudio " + alias + " volume to " + (_voiceVolume062 * 10), null, 0, IntPtr.Zero);
            _voiceActiveAlias062 = alias;
            if (mciSendString("play " + alias, null, 0, IntPtr.Zero) != 0)
            {
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
                if (_voiceActiveAlias062 == alias) _voiceActiveAlias062 = string.Empty;
                return false;
            }
            Task.Run(async delegate
            {
                await Task.Delay(Math.Max(800, lengthMs + 400));
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

'@
    $voiceText = $voiceText.Replace($playIdNeedle, $helpers + $playIdNeedle)
}

$patternPlayId = '(?s)    private bool VoicePlayId062\(int id, bool interrupt = true\)\s*\{.*?\r?\n    \}\r?\n\r?\n    private void VoiceStop062\('
$newPlayId = @'
    private bool VoicePlayId062(int id, bool interrupt = true)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
        string individual166 = VoiceFindIndividual166(id);
        if (!string.IsNullOrWhiteSpace(individual166))
        {
            bool ok166 = VoicePlayFile166(individual166, interrupt);
            if (ok166) ClientStore.Log("voz individual 1.0.66: " + Path.GetFileName(individual166));
            return ok166;
        }

        string file = VoicePackFile062();
        if (!File.Exists(file) || id < 1 || id >= VoiceCue062.Length) return false;
        if (!interrupt && !string.IsNullOrWhiteSpace(_voiceActiveAlias062)) return false;
        if (interrupt) VoiceStop062();
        int serial = Interlocked.Increment(ref _voiceSerial062);
        string alias = "gatvoice062_" + serial.ToString(CultureInfo.InvariantCulture);
        string safe = file.Replace("\"", "");
        int start = VoiceCue062[id - 1];
        int end = VoiceCue062[id];
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
            Task.Run(async delegate
            {
                await Task.Delay(Math.Max(400, end - start + 300));
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

    private void VoiceStop062(
'@
$replaced = [regex]::Replace($voiceText, $patternPlayId, $newPlayId, 1)
if ($replaced -eq $voiceText) { throw 'VoicePlayId062 nao encontrado para trocar por MP3 individual.' }
$voiceText = $replaced

$oldSpeedHead = @'
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
        if (DateTime.UtcNow < _voiceCrashProtectUntil063) return false;

        int idx = Array.IndexOf(SpeedLimitValues065, limit);
'@
$newSpeedHead = @'
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
        if (DateTime.UtcNow < _voiceCrashProtectUntil063) return false;

        string direct166 = VoiceFindSpeed166(limit);
        if (!string.IsNullOrWhiteSpace(direct166))
        {
            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(5200);
            bool ok166 = VoicePlayFile166(direct166, true);
            if (ok166) ClientStore.Log("voz limite individual 1.0.66: " + limit + " km/h");
            return ok166;
        }

        int idx = Array.IndexOf(SpeedLimitValues065, limit);
'@
if ($voiceText.Contains($oldSpeedHead)) { $voiceText = $voiceText.Replace($oldSpeedHead, $newSpeedHead) }
elseif ($voiceText -notlike '*voz limite individual 1.0.66*') { throw 'Cabecalho VoicePlaySpeedLimit065 nao encontrado.' }

$oldStatus = @'
            if (_voiceStatusLabel062 != null)
                _voiceStatusLabel062.Text = File.Exists(VoicePackFile062())
                    ? "Pacote Ayres pronto • limites 20–130 km/h • batida por dano real • volume independente."
                    : "Áudio ainda não importado. Clique em IMPORTAR ÁUDIO e selecione o MP3 Ayres de 4min49s.";
'@
$newStatus = @'
            if (_voiceStatusLabel062 != null)
            {
                int individualCount166 = 0;
                try { individualCount166 = Directory.Exists(VoiceIndividualDir166()) ? Directory.GetFiles(VoiceIndividualDir166(), "*.mp3").Length : 0; } catch { }
                _voiceStatusLabel062.Text = individualCount166 >= 100
                    ? "Voz 1.0.66 pronta • " + individualCount166 + " MP3s individuais • sem cortes por tempo."
                    : (File.Exists(VoicePackFile062())
                        ? "Pacote antigo detectado • instale o pacote 1.0.66 para usar MP3s individuais."
                        : "Voz 1.0.66 não encontrada. Reinstale usando INSTALAR_COM_VOZ_1.0.66.bat.");
            }
'@
if ($voiceText.Contains($oldStatus)) { $voiceText = $voiceText.Replace($oldStatus, $newStatus) }
else { throw 'Bloco de status da voz 1.0.65 nao encontrado.' }

$voiceText = $voiceText.Replace('if (maxRise >= 0.01 && moving && (now - _voiceLastCrash062).TotalSeconds >= 3.5)', 'if (maxRise > 0.000001 && (now - _voiceLastCrash062).TotalSeconds >= 4.0)')
$voiceText = $voiceText.Replace('damage - _voiceLastDamage062 >= 0.004 && (now - _voiceLastCrash062).TotalSeconds >= 7', 'damage - _voiceLastDamage062 > 0.00005 && (now - _voiceLastCrash062).TotalSeconds >= 4')
$voiceText = $voiceText.Replace('Pacote Ayres pronto • limites 20–130 km/h • batida por dano real • volume independente.', 'Voz Ayres 1.0.66 • MP3 individual por evento • limites individuais • volume independente.')

foreach ($m in @('CurrentVersion = "1.0.66.0"','ObserveDamageDisplay064(d.CargoDamage')) { if ($mainText -notlike "*$m*") { throw "MainForm 1.0.66 sem $m" } }
foreach ($m in @('Cliente 1.0.66 TESTE','VoiceHandleDashSpeak062(t);')) { if ($hubText -notlike "*$m*") { throw "Hub 1.0.66 sem $m" } }
foreach ($m in @('VoiceIndividualDir166','VoiceSpeedDir166','VoiceFindIndividual166','VoicePlayFile166','voz individual 1.0.66','voz limite individual 1.0.66','maxRise > 0.000001')) { if ($voiceText -notlike "*$m*") { throw "Voice 1.0.66 sem $m" } }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.66 TESTE: 122 MP3s individuais, limites individuais e batida sensivel.'