param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar Radio Stream 1.0.37.' }
if (-not $radio) { throw 'RadioForm.cs 1.0.36 nao encontrado para aplicar Radio Stream 1.0.37.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.36.0"', 'CurrentVersion = "1.0.37.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.36"', 'Text = "Cliente 1.0.37"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.37\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.37.0.' }

$radioText = $radioText.Replace('Escolha o Canal GAT da Central ou use sua própria playlist somente neste PC.', 'Escolha o Canal GAT da Central ou use YouTube / Rádio Online somente neste PC.')
$radioText = $radioText.Replace('Sua playlist/vídeo do YouTube (fica salvo somente neste PC):', 'Sua fonte: YouTube ou URL direta de Rádio Online MP3/AAC (somente neste PC):')
$radioText = $radioText.Replace('ABRIR NO YOUTUBE', 'ABRIR FONTE')
$radioText = $radioText.Replace('Minha Rádio: link do YouTube inválido. Use vídeo ou playlist.', 'Minha Rádio: fonte inválida. Use YouTube ou URL direta de rádio online (MP3/AAC).')
$radioText = $radioText.Replace('Minha Rádio: cole uma playlist ou vídeo do YouTube acima', 'Minha Rádio: cole YouTube ou URL direta de rádio online acima')
$radioText = $radioText.Replace('Não foi possível abrir o YouTube.', 'Não foi possível abrir a fonte.')
$radioText = $radioText.Replace('Radio-1.0.36', 'Radio-1.0.37')
$radioText = $radioText.Replace('/index.html?v=136', '/index.html?v=137')
$radioText = $radioText.Replace('TryParseYoutubeSource', 'TryParseMediaSource')

# WebView2: permite streams HTTP antigos de Icecast/Shoutcast. A rádio continua
# iniciando somente quando o motorista clica em OUVIR RÁDIO.
$oldEnvironment = '            var environment = await CoreWebView2Environment.CreateAsync(null, webViewData);'
$newEnvironment = @'
            var options = new CoreWebView2EnvironmentOptions();
            options.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required --allow-running-insecure-content";
            var environment = await CoreWebView2Environment.CreateAsync(null, webViewData, options);
'@
if ($radioText -notlike "*$oldEnvironment*") { throw 'Inicializacao WebView2 da 1.0.36 nao encontrada.' }
$radioText = $radioText.Replace($oldEnvironment, $newEnvironment.TrimEnd())

# Descricao amigavel da fonte ativa.
$uiMarker = '    private void UpdateActiveSourceUi()'
$uiHelper = @'
    private static string SourceDescription(string type)
    {
        if (string.Equals(type, "stream", StringComparison.OrdinalIgnoreCase)) return "rádio online MP3/AAC";
        if (string.Equals(type, "playlist", StringComparison.OrdinalIgnoreCase)) return "playlist do YouTube";
        if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)) return "vídeo do YouTube";
        return "fonte desconhecida";
    }

    private void UpdateActiveSourceUi()
'@
if ($radioText -notlike "*$uiMarker*") { throw 'UpdateActiveSourceUi nao encontrado.' }
$radioText = $radioText.Replace($uiMarker, $uiHelper.TrimEnd())
$radioText = $radioText.Replace('_source.Text = available ? "Fonte local: " + (_personalSourceType == "playlist" ? "playlist do YouTube" : "vídeo do YouTube") + " • somente neste PC" : "Fonte local: nenhuma configurada";', '_source.Text = available ? "Fonte local: " + SourceDescription(_personalSourceType) + " • somente neste PC" : "Fonte local: nenhuma configurada";')
$radioText = $radioText.Replace('_source.Text = "Fonte oficial: " + (_serverSourceType == "video" ? "vídeo do YouTube" : "playlist do YouTube") + " • revisão " + _serverRevision;', '_source.Text = "Fonte oficial: " + SourceDescription(_serverSourceType) + " • revisão " + _serverRevision;')

# Canal GAT: stream usa a propria URL como identificador local.
$videoSourceLine = '            if (sourceType == "video") sourceId = Convert.ToString(radio["video_id"]) ?? string.Empty;'
$streamSourceLine = '            else if (sourceType == "stream") sourceId = sourceUrl;'
if (-not $radioText.Contains($videoSourceLine)) { throw 'Leitura da fonte do Canal GAT 1.0.36 nao encontrada.' }
$radioText = $radioText.Replace($videoSourceLine, $videoSourceLine + "`r`n" + $streamSourceLine)

