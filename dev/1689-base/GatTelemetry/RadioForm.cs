using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class RadioForm : Form
{
    private const string RadioEndpoint = "https://api.gatlogets2.com.br/api/public/radio";
    private const string VirtualHost = "radio.gatlogets2.local";

    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
    private const string ChannelAdminEndpoint049 = "https://api.gatlogets2.com.br/api/site/admin/radio";
    private const string AccountSessionEndpoint049 = "https://api.gatlogets2.com.br/api/account/session";
    private string _accountUser049 = string.Empty;
    private string _accountToken049 = string.Empty;
    private string _accountRole049 = string.Empty;
    private readonly Timer _pollTimer = new Timer { Interval = 15000 };
    private readonly WebView2 _web = new WebView2();
    private readonly Label _title = new Label();
    private readonly Label _description = new Label();
    private readonly Label _state = new Label();
    private readonly Label _track = new Label();
    private readonly Label _source = new Label();
    private readonly Label _personalLabel = new Label();
    private readonly Button _channelGat = new Button();
    private readonly Button _myRadio = new Button();
    private readonly Button _siteWeb = new Button();
    private readonly Button _loadPersonal = new Button();
    private readonly Button _toggle = new Button();
    private readonly Button _openYoutube = new Button();
    private readonly Button _fullScreen = new Button();
    private readonly Button _overlay = new Button();
    private readonly TextBox _personalInput = new TextBox();
    private readonly TrackBar _volume = new TrackBar();

    private bool _browserReady;
    private bool _playerReady;
    private bool _listening;
    private bool _fullScreenMode;
    private bool _overlayMode;
    private bool _personalMode;
    private bool _webMode;
    private Rectangle _normalBounds;

    private bool _serverEnabled;
    private string _serverSourceType = string.Empty;
    private string _serverSourceId = string.Empty;
    private string _serverSourceUrl = string.Empty;
    private long _serverRevision = -1;

    private string _personalSourceType = string.Empty;
    private string _personalSourceId = string.Empty;
    private string _personalSourceUrl = string.Empty;
    private string _webUrl = string.Empty;

    public RadioForm()
    {
        Text = "Rádio / TV GAT";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 680);
        Size = new Size(840, 740);
        BackColor = Color.FromArgb(4, 13, 25);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;
        TopMost = true;
        ShowInTaskbar = true;

        BuildUi();
        LoadSavedPersonalSource();
        LoadSavedWebUrl();
        string sharedMode041 = ReadSharedMediaMode041();
        _personalMode = sharedMode041 == "mine";
        _webMode = false;
        if (_personalMode) _personalInput.Text = _personalSourceUrl;
        
        ApplyModeUi();

        Shown += async delegate
        {
            await InitializePlayerAsync();
            await RefreshRadioAsync(true);
            _pollTimer.Start();
        };
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _fullScreenMode) ToggleFullScreen();
        };
        _web.KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _fullScreenMode) ToggleFullScreen();
        };
        FormClosing += async delegate
        {
            _pollTimer.Stop();
            try { await ExecutePlayerAsync("gatPause()"); } catch { }
        };
        FormClosed += delegate
        {
            _pollTimer.Dispose();
            _http.Dispose();
            _web.Dispose();
        };
        _pollTimer.Tick += async delegate { await RefreshRadioAsync(false); };
    }

    private void BuildUi()
    {
        _title.Text = "RÁDIO / TV GAT";
        _title.Left = 24; _title.Top = 16; _title.Width = 360; _title.Height = 38;
        _title.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold); _title.ForeColor = Color.White;
        Controls.Add(_title);

        _description.Text = "Escolha o Canal GAT para todos ou o MEU VÍDEO somente para você.";
        _description.Left = 26; _description.Top = 53; _description.Width = 720; _description.Height = 28;
        _description.ForeColor = Color.FromArgb(168, 181, 199);
        Controls.Add(_description);

        SetupButton(_channelGat, "📡 CANAL GAT", 24, 82, 145);
        _channelGat.Click += async delegate { await SwitchModeAsync(false); };
        Controls.Add(_channelGat);

        SetupButton(_myRadio, "🎬 MEU VÍDEO", 178, 82, 155);
        _myRadio.Click += async delegate { await SwitchModeAsync(true); };
        Controls.Add(_myRadio);

        SetupButton(_siteWeb, "🌐 CANAL WEB", 342, 82, 145);
        _siteWeb.Click += delegate { };
        Controls.Add(_siteWeb); _siteWeb.Visible = false; _siteWeb.Enabled = false;

        SetupButton(_overlay, "MODO JOGO • SOBREPOSTO", ClientSize.Width - 224, 18, 200);
        _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _overlay.Click += delegate { ToggleOverlayMode(); };
        Controls.Add(_overlay);

        _personalLabel.Text = "Seu vídeo/playlist do YouTube (fica disponível também no GAT DASH):";
        _personalLabel.Left = 24; _personalLabel.Top = 126; _personalLabel.Width = 560; _personalLabel.Height = 22;
        _personalLabel.ForeColor = Color.FromArgb(168, 181, 199);
        Controls.Add(_personalLabel);

        _personalInput.Left = 24; _personalInput.Top = 149; _personalInput.Width = ClientSize.Width - 190; _personalInput.Height = 27;
        _personalInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _personalInput.BackColor = Color.FromArgb(12, 26, 43); _personalInput.ForeColor = Color.WhiteSmoke;
        _personalInput.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_personalInput);

        SetupButton(_loadPersonal, "CARREGAR", ClientSize.Width - 154, 145, 130);
        _loadPersonal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _loadPersonal.Click += async delegate { if (_personalMode) await LoadPersonalFromInputAsync(); else await SaveChannel049Async(); };
        Controls.Add(_loadPersonal);

        _web.Left = 24; _web.Top = 190; _web.Width = ClientSize.Width - 48; _web.Height = 350;
        _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _web.BackColor = Color.Black;
        Controls.Add(_web);

        _state.Text = "Rádio: conectando à Central GAT...";
        _state.Left = 26; _state.Top = 555; _state.Width = 760; _state.Height = 22;
        _state.ForeColor = Color.FromArgb(130, 224, 69);
        _state.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        Controls.Add(_state);

        _track.Text = "Tocando agora: —";
        _track.Left = 26; _track.Top = 580; _track.Width = 760; _track.Height = 34;
        _track.ForeColor = Color.Gainsboro;
        Controls.Add(_track);

        _source.Text = "Fonte: —";
        _source.Left = 26; _source.Top = 613; _source.Width = 760; _source.Height = 22;
        _source.ForeColor = Color.FromArgb(143, 158, 178);
        Controls.Add(_source);

        SetupButton(_toggle, "OUVIR RÁDIO", 24, 650, 155);
        _toggle.Click += async delegate { await ToggleListeningAsync(); };
        Controls.Add(_toggle);

        SetupButton(_openYoutube, "ABRIR FONTE", 188, 650, 165);
        _openYoutube.Enabled = false;
        _openYoutube.Click += delegate { OpenYoutube(); };
        Controls.Add(_openYoutube);

        SetupButton(_fullScreen, "TELA CHEIA", 362, 650, 125);
        _fullScreen.Click += delegate { ToggleFullScreen(); };
        Controls.Add(_fullScreen);

        var volumeLabel = new Label { Text = "VOLUME", Left = 504, Top = 657, Width = 65, Height = 22, ForeColor = Color.FromArgb(168, 181, 199) };
        volumeLabel.Name = "volumeLabel";
        Controls.Add(volumeLabel);
        _volume.Left = 565; _volume.Top = 644; _volume.Width = 220; _volume.Height = 45;
        _volume.Minimum = 0; _volume.Maximum = 100; _volume.TickFrequency = 10; _volume.Value = LoadVolume();
        _volume.Scroll += async delegate
        {
            SaveVolume(_volume.Value);
            if (_playerReady) await ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
        };
        Controls.Add(_volume);

        Resize += delegate
        {
            if (_fullScreenMode || _overlayMode) return;
            _web.Width = Math.Max(420, ClientSize.Width - 48);
            _personalInput.Width = Math.Max(350, ClientSize.Width - 190);
            _loadPersonal.Left = ClientSize.Width - 154;
            _state.Width = Math.Max(420, ClientSize.Width - 52);
            _track.Width = Math.Max(420, ClientSize.Width - 52);
            _source.Width = Math.Max(420, ClientSize.Width - 52);
        };
    }

    private static void SetupButton(Button button, string text, int left, int top, int width)
    {
        button.Text = text; button.Left = left; button.Top = top; button.Width = width; button.Height = 38;
        button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(11, 43, 82);
        button.ForeColor = Color.FromArgb(215, 229, 249); button.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
        button.FlatAppearance.BorderColor = Color.FromArgb(60, 137, 245); button.Cursor = Cursors.Hand;
    }

    private void ApplyModeUi()
    {
        _webMode = false;
        bool channelMode = !_personalMode;
        _channelGat.BackColor = channelMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _myRadio.BackColor = _personalMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _siteWeb.Visible = false;
        _siteWeb.Enabled = false;

        Control volumeLabel = Controls["volumeLabel"];
        bool showSourceEditor1689 = _personalMode || CanEditChannel049();
        _personalLabel.Visible = showSourceEditor1689;
        _personalInput.Visible = showSourceEditor1689;
        _loadPersonal.Visible = showSourceEditor1689;
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
    private bool ActiveAvailable()
    {
        if (_personalMode) return !string.IsNullOrWhiteSpace(_personalSourceId);
        return _serverEnabled && !string.IsNullOrWhiteSpace(_serverSourceId);
    }

    private string ActiveSourceType() => _personalMode ? _personalSourceType : _serverSourceType;
    private string ActiveSourceId() => _personalMode ? _personalSourceId : _serverSourceId;
    private string ActiveSourceUrl() => _personalMode ? _personalSourceUrl : _serverSourceUrl;

    private static string SourceDescription(string type)
    {
        if (string.Equals(type, "stream", StringComparison.OrdinalIgnoreCase)) return "rádio online MP3/AAC";
        if (string.Equals(type, "playlist", StringComparison.OrdinalIgnoreCase)) return "playlist do YouTube";
        if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)) return "vídeo do YouTube";
        return "fonte desconhecida";
    }

    private void UpdateActiveSourceUi()
    {
        if (_personalMode)
        {
            bool available = !string.IsNullOrWhiteSpace(_personalSourceId);
            _toggle.Enabled = available && _browserReady;
            _openYoutube.Enabled = available;
            _state.Text = available ? (_listening ? "Meu Vídeo: tocando sua programação" : "Meu Vídeo: pronta • clique em OUVIR RÁDIO") : "Meu Vídeo: cole YouTube ou URL direta de rádio online acima";
            _state.ForeColor = available ? Color.FromArgb(130, 224, 69) : Color.FromArgb(168, 181, 199);
            _source.Text = available ? "Fonte local: " + SourceDescription(_personalSourceType) + " • somente neste PC" : "Fonte local: nenhuma configurada";
        }
        else
        {
            bool available = _serverEnabled && !string.IsNullOrWhiteSpace(_serverSourceId);
            _toggle.Enabled = available && _browserReady;
            _openYoutube.Enabled = !string.IsNullOrWhiteSpace(_serverSourceUrl);
            if (!available)
            {
                _state.Text = "Canal GAT: DESLIGADO pela Central";
                _state.ForeColor = Color.FromArgb(168, 181, 199);
                _source.Text = "Fonte oficial: nenhuma programação ativa";
            }
            else
            {
                _state.Text = _listening ? "Canal GAT: AO VIVO • ouvindo" : "Canal GAT: AO VIVO • clique em OUVIR RÁDIO";
                _state.ForeColor = Color.FromArgb(130, 224, 69);
                _source.Text = "Fonte oficial: " + SourceDescription(_serverSourceType) + " • revisão " + _serverRevision;
            }
        }
    }

    private async Task SwitchModeAsync(bool personal)
    {
        _webMode = false;

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
    private async Task LoadPersonalFromInputAsync()
    {
        string type, id, canonical;
        if (!TryParseMediaSource(_personalInput.Text, out type, out id, out canonical))
        {
            _state.Text = "Meu Vídeo: fonte inválida. Use YouTube ou URL direta de rádio online (MP3/AAC).";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        _personalSourceType = type;
        _personalSourceId = id;
        _personalSourceUrl = canonical;
        _personalInput.Text = canonical;
        SavePersonalSource(canonical);
        _personalMode = true;
        ApplyModeUi();

        if (_listening && _playerReady) await LoadActiveSourceAsync();
    }

    private async Task LoadWebFromInputAsync()
    {
        string canonical;
        if (!TryParseWebUrl(_personalInput.Text, out canonical))
        {
            _state.Text = "CANAL WEB: endereço inválido. Use somente HTTP ou HTTPS.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        _webUrl = canonical;
        _personalInput.Text = canonical;
        SaveWebUrl(canonical);
        _personalMode = false;
        _webMode = true;
        SaveSharedMediaMode041("web");
        ApplyModeUi();
        await NavigateWebAsync(canonical);
    }

    private async Task NavigateWebAsync(string url)
    {
        if (!_browserReady || _web.CoreWebView2 == null)
        {
            _state.Text = "CANAL WEB: navegador interno indisponível neste PC.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        string canonical;
        if (!TryParseWebUrl(url, out canonical))
        {
            _state.Text = "CANAL WEB: endereço inválido.";
            _state.ForeColor = Color.OrangeRed;
            return;
        }

        _webUrl = canonical;
        SaveWebUrl(canonical);
        _state.Text = "CANAL WEB: carregando...";
        _state.ForeColor = Color.FromArgb(168, 181, 199);
        _track.Text = "Página atual: " + canonical;
        _web.CoreWebView2.Navigate(canonical);
        await Task.Delay(1);
    }

    private void ShowWebStartPage()
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        _web.CoreWebView2.NavigateToString(@"<!doctype html><html><head><meta charset='utf-8'><style>html,body{height:100%;margin:0;background:#020711;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif}.box{height:100%;display:flex;align-items:center;justify-content:center;text-align:center}.card{max-width:620px;padding:32px}.icon{font-size:58px}.title{font-size:26px;font-weight:700;margin-top:8px}.sub{color:#9eb6d1;margin-top:10px;line-height:1.5}</style></head><body><div class='box'><div class='card'><div class='icon'>🌐</div><div class='title'>CANAL WEB</div><div class='sub'>Cole um endereço HTTP/HTTPS acima e clique em ABRIR NO GAT.<br>Alguns sites podem bloquear vídeo incorporado, autoplay, login ou conteúdo protegido.</div></div></div></body></html>");
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
            _state.Text = "CANAL WEB: página carregada";
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
            _state.Text = "CANAL WEB: não foi possível carregar esta página.";
            _state.ForeColor = Color.OrangeRed;
        }
    }
    private async Task InitializePlayerAsync()
    {
        if (_browserReady) return;
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "Radio-1.0.68.9");
            string webViewData = Path.Combine(root, "WebView2");
            string pageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "radio");
            Directory.CreateDirectory(webViewData);
            if (!Directory.Exists(pageFolder) || !File.Exists(Path.Combine(pageFolder, "player.html")))
            {
                pageFolder = Path.Combine(root, "player");
                Directory.CreateDirectory(pageFolder);
                File.WriteAllText(Path.Combine(pageFolder, "player.html"), PlayerHtml());
            }
            string probe = Path.Combine(webViewData, "write-test.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            var options = new CoreWebView2EnvironmentOptions();
            options.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required --allow-running-insecure-content";
            var environment = await CoreWebView2Environment.CreateAsync(null, webViewData, options);
            await _web.EnsureCoreWebView2Async(environment);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube.com/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://youtube.com/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://m.youtube.com/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://music.youtube.com/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://youtu.be/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube-nocookie.com/*", CoreWebView2WebResourceContext.All);
            _web.CoreWebView2.WebResourceRequested += delegate(object sender, CoreWebView2WebResourceRequestedEventArgs e)
            {
                try
                {
                    Uri requested;
                    if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out requested)) return;
                    string host = (requested.Host ?? string.Empty).ToLowerInvariant();
                    bool youtubeHost = host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) || host == "youtu.be" || host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
                    if (!youtubeHost) return;
                    e.Request.Headers.SetHeader("Referer", "https://radio.gatlogets2.local/");
                }
                catch { }
            };
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, pageFolder, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.WebMessageReceived += WebMessageReceived;
            _web.CoreWebView2.NavigationStarting += WebNavigationStarting;
            _web.CoreWebView2.NavigationCompleted += WebNavigationCompleted;
            _web.CoreWebView2.NewWindowRequested += WebNewWindowRequested;
            _web.Source = new Uri("https://" + VirtualHost + "/player.html?v=1689");
            _browserReady = true;
            ApplyModeUi();
        }
        catch (Exception ex)
        {
            _browserReady = false;
            _state.Text = "Rádio: player interno indisponível neste PC.";
            _state.ForeColor = Color.OrangeRed;
            _track.Text = "Detalhe: " + ex.Message;
            _toggle.Enabled = false;
        }
    }

    private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_webMode) return;
        try
        {
            JObject msg = JObject.Parse(e.WebMessageAsJson);
            string type = Convert.ToString(msg["type"]);
            if (type == "ready")
            {
                _playerReady = true;
                _ = ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
                if (_listening && ActiveAvailable()) _ = LoadActiveSourceAsync();
            }
            else if (type == "track")
            {
                string name = Convert.ToString(msg["title"]);
                if (!string.IsNullOrWhiteSpace(name)) _track.Text = "Tocando agora: " + name;
            }
            else if (type == "position")
            {
                if (!_personalMode && !_webMode && CanEditChannel049()) _ = SaveChannelPosition1689Async(msg);
            }
            else if (type == "error")
            {
                int code = 0;
                int.TryParse(Convert.ToString(msg["code"]), out code);
                _track.Text = YoutubeErrorText(code);
            }
        }
        catch { }
    }

    private static string YoutubeErrorText(int code)
    {
        if (code == 100) return "Este vídeo foi removido, é privado ou não está disponível. Em playlist, a Rádio GAT tenta pular para o próximo.";
        if (code == 101 || code == 150) return "Este videoclipe bloqueia reprodução incorporada. Em playlist ele será pulado; para vídeo único use ABRIR FONTE.";
        if (code == 153) return "O YouTube recusou o player incorporado neste vídeo. Use ABRIR FONTE.";
        if (code == 2) return "Link/ID de vídeo inválido.";
        if (code == 5) return "O player HTML5 do YouTube não conseguiu reproduzir este vídeo.";
        if (code == 900) return "Não foi possível tocar esta rádio online. Confirme se o link é o stream direto MP3/AAC; HTTPS é recomendado.";
        if (code == 901) return "A rádio online foi interrompida. O servidor da estação pode estar offline ou ter recusado a conexão.";
        return "Player do YouTube informou erro " + code + ".";
    }

    private static string SharedMediaModeFile041()
    {
        return Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
    }

    private static void SaveSharedMediaMode041(string mode)
    {
        try
        {
            mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
            if (mode != "gat" && mode != "mine") return;
            File.WriteAllText(SharedMediaModeFile041(), mode);
        }
        catch { }
    }

    private static string ReadSharedMediaMode041()
    {
        try
        {
            string file = SharedMediaModeFile041();
            if (!File.Exists(file)) return "gat";
            string mode = (File.ReadAllText(file) ?? string.Empty).Trim().ToLowerInvariant();
            return mode == "mine" ? "mine" : "gat";
        }
        catch { return "gat"; }
    }

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
    internal string HubNowPlaying042
    {
        get
        {
            try { return _track == null ? string.Empty : (_track.Text ?? string.Empty); }
            catch { return string.Empty; }
        }
    }

    internal async void HubPause042()
    {
        try
        {
            _listening = false;
            _toggle.Text = "OUVIR RÁDIO";
            if (_webMode && _web.CoreWebView2 != null)
            {
                try { await _web.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('video,audio').forEach(function(x){try{x.pause()}catch(e){}})"); } catch { }
            }
            else
            {
                try { await ExecutePlayerAsync("gatPause()"); } catch { }
            }
            UpdateActiveSourceUi();
        }
        catch { }
    }

    internal async void HubSyncMode042()
    {
        try { await SyncSharedMediaMode041(); }
        catch { }
    }
    private async Task RefreshRadioAsync(bool force)
    {
        await SyncSharedMediaMode041();
        try
        {
            string text = await _http.GetStringAsync(RadioEndpoint + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            JObject root = JObject.Parse(text);
            JObject radio = root["radio"] as JObject;
            if (radio == null || root.Value<bool?>("ok") != true) throw new InvalidDataException("Resposta inválida da Central GAT.");

            bool enabled = radio.Value<bool?>("enabled") == true;
            string sourceType = (Convert.ToString(radio["source_type"]) ?? string.Empty).Trim().ToLowerInvariant();
            string sourceUrl = Convert.ToString(radio["source_url"]) ?? Convert.ToString(radio["playlist_url"]) ?? string.Empty;
            string sourceId = string.Empty;
            if (sourceType == "video") sourceId = Convert.ToString(radio["video_id"]) ?? string.Empty;
            else if (sourceType == "stream") sourceId = sourceUrl;
            else
            {
                sourceId = Convert.ToString(radio["playlist_id"]) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceType) && !string.IsNullOrWhiteSpace(sourceId)) sourceType = "playlist";
            }
            long revision = radio.Value<long?>("revision") ?? 0;
            bool changed = force || revision != _serverRevision || !string.Equals(sourceType, _serverSourceType, StringComparison.Ordinal) || !string.Equals(sourceId, _serverSourceId, StringComparison.Ordinal);

            _serverEnabled = enabled;
            _serverSourceType = sourceType;
            _serverSourceId = sourceId;
            _serverSourceUrl = sourceUrl;
            _serverRevision = revision;
            ReadLiveRadioState1689(radio);

            if (!_personalMode && !_webMode)
            {
                if ((!enabled || string.IsNullOrWhiteSpace(sourceId)) && _listening)
                {
                    _listening = false;
                    _toggle.Text = "OUVIR RÁDIO";
                    await ExecutePlayerAsync("gatPause()");
                }
                UpdateActiveSourceUi();
                if (changed && _listening && _playerReady && ActiveAvailable()) await LoadActiveSourceAsync();
            }
        }
        catch (Exception ex)
        {
            if (!_personalMode && !_webMode)
            {
                _state.Text = "Canal GAT: Central temporariamente indisponível.";
                _state.ForeColor = Color.Orange;
                _track.Text = "Detalhe: " + ex.Message;
            }
        }
    }

    private async Task ToggleListeningAsync()
    {
        if (!ActiveAvailable()) return;
        if (!_listening)
        {
            _listening = true;
            _toggle.Text = "PARAR RÁDIO";
            UpdateActiveSourceUi();
            if (_playerReady) await LoadActiveSourceAsync();
        }
        else
        {
            _listening = false;
            _toggle.Text = "OUVIR RÁDIO";
            await ExecutePlayerAsync("gatPause()");
            UpdateActiveSourceUi();
        }
    }

    private async Task LoadActiveSourceAsync()
    {
        string id = ActiveSourceId();
        if (!_playerReady || string.IsNullOrWhiteSpace(id)) return;
        string type = ActiveSourceType();
        string command = BuildSyncedLoadCommand1689(type);
        if (!string.IsNullOrWhiteSpace(command))
        {
        }
        else if (type == "stream")
        {
            string jsUrl = Newtonsoft.Json.JsonConvert.SerializeObject(ActiveSourceUrl());
            command = "gatLoadStream(" + jsUrl + ")";
        }
        else
        {
            string jsId = Newtonsoft.Json.JsonConvert.SerializeObject(id);
            command = type == "video" ? "gatLoadVideo(" + jsId + ")" : "gatLoadPlaylist(" + jsId + ")";
        }
        await ExecutePlayerAsync(command + ";gatVolume(" + _volume.Value + ")");
    }
    private async Task ExecutePlayerAsync(string script)
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
    }

    private void OpenYoutube()
    {
        string url = _webMode ? _webUrl : ActiveSourceUrl();
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível abrir no navegador.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void ToggleFullScreen()
    {
        if (!_fullScreenMode)
        {
            if (_overlayMode) ToggleOverlayMode();
            _fullScreenMode = true;
            _normalBounds = Bounds;
            WindowState = FormWindowState.Maximized;
            foreach (Control control in Controls) control.Visible = false;
            _web.Visible = true;
            _fullScreen.Visible = true;
            _web.Left = 8; _web.Top = 8; _web.Width = ClientSize.Width - 16; _web.Height = Math.Max(350, ClientSize.Height - 62);
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _fullScreen.Text = "SAIR TELA CHEIA";
            _fullScreen.Left = Math.Max(8, ClientSize.Width - 145); _fullScreen.Top = Math.Max(8, ClientSize.Height - 48);
            _fullScreen.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _fullScreen.BringToFront();
        }
        else
        {
            _fullScreenMode = false;
            WindowState = FormWindowState.Normal;
            if (_normalBounds.Width >= 760 && _normalBounds.Height >= 680) Bounds = _normalBounds;
            else Size = new Size(840, 740);
            foreach (Control control in Controls) control.Visible = true;
            _web.Left = 24; _web.Top = 190; _web.Width = ClientSize.Width - 48; _web.Height = 350;
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _fullScreen.Text = "TELA CHEIA"; _fullScreen.Left = 362; _fullScreen.Top = 650; _fullScreen.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            RestoreNormalLayout();
            ApplyModeUi();
        }
    }

    private void ToggleOverlayMode()
    {
        if (!_overlayMode)
        {
            if (_fullScreenMode) ToggleFullScreen();
            _overlayMode = true;
            _normalBounds = Bounds;
            TopMost = true;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(360, 240);
            Size = new Size(520, 340);

            foreach (Control control in Controls)
            {
                if (!ReferenceEquals(control, _web) && !ReferenceEquals(control, _overlay)) control.Visible = false;
            }
            _web.Visible = true;
            _overlay.Visible = true;
            _web.Left = 8; _web.Top = 8;
            _web.Width = Math.Max(320, ClientSize.Width - 16);
            _web.Height = Math.Max(160, ClientSize.Height - 60);
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            _overlay.Text = "VOLTAR AO GAT";
            _overlay.Width = 160; _overlay.Height = 36;
            _overlay.Left = Math.Max(8, ClientSize.Width - _overlay.Width - 8);
            _overlay.Top = Math.Max(8, ClientSize.Height - _overlay.Height - 8);
            _overlay.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _overlay.BringToFront();

            Screen screen = Screen.FromControl(this);
            Rectangle work = screen.WorkingArea;
            Location = new Point(Math.Max(work.Left, work.Right - Width - 16), Math.Max(work.Top, work.Bottom - Height - 16));

            foreach (Form form in Application.OpenForms)
            {
                if (!ReferenceEquals(form, this) && string.Equals(form.GetType().Name, "MainForm", StringComparison.Ordinal))
                {
                    form.WindowState = FormWindowState.Minimized;
                    break;
                }
            }
            Activate();
            BringToFront();
        }
        else
        {
            _overlayMode = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 680);
            foreach (Control control in Controls) control.Visible = true;
            if (_normalBounds.Width >= 760 && _normalBounds.Height >= 680) Bounds = _normalBounds;
            else Size = new Size(840, 740);
            RestoreNormalLayout();
            ApplyModeUi();
        }
    }

    private void RestoreNormalLayout()
    {
        _web.Left = 24; _web.Top = 190; _web.Width = ClientSize.Width - 48; _web.Height = 350;
        _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _state.Top = 555; _track.Top = 580; _source.Top = 613;
        _toggle.Left = 24; _toggle.Top = 650;
        _openYoutube.Left = 188; _openYoutube.Top = 650;
        _fullScreen.Left = 362; _fullScreen.Top = 650;
        _overlay.Text = "MODO JOGO • SOBREPOSTO";
        _overlay.Width = 200; _overlay.Height = 38;
        _overlay.Left = ClientSize.Width - 224; _overlay.Top = 18;
        _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _personalInput.Width = Math.Max(350, ClientSize.Width - 190);
        _loadPersonal.Left = ClientSize.Width - 154;
        _overlay.BringToFront();
    }

    private void LoadSavedPersonalSource()
    {
        try
        {
            string file = PersonalSourceFile();
            if (!File.Exists(file)) return;
            string saved = File.ReadAllText(file).Trim();
            string type, id, canonical;
            if (!TryParseMediaSource(saved, out type, out id, out canonical)) return;
            _personalSourceType = type;
            _personalSourceId = id;
            _personalSourceUrl = canonical;
            _personalInput.Text = canonical;
        }
        catch { }
    }

    private static void SavePersonalSource(string value)
    {
        try
        {
            Directory.CreateDirectory(Application.LocalUserAppDataPath);
            File.WriteAllText(PersonalSourceFile(), value ?? string.Empty);
        }
        catch { }
    }

    private static string PersonalSourceFile() => Path.Combine(Application.LocalUserAppDataPath, "radio-personal-source.txt");

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
    private static int LoadVolume()
    {
        try
        {
            string file = Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt");
            int value; if (int.TryParse(File.ReadAllText(file), out value)) return Math.Max(0, Math.Min(100, value));
        }
        catch { }
        return 55;
    }

    private static void SaveVolume(int value)
    {
        try { Directory.CreateDirectory(Application.LocalUserAppDataPath); File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt"), value.ToString()); }
        catch { }
    }

    private static bool TryParseMediaSource(string input, out string sourceType, out string sourceId, out string canonicalUrl)
    {
        sourceType = string.Empty; sourceId = string.Empty; canonicalUrl = string.Empty;
        string raw = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return false;

        if (IsSafeYoutubeId(raw))
        {
            if (raw.Length == 11)
            {
                sourceType = "video"; sourceId = raw; canonicalUrl = "https://www.youtube.com/watch?v=" + raw; return true;
            }
            if (LooksLikePlaylistId(raw))
            {
                sourceType = "playlist"; sourceId = raw; canonicalUrl = "https://www.youtube.com/playlist?list=" + raw; return true;
            }
        }

        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("youtu.be", StringComparison.OrdinalIgnoreCase)) raw = "https://" + raw;

        Uri uri;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrWhiteSpace(uri.UserInfo)) return false;

        string host = (uri.Host ?? string.Empty).ToLowerInvariant();
        bool youtube = host == "youtube.com" || host == "www.youtube.com" || host == "m.youtube.com" || host == "music.youtube.com" || host == "youtu.be";
        if (youtube)
        {
            Dictionary<string, string> query = ParseQuery(uri.Query);
            string list;
            if (query.TryGetValue("list", out list) && IsSafeYoutubeId(list) && LooksLikePlaylistId(list))
            {
                sourceType = "playlist"; sourceId = list; canonicalUrl = "https://www.youtube.com/playlist?list=" + list; return true;
            }

            string video = string.Empty;
            if (host == "youtu.be") video = uri.AbsolutePath.Trim('/');
            else if (query.ContainsKey("v")) video = query["v"];
            else
            {
                string[] parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length >= 2 && (parts[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("embed", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("live", StringComparison.OrdinalIgnoreCase))) video = parts[1];
            }

            if (video.Length == 11 && IsSafeYoutubeId(video))
            {
                sourceType = "video"; sourceId = video; canonicalUrl = "https://www.youtube.com/watch?v=" + video; return true;
            }
            return false;
        }

        canonicalUrl = uri.AbsoluteUri;
        sourceType = "stream";
        sourceId = canonicalUrl;
        return true;
    }
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
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string q = (query ?? string.Empty).TrimStart('?');
        if (string.IsNullOrWhiteSpace(q)) return result;
        foreach (string part in q.Split('&'))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            int p = part.IndexOf('=');
            string key = p >= 0 ? part.Substring(0, p) : part;
            string value = p >= 0 ? part.Substring(p + 1) : string.Empty;
            try
            {
                key = Uri.UnescapeDataString(key.Replace("+", " "));
                value = Uri.UnescapeDataString(value.Replace("+", " "));
            }
            catch { }
            if (!result.ContainsKey(key)) result[key] = value;
        }
        return result;
    }

    private static bool IsSafeYoutubeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100) return false;
        foreach (char c in value)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
        }
        return true;
    }

    private static bool LooksLikePlaylistId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12) return false;
        string upper = value.ToUpperInvariant();
        return upper.StartsWith("PL") || upper.StartsWith("UU") || upper.StartsWith("LL") || upper.StartsWith("FL") || upper.StartsWith("OL") || upper.StartsWith("RD");
    }

    private static string PlayerHtml()
    {
        return @"<!doctype html>
<html><head><meta charset='utf-8'><meta name='referrer' content='strict-origin-when-cross-origin'>
<style>
html,body{margin:0;width:100%;height:100%;background:#020711;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;color:#eaf2ff}
#player,#streamPane{width:100%;height:100%}#streamPane{display:none;align-items:center;justify-content:center;background:radial-gradient(circle at 50% 35%,#0d3155 0,#06192c 45%,#020711 100%)}
.radioCard{text-align:center;max-width:86%;padding:28px}.radioIcon{font-size:64px;margin-bottom:14px}.radioTitle{font-size:25px;font-weight:700}.radioSub{margin-top:8px;color:#9eb6d1;font-size:14px}.radioUrl{margin-top:16px;color:#6f8daa;font-size:11px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:680px}
</style></head>
<body><div id='player'></div><div id='streamPane'><div class='radioCard'><div class='radioIcon'>📻</div><div class='radioTitle'>RÁDIO ONLINE • AO VIVO</div><div class='radioSub'>Stream direto MP3/AAC reproduzido somente neste GAT Telemetria.</div><div id='streamUrl' class='radioUrl'></div></div><audio id='streamAudio' preload='none'></audio></div>
<script src='https://www.youtube.com/iframe_api'></script><script>
let player=null,ytReady=false,pendingType='',pendingId='',currentType='';
const audio=document.getElementById('streamAudio'),playerBox=document.getElementById('player'),streamPane=document.getElementById('streamPane'),streamUrl=document.getElementById('streamUrl');
function post(o){try{chrome.webview.postMessage(o)}catch(e){}}
function showYoutube(){playerBox.style.display='block';streamPane.style.display='none'}
function showStream(url){playerBox.style.display='none';streamPane.style.display='flex';streamUrl.textContent=url||''}
function stopYoutube(){if(ytReady&&player){try{player.pauseVideo()}catch(e){}}}
function stopStream(clear){try{audio.pause();if(clear){audio.removeAttribute('src');audio.load()}}catch(e){}}
function loadPending(){if(!ytReady||!pendingId)return;let t=pendingType,id=pendingId;pendingType='';pendingId='';if(t==='video')gatLoadVideo(id);else if(t==='playlist')gatLoadPlaylist(id)}
function onYouTubeIframeAPIReady(){player=new YT.Player('player',{width:'100%',height:'100%',playerVars:{controls:1,disablekb:0,fs:1,playsinline:1,rel:0,origin:'https://radio.gatlogets2.local',widget_referrer:'https://radio.gatlogets2.local/'},events:{onReady:function(){ytReady=true;loadPending()},onStateChange:function(e){if(e.data===YT.PlayerState.PLAYING){let d=player.getVideoData()||{};post({type:'track',title:d.title||''})}},onError:function(e){post({type:'error',code:e.data});if(currentType==='playlist'&&(e.data===100||e.data===101||e.data===150)){setTimeout(function(){try{player.nextVideo()}catch(x){}},700)}}}})}
function gatLoadPlaylist(id){currentType='playlist';stopStream(true);showYoutube();if(!ytReady){pendingType='playlist';pendingId=id;return}try{player.loadPlaylist({listType:'playlist',list:id,index:0,startSeconds:0})}catch(e){post({type:'error',code:'load'})}}
function gatLoadVideo(id){currentType='video';stopStream(true);showYoutube();if(!ytReady){pendingType='video';pendingId=id;return}try{player.loadVideoById(id)}catch(e){post({type:'error',code:'load'})}}
function gatLoadStream(url){currentType='stream';pendingType='';pendingId='';stopYoutube();showStream(url);try{audio.pause();audio.src=url;audio.load();audio.play().catch(function(){post({type:'error',code:900})})}catch(e){post({type:'error',code:900})}}
function gatPause(){if(currentType==='stream')stopStream(false);else stopYoutube()}
function gatVolume(v){let n=Math.max(0,Math.min(100,Number(v)||0));if(ytReady&&player){try{player.setVolume(n)}catch(e){}}audio.volume=n/100}
audio.addEventListener('playing',function(){post({type:'track',title:'Rádio online • AO VIVO'})});
audio.addEventListener('waiting',function(){post({type:'track',title:'Rádio online • conectando...'})});
audio.addEventListener('stalled',function(){post({type:'error',code:901})});
audio.addEventListener('error',function(){post({type:'error',code:900})});
setTimeout(function(){post({type:'ready'})},0);
</script></body></html>";
    }
}








