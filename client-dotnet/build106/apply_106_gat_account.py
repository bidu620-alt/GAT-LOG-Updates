from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
api=root/'client-dotnet/GatTelemetry/ApiClient.cs'
store=root/'client-dotnet/GatTelemetry/ClientStore.cs'
models=root/'client-dotnet/GatTelemetry/Models.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

# ---------- Models ----------
s=models.read_text(encoding='utf-8')
account_model=r'''
    internal sealed class GatAccountCredential
    {
        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("saved_at")]
        public string SavedAt { get; set; }
    }

'''
if 'internal sealed class GatAccountCredential' not in s:
    marker='    internal sealed class ClientSettings\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('ClientSettings model not found')
    s=s[:pos]+account_model+s[pos:]
models.write_text(s,encoding='utf-8')

# ---------- ClientStore ----------
s=store.read_text(encoding='utf-8')
if 'AccountFile =>' not in s:
    s=s.replace('        public static string SettingsFile => Path.Combine(DataDir, "client_settings.json");\n',
                '        public static string SettingsFile => Path.Combine(DataDir, "client_settings.json");\n        public static string AccountFile => Path.Combine(DataDir, "gat_account.json");\n',1)

account_store=r'''
        public static GatAccountCredential LoadAccountCredential()
        {
            Ensure();
            try
            {
                if (!File.Exists(AccountFile)) return null;
                var saved = JsonConvert.DeserializeObject<GatAccountCredential>(File.ReadAllText(AccountFile, Encoding.UTF8));
                if (saved == null || string.IsNullOrWhiteSpace(saved.User) || string.IsNullOrWhiteSpace(saved.Token)) return null;
                byte[] protectedBytes = Convert.FromBase64String(saved.Token);
                byte[] clear = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                saved.Token = Encoding.UTF8.GetString(clear);
                return saved;
            }
            catch (Exception ex)
            {
                Log("LoadAccountCredential falhou: " + ex.Message);
                return null;
            }
        }

        public static void SaveAccountCredential(string user, string token)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(token)) return;
            byte[] clear = Encoding.UTF8.GetBytes(token);
            byte[] protectedBytes = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            var saved = new GatAccountCredential
            {
                User = user.Trim(),
                Token = Convert.ToBase64String(protectedBytes),
                SavedAt = DateTime.UtcNow.ToString("o")
            };
            File.WriteAllText(AccountFile, JsonConvert.SerializeObject(saved, Formatting.Indented), Encoding.UTF8);
        }

        public static void ClearAccountCredential()
        {
            try { if (File.Exists(AccountFile)) File.Delete(AccountFile); } catch { }
        }

'''
if 'LoadAccountCredential()' not in s:
    marker='        public static string GetDeviceId()\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('GetDeviceId not found')
    s=s[:pos]+account_store+s[pos:]
store.write_text(s,encoding='utf-8')

# ---------- ApiClient ----------
s=api.read_text(encoding='utf-8')
if 'GetBearerAsync' not in s:
    marker='        public async Task<ApiResponse> PostAsync(string url, object body, int seconds = 6)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('PostAsync not found')
    bearer=r'''        public async Task<ApiResponse> GetBearerAsync(string url, string token, int seconds = 6)
        {
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token ?? string.Empty);
                    using (var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false))
                    {
                        string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return Build((int)response.StatusCode, text, null);
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse { Error = ex, StatusCode = 0, Text = ex.Message };
            }
        }

'''
    s=s[:pos]+bearer+s[pos:]

old='''        public Task<ApiResponse> LoginAsync(string endpoint, string driver, string deviceId, string token)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            return PostAsync(ep + "/api/client/login", new { driver, device_id = deviceId, token = token ?? string.Empty }, 8);
        }
'''
new='''        public Task<ApiResponse> LoginAsync(string endpoint, string driver, string deviceId, string token, string accountUser, string accountToken)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            return PostAsync(ep + "/api/client/login", new
            {
                driver,
                device_id = deviceId,
                token = token ?? string.Empty,
                account_user = accountUser ?? string.Empty,
                account_token = accountToken ?? string.Empty
            }, 8);
        }

        public Task<ApiResponse> AccountLoginAsync(string authority, string user, string password)
        {
            string ep = ClientStore.NormalizeEndpoint(authority);
            return PostAsync(ep + "/api/account/login", new { user = user ?? string.Empty, password = password ?? string.Empty }, 8);
        }

        public Task<ApiResponse> AccountSessionAsync(string authority, string token)
        {
            string ep = ClientStore.NormalizeEndpoint(authority);
            return GetBearerAsync(ep + "/api/account/session", token, 6);
        }
'''
if old in s:
    s=s.replace(old,new,1)
