param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub42 = Get-ChildItem $rootPath -Filter 'MainForm.Hub042.cs' -Recurse | Select-Object -First 1
$hub44 = Get-ChildItem $rootPath -Filter 'MainForm.Hub044.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub41 -or -not $hub42 -or -not $hub44 -or -not $hub45 -or -not $radio) {
    throw 'Fonte 1.0.48 incompleto para aplicar a atualizacao 1.0.49.'
}

$mainText = Get-Content $main.FullName -Raw
$hub41Text = Get-Content $hub41.FullName -Raw
$hub42Text = Get-Content $hub42.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$hub45Text = Get-Content $hub45.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

# Versao 1.0.49.
$mainText = $mainText.Replace('CurrentVersion = "1.0.48.0"', 'CurrentVersion = "1.0.49.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.48"', 'Text = "Cliente 1.0.49"')
$mainText = $mainText.Replace('HUB 1.0.48:', 'HUB 1.0.49:')
foreach ($name in @('hub41Text','hub42Text','hub44Text','hub45Text')) {
    $v = Get-Variable $name -ValueOnly
    $v = $v.Replace('GAT Telemetria BETA 1.0.48', 'GAT Telemetria BETA 1.0.49')
    $v = $v.Replace('Cliente 1.0.48 TESTE', 'Cliente 1.0.49 TESTE')
    $v = $v.Replace('Cliente: 1.0.48 TESTE', 'Cliente: 1.0.49 TESTE')
    Set-Variable $name $v
}

# ---------------------------------------------------------------------------
# HOME: usa a lista publica da Central GAT, que ja contem a telemetria ao vivo
# de todos os motoristas conectados. A versao anterior consultava apenas a lista
# do servidor selecionado e depois preenchia os dados detalhados somente do PC
# local, por isso normalmente aparecia apenas o proprio motorista.
# ---------------------------------------------------------------------------
$refreshStart = $hub45Text.IndexOf('    private async Task RefreshDrivers045(bool force)')
$refreshEnd = $hub45Text.IndexOf('    private static string ValueAfter045(', $refreshStart)
if ($refreshStart -lt 0 -or $refreshEnd -lt 0) { throw 'RefreshDrivers045 nao encontrado.' }
$newRefresh = @'
    private async Task RefreshDrivers045(bool force)
    {
        if (_homeDrivers045 == null || _homeDrivers045.IsDisposed || _homeDriversBusy045) return;
        if (!force && (DateTime.UtcNow - _homeDriversLast045).TotalSeconds < 3.0) return;
        _homeDriversBusy045 = true;
        _homeDriversLast045 = DateTime.UtcNow;
        try
        {
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
                _homeDrivers045.Rows.Add(
                    name,
                    inRoute ? "● Em rota" : "● Online",
                    cargo,
                    destination,
                    speedText,
                    remainingText
                );
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
                _homeDrivers045.Rows.Add(current, inRoute ? "● Em rota" : "● Online",
                    string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo,
                    string.IsNullOrWhiteSpace(destination) ? "—" : destination,
                    string.IsNullOrWhiteSpace(speed) ? "0 km/h" : speed,
                    string.IsNullOrWhiteSpace(remaining) ? "—" : remaining);
                onlineCount = 1;
                if (inRoute) routeCount = 1;
            }

            if (_homeDriversCount045 != null)
                _homeDriversCount045.Text = onlineCount + " online  •  " + routeCount + " em rota";
        }
        catch { }
        finally { _homeDriversBusy045 = false; }
    }

'@
$hub45Text = $hub45Text.Substring(0, $refreshStart) + $newRefresh + $hub45Text.Substring($refreshEnd)

# ---------------------------------------------------------------------------
# RADIO: mantem somente CANAL GAT + MEU VIDEO. O Canal GAT agora pode ser
# alterado por Admin/Moderador dentro do proprio Telemetria usando a sessao da
# Conta GAT e o endpoint que ja existe na Central 1.0.62.
# ---------------------------------------------------------------------------
if ($radioText -notmatch 'using System\.Text;') {
    $radioText = $radioText.Replace('using System.Net.Http;', "using System.Net.Http;`r`nusing System.Text;")
}

