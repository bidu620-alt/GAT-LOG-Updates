using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private const string LiveListEndpoint1689 = "https://api.gatlogets2.com.br/api/live/list";
    private readonly Dictionary<string, JObject> _routeStreams1689 = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
    private JObject _micStream1689;
    private DateTime _liveLastFetch1689 = DateTime.MinValue;
    private DateTime _roleLastFetch1689 = DateTime.MinValue;
    private bool _liveFetchBusy1689;
    private bool _radioAutoStarted1689;
    private string _liveRole1689 = string.Empty;

    private Panel _radioHome1689;
    private Label _radioHomeState1689;
    private Label _radioHomeNow1689;
    private RadioMeter1689 _radioMeter1689;
    private TrackBar _radioVolume1689;
    private Button _radioMute1689;
    private Button _routeBroadcast1689;
    private Button _micBroadcast1689;
    private ComboBox _routeQuality1689;

    private LiveMediaForm1689 _routeBroadcastForm1689;
    private LiveMediaForm1689 _routeViewer1689;
    private LiveMediaForm1689 _micBroadcastForm1689;
    private LiveMediaForm1689 _micListener1689;
    private string _micListenerStream1689 = string.Empty;
    private bool _micBroadcasting1689;

    private Control BuildRadioHome1689()
    {
        _radioHome1689 = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(7, 2, 7, 7),
            BackColor = Color.FromArgb(5, 22, 39)
        };

        var title = new Label
        {
            Text = "📻 GAT RÁDIO",
            Left = 15, Top = 11, Width = 125, Height = 24,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
        };
        _radioHomeState1689 = new Label
        {
            Text = "RECONECTANDO",
            Left = 15, Top = 38, Width = 125, Height = 20,
            ForeColor = Color.FromArgb(103, 201, 255),
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold)
        };
        _radioHomeNow1689 = new Label
        {
            Text = "Programação oficial do GAT",
            Left = 145, Top = 12, Width = 270, Height = 46,
            ForeColor = Color.FromArgb(188, 214, 239),
            Font = new Font("Segoe UI", 8.7f)
        };
        _radioMeter1689 = new RadioMeter1689
        {
            Left = 425, Top = 15, Width = 102, Height = 46
        };
        _radioVolume1689 = new TrackBar
        {
            Left = 540, Top = 17, Width = 155, Height = 42,
            Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 55
        };
        _radioVolume1689.Scroll += delegate
        {
            try { _hubRadio041?.HubSetVolume1689(_radioVolume1689.Value); } catch { }
        };

        _radioMute1689 = HubButton041("🔊 SOM", 78);
        _radioMute1689.Left = 700; _radioMute1689.Top = 17; _radioMute1689.Height = 34;
        _radioMute1689.Click += delegate
        {
            try
            {
                _hubRadio041?.HubToggleMute1689();
                SyncLiveHome1689();
            }
            catch { }
        };

        var openRadio = HubButton041("ABRIR RÁDIO", 105);
        openRadio.Left = 785; openRadio.Top = 17; openRadio.Height = 34;
        openRadio.Click += delegate { ShowHubPage041("radio"); };

        _routeQuality1689 = new ComboBox
        {
            Left = 900, Top = 19, Width = 105, Height = 29,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(7, 34, 57),
            ForeColor = Color.White
        };
        _routeQuality1689.Items.Add("Normal");
        _routeQuality1689.Items.Add("Econômico");
        _routeQuality1689.SelectedIndex = 0;

        _routeBroadcast1689 = HubButton041("🔴 TRANSMITIR ROTA", 145);
        _routeBroadcast1689.Left = 1012; _routeBroadcast1689.Top = 17; _routeBroadcast1689.Height = 34;
        _routeBroadcast1689.Click += delegate { StartRouteBroadcast1689(); };

        _micBroadcast1689 = HubButton041("🎙 ENTRAR AO VIVO", 140);
        _micBroadcast1689.Left = 1164; _micBroadcast1689.Top = 17; _micBroadcast1689.Height = 34;
        _micBroadcast1689.Visible = false;
        _micBroadcast1689.Click += delegate { StartMicBroadcast1689(); };

        _radioHome1689.Controls.Add(title);
        _radioHome1689.Controls.Add(_radioHomeState1689);
        _radioHome1689.Controls.Add(_radioHomeNow1689);
        _radioHome1689.Controls.Add(_radioMeter1689);
        _radioHome1689.Controls.Add(_radioVolume1689);
        _radioHome1689.Controls.Add(_radioMute1689);
        _radioHome1689.Controls.Add(openRadio);
        _radioHome1689.Controls.Add(_routeQuality1689);
        _radioHome1689.Controls.Add(_routeBroadcast1689);
        _radioHome1689.Controls.Add(_micBroadcast1689);

        _radioHome1689.Resize += delegate
        {
            int w = _radioHome1689.ClientSize.Width;
            bool compact = w < 1050;
            _radioHomeNow1689.Visible = !compact;
            _radioMeter1689.Visible = w >= 760;
            _routeQuality1689.Visible = w >= 870;
            int right = w - 14;
            if (_micBroadcast1689.Visible)
            {
                _micBroadcast1689.Left = Math.Max(8, right - _micBroadcast1689.Width);
                right = _micBroadcast1689.Left - 7;
            }
            _routeBroadcast1689.Left = Math.Max(8, right - _routeBroadcast1689.Width);
            right = _routeBroadcast1689.Left - 7;
            if (_routeQuality1689.Visible)
            {
                _routeQuality1689.Left = Math.Max(8, right - _routeQuality1689.Width);
                right = _routeQuality1689.Left - 7;
            }
            openRadio.Left = Math.Max(8, right - openRadio.Width);
            right = openRadio.Left - 7;
            _radioMute1689.Left = Math.Max(8, right - _radioMute1689.Width);
            right = _radioMute1689.Left - 7;
            _radioVolume1689.Left = Math.Max(145, right - _radioVolume1689.Width);
            _radioMeter1689.Left = Math.Max(145, _radioVolume1689.Left - _radioMeter1689.Width - 8);
        };

        return _radioHome1689;
    }

    private void ApplyLive1689()
    {
        FormClosed += delegate
        {
            try { _routeBroadcastForm1689?.Close(); } catch { }
            try { _routeViewer1689?.Close(); } catch { }
            try { _micBroadcastForm1689?.Close(); } catch { }
            try { _micListener1689?.Close(); } catch { }
        };
    }

    private async Task LiveTick1689(bool force)
    {
        try
        {
            if (!AccountReady)
            {
                if (_radioHomeState1689 != null) _radioHomeState1689.Text = "ENTRE NA CONTA GAT";
                if (_radioMeter1689 != null) _radioMeter1689.Active1689 = false;
                return;
            }

            EnsureRadio041();
            if (_hubRadio041 != null)
            {
                _hubRadio041.ConfigureAccount049(_accountUser, _accountToken);
                if (!_radioAutoStarted1689)
                {
                    _radioAutoStarted1689 = true;
                    await _hubRadio041.HubStartShared1689Async();
                    if (_radioVolume1689 != null) _radioVolume1689.Value = Math.Max(0, Math.Min(100, _hubRadio041.HubVolume1689));
                }
                else
                {
                    await _hubRadio041.HubRefresh1689Async();
                }
            }

            await RefreshRole1689(force);
            await RefreshLiveStreams1689(force);
            EnsureMicListener1689();
            SyncLiveHome1689();
        }
        catch { }
    }

    private async Task RefreshRole1689(bool force)
    {
        if (!AccountReady) return;
        if (!force && (DateTime.UtcNow - _roleLastFetch1689).TotalSeconds < 20) return;
        _roleLastFetch1689 = DateTime.UtcNow;
        try
        {
            var payload = new JObject { ["token"] = _accountToken };
            using (var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _hubHttp041.PostAsync("https://api.gatlogets2.com.br/api/account/session", content))
            {
                JObject root = JObject.Parse(await response.Content.ReadAsStringAsync());
                if (response.IsSuccessStatusCode && root.Value<bool?>("ok") == true)
                    _liveRole1689 = (Convert.ToString(root["role"]) ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
        catch { }
        if (_micBroadcast1689 != null)
            _micBroadcast1689.Visible = _liveRole1689 == "owner" || _liveRole1689 == "admin";
    }

    private async Task<JObject> LivePost1689(string url, JObject payload)
    {
        if (payload == null) payload = new JObject();
        payload["token"] = _accountToken;
        using (var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
        using (HttpResponseMessage response = await _hubHttp041.PostAsync(url, content))
        {
            string text = await response.Content.ReadAsStringAsync();
            JObject root = JObject.Parse(text);
            if (!response.IsSuccessStatusCode || root.Value<bool?>("ok") != true)
                throw new InvalidOperationException(Convert.ToString(root["error"]) ?? ("HTTP " + (int)response.StatusCode));
            return root;
        }
    }

    private async Task RefreshLiveStreams1689(bool force = false)
    {
        if (!AccountReady || _liveFetchBusy1689) return;
        if (!force && (DateTime.UtcNow - _liveLastFetch1689).TotalSeconds < 2.0) return;
        _liveFetchBusy1689 = true;
        _liveLastFetch1689 = DateTime.UtcNow;
        try
        {
            JObject root = await LivePost1689(LiveListEndpoint1689, new JObject());
            var next = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            JObject mic = null;
            JArray streams = root["streams"] as JArray;
            if (streams != null)
            {
                foreach (JObject item in streams.OfType<JObject>())
                {
                    string kind = (Convert.ToString(item["kind"]) ?? "").Trim().ToLowerInvariant();
                    string user = (Convert.ToString(item["user"]) ?? "").Trim();
                    if (kind == "route" && !string.IsNullOrWhiteSpace(user)) next[user] = item;
                    else if (kind == "radio_mic" && mic == null) mic = item;
                }
            }
            _routeStreams1689.Clear();
            foreach (var kv in next) _routeStreams1689[kv.Key] = kv.Value;
            _micStream1689 = mic;
        }
        catch
        {
            // Mantem a ultima lista por uma oscilacao curta para nao piscar a interface.
        }
        finally { _liveFetchBusy1689 = false; }
    }

    private JObject FindRouteStream1689(string account, string driver)
    {
        JObject item;
        if (!string.IsNullOrWhiteSpace(account) && _routeStreams1689.TryGetValue(account, out item)) return item;
        if (!string.IsNullOrWhiteSpace(driver) && _routeStreams1689.TryGetValue(driver, out item)) return item;
        return null;
    }

    private string RouteLiveLabel1689(string account, string driver)
    {
        return FindRouteStream1689(account, driver) != null ? "▶ ASSISTIR" : "—";
    }

    private void StyleLiveCell1689(DataGridViewRow row, string account, string driver)
    {
        try
        {
            var cell = row.Cells["live"];
            bool live = FindRouteStream1689(account, driver) != null;
            cell.Style.ForeColor = live ? Color.FromArgb(255, 103, 112) : Color.FromArgb(102, 123, 143);
            cell.Style.BackColor = live ? Color.FromArgb(54, 18, 27) : Color.FromArgb(5, 18, 33);
            cell.Style.SelectionBackColor = cell.Style.BackColor;
            cell.Style.SelectionForeColor = cell.Style.ForeColor;
        }
        catch { }
    }

    private void DriversLiveClick1689(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _homeDrivers045 == null) return;
        if (_homeDrivers045.Columns[e.ColumnIndex].Name != "live") return;
        try
        {
            DataGridViewRow row = _homeDrivers045.Rows[e.RowIndex];
            string account = Convert.ToString(row.Cells["account"].Value) ?? "";
            string driver = Convert.ToString(row.Cells["driver"].Value) ?? "";
            JObject stream = FindRouteStream1689(account, driver);
            if (stream == null) return;
            OpenRouteViewer1689(stream);
        }
        catch { }
    }

    private void OpenRouteViewer1689(JObject stream)
    {
        if (!AccountReady || stream == null) return;
        string id = Convert.ToString(stream["stream_id"]) ?? "";
        string owner = Convert.ToString(stream["user"]) ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(owner)) return;
        try { _routeViewer1689?.Close(); } catch { }
        _routeViewer1689 = new LiveMediaForm1689("viewer", "route", _accountToken, _accountUser, owner, id, "normal", false);
        _routeViewer1689.FormClosed += delegate { _routeViewer1689 = null; };
        _routeViewer1689.Show(this);
        _routeViewer1689.BringToFront();
    }

    private void StartRouteBroadcast1689()
    {
        if (!AccountReady)
        {
            MessageBox.Show(this, "Entre na Conta GAT antes de transmitir.", "GAT Ao Vivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_routeBroadcastForm1689 != null && !_routeBroadcastForm1689.IsDisposed)
        {
            _routeBroadcastForm1689.BringToFront();
            return;
        }

        string quality = _routeQuality1689 != null && _routeQuality1689.SelectedIndex == 1 ? "economico" : "normal";
        _routeBroadcastForm1689 = new LiveMediaForm1689("broadcast", "route", _accountToken, _accountUser, "", "", quality, false);
        _routeBroadcastForm1689.LiveMessage1689 += delegate(string type, JObject msg)
        {
            if (type == "broadcastStarted")
            {
                _routeBroadcast1689.Text = "🔴 ROTA AO VIVO";
            }
            else if (type == "broadcastStopped" || type == "error")
            {
                _routeBroadcast1689.Text = "🔴 TRANSMITIR ROTA";
            }
        };
        _routeBroadcastForm1689.FormClosed += delegate
        {
            _routeBroadcastForm1689 = null;
            if (_routeBroadcast1689 != null) _routeBroadcast1689.Text = "🔴 TRANSMITIR ROTA";
        };
        _routeBroadcastForm1689.Show(this);
    }

    private void StartMicBroadcast1689()
    {
        if (!AccountReady || !(_liveRole1689 == "owner" || _liveRole1689 == "admin"))
        {
            MessageBox.Show(this, "Somente ADM/Owner pode entrar ao vivo na Rádio GAT.", "GAT Rádio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_micBroadcastForm1689 != null && !_micBroadcastForm1689.IsDisposed)
        {
            _micBroadcastForm1689.BringToFront();
            return;
        }

        _micBroadcastForm1689 = new LiveMediaForm1689("broadcast", "radio_mic", _accountToken, _accountUser, "", "", "normal", false);
        _micBroadcastForm1689.LiveMessage1689 += delegate(string type, JObject msg)
        {
            if (type == "broadcastStarted")
            {
                _micBroadcasting1689 = true;
                if (_micBroadcast1689 != null) _micBroadcast1689.Text = "🎙 LOCUÇÃO AO VIVO";
                try { _hubRadio041?.HubSetDucked1689(true); } catch { }
                SyncLiveHome1689();
            }
            else if (type == "broadcastStopped" || type == "error")
            {
                _micBroadcasting1689 = false;
                if (_micBroadcast1689 != null) _micBroadcast1689.Text = "🎙 ENTRAR AO VIVO";
                if (_micStream1689 == null) try { _hubRadio041?.HubSetDucked1689(false); } catch { }
                SyncLiveHome1689();
            }
        };
        _micBroadcastForm1689.FormClosed += delegate
        {
            _micBroadcastForm1689 = null;
            _micBroadcasting1689 = false;
            if (_micBroadcast1689 != null) _micBroadcast1689.Text = "🎙 ENTRAR AO VIVO";
            if (_micStream1689 == null) try { _hubRadio041?.HubSetDucked1689(false); } catch { }
        };
        _micBroadcastForm1689.Show(this);
    }

    private void EnsureMicListener1689()
    {
        string streamId = _micStream1689 == null ? "" : (Convert.ToString(_micStream1689["stream_id"]) ?? "");
        string owner = _micStream1689 == null ? "" : (Convert.ToString(_micStream1689["user"]) ?? "");

        bool ownBroadcast = _micBroadcasting1689 && string.Equals(owner, _accountUser, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(streamId) || ownBroadcast)
        {
            if (!ownBroadcast && _micListener1689 != null)
            {
                try { _micListener1689.Close(); } catch { }
                _micListener1689 = null;
                _micListenerStream1689 = "";
            }
            if (string.IsNullOrWhiteSpace(streamId) && !_micBroadcasting1689)
                try { _hubRadio041?.HubSetDucked1689(false); } catch { }
            return;
        }

        try { _hubRadio041?.HubSetDucked1689(true); } catch { }
        if (_micListener1689 != null && !_micListener1689.IsDisposed && _micListenerStream1689 == streamId) return;
        try { _micListener1689?.Close(); } catch { }

        _micListenerStream1689 = streamId;
        _micListener1689 = new LiveMediaForm1689("viewer", "radio_mic", _accountToken, _accountUser, owner, streamId, "normal", true);
        _micListener1689.LiveMessage1689 += delegate(string type, JObject msg)
        {
            if (type == "viewerConnected")
            {
                try { _hubRadio041?.HubSetDucked1689(true); } catch { }
                SyncLiveHome1689();
            }
            else if (type == "viewerDisconnected" || type == "error")
            {
                if (_micStream1689 == null && !_micBroadcasting1689)
                    try { _hubRadio041?.HubSetDucked1689(false); } catch { }
            }
        };
        _micListener1689.FormClosed += delegate
        {
            _micListener1689 = null;
            _micListenerStream1689 = "";
        };
        _micListener1689.Show(this);
    }

    private void SyncLiveHome1689()
    {
        try
        {
            bool micLive = _micBroadcasting1689 || _micStream1689 != null;
            string state = micLive ? "🎙 LOCUÇÃO AO VIVO" : (_hubRadio041 == null ? "RECONECTANDO" : _hubRadio041.HubState1689);
            if (_radioHomeState1689 != null)
            {
                _radioHomeState1689.Text = state;
                _radioHomeState1689.ForeColor = micLive
                    ? Color.FromArgb(255, 104, 116)
                    : (state == "AO VIVO" ? Color.FromArgb(74, 232, 135) : Color.FromArgb(103, 201, 255));
            }
            if (_radioHomeNow1689 != null)
            {
                if (micLive)
                {
                    string who = _micStream1689 == null ? _accountUser : (Convert.ToString(_micStream1689["user"]) ?? "ADM");
                    _radioHomeNow1689.Text = "Locução de " + who + "\r\nA música abaixa automaticamente.";
                }
                else
                {
                    string now = _hubRadio041 == null ? "" : _hubRadio041.HubNowPlaying042;
                    _radioHomeNow1689.Text = string.IsNullOrWhiteSpace(now) ? "Programação oficial do GAT" : now;
                }
            }
            if (_radioMeter1689 != null) _radioMeter1689.Active1689 = micLive || (_hubRadio041 != null && _hubRadio041.HubListening1689);
            if (_radioMute1689 != null) _radioMute1689.Text = _hubRadio041 != null && _hubRadio041.HubMuted1689 ? "🔇 MUDO" : "🔊 SOM";
            if (_micBroadcast1689 != null) _micBroadcast1689.Visible = _liveRole1689 == "owner" || _liveRole1689 == "admin";
            if (_radioHome1689 != null) _radioHome1689.PerformLayout();
        }
        catch { }
    }
}

internal sealed class RadioMeter1689 : Control
{
    private readonly Timer _timer = new Timer { Interval = 90 };
    private int _phase;
    private bool _active;

    internal bool Active1689
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            if (_active) _timer.Start(); else _timer.Stop();
            Invalidate();
        }
    }

    internal RadioMeter1689()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        _timer.Tick += delegate { _phase++; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int bars = 10;
        int gap = 3;
        int bw = Math.Max(3, (Width - (bars - 1) * gap) / bars);
        for (int i = 0; i < bars; i++)
        {
            double wave = _active ? (Math.Sin((_phase + i * 2.1) * 0.48) + 1.0) / 2.0 : 0.12;
            int h = Math.Max(4, (int)Math.Round(Height * (0.18 + wave * 0.78)));
            int x = i * (bw + gap);
            int y = Height - h;
            Color c = (i % 2 == 0) ? Color.FromArgb(48, 221, 135) : Color.FromArgb(62, 153, 255);
            using (var b = new SolidBrush(c)) e.Graphics.FillRectangle(b, x, y, bw, h);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
