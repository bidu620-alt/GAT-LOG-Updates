using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private readonly Dictionary<string, Panel> _hubPages041 = new Dictionary<string, Panel>();
    private readonly Dictionary<string, Button> _hubNav041 = new Dictionary<string, Button>();
    private readonly HttpClient _hubHttp041 = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Timer _hubStatusTimer041 = new Timer { Interval = 700 };
    private readonly Timer _hubDashTimer041 = new Timer { Interval = 450 };
    private Panel _hubBody041;
    private Panel _hubRadioHost041;
    private RadioForm _hubRadio041;
    private WebView2 _hubDash041;
    private ModernCard _hubAccountCard041;
    private ModernCard _hubServerCard041;
    private Label _hAccount041, _hEts041, _hCentral041, _hServer041;
    private bool _hubDashReady041, _hubDashBusy041, _hubApplied041;
    private TruckOverlay041 _truckOverlay041;
    private VideoOverlay041 _videoOverlay041;
    private TrackBar _overlayOpacity041;

    private void ApplyHub041()
    {
        if (_hubApplied041) return;
        _hubApplied041 = true;
        SuspendLayout();

        var old = Controls.Cast<Control>().ToList();
        _hubAccountCard041 = old.OfType<ModernCard>().FirstOrDefault(x => x.Caption == "CONTA GAT");
        _hubServerCard041 = old.OfType<ModernCard>().FirstOrDefault(x => (x.Caption ?? "").StartsWith("COMBOIO / SERVIDOR"));
        Controls.Clear();

        Text = "GAT Telemetria BETA 1.0.41";
        MinimumSize = new Size(1040, 690);
        Size = new Size(1220, 820);
        BackColor = Color.FromArgb(3, 11, 22);

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor, Margin = Padding.Empty, Padding = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(shell);
        shell.Controls.Add(HubHeader041(), 0, 0);
        shell.Controls.Add(HubNav041(), 0, 1);
        _hubBody041 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14), BackColor = BackColor };
        shell.Controls.Add(_hubBody041, 0, 2);
        shell.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "GAT LOG ETS2  •  Cliente 1.0.41 TESTE  •  conexão que move distâncias", Padding = new Padding(20, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(104, 128, 155), BackColor = Color.FromArgb(2, 8, 17) }, 0, 3);

        AddHubPage041("home", Home041());
        AddHubPage041("dash", Dash041());
        AddHubPage041("radio", Radio041());
        AddHubPage041("gps", Simple041("GPS", "GPS por voz e alertas ficarão aqui. A seleção de novas vozes será adicionada depois."));
        AddHubPage041("server", Server041());
        AddHubPage041("updates", Updates041());
        AddHubPage041("settings", Settings041());
        ShowHubPage041("home");

        _hubStatusTimer041.Tick += delegate { SyncHub041(); };
        _hubStatusTimer041.Start();
        _hubDashTimer041.Tick += async delegate { await PollDash041(); };
        Shown += async delegate { SyncHub041(); await InitDash041(); };
        FormClosed += delegate
        {
            try { _hubStatusTimer041.Stop(); _hubDashTimer041.Stop(); _hubStatusTimer041.Dispose(); _hubDashTimer041.Dispose(); } catch { }
            try { _truckOverlay041?.Close(); _videoOverlay041?.Close(); } catch { }
            try { _hubRadio041?.Dispose(); _hubDash041?.Dispose(); _hubHttp041.Dispose(); } catch { }
        };
        ResumeLayout(true);
    }

    private Control HubHeader041()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 29) };
        p.Controls.Add(new Label { Text = "GAT", Left = 22, Top = 10, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Black", 25, FontStyle.Bold | FontStyle.Italic) });
        p.Controls.Add(new Label { Text = "GAT TELEMETRIA BETA", Left = 118, Top = 13, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold) });
        p.Controls.Add(new Label { Text = "Central principal do ecossistema GAT LOG ETS2 • tudo em um só lugar", Left = 121, Top = 47, AutoSize = true, ForeColor = Color.FromArgb(145, 170, 199) });
        return p;
    }

    private Control HubNav041()
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 6, 0, 4), BackColor = Color.FromArgb(5, 18, 33) };
        Nav041(p, "home", "INÍCIO", 105); Nav041(p, "dash", "GAT DASH", 120); Nav041(p, "radio", "RÁDIO GAT", 125); Nav041(p, "gps", "GPS", 80); Nav041(p, "server", "COMBOIO / SERVIDOR", 175); Nav041(p, "updates", "ATUALIZAÇÕES", 135); Nav041(p, "settings", "CONFIGURAÇÕES", 145);
        return p;
    }

    private void Nav041(FlowLayoutPanel p, string key, string text, int width)
    {
        var b = HubButton041(text, width); b.Margin = new Padding(0, 0, 7, 0); b.Click += delegate { ShowHubPage041(key); }; _hubNav041[key] = b; p.Controls.Add(b);
    }

    private Button HubButton041(string text, int width)
    {
        var b = new Button { Text = text, Width = width, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(8, 29, 50), ForeColor = Color.FromArgb(210, 225, 242), Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(38, 88, 135); return b;
    }

    private void AddHubPage041(string key, Panel p) { p.Dock = DockStyle.Fill; p.Visible = false; _hubPages041[key] = p; _hubBody041.Controls.Add(p); }
    private void ShowHubPage041(string key)
    {
        foreach (var x in _hubPages041) x.Value.Visible = x.Key == key;
        foreach (var x in _hubNav041) { bool on = x.Key == key; x.Value.BackColor = on ? Color.FromArgb(14, 74, 133) : Color.FromArgb(8, 29, 50); x.Value.ForeColor = on ? Color.White : Color.FromArgb(210, 225, 242); }
        if (key == "radio") EnsureRadio041();
        if (_hubPages041.ContainsKey(key)) _hubPages041[key].BringToFront();
    }

    private Panel Home041()
    {
        var p = Page041(); p.Controls.Add(Head041("INÍCIO", "Status essencial e acesso aos módulos GAT."));
        var grid = new TableLayoutPanel { Left = 0, Top = 58, Height = 180, Width = p.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _hAccount041 = StatusCard041(grid, 0, "CONTA GAT", "Conta não conectada"); _hEts041 = StatusCard041(grid, 1, "ETS2 / TELEMETRIA", "Aguardando TruckSim GPS"); _hCentral041 = StatusCard041(grid, 2, "CENTRAL GAT", "Aguardando conexão"); _hServer041 = StatusCard041(grid, 3, "COMBOIO / SERVIDOR", "Opcional"); p.Controls.Add(grid);
        var mods = new FlowLayoutPanel { Left = 0, Top = 265, Width = p.Width, Height = 155, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, WrapContents = false, AutoScroll = true };
        Module041(mods, "GAT DASH", "Dashboard e overlays", "dash"); Module041(mods, "RÁDIO GAT", "Canal GAT, Meu Vídeo e Canal Web", "radio"); Module041(mods, "GPS", "Navegação e alertas", "gps"); Module041(mods, "COMBOIO", "Servidor e sala", "server"); Module041(mods, "CONFIGURAÇÕES", "Conta e preferências", "settings"); p.Controls.Add(mods);
        p.Controls.Add(new Label { Left = 0, Top = 445, Width = p.Width, Height = 85, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Text = "TUDO EM UM SÓ LUGAR\r\nNovos recursos passam a abrir como páginas dentro do GAT Telemetria, sem precisar criar outro aplicativo.", ForeColor = Color.FromArgb(122, 164, 210), Font = new Font("Segoe UI Semibold", 11) });
        return p;
    }

    private Label StatusCard041(TableLayoutPanel grid, int col, string title, string text)
    {
        var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(15), BackColor = Color.FromArgb(6, 23, 41) };
        card.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 35, ForeColor = Color.FromArgb(89, 166, 255), Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold) });
        var v = new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI Semibold", 10), TextAlign = ContentAlignment.MiddleLeft }; card.Controls.Add(v); grid.Controls.Add(card, col, 0); return v;
    }

    private void Module041(FlowLayoutPanel p, string title, string sub, string page)
    {
        var b = HubButton041(title + "\r\n" + sub, 205); b.Height = 105; b.TextAlign = ContentAlignment.MiddleLeft; b.Padding = new Padding(14, 0, 4, 0); b.Click += delegate { ShowHubPage041(page); }; p.Controls.Add(b);
    }

    private Panel Dash041()
    {
        var p = Page041(); p.Controls.Add(Head041("GAT DASH", "O dashboard que já usamos, agora dentro do GAT Telemetria."));
        var tools = new Panel { Left = 0, Top = 55, Height = 55, Width = p.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(5, 20, 36) };
        var video = HubButton041("SOBREPOR VÍDEO", 150); video.Left = 10; video.Top = 9; video.Click += delegate { OpenVideoOverlay041(); }; tools.Controls.Add(video);
        var truck = HubButton041("SOBREPOR CAMINHÃO", 180); truck.Left = 170; truck.Top = 9; truck.Click += delegate { OpenTruckOverlay041(); }; tools.Controls.Add(truck);
        var close = HubButton041("FECHAR OVERLAYS", 150); close.Left = 360; close.Top = 9; close.Click += delegate { CloseOverlays041(); }; tools.Controls.Add(close);
        tools.Controls.Add(new Label { Text = "Transparência", Left = 530, Top = 19, Width = 95, ForeColor = Color.FromArgb(155, 179, 207) });
        _overlayOpacity041 = new TrackBar { Left = 625, Top = 6, Width = 180, Minimum = 55, Maximum = 100, Value = 94, TickFrequency = 5 }; _overlayOpacity041.Scroll += delegate { ApplyOverlayOpacity041(); }; tools.Controls.Add(_overlayOpacity041);
        tools.Resize += delegate { _overlayOpacity041.Width = Math.Max(120, tools.ClientSize.Width - 640); };
        p.Controls.Add(tools);
        _hubDash041 = new WebView2 { Left = 0, Top = 120, Width = p.Width, Height = Math.Max(300, p.Height - 120), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(2, 10, 20) };
        p.Controls.Add(_hubDash041); return p;
    }

    private Panel Radio041() { var p = Page041(); p.Controls.Add(Head041("RÁDIO GAT", "A mesma Rádio/TV GAT usada pelo DASH e pelo overlay de vídeo.")); _hubRadioHost041 = new Panel { Left = 0, Top = 55, Width = p.Width, Height = Math.Max(350, p.Height - 55), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(4, 13, 25) }; p.Controls.Add(_hubRadioHost041); return p; }
    private Panel Server041() { var p = Page041(); p.Controls.Add(Head041("COMBOIO / SERVIDOR", "Servidor continua opcional e usa a configuração que já existe.")); if (_hubServerCard041 != null) { _hubServerCard041.Left = 0; _hubServerCard041.Top = 65; _hubServerCard041.Width = p.Width; _hubServerCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(_hubServerCard041); } return p; }
    private Panel Settings041() { var p = Page041(); p.Controls.Add(Head041("CONFIGURAÇÕES", "Conta GAT e preferências. Novas opções de voz entram aqui depois.")); if (_hubAccountCard041 != null) { _hubAccountCard041.Left = 0; _hubAccountCard041.Top = 65; _hubAccountCard041.Width = p.Width; _hubAccountCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(_hubAccountCard041); } p.Controls.Add(new Label { Text = "VOZ E ALERTAS • em breve\r\nVamos adicionar escolha de voz, velocidade e volume numa próxima etapa.", Left = 0, Top = 260, Width = 520, Height = 55, ForeColor = Color.FromArgb(135, 166, 202) }); return p; }
    private Panel Updates041() { var p = Simple041("ATUALIZAÇÕES", "Verifique novas versões do GAT Telemetria sem sair do aplicativo."); var b = HubButton041("VERIFICAR ATUALIZAÇÃO", 230); b.Left = 0; b.Top = 100; b.Click += async delegate { await UpdateClickedAsync(); }; p.Controls.Add(b); return p; }
    private Panel Simple041(string title, string sub) { var p = Page041(); p.Controls.Add(Head041(title, sub)); return p; }
    private Panel Page041() { return new Panel { BackColor = Color.FromArgb(3, 11, 22) }; }
    private Label Head041(string title, string sub) { return new Label { Text = title + "\r\n" + sub, Left = 0, Top = 0, Width = 900, Height = 52, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold) }; }

    private void SyncHub041()
    {
        if (_hAccount041 != null) _hAccount041.Text = AccountReady ? (_accountUser + "\r\nConta conectada") : "Conta não conectada\r\nAbra Configurações";
        if (_hEts041 != null) _hEts041.Text = Safe041(lblTruck, "TruckSim GPS: aguardando");
        if (_hCentral041 != null) _hCentral041.Text = Safe041(lblTelemetry, "Central GAT: aguardando");
        if (_hServer041 != null) _hServer041.Text = Safe041(lblServer, "Servidor: opcional") + "\r\n" + Safe041(lblRoom, "Sala: -");
    }
    private static string Safe041(Label l, string fallback) { try { return l != null && !string.IsNullOrWhiteSpace(l.Text) ? l.Text : fallback; } catch { return fallback; } }

    private void EnsureRadio041()
    {
        if (_hubRadioHost041 == null || (_hubRadio041 != null && !_hubRadio041.IsDisposed)) return;
        try { _hubRadio041 = new RadioForm { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill, TopMost = false, ShowInTaskbar = false }; _hubRadioHost041.Controls.Clear(); _hubRadioHost041.Controls.Add(_hubRadio041); _hubRadio041.Show(); }
        catch (Exception ex) { _hubRadioHost041.Controls.Clear(); _hubRadioHost041.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Rádio GAT indisponível.\r\n" + ex.Message, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.OrangeRed }); }
    }

    private async Task InitDash041()
    {
        if (_hubDashReady041 || _hubDash041 == null) return;
        try
        {
            string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gatdash-www");
            if (!File.Exists(Path.Combine(www, "index.html"))) { _hubDash041.Visible = false; return; }
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "DashWebView2-1.0.41"); Directory.CreateDirectory(data);
            var env = await CoreWebView2Environment.CreateAsync(null, data); await _hubDash041.EnsureCoreWebView2Async(env);
            _hubDash041.CoreWebView2.Settings.AreDevToolsEnabled = false; _hubDash041.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _hubDash041.CoreWebView2.SetVirtualHostNameToFolderMapping("gatdash.local", www, CoreWebView2HostResourceAccessKind.Allow);
            _hubDash041.CoreWebView2.WebMessageReceived += DashMessage041; _hubDash041.Source = new Uri("https://gatdash.local/index.html?hub=1041"); _hubDashReady041 = true; _hubDashTimer041.Start();
        }
        catch { _hubDash041.Visible = false; }
    }

    private async void DashMessage041(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            JObject m = JObject.Parse(e.WebMessageAsJson); string type = Convert.ToString(m["type"]) ?? "";
            if (type == "ready") { await DashJs041("window.gatDashNativeReady('windows',{host:'',voice:true,tolerance:3})"); await DashSession041(); }
            else if (type == "login") await DashSession041();
            else if (type == "mediaRefresh") await DashMedia041();
            else if (type == "mediaMode") { SaveMediaMode041(Convert.ToString(m["mode"])); await DashMedia041(); }
            else if (type == "speak") { string t = (Convert.ToString(m["text"]) ?? "").Trim(); if (t.Length > 0 && t.Length < 240) { try { if (_voice == null) _voice = new System.Speech.Synthesis.SpeechSynthesizer(); _voice.SpeakAsyncCancelAll(); _voice.SpeakAsync(t); } catch { } } }
            else if (type == "setSettings") await DashJs041("window.gatDashSettings({host:'',voice:true,tolerance:3})");
        }
        catch { }
    }

    private async Task DashSession041()
    {
        if (!_hubDashReady041) return;
        if (!AccountReady) { await DashJs041("window.gatDashLoginTransportError('Entre primeiro na Conta GAT em Configurações.')"); return; }
        var r = new JObject { ["ok"] = true, ["user"] = _accountUser, ["role"] = "driver" }; await DashJs041("window.gatDashLoginResult(" + r.ToString(Formatting.None) + ")"); await DashMedia041();
    }

    private async Task PollDash041()
    {
        if (!_hubDashReady041 || _hubDashBusy041 || _hubDash041?.CoreWebView2 == null) return; _hubDashBusy041 = true;
        try { string json = await _hubHttp041.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry"); await DashJs041("window.gatDashPushTelemetry(" + JsonConvert.SerializeObject(json) + ")"); }
        catch (Exception ex) { await DashJs041("window.gatDashTelemetryError(" + JsonConvert.SerializeObject(ex.Message.Length > 80 ? ex.Message.Substring(0, 80) : ex.Message) + ")"); }
        finally { _hubDashBusy041 = false; }
    }
    private async Task DashMedia041() { try { string j = await _hubHttp041.GetStringAsync("http://127.0.0.1:31378/api/gat/media?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); await DashJs041("window.gatDashPushMedia(" + JsonConvert.SerializeObject(j) + ")"); } catch { await DashJs041("window.gatDashMediaError('Rádio GAT indisponível')"); } }
    private async Task DashJs041(string js) { try { if (_hubDashReady041 && _hubDash041?.CoreWebView2 != null) await _hubDash041.CoreWebView2.ExecuteScriptAsync(js); } catch { } }
    private static void SaveMediaMode041(string mode) { try { mode = (mode ?? "").Trim().ToLowerInvariant(); if (mode == "gat" || mode == "mine" || mode == "web") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode); } catch { } }

    private void OpenTruckOverlay041() { if (_truckOverlay041 == null || _truckOverlay041.IsDisposed) { _truckOverlay041 = new TruckOverlay041(); _truckOverlay041.FormClosed += delegate { _truckOverlay041 = null; }; } ApplyOverlayOpacity041(); if (!_truckOverlay041.Visible) _truckOverlay041.Show(this); _truckOverlay041.BringToFront(); }
    private void OpenVideoOverlay041() { if (_videoOverlay041 == null || _videoOverlay041.IsDisposed) { _videoOverlay041 = new VideoOverlay041(); _videoOverlay041.FormClosed += delegate { _videoOverlay041 = null; }; } ApplyOverlayOpacity041(); if (!_videoOverlay041.Visible) _videoOverlay041.Show(this); _videoOverlay041.BringToFront(); }
    private void CloseOverlays041() { try { _truckOverlay041?.Close(); } catch { } try { _videoOverlay041?.Close(); } catch { } }
    private void ApplyOverlayOpacity041() { double o = (_overlayOpacity041 == null ? 94 : _overlayOpacity041.Value) / 100.0; try { if (_truckOverlay041 != null && !_truckOverlay041.IsDisposed) _truckOverlay041.Opacity = o; } catch { } try { if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed) _videoOverlay041.Opacity = o; } catch { } }
}
