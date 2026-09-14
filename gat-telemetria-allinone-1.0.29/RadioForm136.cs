using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class RadioForm : Form
{
    private const string RadioEndpoint = "https://api.gatlogets2.com.br/api/public/radio";
    private const string VirtualHost = "radio.gatlogets2.local";

    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
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
    private Rectangle _normalBounds;

    private bool _serverEnabled;
    private string _serverSourceType = string.Empty;
    private string _serverSourceId = string.Empty;
    private string _serverSourceUrl = string.Empty;
    private long _serverRevision = -1;

    private string _personalSourceType = string.Empty;
    private string _personalSourceId = string.Empty;
    private string _personalSourceUrl = string.Empty;

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

        _description.Text = "Escolha o Canal GAT da Central ou use sua própria playlist somente neste PC.";
        _description.Left = 26; _description.Top = 53; _description.Width = 720; _description.Height = 28;
        _description.ForeColor = Color.FromArgb(168, 181, 199);
        Controls.Add(_description);

        SetupButton(_channelGat, "📡 CANAL GAT", 24, 82, 145);
        _channelGat.Click += async delegate { await SwitchModeAsync(false); };
        Controls.Add(_channelGat);

        SetupButton(_myRadio, "🎧 MINHA RÁDIO", 178, 82, 155);
        _myRadio.Click += async delegate { await SwitchModeAsync(true); };
        Controls.Add(_myRadio);

        SetupButton(_overlay, "MODO JOGO • SOBREPOSTO", ClientSize.Width - 224, 18, 200);
        _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _overlay.Click += delegate { ToggleOverlayMode(); };
        Controls.Add(_overlay);

        _personalLabel.Text = "Sua playlist/vídeo do YouTube (fica salvo somente neste PC):";
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
        _loadPersonal.Click += async delegate { await LoadPersonalFromInputAsync(); };
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

        SetupButton(_openYoutube, "ABRIR NO YOUTUBE", 188, 650, 165);
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
        _channelGat.BackColor = _personalMode ? Color.FromArgb(11, 43, 82) : Color.FromArgb(18, 76, 130);
        _myRadio.BackColor = _personalMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82);
        _personalLabel.Visible = _personalMode;
        _personalInput.Visible = _personalMode;
        _loadPersonal.Visible = _personalMode;
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

    private void UpdateActiveSourceUi()
    {
        if (_personalMode)
        {
            bool available = !string.IsNullOrWhiteSpace(_personalSourceId);
            _toggle.Enabled = available && _browserReady;
            _openYoutube.Enabled = available;
            _state.Text = available ? (_listening ? "Minha Rádio: tocando sua programação" : "Minha Rádio: pronta • clique em OUVIR RÁDIO") : "Minha Rádio: cole uma playlist ou vídeo do YouTube acima";
            _state.ForeColor = available ? Color.FromArgb(130, 224, 69) : Color.FromArgb(168, 181, 199);
            _source.Text = available ? "Fonte local: " + (_personalSourceType == "playlist" ? "playlist do YouTube" : "vídeo do YouTube") + " • somente neste PC" : "Fonte local: nenhuma configurada";
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
                _source.Text = "Fonte oficial: " + (_serverSourceType == "video" ? "vídeo do YouTube" : "playlist do YouTube") + " • revisão " + _serverRevision;
            }
        }
    }

    private async Task SwitchModeAsync(bool personal)
    {
        if (_personalMode == personal)
        {
            ApplyModeUi();
            return;
        }

        _personalMode = personal;
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

    private async Task LoadPersonalFromInputAsync()
    {
        string type, id, canonical;
        if (!TryParseYoutubeSource(_personalInput.Text, out type, out id, out canonical))
        {
            _state.Text = "Minha Rádio: link do YouTube inválido. Use vídeo ou playlist.";
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

    private async Task InitializePlayerAsync()
    {
        if (_browserReady) return;
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "GAT-LOG", "Telemetria", "Radio-1.0.36");
            string webViewData = Path.Combine(root, "WebView2");
            string pageFolder = Path.Combine(root, "player");
            Directory.CreateDirectory(webViewData);
            Directory.CreateDirectory(pageFolder);
            string probe = Path.Combine(webViewData, "write-test.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            var environment = await CoreWebView2Environment.CreateAsync(null, webViewData);
            await _web.EnsureCoreWebView2Async(environment);
            File.WriteAllText(Path.Combine(pageFolder, "index.html"), PlayerHtml());
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, pageFolder, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.WebMessageReceived += WebMessageReceived;
            _web.Source = new Uri("https://" + VirtualHost + "/index.html?v=136");
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
        if (code == 101 || code == 150) return "Este videoclipe bloqueia reprodução incorporada. Em playlist ele será pulado; para vídeo único use ABRIR NO YOUTUBE.";
        if (code == 153) return "O YouTube recusou o player incorporado neste vídeo. Use ABRIR NO YOUTUBE.";
        if (code == 2) return "Link/ID de vídeo inválido.";
        if (code == 5) return "O player HTML5 do YouTube não conseguiu reproduzir este vídeo.";
        return "Player do YouTube informou erro " + code + ".";
    }

    private async Task RefreshRadioAsync(bool force)
    {
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

            if (!_personalMode)
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
            if (!_personalMode)
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
        string jsId = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        string command = ActiveSourceType() == "video" ? "gatLoadVideo(" + jsId + ")" : "gatLoadPlaylist(" + jsId + ")";
        await ExecutePlayerAsync(command + ";gatVolume(" + _volume.Value + ")");
    }

    private async Task ExecutePlayerAsync(string script)
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
    }

    private void OpenYoutube()
    {
        string url = ActiveSourceUrl();
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível abrir o YouTube.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
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
            if (!TryParseYoutubeSource(saved, out type, out id, out canonical)) return;
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

    private static bool TryParseYoutubeSource(string input, out string sourceType, out string sourceId, out string canonicalUrl)
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
        string host = (uri.Host ?? string.Empty).ToLowerInvariant();
        if (host != "youtube.com" && host != "www.youtube.com" && host != "m.youtube.com" && host != "music.youtube.com" && host != "youtu.be") return false;

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
<style>html,body{margin:0;width:100%;height:100%;background:#020711;overflow:hidden}#player{width:100%;height:100%}</style></head>
<body><div id='player'></div><script src='https://www.youtube.com/iframe_api'></script><script>
let player=null,ready=false,pendingType='',pendingId='',currentType='';
function post(o){try{chrome.webview.postMessage(o)}catch(e){}}
function loadPending(){if(!ready||!pendingId)return;let t=pendingType,id=pendingId;pendingType='';pendingId='';if(t==='video')gatLoadVideo(id);else gatLoadPlaylist(id)}
function onYouTubeIframeAPIReady(){player=new YT.Player('player',{width:'100%',height:'100%',playerVars:{controls:1,disablekb:0,fs:1,playsinline:1,rel:0,origin:'https://radio.gatlogets2.local'},events:{onReady:function(){ready=true;post({type:'ready'});loadPending()},onStateChange:function(e){if(e.data===YT.PlayerState.PLAYING){let d=player.getVideoData()||{};post({type:'track',title:d.title||''})}},onError:function(e){post({type:'error',code:e.data});if(currentType==='playlist'&&(e.data===100||e.data===101||e.data===150)){setTimeout(function(){try{player.nextVideo()}catch(x){}},700)}}}})}
function gatLoadPlaylist(id){currentType='playlist';if(!ready){pendingType='playlist';pendingId=id;return}try{player.loadPlaylist({listType:'playlist',list:id,index:0,startSeconds:0})}catch(e){post({type:'error',code:'load'})}}
function gatLoadVideo(id){currentType='video';if(!ready){pendingType='video';pendingId=id;return}try{player.loadVideoById(id)}catch(e){post({type:'error',code:'load'})}}
function gatPause(){if(ready&&player)player.pauseVideo()}
function gatVolume(v){if(ready&&player)player.setVolume(Math.max(0,Math.min(100,Number(v)||0)))}
</script></body></html>";
    }
}
