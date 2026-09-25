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
    private DashOverlay060 _dashOverlay060;
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

        Text = "GAT Telemetria BETA 1.0.68.9";
        MinimumSize = new Size(740, 500);
        AutoScaleMode = AutoScaleMode.Dpi;
        var work051 = Screen.FromControl(this).WorkingArea;
        Size = new Size(
            Math.Min(1220, Math.Max(720, work051.Width - 24)),
            Math.Min(820, Math.Max(480, work051.Height - 36)));
        BackColor = Color.FromArgb(3, 11, 22);

        var shell = new TableLayoutPanel { Name = "hubShell051", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor, Margin = Padding.Empty, Padding = Padding.Empty };
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
        shell.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "GAT LOG ETS2  •  Cliente 1.0.68 TESTE  •  conexão que move distâncias", Padding = new Padding(20, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(104, 128, 155), BackColor = Color.FromArgb(2, 8, 17) }, 0, 3);

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
        Shown += async delegate
        {
            // GAT_FIT_SCREEN_055
            try
            {
                var wa055 = Screen.FromControl(this).WorkingArea;
                if (wa055.Width < 1400 || wa055.Height < 800)
                {
                    int ww055 = Math.Max(MinimumSize.Width, Math.Min(1220, wa055.Width - 20));
                    int hh055 = Math.Max(MinimumSize.Height, Math.Min(820, wa055.Height - 24));
                    Size = new Size(ww055, hh055);
                    Location = new Point(wa055.Left + Math.Max(0, (wa055.Width - ww055) / 2), wa055.Top + Math.Max(0, (wa055.Height - hh055) / 2));
                }
            }
            catch { }
            SyncHub041(); await InitDash041(); ApplyResponsive051();
        };
        Resize += delegate { ApplyResponsive051(); };
        FormClosed += delegate
        {
            try { _hubStatusTimer041.Stop(); _hubDashTimer041.Stop(); _hubStatusTimer041.Dispose(); _hubDashTimer041.Dispose(); } catch { }
            try { _truckOverlay041?.Close(); _videoOverlay041?.Close(); _dashOverlay060?.Close(); } catch { }
            try { _hubRadio041?.Dispose(); _hubDash041?.Dispose(); _hubHttp041.Dispose(); } catch { }
        };
        VoiceInitialize062();
        ResumeLayout(true);
    }

    // GAT_RESPONSIVE_051
    private void ApplyResponsive051()
    {
        try
        {
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);
            bool compact = w < 1180 || h < 760;
            bool veryCompact = w < 1024 || h < 650;
            bool ultraCompact = w < 850 || h < 560;

            if (_hubBody041 != null)
                _hubBody041.Padding = ultraCompact ? new Padding(5, 4, 5, 4) : (compact ? new Padding(9, 7, 9, 7) : new Padding(18, 14, 18, 14));

            var shell = FindControl051<TableLayoutPanel>(this, "hubShell051");
            if (shell != null && shell.RowStyles.Count >= 4)
            {
                shell.RowStyles[0].Height = ultraCompact ? 50 : (veryCompact ? 56 : (compact ? 64 : 76));
                shell.RowStyles[1].Height = ultraCompact ? 36 : (veryCompact ? 40 : (compact ? 44 : 50));
                shell.RowStyles[3].Height = ultraCompact ? 19 : (compact ? 22 : 28);
            }

            var nav = FindControl051<FlowLayoutPanel>(this, "hubNav051");
            if (nav != null)
            {
                nav.Padding = veryCompact ? new Padding(8, 4, 0, 3) : (compact ? new Padding(12, 5, 0, 4) : new Padding(18, 6, 0, 4));
                foreach (Button b in nav.Controls.OfType<Button>())
                {
                    b.Height = veryCompact ? 30 : (compact ? 33 : 36);
                    b.Font = new Font("Segoe UI Semibold", ultraCompact ? 6.5f : (veryCompact ? 7.2f : (compact ? 8f : 8.5f)), FontStyle.Bold);
                }
                // GAT_NAV_WIDTH_055: encaixa todas as abas proporcionalmente na largura disponivel.
                var navButtons055 = nav.Controls.OfType<Button>().ToList();
                int baseTotal055 = 105 + 120 + 125 + 80 + 175 + 135 + 145;
                int avail055 = Math.Max(560, nav.ClientSize.Width - nav.Padding.Horizontal - 42);
                double navScale055 = Math.Min(1.0, avail055 / (double)baseTotal055);
                int[] base055 = { 105, 120, 125, 80, 175, 135, 145 };
                for (int i055 = 0; i055 < navButtons055.Count && i055 < base055.Length; i055++)
                    navButtons055[i055].Width = Math.Max(66, (int)Math.Round(base055[i055] * navScale055));
                foreach (Button b055 in navButtons055) b055.Margin = new Padding(0, 0, ultraCompact ? 3 : 6, 0);
                // mantem o fluxo horizontal; se a janela for extrema ainda ha AutoScroll como seguranca.
                if (ultraCompact) nav.Padding = new Padding(5, 3, 0, 2);
                foreach (Button dummy055 in new Button[0])
                {
                }
            }

                        // GAT_HEADER_055
            var brand055 = FindControl051<Label>(this, "hubBrand055");
            var title055 = FindControl051<Label>(this, "hubTitle055");
            var sub055 = FindControl051<Label>(this, "hubSub055");
            if (brand055 != null)
            {
                brand055.Left = ultraCompact ? 10 : 22; brand055.Top = ultraCompact ? 5 : 10;
                brand055.Font = new Font("Segoe UI Black", ultraCompact ? 18f : (veryCompact ? 21f : 25f), FontStyle.Bold | FontStyle.Italic);
            }
            if (title055 != null)
            {
                title055.Left = ultraCompact ? 78 : (veryCompact ? 98 : 118); title055.Top = ultraCompact ? 7 : 13;
                title055.Font = new Font("Segoe UI Semibold", ultraCompact ? 12f : (veryCompact ? 15f : 18f), FontStyle.Bold);
            }
            if (sub055 != null)
            {
                sub055.Visible = !ultraCompact;
                sub055.Left = veryCompact ? 101 : 121; sub055.Top = veryCompact ? 37 : 47;
                sub055.Font = new Font("Segoe UI", veryCompact ? 7.5f : 9f);
            }
var tools = FindControl051<Panel>(this, "dashTools051");
            if (tools != null)
                ArrangeDashTools051(tools, veryCompact);

            ApplyAllPages056();
            ApplyDashZoom051();
        }
        catch { }
    }

    private static T FindControl051<T>(Control root, string name) where T : Control
    {
        if (root == null) return null;
        foreach (Control c in root.Controls)
        {
            if (c is T match && string.Equals(c.Name, name, StringComparison.Ordinal)) return match;
            var nested = FindControl051<T>(c, name);
            if (nested != null) return nested;
        }
        return null;
    }

    private void ArrangeDashTools051(Panel tools, bool veryCompact)
    {
        var buttons = tools.Controls.OfType<Button>().ToList();
        var label = tools.Controls.OfType<Label>().FirstOrDefault(x => string.Equals(x.Text, "Transparência", StringComparison.Ordinal));
        int width = Math.Max(1, tools.ClientSize.Width);
        int gap = 8;
        int x = 8;
        int bw1 = veryCompact ? 112 : 150;
        int bw2 = veryCompact ? 136 : 180;
        if (buttons.Count > 0) { buttons[0].Left = x; buttons[0].Width = bw1; buttons[0].Font = new Font("Segoe UI Semibold", veryCompact ? 7.0f : 8.0f, FontStyle.Bold); x += bw1 + gap; }
        if (buttons.Count > 1) { buttons[1].Left = x; buttons[1].Width = bw2; buttons[1].Font = new Font("Segoe UI Semibold", veryCompact ? 7.0f : 8.0f, FontStyle.Bold); x += bw2 + gap; }
        if (label != null)
        {
            label.Visible = width >= 700;
            label.Left = x + 4;
            label.Width = 88;
            if (label.Visible) x += 94;
        }
        if (_overlayOpacity041 != null)
        {
            _overlayOpacity041.Left = x;
            _overlayOpacity041.Width = Math.Max(90, width - x - 8);
        }
    }
    private void ApplyDashZoom051()
    {
        try
        {
            if (_hubDash041 == null || _hubDash041.IsDisposed) return;
            const double zoom = 1.0;
            if (Math.Abs(_hubDash041.ZoomFactor - zoom) > 0.001)
                _hubDash041.ZoomFactor = zoom;
        }
        catch { }
    }
    // GAT_FULL_RESPONSIVE_056
    private void ApplyAllPages056()
    {
        try
        {
            foreach (var pair056 in _hubPages041.ToList())
                LayoutPage056(pair056.Key, pair056.Value);
        }
        catch { }
    }

    private void LayoutPage056(string key056, Panel page056)
    {
        if (page056 == null || page056.IsDisposed) return;
        int w056 = Math.Max(1, page056.ClientSize.Width);
        int h056 = Math.Max(1, page056.ClientSize.Height);
        bool compact056 = w056 < 980 || h056 < 620;
        bool narrow056 = w056 < 820 || h056 < 540;

        // Cabecalho de cada pagina sempre acompanha a largura real.
        foreach (Label head056 in page056.Controls.OfType<Label>().Where(x => x.Top <= 8 && x.Height >= 45))
        {
            head056.Width = Math.Max(120, w056 - 6);
            head056.Font = new Font("Segoe UI Semibold", narrow056 ? 9.5f : (compact056 ? 10.5f : 12f), FontStyle.Bold);
        }

        if (string.Equals(key056, "home", StringComparison.OrdinalIgnoreCase))
        {
            // A Home moderna ja usa TableLayout/Dock. Apenas compacta a faixa superior
            // e os dados do perfil para resolucoes menores, sem quebrar o grid.
            page056.AutoScroll = false;
            var root056 = page056.Controls.OfType<TableLayoutPanel>().FirstOrDefault(x => x.Dock == DockStyle.Fill);
            if (root056 != null && root056.RowStyles.Count >= 2)
                root056.RowStyles[0].Height = narrow056 ? 158 : (compact056 ? 178 : 205);

            if (_homeAvatar045 != null)
            {
                int av056 = narrow056 ? 56 : (compact056 ? 70 : 86);
                _homeAvatar045.SetBounds(narrow056 ? 9 : 16, narrow056 ? 48 : 54, av056, av056);
            }
            if (_homeProfileName045 != null)
            {
                _homeProfileName045.Left = narrow056 ? 76 : (compact056 ? 98 : 116);
                _homeProfileName045.Top = narrow056 ? 48 : 55;
                _homeProfileName045.Width = Math.Max(90, (_homeProfileName045.Parent?.ClientSize.Width ?? 260) - _homeProfileName045.Left - 8);
                _homeProfileName045.Font = new Font("Segoe UI Semibold", narrow056 ? 10f : (compact056 ? 12f : 14f), FontStyle.Bold);
            }
            if (_homeProfileMeta045 != null)
            {
                _homeProfileMeta045.Left = narrow056 ? 76 : (compact056 ? 98 : 116);
                _homeProfileMeta045.Top = narrow056 ? 76 : 89;
                _homeProfileMeta045.Width = Math.Max(90, (_homeProfileMeta045.Parent?.ClientSize.Width ?? 280) - _homeProfileMeta045.Left - 8);
                _homeProfileMeta045.Height = narrow056 ? 70 : 92;
                _homeProfileMeta045.Font = new Font("Segoe UI", narrow056 ? 7.3f : (compact056 ? 8.2f : 9.2f));
            }
            if (_homeTrip045 != null) _homeTrip045.Font = new Font("Segoe UI Semibold", narrow056 ? 7.5f : (compact056 ? 8.5f : 9.7f));
            if (_homeSystem045 != null) _homeSystem045.Font = new Font("Segoe UI Semibold", narrow056 ? 7.5f : (compact056 ? 8.5f : 9.6f));
            if (_homeDrivers045 != null)
            {
                _homeDrivers045.ColumnHeadersHeight = narrow056 ? 26 : (compact056 ? 30 : 35);
                _homeDrivers045.RowTemplate.Height = narrow056 ? 28 : (compact056 ? 32 : 36);
                _homeDrivers045.DefaultCellStyle.Font = new Font("Segoe UI", narrow056 ? 7.2f : (compact056 ? 8f : 8.7f));
                _homeDrivers045.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", narrow056 ? 6.8f : (compact056 ? 7.5f : 8.2f), FontStyle.Bold);
            }
            return;
        }

        if (string.Equals(key056, "dash", StringComparison.OrdinalIgnoreCase))
        {
            // DASH nunca usa scroll do host. Ferramentas e WebView ocupam exatamente
            // o retangulo disponivel, independentemente do tamanho em que a pagina nasceu.
            page056.AutoScroll = false;
            page056.AutoScrollMinSize = Size.Empty;
            var tools056 = FindControl051<Panel>(page056, "dashTools051");
            if (tools056 != null)
            {
                int top056 = narrow056 ? 49 : 55;
                int toolH056 = narrow056 ? 49 : 55;
                tools056.SetBounds(0, top056, w056, toolH056);
                ArrangeDashTools051(tools056, compact056 || narrow056);
                if (_hubDash041 != null && !_hubDash041.IsDisposed)
                    _hubDash041.SetBounds(0, top056 + toolH056 + 8, w056, Math.Max(1, h056 - (top056 + toolH056 + 8)));
            }
            else if (_hubDash041 != null && !_hubDash041.IsDisposed)
            {
                _hubDash041.SetBounds(0, 112, w056, Math.Max(1, h056 - 112));
            }
            return;
        }

        if (string.Equals(key056, "radio", StringComparison.OrdinalIgnoreCase))
        {
            // Radio ocupa tudo abaixo do titulo. O formulario incorporado recebe o
            // tamanho real da pagina; se algum controle antigo nao couber, o proprio
            // formulario ganha rolagem apenas como fallback.
            page056.AutoScroll = false;
            int top056 = narrow056 ? 48 : 55;
            if (_hubRadioHost041 != null && !_hubRadioHost041.IsDisposed)
                _hubRadioHost041.SetBounds(0, top056, w056, Math.Max(1, h056 - top056));
            if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
            {
                _hubRadio041.AutoScroll = true;
                _hubRadio041.AutoScrollMinSize = new Size(Math.Min(720, Math.Max(560, w056 - 8)), Math.Min(520, Math.Max(390, h056 - top056 - 8)));
                _hubRadio041.Dock = DockStyle.Fill;
            }
            return;
        }

        // Nas demais paginas, usa scroll somente se o conteudo realmente precisar.
        int minH056 = 240;
        if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase)) minH056 = 390;
        else if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase)) minH056 = 330;
        else if (string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase)) minH056 = 190;
        else if (string.Equals(key056, "gps", StringComparison.OrdinalIgnoreCase)) minH056 = 150;
        page056.AutoScroll = h056 < minH056;
        page056.AutoScrollMinSize = page056.AutoScroll ? new Size(0, minH056) : Size.Empty;

        if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase) && _hubServerCard041 != null)
        {
            _hubServerCard041.Left = 0;
            _hubServerCard041.Top = narrow056 ? 52 : 65;
            _hubServerCard041.Width = Math.Max(220, w056 - (page056.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
        }

        if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase))
        {
            if (_hubAccountCard041 != null)
            {
                _hubAccountCard041.Left = 0;
                _hubAccountCard041.Top = narrow056 ? 52 : 65;
                _hubAccountCard041.Width = Math.Max(220, w056 - (page056.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
            }
            foreach (Label l056 in page056.Controls.OfType<Label>().Where(x => x.Top > 120))
            {
                l056.Width = Math.Max(200, w056 - 12);
                l056.Font = new Font("Segoe UI", narrow056 ? 8f : 9f);
            }
        }

        if (string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Button b056 in page056.Controls.OfType<Button>())
            {
                b056.Left = 0;
                b056.Top = narrow056 ? 82 : 100;
                b056.Width = Math.Min(260, Math.Max(180, w056 - 12));
                b056.Height = narrow056 ? 32 : 36;
            }
        }
    }
    private Control HubHeader041()
    {
        var p = new Panel { Name = "hubHeader055", Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 29) };
        p.Controls.Add(new Label { Name = "hubBrand055", Text = "GAT", Left = 22, Top = 10, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Black", 25, FontStyle.Bold | FontStyle.Italic) });
        p.Controls.Add(new Label { Name = "hubTitle055", Text = "GAT TELEMETRIA BETA", Left = 118, Top = 13, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold) });
        p.Controls.Add(new Label { Name = "hubSub055", Text = "Central principal do ecossistema GAT LOG ETS2 • tudo em um só lugar", Left = 121, Top = 47, AutoSize = true, ForeColor = Color.FromArgb(145, 170, 199) });
        return p;
    }

    private Control HubNav041()
    {
        var p = new FlowLayoutPanel { Name = "hubNav051", Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 6, 0, 4), BackColor = Color.FromArgb(5, 18, 33) };
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
        bool radioAlive = _hubRadio041 != null && !_hubRadio041.IsDisposed;
        foreach (var x in _hubPages041)
        {
            if (x.Key == "radio" && radioAlive)
            {
                // Mantem o player vivo por tras das outras paginas para a musica continuar.
                x.Value.Visible = true;
                if (key != "radio") x.Value.SendToBack();
            }
            else
            {
                x.Value.Visible = x.Key == key;
            }
        }

        foreach (var x in _hubNav041)
        {
            bool on = x.Key == key;
            x.Value.BackColor = on ? Color.FromArgb(14, 74, 133) : Color.FromArgb(8, 29, 50);
            x.Value.ForeColor = on ? Color.White : Color.FromArgb(210, 225, 242);
        }

        if (key == "radio")
        {
            EnsureRadio041();
            Panel radioPage;
            if (_hubPages041.TryGetValue("radio", out radioPage)) radioPage.Visible = true;
        }

        Panel activePage;
        if (_hubPages041.TryGetValue(key, out activePage)) activePage.BringToFront();
    }
    private Panel Home041()
    {
        var p = Page041(); p.Controls.Add(Head041("INÍCIO", "Status essencial e acesso aos módulos GAT."));
        var grid = new TableLayoutPanel { Left = 0, Top = 58, Height = 180, Width = p.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _hAccount041 = StatusCard041(grid, 0, "CONTA GAT", "Conta não conectada"); _hEts041 = StatusCard041(grid, 1, "ETS2 / TELEMETRIA", "Aguardando TruckSim GPS"); _hCentral041 = StatusCard041(grid, 2, "CENTRAL GAT", "Aguardando conexão"); _hServer041 = StatusCard041(grid, 3, "COMBOIO / SERVIDOR", "Opcional"); p.Controls.Add(grid);
        var mods = new FlowLayoutPanel { Left = 0, Top = 265, Width = p.Width, Height = 155, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, WrapContents = false, AutoScroll = true };
        Module041(mods, "GAT DASH", "Dashboard e overlays", "dash"); Module041(mods, "RÁDIO GAT", "Canal GAT e Meu Vídeo", "radio"); Module041(mods, "GPS", "Navegação e alertas", "gps"); Module041(mods, "COMBOIO", "Servidor e sala", "server"); Module041(mods, "CONFIGURAÇÕES", "Conta e preferências", "settings"); p.Controls.Add(mods);
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
        var p = Page041();
        p.Controls.Add(Head041("GAT DASH", "Dashboard completo em janela independente, com proporção fixa em qualquer resolução."));

        var tools = new Panel
        {
            Name = "dashTools060",
            Left = 0, Top = 55, Height = 58, Width = p.Width,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };

        var full = HubButton041("ABRIR DASH COMPLETO", 190);
        full.Top = 10;
        full.Click += async delegate { await OpenDashOverlay060(); };
        tools.Controls.Add(full);

        var video = HubButton041("SOBREPOR VÍDEO", 145);
        video.Top = 10;
        video.Click += delegate { OpenVideoOverlay041(); };
        tools.Controls.Add(video);

        var truck = HubButton041("SOBREPOR CAMINHÃO", 175);
        truck.Top = 10;
        truck.Click += delegate { OpenTruckOverlay041(); };
        tools.Controls.Add(truck);

        var opacityLabel = new Label
        {
            Text = "Transparência",
            Top = 20, Width = 88,
            ForeColor = Color.FromArgb(155, 179, 207)
        };
        tools.Controls.Add(opacityLabel);

        _overlayOpacity041 = new TrackBar
        {
            Top = 7, Width = 180,
            Minimum = 55, Maximum = 100, Value = 94, TickFrequency = 5
        };
        _overlayOpacity041.Scroll += delegate { ApplyOverlayOpacity041(); };
        tools.Controls.Add(_overlayOpacity041);

        Action arrange = delegate
        {
            int w = Math.Max(1, tools.ClientSize.Width);
            int gap = w < 900 ? 6 : 9;
            int x = 8;
            int fullW = w < 900 ? 150 : 190;
            int videoW = w < 900 ? 112 : 145;
            int truckW = w < 900 ? 136 : 175;
            full.Left = x; full.Width = fullW; x += fullW + gap;
            video.Left = x; video.Width = videoW; x += videoW + gap;
            truck.Left = x; truck.Width = truckW; x += truckW + gap;
            opacityLabel.Visible = w >= 760;
            if (opacityLabel.Visible) { opacityLabel.Left = x + 4; x += 96; }
            _overlayOpacity041.Left = x;
            _overlayOpacity041.Width = Math.Max(90, w - x - 8);
            float fs = w < 900 ? 6.8f : 8.0f;
            full.Font = video.Font = truck.Font = new Font("Segoe UI Semibold", fs, FontStyle.Bold);
        };
        tools.Resize += delegate { arrange(); };
        p.Controls.Add(tools);

        var info = new Panel
        {
            Left = 0, Top = 125, Width = p.Width,
            Height = Math.Max(260, p.Height - 125),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(3, 13, 25)
        };
        var title = new Label
        {
            Text = "GAT DASH COMPLETO",
            AutoSize = true, Left = 28, Top = 32,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold)
        };
        var desc = new Label
        {
            Text = "A tela completa agora abre separada do GAT Telemetria.\r\n" +
                   "Ao redimensionar, TODO o dashboard diminui ou aumenta junto, sem mudar a proporção.\r\n" +
                   "Se a janela ficar em outro formato, aparecem apenas margens escuras — nada é cortado ou espremido.",
            Left = 30, Top = 82, Width = 820, Height = 84,
            ForeColor = Color.FromArgb(167, 199, 230),
            Font = new Font("Segoe UI", 10.5f)
        };
        var open = HubButton041("ABRIR / SOBREPOR DASH COMPLETO", 285);
        open.Left = 30; open.Top = 182; open.Height = 42;
        open.Click += async delegate { await OpenDashOverlay060(); };
        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open);
        p.Controls.Add(info);

        arrange();
        return p;
    }

    private Panel Radio041() { var p = Page041(); p.Controls.Add(Head041("RÁDIO GAT", "A mesma Rádio/TV GAT usada pelo DASH e pelo overlay de vídeo.")); _hubRadioHost041 = new Panel { Left = 0, Top = 55, Width = p.Width, Height = Math.Max(350, p.Height - 55), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(4, 13, 25) }; p.Controls.Add(_hubRadioHost041); return p; }
    private Panel Server041() { var p = Page041(); p.Controls.Add(Head041("COMBOIO / SERVIDOR", "Servidor continua opcional e usa a configuração que já existe.")); if (_hubServerCard041 != null) { _hubServerCard041.Left = 0; _hubServerCard041.Top = 65; _hubServerCard041.Width = p.Width; _hubServerCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(_hubServerCard041); } return p; }
    private Panel Settings041() { var p = Page041(); p.Controls.Add(Head041("CONFIGURAÇÕES", "Conta GAT e preferências. Novas opções de voz entram aqui depois.")); if (_hubAccountCard041 != null) { _hubAccountCard041.Left = 0; _hubAccountCard041.Top = 65; _hubAccountCard041.Width = p.Width; _hubAccountCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(_hubAccountCard041); } BuildVoiceSettings062(p); return p; }
    private Panel Updates041() { var p = Simple041("ATUALIZAÇÕES", "Verifique novas versões do GAT Telemetria sem sair do aplicativo."); var b = HubButton041("VERIFICAR ATUALIZAÇÃO", 230); b.Left = 0; b.Top = 100; b.Click += async delegate { await UpdateClickedAsync(); }; p.Controls.Add(b); return p; }
    private Panel Simple041(string title, string sub) { var p = Page041(); p.Controls.Add(Head041(title, sub)); return p; }
    private Panel Page041() { return new Panel { BackColor = Color.FromArgb(3, 11, 22), AutoScroll = true, AutoScrollMinSize = new Size(680, 400) }; }
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
        if (_hubRadioHost041 == null) return;
        if (_hubRadio041 != null && !_hubRadio041.IsDisposed) { _hubRadio041.ConfigureAccount049(_accountUser, _accountToken); return; }
        try { _hubRadio041 = new RadioForm { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill, TopMost = false, ShowInTaskbar = true }; _hubRadioHost041.Controls.Clear(); _hubRadioHost041.Controls.Add(_hubRadio041); _hubRadio041.Show(); _hubRadio041.ConfigureAccount049(_accountUser, _accountToken); }
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
            if (type == "ready") { await DashJs041("window.gatDashNativeReady('windows'," + DashSettingsJson045() + ")"); await DashSession041(); }
            else if (type == "login") await DashSession041();
            else if (type == "mediaRefresh") await DashMedia041();
            else if (type == "mediaMode") { SaveMediaMode041(Convert.ToString(m["mode"])); try { _hubRadio041?.HubSyncMode042(); } catch { } await DashMedia041(); }
            else if (type == "speak") { /* 1.0.68.9 VOZ LIMPA: o GAT DASH nao controla a voz do Telemetria. */ }
            else if (type == "stopSpeech") { /* isolado: nenhum comando de voz vem do DASH */ }
            else if (type == "setSettings") { SaveDashSettings045(m); await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")"); }
        }
        catch { }
    }

    private async Task DashSession041()
    {
        if (!DashTargetReady060()) return;
        if (!AccountReady) { await DashJs041("window.gatDashLoginTransportError('Entre primeiro na Conta GAT em Configurações.')"); return; }
        var r = new JObject { ["ok"] = true, ["user"] = _accountUser, ["role"] = "driver" }; await DashJs041("window.gatDashLoginResult(" + r.ToString(Formatting.None) + ")"); await DashMedia041();
    }

    private async Task PollDash041()
    {
        if (!DashTargetReady060() || _hubDashBusy041) return; _hubDashBusy041 = true;
        try { string json = await _hubHttp041.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry"); await DashJs041("window.gatDashPushTelemetry(" + JsonConvert.SerializeObject(json) + ")"); }
        catch (Exception ex) { await DashJs041("window.gatDashTelemetryError(" + JsonConvert.SerializeObject(ex.Message.Length > 80 ? ex.Message.Substring(0, 80) : ex.Message) + ")"); }
        finally { _hubDashBusy041 = false; }
    }
    private async Task DashMedia041() { try { string j = await _hubHttp041.GetStringAsync("http://127.0.0.1:31378/api/gat/media?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); await DashJs041("window.gatDashPushMedia(" + JsonConvert.SerializeObject(j) + ")"); } catch { await DashJs041("window.gatDashMediaError('Rádio GAT indisponível')"); } }
    private async Task DashJs041(string js)
    {
        try
        {
            if (_hubDashReady041 && _hubDash041?.CoreWebView2 != null)
                await _hubDash041.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch { }
        try
        {
            if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady)
                await _dashOverlay060.ExecuteScriptAsync(js);
        }
        catch { }
    }
    private static void SaveMediaMode041(string mode) { try { mode = (mode ?? "").Trim().ToLowerInvariant(); if (mode == "gat" || mode == "mine") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode); else if (mode == "web") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), "gat"); } catch { } }

    private bool DashTargetReady060()
    {
        bool embedded = _hubDashReady041 && _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
        bool full = _dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady;
        return embedded || full;
    }

    private async Task OpenDashOverlay060()
    {
        try
        {
            if (_dashOverlay060 == null || _dashOverlay060.IsDisposed)
            {
                _dashOverlay060 = new DashOverlay060(DashMessage041);
                _dashOverlay060.FormClosed += delegate
                {
                    _dashOverlay060 = null;
                    _hubDashReady041 = _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
                    if (!_hubDashReady041) _hubDashTimer041.Stop();
                };
            }

            if (!_dashOverlay060.Visible) _dashOverlay060.Show();
            await _dashOverlay060.InitializeAsync();
            _hubDashReady041 = true;
            _hubDashTimer041.Start();
            ApplyOverlayOpacity041();
            _dashOverlay060.BringToFront();
            await DashSession041();
            await PollDash041();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir o GAT DASH completo.\r\n" + ex.Message, "GAT DASH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void OpenTruckOverlay041() { if (_truckOverlay041 == null || _truckOverlay041.IsDisposed) { _truckOverlay041 = new TruckOverlay041(); _truckOverlay041.FormClosed += delegate { _truckOverlay041 = null; }; } ApplyOverlayOpacity041(); _truckOverlay041.TopMost = true; _truckOverlay041.ShowInTaskbar = true; if (!_truckOverlay041.Visible) _truckOverlay041.Show(); _truckOverlay041.BringToFront(); }
    private void OpenVideoOverlay041() { if (_videoOverlay041 == null || _videoOverlay041.IsDisposed) { _videoOverlay041 = new VideoOverlay041(); _videoOverlay041.FormClosed += delegate { _videoOverlay041 = null; }; } ApplyOverlayOpacity041(); _videoOverlay041.TopMost = true; _videoOverlay041.ShowInTaskbar = true; if (!_videoOverlay041.Visible) _videoOverlay041.Show(); _videoOverlay041.BringToFront(); }
    private void CloseOverlays041() { try { _truckOverlay041?.Close(); } catch { } try { _videoOverlay041?.Close(); } catch { } }
    private void ApplyOverlayOpacity041() { double o = (_overlayOpacity041 == null ? 94 : _overlayOpacity041.Value) / 100.0; try { if (_truckOverlay041 != null && !_truckOverlay041.IsDisposed) _truckOverlay041.Opacity = o; } catch { } try { if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed) _videoOverlay041.Opacity = o; } catch { } try { if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed) _dashOverlay060.Opacity = o; } catch { } }
}




























