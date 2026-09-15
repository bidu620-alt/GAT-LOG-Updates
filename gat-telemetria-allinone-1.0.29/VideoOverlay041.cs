using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class VideoOverlay041 : Form
{
    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    private readonly WebView2 _web = new WebView2();
    private bool _ready;

    internal VideoOverlay041()
    {
        Text = "GAT DASH • Vídeo flutuante";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(500, 320);
        MinimumSize = new Size(300, 190);
        Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 530), 80);
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        _web.Dock = DockStyle.Fill;
        _web.BackColor = Color.Black;
        Controls.Add(_web);
        Shown += async delegate { await InitAsync(); };
        FormClosed += delegate { try { _web.Dispose(); _http.Dispose(); } catch { } };
    }

    private async Task InitAsync()
    {
        if (_ready) return;
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "VideoOverlay-1.0.41");
            string www = Path.Combine(root, "www");
            Directory.CreateDirectory(www);
            File.WriteAllText(Path.Combine(www, "index.html"), Html(), Encoding.UTF8);
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "WebView2"));
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping("gatoverlay.local", www, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.WebMessageReceived += async delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
            {
                try
                {
                    JObject m = JObject.Parse(e.WebMessageAsJson);
                    string type = Convert.ToString(m["type"]) ?? string.Empty;
                    if (type == "mediaRefresh") await PushMediaAsync();
                    else if (type == "mediaMode")
                    {
                        string mode = Convert.ToString(m["mode"]) ?? string.Empty;
                        mode = mode.Trim().ToLowerInvariant();
                        if (mode == "gat" || mode == "mine" || mode == "web")
                            File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode);
                        await PushMediaAsync();
                    }
                }
                catch { }
            };
            _web.Source = new Uri("https://gatoverlay.local/index.html?v=1041");
            _ready = true;
        }
        catch { }
    }

    private async Task PushMediaAsync()
    {
        if (!_ready || _web.CoreWebView2 == null) return;
        try
        {
            string json = await _http.GetStringAsync("http://127.0.0.1:31378/api/gat/media?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await _web.CoreWebView2.ExecuteScriptAsync("window.pushMedia(" + JsonConvert.SerializeObject(json) + ")");
        }
        catch { try { await _web.CoreWebView2.ExecuteScriptAsync("window.mediaError()"); } catch { } }
    }

    private static string Html()
    {
        return @"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><style>
html,body{margin:0;width:100%;height:100%;background:#02060c;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif;overflow:hidden}.app{height:100%;display:grid;grid-template-rows:38px 1fr}.top{display:flex;align-items:center;gap:6px;padding:0 8px;background:#061525;border-bottom:1px solid #174b78}.brand{font-weight:800;color:#55a5ff;margin-right:auto}.tab{border:1px solid #245b8d;background:#09223a;color:#bcd7f7;border-radius:5px;padding:4px 8px;font-size:11px;cursor:pointer}.tab.on{background:#124b7f;color:white}.stage{position:relative;background:#000}.stage iframe{border:0;width:100%;height:100%}.empty{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;color:#8ca8c7;padding:20px}.hidden{display:none}.badge{font-size:10px;color:#78b8ff}</style></head><body><div class='app'><div class='top'><span class='brand'>GAT • VÍDEO</span><button id='gat' class='tab'>Canal GAT</button><button id='mine' class='tab'>Meu Vídeo</button><button id='web' class='tab'>Canal Web</button></div><div class='stage'><iframe id='frame' allow='autoplay; encrypted-media; picture-in-picture' allowfullscreen></iframe><div id='empty' class='empty'>Aguardando Rádio/TV GAT...</div></div></div><script>
const state={mode:'gat',media:null,embed:''};const post=o=>{try{chrome.webview.postMessage(o)}catch{}};function yt(url){try{const u=new URL(url),h=u.hostname.toLowerCase(),list=u.searchParams.get('list');const s='&autoplay=1&rel=0&playsinline=1';if(list)return 'https://www.youtube.com/embed/videoseries?list='+encodeURIComponent(list)+s;let id=h==='youtu.be'?u.pathname.slice(1).split('/')[0]:u.searchParams.get('v')||'';if(!id){const p=u.pathname.split('/').filter(Boolean);if(p.length>1)id=p[1]}return /^[A-Za-z0-9_-]{11}$/.test(id)?'https://www.youtube.com/embed/'+id+'?autoplay=1&rel=0&playsinline=1':''}catch{return ''}}function pick(){const m=state.media||{};if(state.mode==='mine')return(m.myVideo||{}).url||'';if(state.mode==='web')return(m.web||{}).url||'';return(m.channelGat&&m.channelGat.enabled)?m.channelGat.url||'':''}function render(){['gat','mine','web'].forEach(x=>document.getElementById(x).classList.toggle('on',x===state.mode));const url=String(pick()||'').trim(),f=document.getElementById('frame'),e=document.getElementById('empty');if(!url){f.classList.add('hidden');e.classList.remove('hidden');e.textContent='Nenhuma fonte ativa na Rádio/TV GAT.';return}const src=state.mode==='web'?url:(yt(url)||url);if(src!==state.embed){state.embed=src;f.src=src}f.classList.remove('hidden');e.classList.add('hidden')}window.pushMedia=p=>{let m=p;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}state.media=m;if(['gat','mine','web'].includes(String(m.activeMode||'')))state.mode=String(m.activeMode);render()};window.mediaError=()=>{document.getElementById('empty').textContent='Abra o GAT Telemetria e confira a Rádio GAT.'};['gat','mine','web'].forEach(x=>document.getElementById(x).onclick=()=>{state.mode=x;post({type:'mediaMode',mode:x});render()});post({type:'mediaRefresh'});setInterval(()=>post({type:'mediaRefresh'}),3000);
</script></body></html>";
    }
}
