param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar SITE / WEB 1.0.38.' }
if (-not $radio) { throw 'RadioForm.cs 1.0.37 nao encontrado para aplicar SITE / WEB 1.0.38.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.37.0"', 'CurrentVersion = "1.0.38.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.37"', 'Text = "Cliente 1.0.38"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.38\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.38.0.' }

$radioText = $radioText.Replace('Radio-1.0.37', 'Radio-1.0.38')
$radioText = $radioText.Replace('/index.html?v=137', '/index.html?v=138')
$radioText = $radioText.Replace('Escolha o Canal GAT da Central ou use YouTube / Rádio Online somente neste PC.', 'Escolha o Canal GAT, use sua rádio pessoal ou abra um site no player interno.')

$fieldButton = '    private readonly Button _myRadio = new Button();'
if (-not $radioText.Contains($fieldButton)) { throw 'Campo _myRadio nao encontrado.' }
$radioText = $radioText.Replace($fieldButton, $fieldButton + "`r`n    private readonly Button _siteWeb = new Button();")

$fieldMode = '    private bool _personalMode;'
if (-not $radioText.Contains($fieldMode)) { throw 'Campo _personalMode nao encontrado.' }
$radioText = $radioText.Replace($fieldMode, $fieldMode + "`r`n    private bool _webMode;")

$fieldPersonalUrl = '    private string _personalSourceUrl = string.Empty;'
if (-not $radioText.Contains($fieldPersonalUrl)) { throw 'Campo _personalSourceUrl nao encontrado.' }
$radioText = $radioText.Replace($fieldPersonalUrl, $fieldPersonalUrl + "`r`n    private string _webUrl = string.Empty;")

$loadSaved = '        LoadSavedPersonalSource();'
if (-not $radioText.Contains($loadSaved)) { throw 'LoadSavedPersonalSource nao encontrado.' }
$radioText = $radioText.Replace($loadSaved, $loadSaved + "`r`n        LoadSavedWebUrl();")

$myRadioBlock = @'
        SetupButton(_myRadio, "🎧 MINHA RÁDIO", 178, 82, 155);
        _myRadio.Click += async delegate { await SwitchModeAsync(true); };
        Controls.Add(_myRadio);
'@
if (-not $radioText.Contains($myRadioBlock.TrimEnd())) { throw 'Bloco MINHA RADIO nao encontrado.' }
$siteBlock = @'
        SetupButton(_myRadio, "🎧 MINHA RÁDIO", 178, 82, 155);
        _myRadio.Click += async delegate { await SwitchModeAsync(true); };
        Controls.Add(_myRadio);

        SetupButton(_siteWeb, "🌐 SITE / WEB", 342, 82, 145);
        _siteWeb.Click += async delegate { await SwitchWebModeAsync(); };
        Controls.Add(_siteWeb);
'@
$radioText = $radioText.Replace($myRadioBlock.TrimEnd(), $siteBlock.TrimEnd())

$oldLoadClick = '        _loadPersonal.Click += async delegate { await LoadPersonalFromInputAsync(); };'
$newLoadClick = '        _loadPersonal.Click += async delegate { if (_webMode) await LoadWebFromInputAsync(); else await LoadPersonalFromInputAsync(); };'
if (-not $radioText.Contains($oldLoadClick)) { throw 'Clique CARREGAR nao encontrado.' }
$radioText = $radioText.Replace($oldLoadClick, $newLoadClick)