# Campos de conta e endpoint administrativo.
$fieldMarker = '    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };'
if (-not $radioText.Contains($fieldMarker)) { throw 'HttpClient da Radio nao encontrado.' }
if ($radioText -notlike '*_accountToken049*') {
    $fields = @'
    private const string ChannelAdminEndpoint049 = "https://api.gatlogets2.com.br/api/site/admin/radio";
    private const string AccountSessionEndpoint049 = "https://api.gatlogets2.com.br/api/account/session";
    private string _accountUser049 = string.Empty;
    private string _accountToken049 = string.Empty;
    private string _accountRole049 = string.Empty;
'@
    $radioText = $radioText.Replace($fieldMarker, $fieldMarker + "`r`n" + $fields.TrimEnd())
}

# O terceiro botao deixa de existir visualmente.
$radioText = $radioText.Replace('Controls.Add(_siteWeb);', 'Controls.Add(_siteWeb); _siteWeb.Visible = false; _siteWeb.Enabled = false;')
$radioText = $radioText.Replace('Escolha o Canal GAT, use seu vídeo pessoal ou abra um site no player interno.', 'Escolha o Canal GAT para todos ou o MEU VÍDEO somente para você.')
$radioText = $radioText.Replace('Escolha o Canal GAT, use sua rádio pessoal ou abra um site no player interno.', 'Escolha o Canal GAT para todos ou o MEU VÍDEO somente para você.')

# O botao principal usa o campo de acordo com a aba atual.
$oldLoad = '_loadPersonal.Click += async delegate { if (_webMode) await LoadWebFromInputAsync(); else await LoadPersonalFromInputAsync(); };'
$newLoad = '_loadPersonal.Click += async delegate { if (_personalMode) await LoadPersonalFromInputAsync(); else await SaveChannel049Async(); };'
if ($radioText.Contains($oldLoad)) { $radioText = $radioText.Replace($oldLoad, $newLoad) }
elseif ($radioText -notlike '*SaveChannel049Async()*') { throw 'Clique do botao CARREGAR da Radio nao encontrado.' }

# Modos compartilhados: web antigo passa automaticamente para Canal GAT.
$radioText = $radioText.Replace('if (mode != "gat" && mode != "mine" && mode != "web") return;', 'if (mode != "gat" && mode != "mine") return;')
$radioText = $radioText.Replace('return mode == "mine" || mode == "web" ? mode : "gat";', 'return mode == "mine" ? "mine" : "gat";')
$radioText = $radioText.Replace('_webMode = sharedMode041 == "web";', '_webMode = false;')
$radioText = $radioText.Replace('else if (_webMode) _personalInput.Text = _webUrl;', '')

$syncStart = $radioText.IndexOf('    private async Task SyncSharedMediaMode041()')
$syncEnd = $radioText.IndexOf('    internal string HubNowPlaying042', $syncStart)
if ($syncStart -ge 0 -and $syncEnd -gt $syncStart) {
    $newSync = @'
    private async Task SyncSharedMediaMode041()
    {
        string mode = ReadSharedMediaMode041();
        if (mode == "mine")
        {
            if (!_personalMode || _webMode) await SwitchModeAsync(true);
            return;
        }
        if (_personalMode || _webMode) await SwitchModeAsync(false);
    }

'@
    $radioText = $radioText.Substring(0, $syncStart) + $newSync + $radioText.Substring($syncEnd)
}

