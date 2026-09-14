using System;
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
    private readonly Label _state = new Label();
    private readonly Label _track = new Label();
    private readonly Label _source = new Label();
    private readonly Button _toggle = new Button();
    private readonly Button _openYoutube = new Button();
    private readonly Button _fullScreen = new Button();
    private readonly TrackBar _volume = new TrackBar();

    private bool _browserReady;
    private bool _playerReady;
    private bool _listening;
    private bool _radioEnabled;
    private bool _fullScreenMode;
    private string _sourceType = string.Empty;
    private string _sourceId = string.Empty;
    private string _sourceUrl = string.Empty;
    private long _revision = -1;

    public RadioForm()
    {
        Text = "Rádio / TV GAT";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 620);
        Size = new Size(760, 680);
        BackColor = Color.FromArgb(4, 13, 25);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        BuildUi();

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
        Controls.Add(new Label
        {
            Text = "RÁDIO / TV GAT",
            Left = 24, Top = 16, Width = 350, Height = 38,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold), ForeColor = Color.White
        });
        Controls.Add(new Label
        {
            Text = "Vídeos e playlists definidos pela Central GAT. O conteúdo toca somente aqui no Telemetria.",
            Left = 26, Top = 56, Width = 680, Height = 38, ForeColor = Color.FromArgb(168, 181, 199)
        });

        _web.Left = 24; _web.Top = 100; _web.Width = ClientSize.Width - 48; _web.Height = 350;
        _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _web.BackColor = Color.Black;
        Controls.Add(_web);

        _state.Text = "Rádio: conectando à Central GAT...";
        _state.Left = 26; _state.Top = 466; _state.Width = 690; _state.Height = 22;
        _state.ForeColor = Color.FromArgb(130, 224, 69);
        _state.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        Controls.Add(_state);

        _track.Text = "Tocando agora: —";
        _track.Left = 26; _track.Top = 493; _track.Width = 690; _track.Height = 36;
        _track.ForeColor = Color.Gainsboro;
        Controls.Add(_track);

        _source.Text = "Fonte: —";
        _source.Left = 26; _source.Top = 529; _source.Width = 690; _source.Height = 22;
        _source.ForeColor = Color.FromArgb(143, 158, 178);
        Controls.Add(_source);

        SetupButton(_toggle, "OUVIR RÁDIO", 24, 566, 155);
        _toggle.Click += async delegate { await ToggleListeningAsync(); };
        Controls.Add(_toggle);

        SetupButton(_openYoutube, "ABRIR NO YOUTUBE", 188, 566, 165);
        _openYoutube.Enabled = false;
        _openYoutube.Click += delegate { OpenYoutube(); };
        Controls.Add(_openYoutube);

        SetupButton(_fullScreen, "TELA CHEIA", 362, 566, 125);
        _fullScreen.Click += delegate { ToggleFullScreen(); };
        Controls.Add(_fullScreen);

        var volumeLabel = new Label { Text = "VOLUME", Left = 501, Top = 571, Width = 68, Height = 22, ForeColor = Color.FromArgb(168, 181, 199) };
        Controls.Add(volumeLabel);
        _volume.Left = 565; _volume.Top = 560; _volume.Width = 160; _volume.Height = 45;
        _volume.Minimum = 0; _volume.Maximum = 100; _volume.TickFrequency = 10; _volume.Value = LoadVolume();
        _volume.Scroll += async delegate
        {
            SaveVolume(_volume.Value);
            if (_playerReady) await ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
        };
        Controls.Add(_volume);

        Resize += delegate
        {
            if (_fullScreenMode) return;
            _web.Width = Math.Max(360, ClientSize.Width - 48);
            _state.Width = Math.Max(360, ClientSize.Width - 52);
            _track.Width = Math.Max(360, ClientSize.Width - 52);
            _source.Width = Math.Max(360, ClientSize.Width - 52);
        };
    }

    private static void SetupButton(Button button, string text, int left, int top, int width)
    {
        button.Text = text; button.Left = left; button.Top = top; button.Width = width; button.Height = 38;
        button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(11, 43, 82);
        button.ForeColor = Color.FromArgb(215, 229, 249); button.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
        button.FlatAppearance.BorderColor = Color.FromArgb(60, 137, 245); button.Cursor = Cursors.Hand;
    }

    private async Task InitializePlayerAsync()
    {
        if (_browserReady) return;
        try
        {
            // Pasta nova e gravável por versão. Isso evita o E_ACCESSDENIED causado por dados antigos/ACL do WebView2.
            string root = Path.Combine(Path.GetTempPath(), "GAT-LOG", "Telemetria", "Radio-1.0.34");
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
            _web.Source = new Uri("https://" + VirtualHost + "/index.html?v=134");
            _browserReady = true;
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
                if (_listening && _radioEnabled) _ = LoadSourceAsync();
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
            bool changed = force || revision != _revision || !string.Equals(sourceType, _sourceType, StringComparison.Ordinal) || !string.Equals(sourceId, _sourceId, StringComparison.Ordinal);

            _radioEnabled = enabled; _sourceType = sourceType; _sourceId = sourceId; _sourceUrl = sourceUrl; _revision = revision;
            _openYoutube.Enabled = !string.IsNullOrWhiteSpace(_sourceUrl);
            _source.Text = string.IsNullOrWhiteSpace(_sourceId) ? "Fonte: nenhuma configurada" : "Fonte oficial: " + (sourceType == "video" ? "vídeo do YouTube" : "playlist do YouTube") + " • revisão " + revision;

            if (!enabled || string.IsNullOrWhiteSpace(sourceId))
            {
                _state.Text = "Rádio: DESLIGADA pela Central GAT";
                _state.ForeColor = Color.FromArgb(168, 181, 199);
                _toggle.Enabled = false;
                if (_listening) { _listening = false; _toggle.Text = "OUVIR RÁDIO"; await ExecutePlayerAsync("gatPause()"); }
                return;
            }

            if (!_browserReady)
            {
                _state.Text = "Rádio: AO VIVO • player interno indisponível; use ABRIR NO YOUTUBE";
                _state.ForeColor = Color.OrangeRed;
                _toggle.Enabled = false;
                return;
            }

            _toggle.Enabled = true;
            _state.Text = _listening ? "Rádio: AO VIVO • ouvindo" : "Rádio: AO VIVO • clique em OUVIR RÁDIO";
            _state.ForeColor = Color.FromArgb(130, 224, 69);
            if (changed && _listening && _playerReady) await LoadSourceAsync();
        }
        catch (Exception ex)
        {
            _state.Text = "Rádio: Central GAT temporariamente indisponível.";
            _state.ForeColor = Color.Orange;
            _track.Text = "Detalhe: " + ex.Message;
        }
    }

    private async Task ToggleListeningAsync()
    {
        if (!_radioEnabled || string.IsNullOrWhiteSpace(_sourceId)) return;
        if (!_listening)
        {
            _listening = true; _toggle.Text = "PARAR RÁDIO"; _state.Text = "Rádio: AO VIVO • ouvindo";
            if (_playerReady) await LoadSourceAsync();
        }
        else
        {
            _listening = false; _toggle.Text = "OUVIR RÁDIO"; _state.Text = "Rádio: AO VIVO • pausada neste PC";
            await ExecutePlayerAsync("gatPause()");
        }
    }

    private async Task LoadSourceAsync()
    {
        if (!_playerReady || string.IsNullOrWhiteSpace(_sourceId)) return;
        string jsId = Newtonsoft.Json.JsonConvert.SerializeObject(_sourceId);
        string command = _sourceType == "video" ? "gatLoadVideo(" + jsId + ")" : "gatLoadPlaylist(" + jsId + ")";
        await ExecutePlayerAsync(command + ";gatVolume(" + _volume.Value + ")");
    }

    private async Task ExecutePlayerAsync(string script)
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
    }

    private void OpenYoutube()
    {
        if (string.IsNullOrWhiteSpace(_sourceUrl)) return;
        try { Process.Start(new ProcessStartInfo(_sourceUrl) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível abrir o YouTube.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ToggleFullScreen()
    {
        if (!_fullScreenMode)
        {
            _fullScreenMode = true;
            _fullScreen.Text = "SAIR TELA CHEIA";
            WindowState = FormWindowState.Maximized;
            _web.Left = 8; _web.Top = 8; _web.Width = ClientSize.Width - 16; _web.Height = Math.Max(350, ClientSize.Height - 86);
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _state.Top = ClientSize.Height - 68;
            _track.Top = ClientSize.Height - 45;
            _source.Visible = false; _toggle.Visible = false; _openYoutube.Visible = false; _volume.Visible = false;
        }
        else
        {
            _fullScreenMode = false;
            _fullScreen.Text = "TELA CHEIA";
            WindowState = FormWindowState.Normal;
            Size = new Size(760, 680);
            _web.Left = 24; _web.Top = 100; _web.Width = ClientSize.Width - 48; _web.Height = 350;
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _state.Top = 466; _track.Top = 493; _source.Visible = true; _source.Top = 529;
            _toggle.Visible = true; _openYoutube.Visible = true; _volume.Visible = true;
        }
    }

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
