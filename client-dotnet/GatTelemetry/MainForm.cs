using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal sealed class MainForm : Form
    {
        private const string CurrentVersion = "1.0.0";
        private const string VersionUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json";

        private readonly ApiClient _api = new ApiClient();
        private readonly TelemetryEngine _telemetry = new TelemetryEngine();
        private readonly Timer _timer = new Timer { Interval = 1000 };

        private List<ServerEntry> _servers;
        private ClientSettings _settings;
        private readonly string _deviceId;

        private bool _busy;
        private bool _waiting;
        private bool _loggedIn;
        private string _endpoint = string.Empty;
        private string _driver = string.Empty;
        private string _token = string.Empty;
        private DateTime _lastServerProbe = DateTime.MinValue;
        private DateTime _lastPlayersProbe = DateTime.MinValue;
        private DateTime _lastHeartbeat = DateTime.MinValue;
        private DateTime _lastTelemetry = DateTime.MinValue;
        private ServerInfo _serverInfo = new ServerInfo();
        private RemoteVersion _availableUpdate;

        private ComboBox cmbServers;
        private Button btnRemove;
        private Button btnEnter;
        private Button btnUpdate;
        private CheckBox chkAuto;
        private Label lblServer;
        private Label lblRoom;
        private Label lblDriver;
        private Label lblSession;
        private Label lblTruck;
        private Label lblTelemetry;
        private Label lblCargo;
        private Label lblRoute;
        private Label lblDistance;
        private Label lblSpeed;
        private Label lblWeight;
        private Label lblVersion;

        public MainForm()
        {
            Text = "GAT Telemetria C# 1.0 TESTE";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 600);
            Size = new Size(860, 650);
            BackColor = Color.FromArgb(20, 25, 34);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);

            ClientStore.Ensure();
            _servers = ClientStore.LoadServers();
            _settings = ClientStore.LoadSettings();
            _deviceId = ClientStore.GetDeviceId();

            BuildUi();
            LoadServerList();

            chkAuto.Checked = _settings.AutoConnect;
            SelectLastServer();

            _timer.Tick += async (s, e) => await TickAsync();
            _timer.Start();

            Shown += async (s, e) =>
            {
                await RefreshServerInfoAsync(true);
                await CheckUpdateAsync(false);
                if (_settings.AutoConnect && cmbServers.SelectedItem is ServerEntry)
                    BeginWaiting(false);
            };

            FormClosed += (s, e) =>
            {
                _timer.Stop();
                _api.Dispose();
                _telemetry.Dispose();
            };
        }

        private void BuildUi()
        {
            var title = new Label
            {
                Text = "GAT TELEMETRIA",
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.White,
                Location = new Point(24, 18)
            };
            Controls.Add(title);

            var subtitle = new Label
            {
                Text = "Cliente ETS2 • C# WinForms • conexão automática",
                AutoSize = true,
                ForeColor = Color.Silver,
                Location = new Point(28, 58)
            };
            Controls.Add(subtitle);

            var serverBox = NewGroup("SERVIDOR", 24, 88, 800, 150);
            cmbServers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 18, Top = 35, Width = 420 };
            cmbServers.SelectedIndexChanged += async (s, e) => await SelectedServerChangedAsync();
            serverBox.Controls.Add(cmbServers);

            serverBox.Controls.Add(MakeButton("ADICIONAR", 450, 33, 96, 30, AddServerClicked));
            btnRemove = MakeButton("REMOVER", 554, 33, 96, 30, RemoveServerClicked);
            serverBox.Controls.Add(btnRemove);
            serverBox.Controls.Add(MakeButton("ATUALIZAR", 658, 33, 118, 30, async (s, e) => await RefreshServerInfoAsync(true)));

            lblServer = MakeValue("Servidor: aguardando", 18, 75, 520);
            lblRoom = MakeValue("Sala: -", 18, 101, 520);
            serverBox.Controls.Add(lblServer);
            serverBox.Controls.Add(lblRoom);
            serverBox.Controls.Add(MakeButton("COPIAR ID", 658, 91, 118, 30, CopyRoomClicked));
            Controls.Add(serverBox);

            var sessionBox = NewGroup("CONEXÃO DO MOTORISTA", 24, 250, 800, 145);
            chkAuto = new CheckBox { Text = "Conectar automaticamente", Left = 18, Top = 31, Width = 210, ForeColor = Color.WhiteSmoke, BackColor = Color.Transparent };
            chkAuto.CheckedChanged += AutoChanged;
            sessionBox.Controls.Add(chkAuto);

            btnEnter = MakeButton("ENTRAR / AGUARDAR", 585, 26, 191, 36, EnterClicked);
            sessionBox.Controls.Add(btnEnter);

            lblSession = MakeValue("GAT LOG: parado", 18, 70, 340);
            lblDriver = MakeValue("Motorista: -", 390, 70, 385);
            sessionBox.Controls.Add(lblSession);
            sessionBox.Controls.Add(lblDriver);
            Controls.Add(sessionBox);

            var telBox = NewGroup("TELEMETRIA", 24, 407, 800, 148);
            lblTruck = MakeValue("TruckSim GPS: aguardando", 18, 30, 365);
            lblTelemetry = MakeValue("Envio: parado", 405, 30, 370);
            lblCargo = MakeValue("Carga: Sem carga", 18, 62, 365);
            lblRoute = MakeValue("Rota: -", 405, 62, 370);
            lblDistance = MakeValue("Restante: -", 18, 94, 220);
            lblSpeed = MakeValue("Velocidade: 0 km/h", 270, 94, 220);
            lblWeight = MakeValue("Peso: -", 520, 94, 255);
            telBox.Controls.Add(lblTruck);
            telBox.Controls.Add(lblTelemetry);
            telBox.Controls.Add(lblCargo);
            telBox.Controls.Add(lblRoute);
            telBox.Controls.Add(lblDistance);
            telBox.Controls.Add(lblSpeed);
            telBox.Controls.Add(lblWeight);
            Controls.Add(telBox);

            btnUpdate = MakeButton("VERIFICAR ATUALIZAÇÃO", 24, 570, 220, 32, async (s, e) => await UpdateClickedAsync());
            Controls.Add(btnUpdate);

            lblVersion = new Label
            {
                Text = "GAT Telemetria C# " + CurrentVersion + " TESTE",
                AutoSize = true,
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(625, 578)
            };
            Controls.Add(lblVersion);

            Resize += (s, e) =>
            {
                int width = Math.Max(720, ClientSize.Width - 48);
                serverBox.Width = width;
                sessionBox.Width = width;
                telBox.Width = width;
                lblVersion.Left = Math.Max(24, ClientSize.Width - lblVersion.Width - 28);
            };
        }

        private GroupBox NewGroup(string text, int x, int y, int width, int height)
        {
            return new GroupBox
            {
                Text = text,
                Left = x,
                Top = y,
                Width = width,
                Height = height,
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(28, 34, 45)
            };
        }

        private Label MakeValue(string text, int x, int y, int width)
        {
            return new Label { Text = text, Left = x, Top = y, Width = width, Height = 24, ForeColor = Color.Gainsboro };
        }

        private Button MakeButton(string text, int x, int y, int width, int height, EventHandler handler)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = width, Height = height, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 86, 150), ForeColor = Color.White };
            b.FlatAppearance.BorderSize = 0;
            b.Click += handler;
            return b;
        }

        private void LoadServerList()
        {
            cmbServers.BeginUpdate();
            cmbServers.Items.Clear();
            foreach (var server in _servers) cmbServers.Items.Add(server);
            cmbServers.EndUpdate();
            btnRemove.Enabled = _servers.Count > 0;
        }

        private void SelectLastServer()
        {
            if (_servers.Count == 0) return;
            int index = -1;
            if (!string.IsNullOrWhiteSpace(_settings.LastServer))
            {
                index = _servers.FindIndex(x =>
                    string.Equals(ClientStore.NormalizeEndpoint(x.Endpoint), ClientStore.NormalizeEndpoint(_settings.LastServer), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, _settings.LastServer, StringComparison.OrdinalIgnoreCase));
            }
            cmbServers.SelectedIndex = index >= 0 ? index : 0;
        }

        private async Task SelectedServerChangedAsync()
        {
            if (!(cmbServers.SelectedItem is ServerEntry s)) return;
            _settings.LastServer = s.Endpoint;
            ClientStore.SaveSettings(_settings);
            if (_loggedIn || _waiting)
            {
                _loggedIn = false;
                _waiting = chkAuto.Checked;
                _driver = string.Empty;
                _token = string.Empty;
            }
            _serverInfo = new ServerInfo();
            _lastServerProbe = DateTime.MinValue;
            await RefreshServerInfoAsync(true);
        }

        private void AutoChanged(object sender, EventArgs e)
        {
            _settings.AutoConnect = chkAuto.Checked;
            ClientStore.SaveSettings(_settings);
            if (chkAuto.Checked && cmbServers.SelectedItem is ServerEntry)
                BeginWaiting(false);
        }

        private void EnterClicked(object sender, EventArgs e)
        {
            if (!(cmbServers.SelectedItem is ServerEntry))
            {
                MessageBox.Show("Adicione ou selecione um servidor primeiro.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginWaiting(true);
        }

        private void BeginWaiting(bool manual)
        {
            var s = cmbServers.SelectedItem as ServerEntry;
            if (s == null) return;
            _endpoint = ClientStore.NormalizeEndpoint(s.Endpoint);
            _settings.LastServer = _endpoint;
            ClientStore.SaveSettings(_settings);
            _waiting = true;
            if (manual) _loggedIn = false;
            lblSession.Text = "GAT LOG: aguardando sessão...";
            lblTelemetry.Text = "Envio: aguardando motorista";
            ClientStore.Log("aguardando sessao em " + _endpoint);
        }

        private async Task TickAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (cmbServers.SelectedItem is ServerEntry selected)
                {
                    _endpoint = ClientStore.NormalizeEndpoint(selected.Endpoint);
                    if ((DateTime.UtcNow - _lastServerProbe).TotalSeconds >= 4)
                        await RefreshServerInfoAsync(false);
                }

                if (!_waiting && !_loggedIn) return;
                if (string.IsNullOrWhiteSpace(_endpoint)) return;

                if (!_serverInfo.Reachable)
                {
                    _loggedIn = false;
                    lblSession.Text = "GAT LOG: servidor indisponível";
                    lblTelemetry.Text = "Envio: aguardando servidor";
                    return;
                }
                if (_serverInfo.Supported && !_serverInfo.Online)
                {
                    _loggedIn = false;
                    lblSession.Text = "GAT LOG: servidor ETS2 offline";
                    lblTelemetry.Text = "Envio: aguardando sala";
                    return;
                }

                PlayersResult players = null;
                if ((DateTime.UtcNow - _lastPlayersProbe).TotalSeconds >= 2)
                {
                    players = await _api.GetPlayersAsync(_endpoint);
                    _lastPlayersProbe = DateTime.UtcNow;
                }

                if (players != null && players.Ok)
                {
                    string matched = ChooseDriver(players.Players);
                    if (string.IsNullOrWhiteSpace(matched))
                    {
                        _loggedIn = false;
                        lblDriver.Text = "Motorista: -";
                        lblSession.Text = players.Players.Count == 0
                            ? "GAT LOG: aguardando você entrar na sessão"
                            : "GAT LOG: aguardando motorista conhecido";
                        lblTelemetry.Text = "Envio: aguardando sessão";
                        return;
                    }

                    if (!string.Equals(_driver, matched, StringComparison.OrdinalIgnoreCase))
                    {
                        _driver = matched;
                        _loggedIn = false;
                    }
                }

                if (!_loggedIn)
                {
                    if (string.IsNullOrWhiteSpace(_driver)) return;
                    if (!await LoginAsync(_driver)) return;
                }

                if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds >= 3)
                {
                    var hb = await _api.HeartbeatAsync(_endpoint, _driver, _deviceId, _token);
                    _lastHeartbeat = DateTime.UtcNow;
                    if (!IsAccepted(hb))
                    {
                        if (NeedsTokenRenewal(hb) && await LoginAsync(_driver, true))
                        {
                            hb = await _api.HeartbeatAsync(_endpoint, _driver, _deviceId, _token);
                            _lastHeartbeat = DateTime.UtcNow;
                        }
                        if (!IsAccepted(hb))
                        {
                            _loggedIn = false;
                            lblTelemetry.Text = "Envio: heartbeat recusado";
                            return;
                        }
                    }
                }

                if ((DateTime.UtcNow - _lastTelemetry).TotalMilliseconds >= 900)
                {
                    JObject tele = await _telemetry.ReadAsync();
                    _lastTelemetry = DateTime.UtcNow;
                    if (tele == null)
                    {
                        lblTruck.Text = "TruckSim GPS: aguardando";
                        lblTelemetry.Text = "Envio: conectado, sem telemetria";
                        return;
                    }

                    lblTruck.Text = "TruckSim GPS: CONECTADO";
                    UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(tele));
                    var sent = await _api.SendTelemetryAsync(_endpoint, _driver, _deviceId, _token, tele);
                    if (!IsAccepted(sent) && NeedsTokenRenewal(sent) && await LoginAsync(_driver, true))
                        sent = await _api.SendTelemetryAsync(_endpoint, _driver, _deviceId, _token, tele);

                    lblTelemetry.Text = IsAccepted(sent) ? "Envio: ONLINE" : "Envio: falha ao enviar";
                    if (!IsAccepted(sent)) ClientStore.Log("telemetria falhou: " + sent.StatusCode + " " + sent.Text);
                }
            }
            catch (Exception ex)
            {
                ClientStore.Log("tick: " + ex);
            }
            finally
            {
                _busy = false;
            }
        }

        private string ChooseDriver(List<string> players)
        {
            if (players == null || players.Count == 0) return string.Empty;

            if (!string.IsNullOrWhiteSpace(_driver))
            {
                var m = players.FirstOrDefault(x => string.Equals(x, _driver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            var saved = ClientStore.FindCredential(_endpoint, _settings.LastDriver);
            if (saved != null)
            {
                var m = players.FirstOrDefault(x => string.Equals(x, saved.Driver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            if (!string.IsNullOrWhiteSpace(_settings.LastDriver))
            {
                var m = players.FirstOrDefault(x => string.Equals(x, _settings.LastDriver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            return players.Count == 1 ? players[0] : string.Empty;
        }

        private async Task<bool> LoginAsync(string driver, bool forceNewToken = false)
        {
            var credential = ClientStore.FindCredential(_endpoint, driver);
            string tok = forceNewToken ? string.Empty : ClientStore.GetPlainToken(credential);
            var r = await _api.LoginAsync(_endpoint, driver, _deviceId, tok);
            if (!IsAccepted(r) && !forceNewToken && !string.IsNullOrWhiteSpace(tok))
                r = await _api.LoginAsync(_endpoint, driver, _deviceId, string.Empty);

            if (!IsAccepted(r))
            {
                lblSession.Text = "GAT LOG: login recusado";
                lblTelemetry.Text = "Envio: aguardando login";
                ClientStore.Log("login recusado " + r.StatusCode + " " + r.Text);
                return false;
            }

            string canonical = ApiClient.Str(r.Json?["driver"]);
            if (string.IsNullOrWhiteSpace(canonical)) canonical = driver;
            string newToken = ApiClient.Str(r.Json?["token"]);
            if (string.IsNullOrWhiteSpace(newToken)) newToken = tok;

            _driver = canonical;
            _token = newToken;
            _loggedIn = true;
            _waiting = true;
            _settings.LastDriver = canonical;
            ClientStore.SaveSettings(_settings);
            if (!string.IsNullOrWhiteSpace(newToken)) ClientStore.SaveCredential(_endpoint, canonical, newToken);

            lblDriver.Text = "Motorista: " + canonical;
            lblSession.Text = "GAT LOG: CONECTADO";
            lblTelemetry.Text = "Envio: iniciando telemetria";
            ClientStore.Log("login ok: " + canonical);
            return true;
        }

        private static bool IsAccepted(ApiResponse r)
        {
            if (r == null || r.StatusCode != 200 || r.Json == null) return false;
            var ok = r.Json["ok"];
            return ok == null || ApiClient.Bool(ok);
        }

        private static bool NeedsTokenRenewal(ApiResponse r)
        {
            if (r == null) return false;
            if (r.StatusCode == 401) return true;
            string err = ApiClient.Str(r.Json?["error"]);
            return string.Equals(err, "token_required", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(err, "invalid_token", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateTelemetryDisplay(TelemetryDisplay d)
        {
            lblCargo.Text = "Carga: " + d.Cargo;
            lblRoute.Text = "Rota: " + d.Route;
            lblDistance.Text = "Restante: " + d.Distance;
            lblSpeed.Text = "Velocidade: " + d.Speed;
            lblWeight.Text = "Peso: " + d.Weight;
        }

        private async Task RefreshServerInfoAsync(bool force)
        {
            var selected = cmbServers.SelectedItem as ServerEntry;
            if (selected == null)
            {
                lblServer.Text = "Servidor: nenhum cadastrado";
                lblRoom.Text = "Sala: -";
                return;
            }
            if (!force && (DateTime.UtcNow - _lastServerProbe).TotalSeconds < 3) return;

            string ep = ClientStore.NormalizeEndpoint(selected.Endpoint);
            _serverInfo = await _api.GetServerInfoAsync(ep);
            _lastServerProbe = DateTime.UtcNow;

            if (!_serverInfo.Reachable)
            {
                lblServer.Text = "Servidor: OFFLINE";
                lblRoom.Text = "Sala: -";
            }
            else if (!_serverInfo.Supported)
            {
                lblServer.Text = "Servidor: acessível, API antiga";
                lblRoom.Text = "Sala: -";
            }
            else
            {
                string name = string.IsNullOrWhiteSpace(_serverInfo.ServerName) ? selected.Name : _serverInfo.ServerName;
                lblServer.Text = "Servidor: " + (string.IsNullOrWhiteSpace(name) ? "ONLINE" : name) + " • " + _serverInfo.Players + "/" + _serverInfo.MaxPlayers;
                lblRoom.Text = "Sala: " + (string.IsNullOrWhiteSpace(_serverInfo.SessionId) ? "-" : _serverInfo.SessionId);
            }
        }

        private void CopyRoomClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_serverInfo.SessionId)) return;
            Clipboard.SetText(_serverInfo.SessionId);
        }

        private void AddServerClicked(object sender, EventArgs e)
        {
            using (var d = new AddServerForm())
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string endpoint = ClientStore.NormalizeEndpoint(d.Endpoint);
                if (string.IsNullOrWhiteSpace(endpoint)) return;
                string name = string.IsNullOrWhiteSpace(d.ServerName) ? endpoint : d.ServerName.Trim();
                if (_servers.Any(x => string.Equals(ClientStore.NormalizeEndpoint(x.Endpoint), endpoint, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Esse servidor já está cadastrado.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _servers.Add(new ServerEntry { Name = name, Endpoint = endpoint });
                ClientStore.SaveServers(_servers);
                LoadServerList();
                cmbServers.SelectedIndex = _servers.Count - 1;
            }
        }

        private void RemoveServerClicked(object sender, EventArgs e)
        {
            int i = cmbServers.SelectedIndex;
            if (i < 0 || i >= _servers.Count) return;
            if (MessageBox.Show("Remover este servidor da lista?", "GAT Telemetria", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _servers.RemoveAt(i);
            ClientStore.SaveServers(_servers);
            _loggedIn = false;
            _waiting = false;
            _driver = string.Empty;
            _token = string.Empty;
            LoadServerList();
            if (_servers.Count > 0) cmbServers.SelectedIndex = Math.Min(i, _servers.Count - 1);
        }

        private async Task CheckUpdateAsync(bool showNoUpdate)
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
                {
                    string text = await http.GetStringAsync(VersionUrl);
                    var remote = JsonConvert.DeserializeObject<RemoteVersion>(text);
                    if (remote != null && IsNewer(remote.Version, CurrentVersion) && !string.IsNullOrWhiteSpace(remote.EffectiveUrl))
                    {
                        _availableUpdate = remote;
                        btnUpdate.Text = "ATUALIZAR PARA " + remote.Version;
                        btnUpdate.BackColor = Color.FromArgb(32, 132, 91);
                        return;
                    }
                }
                _availableUpdate = null;
                btnUpdate.Text = "VERIFICAR ATUALIZAÇÃO";
                if (showNoUpdate) MessageBox.Show("Você já está na versão mais recente.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (showNoUpdate) MessageBox.Show("Não foi possível verificar atualização.\r\n\r\n" + ex.Message, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task UpdateClickedAsync()
        {
            if (_availableUpdate == null)
            {
                await CheckUpdateAsync(true);
                return;
            }

            var r = MessageBox.Show(
                "Instalar GAT Telemetria " + _availableUpdate.Version + "?\r\n\r\n" + (_availableUpdate.Notes ?? string.Empty),
                "Atualização GAT Telemetria", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r != DialogResult.Yes) return;

            btnUpdate.Enabled = false;
            btnUpdate.Text = "BAIXANDO...";
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "GAT_TELEMETRIA_DOTNET_SETUP_" + _availableUpdate.Version + ".exe");
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
                {
                    byte[] bytes = await http.GetByteArrayAsync(_availableUpdate.EffectiveUrl);
                    File.WriteAllBytes(path, bytes);
                }
                if (!string.IsNullOrWhiteSpace(_availableUpdate.Sha256))
                {
                    string got;
                    using (var sha = SHA256.Create()) got = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(got, _availableUpdate.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SHA256 do instalador não confere.");
                }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                Application.Exit();
            }
            catch (Exception ex)
            {
                btnUpdate.Enabled = true;
                btnUpdate.Text = "TENTAR ATUALIZAÇÃO";
                MessageBox.Show("Falha ao atualizar:\r\n\r\n" + ex.Message, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsNewer(string remote, string local)
        {
            Version r, l;
            return Version.TryParse(remote, out r) && Version.TryParse(local, out l) && r > l;
        }
    }

    internal sealed class AddServerForm : Form
    {
        private readonly TextBox txtName;
        private readonly TextBox txtEndpoint;
        public string ServerName => txtName.Text;
        public string Endpoint => txtEndpoint.Text;

        public AddServerForm()
        {
            Text = "Adicionar servidor";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(510, 180);

            Controls.Add(new Label { Text = "Nome (opcional):", Left = 18, Top = 20, Width = 130 });
            txtName = new TextBox { Left = 150, Top = 17, Width = 335 };
            Controls.Add(txtName);

            Controls.Add(new Label { Text = "Endereço do servidor:", Left = 18, Top = 60, Width = 130 });
            txtEndpoint = new TextBox { Left = 150, Top = 57, Width = 335 };
            Controls.Add(txtEndpoint);

            Controls.Add(new Label { Text = "Ex.: https://nome.ts.net", Left = 150, Top = 86, Width = 335, ForeColor = Color.Gray });

            var ok = new Button { Text = "ADICIONAR", DialogResult = DialogResult.OK, Left = 285, Top = 125, Width = 96 };
            var cancel = new Button { Text = "CANCELAR", DialogResult = DialogResult.Cancel, Left = 389, Top = 125, Width = 96 };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
