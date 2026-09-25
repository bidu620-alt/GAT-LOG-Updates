using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class LiveStreamItem1689
{
    internal string StreamId = string.Empty;
    internal string User = string.Empty;
    internal string Kind = string.Empty;
    public override string ToString() => User + "   🔴 AO VIVO";
}

internal sealed class LiveBars1689 : Control
{
    private readonly Timer _timer = new Timer { Interval = 180 };
    private readonly int[] _levels = new int[12];
    private int _tick;
    internal bool Active { get; set; }

    internal LiveBars1689()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(5, 20, 36);
        _timer.Tick += delegate
        {
            _tick++;
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = Active ? 2 + ((_tick * 5 + i * 7 + i * i) % 15) : 2;
            Invalidate();
        };
        _timer.Start();
        Disposed += delegate { try { _timer.Dispose(); } catch { } };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int gap = 3;
        int bw = Math.Max(3, (ClientSize.Width - gap * (_levels.Length - 1)) / _levels.Length);
        for (int i = 0; i < _levels.Length; i++)
        {
            int h = Math.Max(3, Math.Min(ClientSize.Height - 2, _levels[i] * Math.Max(2, ClientSize.Height / 18)));
            int x = i * (bw + gap);
            int y = ClientSize.Height - h;
            using (var b = new SolidBrush(i % 2 == 0 ? Color.FromArgb(45, 220, 115) : Color.FromArgb(55, 150, 255)))
                e.Graphics.FillRectangle(b, x, y, bw, h);
        }
    }
}

internal sealed class LiveRtcForm1689 : Form
{
    private const string VirtualHost = "live.gatlogets2.local";
    private readonly WebView2 _web = new WebView2();
    private readonly Label _status = new Label();
    private readonly Button _stop = new Button();
    private readonly string _mode;
    private readonly string _kind;
    private readonly string _streamId;
    private readonly string _owner;
    private readonly string _user;
    private readonly string _token;
    private readonly bool _silentViewer;
    private bool _started;
    private bool _closing;

    internal event Action<bool, string>? LiveStateChanged1689;

    internal LiveRtcForm1689(string mode, string kind, string streamId, string owner, string user, string token, bool silentViewer = false)
    {
        _mode = mode ?? string.Empty;
        _kind = kind ?? string.Empty;
        _streamId = streamId ?? string.Empty;
        _owner = owner ?? string.Empty;
        _user = user ?? string.Empty;
        _token = token ?? string.Empty;
        _silentViewer = silentViewer;

        Text = kind == "radio_mic" ? "GAT Rádio • Locução ao vivo" :
               mode == "broadcast" ? "GAT • Transmitindo rota" : "GAT • Assistindo rota";
        BackColor = Color.FromArgb(3, 11, 22);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 260);
        Size = kind == "route" && mode == "viewer" ? new Size(720, 460) : new Size(520, 330);
        TopMost = kind == "route" && mode == "viewer";
        ShowInTaskbar = !silentViewer;

        _status.Dock = DockStyle.Top;
        _status.Height = 42;
        _status.Padding = new Padding(12, 0, 0, 0);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Color.FromArgb(160, 190, 220);
        _status.Text = mode == "broadcast"
            ? (kind == "radio_mic" ? "Preparando microfone..." : "Selecione a janela do Euro Truck Simulator 2 para transmitir.")
            : "Conectando à transmissão...";
        Controls.Add(_status);

        _stop.Dock = DockStyle.Bottom;
        _stop.Height = 40;
        _stop.FlatStyle = FlatStyle.Flat;
        _stop.BackColor = Color.FromArgb(75, 24, 34);
        _stop.ForeColor = Color.White;
        _stop.Text = mode == "broadcast" ? "PARAR TRANSMISSÃO" : "FECHAR";
        _stop.Click += delegate { Close(); };
        Controls.Add(_stop);

        _web.Dock = DockStyle.Fill;
        _web.BackColor = Color.Black;
        Controls.Add(_web);
        _web.BringToFront();
        _status.BringToFront();
        _stop.BringToFront();