# Carrega video, playlist ou stream direto. Troca o metodo inteiro por indices,
# evitando dependencia de CRLF/LF do fonte reconstruido.
$loadMethodStart = $radioText.IndexOf('    private async Task LoadActiveSourceAsync()')
$loadMethodEnd = $radioText.IndexOf('    private async Task ExecutePlayerAsync(string script)', $loadMethodStart)
if ($loadMethodStart -lt 0 -or $loadMethodEnd -lt 0) { throw 'LoadActiveSourceAsync 1.0.36 nao encontrado.' }
$newLoadMethod = @'
    private async Task LoadActiveSourceAsync()
    {
        string id = ActiveSourceId();
        if (!_playerReady || string.IsNullOrWhiteSpace(id)) return;
        string type = ActiveSourceType();
        string command;
        if (type == "stream")
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

'@
$radioText = $radioText.Substring(0, $loadMethodStart) + $newLoadMethod + $radioText.Substring($loadMethodEnd)

# Mensagens de erro do HTML5 Audio.
$oldYoutubeErrorTail = '        if (code == 5) return "O player HTML5 do YouTube não conseguiu reproduzir este vídeo.";'
$newYoutubeErrorTail = @'
        if (code == 5) return "O player HTML5 do YouTube não conseguiu reproduzir este vídeo.";
        if (code == 900) return "Não foi possível tocar esta rádio online. Confirme se o link é o stream direto MP3/AAC; HTTPS é recomendado.";
        if (code == 901) return "A rádio online foi interrompida. O servidor da estação pode estar offline ou ter recusado a conexão.";
'@
if (-not $radioText.Contains($oldYoutubeErrorTail)) { throw 'Tabela de erros do player nao encontrada.' }
$radioText = $radioText.Replace($oldYoutubeErrorTail, $newYoutubeErrorTail.TrimEnd())

# Parser: continua priorizando list= em links watch e passa a aceitar URL direta
# http/https de radio online. O GAT Server nao baixa nem retransmite o audio.
$parserStart = $radioText.IndexOf('    private static bool TryParseMediaSource(')
$parserEnd = $radioText.IndexOf('    private static Dictionary<string, string> ParseQuery(', $parserStart)
if ($parserStart -lt 0 -or $parserEnd -lt 0) { throw 'Parser de YouTube 1.0.36 nao encontrado.' }
$newParser = @'
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

'@
$radioText = $radioText.Substring(0, $parserStart) + $newParser + $radioText.Substring($parserEnd)

# Player unico: YouTube IFrame + HTML5 Audio para streams MP3/AAC.
$htmlStart = $radioText.IndexOf('    private static string PlayerHtml()')
$classEnd = $radioText.LastIndexOf("`n}")
if ($htmlStart -lt 0 -or $classEnd -le $htmlStart) { throw 'PlayerHtml 1.0.36 nao encontrado.' }
$newHtml = @'
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
function onYouTubeIframeAPIReady(){player=new YT.Player('player',{width:'100%',height:'100%',playerVars:{controls:1,disablekb:0,fs:1,playsinline:1,rel:0,origin:'https://radio.gatlogets2.local'},events:{onReady:function(){ytReady=true;loadPending()},onStateChange:function(e){if(e.data===YT.PlayerState.PLAYING){let d=player.getVideoData()||{};post({type:'track',title:d.title||''})}},onError:function(e){post({type:'error',code:e.data});if(currentType==='playlist'&&(e.data===100||e.data===101||e.data===150)){setTimeout(function(){try{player.nextVideo()}catch(x){}},700)}}}})}
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
'@
$radioText = $radioText.Substring(0, $htmlStart) + $newHtml.TrimEnd() + "`r`n}"

foreach ($marker in @(
    'CurrentVersion = "1.0.37.0"',
    'ABRIR FONTE',
    'TryParseMediaSource',
    'sourceType = "stream"',
    'gatLoadStream',
    'streamAudio',
    'Rádio online MP3/AAC',
    'Radio-1.0.37',
    '/index.html?v=137',
    '--allow-running-insecure-content'
)) {
    $haystack = if ($marker -like 'CurrentVersion*') { $mainText } else { $radioText }
    if ($haystack -notlike "*$marker*") { throw "Patch Radio Stream 1.0.37 incompleto: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.37: Canal GAT + Minha Radio com YouTube e stream direto MP3/AAC, mantendo overlay.'