$applyStart = $radioText.IndexOf('    private void ApplyModeUi()')
$applyEnd = $radioText.IndexOf('    private bool ActiveAvailable()', $applyStart)
if ($applyStart -lt 0 -or $applyEnd -lt 0) { throw 'ApplyModeUi nao encontrado.' }
$newApply = @'
    private void ApplyModeUi()
    {
        bool channelMode = !_personalMode && !_webMode;
        _channelGat.BackColor = channelMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _myRadio.BackColor = _personalMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _siteWeb.BackColor = _webMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);

        Control volumeLabel = Controls["volumeLabel"];

        if (_webMode)
        {
            _description.Text = "SITE / WEB abre uma página HTTP/HTTPS dentro do GAT. Alguns sites podem bloquear reprodução ou exigir login.";
            _personalLabel.Text = "Endereço do site (fica salvo somente neste PC):";
            _personalLabel.Visible = true;
            _personalInput.Visible = true;
            _loadPersonal.Visible = true;
            _loadPersonal.Text = "ABRIR NO GAT";

            _toggle.Visible = false;
            _openYoutube.Visible = true;
            _openYoutube.Text = "ABRIR NO NAVEGADOR";
            _openYoutube.Enabled = !string.IsNullOrWhiteSpace(_webUrl);
            _fullScreen.Visible = true;
            _volume.Visible = false;
            if (volumeLabel != null) volumeLabel.Visible = false;

            _state.Text = string.IsNullOrWhiteSpace(_webUrl)
                ? "SITE / WEB: cole um endereço acima"
                : "SITE / WEB: pronto";
            _state.ForeColor = string.IsNullOrWhiteSpace(_webUrl) ? Color.FromArgb(168, 181, 199) : Color.FromArgb(130, 224, 69);
            _track.Text = string.IsNullOrWhiteSpace(_webUrl) ? "Página atual: —" : "Página atual: " + _webUrl;
            _source.Text = "Fonte local: SITE / WEB • somente neste PC";
            return;
        }

        _description.Text = "Escolha o Canal GAT da Central ou use YouTube / Rádio Online somente neste PC.";
        _personalLabel.Text = "Sua fonte: YouTube ou URL direta de Rádio Online MP3/AAC (somente neste PC):";
        _personalLabel.Visible = _personalMode;
        _personalInput.Visible = _personalMode;
        _loadPersonal.Visible = _personalMode;
        _loadPersonal.Text = "CARREGAR";

        _toggle.Visible = true;
        _openYoutube.Visible = true;
        _openYoutube.Text = "ABRIR FONTE";
        _fullScreen.Visible = true;
        _volume.Visible = true;
        if (volumeLabel != null) volumeLabel.Visible = true;

        UpdateActiveSourceUi();
    }

'@
$radioText = $radioText.Substring(0, $applyStart) + $newApply + $radioText.Substring($applyEnd)

$switchStart = $radioText.IndexOf('    private async Task SwitchModeAsync(bool personal)')
$switchEnd = $radioText.IndexOf('    private async Task LoadPersonalFromInputAsync()', $switchStart)
if ($switchStart -lt 0 -or $switchEnd -lt 0) { throw 'SwitchModeAsync nao encontrado.' }
$newSwitch = @'
    private async Task SwitchModeAsync(bool personal)
    {
        if (_webMode)
        {
            _webMode = false;
            _personalMode = personal;
            if (personal) _personalInput.Text = _personalSourceUrl;
            await RestorePlayerPageAsync();
            ApplyModeUi();
            return;
        }

        if (_personalMode == personal)
        {
            ApplyModeUi();
            return;
        }

        _personalMode = personal;
        if (personal) _personalInput.Text = _personalSourceUrl;
        ApplyModeUi();
        if (_listening)
        {
            if (ActiveAvailable() && _playerReady)
            {
                await LoadActiveSourceAsync();
            }
            else
            {
                _listening = false;
                _toggle.Text = "OUVIR RÁDIO";
                await ExecutePlayerAsync("gatPause()");
                UpdateActiveSourceUi();
            }
        }
    }

    private async Task SwitchWebModeAsync()
    {
        if (_webMode)
        {
            ApplyModeUi();
            return;
        }

        if (_listening)
        {
            _listening = false;
            _toggle.Text = "OUVIR RÁDIO";
            try { await ExecutePlayerAsync("gatPause()"); } catch { }
        }

        _personalMode = false;
        _webMode = true;
        _personalInput.Text = _webUrl;
        ApplyModeUi();

        if (!string.IsNullOrWhiteSpace(_webUrl))
            await NavigateWebAsync(_webUrl);
        else
            ShowWebStartPage();
    }

    private async Task RestorePlayerPageAsync()
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        _playerReady = false;
        _web.Source = new Uri("https://" + VirtualHost + "/index.html?v=138&t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await Task.Delay(50);
    }

