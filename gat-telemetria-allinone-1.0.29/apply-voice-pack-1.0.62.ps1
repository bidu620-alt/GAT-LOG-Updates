param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $project) { throw 'Fonte 1.0.61 incompleto para aplicar 1.0.62.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$projectText = Get-Content $project.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

# Versao 1.0.62 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.61.0"', 'CurrentVersion = "1.0.62.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.61"', 'Text = "Cliente 1.0.62"')
$mainText = $mainText.Replace('HUB 1.0.61:', 'HUB 1.0.62:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+"', 'Text = "GAT Telemetria BETA 1.0.62"', 1)
$hubText = [regex]::Replace($hubText, 'Cliente 1\.0\.\d+(?: TESTE)?', 'Cliente 1.0.62 TESTE')
if ($hub45Text) {
    $hub45Text = [regex]::Replace($hub45Text, 'GAT Telemetria BETA 1\.0\.\d+', 'GAT Telemetria BETA 1.0.62')
    $hub45Text = [regex]::Replace($hub45Text, 'Cliente:\s*1\.0\.\d+(?: TESTE)?', 'Cliente: 1.0.62 TESTE')
}

# Inicializa o novo motor de voz junto com o Hub.
if ($hubText -notlike '*VoiceInitialize062();*') {
    $needle = '        ResumeLayout(true);'
    if (-not $hubText.Contains($needle)) { throw 'ResumeLayout do Hub nao encontrado.' }
    $hubText = $hubText.Replace($needle, "        VoiceInitialize062();`r`n" + $needle)
}

# Substitui o aviso antigo de "em breve" pelo painel real de voz/volume.
$oldVoiceLabel = 'p.Controls.Add(new Label { Text = "VOZ E ALERTAS • em breve\r\nVamos adicionar escolha de voz, velocidade e volume numa próxima etapa.", Left = 0, Top = 260, Width = 520, Height = 55, ForeColor = Color.FromArgb(135, 166, 202) });'
if ($hubText.Contains($oldVoiceLabel)) {
    $hubText = $hubText.Replace($oldVoiceLabel, 'BuildVoiceSettings062(p);')
} elseif ($hubText -notlike '*BuildVoiceSettings062(p);*') {
    throw 'Bloco de configuracao de voz antigo nao encontrado.'
}

# Voz solicitada pelo GAT DASH: tenta o pacote novo e cai para TTS antigo se o pacote nao estiver importado.
$oldSpeak = 'else if (type == "speak") { string t = (Convert.ToString(m["text"]) ?? "").Trim(); if (t.Length > 0 && t.Length < 240) { try { if (_voice == null) _voice = new System.Speech.Synthesis.SpeechSynthesizer(); _voice.SpeakAsyncCancelAll(); _voice.SpeakAsync(t); } catch { } } }'
$newSpeak = 'else if (type == "speak") { string t = (Convert.ToString(m["text"]) ?? "").Trim(); if (t.Length > 0 && t.Length < 240 && !VoiceHandleDashSpeak062(t)) { try { if (_voice == null) _voice = new System.Speech.Synthesis.SpeechSynthesizer(); _voice.Volume = VoiceVolume062Value(); _voice.SpeakAsyncCancelAll(); _voice.SpeakAsync(t); } catch { } } }'
if ($hubText.Contains($oldSpeak)) { $hubText = $hubText.Replace($oldSpeak, $newSpeak) }
elseif ($hubText -notlike '*VoiceHandleDashSpeak062(t)*') { throw 'Handler speak do DASH nao encontrado.' }

# Mute do DASH para imediatamente MP3 + TTS e continua persistindo a preferencia.
$oldStop = 'else if (type == "stopSpeech") { try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { } }'
$newStop = 'else if (type == "stopSpeech") { VoiceStop062(); try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { } }'
if ($hubText.Contains($oldStop)) { $hubText = $hubText.Replace($oldStop, $newStop) }
elseif ($hubText -notlike '*VoiceStop062();*') { throw 'Handler stopSpeech nao encontrado.' }

$oldSettings = 'else if (type == "setSettings") { SaveDashSettings045(m); await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")"); }'
$newSettings = 'else if (type == "setSettings") { VoiceSyncDashSetting062(m); SaveDashSettings045(m); await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")"); }'
if ($hubText.Contains($oldSettings)) { $hubText = $hubText.Replace($oldSettings, $newSettings) }
elseif ($hubText -notlike '*VoiceSyncDashSetting062(m)*') { throw 'Handler setSettings nao encontrado.' }

# O TTS antigo respeita a mesma barra de volume. Se houver pacote importado, os anuncios antigos
# Trabalho iniciado/concluido sao convertidos automaticamente para a nova voz.
$mainText = $mainText.Replace('_voice.Volume = 100;', '_voice.Volume = VoiceVolume062Value();')
if ($mainText -notlike '*VoiceHandleLegacySpeak062(text, logLabel)*') {
    $needle = 'EnsureVoice().SpeakAsync(text);'
    if (-not $mainText.Contains($needle)) { throw 'SpeakGat nao encontrado.' }
    $mainText = $mainText.Replace($needle, 'if (VoiceHandleLegacySpeak062(text, logLabel)) return true;' + "`r`n`t`t`t" + 'EnsureVoice().Volume = VoiceVolume062Value();' + "`r`n`t`t`t" + $needle)
}

# Observa a telemetria mesmo com o DASH fechado: batida, dano, combustivel, abastecimento,
# chuva e chegada ao destino passam a poder disparar falas do pacote.
if ($mainText -notlike '*ObserveVoiceTelemetry062(tele);*') {
    $needle = 'tele = StabilizeJobTelemetry(tele);'
    $idx = $mainText.IndexOf($needle)
    if ($idx -lt 0) { throw 'StabilizeJobTelemetry(tele) nao encontrado.' }
    $mainText = $mainText.Insert($idx + $needle.Length, "`r`n`t`tObserveVoiceTelemetry062(tele);")
}

$srcDir = Split-Path $main.FullName -Parent
$voicePath = Join-Path $srcDir 'Voice062.cs'
$voiceSource = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private const string VoicePackName062 = "gat-voice-pack-1.0.62.mp3";
    private static readonly int[] VoiceCue062 = new int[] { 0,3360,5780,8785,11355,14173,16496,19100,22155,24236,26681,28188,31337,33071,34336,37537,39876,41235,42349,44098,46554,47887,49954,50977,52439,56523,58263,60575,63640,65936,67635,70253,72531,74450,75710,77598,79549,81796,85104,87169,88979,91179,92825,95176,97225,100022,101971,104363,107099,109341,111284,114008,115227,119230,121588,124264,126034,129293,131992,133519,136517,137790,139131,142754,144122,145725,147958,148969,152407,154915,156438,158309,160652,163117,165780,167597,169681,172437,174308,176745,180313,182471,184139,186502,188306,191165,193958,195787,197867,201100,204306,205689,207563,209798,212307,213455,214571,217690,219684,221012,222632,224319,226829,231301,235968,240172,243436,246067,249515,252504,256640,259045,261531,264415,266025,269569,270582,273564,275294,278253,283691,286280,289306 };

    private readonly Random _voiceRandom062 = new Random();
    private System.Windows.Forms.Timer _voiceRandomTimer062;
    private TrackBar _voiceVolumeBar062;
    private Label _voiceVolumeLabel062;
    private Label _voiceStatusLabel062;
    private bool _voiceInit062;
    private bool _voiceMuted062;
    private bool _voiceSeenTelemetry062;
    private int _voiceVolume062 = 80;
    private int _voiceSerial062;
    private string _voiceActiveAlias062 = string.Empty;
    private DateTime _voiceNextRandom062 = DateTime.UtcNow.AddMinutes(20);
    private DateTime _voiceLastObserve062 = DateTime.MinValue;
    private DateTime _voiceLastCrash062 = DateTime.MinValue;
    private DateTime _voiceLastFuelAlert062 = DateTime.MinValue;
    private DateTime _voiceLastRain062 = DateTime.MinValue;
    private double _voiceLastDamage062 = -1;
    private double _voiceLastFuel062 = -1;
    private double _voiceLastRainValue062 = -1;
    private bool _voiceFuelWarned062;
    private bool _voiceArriving062;

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

    private static string VoiceDataDir062()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoicePackFile062() { return Path.Combine(VoiceDataDir062(), VoicePackName062); }
    private static string VoiceVolumeFile062() { return Path.Combine(VoiceDataDir062(), "voice-volume-1.0.62.txt"); }

    private void VoiceInitialize062()
    {
        if (_voiceInit062) return;
        _voiceInit062 = true;
        VoiceLoadSettings062();
        VoiceTryAutoImport062();
        _voiceRandomTimer062 = new System.Windows.Forms.Timer { Interval = 60000 };
        _voiceRandomTimer062.Tick += delegate
        {
            if (!_voiceSeenTelemetry062 || DateTime.UtcNow < _voiceNextRandom062) return;
            VoicePlayGroup062("random", false);
            VoiceScheduleRandom062();
        };
        _voiceRandomTimer062.Start();
        FormClosed += delegate
        {
            try { _voiceRandomTimer062.Stop(); _voiceRandomTimer062.Dispose(); } catch { }
            VoiceStop062();
        };
        VoiceRefreshUi062();
    }

    private void VoiceLoadSettings062()
    {
        try
        {
            string f = VoiceVolumeFile062();
            int v;
            if (File.Exists(f) && int.TryParse(File.ReadAllText(f).Trim(), out v)) _voiceVolume062 = Math.Max(0, Math.Min(100, v));
        }
        catch { }
        try
        {
            JObject o = JObject.Parse(DashSettingsJson045());
            if (o["voice"] != null) _voiceMuted062 = !Convert.ToBoolean(o["voice"]);
        }
        catch { }
    }

    private void VoiceScheduleRandom062()
    {
        _voiceNextRandom062 = DateTime.UtcNow.AddMinutes(_voiceRandom062.Next(15, 31));
    }

    private void BuildVoiceSettings062(Panel p)
    {
        VoiceLoadSettings062();
        var box = new Panel
        {
            Left = 0, Top = 252, Width = Math.Max(520, p.Width), Height = 125,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };
        box.Controls.Add(new Label
        {
            Text = "VOZ E ALERTAS • AYRES",
            Left = 14, Top = 10, Width = 220, Height = 23,
            ForeColor = Color.FromArgb(100, 180, 255),
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });
        _voiceVolumeLabel062 = new Label
        {
            Text = "Volume da voz: " + _voiceVolume062 + "%",
            Left = 14, Top = 39, Width = 150, Height = 24,
            ForeColor = Color.Gainsboro
        };
        box.Controls.Add(_voiceVolumeLabel062);
        _voiceVolumeBar062 = new TrackBar
        {
            Left = 160, Top = 30, Width = 250, Minimum = 0, Maximum = 100,
            Value = _voiceVolume062, TickFrequency = 10, SmallChange = 5, LargeChange = 10
        };
        _voiceVolumeBar062.Scroll += delegate
        {
            VoiceSetVolume062(_voiceVolumeBar062.Value);
        };
        box.Controls.Add(_voiceVolumeBar062);

        var test = HubButton041("TESTAR VOZ", 120);
        test.Left = 425; test.Top = 31;
        test.Click += delegate { if (!VoicePlayGroup062("startup")) MessageBox.Show("Importe primeiro o áudio da voz Ayres.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        box.Controls.Add(test);

        var import = HubButton041("IMPORTAR ÁUDIO", 135);
        import.Left = 555; import.Top = 31;
        import.Click += delegate { VoiceImport062(); };
        box.Controls.Add(import);

        _voiceStatusLabel062 = new Label
        {
            Left = 14, Top = 76, Width = Math.Max(480, box.Width - 28), Height = 38,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(145, 175, 205)
        };
        box.Controls.Add(_voiceStatusLabel062);
        box.Resize += delegate
        {
            int w = box.ClientSize.Width;
            _voiceVolumeBar062.Width = Math.Max(130, Math.Min(320, w - 500));
            test.Left = Math.Max(300, w - 285);
            import.Left = Math.Max(430, w - 150);
        };
        p.Controls.Add(box);
        VoiceRefreshUi062();
    }

    private void VoiceRefreshUi062()
    {
        try
        {
            if (_voiceVolumeLabel062 != null) _voiceVolumeLabel062.Text = "Volume da voz: " + _voiceVolume062 + "%";
            if (_voiceVolumeBar062 != null && _voiceVolumeBar062.Value != _voiceVolume062) _voiceVolumeBar062.Value = _voiceVolume062;
            if (_voiceStatusLabel062 != null)
                _voiceStatusLabel062.Text = File.Exists(VoicePackFile062())
                    ? "Pacote de voz pronto • 122 falas • piadas e reações automáticas."
                    : "Áudio ainda não importado. Clique em IMPORTAR ÁUDIO e selecione o MP3 Ayres de 4min49s.";
        }
        catch { }
    }

    private void VoiceSetVolume062(int value)
    {
        _voiceVolume062 = Math.Max(0, Math.Min(100, value));
        try { File.WriteAllText(VoiceVolumeFile062(), _voiceVolume062.ToString(CultureInfo.InvariantCulture)); } catch { }
        try
        {
            if (!string.IsNullOrWhiteSpace(_voiceActiveAlias062))
                mciSendString("setaudio " + _voiceActiveAlias062 + " volume to " + (_voiceVolume062 * 10), null, 0, IntPtr.Zero);
        }
        catch { }
        try { if (_voice != null) _voice.Volume = _voiceVolume062; } catch { }
        VoiceRefreshUi062();
    }

    private int VoiceVolume062Value() { return Math.Max(0, Math.Min(100, _voiceVolume062)); }

    private void VoiceImport062()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Selecione o áudio Ayres com as 122 falas";
            dlg.Filter = "Áudio MP3 (*.mp3)|*.mp3|Todos os arquivos (*.*)|*.*";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!VoiceValidateFile062(dlg.FileName))
            {
                MessageBox.Show("Esse arquivo não parece ser o áudio completo de 4min49s usado no pacote 1.0.62.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                File.Copy(dlg.FileName, VoicePackFile062(), true);
                VoiceRefreshUi062();
                MessageBox.Show("Voz importada. As 122 falas já estão disponíveis no GAT Telemetria.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                VoicePlayGroup062("startup");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível importar a voz: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void VoiceTryAutoImport062()
    {
        try
        {
            if (File.Exists(VoicePackFile062())) return;
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dirs = new[] { Path.Combine(user, "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            var candidates = new List<FileInfo>();
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                try { candidates.AddRange(new DirectoryInfo(d).GetFiles("ElevenLabs_*Ayres*.mp3")); } catch { }
            }
            foreach (FileInfo f in candidates.OrderByDescending(x => x.LastWriteTimeUtc))
            {
                if (!VoiceValidateFile062(f.FullName)) continue;
                File.Copy(f.FullName, VoicePackFile062(), true);
                break;
            }
        }
        catch { }
    }

    private bool VoiceValidateFile062(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        string alias = "gatcheck062" + Environment.TickCount.ToString("x");
        try
        {
            string safe = path.Replace("\"", "");
            if (mciSendString("open \"" + safe + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero) != 0) return false;
            mciSendString("set " + alias + " time format milliseconds", null, 0, IntPtr.Zero);
            var sb = new StringBuilder(64);
            if (mciSendString("status " + alias + " length", sb, sb.Capacity, IntPtr.Zero) != 0) return false;
            int ms;
            if (!int.TryParse(sb.ToString().Trim(), out ms)) return false;
            return ms >= 285000 && ms <= 294000;
        }
        catch { return false; }
        finally { try { mciSendString("close " + alias, null, 0, IntPtr.Zero); } catch { } }
    }

    private bool VoicePlayGroup062(string group, bool interrupt = true)
    {
        int first = 0, last = 0;
        switch ((group ?? "").ToLowerInvariant())
        {
            case "startup": first = 1; last = 5; break;
            case "cargo_start": first = 6; last = 10; break;
            case "trip": first = 11; last = 15; break;
            case "speed": first = 16; last = 22; break;
            case "radar": first = 23; last = 27; break;
            case "fine": first = 28; last = 33; break;
            case "crash": first = 34; last = 43; break;
            case "damage": first = 44; last = 48; break;
            case "fuel_low": first = 49; last = 54; break;
            case "refuel": first = 55; last = 59; break;
            case "fatigue": first = 60; last = 64; break;
            case "arriving": first = 65; last = 69; break;
            case "delivery": first = 70; last = 77; break;
            case "perfect": first = 78; last = 82; break;
            case "rain": first = 83; last = 86; break;
            case "night": first = 87; last = 90; break;
            case "lights": first = 91; last = 94; break;
            case "toll": first = 95; last = 98; break;
            case "service": first = 99; last = 102; break;
            case "random": first = 103; last = 122; break;
            default: return false;
        }
        return VoicePlayId062(_voiceRandom062.Next(first, last + 1), interrupt);
    }

    private bool VoicePlayId062(int id, bool interrupt = true)
    {
        if (_voiceMuted062 || _voiceVolume062 <= 0) return true;
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

    private void VoiceStop062()
    {
        string alias = _voiceActiveAlias062;
        _voiceActiveAlias062 = string.Empty;
        if (string.IsNullOrWhiteSpace(alias)) return;
        try { mciSendString("stop " + alias, null, 0, IntPtr.Zero); } catch { }
        try { mciSendString("close " + alias, null, 0, IntPtr.Zero); } catch { }
    }

    private bool VoiceHandleLegacySpeak062(string text, string logLabel)
    {
        if (_voiceMuted062) return true;
        string s = (text ?? "").ToLowerInvariant();
        if (s.Contains("trabalho iniciado")) return VoicePlayGroup062("cargo_start");
        if (s.Contains("trabalho conclu")) return VoicePlayGroup062("delivery");
        return false;
    }

    private bool VoiceHandleDashSpeak062(string text)
    {
        if (_voiceMuted062) return true;
        string s = (text ?? "").ToLowerInvariant();
        if (s.Contains("radar") || s.Contains("fiscaliza")) return VoicePlayGroup062("radar");
        if (s.Contains("veloc") || s.Contains("limite") || s.Contains("reduza") || s.Contains("reduzir")) return VoicePlayGroup062("speed");
        return false;
    }

    private void VoiceSyncDashSetting062(JObject message)
    {
        try
        {
            if (message != null && message["voice"] != null)
            {
                _voiceMuted062 = !Convert.ToBoolean(message["voice"]);
                if (_voiceMuted062) VoiceStop062();
            }
        }
        catch { }
    }

    private static double VoiceMetric062(JObject root, params string[] names)
    {
        if (root == null) return double.NaN;
        var wanted = new HashSet<string>((names ?? new string[0]).Select(VoiceNorm062), StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (JProperty p in root.Descendants().OfType<JProperty>())
            {
                if (!wanted.Contains(VoiceNorm062(p.Name))) continue;
                double value;
                if (double.TryParse(Convert.ToString(p.Value), NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
            }
        }
        catch { }
        return double.NaN;
    }

    private static string VoiceNorm062(string s)
    {
        return new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static double VoiceDamage062(JObject root)
    {
        if (root == null) return double.NaN;
        double max = -1;
        try
        {
            foreach (JProperty p in root.Descendants().OfType<JProperty>())
            {
                string n = VoiceNorm062(p.Name);
                if (!n.Contains("damage") && !n.Contains("wear")) continue;
                double v;
                if (!double.TryParse(Convert.ToString(p.Value), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) continue;
                if (v > 1.0 && v <= 100.0) v /= 100.0;
                if (v >= 0 && v <= 1.5) max = Math.Max(max, v);
            }
        }
        catch { }
        return max;
    }

    private void ObserveVoiceTelemetry062(JObject tele)
    {
        if (tele == null) return;
        DateTime now = DateTime.UtcNow;
        if ((now - _voiceLastObserve062).TotalMilliseconds < 650) return;
        _voiceLastObserve062 = now;
        _voiceSeenTelemetry062 = true;

        double damage = VoiceDamage062(tele);
        if (!double.IsNaN(damage) && damage >= 0)
        {
            if (_voiceLastDamage062 >= 0 && damage - _voiceLastDamage062 >= 0.004 && (now - _voiceLastCrash062).TotalSeconds >= 7)
            {
                _voiceLastCrash062 = now;
                VoicePlayGroup062("crash");
            }
            else if (_voiceLastDamage062 >= 0 && _voiceLastDamage062 < 0.25 && damage >= 0.25 && (now - _voiceLastCrash062).TotalSeconds >= 5)
            {
                _voiceLastCrash062 = now;
                VoicePlayGroup062("damage");
            }
            _voiceLastDamage062 = damage;
        }

        double fuel = VoiceMetric062(tele, "fuel", "fuelAmount", "fuelLiters", "fuelLevel");
        double capacity = VoiceMetric062(tele, "fuelCapacity", "fuelTankCapacity", "tankCapacity");
        double speed = VoiceMetric062(tele, "speedKph", "speedKmh", "speed");
        if (!double.IsNaN(fuel) && fuel >= 0)
        {
            if (_voiceLastFuel062 >= 0 && fuel - _voiceLastFuel062 >= Math.Max(2.0, (!double.IsNaN(capacity) && capacity > 0 ? capacity * 0.015 : 3.0)) && (double.IsNaN(speed) || Math.Abs(speed) < 6))
                VoicePlayGroup062("refuel");
            if (!double.IsNaN(capacity) && capacity > 0)
            {
                double ratio = fuel / capacity;
                if (ratio <= 0.15 && !_voiceFuelWarned062 && (now - _voiceLastFuelAlert062).TotalMinutes >= 2)
                {
                    _voiceFuelWarned062 = true;
                    _voiceLastFuelAlert062 = now;
                    VoicePlayGroup062("fuel_low");
                }
                else if (ratio >= 0.25) _voiceFuelWarned062 = false;
            }
            _voiceLastFuel062 = fuel;
        }

        double rain = VoiceMetric062(tele, "rainIntensity", "rainDensity", "rain");
        if (!double.IsNaN(rain))
        {
            if (_voiceLastRainValue062 >= 0 && _voiceLastRainValue062 < 0.15 && rain >= 0.15 && (now - _voiceLastRain062).TotalMinutes >= 5)
            {
                _voiceLastRain062 = now;
                VoicePlayGroup062("rain");
            }
            _voiceLastRainValue062 = rain;
        }

        bool onJob = BoolAny(tele, "gameplay.onJob", "onJob", "job.onJob", "job.active", "on_job");
        double remaining = NumberAny(tele, "remaining_km", "job.remainingDistanceKm", "navigation.remainingKm");
        if (onJob && remaining > 0 && remaining <= 10 && !_voiceArriving062)
        {
            _voiceArriving062 = true;
            VoicePlayGroup062("arriving");
        }
        if (!onJob || remaining > 15) _voiceArriving062 = false;
    }
}
'@
Set-Content $voicePath $voiceSource -Encoding UTF8

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'Voice062\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="Voice062.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + "`r`n</Project>")
}

if ($mainText -notlike '*CurrentVersion = "1.0.62.0"*') { throw 'Versao 1.0.62 nao aplicada.' }
foreach ($m in @('VoiceInitialize062();','BuildVoiceSettings062(p);','VoiceHandleDashSpeak062(t)','VoiceSyncDashSetting062(m)')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.62 sem $m" }
}
foreach ($m in @('VoiceHandleLegacySpeak062(text, logLabel)','ObserveVoiceTelemetry062(tele)','VoiceVolume062Value()')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.62 sem $m" }
}
foreach ($m in @('VoiceCue062','IMPORTAR ÁUDIO','TESTAR VOZ','Volume da voz','VoicePlayGroup062','ObserveVoiceTelemetry062','mciSendString')) {
    if ($voiceSource -notlike "*$m*") { throw "Voice062 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Set-Content $project.FullName $projectText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.62 TESTE: pacote Ayres com 122 falas, reacoes e controle de volume aplicado.'
