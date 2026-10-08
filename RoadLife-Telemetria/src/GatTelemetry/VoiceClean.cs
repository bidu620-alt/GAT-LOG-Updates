using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private static readonly int[] VoiceCleanLimits = new int[] { 20,30,40,50,60,70,80,90,100,110,120,130 };

    private HttpClient _voiceCleanHttp;
    private System.Windows.Forms.Timer _voiceCleanTimer;
    private bool _voiceCleanBusy;
    private bool _voiceCleanInitialized;
    private bool _voiceCleanMuted;
    private int _voiceCleanVolume = 80;
    private int _voiceCleanSerial;
    private string _voiceCleanAlias = string.Empty;

    private bool _voiceCleanOver;
    private int _voiceCleanLastEventLimit;
    private double _voiceCleanLastRealLimit = double.NaN;
    private DateTime _voiceCleanLastAlert = DateTime.MinValue;
    private DateTime _voiceCleanLastError = DateTime.MinValue;
    private bool _voiceCleanConnectedLogged;

    private TrackBar _voiceCleanVolumeBar;
    private Label _voiceCleanVolumeLabel;
    private Label _voiceCleanStatusLabel;

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

    private static string VoiceCleanRoot()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voz");
    }

    private static string VoiceCleanLimitsDir()
    {
        return Path.Combine(VoiceCleanRoot(), "limites");
    }

    private static string VoiceCleanEventsDir()
    {
        return Path.Combine(VoiceCleanRoot(), "eventos");
    }

    private static string VoiceCleanSettingsFile()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GAT-LOG", "GAT-Telemetria-CleanVoice");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "voice-clean-v1.txt");
    }

    private void VoiceCleanInitialize()
    {
        if (_voiceCleanInitialized) return;
        _voiceCleanInitialized = true;
        VoiceCleanLoadSettings();

        _voiceCleanHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        _voiceCleanTimer = new System.Windows.Forms.Timer { Interval = 350 };
        _voiceCleanTimer.Tick += async delegate { await VoiceCleanPoll(); };
        _voiceCleanTimer.Start();

        FormClosed += delegate
        {
            try { _voiceCleanTimer?.Stop(); _voiceCleanTimer?.Dispose(); } catch { }
            try { _voiceCleanHttp?.Dispose(); } catch { }
            VoiceCleanStop();
        };

        ClientStore.Log("VOZ NOVA 1.0.68.8 iniciada; DASH visual; pasta=" + VoiceCleanRoot());
    }

    private void VoiceCleanLoadSettings()
    {
        _voiceCleanMuted = false;
        _voiceCleanVolume = 80;
        try
        {
            string f = VoiceCleanSettingsFile();
            if (!File.Exists(f)) return;
            string[] p = File.ReadAllText(f).Trim().Split('|');
            int v;
            if (p.Length > 0 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                _voiceCleanVolume = Math.Max(0, Math.Min(100, v));
            if (p.Length > 1)
                _voiceCleanMuted = string.Equals(p[1], "1", StringComparison.Ordinal);
        }
        catch { _voiceCleanMuted = false; _voiceCleanVolume = 80; }
    }

    private void VoiceCleanSaveSettings()
    {
        try
        {
            File.WriteAllText(
                VoiceCleanSettingsFile(),
                _voiceCleanVolume.ToString(CultureInfo.InvariantCulture) + "|" + (_voiceCleanMuted ? "1" : "0"));
        }
        catch { }
    }

    private async Task VoiceCleanPoll()
    {
        if (_voiceCleanBusy || _voiceCleanHttp == null) return;
        _voiceCleanBusy = true;
        try
        {
            string raw = await _voiceCleanHttp.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry");
            JObject tele = JObject.Parse(raw);
            TelemetryEngine.Normalize(tele);
            VoiceCleanObserveRoad(tele, DateTime.UtcNow);

            if (!_voiceCleanConnectedLogged)
            {
                _voiceCleanConnectedLogged = true;
                ClientStore.Log("VOZ NOVA 1.0.68.8 TruckSim GPS conectado diretamente.");
            }
        }
        catch (Exception ex)
        {
            if ((DateTime.UtcNow - _voiceCleanLastError).TotalSeconds >= 15)
            {
                _voiceCleanLastError = DateTime.UtcNow;
                ClientStore.Log("VOZ NOVA 1.0.68.8 TruckSim indisponivel: " + ex.Message);
            }
        }
        finally { _voiceCleanBusy = false; }
    }

    private static double VoiceCleanNumber(JObject root, params string[] paths)
    {
        if (root == null) return double.NaN;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken t = root.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                double v;
                if (double.TryParse(Convert.ToString(t, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                    return v;
            }
            catch { }
        }
        return double.NaN;
    }

    private static int VoiceCleanEventLimit(double realLimit)
    {
        if (double.IsNaN(realLimit) || double.IsInfinity(realLimit) || realLimit <= 0) return 0;

        int exact = (int)Math.Round(realLimit, MidpointRounding.AwayFromZero);
        foreach (int v in VoiceCleanLimits)
            if (exact == v) return v;

        int nearest = (int)Math.Round(realLimit / 10.0, MidpointRounding.AwayFromZero) * 10;
        if (nearest < 20 || nearest > 130) return 0;

        // TruckSim pode devolver limites britanicos convertidos: 32, 48, 64, 97, 113...
        // O limite real decide o excesso. O arredondamento serve SOMENTE para escolher o MP3.
        if (Math.Abs(realLimit - nearest) <= 5.1) return nearest;
        return 0;
    }

    private void VoiceCleanObserveRoad(JObject tele, DateTime now)
    {
        if (tele == null || _voiceCleanMuted || _voiceCleanVolume <= 0) return;

        double speed = VoiceCleanNumber(tele, "truck.speed", "Truck.Speed");
        double realLimit = VoiceCleanNumber(tele, "speed_limit_kmh");

        if (double.IsNaN(speed) || double.IsNaN(realLimit) || realLimit <= 0)
        {
            _voiceCleanOver = false;
            _voiceCleanLastEventLimit = 0;
            _voiceCleanLastRealLimit = double.NaN;
            return;
        }

        speed = Math.Abs(speed);
        int eventLimit = VoiceCleanEventLimit(realLimit);
        if (eventLimit <= 0)
        {
            _voiceCleanOver = false;
            return;
        }

        bool over = speed > realLimit;
        bool eventChanged = _voiceCleanLastEventLimit > 0 && eventLimit != _voiceCleanLastEventLimit;
        bool realChanged = !double.IsNaN(_voiceCleanLastRealLimit) && Math.Abs(realLimit - _voiceCleanLastRealLimit) >= 1.0;

        if (!over)
        {
            _voiceCleanOver = false;
            _voiceCleanLastEventLimit = eventLimit;
            _voiceCleanLastRealLimit = realLimit;
            return;
        }

        if (eventChanged || realChanged) _voiceCleanOver = false;

        bool due = !_voiceCleanOver || (now - _voiceCleanLastAlert).TotalSeconds >= 15.0;
        if (due)
        {
            string eventName = "limite_" + eventLimit.ToString("D3", CultureInfo.InvariantCulture);
            if (VoiceCleanPlayEvent(eventName, true))
            {
                _voiceCleanOver = true;
                _voiceCleanLastAlert = now;
                ClientStore.Log(
                    "VOZ NOVA excesso: velocidade=" + Math.Round(speed).ToString(CultureInfo.InvariantCulture) +
                    " limiteReal=" + Math.Round(realLimit).ToString(CultureInfo.InvariantCulture) +
                    " evento=" + eventName);
            }
            else
            {
                ClientStore.Log("VOZ NOVA arquivo ausente/falhou: " + eventName + ".mp3");
            }
        }

        _voiceCleanLastEventLimit = eventLimit;
        _voiceCleanLastRealLimit = realLimit;
    }

    private static string VoiceCleanEventFile(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return string.Empty;
        string safe = eventName.Trim().ToLowerInvariant();
        if (safe.StartsWith("limite_", StringComparison.Ordinal))
            return Path.Combine(VoiceCleanLimitsDir(), safe + ".mp3");
        return Path.Combine(VoiceCleanEventsDir(), safe + ".mp3");
    }

    private bool VoiceCleanPlayEvent(string eventName, bool interrupt)
    {
        if (_voiceCleanMuted || _voiceCleanVolume <= 0) return false;
        string file = VoiceCleanEventFile(eventName);
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return false;

        try
        {
            if (interrupt) VoiceCleanStop();
            else if (!string.IsNullOrWhiteSpace(_voiceCleanAlias)) return false;

            int serial = Interlocked.Increment(ref _voiceCleanSerial);
            string alias = "gatcleanvoice_" + serial.ToString(CultureInfo.InvariantCulture);
            string safe = file.Replace("\"", "");
            int open = mciSendString("open \"" + safe + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
            if (open != 0)
            {
                ClientStore.Log("VOZ NOVA MCI open erro=" + open + " arquivo=" + Path.GetFileName(file));
                return false;
            }

            mciSendString("setaudio " + alias + " volume to " + (_voiceCleanVolume * 10), null, 0, IntPtr.Zero);
            int play = mciSendString("play " + alias, null, 0, IntPtr.Zero);
            if (play != 0)
            {
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
                ClientStore.Log("VOZ NOVA MCI play erro=" + play + " arquivo=" + Path.GetFileName(file));
                return false;
            }

            _voiceCleanAlias = alias;
            ClientStore.Log("VOZ NOVA tocando evento=" + eventName + " arquivo=" + Path.GetFileName(file));

            Task.Run(async delegate
            {
                await Task.Delay(10000);
                try
                {
                    mciSendString("close " + alias, null, 0, IntPtr.Zero);
                    if (_voiceCleanAlias == alias) _voiceCleanAlias = string.Empty;
                }
                catch { }
            });
            return true;
        }
        catch (Exception ex)
        {
            ClientStore.Log("VOZ NOVA falha player: " + ex.Message);
            return false;
        }
    }

    private void VoiceCleanStop()
    {
        try
        {
            string alias = _voiceCleanAlias;
            _voiceCleanAlias = string.Empty;
            if (!string.IsNullOrWhiteSpace(alias))
            {
                mciSendString("stop " + alias, null, 0, IntPtr.Zero);
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
            }
        }
        catch { }
    }

    private bool VoiceCleanHandleLegacy(string text, string logLabel)
    {
        string joined = ((logLabel ?? string.Empty) + " " + (text ?? string.Empty)).ToLowerInvariant();

        if ((joined.Contains("trabalho") || joined.Contains("carga")) &&
            (joined.Contains("iniciado") || joined.Contains("iniciad") || joined.Contains("pegou")))
            return VoiceCleanPlayEvent("trabalho_iniciado", true);

        if ((joined.Contains("trabalho") || joined.Contains("entrega") || joined.Contains("carga")) &&
            (joined.Contains("finalizado") || joined.Contains("conclu") || joined.Contains("entreg")))
            return VoiceCleanPlayEvent("trabalho_finalizado", true);

        return false;
    }

    private void BuildVoiceCleanSettings(Panel p)
    {
        VoiceCleanLoadSettings();

        var box = new Panel
        {
            Left = 0, Top = 252, Width = Math.Max(520, p.Width), Height = 132,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };

        box.Controls.Add(new Label
        {
            Text = "VOZ LOCAL • MOTOR NOVO LIMPO",
            Left = 14, Top = 8, Width = 290, Height = 22,
            ForeColor = Color.FromArgb(100, 180, 255),
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });

        _voiceCleanVolumeLabel = new Label
        {
            Text = "Volume: " + _voiceCleanVolume + "%",
            Left = 14, Top = 39, Width = 115, Height = 24,
            ForeColor = Color.Gainsboro
        };
        box.Controls.Add(_voiceCleanVolumeLabel);

        _voiceCleanVolumeBar = new TrackBar
        {
            Left = 125, Top = 30, Width = 240,
            Minimum = 0, Maximum = 100, Value = _voiceCleanVolume,
            TickFrequency = 10, SmallChange = 5, LargeChange = 10
        };
        _voiceCleanVolumeBar.Scroll += delegate
        {
            _voiceCleanVolume = _voiceCleanVolumeBar.Value;
            _voiceCleanMuted = _voiceCleanVolume <= 0;
            VoiceCleanSaveSettings();
            VoiceCleanRefreshUi();
            try
            {
                if (!string.IsNullOrWhiteSpace(_voiceCleanAlias))
                    mciSendString("setaudio " + _voiceCleanAlias + " volume to " + (_voiceCleanVolume * 10), null, 0, IntPtr.Zero);
            }
            catch { }
        };
        box.Controls.Add(_voiceCleanVolumeBar);

        var test = HubButton041("TESTAR VOZ", 120);
        test.Left = 380; test.Top = 31;
        test.Click += delegate
        {
            _voiceCleanMuted = false;
            if (_voiceCleanVolume <= 0) _voiceCleanVolume = 80;
            VoiceCleanSaveSettings();
            VoiceCleanRefreshUi();
            if (!VoiceCleanPlayEvent("limite_080", true))
                MessageBox.Show("Nao foi possivel tocar voz\\limites\\limite_080.mp3.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        box.Controls.Add(test);

        var folder = HubButton041("ABRIR PASTA VOZ", 150);
        folder.Left = 510; folder.Top = 31;
        folder.Click += delegate
        {
            try { Process.Start("explorer.exe", VoiceCleanRoot()); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        box.Controls.Add(folder);

        _voiceCleanStatusLabel = new Label
        {
            Left = 14, Top = 78, Width = Math.Max(480, box.Width - 28), Height = 45,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(145, 175, 205)
        };
        box.Controls.Add(_voiceCleanStatusLabel);

        p.Controls.Add(box);
        VoiceCleanRefreshUi();
    }

    private void VoiceCleanRefreshUi()
    {
        try
        {
            if (_voiceCleanVolumeLabel != null)
                _voiceCleanVolumeLabel.Text = "Volume: " + _voiceCleanVolume + "%";
            if (_voiceCleanVolumeBar != null && _voiceCleanVolumeBar.Value != _voiceCleanVolume)
                _voiceCleanVolumeBar.Value = Math.Max(0, Math.Min(100, _voiceCleanVolume));

            int installed = 0;
            foreach (int v in VoiceCleanLimits)
            {
                string f = Path.Combine(VoiceCleanLimitsDir(), "limite_" + v.ToString("D3", CultureInfo.InvariantCulture) + ".mp3");
                if (File.Exists(f)) installed++;
            }

            if (_voiceCleanStatusLabel != null)
                _voiceCleanStatusLabel.Text =
                    installed + "/12 limites instalados • TruckSim GPS direto • DASH somente visual\r\n" +
                    "Pasta oficial: " + VoiceCleanRoot();
        }
        catch { }
    }
}