'@
$radioText = $radioText.Substring(0, $switchStart) + $newSwitch + $radioText.Substring($switchEnd)

$initMarker = '    private async Task InitializePlayerAsync()'
$initIndex = $radioText.IndexOf($initMarker)
if ($initIndex -lt 0) { throw 'InitializePlayerAsync nao encontrado.' }
$webMethods = @'
    private async Task LoadWebFromInputAsync()
    {
        string canonical;
        if (!TryParseWebUrl(_personalInput.Text, out canonical))
        {
            _state.Text = "SITE / WEB: endereço inválido. Use somente HTTP ou HTTPS.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        _webUrl = canonical;
        _personalInput.Text = canonical;
        SaveWebUrl(canonical);
        _personalMode = false;
        _webMode = true;
        ApplyModeUi();
        await NavigateWebAsync(canonical);
    }

    private async Task NavigateWebAsync(string url)
    {
        if (!_browserReady || _web.CoreWebView2 == null)
        {
            _state.Text = "SITE / WEB: navegador interno indisponível neste PC.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        string canonical;
        if (!TryParseWebUrl(url, out canonical))
        {
            _state.Text = "SITE / WEB: endereço inválido.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        _webUrl = canonical;
        SaveWebUrl(canonical);
        _state.Text = "SITE / WEB: carregando...";
        _state.ForeColor = Color.FromArgb(168, 181, 199);
        _track.Text = "Página atual: " + canonical;
        _web.CoreWebView2.Navigate(canonical);
        await Task.Delay(1);
    }

    private void ShowWebStartPage()
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        _web.CoreWebView2.NavigateToString(@"<!doctype html><html><head><meta charset='utf-8'><style>html,body{height:100%;margin:0;background:#020711;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif}.box{height:100%;display:flex;align-items:center;justify-content:center;text-align:center}.card{max-width:620px;padding:32px}.icon{font-size:58px}.title{font-size:26px;font-weight:700;margin-top:8px}.sub{color:#9eb6d1;margin-top:10px;line-height:1.5}</style></head><body><div class='box'><div class='card'><div class='icon'>🌐</div><div class='title'>SITE / WEB</div><div class='sub'>Cole um endereço HTTP/HTTPS acima e clique em ABRIR NO GAT.<br>Alguns sites podem bloquear vídeo incorporado, autoplay, login ou conteúdo protegido.</div></div></div></body></html>");
    }

    private void WebNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_webMode) return;
        Uri uri;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out uri))
        {
            e.Cancel = true;
            return;
        }

        bool allowed = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "about";
        if (!allowed) e.Cancel = true;
    }

    private void WebNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!_webMode) return;
        e.Handled = true;
        string canonical;
        if (TryParseWebUrl(e.Uri, out canonical) && _web.CoreWebView2 != null)
            _web.CoreWebView2.Navigate(canonical);
    }

    private void WebNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_webMode) return;
        if (e.IsSuccess)
        {
            _state.Text = "SITE / WEB: página carregada";
            _state.ForeColor = Color.FromArgb(130, 224, 69);
            if (_web.CoreWebView2 != null)
            {
                string current = _web.CoreWebView2.Source;
                if (!string.IsNullOrWhiteSpace(current) && !current.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    _webUrl = current;
                    SaveWebUrl(current);
                    _personalInput.Text = current;
                    _track.Text = "Página atual: " + current;
                }
            }
        }
        else
        {
            _state.Text = "SITE / WEB: não foi possível carregar esta página.";
            _state.ForeColor = Color.OrangeRed;
        }
    }

'@
$radioText = $radioText.Insert($initIndex, $webMethods)