# UI dos dois modos.
$applyStart = $radioText.IndexOf('    private void ApplyModeUi()')
$applyEnd = $radioText.IndexOf('    private bool ActiveAvailable()', $applyStart)
if ($applyStart -lt 0 -or $applyEnd -lt 0) { throw 'ApplyModeUi da Radio nao encontrado.' }
$newApply = @'
    private void ApplyModeUi()
    {
        _webMode = false;
        bool channelMode = !_personalMode;
        _channelGat.BackColor = channelMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _myRadio.BackColor = _personalMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _siteWeb.Visible = false;
        _siteWeb.Enabled = false;

        Control volumeLabel = Controls["volumeLabel"];
        _personalLabel.Visible = true;
        _personalInput.Visible = true;
        _loadPersonal.Visible = true;
        _toggle.Visible = true;
        _openYoutube.Visible = true;
        _fullScreen.Visible = true;
        _volume.Visible = true;
        if (volumeLabel != null) volumeLabel.Visible = true;

        if (channelMode)
        {
            _description.Text = "CANAL GAT toca para todos os motoristas. Admin/Moderador define a programação aqui no Telemetria.";
            _personalLabel.Text = CanEditChannel049()
                ? "Link do CANAL GAT para todos (YouTube vídeo/playlist ou Rádio Online MP3/AAC):"
                : "CANAL GAT • programação compartilhada com todos os motoristas:";
            _personalInput.Enabled = CanEditChannel049();
            _loadPersonal.Enabled = CanEditChannel049();
            _loadPersonal.Text = "SALVAR P/ TODOS";
            if (!_personalInput.Focused) _personalInput.Text = _serverSourceUrl ?? string.Empty;
        }
        else
        {
            _description.Text = "MEU VÍDEO é individual: a fonte fica somente neste PC e não altera o Canal GAT dos outros motoristas.";
            _personalLabel.Text = "Seu vídeo/playlist ou Rádio Online (somente neste PC):";
            _personalInput.Enabled = true;
            _loadPersonal.Enabled = true;
            _loadPersonal.Text = "CARREGAR MEU VÍDEO";
            if (!_personalInput.Focused) _personalInput.Text = _personalSourceUrl ?? string.Empty;
        }

        _openYoutube.Text = "ABRIR FONTE";
        UpdateActiveSourceUi();
    }

'@
$radioText = $radioText.Substring(0, $applyStart) + $newApply + $radioText.Substring($applyEnd)

# Troca Canal GAT <-> Meu Video sem recarregar a pagina inteira e sem modo web.
$switchStart = $radioText.IndexOf('    private async Task SwitchModeAsync(bool personal)')
$switchEnd = $radioText.IndexOf('    private async Task LoadPersonalFromInputAsync()', $switchStart)
if ($switchStart -lt 0 -or $switchEnd -lt 0) { throw 'SwitchModeAsync da Radio nao encontrado.' }
$newSwitch = @'
    private async Task SwitchModeAsync(bool personal)
    {
        if (_webMode)
        {
            _webMode = false;
            await RestorePlayerPageAsync();
        }

        bool changed = _personalMode != personal;
        _personalMode = personal;
        SaveSharedMediaMode041(personal ? "mine" : "gat");
        if (personal) _personalInput.Text = _personalSourceUrl ?? string.Empty;
        else _personalInput.Text = _serverSourceUrl ?? string.Empty;
        ApplyModeUi();

        if (changed && _listening)
        {
            if (ActiveAvailable() && _playerReady)
            {
                await LoadActiveSourceAsync();
            }
            else
            {
                _listening = false;
                _toggle.Text = "OUVIR RÁDIO";
                try { await ExecutePlayerAsync("gatPause()"); } catch { }
                UpdateActiveSourceUi();
            }
        }
    }

'@
$radioText = $radioText.Substring(0, $switchStart) + $newSwitch + $radioText.Substring($switchEnd)