elif 'AccountLoginAsync' not in s:
    raise SystemExit('LoginAsync block not found')
api.write_text(s,encoding='utf-8')

# ---------- MainForm ----------
s=main.read_text(encoding='utf-8')
if 'AccountAuthority' not in s:
    s=s.replace('        private const string VersionUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json";\n',
                '        private const string VersionUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json";\n        private const string AccountAuthority = "https://douglas.tail4577e8.ts.net";\n',1)

if 'private string _accountUser' not in s:
    s=s.replace('        private string _token = string.Empty;\n',
                '        private string _token = string.Empty;\n        private string _accountUser = string.Empty;\n        private string _accountToken = string.Empty;\n',1)

if 'private TextBox txtAccountUser' not in s:
    s=s.replace('        private ComboBox cmbServers;\n',
                '        private ComboBox cmbServers;\n        private TextBox txtAccountUser;\n        private TextBox txtAccountPassword;\n        private Button btnAccountLogin;\n        private Label lblAccount;\n',1)

# Form size for one compact account row.
s=s.replace('            MinimumSize = new Size(780, 600);\n            Size = new Size(860, 650);',
            '            MinimumSize = new Size(780, 700);\n            Size = new Size(860, 735);')

# Restore account before automatic server waiting.
old_shown='''            Shown += async (s, e) =>
            {
                await RefreshServerInfoAsync(true);
                await CheckUpdateAsync(false);
                if (_settings.AutoConnect && cmbServers.SelectedItem is ServerEntry)
                    BeginWaiting(false);
            };
'''
new_shown='''            Shown += async (s, e) =>
            {
                await RestoreAccountAsync();
                await RefreshServerInfoAsync(true);
                await CheckUpdateAsync(false);
                if (_settings.AutoConnect && AccountReady && cmbServers.SelectedItem is ServerEntry)
                    BeginWaiting(false);
            };
'''
if old_shown in s:
    s=s.replace(old_shown,new_shown,1)
elif 'await RestoreAccountAsync();' not in s:
    raise SystemExit('Shown block not found')

# Insert compact GAT account group before server group and move existing groups down.
old_server='''            var serverBox = NewGroup("SERVIDOR", 24, 88, 800, 150);
'''
account_ui=r'''            var accountBox = NewGroup("CONTA GAT", 24, 88, 800, 90);
            txtAccountUser = new TextBox { Left = 18, Top = 34, Width = 210, BackColor = Color.White, ForeColor = Color.Black };
            txtAccountPassword = new TextBox { Left = 240, Top = 34, Width = 210, BackColor = Color.White, ForeColor = Color.Black, UseSystemPasswordChar = true };
            txtAccountUser.PlaceholderTextCompat("Usuário cadastrado no site");
            txtAccountPassword.PlaceholderTextCompat("Senha");
            btnAccountLogin = MakeButton("ENTRAR NA CONTA", 465, 31, 145, 32, async (sender, e) => await AccountLoginClickedAsync());
            lblAccount = MakeValue("Conta: não conectada", 625, 37, 150);
            accountBox.Controls.Add(txtAccountUser);
            accountBox.Controls.Add(txtAccountPassword);
            accountBox.Controls.Add(btnAccountLogin);
            accountBox.Controls.Add(lblAccount);
            Controls.Add(accountBox);

            var serverBox = NewGroup("SERVIDOR", 24, 190, 800, 150);
'''
# .NET 4.8 TextBox has no PlaceholderText. Replace helper call later with labels or remove.
account_ui=account_ui.replace('            txtAccountUser.PlaceholderTextCompat("Usuário cadastrado no site");\n            txtAccountPassword.PlaceholderTextCompat("Senha");\n','')
if old_server in s:
    s=s.replace(old_server,account_ui,1)
elif 'var accountBox = NewGroup("CONTA GAT"' not in s:
    raise SystemExit('serverBox insertion point not found')