$webMessageHook = '            _web.CoreWebView2.WebMessageReceived += WebMessageReceived;'
$webHooks = @'
            _web.CoreWebView2.WebMessageReceived += WebMessageReceived;
            _web.CoreWebView2.NavigationStarting += WebNavigationStarting;
            _web.CoreWebView2.NavigationCompleted += WebNavigationCompleted;
            _web.CoreWebView2.NewWindowRequested += WebNewWindowRequested;
'@
if (-not $radioText.Contains($webMessageHook)) { throw 'WebMessageReceived hook nao encontrado.' }
$radioText = $radioText.Replace($webMessageHook, $webHooks.TrimEnd())

$msgStart = @'
    private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
'@
$msgReplacement = @'
    private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_webMode) return;
        try
'@
if (-not $radioText.Contains($msgStart.TrimEnd())) { throw 'Inicio de WebMessageReceived nao encontrado.' }
$radioText = $radioText.Replace($msgStart.TrimEnd(), $msgReplacement.TrimEnd())

$radioText = $radioText.Replace('            if (!_personalMode)', '            if (!_personalMode && !_webMode)')

$openStart = $radioText.IndexOf('    private void OpenYoutube()')
$openEnd = $radioText.IndexOf('    private void ToggleFullScreen()', $openStart)
if ($openStart -lt 0 -or $openEnd -lt 0) { throw 'OpenYoutube nao encontrado.' }
$newOpen = @'
    private void OpenYoutube()
    {
        string url = _webMode ? _webUrl : ActiveSourceUrl();
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível abrir no navegador.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

'@
$radioText = $radioText.Substring(0, $openStart) + $newOpen + $radioText.Substring($openEnd)

$volumeMarker = '    private static int LoadVolume()'
$volumeIndex = $radioText.IndexOf($volumeMarker)
if ($volumeIndex -lt 0) { throw 'LoadVolume nao encontrado.' }
$webPersistence = @'
    private void LoadSavedWebUrl()
    {
        try
        {
            string file = WebSourceFile();
            if (!File.Exists(file)) return;
            string saved = File.ReadAllText(file).Trim();
            string canonical;
            if (TryParseWebUrl(saved, out canonical)) _webUrl = canonical;
        }
        catch { }
    }

    private static void SaveWebUrl(string value)
    {
        try
        {
            Directory.CreateDirectory(Application.LocalUserAppDataPath);
            File.WriteAllText(WebSourceFile(), value ?? string.Empty);
        }
        catch { }
    }

    private static string WebSourceFile() => Path.Combine(Application.LocalUserAppDataPath, "radio-web-url.txt");

'@
$radioText = $radioText.Insert($volumeIndex, $webPersistence)

$queryMarker = '    private static Dictionary<string, string> ParseQuery(string query)'
$queryIndex = $radioText.IndexOf($queryMarker)
if ($queryIndex -lt 0) { throw 'ParseQuery nao encontrado.' }
$webParser = @'
    private static bool TryParseWebUrl(string input, out string canonicalUrl)
    {
        canonicalUrl = string.Empty;
        string raw = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) raw = "https://" + raw;

        Uri uri;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrWhiteSpace(uri.UserInfo)) return false;

        canonicalUrl = uri.AbsoluteUri;
        return true;
    }

'@
$radioText = $radioText.Insert($queryIndex, $webParser)

foreach($marker in @(
    'CurrentVersion = "1.0.38.0"',
    'SITE / WEB',
    'radio-web-url.txt',
    'TryParseWebUrl',
    'NavigateWebAsync',
    'WebNavigationStarting',
    'ABRIR NO NAVEGADOR',
    'Radio-1.0.38',
    '/index.html?v=138'
)) {
    $haystack = if ($marker -like 'CurrentVersion*') { $mainText } else { $radioText }
    if ($haystack -notlike "*$marker*") { throw "Patch 1.0.38 sem marcador: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Write-Host 'SITE / WEB 1.0.38 aplicado com sucesso.'
