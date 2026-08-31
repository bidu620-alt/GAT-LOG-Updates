from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
api=root/'client-dotnet/GatTelemetry/ApiClient.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

# A sessão da Conta GAT no Worker Cloudflare é POST e aceita o token no corpo.
a=api.read_text(encoding='utf-8')
a=a.replace('return GetBearerAsync(ep + "/api/account/session", token, 6);',
            'return PostAsync(ep + "/api/account/session", new { token = token ?? string.Empty }, 6);')
api.write_text(a,encoding='utf-8')

m=main.read_text(encoding='utf-8')
old='''            string centralDriver = string.IsNullOrWhiteSpace(_driver) ? _accountUser : _driver;
            var progress = await _api.SendAccountTelemetryAsync(AccountAuthority, _accountToken, centralDriver, tele);
'''
new='''            // Na Central Cloudflare a identidade do motorista e a propria Conta GAT.
            // O token de dispositivo e separado da sessao da conta e so nasce depois
            // que o codigo de 8 caracteres e confirmado no site.
            string centralDriver = _accountUser;
            var centralCredential = ClientStore.FindCredential(AccountAuthority, centralDriver);
            string centralClientToken = ClientStore.GetPlainToken(centralCredential);

            if (string.IsNullOrWhiteSpace(centralClientToken))
            {
                var link = await _api.LoginAsync(AccountAuthority, centralDriver, _deviceId, string.Empty, _accountUser, _accountToken);
                if (link.StatusCode == 428 && link.Json != null)
                {
                    string code = ApiClient.Str(link.Json["pairing_code"]);
                    lblAccount.Text = string.IsNullOrWhiteSpace(code) ? "Conta: @" + _accountUser : "Vincular PC: " + code;
                    lblAccount.ForeColor = Color.Gold;
                    lblTelemetry.Text = string.IsNullOrWhiteSpace(code)
                        ? "Central GAT: computador ainda nao vinculado"
                        : "Central GAT: digite o codigo " + code + " no site";
                    return;
                }
                if (link.StatusCode == 200 && link.Json != null && ApiClient.Bool(link.Json["ok"]))
                {
                    centralClientToken = ApiClient.Str(link.Json["token"]);
                    if (!string.IsNullOrWhiteSpace(centralClientToken))
                    {
                        ClientStore.SaveCredential(AccountAuthority, centralDriver, centralClientToken);
                        lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
                        lblAccount.ForeColor = Color.LightGreen;
                    }
                }
                if (string.IsNullOrWhiteSpace(centralClientToken))
                {
                    lblTelemetry.Text = link.StatusCode == 0
                        ? "Central GAT: reconectando..."
                        : "Central GAT: falha ao vincular HTTP " + link.StatusCode;
                    return;
                }
            }

            var progress = await _api.SendTelemetryAsync(AccountAuthority, centralDriver, _deviceId, centralClientToken, tele);
'''
if old not in m:
    raise SystemExit('bloco de envio central 1.0.19 nao encontrado')
m=m.replace(old,new,1)

# O Worker retorna mission_event. Convertemos para os aliases que a interface
# existente ja usa, sem alterar diario, voz, mapa, integridade ou regras de carga.
needle='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                bool startedNow = ApiClient.Bool(progress.Json["started"]);
'''
repl='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                var missionEvent = progress.Json["mission_event"] as JObject;
                if (missionEvent != null)
                {
                    string eventType = ApiClient.Str(missionEvent["type"]);
                    if (string.Equals(eventType, "mission_in_progress", StringComparison.OrdinalIgnoreCase))
                    {
                        progress.Json["started"] = true;
                        if (missionEvent["mission"] != null) progress.Json["mission"] = missionEvent["mission"];
                    }
                    if (string.Equals(eventType, "delivery_completed", StringComparison.OrdinalIgnoreCase))
                        progress.Json["completed_now"] = true;
                }
                lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
                lblAccount.ForeColor = Color.LightGreen;
                bool startedNow = ApiClient.Bool(progress.Json["started"]);
'''
if needle not in m:
    raise SystemExit('retorno central nao encontrado')
m=m.replace(needle,repl,1)

# Se o token do dispositivo foi revogado, pede novo vinculo em vez de acusar
# sessao da Conta GAT expirada.
old401='''            if (progress.StatusCode == 401)
                lblTelemetry.Text = "Central GAT: sessão da conta expirada";
'''
new401='''            if (progress.StatusCode == 401)
            {
                var relink = await _api.LoginAsync(AccountAuthority, centralDriver, _deviceId, centralClientToken, _accountUser, _accountToken);
                if (relink.StatusCode == 428 && relink.Json != null)
                {
                    string code = ApiClient.Str(relink.Json["pairing_code"]);
                    lblAccount.Text = "Vincular PC: " + code;
                    lblAccount.ForeColor = Color.Gold;
                    lblTelemetry.Text = "Central GAT: digite o codigo " + code + " no site";
                }
                else
                    lblTelemetry.Text = "Central GAT: dispositivo precisa ser vinculado";
            }
'''
if old401 not in m:
    raise SystemExit('tratamento 401 nao encontrado')
m=m.replace(old401,new401,1)

m=m.replace('private const string CurrentVersion = "1.0.19";', 'private const string CurrentVersion = "1.0.20";')
m=m.replace('GAT Telemetria C# 1.0.19 TESTE','GAT Telemetria C# 1.0.20 TESTE')
m=m.replace('C# WinForms 1.0.19','C# WinForms 1.0.20')
main.write_text(m,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.19.0','1.0.20.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.19?','Atualizar GAT Telemetria para 1.0.20?')
i=i.replace('GAT Telemetria C# 1.0.19 atualizado.','GAT Telemetria C# 1.0.20 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.19_CLOUDFLARE_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.20_VINCULO_CLOUDFLARE_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

checks=[
    (main,'CurrentVersion = "1.0.20"'),
    (main,'Vincular PC:'),
    (main,'SendTelemetryAsync(AccountAuthority'),
    (main,'mission_event'),
    (main,'PC vinculado'),
    (api,'PostAsync(ep + "/api/account/session"'),
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'):
        raise SystemExit('patch 1.0.20 incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.20_VINCULO_CLOUDFLARE_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome do atualizador 1.0.20 nao aplicado')
print('GAT Telemetria 1.0.20: pairing de 8 caracteres e envio /api/client/telemetry Cloudflare aplicados')
