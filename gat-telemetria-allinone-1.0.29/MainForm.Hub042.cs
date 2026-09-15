using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private sealed class DriverAvatar042 : Control
    {
        public string Initials { get; set; } = "GAT";
        public DriverAvatar042()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int d = Math.Max(20, Math.Min(Width, Height) - 6);
            var r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
            using (var b = new SolidBrush(Color.FromArgb(12, 91, 164))) e.Graphics.FillEllipse(b, r);
            using (var p = new Pen(Color.FromArgb(36, 167, 255), 2f)) e.Graphics.DrawEllipse(p, r);
            string text = string.IsNullOrWhiteSpace(Initials) ? "GAT" : Initials.Trim().ToUpperInvariant();
            if (text.Length > 2) text = text.Substring(0, 2);
            using (var f = new Font("Segoe UI Semibold", 22f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            {
                var s = e.Graphics.MeasureString(text, f);
                e.Graphics.DrawString(text, f, b, r.Left + (r.Width - s.Width) / 2f, r.Top + (r.Height - s.Height) / 2f);
            }
        }
    }

    private Label _profileName042;
    private Label _profileMeta042;
    private Label _trip042;
    private Label _radio042;
    private Label _connect042;
    private DriverAvatar042 _avatar042;
    private string _hubCurrentPage042 = "home";
    private Size _lastDashSize042 = Size.Empty;
    private bool _hub042Applied;

    private void ApplyHub042()
    {
        if (_hub042Applied) return;
        _hub042Applied = true;
        Text = "GAT Telemetria BETA 1.0.42";

        RebuildHome042();
        ImproveDash042();
        WireMediaRouting042();

        _hubStatusTimer041.Tick += delegate
        {
            SyncHome042();
            if (_hubCurrentPage042 == "dash" && _hubDashReady041 && _hubDash041 != null && _hubDash041.ClientSize != _lastDashSize042)
                _ = FitDash042();
            try { _hubRadio041?.HubSyncMode042(); } catch { }
        };

        Shown += async delegate
        {
            SyncHome042();
            await Task.Delay(700);
            await SetDashMediaActive042(false);
            await FitDash042();
        };
    }

    private Panel Card042(string title)
    {
        var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6), Padding = new Padding(16), BackColor = Color.FromArgb(6, 23, 41) };
        p.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(26, 91, 145), 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(1, p.Width - 1), Math.Max(1, p.Height - 1));
        };
        p.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = Color.FromArgb(61, 174, 255),
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });
        return p;
    }

    private void RebuildHome042()
    {
        Panel page;
        if (!_hubPages041.TryGetValue("home", out page) || page == null) return;
        page.Controls.Clear();
        page.Padding = Padding.Empty;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(3, 11, 22)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "INÍCIO\r\nSeu perfil, sua viagem e o ecossistema GAT LOG em um só lugar.",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        root.Controls.Add(top, 0, 1);

        var profile = Card042("PERFIL DO MOTORISTA");
        _avatar042 = new DriverAvatar042 { Left = 16, Top = 54, Width = 86, Height = 86 };
        _profileName042 = new Label { Left = 118, Top = 57, Width = 220, Height = 32, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), Text = "Motorista GAT" };
        _profileMeta042 = new Label { Left = 118, Top = 94, Width = 230, Height = 88, ForeColor = Color.FromArgb(159, 187, 214), Font = new Font("Segoe UI", 9.5f), Text = "Conta GAT aguardando..." };
        profile.Controls.Add(_avatar042);
        profile.Controls.Add(_profileName042);
        profile.Controls.Add(_profileMeta042);
        profile.Controls.Add(new Label { Left = 16, Top = 157, Width = 92, Height = 27, Text = "GAT LOG", TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(10, 76, 130), ForeColor = Color.FromArgb(177, 224, 255), Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold) });
        top.Controls.Add(profile, 0, 0);

        var trip = Card042("ETS2 / VIAGEM ATUAL");
        _trip042 = new Label { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI Semibold", 10f), Text = "Aguardando telemetria do ETS2..." };
        trip.Controls.Add(_trip042);
        top.Controls.Add(trip, 1, 0);

        var radio = Card042("RÁDIO / TV GAT • TOCANDO AGORA");
        _radio042 = new Label { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI Semibold", 10f), Text = "Canal GAT\r\nAguardando mídia..." };
        radio.Controls.Add(_radio042);
        top.Controls.Add(radio, 2, 0);

        var banner = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6, 14, 6, 6), BackColor = Color.FromArgb(4, 18, 33) };
        banner.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(23, 92, 148), 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(1, banner.Width - 1), Math.Max(1, banner.Height - 1));
        };
        banner.Controls.Add(new Label
        {
            Text = "GAT LOG ETS2\r\nCONEXÃO QUE MOVE DISTÂNCIAS",
            Left = 28,
            Top = 36,
            Width = 500,
            Height = 86,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold)
        });
        banner.Controls.Add(new Label
        {
            Text = "Telemetria • comunidade • rádio • dashboard • GPS • comboios\r\nTudo integrado no mesmo GAT Telemetria.",
            Left = 30,
            Top = 132,
            Width = 540,
            Height = 50,
            ForeColor = Color.FromArgb(112, 171, 220),
            Font = new Font("Segoe UI", 10f)
        });

        var truck = new TruckOutline { Width = 330, Height = 130, Top = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var gatTruck = new Label { Text = "GAT LOG", Width = 150, Height = 34, Top = 150, TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = Color.FromArgb(70, 182, 255), Font = new Font("Segoe UI Black", 16f, FontStyle.Bold | FontStyle.Italic) };
        _connect042 = new Label { Width = 300, Height = 62, Top = 52, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = Color.FromArgb(143, 180, 214), Font = new Font("Segoe UI Semibold", 9.5f) };
        banner.Controls.Add(truck);
        banner.Controls.Add(gatTruck);
        banner.Controls.Add(_connect042);
        banner.Resize += delegate
        {
            truck.Left = Math.Max(560, banner.ClientSize.Width - 360);
            gatTruck.Left = Math.Max(640, banner.ClientSize.Width - 270);
            _connect042.Left = Math.Max(500, banner.ClientSize.Width - 330);
        };
        root.Controls.Add(banner, 0, 2);
    }

    private void SyncHome042()
    {
        try
        {
            string user = !string.IsNullOrWhiteSpace(_accountUser) ? _accountUser : "Motorista GAT";
            string driver = !string.IsNullOrWhiteSpace(_driver) ? _driver : user;
            if (_profileName042 != null) _profileName042.Text = driver;
            if (_avatar042 != null) { _avatar042.Initials = Initials042(driver); _avatar042.Invalidate(); }
            if (_profileMeta042 != null)
                _profileMeta042.Text = AccountReady
                    ? "@" + user + "\r\nConta GAT conectada\r\nPC vinculado a esta instalação"
                    : "Conta GAT não conectada\r\nAbra Configurações para entrar.";

            if (_trip042 != null)
            {
                _trip042.Text = Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" +
                                Safe041(lblCargo, "Carga: Sem carga") + "\r\n" +
                                Safe041(lblRoute, "Rota: -") + "\r\n" +
                                Safe041(lblDistance, "Restante: -") + "\r\n" +
                                Safe041(lblSpeed, "Velocidade: 0 km/h");
            }

            if (_radio042 != null)
            {
                string mode = ReadMediaMode042();
                string label = mode == "mine" ? "MEU VÍDEO" : mode == "web" ? "CANAL WEB" : "CANAL GAT";
                string now = "";
                try { if (_hubRadio041 != null && !_hubRadio041.IsDisposed) now = _hubRadio041.HubNowPlaying042; } catch { }
                if (string.IsNullOrWhiteSpace(now) || now.EndsWith("—")) now = "Fonte selecionada: " + label;
                _radio042.Text = label + "\r\n\r\n" + now + "\r\n\r\nSom único: ao mudar de página, o player anterior é pausado.";
            }

            if (_connect042 != null)
                _connect042.Text = Safe041(lblTelemetry, "Central GAT: aguardando") + "\r\n" + Safe041(lblServer, "Servidor: opcional") + "\r\nCliente 1.0.42 TESTE";
        }
        catch { }
    }

    private static string Initials042(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "GAT";
        var p = text.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 2) return (p[0].Substring(0, 1) + p[p.Length - 1].Substring(0, 1)).ToUpperInvariant();
        string x = p.Length == 0 ? text.Trim() : p[0];
        return x.Length >= 2 ? x.Substring(0, 2).ToUpperInvariant() : x.ToUpperInvariant();
    }

    private static string ReadMediaMode042()
    {
        try
        {
            string f = Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
            if (!File.Exists(f)) return "gat";
            string m = (File.ReadAllText(f) ?? "").Trim().ToLowerInvariant();
            return m == "mine" || m == "web" ? m : "gat";
        }
        catch { return "gat"; }
    }

    private void ImproveDash042()
    {
        Panel page;
        if (!_hubPages041.TryGetValue("dash", out page) || page == null || _hubDash041 == null) return;
        foreach (Control c in page.Controls)
        {
            var tools = c as Panel;
            if (tools != null && tools.Top >= 50 && tools.Top <= 70)
            {
                tools.Top = 50;
                tools.Height = 46;
                foreach (Control child in tools.Controls)
                    if (child is Button) { child.Top = 5; child.Height = 34; }
            }
        }
        _hubDash041.Top = 102;
        _hubDash041.Height = Math.Max(280, page.ClientSize.Height - 102);
        page.Resize += delegate
        {
            _hubDash041.Height = Math.Max(280, page.ClientSize.Height - 102);
            if (_hubCurrentPage042 == "dash") _ = FitDash042();
        };
    }

    private async Task FitDash042()
    {
        if (!_hubDashReady041 || _hubDash041 == null || _hubDash041.CoreWebView2 == null) return;
        int h = Math.Max(320, _hubDash041.ClientSize.Height);
        double zoom = Math.Max(0.66, Math.Min(1.0, h / 700.0));
        double expanded = 100.0 / zoom;
        string z = zoom.ToString("0.000", CultureInfo.InvariantCulture);
        string e = expanded.ToString("0.0", CultureInfo.InvariantCulture);
        await DashJs041("document.documentElement.style.overflow='hidden';document.body.style.zoom='" + z + "';document.body.style.width='" + e + "%';document.body.style.height='" + e + "%';");
        _lastDashSize042 = _hubDash041.ClientSize;
    }

    private void WireMediaRouting042()
    {
        foreach (var pair in _hubNav041.ToList())
        {
            string key = pair.Key;
            pair.Value.Click += async delegate
            {
                _hubCurrentPage042 = key;
                await RouteMedia042(key);
                if (key == "dash") await FitDash042();
            };
        }

        Panel dash;
        if (_hubPages041.TryGetValue("dash", out dash) && dash != null)
        {
            foreach (Control container in dash.Controls)
            {
                foreach (Button b in container.Controls.OfType<Button>())
                {
                    if (!string.Equals(b.Text, "SOBREPOR VÍDEO", StringComparison.OrdinalIgnoreCase)) continue;
                    b.Click += async delegate
                    {
                        try { _hubRadio041?.HubPause042(); } catch { }
                        await SetDashMediaActive042(false);
                        if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed)
                        {
                            _videoOverlay041.FormClosed += async delegate
                            {
                                if (_hubCurrentPage042 == "dash") await SetDashMediaActive042(true);
                            };
                        }
                    };
                }
            }
        }
    }

    private async Task RouteMedia042(string key)
    {
        if (key == "dash")
        {
            try { _hubRadio041?.HubPause042(); } catch { }
            await SetDashMediaActive042(true);
            return;
        }
        if (key == "radio")
        {
            await SetDashMediaActive042(false);
            try { _hubRadio041?.HubSyncMode042(); } catch { }
            return;
        }
        try { _hubRadio041?.HubPause042(); } catch { }
        await SetDashMediaActive042(false);
    }

    private async Task SetDashMediaActive042(bool active)
    {
        if (!_hubDashReady041) return;
        await DashJs041("if(window.gatDashSetMediaActive042)window.gatDashSetMediaActive042(" + (active ? "true" : "false") + ");");
    }
}
