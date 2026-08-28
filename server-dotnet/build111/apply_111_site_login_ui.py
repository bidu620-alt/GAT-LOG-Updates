from pathlib import Path

ui = Path('server-dotnet/GatLogServer/MainForm.cs')
auth = Path('server-dotnet/GatLogServer/AuthService.cs')
if not ui.exists() or not auth.exists():
    raise SystemExit('fontes C# do servidor nao encontradas')

# ---------------- AuthService: separate site credential file ----------------
s = auth.read_text(encoding='utf-8')
if 'public static string SiteAuthPath' not in s:
    s = s.replace(
        'public static string AuthPath => Path.Combine(DataDir, "native_auth.json");\n',
        'public static string AuthPath => Path.Combine(DataDir, "native_auth.json");\n        public static string SiteAuthPath => Path.Combine(DataDir, "site_auth.json");\n',
        1,
    )

insert_before = '        private static NativeAuth NewAuth(string user, string password)\n'
site_methods = r'''        public static NativeAuth GetSiteAuth()
        {
            Directory.CreateDirectory(DataDir);
            try
            {
                if (!File.Exists(SiteAuthPath)) return null;
                var a = JsonConvert.DeserializeObject<NativeAuth>(File.ReadAllText(SiteAuthPath, Encoding.UTF8));
                if (a != null && !string.IsNullOrWhiteSpace(a.User) && !string.IsNullOrWhiteSpace(a.Salt) && !string.IsNullOrWhiteSpace(a.Hash))
                    return a;
            }
            catch { }
            return null;
        }

        public static void ChangeSite(string user, string password)
        {
            user = (user ?? "").Trim();
            if (user.Length == 0) throw new InvalidOperationException("Usuário do site vazio.");
            if ((password ?? "").Length < 6) throw new InvalidOperationException("A senha do site precisa ter pelo menos 6 caracteres.");
            SaveSite(NewAuth(user, password));
        }

        public static void RemoveSite()
        {
            try { if (File.Exists(SiteAuthPath)) File.Delete(SiteAuthPath); } catch { }
        }

        private static void SaveSite(NativeAuth auth)
        {
            Directory.CreateDirectory(DataDir);
            var tmp = SiteAuthPath + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(auth, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(SiteAuthPath)) File.Delete(SiteAuthPath);
            File.Move(tmp, SiteAuthPath);
        }

'''
if 'public static NativeAuth GetSiteAuth()' not in s:
    if insert_before not in s:
        raise SystemExit('ponto NewAuth nao encontrado')
    s = s.replace(insert_before, site_methods + insert_before, 1)
auth.write_text(s, encoding='utf-8')

# ---------------- MainForm: site access card ----------------
s = ui.read_text(encoding='utf-8')
field = '        private TextBox _accUser, _accCurrent, _accNew, _accConfirm;\n'
if field in s and '_siteUser' not in s:
    s = s.replace(field, field + '        private TextBox _siteUser, _sitePassword, _siteConfirm;\n        private Label _siteState;\n', 1)

old_build = '''            card.Controls.Add(new Label { Text = "A senha continua usando o mesmo formato criptográfico das versões anteriores.", ForeColor = Muted, Location = new Point(30, 365), Size = new Size(650, 35) });
        }
'''
new_build = '''            card.Controls.Add(new Label { Text = "A senha continua usando o mesmo formato criptográfico das versões anteriores.", ForeColor = Muted, Location = new Point(30, 365), Size = new Size(650, 35) });

            var siteCard = MakeCard(10, 495, 760, 305); p.Controls.Add(siteCard);
            siteCard.Controls.Add(Title("ACESSO DO SITE", 20, 18, 500));
            siteCard.Controls.Add(new Label { Text = "Login separado usado somente para liberar o painel do site.", ForeColor = Muted, Location = new Point(30, 55), Size = new Size(650, 28) });
            _siteUser = AddField(siteCard, "Usuário do site", 30, 92, 300);
            _sitePassword = AddField(siteCard, "Senha do site", 390, 92, 300); _sitePassword.UseSystemPasswordChar = true;
            _siteConfirm = AddField(siteCard, "Confirmar senha", 30, 180, 300); _siteConfirm.UseSystemPasswordChar = true;
            var siteSave = MakeButton("SALVAR ACESSO", Blue, 390, 180, 145, 48);
            siteSave.Click += (s, e) => SaveSiteAccess(); siteCard.Controls.Add(siteSave);
            var siteRemove = MakeButton("REMOVER", Red, 545, 180, 145, 48);
            siteRemove.Click += (s, e) => RemoveSiteAccess(); siteCard.Controls.Add(siteRemove);
            _siteState = new Label { Text = "Site: NÃO CONFIGURADO", ForeColor = Color.Gold, Location = new Point(390, 246), Size = new Size(300, 30), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            siteCard.Controls.Add(_siteState);
        }
'''
if 'Title("ACESSO DO SITE"' not in s:
    if old_build not in s:
        raise SystemExit('fim BuildAccount nao encontrado')
    s = s.replace(old_build, new_build, 1)

apply_line = '            if (_accUser != null) _accUser.Text = AuthService.EnsureAuth().User;\n'
apply_extra = '''            if (_siteUser != null)
            {
                var siteAuth = AuthService.GetSiteAuth();
                _siteUser.Text = siteAuth?.User ?? "";
                _siteState.Text = siteAuth == null ? "Site: NÃO CONFIGURADO" : "Site: LOGIN ATIVO";
                _siteState.ForeColor = siteAuth == null ? Color.Gold : Color.FromArgb(24, 235, 123);
            }
'''
if apply_extra not in s:
    if apply_line not in s:
        raise SystemExit('ApplyConfig conta nao encontrado')
    s = s.replace(apply_line, apply_line + apply_extra, 1)

marker = '        private void CopyCommand(string command, string message)\n'
methods = r'''        private void SaveSiteAccess()
        {
            try
            {
                if (_sitePassword.Text != _siteConfirm.Text) throw new InvalidOperationException("A confirmação da senha do site não confere.");
                AuthService.ChangeSite(_siteUser.Text.Trim(), _sitePassword.Text);
                _sitePassword.Clear(); _siteConfirm.Clear();
                var a = AuthService.GetSiteAuth();
                _siteUser.Text = a?.User ?? "";
                _siteState.Text = "Site: LOGIN ATIVO";
                _siteState.ForeColor = Color.FromArgb(24, 235, 123);
                MessageBox.Show(this, "Login do site salvo. Use este mesmo usuário e senha nos outros GAT Servers para ter um único acesso.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void RemoveSiteAccess()
        {
            if (MessageBox.Show(this, "Remover o login de acesso do site neste servidor?", "GAT-LOG", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            AuthService.RemoveSite();
            _siteUser.Clear(); _sitePassword.Clear(); _siteConfirm.Clear();
            _siteState.Text = "Site: NÃO CONFIGURADO";
            _siteState.ForeColor = Color.Gold;
        }

'''
if 'private void SaveSiteAccess()' not in s:
    if marker not in s:
        raise SystemExit('ponto CopyCommand nao encontrado')
    s = s.replace(marker, methods + marker, 1)

ui.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.11 site access UI applied')
