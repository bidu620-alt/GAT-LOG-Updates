param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar YouTube Referrer 1.0.39.' }
if (-not $radio) { throw 'RadioForm.cs 1.0.37 nao encontrado para aplicar YouTube Referrer 1.0.39.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.37.0"', 'CurrentVersion = "1.0.39.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.37"', 'Text = "Cliente 1.0.39"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.39\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.39.0.' }

$radioText = $radioText.Replace('Radio-1.0.37', 'Radio-1.0.39')
$radioText = $radioText.Replace('/index.html?v=137', '/index.html?v=139')

# YouTube exige identificacao do cliente por HTTP Referer em apps desktop/WebView.
# O parametro origin sozinho nao e suficiente em todos os ambientes e pode gerar erro 153.
$ensure = '            await _web.EnsureCoreWebView2Async(environment);'
$refererHook = @'
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
'@
if (-not $radioText.Contains($ensure)) { throw 'EnsureCoreWebView2Async da 1.0.37 nao encontrado.' }
$radioText = $radioText.Replace($ensure, $refererHook.TrimEnd())

# Reforca a identidade do host tambem nos parametros do IFrame Player.
$originMarker = "origin:'https://radio.gatlogets2.local'"
$originWithWidget = "origin:'https://radio.gatlogets2.local',widget_referrer:'https://radio.gatlogets2.local/'"
if (-not $radioText.Contains($originMarker)) { throw 'origin do YouTube IFrame nao encontrado.' }
$radioText = $radioText.Replace($originMarker, $originWithWidget)

# Mensagem de diagnostico especifica caso o YouTube ainda devolva erro 153.
$old153 = '        if (code == 153) return "O YouTube recusou o player incorporado neste vídeo. Use ABRIR NO YOUTUBE.";'
$new153 = '        if (code == 153) return "O YouTube recusou a identificação do player incorporado (erro 153). O GAT 1.0.39 envia Referer automaticamente; tente recarregar a Rádio GAT.";'
if ($radioText.Contains($old153)) { $radioText = $radioText.Replace($old153, $new153) }

foreach ($marker in @(
    'CurrentVersion = "1.0.39.0"',
    'CoreWebView2WebResourceContext.All',
    'WebResourceRequested +=',
    'SetHeader("Referer", "https://radio.gatlogets2.local/")',
    "widget_referrer:'https://radio.gatlogets2.local/'",
    'Radio-1.0.39',
    '/index.html?v=139',
    'gatLoadVideo',
    'gatLoadPlaylist',
    'gatLoadStream',
    'CANAL GAT',
    'MINHA RÁDIO'
)) {
    $haystack = if ($marker -like 'CurrentVersion*') { $mainText } else { $radioText }
    if ($haystack -notlike "*$marker*") { throw "Patch YouTube Referrer 1.0.39 incompleto: $marker" }
}

# A 1.0.39 parte da 1.0.37 e nao inclui o modo SITE/WEB experimental da 1.0.38.
if ($radioText -like '*SITE / WEB*' -or $radioText -like '*radio-web-url.txt*') { throw 'Modo SITE/WEB nao deve entrar na 1.0.39.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.39: hotfix YouTube/WebView2 Referer; Canal GAT + Minha Radio, sem SITE/WEB.'