# Metodos 1.0.49 inseridos antes do carregamento da fonte pessoal.
$insertMarker = '    private async Task LoadPersonalFromInputAsync()'
$insertIndex = $radioText.IndexOf($insertMarker)
if ($insertIndex -lt 0) { throw 'Ponto de insercao dos metodos Radio 1.0.49 nao encontrado.' }
if ($radioText -notlike '*ConfigureAccount049*') {
$radioMethods = @'
    internal void ConfigureAccount049(string user, string token)
    {
        string nextUser = (user ?? string.Empty).Trim();
        string nextToken = token ?? string.Empty;
        bool changed = !string.Equals(_accountUser049, nextUser, StringComparison.OrdinalIgnoreCase) || _accountToken049 != nextToken;
        _accountUser049 = nextUser;
        _accountToken049 = nextToken;
        if (changed) _ = RefreshAccountRole049();
        else ApplyModeUi();
    }

    private bool CanEditChannel049()
    {
        return string.Equals(_accountRole049, "owner", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(_accountRole049, "admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(_accountRole049, "moderator", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshAccountRole049()
    {
        _accountRole049 = string.Empty;
        if (string.IsNullOrWhiteSpace(_accountToken049))
        {
            try { if (!IsDisposed) BeginInvoke((Action)(() => ApplyModeUi())); } catch { }
            return;
        }
        try
        {
            JObject payload = new JObject { ["token"] = _accountToken049 };
            using (var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _http.PostAsync(AccountSessionEndpoint049, content))
            {
                string text = await response.Content.ReadAsStringAsync();
                JObject root = JObject.Parse(text);
                if (response.IsSuccessStatusCode && root.Value<bool?>("ok") == true)
                    _accountRole049 = (Convert.ToString(root["role"]) ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
        catch { _accountRole049 = string.Empty; }
        try { if (!IsDisposed) BeginInvoke((Action)(() => ApplyModeUi())); } catch { }
    }

    private async Task SaveChannel049Async()
    {
        if (!CanEditChannel049())
        {
            _state.Text = "Canal GAT: sua conta não tem permissão para alterar a programação global.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        string raw = (_personalInput.Text ?? string.Empty).Trim();
        string sourceType = string.Empty, sourceId = string.Empty, canonical = string.Empty;
        bool enabled = !string.IsNullOrWhiteSpace(raw);
        if (enabled && !TryParseMediaSource(raw, out sourceType, out sourceId, out canonical))
        {
            _state.Text = "Canal GAT: link inválido. Use YouTube ou URL direta de Rádio Online MP3/AAC.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        try
        {
            _loadPersonal.Enabled = false;
            _state.Text = enabled ? "Canal GAT: salvando para todos..." : "Canal GAT: desligando programação global...";
            _state.ForeColor = Color.FromArgb(168, 181, 199);
            JObject payload = new JObject
            {
                ["token"] = _accountToken049,
                ["enabled"] = enabled,
                ["playlist_url"] = enabled ? canonical : string.Empty,
                ["label"] = "Canal GAT"
            };
            using (var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _http.PostAsync(ChannelAdminEndpoint049, content))
            {
                string text = await response.Content.ReadAsStringAsync();
                JObject root = null;
                try { root = JObject.Parse(text); } catch { }
                if (!response.IsSuccessStatusCode || root == null || root.Value<bool?>("ok") != true)
                {
                    string error = root == null ? ("HTTP " + (int)response.StatusCode) : (Convert.ToString(root["error"]) ?? ("HTTP " + (int)response.StatusCode));
                    _state.Text = "Canal GAT: não foi possível salvar • " + error;
                    _state.ForeColor = Color.OrangeRed;
                    return;
                }
            }

            _personalInput.Text = enabled ? canonical : string.Empty;
            await RefreshRadioAsync(true);
            _state.Text = enabled ? "Canal GAT: programação salva para todos os motoristas ✓" : "Canal GAT: programação global desligada ✓";
            _state.ForeColor = Color.FromArgb(130, 224, 69);
        }
        catch (Exception ex)
        {
            _state.Text = "Canal GAT: falha ao salvar • " + ex.Message;
            _state.ForeColor = Color.OrangeRed;
        }
        finally
        {
            _loadPersonal.Enabled = CanEditChannel049();
        }
    }

'@
    $radioText = $radioText.Insert($insertIndex, $radioMethods)
}

# O Hub fornece a sessao da Conta GAT ao RadioForm (sem salvar senha).
$oldGuard = 'if (_hubRadioHost041 == null || (_hubRadio041 != null && !_hubRadio041.IsDisposed)) return;'
$newGuard = 'if (_hubRadioHost041 == null) return;`r`n        if (_hubRadio041 != null && !_hubRadio041.IsDisposed) { _hubRadio041.ConfigureAccount049(_accountUser, _accountToken); return; }'
if ($hub41Text.Contains($oldGuard)) { $hub41Text = $hub41Text.Replace($oldGuard, $newGuard) }
elseif ($hub41Text -notlike '*ConfigureAccount049(_accountUser, _accountToken)*') { throw 'Guard EnsureRadio041 nao encontrado.' }
$showRadio = '_hubRadio041.Show();'
if ($hub41Text.Contains($showRadio) -and $hub41Text -notlike '*_hubRadio041.Show(); _hubRadio041.ConfigureAccount049*') {
    $hub41Text = $hub41Text.Replace($showRadio, '_hubRadio041.Show(); _hubRadio041.ConfigureAccount049(_accountUser, _accountToken);')
}
$hub41Text = $hub41Text.Replace('Canal GAT, Meu Vídeo e Canal Web', 'Canal GAT e Meu Vídeo')

# MainForm.Hub042: nao oferece mais o modo WEB nem restaura um WEB antigo.
$oldModeReturn = 'return mode == "gat" || mode == "mine" || mode == "web" ? mode : "gat";'
if ($hub42Text.Contains($oldModeReturn)) { $hub42Text = $hub42Text.Replace($oldModeReturn, 'return mode == "mine" ? "mine" : "gat";') }
$oldModeValue = 'string value = string.Equals(mode, "web", StringComparison.OrdinalIgnoreCase) ? "web" : string.Equals(mode, "mine", StringComparison.OrdinalIgnoreCase) ? "mine" : "gat";'
if ($hub42Text.Contains($oldModeValue)) { $hub42Text = $hub42Text.Replace($oldModeValue, 'string value = string.Equals(mode, "mine", StringComparison.OrdinalIgnoreCase) ? "mine" : "gat";') }
# Remove navegacao automatica para web do HubShowRadio041 sem apagar os metodos legados (ficam inacessiveis).
$hub42Text = [regex]::Replace($hub42Text, '(?ms)\s*if \(string\.Equals\(HubRadioMode042\(\), "web", StringComparison\.OrdinalIgnoreCase\)\)\s*HubNavigateRadio042\("web"\);', '')

# Cache/revisao interna do player.
$radioText = $radioText.Replace('Radio-1.0.42', 'Radio-1.0.49')
$radioText = $radioText.Replace('/index.html?v=142', '/index.html?v=149')

foreach ($m in @('CurrentVersion = "1.0.49.0"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.49 sem $m" }
}
foreach ($m in @('/api/public/account-live','destination_city','remaining_km','online  •')) {
    if ($hub45Text -notlike "*$m*") { throw "Home 1.0.49 sem $m" }
}
foreach ($m in @('SALVAR P/ TODOS','SaveChannel049Async','ConfigureAccount049','ChannelAdminEndpoint049','CanEditChannel049','_siteWeb.Visible = false')) {
    if ($radioText -notlike "*$m*") { throw "Radio 1.0.49 sem $m" }
}
if ($hub41Text -notlike '*ConfigureAccount049(_accountUser, _accountToken)*') { throw 'Hub nao repassa Conta GAT para a Radio 1.0.49.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hub41Text -Encoding UTF8
Set-Content $hub42.FullName $hub42Text -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $hub45.FullName $hub45Text -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.49: Canal GAT + Meu Video, controle global no Telemetria e lista completa de motoristas online aplicados.'
