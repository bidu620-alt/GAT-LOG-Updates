from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
api=root/'client-dotnet/GatTelemetry/ApiClient.cs'
tele=root/'client-dotnet/GatTelemetry/TelemetryEngine.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

# Telemetry: expose World of Trucks market in the normalized payload.
s=tele.read_text(encoding='utf-8')
needle='            CopyAlias(m, "gameplay.onJob", "on_job");\n'
if 'job_market' not in s:
    if needle not in s: raise SystemExit('telemetry alias point not found')
    s=s.replace(needle, '            CopyAlias(m, "job.market", "job_market");\n'+needle, 1)
tele.write_text(s,encoding='utf-8')

# ApiClient: authenticated progress telemetry goes to the central account authority.
s=api.read_text(encoding='utf-8')
methods=r'''
        public async Task<ApiResponse> PostBearerAsync(string url, string token, object body, int seconds = 6)
        {
            try
            {
                string json = JsonConvert.SerializeObject(body);
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token ?? string.Empty);
                    request.Content = content;
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

        public Task<ApiResponse> SendAccountTelemetryAsync(string authority, string accountToken, string driver, JObject telemetry)
        {
            string ep = ClientStore.NormalizeEndpoint(authority);
            return PostBearerAsync(ep + "/api/account/telemetry", accountToken, new JObject
            {
                ["driver"] = driver ?? string.Empty,
                ["telemetry"] = telemetry
            }, 5);
        }

'''
if 'SendAccountTelemetryAsync' not in s:
    marker='        public static bool Bool(JToken token)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('ApiClient Bool marker not found')
    s=s[:pos]+methods+s[pos:]
api.write_text(s,encoding='utf-8')

# MainForm: send a central progress snapshot every ~2 seconds after normal telemetry succeeds.
s=main.read_text(encoding='utf-8')
if 'private DateTime _lastAccountTelemetry' not in s:
    s=s.replace('        private DateTime _lastTelemetry = DateTime.MinValue;\n', '        private DateTime _lastTelemetry = DateTime.MinValue;\n        private DateTime _lastAccountTelemetry = DateTime.MinValue;\n',1)

old='''                    lblTelemetry.Text = IsAccepted(sent) ? "Envio: ONLINE" : "Envio: falha ao enviar";
                    if (!IsAccepted(sent)) ClientStore.Log("telemetria falhou: " + sent.StatusCode + " " + sent.Text);
'''
new='''                    lblTelemetry.Text = IsAccepted(sent) ? "Envio: ONLINE" : "Envio: falha ao enviar";
                    if (!IsAccepted(sent)) ClientStore.Log("telemetria falhou: " + sent.StatusCode + " " + sent.Text);

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
if old in s:
    s=s.replace(old,new,1)
elif 'SendAccountTelemetryAsync(AccountAuthority' not in s:
    raise SystemExit('telemetry send block not found')

s=s.replace('private const string CurrentVersion = "1.0.6";', 'private const string CurrentVersion = "1.0.7";')
s=s.replace('GAT Telemetria C# 1.0.6 TESTE', 'GAT Telemetria C# 1.0.7 TESTE')
s=s.replace('C# WinForms 1.0.6', 'C# WinForms 1.0.7')
main.write_text(s,encoding='utf-8')

# Assembly version and updater filename.
s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.0.0</Version>','<Version>1.0.7.0</Version>')
s=s.replace('<FileVersion>1.0.0.0</FileVersion>','<FileVersion>1.0.7.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.0.0</AssemblyVersion>','<AssemblyVersion>1.0.7.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Instalar GAT Telemetria C# 1.0.0 TESTE?','Atualizar GAT Telemetria C# para 1.0.7?')
s=s.replace('GAT Telemetria C# 1.0.0 TESTE instalado.','GAT Telemetria C# 1.0.7 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_SETUP_1.0.0_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.7_TESTE')
installer_proj.write_text(s,encoding='utf-8')
print('GAT Telemetria 1.0.7 mission progress applied')