        Shown += async delegate { await InitAsync1689(); };
        FormClosing += async delegate(object? sender, FormClosingEventArgs e)
        {
            if (_closing) return;
            _closing = true;
            try
            {
                if (_web.CoreWebView2 != null)
                    await _web.CoreWebView2.ExecuteScriptAsync("gatStop()");
            }
            catch { }
        };
        FormClosed += delegate
        {
            try { _web.Dispose(); } catch { }
            if (_started) LiveStateChanged1689?.Invoke(false, _kind);
        };

        if (_silentViewer)
        {
            Opacity = 0.02;
            Size = new Size(2, 2);
            MinimumSize = new Size(2, 2);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-3000, -3000);
            _status.Visible = false;
            _stop.Visible = false;
        }
    }

    private async Task InitAsync1689()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string root = Path.Combine(appData, "GAT-LOG", "GAT-Telemetria", "transmissao");
            string webData = Path.Combine(root, "WebView2-" + (_silentViewer ? "audio" : "video"));
            string page = Path.Combine(root, "player");
            Directory.CreateDirectory(webData);
            Directory.CreateDirectory(page);

            var env = await CoreWebView2Environment.CreateAsync(null, webData);
            await _web.EnsureCoreWebView2Async(env);
            File.WriteAllText(Path.Combine(page, "index.html"), Html1689(), Encoding.UTF8);
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, page, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.WebMessageReceived += WebMessage1689;
            _web.Source = new Uri("https://" + VirtualHost + "/index.html?v=1689");

            await WaitReady1689();
            var cfg = new JObject
            {
                ["mode"] = _mode,
                ["kind"] = _kind,
                ["streamId"] = _streamId,
                ["owner"] = _owner,
                ["user"] = _user,
                ["token"] = _token,
                ["fps"] = 30
            };
            await _web.CoreWebView2.ExecuteScriptAsync("gatInit(" + cfg.ToString(Formatting.None) + ")");
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao iniciar transmissão: " + ex.Message;
            _status.ForeColor = Color.OrangeRed;
        }
    }

    private async Task WaitReady1689()
    {
        for (int i = 0; i < 80; i++)
        {
            try
            {
                if (_web.CoreWebView2 != null)
                {
                    string r = await _web.CoreWebView2.ExecuteScriptAsync("typeof gatInit==='function'");
                    if (string.Equals(r, "true", StringComparison.OrdinalIgnoreCase)) return;
                }
            }
            catch { }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Player de transmissão não ficou pronto.");
    }

    private void WebMessage1689(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            JObject m = JObject.Parse(e.WebMessageAsJson);
            string type = Convert.ToString(m["type"]) ?? string.Empty;
            if (type == "started")
            {
                _started = true;
                _status.Text = _kind == "radio_mic" ? "🎙 LOCUÇÃO AO VIVO" :
                    _mode == "broadcast" ? "🔴 SUA ROTA ESTÁ AO VIVO" : "🔴 ASSISTINDO AO VIVO";
                _status.ForeColor = Color.FromArgb(70, 225, 125);
                LiveStateChanged1689?.Invoke(true, _kind);
                if (_silentViewer) Hide();
            }
            else if (type == "stopped")
            {
                if (_started) LiveStateChanged1689?.Invoke(false, _kind);
                _started = false;
                if (!_closing) BeginInvoke((Action)(() => Close()));
            }
            else if (type == "error")
            {
                string message = Convert.ToString(m["message"]) ?? "erro desconhecido";
                _status.Text = "Transmissão: " + message;
                _status.ForeColor = Color.OrangeRed;
            }
            else if (type == "viewer")
            {
                string peer = Convert.ToString(m["user"]) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(peer)) _status.Text = "🔴 AO VIVO • espectador: " + peer;
            }
        }
        catch { }
    }

    private static string Html1689()
    {
        return @"<!doctype html>
<html><head><meta charset='utf-8'>
<style>
html,body{margin:0;width:100%;height:100%;background:#020711;color:#dfefff;font-family:Segoe UI,Arial;overflow:hidden}
#media{width:100%;height:100%;object-fit:contain;background:#000}
#hint{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;padding:20px;color:#8fb5d9}
</style></head><body>
<div id='hint'>GAT LIVE • aguardando conexão...</div>
<video id='media' autoplay playsinline></video>
<audio id='audio' autoplay></audio>
<script>
const API='https://api.gatlogets2.com.br';
let cfg=null,localStream=null,streamId='',last=0,pollTimer=0,beatTimer=0;
let peers=new Map(), viewerPc=null,stopping=false;
const media=document.getElementById('media'),audio=document.getElementById('audio'),hint=document.getElementById('hint');
function post(o){try{chrome.webview.postMessage(o)}catch(e){}}
async function api(path,body){
 const r=await fetch(API+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...body,token:cfg.token})});
 const t=await r.text();let j={};try{j=JSON.parse(t)}catch{}
 if(!r.ok||j.ok===false)throw new Error(j.error||('HTTP '+r.status));return j;
}
function pcNew(){
 const pc=new RTCPeerConnection({iceServers:[{urls:'stun:stun.l.google.com:19302'}]});
 pc.onconnectionstatechange=()=>{if(['failed','closed','disconnected'].includes(pc.connectionState)){}};
 return pc;
}
async function send(to,payload){await api('/api/live/signal',{stream_id:streamId,to_user:to,payload});}
async function broadcasterSignal(s){
 const from=s.from_user,p=s.payload||{};
 if(p.type==='join'){
   if(peers.has(from)){try{peers.get(from).close()}catch(e){}}
   const pc=pcNew();peers.set(from,pc);
   localStream.getTracks().forEach(t=>pc.addTrack(t,localStream));
   pc.onicecandidate=e=>{if(e.candidate)send(from,{type:'ice',candidate:e.candidate}).catch(()=>{})};
   const offer=await pc.createOffer();await pc.setLocalDescription(offer);
   await send(from,{type:'offer',sdp:pc.localDescription});
   post({type:'viewer',user:from});
 }else if(p.type==='answer'){
   const pc=peers.get(from);if(pc)await pc.setRemoteDescription(p.sdp);
 }else if(p.type==='ice'){
   const pc=peers.get(from);if(pc&&p.candidate)try{await pc.addIceCandidate(p.candidate)}catch(e){}
 }
}
async function viewerSignal(s){
 const p=s.payload||{};
 if(p.type==='offer'){
   if(!viewerPc){
     viewerPc=pcNew();
     viewerPc.ontrack=e=>{
       const st=e.streams&&e.streams[0]?e.streams[0]:new MediaStream([e.track]);
       if(cfg.kind==='radio_mic'){audio.srcObject=st;audio.play().catch(()=>{});media.style.display='none'}
       else{media.srcObject=st;media.play().catch(()=>{});audio.style.display='none'}
       hint.style.display='none';post({type:'started'});
     };
     viewerPc.onicecandidate=e=>{if(e.candidate)send(cfg.owner,{type:'ice',candidate:e.candidate}).catch(()=>{})};
   }
   await viewerPc.setRemoteDescription(p.sdp);
   const ans=await viewerPc.createAnswer();await viewerPc.setLocalDescription(ans);
   await send(cfg.owner,{type:'answer',sdp:viewerPc.localDescription});
 }else if(p.type==='ice'){
   if(viewerPc&&p.candidate)try{await viewerPc.addIceCandidate(p.candidate)}catch(e){}
 }
}
async function poll(){
 if(stopping||!streamId)return;
 try{
  const j=await api('/api/live/signals',{stream_id:streamId,since:last});
  for(const s of (j.signals||[])){last=Math.max(last,Number(s.id||0));if(cfg.mode==='broadcast')await broadcasterSignal(s);else await viewerSignal(s)}
 }catch(e){}
 pollTimer=setTimeout(poll,650);
}
async function heartbeat(){if(stopping||cfg.mode!=='broadcast'||!streamId)return;try{await api('/api/live/heartbeat',{stream_id:streamId})}catch(e){}}
async function startBroadcast(){
 const started=await api('/api/live/start',{kind:cfg.kind});streamId=started.stream.stream_id;
 if(cfg.kind==='radio_mic'){
   localStream=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true,autoGainControl:true},video:false});
   media.style.display='none';
 }else{
   localStream=await navigator.mediaDevices.getDisplayMedia({video:{frameRate:{ideal:Number(cfg.fps)||30,max:30}},audio:false});
   media.srcObject=localStream;media.muted=true;await media.play().catch(()=>{});audio.style.display='none';hint.style.display='none';
 }
 localStream.getTracks().forEach(t=>t.addEventListener('ended',()=>gatStop()));
 post({type:'started',stream_id:streamId});poll();beatTimer=setInterval(heartbeat,7000);
}
async function startViewer(){
 streamId=cfg.streamId;
 await send(cfg.owner,{type:'join'});
 poll();
 setInterval(()=>send(cfg.owner,{type:'join'}).catch(()=>{}),8000);
}
async function gatInit(x){
 cfg=x;try{if(cfg.mode==='broadcast')await startBroadcast();else await startViewer()}catch(e){post({type:'error',message:String(e.message||e)})}
}
async function gatStop(){
 if(stopping)return;stopping=true;
 clearTimeout(pollTimer);clearInterval(beatTimer);
 try{if(localStream)localStream.getTracks().forEach(t=>t.stop())}catch(e){}
 try{for(const p of peers.values())p.close()}catch(e){}peers.clear();
 try{if(viewerPc)viewerPc.close()}catch(e){}
 if(cfg&&cfg.mode==='broadcast'&&streamId){try{await api('/api/live/stop',{stream_id:streamId})}catch(e){}}
 post({type:'stopped'});
}
window.addEventListener('beforeunload',()=>{gatStop()});
</script></body></html>";
    }
}

