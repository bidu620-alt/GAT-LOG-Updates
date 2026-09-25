using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private sealed class DriverAvatar044 : Control
    {
        public string Initials { get; set; } = "GAT";
        public DriverAvatar044()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Max(24, Math.Min(Width, Height) - 8);
            var r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
            using (var halo = new SolidBrush(Color.FromArgb(38, 50, 174, 255)))
                e.Graphics.FillEllipse(halo, new Rectangle(r.X - 5, r.Y - 5, r.Width + 10, r.Height + 10));
            using (var fill = new LinearGradientBrush(r, Color.FromArgb(5, 41, 74), Color.FromArgb(5, 18, 34), 90f))
                e.Graphics.FillEllipse(fill, r);
            using (var p = new Pen(Color.FromArgb(47, 174, 255), 2f))
                e.Graphics.DrawEllipse(p, r);
            string text = string.IsNullOrWhiteSpace(Initials) ? "GAT" : Initials.Trim().ToUpperInvariant();
            if (text.Length > 2) text = text.Substring(0, 2);
            using (var f = new Font("Segoe UI Semibold", 24f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            {
                var s = e.Graphics.MeasureString(text, f);
                e.Graphics.DrawString(text, f, b, r.Left + (r.Width - s.Width) / 2f, r.Top + (r.Height - s.Height) / 2f);
            }
        }
    }

    private sealed class GlassCard044 : Panel
    {
        public string Caption { get; set; } = "";
        public GlassCard044()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(7, 24, 43);
            Padding = new Padding(18, 50, 18, 16);
            Margin = new Padding(7);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));
            using (var path = Round044(rect, 14))
            using (var border = new Pen(Color.FromArgb(34, 125, 205), 1.2f))
                e.Graphics.DrawPath(border, path);
            using (var line = new Pen(Color.FromArgb(30, 83, 132), 1f))
                e.Graphics.DrawLine(line, 18, 42, Math.Max(19, Width - 18), 42);
            using (var f = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(225, 239, 255)))
                e.Graphics.DrawString(Caption, f, b, 18f, 15f);
        }
    }

    private sealed class HeroPanel044 : Panel
    {
        public Image HeroImage { get; set; }
        public HeroPanel044()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(2, 11, 22);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var bounds = ClientRectangle;
            if (HeroImage != null && bounds.Width > 0 && bounds.Height > 0)
            {
                float scale = Math.Max((float)bounds.Width / HeroImage.Width, (float)bounds.Height / HeroImage.Height);
                int w = (int)Math.Ceiling(HeroImage.Width * scale);
                int h = (int)Math.Ceiling(HeroImage.Height * scale);
                int x = (bounds.Width - w) / 2;
                int y = (bounds.Height - h) / 2;
                e.Graphics.DrawImage(HeroImage, new Rectangle(x, y, w, h));
            }
            using (var dark = new LinearGradientBrush(bounds,
                Color.FromArgb(205, 1, 11, 23), Color.FromArgb(35, 1, 11, 23), 0f))
                e.Graphics.FillRectangle(dark, bounds);
            using (var bottom = new LinearGradientBrush(bounds,
                Color.FromArgb(0, 1, 9, 18), Color.FromArgb(185, 1, 9, 18), 90f))
                e.Graphics.FillRectangle(bottom, bounds);

            float sx = 30f, sy = Math.Max(25f, Height * 0.18f);
            using (var f1 = new Font("Segoe UI Black", Math.Max(28f, Math.Min(54f, Width / 22f)), FontStyle.Bold | FontStyle.Italic))
            using (var white = new SolidBrush(Color.White))
            using (var blue = new SolidBrush(Color.FromArgb(24, 154, 255)))
            {
                string a = "GAT";
                e.Graphics.DrawString(a, f1, white, sx, sy);
                var s = e.Graphics.MeasureString(a, f1);
                e.Graphics.DrawString("LOG", f1, blue, sx + s.Width - 5, sy);
            }
            using (var f2 = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(215, 232, 249)))
                e.Graphics.DrawString("E S T R A D A S   Q U E   N O S   U N E M", f2, b, sx + 4, sy + 66);

            using (var f3 = new Font("Segoe UI", 9.5f))
            using (var b = new SolidBrush(Color.FromArgb(197, 219, 240)))
            {
                e.Graphics.DrawString("● COMUNIDADE ATIVA", f3, b, sx + 4, sy + 108);
                e.Graphics.DrawString("★ VIAGENS REAIS • AMIZADES VERDADEIRAS", f3, b, sx + 4, sy + 136);
                e.Graphics.DrawString("▣ SEMPRE EM FRENTE", f3, b, sx + 4, sy + 164);
            }

            using (var f4 = new Font("Segoe UI Semibold", 10f, FontStyle.Italic))
            using (var b = new SolidBrush(Color.FromArgb(235, 243, 252)))
                e.Graphics.DrawString("“Na estrada, cada quilômetro conta uma história.”", f4, b, 32f, Math.Max(40, Height - 54));
        }

        private static GraphicsPath Round044(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            var arc = new Rectangle(bounds.X, bounds.Y, d, d);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - d; path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - d; path.AddArc(arc, 0, 90);
            arc.X = bounds.Left; path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private Label _homeProfileName044;
    private Label _homeProfileMeta044;
    private Label _homeTrip044;
    private Label _homeRadio044;
    private Label _homeSystem044;
    private DriverAvatar044 _homeAvatar044;
    private Image _homeHeroImage044;
    private bool _hub044Applied;

    private void ApplyHub044()
    {
        if (_hub044Applied) return;
        _hub044Applied = true;
        Text = "GAT Telemetria BETA 1.0.50";
        MinimumSize = new Size(1100, 720);
        if (Width < 1240 || Height < 820) Size = new Size(1280, 840);

        StyleHub044();
        BuildHome044();
        ReplaceTextRecursive044(this, "Cliente 1.0.43 TESTE", "Cliente 1.0.50 TESTE");
        ReplaceTextRecursive044(this, "Central principal do ecossistema GAT LOG ETS2 • tudo em um só lugar",
            "Conectando motoristas, estradas e amizades • GAT LOG ETS2");

        _hubStatusTimer041.Tick += delegate { SyncHome044(); };
        Shown += delegate { SyncHome044(); };
        FormClosed += delegate
        {
            try { _homeHeroImage044?.Dispose(); } catch { }
        };
    }

    private void StyleHub044()
    {
        foreach (var pair in _hubNav041)
        {
            Button b = pair.Value;
            if (b == null) continue;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 64, 108);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(15, 91, 154);
            b.Height = 38;
            b.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            b.ForeColor = Color.FromArgb(218, 233, 248);
            b.Cursor = Cursors.Hand;
        }
    }

    private void BuildHome044()
    {
        Panel page;
        if (!_hubPages041.TryGetValue("home", out page) || page == null) return;

        page.SuspendLayout();
        page.Controls.Clear();
        page.Padding = Padding.Empty;
        page.BackColor = Color.FromArgb(2, 10, 20);

        try
        {
            string heroPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gat-home-hero-1.0.44.jpg");
            if (File.Exists(heroPath))
            {
                using (var tmp = Image.FromFile(heroPath))
                    _homeHeroImage044 = new Bitmap(tmp);
            }
        }
        catch { _homeHeroImage044 = null; }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = page.BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 244));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = page.BackColor
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        root.Controls.Add(top, 0, 0);

        var profile = new GlassCard044 { Caption = "PERFIL DO MOTORISTA", Dock = DockStyle.Fill };
        _homeAvatar044 = new DriverAvatar044 { Left = 18, Top = 61, Width = 92, Height = 92 };
        _homeProfileName044 = new Label
        {
            Left = 126, Top = 62, Width = 260, Height = 30,
            Text = "Motorista GAT", ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold)
        };
        _homeProfileMeta044 = new Label
        {
            Left = 126, Top = 98, Width = 270, Height = 105,
            Text = "Conta GAT aguardando...", ForeColor = Color.FromArgb(161, 193, 222),
            Font = new Font("Segoe UI", 9.5f)
        };
        var badge = new Label
        {
            Left = 18, Top = 165, Width = 92, Height = 28,
            Text = "MOTORISTA GAT", TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(8, 68, 116), ForeColor = Color.FromArgb(108, 204, 255),
            Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold)
        };
        profile.Controls.Add(_homeAvatar044);
        profile.Controls.Add(_homeProfileName044);
        profile.Controls.Add(_homeProfileMeta044);
        profile.Controls.Add(badge);
        top.Controls.Add(profile, 0, 0);

        var trip = new GlassCard044 { Caption = "VIAGEM ATUAL", Dock = DockStyle.Fill };
        _homeTrip044 = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Aguardando telemetria do ETS2...",
            ForeColor = Color.FromArgb(225, 236, 248),
            Font = new Font("Segoe UI Semibold", 10.2f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };
        trip.Controls.Add(_homeTrip044);
        top.Controls.Add(trip, 1, 0);

        var radio = new GlassCard044 { Caption = "RÁDIO GAT • TOCANDO AGORA", Dock = DockStyle.Fill };
        _homeRadio044 = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Canal GAT\r\nAguardando mídia...",
            ForeColor = Color.FromArgb(225, 236, 248),
            Font = new Font("Segoe UI Semibold", 10.2f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };
        radio.Controls.Add(_homeRadio044);
        top.Controls.Add(radio, 2, 0);

        var hero = new HeroPanel044
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(7, 4, 7, 7),
            HeroImage = _homeHeroImage044
        };
        root.Controls.Add(hero, 0, 1);

        _homeSystem044 = new Label
        {
            Width = 330, Height = 58, Top = 18,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(154, 207, 244),
            BackColor = Color.FromArgb(135, 2, 14, 26),
            Font = new Font("Segoe UI Semibold", 9f)
        };
        hero.Controls.Add(_homeSystem044);

        var video = OverlayAction044("SOBREPOSIÇÃO DE VÍDEO\r\nVídeo flutuante sem barra branca", 222, 72);
        var truck = OverlayAction044("SOBREPOSIÇÃO DO CAMINHÃO\r\nPainel compacto e redimensionável", 244, 72);
        video.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        truck.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        video.Click += delegate { OpenVideoOverlay041(); };
        truck.Click += delegate { OpenTruckOverlay041(); };
        hero.Controls.Add(video);
        hero.Controls.Add(truck);

        hero.Resize += delegate
        {
            _homeSystem044.Left = Math.Max(20, hero.ClientSize.Width - _homeSystem044.Width - 20);
            truck.Left = Math.Max(20, hero.ClientSize.Width - truck.Width - 20);
            truck.Top = Math.Max(20, hero.ClientSize.Height - truck.Height - 22);
            video.Left = Math.Max(20, truck.Left - video.Width - 12);
            video.Top = truck.Top;
        };

        page.ResumeLayout(true);
        SyncHome044();
    }

    private Button OverlayAction044(string text, int width, int height)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = height,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 5, 27, 47),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(35, 135, 215);
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 8, 45, 76);
        return b;
    }

    private void SyncHome044()
    {
        try
        {
            string user = !string.IsNullOrWhiteSpace(_accountUser) ? _accountUser : "Motorista GAT";
            string driver = !string.IsNullOrWhiteSpace(_driver) ? _driver : user;
            if (_homeProfileName044 != null) _homeProfileName044.Text = driver;
            if (_homeAvatar044 != null)
            {
                _homeAvatar044.Initials = Initials044(driver);
                _homeAvatar044.Invalidate();
            }
            if (_homeProfileMeta044 != null)
            {
                _homeProfileMeta044.Text = AccountReady
                    ? "● Conta ativa\r\n● PC vinculado\r\n@" + user + "\r\nGAT LOG ETS2"
                    : "○ Conta não conectada\r\n○ PC aguardando vínculo\r\nAbra Configurações para entrar.";
                _homeProfileMeta044.ForeColor = AccountReady ? Color.FromArgb(124, 231, 154) : Color.FromArgb(198, 178, 132);
            }

            if (_homeTrip044 != null)
            {
                _homeTrip044.Text =
                    Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" +
                    Safe041(lblCargo, "Carga: Sem carga") + "\r\n" +
                    Safe041(lblRoute, "Rota: -") + "\r\n" +
                    Safe041(lblDistance, "Distância restante: -") + "\r\n" +
                    Safe041(lblSpeed, "Velocidade: 0 km/h");
            }

            if (_homeRadio044 != null)
            {
                string mode = ReadMediaMode042();
                string label = mode == "mine" ? "MEU VÍDEO" : mode == "web" ? "CANAL WEB" : "CANAL GAT";
                string now = "";
                try { if (_hubRadio041 != null && !_hubRadio041.IsDisposed) now = _hubRadio041.HubNowPlaying042; } catch { }
                if (string.IsNullOrWhiteSpace(now) || now.EndsWith("—")) now = "Fonte selecionada: " + label;
                _homeRadio044.Text = label + "\r\n\r\n" + now + "\r\n\r\nMídia compartilhada com o GAT DASH e o overlay.";
            }

            if (_homeSystem044 != null)
            {
                _homeSystem044.Text =
                    "● SISTEMA GAT\r\n" +
                    Safe041(lblTelemetry, "Central GAT: aguardando") + "   •   " +
                    Safe041(lblServer, "Servidor: opcional") + "\r\n" +
                    "Cliente 1.0.50 TESTE";
            }
        }
        catch { }
    }

    private static string Initials044(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "GAT";
        var p = text.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 2)
            return (p[0].Substring(0, 1) + p[p.Length - 1].Substring(0, 1)).ToUpperInvariant();
        string x = p.Length == 0 ? text.Trim() : p[0];
        return x.Length >= 2 ? x.Substring(0, 2).ToUpperInvariant() : x.ToUpperInvariant();
    }

    private static void ReplaceTextRecursive044(Control root, string from, string to)
    {
        foreach (Control c in root.Controls)
        {
            if (!string.IsNullOrEmpty(c.Text) && c.Text.Contains(from))
                c.Text = c.Text.Replace(from, to);
            if (c.HasChildren) ReplaceTextRecursive044(c, from, to);
        }
    }

    private static GraphicsPath Round044(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        var arc = new Rectangle(bounds.X, bounds.Y, d, d);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - d; path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - d; path.AddArc(arc, 0, 90);
        arc.X = bounds.Left; path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}






