using System;
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
    private readonly TrackBar _volume = new TrackBar();

    private bool _browserReady;
    private bool _playerReady;
    private bool _listening;
    private bool _radioEnabled;
    private string _playlistId = string.Empty;
    private string _playlistUrl = string.Empty;
    private long _revision = -1;

    public RadioForm()
    {
        Text = "Rádio GAT";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 540);
        Size = new Size(640, 590);
        BackColor = Color.FromArgb(4, 13, 25);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        BuildUi();

        Shown += async delegate
        {
            await InitializePlayerAsync();
            await RefreshRadioAsync(true);
            _pollTimer.Start();
        };

        FormClosing += async (sender, e) =>
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
        var title = new Label
        {
            Text = "RÁDIO GAT",
            Left = 24,
            Top = 18,
            Width = 250,
            Height = 35,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            ForeColor = Color.White
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "A programação é definida pela Central GAT. O áudio toca somente aqui no Telemetria.",
            Left = 26,
            Top = 56,
            Width = 570,
            Height = 40,
            ForeColor = Color.FromArgb(168, 181, 199)
        };
        Controls.Add(subtitle);

        _web.Left = 24;
        _web.Top = 102;
        _web.Width = ClientSize.Width - 48;
        _web.Height = 300;
        _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _web.BackColor = Color.Black;
        Controls.Add(_web);

        _state.Text = "Rádio: conectando à Central GAT...";
        _state.Left = 26;
        _state.Top = 420;
        _state.Width = 565;
        _state.Height = 22;
        _state.ForeColor = Color.FromArgb(130, 224, 69);
        _state.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        Controls.Add(_state);

        _track.Text = "Tocando agora: —";
        _track.Left = 26;
        _track.Top = 447;
        _track.Width = 565;
        _track.Height = 22;
        _track.ForeColor = Color.Gainsboro;
        Controls.Add(_track);

        _source.Text = "Playlist: —";
        _source.Left = 26;
        _source.Top = 473;
        _source.Width = 565;
        _source.Height = 22;
        _source.ForeColor = Color.FromArgb(143, 158, 178);
        Controls.Add(_source);

        _toggle.Text = "OUVIR RÁDIO";
        _toggle.Left = 24;
        _toggle.Top = 507;
        _toggle.Width = 175;
        _toggle.Height = 36;
        _toggle.FlatStyle = FlatStyle.Flat;
        _toggle.BackColor = Color.FromArgb(11, 43, 82);
        _toggle.ForeColor = Color.FromArgb(215, 229, 249);
        _toggle.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        _toggle.FlatAppearance.BorderColor = Color.FromArgb(60, 137, 245);
        _toggle.Cursor = Cursors.Hand;
        _toggle.Click += async delegate { await ToggleListeningAsync(); };
        Controls.Add(_toggle);

        var volumeLabel = new Label
        {
            Text = "VOLUME",
            Left = 220,
            Top = 511,
            Width = 72,
            Height = 22,
            ForeColor = Color.FromArgb(168, 181, 199)
        };
        Controls.Add(volumeLabel);

        _volume.Left = 288;
        _volume.Top = 502;
        _volume.Width = 300;
        _volume.Height = 45;
        _volume.Minimum = 0;
        _volume.Maximum = 100;
        _volume.TickFrequency = 10;
        _volume.Value = LoadVolume();
        _volume.Scroll += async delegate
        {
            SaveVolume(_volume.Value);
            if (_playerReady) await ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
        };
        Controls.Add(_volume);

        Resize += delegate
        {
            _web.Width = Math.Max(300, ClientSize.Width - 48);
            _state.Width = Math.Max(300, ClientSize.Width - 52);
            _track.Width = Math.Max(300, ClientSize.Width - 52);
            _source.Width = Math.Max(300, ClientSize.Width - 52);
            _volume.Width = Math.Max(160, ClientSize.Width - 316);
        };
    }

    private async Task InitializePlayerAsync()
    {
        if (_browserReady) return;
        try
        {
            await _web.EnsureCoreWebView2Async();
            string folder = Path.Combine(Application.LocalUserAppDataPath, "radio-player");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "index.html"), PlayerHtml());
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, folder, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.WebMessageReceived += WebMessageReceived;
            _web.Source = new Uri("https://" + VirtualHost + "/index.html");
            _browserReady = true;
        }
        catch (Exception ex)
        {
            _state.Text = "Rádio: não foi possível iniciar o player WebView2.";
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
                if (_listening && _radioEnabled && !string.IsNullOrWhiteSpace(_playlistId))
                    _ = LoadPlaylistAsync(_playlistId);
            }
            else if (type == "track")
            {
                string name = Convert.ToString(msg["title"]);
                if (!string.IsNullOrWhiteSpace(name)) _track.Text = "Tocando agora: " + name;
            }
            else if (type == "error")
            {
                _track.Text = "Player do YouTube informou erro " + Convert.ToString(msg["code"]) + ".";
            }
        }
        catch { }
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
            string playlistId = Convert.ToString(radio["playlist_id"]) ?? string.Empty;
            string playlistUrl = Convert.ToString(radio["playlist_url"]) ?? string.Empty;
            long revision = radio.Value<long?>("revision") ?? 0;
            bool changed = force || revision != _revision || !string.Equals(playlistId, _playlistId, StringComparison.Ordinal);

            _radioEnabled = enabled;
            _playlistId = playlistId;
            _playlistUrl = playlistUrl;
            _revision = revision;

            _source.Text = string.IsNullOrWhiteSpace(playlistUrl) ? "Playlist: nenhuma configurada" : "Playlist oficial da Central GAT • revisão " + revision;

            if (!enabled || string.IsNullOrWhiteSpace(playlistId))
            {
                _state.Text = "Rádio: DESLIGADA pela Central GAT";
                _state.ForeColor = Color.FromArgb(168, 181, 199);
                _toggle.Enabled = false;
                if (_listening)
                {
                    _listening = false;
                    _toggle.Text = "OUVIR RÁDIO";
                    await ExecutePlayerAsync("gatPause()");
                }
                return;
            }

            _toggle.Enabled = _browserReady;
            _state.Text = _listening ? "Rádio: AO VIVO • ouvindo" : "Rádio: AO VIVO • clique em OUVIR RÁDIO";
            _state.ForeColor = Color.FromArgb(130, 224, 69);
            if (changed && _listening && _playerReady) await LoadPlaylistAsync(playlistId);
        }
        catch
        {
            _state.Text = "Rádio: Central GAT temporariamente indisponível.";
            _state.ForeColor = Color.Orange;
        }
    }

    private async Task ToggleListeningAsync()
    {
        if (!_radioEnabled || string.IsNullOrWhiteSpace(_playlistId)) return;
        if (!_listening)
        {
            _listening = true;
            _toggle.Text = "PARAR RÁDIO";
            _state.Text = "Rádio: AO VIVO • ouvindo";
            if (_playerReady) await LoadPlaylistAsync(_playlistId);
        }
        else
        {
            _listening = false;
            _toggle.Text = "OUVIR RÁDIO";
            _state.Text = "Rádio: AO VIVO • pausada neste PC";
            await ExecutePlayerAsync("gatPause()");
        }
    }

    private async Task LoadPlaylistAsync(string playlistId)
    {
        if (!_playerReady || string.IsNullOrWhiteSpace(playlistId)) return;
        string jsId = Newtonsoft.Json.JsonConvert.SerializeObject(playlistId);
        await ExecutePlayerAsync("gatLoad(" + jsId + ");gatVolume(" + _volume.Value + ")");
    }

    private async Task ExecutePlayerAsync(string script)
    {
        if (!_browserReady || _web.CoreWebView2 == null) return;
        try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
    }

    private static int LoadVolume()
    {
        try
        {
            string file = Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt");
            int value;
            if (int.TryParse(File.ReadAllText(file), out value)) return Math.Max(0, Math.Min(100, value));
        }
        catch { }
        return 55;
    }

    private static void SaveVolume(int value)
    {
        try
        {
            Directory.CreateDirectory(Application.LocalUserAppDataPath);
            File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt"), value.ToString());
        }
        catch { }
    }

    private static string PlayerHtml()
    {
        return @"<!doctype html>
<html><head><meta charset='utf-8'><meta name='referrer' content='strict-origin-when-cross-origin'>
<style>html,body{margin:0;width:100%;height:100%;background:#020711;overflow:hidden}#wrap{display:flex;align-items:center;justify-content:center;width:100%;height:100%}#player{width:100%;height:100%}</style></head>
<body><div id='wrap'><div id='player'></div></div><script src='https://www.youtube.com/iframe_api'></script><script>
let player=null,ready=false,pending='';
function post(o){try{chrome.webview.postMessage(o)}catch(e){}}
function onYouTubeIframeAPIReady(){player=new YT.Player('player',{width:'100%',height:'100%',playerVars:{controls:0,disablekb:1,fs:0,playsinline:1,rel:0,origin:'https://radio.gatlogets2.local'},events:{onReady:function(){ready=true;post({type:'ready'});if(pending){gatLoad(pending);pending=''}},onStateChange:function(e){if(e.data===YT.PlayerState.PLAYING){let d=player.getVideoData()||{};post({type:'track',title:d.title||''})}},onError:function(e){post({type:'error',code:e.data})}}})}
function gatLoad(id){if(!ready){pending=id;return}try{player.loadPlaylist({listType:'playlist',list:id,index:0,startSeconds:0})}catch(e){post({type:'error',code:'load'})}}
function gatPause(){if(ready&&player)player.pauseVideo()}
function gatVolume(v){if(ready&&player)player.setVolume(Math.max(0,Math.min(100,Number(v)||0)))}
</script></body></html>";
    }
}