s=s.replace('var sessionBox = NewGroup("CONEXÃO DO MOTORISTA", 24, 250, 800, 145);','var sessionBox = NewGroup("CONEXÃO DO MOTORISTA", 24, 352, 800, 145);')
s=s.replace('var telBox = NewGroup("TELEMETRIA", 24, 407, 800, 148);','var telBox = NewGroup("TELEMETRIA", 24, 509, 800, 148);')
s=s.replace('btnUpdate = MakeButton("VERIFICAR ATUALIZAÇÃO", 24, 570, 220, 32','btnUpdate = MakeButton("VERIFICAR ATUALIZAÇÃO", 24, 672, 220, 32')
s=s.replace('Location = new Point(625, 578)','Location = new Point(625, 680)')
if 'accountBox.Width = width;' not in s:
    s=s.replace('                serverBox.Width = width;\n', '                accountBox.Width = width;\n                serverBox.Width = width;\n',1)

# Require the account before auto/manual waiting and before telemetry tick.
s=s.replace('            if (chkAuto.Checked && cmbServers.SelectedItem is ServerEntry)\n                BeginWaiting(false);',
            '            if (chkAuto.Checked && AccountReady && cmbServers.SelectedItem is ServerEntry)\n                BeginWaiting(false);')

enter_old='''        private void EnterClicked(object sender, EventArgs e)
        {
            if (!(cmbServers.SelectedItem is ServerEntry))
'''
enter_new='''        private void EnterClicked(object sender, EventArgs e)
        {
            if (!AccountReady)
            {
                MessageBox.Show("Entre primeiro com a mesma conta criada no site GAT LOG.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!(cmbServers.SelectedItem is ServerEntry))
'''
if enter_old in s:
    s=s.replace(enter_old,enter_new,1)

begin_old='''        private void BeginWaiting(bool manual)
        {
            var s = cmbServers.SelectedItem as ServerEntry;
'''
begin_new='''        private void BeginWaiting(bool manual)
        {
            if (!AccountReady)
            {
                lblSession.Text = "GAT LOG: entre na Conta GAT";
                lblTelemetry.Text = "Envio: aguardando conta";
                return;
            }
            var s = cmbServers.SelectedItem as ServerEntry;
'''
if begin_old in s:
    s=s.replace(begin_old,begin_new,1)

# Tick guard after busy acquisition.
needle='''            try
            {
                if (cmbServers.SelectedItem is ServerEntry selected)
'''
replacement='''            try
            {
                if (!AccountReady)
                {
                    _loggedIn = false;
                    _waiting = false;
                    lblTelemetry.Text = "Envio: aguardando Conta GAT";
                    return;
                }
                if (cmbServers.SelectedItem is ServerEntry selected)
'''
if needle in s:
    s=s.replace(needle,replacement,1)

# Per-server login includes permanent GAT account identity.
s=s.replace('var r = await _api.LoginAsync(_endpoint, driver, _deviceId, tok);',
            'var r = await _api.LoginAsync(_endpoint, driver, _deviceId, tok, _accountUser, _accountToken);')
s=s.replace('r = await _api.LoginAsync(_endpoint, driver, _deviceId, string.Empty);',
            'r = await _api.LoginAsync(_endpoint, driver, _deviceId, string.Empty, _accountUser, _accountToken);')

# Ensure server really linked the account (requires server 1.0.12).
needle='''            string canonical = ApiClient.Str(r.Json?["driver"]);
            if (string.IsNullOrWhiteSpace(canonical)) canonical = driver;
'''
replacement='''            string linkedAccount = ApiClient.Str(r.Json?["account_user"]);
            if (!string.Equals(linkedAccount, _accountUser, StringComparison.OrdinalIgnoreCase))
            {
                lblSession.Text = "GAT LOG: servidor precisa da versão 1.0.12";
                lblTelemetry.Text = "Envio: conta não vinculada";
                return false;
            }

            string canonical = ApiClient.Str(r.Json?["driver"]);
            if (string.IsNullOrWhiteSpace(canonical)) canonical = driver;
'''
if needle in s:
    s=s.replace(needle,replacement,1)
elif 'servidor precisa da versão 1.0.12' not in s:
    raise SystemExit('canonical login point not found')

