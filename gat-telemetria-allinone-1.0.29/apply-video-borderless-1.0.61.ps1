param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $video) { throw 'Fonte 1.0.60 incompleto para aplicar 1.0.61.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# Versao 1.0.61.
$mainText = $mainText.Replace('CurrentVersion = "1.0.60.0"', 'CurrentVersion = "1.0.61.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.60"', 'Text = "Cliente 1.0.61"')
$mainText = $mainText.Replace('HUB 1.0.60:', 'HUB 1.0.61:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+"', 'Text = "GAT Telemetria BETA 1.0.61"', 1)
$hubText = [regex]::Replace($hubText, 'Cliente 1\.0\.\d+ TESTE', 'Cliente 1.0.61')

# ---------------------------------------------------------------------------
# SOBREPOSICAO DE VIDEO SEM BARRA BRANCA DO WINDOWS.
# Mantem janela independente, redimensionavel, minimizavel e fechavel.
# A faixa GAT • VIDEO vira a propria barra da janela.
# ---------------------------------------------------------------------------
if ($videoText -notmatch 'using System\.Runtime\.InteropServices;') {
    $videoText = $videoText.Replace('using System.Net.Http;', "using System.Net.Http;`r`nusing System.Runtime.InteropServices;")
}

$videoText = [regex]::Replace($videoText, 'FormBorderStyle\s*=\s*FormBorderStyle\.[A-Za-z]+\s*;', 'FormBorderStyle = FormBorderStyle.None;', 1)
$videoText = [regex]::Replace($videoText, 'ShowInTaskbar\s*=\s*(true|false)\s*;', 'ShowInTaskbar = true;', 1)
$videoText = [regex]::Replace($videoText, 'TopMost\s*=\s*(true|false)\s*;', 'TopMost = true;', 1)

# Cria uma borda escura de 5px que recebe o hit-test de redimensionamento.
$backNeedle = '        BackColor = Color.Black;'
if ($videoText.Contains($backNeedle) -and $videoText -notlike '*GAT_VIDEO_BORDERLESS_061*') {
    $videoText = $videoText.Replace($backNeedle, $backNeedle + "`r`n        Padding = new Padding(5);`r`n        ResizeRedraw = true;")
}

# Controles da janela vindos do HTML/WebView.
$messageNeedle = '                    if (type == "mediaRefresh") await PushMediaAsync();'
if ($videoText.Contains($messageNeedle) -and $videoText -notlike '*windowMinimize*') {
    $messagePatch = @'
                    if (type == "windowMinimize")
                    {
                        WindowState = FormWindowState.Minimized;
                        return;
                    }
                    if (type == "windowClose")
                    {
                        Close();
                        return;
                    }
                    if (type == "windowToggleMax")
                    {
                        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                        return;
                    }
                    if (type == "windowDrag")
                    {
                        if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
                        ReleaseCapture061();
                        SendMessage061(Handle, WM_NCLBUTTONDOWN_061, (IntPtr)HTCAPTION_061, IntPtr.Zero);
                        return;
                    }
                    if (type == "mediaRefresh") await PushMediaAsync();
'@.TrimEnd()
    $videoText = $videoText.Replace($messageNeedle, $messagePatch)
}

# Helpers nativos para mover/redimensionar a janela sem moldura.
$helperMarker = '    private async Task InitAsync()'
$helperIndex = $videoText.IndexOf($helperMarker)
if ($helperIndex -lt 0) { throw 'InitAsync do overlay de video nao encontrado.' }
if ($videoText -notlike '*GAT_VIDEO_BORDERLESS_061*') {
$helpers = @'
    // GAT_VIDEO_BORDERLESS_061
    private const int WM_NCLBUTTONDOWN_061 = 0x00A1;
    private const int HTCAPTION_061 = 2;
    private const int WM_NCHITTEST_061 = 0x0084;
    private const int HTCLIENT_061 = 1;
    private const int HTLEFT_061 = 10;
    private const int HTRIGHT_061 = 11;
    private const int HTTOP_061 = 12;
    private const int HTTOPLEFT_061 = 13;
    private const int HTTOPRIGHT_061 = 14;
    private const int HTBOTTOM_061 = 15;
    private const int HTBOTTOMLEFT_061 = 16;
    private const int HTBOTTOMRIGHT_061 = 17;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture061();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage061(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST_061 || WindowState != FormWindowState.Normal || (int)m.Result != HTCLIENT_061) return;

        Point p = PointToClient(Cursor.Position);
        int g = 7;
        bool left = p.X <= g;
        bool right = p.X >= ClientSize.Width - g;
        bool top = p.Y <= g;
        bool bottom = p.Y >= ClientSize.Height - g;

        if (left && top) m.Result = (IntPtr)HTTOPLEFT_061;
        else if (right && top) m.Result = (IntPtr)HTTOPRIGHT_061;
        else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT_061;
        else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT_061;
        else if (left) m.Result = (IntPtr)HTLEFT_061;
        else if (right) m.Result = (IntPtr)HTRIGHT_061;
        else if (top) m.Result = (IntPtr)HTTOP_061;
        else if (bottom) m.Result = (IntPtr)HTBOTTOM_061;
    }

'@
    $videoText = $videoText.Insert($helperIndex, $helpers)
}