internal sealed partial class MainForm
{
    private readonly Timer _liveTimer1689 = new Timer { Interval = 1400 };
    private readonly List<LiveStreamItem1689> _liveRoutes1689 = new List<LiveStreamItem1689>();
    private LiveBars1689? _radioBars1689;
    private Label? _radioLiveLabel1689;
    private TrackBar? _radioVolume1689;
    private ListBox? _liveList1689;
    private Label? _liveCount1689;
    private Button? _routeBroadcast1689;
    private Button? _watch1689;
    private Button? _mic1689;
    private string _role1689 = string.Empty;
    private LiveRtcForm1689? _routeCaster1689;
    private LiveRtcForm1689? _micCaster1689;
    private LiveRtcForm1689? _autoMicViewer1689;
    private string _autoMicStream1689 = string.Empty;
    private int _radioBaseVolume1689 = 55;
    private bool _liveBusy1689;

    private void BuildLiveHome1689(Panel p)
    {
        var radio = new Panel
        {
            Left = 0, Top = 430, Width = Math.Max(420, p.Width / 2 - 8), Height = 150,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };
        radio.Controls.Add(new Label
        {
            Text = "GAT RÁDIO", Left = 14, Top = 10, Width = 130, Height = 24,
            ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold)
        });
        _radioLiveLabel1689 = new Label
        {
            Text = "● CONECTANDO", Left = 150, Top = 11, Width = 200, Height = 22,
            ForeColor = Color.FromArgb(70, 225, 125), Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold)
        };
        radio.Controls.Add(_radioLiveLabel1689);
        _radioBars1689 = new LiveBars1689 { Left = 14, Top = 42, Width = 170, Height = 42 };
        radio.Controls.Add(_radioBars1689);
        radio.Controls.Add(new Label { Text = "VOLUME", Left = 200, Top = 43, Width = 70, Height = 20, ForeColor = Color.FromArgb(145, 170, 199) });
        _radioVolume1689 = new TrackBar { Left = 260, Top = 34, Width = 170, Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 55 };
        _radioVolume1689.Scroll += async delegate
        {
            _radioBaseVolume1689 = _radioVolume1689.Value;
            if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
                await _hubRadio041.SetExternalVolume1689(_radioBaseVolume1689);
        };
        radio.Controls.Add(_radioVolume1689);
        var open = HubButton041("ABRIR SOBREPOSIÇÃO", 190);
        open.Left = 200; open.Top = 93; open.Click += delegate
        {
            EnsureRadio041();
            try { _hubRadio041?.OpenOverlay1689(); } catch { }
        };
        radio.Controls.Add(open);
        p.Controls.Add(radio);

        var live = new Panel
        {
            Left = Math.Max(430, p.Width / 2 + 8), Top = 430,
            Width = Math.Max(420, p.Width / 2 - 8), Height = 150,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };
        live.Controls.Add(new Label
        {
            Text = "MOTORISTAS AO VIVO", Left = 14, Top = 10, Width = 190, Height = 24,
            ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold)
        });
        _liveCount1689 = new Label { Text = "0 transmitindo", Left = 208, Top = 12, Width = 120, Height = 20, ForeColor = Color.FromArgb(145, 170, 199) };
        live.Controls.Add(_liveCount1689);
        _liveList1689 = new ListBox
        {
            Left = 14, Top = 40, Width = 225, Height = 92, BackColor = Color.FromArgb(8, 29, 50),
            ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle
        };
        live.Controls.Add(_liveList1689);
        _watch1689 = HubButton041("▶ ASSISTIR", 125);
        _watch1689.Left = 252; _watch1689.Top = 40; _watch1689.Click += delegate { WatchSelected1689(); };
        live.Controls.Add(_watch1689);
        _routeBroadcast1689 = HubButton041("🔴 TRANSMITIR ROTA", 155);
        _routeBroadcast1689.Left = 252; _routeBroadcast1689.Top = 82; _routeBroadcast1689.Click += delegate { ToggleRouteBroadcast1689(); };
        live.Controls.Add(_routeBroadcast1689);
        _mic1689 = HubButton041("🎙 ENTRAR AO VIVO", 155);
        _mic1689.Left = 252; _mic1689.Top = 122; _mic1689.Visible = false; _mic1689.Click += delegate { ToggleMicBroadcast1689(); };
        live.Controls.Add(_mic1689);
        p.Controls.Add(live);

        p.Resize += delegate
        {
            int half = Math.Max(420, (p.ClientSize.Width - 16) / 2);
            radio.Width = half;
            live.Left = half + 16;
            live.Width = Math.Max(420, p.ClientSize.Width - live.Left);
        };
    }

    private async Task LiveMediaShown1689()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "radio"));
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transmissao"));
        }
        catch { }

        EnsureRadio041();
        try
        {
            if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
            {
                await _hubRadio041.StartLiveAuto1689();
                _radioBaseVolume1689 = _hubRadio041.ExternalVolume1689;
                if (_radioVolume1689 != null) _radioVolume1689.Value = Math.Max(0, Math.Min(100, _radioBaseVolume1689));
            }
        }
        catch { }

        await RefreshRole1689();
        await RefreshLiveStreams1689();
        _liveTimer1689.Tick += async delegate { await RefreshLiveStreams1689(); SyncRadioHome1689(); };
        _liveTimer1689.Start();
    }

    private async Task RefreshRole1689()
    {
        _role1689 = string.Empty;
        if (!AccountReady || string.IsNullOrWhiteSpace(_accountToken)) { ApplyRole1689(); return; }
        try
        {
            var body = new JObject { ["token"] = _accountToken };
            using (var c = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage r = await _hubHttp041.PostAsync("https://api.gatlogets2.com.br/api/account/session", c))
            {
                JObject j = JObject.Parse(await r.Content.ReadAsStringAsync());
                if (r.IsSuccessStatusCode && j.Value<bool?>("ok") == true)
                    _role1689 = (Convert.ToString(j["role"]) ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
        catch { }
        ApplyRole1689();
    }

    private void ApplyRole1689()
    {
        bool canMic = _role1689 == "owner" || _role1689 == "admin" || _role1689 == "moderator";
        if (_mic1689 != null) _mic1689.Visible = canMic;
    }

    private async Task RefreshLiveStreams1689()
    {
        if (_liveBusy1689) return;
        _liveBusy1689 = true;
        try
        {
            string text = await _hubHttp041.GetStringAsync("https://api.gatlogets2.com.br/api/public/live?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            JObject root = JObject.Parse(text);
            JArray rows = root["streams"] as JArray ?? new JArray();
            var routes = new List<LiveStreamItem1689>();
            JObject? mic = null;
            foreach (JObject x in rows.OfType<JObject>())
            {
                string kind = Convert.ToString(x["kind"]) ?? string.Empty;
                if (kind == "route")
                {
                    routes.Add(new LiveStreamItem1689
                    {
                        StreamId = Convert.ToString(x["stream_id"]) ?? string.Empty,
                        User = Convert.ToString(x["user"]) ?? string.Empty,
                        Kind = kind
                    });
                }
                else if (kind == "radio_mic") mic = x;
            }

            _liveRoutes1689.Clear();
            _liveRoutes1689.AddRange(routes);
            if (_liveList1689 != null)
            {
                string selected = (_liveList1689.SelectedItem as LiveStreamItem1689)?.StreamId ?? string.Empty;
                _liveList1689.Items.Clear();
                foreach (var x in routes) _liveList1689.Items.Add(x);
                for (int i = 0; i < _liveList1689.Items.Count; i++)
                    if ((_liveList1689.Items[i] as LiveStreamItem1689)?.StreamId == selected) _liveList1689.SelectedIndex = i;
            }
            if (_liveCount1689 != null) _liveCount1689.Text = routes.Count + " transmitindo";

            await SyncAutoMic1689(mic);
        }
        catch
        {
            if (_liveCount1689 != null) _liveCount1689.Text = "Central indisponível";
        }
        finally { _liveBusy1689 = false; }
    }

    private async Task SyncAutoMic1689(JObject? mic)
    {
        string stream = mic == null ? string.Empty : (Convert.ToString(mic["stream_id"]) ?? string.Empty);
        string owner = mic == null ? string.Empty : (Convert.ToString(mic["user"]) ?? string.Empty);

        if (string.IsNullOrWhiteSpace(stream) || string.Equals(owner, _accountUser, StringComparison.OrdinalIgnoreCase))
        {
            if (_autoMicViewer1689 != null)
            {
                try { _autoMicViewer1689.Close(); } catch { }
                _autoMicViewer1689 = null; _autoMicStream1689 = string.Empty;
                await RestoreRadioAfterMic1689();
            }
            return;
        }

        if (_autoMicViewer1689 != null && _autoMicStream1689 == stream) return;
        if (!AccountReady || string.IsNullOrWhiteSpace(_accountToken)) return;

        if (_autoMicViewer1689 != null) try { _autoMicViewer1689.Close(); } catch { }
        _autoMicStream1689 = stream;
        _autoMicViewer1689 = new LiveRtcForm1689("viewer", "radio_mic", stream, owner, _accountUser, _accountToken, true);
        _autoMicViewer1689.LiveStateChanged1689 += async (on, kind) =>
        {
            if (on) await DuckRadioForMic1689(); else await RestoreRadioAfterMic1689();
        };
        _autoMicViewer1689.Show(this);
    }

    private void SyncRadioHome1689()
    {
        try
        {
            if (_hubRadio041 == null || _hubRadio041.IsDisposed) return;
            string state = _hubRadio041.LiveState1689;
            bool live = state.IndexOf("AO VIVO", StringComparison.OrdinalIgnoreCase) >= 0;
            if (_radioLiveLabel1689 != null)
            {
                _radioLiveLabel1689.Text = live ? "● AO VIVO" : "● " + (string.IsNullOrWhiteSpace(state) ? "CONECTANDO" : "RÁDIO");
                _radioLiveLabel1689.ForeColor = live ? Color.FromArgb(70, 225, 125) : Color.FromArgb(145, 170, 199);
            }
            if (_radioBars1689 != null) _radioBars1689.Active = live && _hubRadio041.ExternalVolume1689 > 0;
        }
        catch { }
    }

    private void ToggleRouteBroadcast1689()
    {
        if (_routeCaster1689 != null && !_routeCaster1689.IsDisposed)
        {
            _routeCaster1689.Close();
            return;
        }
        if (!AccountReady || string.IsNullOrWhiteSpace(_accountToken))
        {
            MessageBox.Show(this, "Entre primeiro na Conta GAT para transmitir sua rota.", "Transmissão GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _routeCaster1689 = new LiveRtcForm1689("broadcast", "route", string.Empty, _accountUser, _accountUser, _accountToken);
        _routeCaster1689.LiveStateChanged1689 += (on, kind) =>
        {
            if (_routeBroadcast1689 != null) _routeBroadcast1689.Text = on ? "⏹ PARAR TRANSMISSÃO" : "🔴 TRANSMITIR ROTA";
        };
        _routeCaster1689.FormClosed += delegate
        {
            _routeCaster1689 = null;
            if (_routeBroadcast1689 != null) _routeBroadcast1689.Text = "🔴 TRANSMITIR ROTA";
        };
        _routeCaster1689.Show(this);
    }

    private void WatchSelected1689()
    {
        if (_liveList1689?.SelectedItem is not LiveStreamItem1689 item) return;
        if (!AccountReady || string.IsNullOrWhiteSpace(_accountToken))
        {
            MessageBox.Show(this, "Entre na Conta GAT para assistir uma transmissão.", "GAT Ao Vivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var f = new LiveRtcForm1689("viewer", "route", item.StreamId, item.User, _accountUser, _accountToken);
        f.Show(this);
    }

    private void ToggleMicBroadcast1689()
    {
        if (_micCaster1689 != null && !_micCaster1689.IsDisposed)
        {
            _micCaster1689.Close();
            return;
        }
        bool can = _role1689 == "owner" || _role1689 == "admin" || _role1689 == "moderator";
        if (!can)
        {
            MessageBox.Show(this, "Sua conta não tem permissão de locutor.", "GAT Rádio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _micCaster1689 = new LiveRtcForm1689("broadcast", "radio_mic", string.Empty, _accountUser, _accountUser, _accountToken);
        _micCaster1689.LiveStateChanged1689 += async (on, kind) =>
        {
            if (_mic1689 != null) _mic1689.Text = on ? "⏹ SAIR DO AO VIVO" : "🎙 ENTRAR AO VIVO";
            if (on) await DuckRadioForMic1689(); else await RestoreRadioAfterMic1689();
        };
        _micCaster1689.FormClosed += async delegate
        {
            _micCaster1689 = null;
            if (_mic1689 != null) _mic1689.Text = "🎙 ENTRAR AO VIVO";
            await RestoreRadioAfterMic1689();
        };
        _micCaster1689.Show(this);
    }

    private async Task DuckRadioForMic1689()
    {
        try
        {
            if (_hubRadio041 == null || _hubRadio041.IsDisposed) return;
            _radioBaseVolume1689 = _radioVolume1689?.Value ?? _hubRadio041.ExternalVolume1689;
            await _hubRadio041.SetExternalVolume1689(Math.Max(5, _radioBaseVolume1689 / 5));
            if (_radioLiveLabel1689 != null) _radioLiveLabel1689.Text = "🎙 LOCUÇÃO AO VIVO";
        }
        catch { }
    }

    private async Task RestoreRadioAfterMic1689()
    {
        try
        {
            if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
                await _hubRadio041.SetExternalVolume1689(_radioBaseVolume1689);
        }
        catch { }
    }

    private void DisposeLiveMedia1689()
    {
        try { _liveTimer1689.Stop(); _liveTimer1689.Dispose(); } catch { }
        try { _routeCaster1689?.Close(); _micCaster1689?.Close(); _autoMicViewer1689?.Close(); } catch { }
    }
}
