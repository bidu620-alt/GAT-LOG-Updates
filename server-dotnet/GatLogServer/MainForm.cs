using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GatLogServer
{
    internal sealed class MainForm : Form
    {
        private const string CurrentVersion = "1.0.0";
        private readonly AgentClient _agent = new AgentClient();
        private readonly Timer _timer = new Timer { Interval = 3000 };
        private bool _refreshing;
        private ServerStatus _status = new ServerStatus();
        private ServerConfig _config = new ServerConfig();

        private readonly Dictionary<string, Panel> _pages = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private Panel _pageHost;
        private Label _lblServerState, _lblSession, _lblRoom, _lblPorts, _lblPlayers, _lblAgent, _lblPackages, _lblFunnel, _lblFooter;
        private DataGridView _telemetryGrid, _bindingsGrid;
        private ListBox _modsList;

        private TextBox _cfgName, _cfgDescription, _cfgWelcome, _cfgPassword, _cfgMaxPlayers;
        private CheckBox _cfgTraffic, _cfgDamage, _cfgRegistration;
        private TextBox _moderatorId;
        private Label _moderatorState;
        private TextBox _accUser, _accCurrent, _accNew, _accConfirm;

        private static readonly Color Bg = Color.FromArgb(3, 29, 44);
        private static readonly Color Card = Color.FromArgb(8, 54, 79);
        private static readonly Color Card2 = Color.FromArgb(8, 46, 67);
        private static readonly Color Blue = Color.FromArgb(31, 111, 211);
        private static readonly Color Blue2 = Color.FromArgb(14, 83, 119);
        private static readonly Color Green = Color.FromArgb(16, 185, 88);
        private static readonly Color Red = Color.FromArgb(210, 52, 43);
        private static readonly Color Purple = Color.FromArgb(92, 61, 177);
        private static readonly Color Orange = Color.FromArgb(227, 147, 0);
        private static readonly Color Cyan = Color.FromArgb(28, 132, 174);
        private static readonly Color Muted = Color.FromArgb(156, 195, 218);

        public MainForm()
        {
            Text = "GAT-LOG SERVER 1.0 | ETS2 + Telemetria";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1300, 830);
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            DoubleBuffered = true;

            BuildShell();
            BuildPages();
            ShowPage("home");

            _timer.Tick += async (s, e) => await RefreshStatusAsync();
            Shown += async (s, e) => await StartAsync();
            FormClosed += (s, e) => { _timer.Stop(); _agent.Dispose(); };
        }

        private async Task StartAsync()
        {
            _lblFooter.Text = "Conectando ao agente...";
            var ok = await _agent.EnsureAgentAsync();
            if (!ok)
            {
                _lblFooter.Text = "Agente 5055 não encontrado";
                MessageBox.Show(this, "Não foi possível iniciar ou localizar o GAT_LOG_AGENT.exe.\r\nA interface continuará aberta para diagnóstico.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                await LoadConfigAsync();
                await RefreshStatusAsync();
            }
            _timer.Start();
            _ = UpdateService.CheckAsync(CurrentVersion, this, true);
        }

        private void BuildShell()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 205, BackColor = Color.FromArgb(2, 37, 55), Padding = new Padding(10) };
            Controls.Add(sidebar);

            var logo = new PictureBox { Location = new Point(20, 16), Size = new Size(165, 145), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            logo.Image = LoadAsset("logo.png");
            sidebar.Controls.Add(logo);
            sidebar.Controls.Add(new Label { Text = "GAT LOG", Font = new Font("Segoe UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(48, 168), ForeColor = Color.White });

            int y = 220;
            AddSideButton(sidebar, "INÍCIO", "home", ref y);
            AddSideButton(sidebar, "CONFIGURAÇÕES", "config", ref y);
            AddSideButton(sidebar, "MODERADOR", "moderator", ref y);
            AddSideButton(sidebar, "TELEMETRIA", "telemetry", ref y);
            AddSideButton(sidebar, "SISTEMA", "system", ref y);
            AddSideButton(sidebar, "CONTA / SENHA", "account", ref y);

            var update = MakeButton("ATUALIZAR APP", Blue, 15, y, 175, 48);
            update.Click += async (s, e) => await UpdateService.CheckAsync(CurrentVersion, this, false);
            sidebar.Controls.Add(update);

            sidebar.Controls.Add(new Label { Text = "Proprietário do GAT-LOG:\r\nBiduzao", ForeColor = Color.Gold, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(10, y + 60), Size = new Size(180, 55) });
            sidebar.Controls.Add(new Label { Text = "C# WinForms 1.0.0", ForeColor = Muted, Location = new Point(16, 760), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom });

            var main = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(10) };
            Controls.Add(main);
            main.BringToFront();

            var header = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = Color.Black };
            main.Controls.Add(header);
            var banner = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Black };
            banner.Image = LoadAsset("banner.png");
            header.Controls.Add(banner);

            var hero = new Panel { BackColor = Card2, Location = new Point(20, 35), Size = new Size(400, 225) };
            header.Controls.Add(hero); hero.BringToFront();
            hero.Controls.Add(new Label { Text = "GAT-LOG SERVER", Font = new Font("Segoe UI", 22F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(25, 20), Size = new Size(350, 46) });
            hero.Controls.Add(new Label { Text = "Servidor dedicado autônomo - ETS2 | 128 jogadores", ForeColor = Color.FromArgb(225, 238, 247), Location = new Point(25, 72), Size = new Size(350, 26) });
            _lblServerState = new Label { Text = "SERVIDOR ...", Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = Color.Gold, Location = new Point(25, 120), Size = new Size(350, 38) };
            _lblSession = new Label { Text = "Sessão: -", ForeColor = Color.White, Location = new Point(25, 174), Size = new Size(350, 28) };
            hero.Controls.Add(_lblServerState); hero.Controls.Add(_lblSession);

            var room = new Panel { BackColor = Card2, Width = 300, Height = 175, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(header.Width - 320, 35) };
            room.Left = header.ClientSize.Width - room.Width - 20;
            header.SizeChanged += (s, e) => room.Left = Math.Max(450, header.ClientSize.Width - room.Width - 20);
            header.Controls.Add(room); room.BringToFront();
            room.Controls.Add(new Label { Text = "ID DA SALA", Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(18, 16), Size = new Size(250, 26) });
            _lblRoom = new Label { Text = "-", BackColor = Color.White, ForeColor = Color.FromArgb(20, 40, 60), Location = new Point(18, 53), Size = new Size(188, 37), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
            room.Controls.Add(_lblRoom);
            var copy = MakeButton("COPIAR", Blue, 214, 53, 70, 37);
            copy.Click += (s, e) => { if (!string.IsNullOrWhiteSpace(_status.SessionId)) Clipboard.SetText(_status.SessionId); };
            room.Controls.Add(copy);
            _lblPorts = new Label { Text = "Portas: -", Location = new Point(18, 110), Size = new Size(260, 28), ForeColor = Color.White };
            room.Controls.Add(_lblPorts);

            _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(0, 12, 0, 0) };
            main.Controls.Add(_pageHost);
            _pageHost.BringToFront();

            _lblFooter = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Muted, TextAlign = ContentAlignment.MiddleRight, Text = "Pronto" };
            main.Controls.Add(_lblFooter);
        }

        private void AddSideButton(Panel sidebar, string text, string page, ref int y)
        {
            var b = MakeButton(text, Blue2, 5, y, 185, 52);
            b.Click += async (s, e) =>
            {
                ShowPage(page);
                if (page == "system") await RefreshSystemExtrasAsync();
            };
            sidebar.Controls.Add(b);
            y += 64;
        }

        private void BuildPages()
        {
            BuildHome();
            BuildConfig();
            BuildModerator();
            BuildTelemetry();
            BuildSystem();
            BuildAccount();
        }

        private Panel NewPage(string key)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Bg, AutoScroll = true };
            _pages[key] = p;
            _pageHost.Controls.Add(p);
            return p;
        }

        private void ShowPage(string key)
        {
            foreach (var p in _pages.Values) p.Visible = false;
            if (_pages.TryGetValue(key, out var page)) { page.Visible = true; page.BringToFront(); }
        }

        private void BuildHome()
        {
            var p = NewPage("home");
            var controlsCard = MakeCard(10, 5, 690, 225);
            p.Controls.Add(controlsCard);
            controlsCard.Controls.Add(Title("HORÁRIO, CLIMA E TRÁFEGO", 20, 16, 600));

            var day = MakeButton("DIA  06:00", Orange, 20, 62, 130, 66);
            day.Click += (s, e) => CopyCommand("/set_time 06:00", "Comando de DIA copiado.");
            controlsCard.Controls.Add(day);
            var night = MakeButton("NOITE  20:00", Purple, 160, 62, 130, 66);
            night.Click += (s, e) => CopyCommand("/set_time 20:00", "Comando de NOITE copiado.");
            controlsCard.Controls.Add(night);
            var noRain = MakeButton("SEM CHUVA", Color.FromArgb(38, 119, 190), 300, 62, 130, 66);
            noRain.Click += (s, e) => CopyCommand("/set_rain_factor 0", "Comando SEM CHUVA copiado.");
            controlsCard.Controls.Add(noRain);
            var rain = MakeButton("CHUVA FORTE", Color.FromArgb(38, 119, 190), 440, 62, 130, 66);
            rain.Click += (s, e) => CopyCommand("/set_rain_factor 1", "Comando CHUVA FORTE copiado.");
            controlsCard.Controls.Add(rain);

            var noTraffic = MakeButton("SEM TRÁFEGO", Color.FromArgb(20, 145, 70), 20, 143, 180, 58);
            noTraffic.Click += async (s, e) => await SetTrafficAsync(false);
            controlsCard.Controls.Add(noTraffic);
            var traffic = MakeButton("COM TRÁFEGO", Cyan, 210, 143, 180, 58);
            traffic.Click += async (s, e) => await SetTrafficAsync(true);
            controlsCard.Controls.Add(traffic);
            var mod = MakeButton("ACESSO DE MODERADOR", Purple, 400, 143, 250, 58);
            mod.Click += (s, e) => ShowPage("moderator");
            controlsCard.Controls.Add(mod);

            var statusCard = MakeCard(715, 5, 360, 225);
            statusCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            p.Controls.Add(statusCard);
            statusCard.Controls.Add(Title("STATUS DO SERVIDOR", 20, 16, 320));
            _lblAgent = InfoLabel("Agente/API 5055: verificando...", 20, 60, 320, Color.Gold);
            _lblPackages = InfoLabel("Pacotes: verificando...", 20, 95, 320, Color.Gold);
            _lblFunnel = InfoLabel("Funnel: -", 20, 145, 320, Color.DeepSkyBlue);
            statusCard.Controls.Add(_lblAgent); statusCard.Controls.Add(_lblPackages); statusCard.Controls.Add(_lblFunnel);

            var actions = MakeCard(10, 242, 1065, 82);
            actions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            p.Controls.Add(actions);
            var start = MakeButton("INICIAR SERVIDOR", Color.FromArgb(16, 157, 66), 16, 15, 180, 52);
            start.Click += async (s, e) => await DoActionAsync("start_server"); actions.Controls.Add(start);
            var stop = MakeButton("PARAR SERVIDOR", Red, 208, 15, 180, 52);
            stop.Click += async (s, e) => { if (MessageBox.Show(this, "Deseja parar o servidor ETS2?", "GAT-LOG", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) await DoActionAsync("stop_server"); }; actions.Controls.Add(stop);
            var modsUpdate = MakeButton("ATUALIZAR MODS / PACOTES", Blue, 400, 15, 220, 52);
            modsUpdate.Click += async (s, e) => await DoActionAsync("prepare_mods"); actions.Controls.Add(modsUpdate);
            var mods = MakeButton("VER MODS", Blue2, 632, 15, 130, 52);
            mods.Click += async (s, e) => { ShowPage("system"); await RefreshSystemExtrasAsync(); }; actions.Controls.Add(mods);
            var firewall = MakeButton("FIREWALL", Blue2, 774, 15, 130, 52);
            firewall.Click += async (s, e) => await DoActionAsync("firewall"); actions.Controls.Add(firewall);
            var redetect = MakeButton("REDETECTAR", Blue2, 916, 15, 130, 52);
            redetect.Click += async (s, e) => await DoActionAsync("redetect"); actions.Controls.Add(redetect);

            var playersCard = MakeCard(10, 336, 1065, 145);
            playersCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            p.Controls.Add(playersCard);
            playersCard.Controls.Add(Title("JOGADORES ONLINE / TELEMETRIA", 20, 14, 500));
            _lblPlayers = new Label { Text = "0 / 128", Font = new Font("Segoe UI", 25F, FontStyle.Bold), ForeColor = Color.FromArgb(40, 137, 255), Location = new Point(20, 62), Size = new Size(190, 55) };
            playersCard.Controls.Add(_lblPlayers);
            playersCard.Controls.Add(new Label { Text = "Abra TELEMETRIA para acompanhar carga, peso, rota e velocidade de cada motorista.", ForeColor = Color.White, Location = new Point(220, 70), Size = new Size(700, 35) });
        }

        private void BuildConfig()
        {
            var p = NewPage("config");
            var card = MakeCard(10, 5, 920, 500); p.Controls.Add(card);
            card.Controls.Add(Title("CONFIGURAÇÕES DO SERVIDOR", 20, 18, 700));
            _cfgName = AddField(card, "Nome do servidor", 30, 82, 390);
            _cfgDescription = AddField(card, "Descrição", 470, 82, 390);
            _cfgWelcome = AddField(card, "Mensagem de boas-vindas", 30, 160, 390);
            _cfgPassword = AddField(card, "Senha da sessão", 470, 160, 390);
            _cfgMaxPlayers = AddField(card, "Máximo de jogadores", 30, 238, 180);
            _cfgTraffic = AddCheck(card, "Tráfego", 270, 252);
            _cfgDamage = AddCheck(card, "Dano entre jogadores", 430, 252);
            _cfgRegistration = AddCheck(card, "Permitir novos clientes", 650, 252);
            var save = MakeButton("SALVAR CONFIGURAÇÕES", Blue, 30, 330, 260, 52);
            save.Click += async (s, e) => await SaveConfigAsync(save);
            card.Controls.Add(save);
            card.Controls.Add(new Label { Text = "O salvamento é assíncrono: a janela não espera leitura de logs, Tailscale ou servidor dedicado.", ForeColor = Muted, Location = new Point(30, 405), Size = new Size(800, 45) });
        }

        private void BuildModerator()
        {
            var p = NewPage("moderator");
            var card = MakeCard(10, 5, 880, 370); p.Controls.Add(card);
            card.Controls.Add(Title("MODERADOR", 20, 18, 500));
            _moderatorId = AddField(card, "Steam ID64 do moderador", 30, 85, 430);
            var save = MakeButton("SALVAR MODERADOR", Blue, 500, 108, 220, 44);
            save.Click += async (s, e) => await SaveModeratorAsync(save); card.Controls.Add(save);
            _moderatorState = InfoLabel("Moderador: não configurado", 30, 175, 700, Color.Gold); card.Controls.Add(_moderatorState);
            var d = MakeButton("DIA 06:00", Orange, 30, 230, 150, 55); d.Click += (s, e) => CopyCommand("/set_time 06:00", "Comando copiado."); card.Controls.Add(d);
            var n = MakeButton("NOITE 20:00", Purple, 195, 230, 150, 55); n.Click += (s, e) => CopyCommand("/set_time 20:00", "Comando copiado."); card.Controls.Add(n);
            var nr = MakeButton("SEM CHUVA", Blue, 360, 230, 150, 55); nr.Click += (s, e) => CopyCommand("/set_rain_factor 0", "Comando copiado."); card.Controls.Add(nr);
            var r = MakeButton("CHUVA FORTE", Blue, 525, 230, 150, 55); r.Click += (s, e) => CopyCommand("/set_rain_factor 1", "Comando copiado."); card.Controls.Add(r);
        }

        private void BuildTelemetry()
        {
            var p = NewPage("telemetry");
            p.Controls.Add(Title("TELEMETRIA DOS MOTORISTAS", 20, 18, 700));
            _telemetryGrid = NewGrid(new[] { "MOTORISTA", "STATUS", "CARGA", "PESO", "ROTA", "RESTANTE", "VELOCIDADE", "ATUALIZAÇÃO" });
            _telemetryGrid.Location = new Point(20, 68);
            _telemetryGrid.Size = new Size(1030, 510);
            _telemetryGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            p.Controls.Add(_telemetryGrid);
        }

        private void BuildSystem()
        {
            var p = NewPage("system");
            var status = MakeCard(10, 5, 1040, 190); p.Controls.Add(status);
            status.Controls.Add(Title("SISTEMA / DIAGNÓSTICO", 20, 16, 600));
            var info = new Label { Name = "systemInfo", ForeColor = Color.FromArgb(140, 232, 180), Location = new Point(20, 58), Size = new Size(990, 110), Font = new Font("Consolas", 9.5F) };
            status.Controls.Add(info);

            var actions = MakeCard(10, 208, 1040, 76); p.Controls.Add(actions);
            string[] labels = { "BACKUP", "ABRIR DADOS", "FUNNEL", "REINICIAR AGENTE", "FIREWALL", "REDETECTAR" };
            string[] acts = { "backup", "open", "funnel", "restart", "firewall", "redetect" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = MakeButton(labels[i], idx == 2 ? Purple : Blue2, 12 + i * 168, 13, 155, 48);
                b.Click += async (s, e) =>
                {
                    if (acts[idx] == "open") { OpenDataDir(); return; }
                    if (acts[idx] == "restart") { await RestartAgentAsync(); return; }
                    await DoActionAsync(acts[idx]);
                };
                actions.Controls.Add(b);
            }

            var modsCard = MakeCard(10, 297, 505, 330); p.Controls.Add(modsCard);
            modsCard.Controls.Add(Title("MODS DETECTADOS", 20, 14, 400));
            _modsList = new ListBox { Location = new Point(20, 55), Size = new Size(465, 250), BackColor = Color.FromArgb(4, 36, 54), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            modsCard.Controls.Add(_modsList);

            var bindCard = MakeCard(530, 297, 520, 330); p.Controls.Add(bindCard);
            bindCard.Controls.Add(Title("CLIENTES VINCULADOS", 20, 14, 400));
            _bindingsGrid = NewGrid(new[] { "MOTORISTA", "PC", "STATUS", "ÚLTIMO CONTATO" });
            _bindingsGrid.Location = new Point(20, 55); _bindingsGrid.Size = new Size(480, 250);
            bindCard.Controls.Add(_bindingsGrid);
        }

        private void BuildAccount()
        {
            var p = NewPage("account");
            var card = MakeCard(10, 5, 760, 470); p.Controls.Add(card);
            card.Controls.Add(Title("CONTA / SENHA", 20, 18, 500));
            _accUser = AddField(card, "Usuário", 30, 82, 330);
            _accCurrent = AddField(card, "Senha atual", 390, 82, 300); _accCurrent.UseSystemPasswordChar = true;
            _accNew = AddField(card, "Nova senha", 30, 170, 330); _accNew.UseSystemPasswordChar = true;
            _accConfirm = AddField(card, "Confirmar nova senha", 390, 170, 300); _accConfirm.UseSystemPasswordChar = true;
            var save = MakeButton("SALVAR NOVA SENHA", Blue, 30, 280, 250, 50);
            save.Click += (s, e) => ChangePassword(); card.Controls.Add(save);
            card.Controls.Add(new Label { Text = "A senha continua usando o mesmo formato criptográfico das versões anteriores.", ForeColor = Muted, Location = new Point(30, 365), Size = new Size(650, 35) });
        }

        private async Task LoadConfigAsync()
        {
            try
            {
                _config = await _agent.GetConfigAsync();
                ApplyConfigToControls();
                _lblFooter.Text = "Configuração carregada";
            }
            catch (Exception ex) { _lblFooter.Text = "Configuração: " + ex.Message; }
        }

        private async Task RefreshStatusAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                if (!await _agent.HealthAsync())
                {
                    _lblAgent.Text = "Agente/API 5055: INATIVO";
                    _lblAgent.ForeColor = Color.OrangeRed;
                    _lblFooter.Text = "Agente desconectado - tentando reconectar...";
                    await _agent.EnsureAgentAsync();
                    return;
                }
                _status = await _agent.GetStatusAsync();
                ApplyStatus();
            }
            catch (Exception ex)
            {
                _lblFooter.Text = "Atualização: " + ex.Message;
            }
            finally { _refreshing = false; }
        }

        private void ApplyStatus()
        {
            _lblServerState.Text = _status.ServerOnline ? "SERVIDOR ONLINE" : "SERVIDOR OFFLINE";
            _lblServerState.ForeColor = _status.ServerOnline ? Color.FromArgb(38, 238, 122) : Color.OrangeRed;
            _lblSession.Text = "Sessão: " + (string.IsNullOrWhiteSpace(_status.ServerName) ? "-" : _status.ServerName);
            _lblRoom.Text = string.IsNullOrWhiteSpace(_status.SessionId) ? "-" : _status.SessionId;
            _lblPorts.Text = "Portas: " + (string.IsNullOrWhiteSpace(_status.Ports) ? "27015 / 27016" : _status.Ports);
            _lblPlayers.Text = _status.PlayerCount + " / " + (_status.MaxPlayers <= 0 ? 128 : _status.MaxPlayers);
            _lblAgent.Text = "Agente/API 5055: ATIVA | v" + (_status.AgentVersion ?? "-");
            _lblAgent.ForeColor = Color.FromArgb(24, 235, 123);
            _lblPackages.Text = "Pacotes: " + (_status.PackagesText ?? "-");
            _lblPackages.ForeColor = _status.PackagesOk ? Color.FromArgb(24, 235, 123) : Color.Gold;
            _lblFunnel.Text = "Funnel: " + (string.IsNullOrWhiteSpace(_status.FunnelUrl) ? "-" : _status.FunnelUrl);
            _lblFooter.Text = "Última atualização: " + DateTime.Now.ToString("HH:mm:ss") + " | UI não bloqueante";

            if (_moderatorState != null)
            {
                var configured = !string.IsNullOrWhiteSpace(_config.ModeratorSteamId);
                _moderatorState.Text = configured ? "Moderador ATIVO: " + _config.ModeratorSteamId : "Moderador: NÃO CONFIGURADO";
                _moderatorState.ForeColor = configured ? Color.FromArgb(24, 235, 123) : Color.OrangeRed;
            }
            UpdateTelemetryGrid();
            UpdateSystemInfo();
        }

        private void UpdateTelemetryGrid()
        {
            if (_telemetryGrid == null) return;
            var current = _status.Telemetry ?? new List<TelemetryRecord>();
            _telemetryGrid.SuspendLayout();
            _telemetryGrid.Rows.Clear();
            foreach (var t in current.OrderBy(x => x.Driver, StringComparer.OrdinalIgnoreCase))
            {
                var route = string.IsNullOrWhiteSpace(t.Source) && string.IsNullOrWhiteSpace(t.Destination) ? "-" : (t.Source + " → " + t.Destination).Trim(' ', '→');
                var weight = t.CargoMassKg > 0 ? (t.CargoMassKg / 1000d).ToString("0.00") + " t" : "-";
                var updated = t.UpdatedAt;
                DateTimeOffset dto;
                if (DateTimeOffset.TryParse(t.UpdatedAt, out dto)) updated = dto.ToLocalTime().ToString("HH:mm:ss");
                _telemetryGrid.Rows.Add(t.Driver, t.Status, string.IsNullOrWhiteSpace(t.Cargo) ? "Sem carga" : t.Cargo, weight, route, t.RemainingKm > 0 ? t.RemainingKm.ToString("0.0") + " km" : "-", t.SpeedKmh.ToString("0") + " km/h", updated);
            }
            _telemetryGrid.ResumeLayout();
        }

        private void ApplyConfigToControls()
        {
            if (_cfgName != null)
            {
                _cfgName.Text = _config.ServerName ?? "";
                _cfgDescription.Text = _config.Description ?? "";
                _cfgWelcome.Text = _config.WelcomeMessage ?? "";
                _cfgPassword.Text = _config.ServerPassword ?? "";
                _cfgMaxPlayers.Text = (_config.MaxPlayers <= 0 ? 128 : _config.MaxPlayers).ToString();
                _cfgTraffic.Checked = _config.Traffic;
                _cfgDamage.Checked = _config.PlayerDamage;
                _cfgRegistration.Checked = _config.RegistrationOpen;
            }
            if (_moderatorId != null) _moderatorId.Text = _config.ModeratorSteamId ?? "";
            if (_accUser != null) _accUser.Text = AuthService.EnsureAuth().User;
        }

        private async Task SaveConfigAsync(Button button)
        {
            int max;
            if (!int.TryParse(_cfgMaxPlayers.Text.Trim(), out max) || max < 1) max = 128;
            _config.ServerName = _cfgName.Text.Trim();
            _config.Description = _cfgDescription.Text.Trim();
            _config.WelcomeMessage = _cfgWelcome.Text.Trim();
            _config.ServerPassword = _cfgPassword.Text;
            _config.MaxPlayers = max;
            _config.Traffic = _cfgTraffic.Checked;
            _config.PlayerDamage = _cfgDamage.Checked;
            _config.RegistrationOpen = _cfgRegistration.Checked;
            await WithButtonAsync(button, async () =>
            {
                await _agent.SaveConfigAsync(_config);
                await LoadConfigAsync();
                await RefreshStatusAsync();
                MessageBox.Show(this, "Configurações salvas.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private async Task SaveModeratorAsync(Button button)
        {
            var id = _moderatorId.Text.Trim();
            if (id.Length > 0 && (id.Length < 15 || id.Length > 20 || id.Any(c => !char.IsDigit(c))))
            {
                MessageBox.Show(this, "Informe um Steam ID64 válido ou deixe vazio para remover.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _config.ModeratorSteamId = id;
            await WithButtonAsync(button, async () =>
            {
                await _agent.SaveConfigAsync(_config);
                await LoadConfigAsync();
                await RefreshStatusAsync();
                MessageBox.Show(this, "Moderador salvo.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private async Task SetTrafficAsync(bool enabled)
        {
            try
            {
                _config.Traffic = enabled;
                await _agent.SaveConfigAsync(_config);
                await RefreshStatusAsync();
                _lblFooter.Text = enabled ? "Tráfego ativado" : "Tráfego desativado";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async Task DoActionAsync(string action)
        {
            try
            {
                _lblFooter.Text = "Executando " + action + "...";
                var msg = await _agent.ActionAsync(action);
                _lblFooter.Text = msg;
                await RefreshStatusAsync();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async Task RestartAgentAsync()
        {
            try
            {
                await _agent.ActionAsync("shutdown_agent");
            }
            catch { }
            await Task.Delay(400);
            var ok = await _agent.EnsureAgentAsync();
            _lblFooter.Text = ok ? "Agente reiniciado" : "Falha ao reiniciar agente";
            await RefreshStatusAsync();
        }

        private async Task RefreshSystemExtrasAsync()
        {
            try
            {
                var modsTask = _agent.GetModsAsync();
                var bindTask = _agent.GetBindingsAsync();
                await Task.WhenAll(modsTask, bindTask);
                _modsList.Items.Clear();
                foreach (var m in modsTask.Result) _modsList.Items.Add(m);
                _bindingsGrid.Rows.Clear();
                foreach (var b in bindTask.Result.OrderBy(x => x.Driver, StringComparer.OrdinalIgnoreCase))
                {
                    var state = b.Blocked ? "BLOQUEADO" : b.Disconnected ? "DESCONECTADO" : "ATIVO";
                    _bindingsGrid.Rows.Add(b.Driver, string.IsNullOrWhiteSpace(b.DeviceId) ? "-" : b.DeviceId, state, b.LastSeen);
                }
                UpdateSystemInfo();
            }
            catch (Exception ex) { _lblFooter.Text = "Sistema: " + ex.Message; }
        }

        private void UpdateSystemInfo()
        {
            if (!_pages.TryGetValue("system", out var p)) return;
            var info = p.Controls.Find("systemInfo", true).FirstOrDefault() as Label;
            if (info == null) return;
            info.Text =
                "Agente: " + (_status.AgentVersion ?? "-") + " | Uptime: " + _status.AgentUptimeSec + " s | API 5055: ATIVA\r\n" +
                "Servidor: " + (_status.ServerOnline ? "ONLINE" : "OFFLINE") + " | Executável: " + (_status.ServerExe ?? "-") + "\r\n" +
                "Log: " + (_status.ServerLog ?? "-") + "\r\n" +
                "Dados: " + (_status.DataDir ?? AuthService.DataDir) + "\r\n" +
                "Funnel: " + (string.IsNullOrWhiteSpace(_status.FunnelUrl) ? "-" : _status.FunnelUrl);
        }

        private void ChangePassword()
        {
            try
            {
                if (!AuthService.Verify(_accUser.Text, _accCurrent.Text)) throw new InvalidOperationException("Senha atual incorreta.");
                if (_accNew.Text != _accConfirm.Text) throw new InvalidOperationException("A confirmação da nova senha não confere.");
                AuthService.Change(_accUser.Text.Trim(), _accNew.Text);
                _accCurrent.Clear(); _accNew.Clear(); _accConfirm.Clear();
                MessageBox.Show(this, "Usuário/senha atualizados.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void CopyCommand(string command, string message)
        {
            Clipboard.SetText(command);
            _lblFooter.Text = message + " Cole no chat (Y) do ETS2.";
        }

        private void OpenDataDir()
        {
            Directory.CreateDirectory(AuthService.DataDir);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AuthService.DataDir + "\"") { UseShellExecute = true });
        }

        private async Task WithButtonAsync(Button b, Func<Task> action)
        {
            var old = b.Text;
            b.Enabled = false;
            b.Text = "AGUARDE...";
            try { await action(); }
            catch (Exception ex) { ShowError(ex); }
            finally { b.Text = old; b.Enabled = true; }
        }

        private void ShowError(Exception ex)
        {
            _lblFooter.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static Panel MakeCard(int x, int y, int w, int h) => new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Card };
        private static Label Title(string text, int x, int y, int w) => new Label { Text = text, Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(x, y), Size = new Size(w, 35) };
        private static Label InfoLabel(string text, int x, int y, int w, Color color) => new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 42), ForeColor = color };

        private static Button MakeButton(string text, Color color, int x, int y, int w, int h)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private static TextBox AddField(Control parent, string label, int x, int y, int width)
        {
            parent.Controls.Add(new Label { Text = label, ForeColor = Color.FromArgb(210, 230, 242), Location = new Point(x, y), Size = new Size(width, 25) });
            var t = new TextBox { Location = new Point(x, y + 28), Size = new Size(width, 28), BackColor = Color.White, ForeColor = Color.FromArgb(20, 40, 60) };
            parent.Controls.Add(t);
            return t;
        }

        private static CheckBox AddCheck(Control parent, string text, int x, int y)
        {
            var c = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent };
            parent.Controls.Add(c);
            return c;
        }

        private static DataGridView NewGrid(string[] columns)
        {
            var g = new DataGridView
            {
                BackgroundColor = Color.FromArgb(5, 42, 62),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(20, 76, 101)
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Card2;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(87, 190, 255);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            g.DefaultCellStyle.BackColor = Color.FromArgb(5, 42, 62);
            g.DefaultCellStyle.ForeColor = Color.White;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(17, 82, 117);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.RowTemplate.Height = 30;
            foreach (var c in columns) g.Columns.Add(c.Replace(" ", "_"), c);
            return g;
        }

        private static Image LoadAsset(string file)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", file);
                if (!File.Exists(path)) return null;
                using (var img = Image.FromFile(path)) return new Bitmap(img);
            }
            catch { return null; }
        }
    }
}
