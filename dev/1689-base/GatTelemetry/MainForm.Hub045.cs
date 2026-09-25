using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private Label _homeProfileName045;
    private Label _homeProfileMeta045;
    private Label _homeTrip045;
    private Label _homeSystem045;
    private Label _homeDriversCount045;
    private DriverAvatar044 _homeAvatar045;
    private DataGridView _homeDrivers045;
    private DateTime _homeDriversLast045 = DateTime.MinValue;
    private bool _homeDriversBusy045;
    private bool _hub045Applied;

    private void ApplyHub045()
    {
        if (_hub045Applied) return;
        _hub045Applied = true;
        Text = "GAT Telemetria BETA 1.0.68.9";

        ReplaceTextRecursive044(this, "1.0.44", "1.0.45");
        BuildHome045();
        ApplyLive1689();

        _hubStatusTimer041.Tick += async delegate
        {
            SyncHome045();
            await RefreshDrivers045(false);
            await LiveTick1689(false);
        };
        Shown += async delegate
        {
            SyncHome045();
            await RefreshDrivers045(true);
            await LiveTick1689(true);
        };
    }

    private void BuildHome045()
    {
        Panel page;
        if (!_hubPages041.TryGetValue("home", out page) || page == null) return;

        page.SuspendLayout();
        page.Controls.Clear();
        page.Padding = Padding.Empty;
        page.BackColor = Color.FromArgb(2, 10, 20);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = page.BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
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
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        root.Controls.Add(top, 0, 0);

        var profile = new GlassCard044 { Caption = "PERFIL DO MOTORISTA", Dock = DockStyle.Fill };
        _homeAvatar045 = new DriverAvatar044 { Left = 16, Top = 54, Width = 86, Height = 86 };
        _homeProfileName045 = new Label
        {
            Left = 116, Top = 55, Width = 250, Height = 30,
            Text = "Motorista GAT", ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold)
        };
        _homeProfileMeta045 = new Label
        {
            Left = 116, Top = 89, Width = 270, Height = 92,
            Text = "Conta GAT aguardando...", ForeColor = Color.FromArgb(161, 193, 222),
            Font = new Font("Segoe UI", 9.2f)
        };
        var badge = new Label
        {
            Left = 16, Top = 147, Width = 88, Height = 25,
            Text = "MOTORISTA GAT", TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(8, 68, 116), ForeColor = Color.FromArgb(108, 204, 255),
            Font = new Font("Segoe UI Semibold", 7.7f, FontStyle.Bold)
        };
        profile.Controls.Add(_homeAvatar045);
        profile.Controls.Add(_homeProfileName045);
        profile.Controls.Add(_homeProfileMeta045);
        profile.Controls.Add(badge);
        top.Controls.Add(profile, 0, 0);

        var trip = new GlassCard044 { Caption = "VIAGEM ATUAL", Dock = DockStyle.Fill };
        _homeTrip045 = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Aguardando telemetria do ETS2...",
            ForeColor = Color.FromArgb(225, 236, 248),
            Font = new Font("Segoe UI Semibold", 9.7f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };
        trip.Controls.Add(_homeTrip045);
        top.Controls.Add(trip, 1, 0);

        var system = new GlassCard044 { Caption = "SISTEMA GAT", Dock = DockStyle.Fill };
        _homeSystem045 = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Central GAT: aguardando...",
            ForeColor = Color.FromArgb(191, 219, 242),
            Font = new Font("Segoe UI Semibold", 9.6f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };
        system.Controls.Add(_homeSystem045);
        top.Controls.Add(system, 2, 0);

        var middle = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = page.BackColor
        };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        root.Controls.Add(middle, 0, 1);
        root.Controls.Add(BuildRadioHome1689(), 0, 2);

        var hero = new HeroPanel044
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(7, 3, 4, 7),
            HeroImage = _homeHeroImage044
        };
        middle.Controls.Add(hero, 0, 0);

        var video = OverlayAction044("SOBREPOSIÇÃO DE VÍDEO\r\nVídeo flutuante sem barra branca", 222, 66);
        var truck = OverlayAction044("SOBREPOSIÇÃO DO CAMINHÃO\r\nPainel completo e redimensionável", 250, 66);
        video.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        truck.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        video.Click += delegate { OpenVideoOverlay041(); };
        truck.Click += delegate { OpenTruckOverlay041(); };
        hero.Controls.Add(video);
        hero.Controls.Add(truck);
        hero.Resize += delegate
        {
            truck.Left = Math.Max(16, hero.ClientSize.Width - truck.Width - 16);
            truck.Top = Math.Max(16, hero.ClientSize.Height - truck.Height - 18);
            video.Left = Math.Max(16, truck.Left - video.Width - 10);
            video.Top = truck.Top;
            if (video.Left <= 18)
            {
                video.Width = Math.Max(170, (hero.ClientSize.Width - 42) / 2);
                truck.Width = video.Width;
                video.Left = 14;
                truck.Left = video.Right + 8;
            }
        };

        var drivers = new GlassCard044
        {
            Caption = "MOTORISTAS ONLINE EM ROTA",
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 3, 7, 7)
        };
        _homeDriversCount045 = new Label
        {
            Text = "0 online  •  0 em rota",
            Width = 220, Height = 25, Top = 12,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(80, 181, 255),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
        };
        drivers.Controls.Add(_homeDriversCount045);
        drivers.Resize += delegate
        {
            if (_homeDriversCount045 != null)
                _homeDriversCount045.Left = Math.Max(20, drivers.ClientSize.Width - _homeDriversCount045.Width - 18);
        };

        _homeDrivers045 = CreateDriversGrid045();
        drivers.Controls.Add(_homeDrivers045);
        middle.Controls.Add(drivers, 1, 0);

        page.ResumeLayout(true);
        SyncHome045();
    }

    private DataGridView CreateDriversGrid045()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(5, 18, 33),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(24, 59, 89),
            RowHeadersVisible = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(10, 43, 72);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(220, 235, 250);
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        g.ColumnHeadersHeight = 35;
        g.DefaultCellStyle.BackColor = Color.FromArgb(5, 18, 33);
        g.DefaultCellStyle.ForeColor = Color.FromArgb(220, 232, 245);
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(10, 55, 88);
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.DefaultCellStyle.Font = new Font("Segoe UI", 8.7f);
        g.RowTemplate.Height = 36;

        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "driver", HeaderText = "MOTORISTA", FillWeight = 120 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "STATUS", FillWeight = 85 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "cargo", HeaderText = "CARGA", FillWeight = 125 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "destination", HeaderText = "DESTINO", FillWeight = 105 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "speed", HeaderText = "VELOCIDADE", FillWeight = 90 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "remaining", HeaderText = "RESTANTE", FillWeight = 80 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "account", HeaderText = "CONTA", Visible = false });
        g.Columns.Add(new DataGridViewButtonColumn { Name = "live", HeaderText = "AO VIVO", FillWeight = 78, FlatStyle = FlatStyle.Flat });
        g.CellContentClick += DriversLiveClick1689;

        g.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1 || e.Value == null) return;
            string s = Convert.ToString(e.Value) ?? "";
            if (s.IndexOf("Em rota", StringComparison.OrdinalIgnoreCase) >= 0)
                e.CellStyle.ForeColor = Color.FromArgb(72, 235, 132);
            else if (s.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0)
                e.CellStyle.ForeColor = Color.FromArgb(87, 184, 255);
            else
                e.CellStyle.ForeColor = Color.FromArgb(255, 121, 121);
        };
        return g;
    }

    private void SyncHome045()
    {
        try
        {
            string user = !string.IsNullOrWhiteSpace(_accountUser) ? _accountUser : "Motorista GAT";
            string driver = !string.IsNullOrWhiteSpace(_driver) ? _driver : user;
            if (_homeProfileName045 != null) _homeProfileName045.Text = driver;
            if (_homeAvatar045 != null)
            {
                _homeAvatar045.Initials = Initials044(driver);
                _homeAvatar045.Invalidate();
            }
            if (_homeProfileMeta045 != null)
            {
                _homeProfileMeta045.Text = AccountReady
                    ? "● Conta ativa\r\n● PC vinculado\r\n@" + user + "\r\nGAT LOG ETS2"
                    : "○ Conta não conectada\r\n○ PC aguardando vínculo\r\nAbra Configurações para entrar.";
                _homeProfileMeta045.ForeColor = AccountReady ? Color.FromArgb(124, 231, 154) : Color.FromArgb(198, 178, 132);
            }

            if (_homeTrip045 != null)
            {
                _homeTrip045.Text =
                    Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" +
                    Safe041(lblCargo, "Carga: Sem carga") + "\r\n" +
                    Safe041(lblRoute, "Rota: -") + "\r\n" +
                    Safe041(lblDistance, "Restante: -") + "\r\n" +
                    Safe041(lblSpeed, "Velocidade: 0 km/h");
            }

            if (_homeSystem045 != null)
            {
                string central = Safe041(lblTelemetry, "Central GAT: aguardando");
                string server = Safe041(lblServer, "Servidor: opcional");
                _homeSystem045.Text =
                    "● " + central + "\r\n" +
                    "● " + server + "\r\n" +
                    "● Cliente: 1.0.68.9 • RÁDIO LIVE\r\n\r\n" +
                    (AccountReady ? "✓ Ecossistema GAT conectado." : "Aguardando Conta GAT.");
                _homeSystem045.ForeColor = AccountReady ? Color.FromArgb(125, 231, 154) : Color.FromArgb(191, 219, 242);
            }
        }
        catch { }
    }

    private async Task RefreshDrivers045(bool force)
    {
        if (_homeDrivers045 == null || _homeDrivers045.IsDisposed || _homeDriversBusy045) return;
        if (!force && (DateTime.UtcNow - _homeDriversLast045).TotalSeconds < 3.0) return;
        _homeDriversBusy045 = true;
        _homeDriversLast045 = DateTime.UtcNow;
        try
        {
            await RefreshLiveStreams1689();
            var liveRows = new List<JObject>();
            try
            {
                string url = "https://api.gatlogets2.com.br/api/public/account-live?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string json = await _hubHttp041.GetStringAsync(url);
                JObject root = JObject.Parse(json);
                JArray telemetry = root["telemetry"] as JArray;
                if (root.Value<bool?>("ok") == true && telemetry != null)
                {
                    foreach (JToken token in telemetry.Take(64))
                    {
                        JObject item = token as JObject;
                        if (item != null) liveRows.Add(item);
                    }
                }
            }
            catch { }

            string current = !string.IsNullOrWhiteSpace(_driver) ? _driver : _accountUser;
            _homeDrivers045.Rows.Clear();
            int onlineCount = 0;
            int routeCount = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // O proprio motorista fica no topo; os demais seguem a ordem de atividade da Central.
            liveRows = liveRows
                .OrderByDescending(x => string.Equals(Convert.ToString(x["driver"]), current, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Value<bool?>("on_job") == true)
                .ThenBy(x => Convert.ToString(x["driver"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (JObject item in liveRows)
            {
                string name = (Convert.ToString(item["driver"]) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name)) name = (Convert.ToString(item["account_user"]) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;

                string cargo = (Convert.ToString(item["cargo_name"]) ?? string.Empty).Trim();
                string destination = (Convert.ToString(item["destination_city"]) ?? string.Empty).Trim();
                bool inRoute = item.Value<bool?>("on_job") == true ||
                               (!string.IsNullOrWhiteSpace(cargo) &&
                                cargo.IndexOf("Sem carga", StringComparison.OrdinalIgnoreCase) < 0 &&
                                cargo != "-" && cargo != "—");

                double speed = item.Value<double?>("speed_kmh") ?? 0.0;
                if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 || speed > 250) speed = 0;
                double remaining = item.Value<double?>("remaining_km") ?? 0.0;
                if (double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0 || remaining > 20000) remaining = 0;

                string speedText = speed.ToString("0") + " km/h";
                string remainingText = remaining > 0 ? remaining.ToString(remaining >= 100 ? "0" : "0.0") + " km" : "—";
                if (string.IsNullOrWhiteSpace(cargo)) cargo = inRoute ? "Carga detectada" : "Sem carga";
                if (string.IsNullOrWhiteSpace(destination)) destination = "—";

                onlineCount++;
                if (inRoute) routeCount++;
                string account1689 = (Convert.ToString(item["account_user"]) ?? string.Empty).Trim();
                int row1689 = _homeDrivers045.Rows.Add(
                    name,
                    inRoute ? "● Em rota" : "● Online",
                    cargo,
                    destination,
                    speedText,
                    remainingText,
                    account1689,
                    RouteLiveLabel1689(account1689, name)
                );
                StyleLiveCell1689(_homeDrivers045.Rows[row1689], account1689, name);
            }

            // Fallback local: evita uma tela vazia durante uma oscilacao curta da Central.
            if (onlineCount == 0 && !string.IsNullOrWhiteSpace(current))
            {
                string cargo = ValueAfter045(Safe041(lblCargo, "Carga: Sem carga"), "Carga:");
                string route = ValueAfter045(Safe041(lblRoute, "Rota: -"), "Rota:");
                string destination = Destination045(route);
                string speed = ValueAfter045(Safe041(lblSpeed, "Velocidade: 0 km/h"), "Velocidade:");
                string remaining = ValueAfter045(Safe041(lblDistance, "Restante: -"), "Distância restante:", "Restante:");
                bool inRoute = !string.IsNullOrWhiteSpace(cargo) &&
                               cargo.IndexOf("Sem carga", StringComparison.OrdinalIgnoreCase) < 0 &&
                               cargo != "-" && cargo != "—";
                int row1689 = _homeDrivers045.Rows.Add(current, inRoute ? "● Em rota" : "● Online",
                    string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo,
                    string.IsNullOrWhiteSpace(destination) ? "—" : destination,
                    string.IsNullOrWhiteSpace(speed) ? "0 km/h" : speed,
                    string.IsNullOrWhiteSpace(remaining) ? "—" : remaining,
                    _accountUser,
                    RouteLiveLabel1689(_accountUser, current));
                StyleLiveCell1689(_homeDrivers045.Rows[row1689], _accountUser, current);
                onlineCount = 1;
                if (inRoute) routeCount = 1;
            }

            if (_homeDriversCount045 != null)
                _homeDriversCount045.Text = onlineCount + " online  •  " + routeCount + " em rota";
        }
        catch { }
        finally { _homeDriversBusy045 = false; }
    }
    private static string ValueAfter045(string text, params string[] prefixes)
    {
        string s = text ?? "";
        foreach (string p in prefixes)
        {
            int i = s.IndexOf(p, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return s.Substring(i + p.Length).Trim();
        }
        return s.Trim();
    }

    private static string Destination045(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "";
        string[] arrows = { "→", "->", ">" };
        foreach (string a in arrows)
        {
            int i = route.LastIndexOf(a, StringComparison.Ordinal);
            if (i >= 0 && i + a.Length < route.Length) return route.Substring(i + a.Length).Trim();
        }
        return route.Trim('-',' ','—');
    }

    private static string DashSettingsFile045()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "gat-dash-settings.json");
    }

    private string DashSettingsJson045()
    {
        var o = new JObject
        {
            ["host"] = "",
            ["voice"] = true,
            ["tolerance"] = 3
        };
        try
        {
            string f = DashSettingsFile045();
            if (File.Exists(f))
            {
                JObject saved = JObject.Parse(File.ReadAllText(f));
                if (saved["host"] != null) o["host"] = Convert.ToString(saved["host"]) ?? "";
                if (saved["voice"] != null) o["voice"] = Convert.ToBoolean(saved["voice"]);
                if (saved["tolerance"] != null)
                {
                    double t = Convert.ToDouble(saved["tolerance"], System.Globalization.CultureInfo.InvariantCulture);
                    o["tolerance"] = Math.Max(0, Math.Min(30, t));
                }
            }
        }
        catch { }
        return o.ToString(Formatting.None);
    }

    private void SaveDashSettings045(JObject message)
    {
        try
        {
            JObject o = JObject.Parse(DashSettingsJson045());
            if (message["host"] != null) o["host"] = (Convert.ToString(message["host"]) ?? "").Trim();
            if (message["voice"] != null) o["voice"] = Convert.ToBoolean(message["voice"]);
            if (message["tolerance"] != null)
            {
                double t = Convert.ToDouble(message["tolerance"], System.Globalization.CultureInfo.InvariantCulture);
                o["tolerance"] = Math.Max(0, Math.Min(30, t));
            }
            string f = DashSettingsFile045();
            string tmp = f + ".tmp";
            File.WriteAllText(tmp, o.ToString(Formatting.Indented));
            if (File.Exists(f)) File.Delete(f);
            File.Move(tmp, f);
        }
        catch { }
    }
}