# Substitui o HTML do player por uma barra GAT propria com minimizar/fechar.
$htmlPattern = '(?s)    private static string Html\(\)\s*\{.*?\r?\n    \}\s*\r?\n\}'
if (-not [regex]::IsMatch($videoText, $htmlPattern)) { throw 'Metodo Html do overlay de video nao encontrado.' }
$newHtml = @'
    private static string Html()
    {
        return @"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><style>
*{box-sizing:border-box}html,body{margin:0;width:100%;height:100%;background:#02060c;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif;overflow:hidden;user-select:none}.app{height:100%;display:grid;grid-template-rows:40px 1fr}.top{display:flex;align-items:center;gap:7px;padding:0 7px 0 10px;background:linear-gradient(90deg,#061525,#071c31);border-bottom:1px solid #174b78;cursor:default}.brand{font-weight:800;color:#55a5ff;margin-right:auto;letter-spacing:.2px}.tab,.win{border:1px solid #245b8d;background:#09223a;color:#d4e8ff;border-radius:5px;height:25px;padding:0 10px;font-size:11px;cursor:pointer}.tab.on{background:#124b7f;color:#fff}.win{width:30px;padding:0;font-size:16px;line-height:22px}.win:hover{background:#143b5e}.win.close:hover{background:#b4232c;border-color:#e14951;color:#fff}.stage{position:relative;background:#000}.stage iframe{border:0;width:100%;height:100%}.empty{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;color:#8ca8c7;padding:20px}.hidden{display:none}</style></head><body><div class='app'><div id='bar' class='top'><span class='brand'>GAT • VÍDEO</span><button id='gat' class='tab'>Canal GAT</button><button id='mine' class='tab'>Meu Vídeo</button><button id='min' class='win' title='Minimizar'>—</button><button id='close' class='win close' title='Fechar'>×</button></div><div class='stage'><iframe id='frame' allow='autoplay; encrypted-media; picture-in-picture' allowfullscreen></iframe><div id='empty' class='empty'>Aguardando Rádio/TV GAT...</div></div></div><script>
const state={mode:'gat',media:null,embed:''};const post=o=>{try{chrome.webview.postMessage(o)}catch{}};function yt(url){try{const u=new URL(url),h=u.hostname.toLowerCase(),list=u.searchParams.get('list');const s='&autoplay=1&rel=0&playsinline=1';if(list)return 'https://www.youtube.com/embed/videoseries?list='+encodeURIComponent(list)+s;let id=h==='youtu.be'?u.pathname.slice(1).split('/')[0]:u.searchParams.get('v')||'';if(!id){const p=u.pathname.split('/').filter(Boolean);if(p.length>1)id=p[1]}return /^[A-Za-z0-9_-]{11}$/.test(id)?'https://www.youtube.com/embed/'+id+'?autoplay=1&rel=0&playsinline=1':''}catch{return ''}}function pick(){const m=state.media||{};if(state.mode==='mine')return(m.myVideo||{}).url||'';return(m.channelGat&&m.channelGat.enabled)?m.channelGat.url||'':''}function render(){['gat','mine'].forEach(x=>document.getElementById(x).classList.toggle('on',x===state.mode));const url=String(pick()||'').trim(),f=document.getElementById('frame'),e=document.getElementById('empty');if(!url){f.classList.add('hidden');e.classList.remove('hidden');e.textContent='Nenhuma fonte ativa na Rádio/TV GAT.';return}const src=yt(url)||url;if(src!==state.embed){state.embed=src;f.src=src}f.classList.remove('hidden');e.classList.add('hidden')}window.pushMedia=p=>{let m=p;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}state.media=m;const mode=String(m.activeMode||'');state.mode=mode==='mine'?'mine':'gat';render()};window.mediaError=()=>{document.getElementById('empty').textContent='Abra o GAT Telemetria e confira a Rádio GAT.'};['gat','mine'].forEach(x=>document.getElementById(x).onclick=()=>{state.mode=x;post({type:'mediaMode',mode:x});render()});document.getElementById('min').onclick=e=>{e.stopPropagation();post({type:'windowMinimize'})};document.getElementById('close').onclick=e=>{e.stopPropagation();post({type:'windowClose'})};const bar=document.getElementById('bar');bar.addEventListener('pointerdown',e=>{if(e.button===0&&!e.target.closest('button'))post({type:'windowDrag'})});bar.addEventListener('dblclick',e=>{if(!e.target.closest('button'))post({type:'windowToggleMax'})});post({type:'mediaRefresh'});setInterval(()=>post({type:'mediaRefresh'}),3000);
</script></body></html>";
    }
}
'@
$videoText = [regex]::Replace($videoText, $htmlPattern, $newHtml, 1)

if ($mainText -notlike '*CurrentVersion = "1.0.61.0"*') { throw 'Versao 1.0.61 nao aplicada.' }
foreach ($m in @('GAT_VIDEO_BORDERLESS_061','FormBorderStyle = FormBorderStyle.None','windowMinimize','windowClose','windowDrag','WM_NCHITTEST_061','Canal GAT','Meu Vídeo')) {
    if ($videoText -notlike "*$m*") { throw "Overlay de video 1.0.61 sem $m" }
}
if ($videoText -like '*Canal Web*') { throw 'Canal Web reapareceu no overlay de video.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.61: overlay de video sem barra branca, com barra GAT propria, minimizar, fechar, mover e redimensionar.'
