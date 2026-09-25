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
    private DateTime _voiceCrashProtectUntil063 = DateTime.MinValue;
    private DateTime _voiceDynamicProtectUntil063 = DateTime.MinValue;
    private int _voiceDynamicSerial063;
    private string _voicePendingDynamic063 = string.Empty;
    private double[] _voiceDisplayDamage064;
    private DateTime _voiceDisplayDamageSeen064 = DateTime.MinValue;

    private string _voiceJobKey168 = string.Empty;
    private bool _voiceJobActive168;
    private double _voiceJobStartOdometer168 = double.NaN;
    private double _voiceJobStartRemaining168 = double.NaN;
    private double _voiceJobPlanned168 = double.NaN;
    private double _voiceNextRandomKm168 = 500.0;
    private string _voiceLastRandomChoice168 = string.Empty;
    private double _voiceDamageBaseline168 = double.NaN;
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
        VoiceTryAutoImportSpeed065();
        _voiceRandomTimer062 = new System.Windows.Forms.Timer { Interval = 60000 };
        _voiceRandomTimer062.Tick += delegate
        {
            if (!_voiceSeenTelemetry062 || DateTime.UtcNow < _voiceNextRandom062) return;
            VoicePlayGroup062("random", false);
            VoiceScheduleRandom062();
        };
        // 1.0.68: fala aleatoria somente por distancia da carga.
        _voiceRandomTimer062.Enabled = false;
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
            Left = 0, Top = 252, Width = Math.Max(520, p.Width), Height = 205,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };
        box.Controls.Add(new Label
        {
            Text = "VOZ E ALERTAS • PACOTES DE VOZ",
            Left = 14, Top = 10, Width = 330, Height = 23,
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
            Left = 160, Top = 30, Width = 310, Minimum = 0, Maximum = 100,
            Value = _voiceVolume062, TickFrequency = 10, SmallChange = 5, LargeChange = 10
        };
        _voiceVolumeBar062.Scroll += delegate { VoiceSetVolume062(_voiceVolumeBar062.Value); };
        box.Controls.Add(_voiceVolumeBar062);

        var test = HubButton041("TESTAR VOZ", 110);
        test.Left = 14; test.Top = 72;
        test.Click += delegate { if (!VoicePlayGroup062("startup")) MessageBox.Show("Nenhuma voz pronta para teste.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        box.Controls.Add(test);

        var replace = HubButton041("SUBSTITUIR VOZ", 135);
        replace.Left = 134; replace.Top = 72;
        replace.Click += delegate { VoiceReplacePackage167(); };
        box.Controls.Add(replace);

        var add = HubButton041("ADICIONAR FALAS", 145);
        add.Left = 279; add.Top = 72;
        add.Click += delegate { VoiceAddExtras167(); };
        box.Controls.Add(add);

        var model = HubButton041("BAIXAR MODELO TXT", 145);
        model.Left = 14; model.Top = 111;
        model.Click += delegate { VoiceOpenTemplate167(); };
        box.Controls.Add(model);

        var restore = HubButton041("RESTAURAR BACKUP", 145);
        restore.Left = 169; restore.Top = 111;
        restore.Click += delegate { VoiceRestoreBackup167(); };
        box.Controls.Add(restore);

        _voiceStatusLabel062 = new Label
        {
            Left = 14, Top = 151, Width = Math.Max(480, box.Width - 28), Height = 46,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(145, 175, 205)
        };
        box.Controls.Add(_voiceStatusLabel062);
        box.Resize += delegate
        {
            int w = box.ClientSize.Width;
            _voiceVolumeBar062.Width = Math.Max(160, Math.Min(360, w - 190));
            _voiceStatusLabel062.Width = Math.Max(480, w - 28);
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
            {
                int general = 0, limits = 0, extras = 0;
                try { if (Directory.Exists(VoiceIndividualDir166())) general = Directory.GetFiles(VoiceIndividualDir166(), "*.mp3").Length; } catch { }
                try { if (Directory.Exists(VoiceSpeedDir166())) limits = Directory.GetFiles(VoiceSpeedDir166(), "*.mp3").Length; } catch { }
                try { string er = VoiceExtrasRoot167(); if (Directory.Exists(er)) extras = Directory.GetFiles(er, "*.mp3", SearchOption.AllDirectories).Length; } catch { }
                _voiceStatusLabel062.Text = "Pacote ativo • " + general + " falas gerais • " + limits + " limites • " + extras + " extras. Substitua ou acrescente vozes sem atualizar o GAT.";
            }
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
        string normalized = (group ?? "").ToLowerInvariant();
        switch (normalized)
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
        string[] extras167 = VoiceExtraFiles167(normalized);
        int baseCount = last - first + 1;
        int pick = _voiceRandom062.Next(0, Math.Max(1, baseCount + extras167.Length));
        if (pick >= baseCount && extras167.Length > 0)
        {
            string extra = extras167[pick - baseCount];
            bool ok = VoicePlayFile166(extra, interrupt);
            if (ok) ClientStore.Log("voz extra 1.0.67 [" + normalized + "]: " + Path.GetFileName(extra));
            return ok;
        }
        return VoicePlayId062(first + pick, interrupt);
    }

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
        string t = (text ?? "").Trim();
        if (t.Length == 0) return true;
        string s = t.ToLowerInvariant();

        // Velocidade/limite e texto dinamico: preserva exatamente a frase antiga do DASH,
        // inclusive o numero do limite da via e o instante/tolerancia em que ele pediu a fala.
        bool dynamicSpeed063 = s.Contains("veloc") || s.Contains("limite") || s.Contains("reduza") ||
                               s.Contains("reduzir") || s.Contains("quilômetro") || s.Contains("km/h");
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

    private static double VoiceParsePercent064(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return double.NaN;
        string s = text.Trim();
        if (s == "—" || s == "-") return double.NaN;
        var chars = s.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray();
        if (chars.Length == 0) return double.NaN;
        s = new string(chars).Replace(',', '.');
        double v;
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return double.NaN;
        return v;
    }

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

    private void VoiceTriggerCrash064(DateTime now, double rise)
    {
        _voiceLastCrash062 = now;
        _voiceCrashProtectUntil063 = now.AddMilliseconds(5200);
        _voiceDynamicProtectUntil063 = DateTime.MinValue;
        _voicePendingDynamic063 = string.Empty;
        Interlocked.Increment(ref _voiceDynamicSerial063);
        try { if (_voice != null) _voice.SpeakAsyncCancelAll(); } catch { }
        try { VoiceStop062(); } catch { }
        if (VoicePlayGroup062("crash"))
            ClientStore.Log("voz batida 1.0.64: aumento de dano " + rise.ToString("0.###", CultureInfo.InvariantCulture) + "%");
    }
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

        string direct166 = VoiceFindSpeed166(limit);
        if (!string.IsNullOrWhiteSpace(direct166))
        {
            _voiceDynamicProtectUntil063 = DateTime.UtcNow.AddMilliseconds(5200);
            bool ok166 = VoicePlayFile166(direct166, true);
            if (ok166) ClientStore.Log("voz limite individual 1.0.66: " + limit + " km/h");
            return ok166;
        }

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
    private static readonly string[] VoiceGroups167 = new string[]
    {
        "startup","cargo_start","trip","speed","radar","fine","crash","damage","fuel_low","refuel",
        "fatigue","arriving","delivery","perfect","rain","night","lights","toll","service","random"
    };

    private static string VoiceExtrasRoot167()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "extras");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoiceBackupRoot167() { return Path.Combine(VoiceDataDir062(), "voicepack167-backup-latest"); }

    private static bool VoiceGroupAllowed167(string group)
    {
        return VoiceGroups167.Any(x => string.Equals(x, group ?? "", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] VoiceExtraFiles167(string group)
    {
        try
        {
            if (!VoiceGroupAllowed167(group)) return new string[0];
            string dir = Path.Combine(VoiceExtrasRoot167(), group.ToLowerInvariant());
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir, "*.mp3").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch { return new string[0]; }
    }

    private static void VoiceCopyDir167(string source, string dest)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (string sub in Directory.GetDirectories(source)) VoiceCopyDir167(sub, Path.Combine(dest, Path.GetFileName(sub)));
    }

    private void VoiceBackupCurrent167()
    {
        string backup = VoiceBackupRoot167();
        try
        {
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            Directory.CreateDirectory(backup);
            VoiceCopyDir167(VoiceIndividualRoot166(), backup);
        }
        catch { }
    }

    private static int VoiceGeneralId167(string file)
    {
        try
        {
            string n = Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(n) || n.Length < 4 || n[3] != '_') return 0;
            int id;
            if (!int.TryParse(n.Substring(0, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return 0;
            return id >= 1 && id <= 122 ? id : 0;
        }
        catch { return 0; }
    }

    private static int VoiceLimitId167(string file)
    {
        try
        {
            string n = Path.GetFileNameWithoutExtension(file) ?? "";
            if (!n.StartsWith("limite_", StringComparison.OrdinalIgnoreCase)) return 0;
            int v;
            if (!int.TryParse(n.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return 0;
            return new int[] {20,30,40,50,60,70,80,90,100,110,120,130}.Contains(v) ? v : 0;
        }
        catch { return 0; }
    }

    private void VoiceReplacePackage167()
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Selecione a pasta do pacote de voz. Ela pode conter general e limits.";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string root = dlg.SelectedPath;
            string generalSrc = Directory.Exists(Path.Combine(root, "general")) ? Path.Combine(root, "general") : root;
            string limitsSrc = Directory.Exists(Path.Combine(root, "limits")) ? Path.Combine(root, "limits") : root;
            List<string> generalFiles = new List<string>();
            List<string> limitFiles = new List<string>();
            try { generalFiles = Directory.GetFiles(generalSrc, "*.mp3").Where(x => VoiceGeneralId167(x) > 0).ToList(); } catch { }
            try { limitFiles = Directory.GetFiles(limitsSrc, "*.mp3").Where(x => VoiceLimitId167(x) > 0).ToList(); } catch { }
            if (generalFiles.Count == 0 && limitFiles.Count == 0)
            {
                MessageBox.Show("Nenhum MP3 reconhecido. Use 001_... até 122_... e/ou limite_020.mp3 até limite_130.mp3.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                VoiceStop062();
                VoiceBackupCurrent167();
                string gd = VoiceIndividualDir166();
                string ld = VoiceSpeedDir166();
                foreach (string src in generalFiles)
                {
                    int id = VoiceGeneralId167(src);
                    string prefix = id.ToString("D3", CultureInfo.InvariantCulture) + "_";
                    foreach (string old in Directory.GetFiles(gd, prefix + "*.mp3")) try { File.Delete(old); } catch { }
                    File.Copy(src, Path.Combine(gd, Path.GetFileName(src)), true);
                }
                foreach (string src in limitFiles)
                {
                    int limit = VoiceLimitId167(src);
                    File.Copy(src, Path.Combine(ld, "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3"), true);
                }
                VoiceRefreshUi062();
                ClientStore.Log("pacote de voz 1.0.67 substituido: " + generalFiles.Count + " gerais / " + limitFiles.Count + " limites");
                MessageBox.Show("Voz atualizada: " + generalFiles.Count + " falas gerais e " + limitFiles.Count + " limites substituídos.\n\nUm backup do pacote anterior foi salvo.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                VoicePlayGroup062("startup");
            }
            catch (Exception ex) { MessageBox.Show("Não foi possível substituir a voz: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private static string VoiceUniqueExtra167(string dir, string fileName)
    {
        Directory.CreateDirectory(dir);
        string safe = Path.GetFileName(fileName);
        string dest = Path.Combine(dir, safe);
        if (!File.Exists(dest)) return dest;
        string stem = Path.GetFileNameWithoutExtension(safe);
        for (int i = 2; i < 1000; i++)
        {
            dest = Path.Combine(dir, stem + "_" + i.ToString(CultureInfo.InvariantCulture) + ".mp3");
            if (!File.Exists(dest)) return dest;
        }
        return Path.Combine(dir, stem + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".mp3");
    }

    private void VoiceAddExtras167()
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Selecione extras/<grupo>/ ou uma pasta de grupo como crash, delivery ou random.";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string root = dlg.SelectedPath;
            string extrasSource = Directory.Exists(Path.Combine(root, "extras")) ? Path.Combine(root, "extras") : root;
            int added = 0;
            try
            {
                string selectedName = (new DirectoryInfo(extrasSource)).Name.ToLowerInvariant();
                if (VoiceGroupAllowed167(selectedName))
                {
                    string destDir = Path.Combine(VoiceExtrasRoot167(), selectedName);
                    foreach (string src in Directory.GetFiles(extrasSource, "*.mp3")) { File.Copy(src, VoiceUniqueExtra167(destDir, Path.GetFileName(src)), false); added++; }
                }
                else
                {
                    foreach (string group in VoiceGroups167)
                    {
                        string srcDir = Path.Combine(extrasSource, group);
                        if (!Directory.Exists(srcDir)) continue;
                        string destDir = Path.Combine(VoiceExtrasRoot167(), group);
                        foreach (string src in Directory.GetFiles(srcDir, "*.mp3")) { File.Copy(src, VoiceUniqueExtra167(destDir, Path.GetFileName(src)), false); added++; }
                    }
                }
                VoiceRefreshUi062();
                if (added > 0)
                {
                    ClientStore.Log("falas extras 1.0.67 adicionadas: " + added);
                    MessageBox.Show(added + " fala(s) extra(s) adicionada(s). Elas entram no sorteio do grupo correspondente.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show("Nenhuma fala extra encontrada. Use pastas como extras/crash, extras/delivery ou extras/random.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) { MessageBox.Show("Não foi possível adicionar as falas: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void VoiceRestoreBackup167()
    {
        string backup = VoiceBackupRoot167();
        if (!Directory.Exists(backup))
        {
            MessageBox.Show("Ainda não existe backup de voz. Ele é criado automaticamente antes de uma substituição.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show("Restaurar o pacote salvo antes da última substituição?", "Voz GAT", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            VoiceStop062();
            string active = VoiceIndividualRoot166();
            if (Directory.Exists(active)) Directory.Delete(active, true);
            Directory.CreateDirectory(active);
            VoiceCopyDir167(backup, active);
            VoiceRefreshUi062();
            ClientStore.Log("backup de voz 1.0.67 restaurado");
            MessageBox.Show("Backup de voz restaurado.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            VoicePlayGroup062("startup");
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível restaurar o backup: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void VoiceOpenTemplate167()
    {
        const string url = "https://gatlogets2.com.br/GAT_MODELO_PACOTE_VOZ_v1.txt";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch
        {
            try { Clipboard.SetText(url); } catch { }
            MessageBox.Show("Abra este endereço para baixar o modelo:\n" + url + "\n\nO link também foi copiado.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
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

        double damage = VoiceTruckDamage168(tele);
        if (!double.IsNaN(damage) && damage >= 0)
        {
            VoiceObserveDamage168(damage, now);
            _voiceLastDamage062 = damage;
        }

        // A fala decorativa agora pertence a carga atual: 500, 1000, 1500 km...
        VoiceObserveJobRandom168(tele, now);

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







