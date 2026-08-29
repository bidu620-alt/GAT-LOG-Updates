from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=main.read_text(encoding='utf-8')

# Keep the saved GAT account on temporary central outages. Only 401 truly expires it.
old='''            ClientStore.ClearAccountCredential();
            SetAccountState(string.Empty, string.Empty);
        }

        private async Task AccountLoginClickedAsync()
'''
new='''            if (r.StatusCode == 401)
            {
                ClientStore.ClearAccountCredential();
                SetAccountState(string.Empty, string.Empty);
                return;
            }

            // Central temporarily offline/unreachable: keep the protected token
            // and retry automatically. Do not force the driver to type password again.
            SetAccountState(saved.User, saved.Token);
            lblAccount.Text = "Conta: @" + saved.User + " • central reconectando";
            lblAccount.ForeColor = Color.Gold;
        }

        private async Task AccountLoginClickedAsync()
'''
if old in s:
    s=s.replace(old,new,1)
elif 'central reconectando' not in s:
    raise SystemExit('RestoreAccountAsync patch point not found')

# Central telemetry is now a first-class path and does not depend on a convoy server.
methods=r'''        private async Task SendCentralTelemetryAsync()
        {
            if (!AccountReady) return;
            if ((DateTime.UtcNow - _lastAccountTelemetry).TotalMilliseconds < 1200) return;

            JObject tele = await _telemetry.ReadAsync();
            _lastAccountTelemetry = DateTime.UtcNow;
            if (tele == null)
            {
                lblTruck.Text = "TruckSim GPS: aguardando";
                lblTelemetry.Text = "Central GAT: aguardando ETS2";
                return;
            }

            tele["gat_account_user"] = _accountUser;
            tele["gat_client_version"] = CurrentVersion;
            lblTruck.Text = "TruckSim GPS: CONECTADO";
            UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(tele));

            string centralDriver = string.IsNullOrWhiteSpace(_driver) ? _accountUser : _driver;
            var progress = await _api.SendAccountTelemetryAsync(AccountAuthority, _accountToken, centralDriver, tele);
            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                if (ApiClient.Bool(progress.Json["completed_now"]))
                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO CONCLUÍDA";
                else if (ApiClient.Bool(progress.Json["started"]))
                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO INICIADA";
                else
                    lblTelemetry.Text = "Central GAT: ONLINE";
                return;
            }

            if (progress.StatusCode == 401)
                lblTelemetry.Text = "Central GAT: sessão da conta expirada";
            else if (progress.StatusCode == 0)
                lblTelemetry.Text = "Central GAT: reconectando...";
            else if (progress.StatusCode == 404)
                lblTelemetry.Text = "Central GAT: atualize o servidor central";
            else
                lblTelemetry.Text = "Central GAT: falha HTTP " + progress.StatusCode;
        }

'''
if 'private async Task SendCentralTelemetryAsync()' not in s:
    marker='        private void EnterClicked(object sender, EventArgs e)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('EnterClicked marker not found')
    s=s[:pos]+methods+s[pos:]

# Run central telemetry before every optional convoy/server check.
old='''                if (!AccountReady)
                {
                    _loggedIn = false;
                    _waiting = false;
                    lblTelemetry.Text = "Envio: aguardando Conta GAT";
                    return;
                }
                if (cmbServers.SelectedItem is ServerEntry selected)
'''
new='''                if (!AccountReady)
                {
                    _loggedIn = false;
                    _waiting = false;
                    lblTelemetry.Text = "Central GAT: aguardando conta";
                    return;
                }

                // Independent path: works playing solo, in another multiplayer,
                // or while the selected GAT convoy server is unavailable.
                await SendCentralTelemetryAsync();

                if (cmbServers.SelectedItem is ServerEntry selected)
'''
if old in s:
    s=s.replace(old,new,1)
elif 'await SendCentralTelemetryAsync();' not in s:
    raise SystemExit('Tick AccountReady block not found')

# Remove the old account telemetry block that only ran after the convoy send succeeded.
old_block='''
                    if (IsAccepted(sent) && AccountReady && (DateTime.UtcNow - _lastAccountTelemetry).TotalMilliseconds >= 1800)
                    {
                        var progress = await _api.SendAccountTelemetryAsync(AccountAuthority, _accountToken, _driver, tele);
                        _lastAccountTelemetry = DateTime.UtcNow;
                        if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
                        {
                            if (ApiClient.Bool(progress.Json["completed_now"]))
                                lblTelemetry.Text = "Envio: ONLINE • MISSÃO GAT CONCLUÍDA";
                            else if (ApiClient.Bool(progress.Json["started"]))
                                lblTelemetry.Text = "Envio: ONLINE • MISSÃO GAT INICIADA";
                        }
                        else if (progress.StatusCode == 404)
                        {
                            ClientStore.Log("progresso GAT: servidor central precisa da versão 1.0.13");
                        }
                        else if (progress.StatusCode != 0 && progress.StatusCode != 401)
                        {
                            ClientStore.Log("progresso GAT falhou: " + progress.StatusCode + " " + progress.Text);
                        }
                    }
'''
if old_block in s:
    s=s.replace(old_block,'\n',1)

# Optional convoy status must not overwrite Central GAT status.
for line in [
    '                    lblTelemetry.Text = "Envio: aguardando servidor";\n',
    '                    lblTelemetry.Text = "Envio: aguardando sala";\n',
    '                        lblTelemetry.Text = "Envio: aguardando sessão";\n',
    '                            lblTelemetry.Text = "Envio: heartbeat recusado";\n',
]:
    s=s.replace(line,'')
old_send='''                    lblTelemetry.Text = IsAccepted(sent) ? "Envio: ONLINE" : "Envio: falha ao enviar";
                    if (!IsAccepted(sent)) ClientStore.Log("telemetria falhou: " + sent.StatusCode + " " + sent.Text);
'''
new_send='''                    if (!IsAccepted(sent)) ClientStore.Log("telemetria opcional do comboio falhou: " + sent.StatusCode + " " + sent.Text);
'''
if old_send in s: s=s.replace(old_send,new_send,1)

# Clarify that server/convoy connection is optional for GAT Central.
s=s.replace('var serverBox = NewGroup("SERVIDOR", 24, 190, 800, 150);','var serverBox = NewGroup("COMBOIO / SERVIDOR (OPCIONAL)", 24, 190, 800, 150);')
s=s.replace('var sessionBox = NewGroup("CONEXÃO DO MOTORISTA", 24, 352, 800, 145);','var sessionBox = NewGroup("COMBOIO (OPCIONAL)", 24, 352, 800, 145);')

s=s.replace('private const string CurrentVersion = "1.0.8";', 'private const string CurrentVersion = "1.0.9";')
s=s.replace('GAT Telemetria C# 1.0.8 TESTE','GAT Telemetria C# 1.0.9 TESTE')
s=s.replace('C# WinForms 1.0.8','C# WinForms 1.0.9')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.8.0</Version>','<Version>1.0.9.0</Version>')
s=s.replace('<FileVersion>1.0.8.0</FileVersion>','<FileVersion>1.0.9.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.8.0</AssemblyVersion>','<AssemblyVersion>1.0.9.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.8?','Atualizar GAT Telemetria para 1.0.9?')
s=s.replace('GAT Telemetria C# 1.0.8 atualizado.','GAT Telemetria C# 1.0.9 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.8_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.9_TESTE')
installer_proj.write_text(s,encoding='utf-8')
print('GAT Telemetria 1.0.9 independent Central GAT applied')