# Add account methods immediately before EnterClicked.
marker='        private void EnterClicked(object sender, EventArgs e)\n'
if 'private bool AccountReady' not in s:
    pos=s.find(marker)
    if pos<0: raise SystemExit('EnterClicked marker not found')
    methods=r'''        private bool AccountReady => !string.IsNullOrWhiteSpace(_accountUser) && !string.IsNullOrWhiteSpace(_accountToken);

        private async Task RestoreAccountAsync()
        {
            var saved = ClientStore.LoadAccountCredential();
            if (saved == null)
            {
                SetAccountState(string.Empty, string.Empty);
                return;
            }

            txtAccountUser.Text = saved.User ?? string.Empty;
            var r = await _api.AccountSessionAsync(AccountAuthority, saved.Token);
            if (r.StatusCode == 200 && r.Json != null && ApiClient.Bool(r.Json["ok"]))
            {
                var user = ApiClient.Str(r.Json["user"]);
                if (string.IsNullOrWhiteSpace(user)) user = saved.User;
                SetAccountState(user, saved.Token);
                return;
            }

            ClientStore.ClearAccountCredential();
            SetAccountState(string.Empty, string.Empty);
        }

        private async Task AccountLoginClickedAsync()
        {
            var user = (txtAccountUser.Text ?? string.Empty).Trim();
            var password = txtAccountPassword.Text ?? string.Empty;
            if (user.Length == 0 || password.Length == 0)
            {
                MessageBox.Show("Informe o usuário e a senha criados no site GAT LOG.", "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnAccountLogin.Enabled = false;
            btnAccountLogin.Text = "ENTRANDO...";
            try
            {
                var r = await _api.AccountLoginAsync(AccountAuthority, user, password);
                if (r.StatusCode != 200 || r.Json == null || !ApiClient.Bool(r.Json["ok"]))
                {
                    lblAccount.Text = "Conta: login inválido";
                    MessageBox.Show("Usuário ou senha inválidos. Use a mesma conta cadastrada no site.", "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var canonical = ApiClient.Str(r.Json["user"]);
                var token = ApiClient.Str(r.Json["token"]);
                if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("O servidor não retornou a sessão da Conta GAT.");

                ClientStore.SaveAccountCredential(canonical, token);
                txtAccountUser.Text = canonical;
                txtAccountPassword.Clear();
                SetAccountState(canonical, token);
                _waiting = false;
                _loggedIn = false;
                _driver = string.Empty;
                _token = string.Empty;
                lblSession.Text = "GAT LOG: conta reconhecida, aguardando sessão";
                if (chkAuto.Checked && cmbServers.SelectedItem is ServerEntry)
                    BeginWaiting(false);
            }
            catch (Exception ex)
            {
                ClientStore.Log("Conta GAT: " + ex);
                MessageBox.Show("Falha ao entrar na Conta GAT: " + ex.Message, "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAccountLogin.Enabled = true;
                btnAccountLogin.Text = "ENTRAR NA CONTA";
            }
        }

        private void SetAccountState(string user, string token)
        {
            _accountUser = (user ?? string.Empty).Trim();
            _accountToken = token ?? string.Empty;
            if (AccountReady)
            {
                lblAccount.Text = "Conta: @" + _accountUser;
                lblAccount.ForeColor = Color.LightGreen;
            }
            else
            {
                lblAccount.Text = "Conta: não conectada";
                lblAccount.ForeColor = Color.Gold;
                lblSession.Text = "GAT LOG: entre na Conta GAT";
                lblTelemetry.Text = "Envio: aguardando conta";
            }
        }

'''
    s=s[:pos]+methods+s[pos:]

# Version bump after preserved 1.0.5 patch.
s=s.replace('private const string CurrentVersion = "1.0.5";', 'private const string CurrentVersion = "1.0.6";')
s=s.replace('Text = "GAT Telemetria C# 1.0.5 TESTE";', 'Text = "GAT Telemetria C# 1.0.6 TESTE";')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.5.0</Version>','<Version>1.0.6.0</Version>')
s=s.replace('<FileVersion>1.0.5.0</FileVersion>','<FileVersion>1.0.6.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.5.0</AssemblyVersion>','<AssemblyVersion>1.0.6.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.5?', 'Atualizar GAT Telemetria para 1.0.6?')
s=s.replace('GAT Telemetria C# 1.0.5 atualizado.', 'GAT Telemetria C# 1.0.6 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.5_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.6_TESTE')
installer_proj.write_text(s,encoding='utf-8')

print('GAT Telemetria 1.0.6 Conta GAT patch applied')
