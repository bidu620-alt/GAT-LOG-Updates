param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$voice = Get-ChildItem $rootPath -Filter 'Voice062.cs' -Recurse | Select-Object -First 1
if (-not $voice) { throw 'Voice062.cs nao encontrado para hotfix 1.0.67.' }

$voiceText = Get-Content $voice.FullName -Raw
$playIdNeedle = '    private bool VoicePlayId062(int id, bool interrupt = true)'
if (-not $voiceText.Contains($playIdNeedle)) { throw 'VoicePlayId062 nao encontrado para hotfix 1.0.67.' }

if ($voiceText -notlike '*private static string VoiceIndividualRoot166()*') {
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

foreach ($m in @('VoiceIndividualRoot166','VoiceIndividualDir166','VoiceSpeedDir166','VoiceFindIndividual166','VoiceFindSpeed166','VoicePlayFile166','VoiceExtraFiles167','VoiceReplacePackage167','VoiceAddExtras167')) {
    if ($voiceText -notlike "*$m*") { throw "Hotfix 1.0.67 sem $m" }
}

Set-Content $voice.FullName $voiceText -Encoding UTF8
Write-Host 'Hotfix 1.0.67: helpers de MP3 individual 1.0.66 restaurados sem alterar o gerenciador de voz.'
