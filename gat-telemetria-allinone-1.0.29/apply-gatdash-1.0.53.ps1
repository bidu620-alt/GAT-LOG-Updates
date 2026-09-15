param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$indexPath = Join-Path $rootPath 'index.html'
$appPath = Join-Path $rootPath 'app.js'
$cssPath = Join-Path $rootPath 'app.css'
$mediaPath = Join-Path $rootPath 'media.js'
if (-not (Test-Path $indexPath) -or -not (Test-Path $appPath) -or -not (Test-Path $cssPath)) { throw 'Arquivos do GAT DASH nao encontrados.' }

$index = Get-Content $indexPath -Raw
$app = Get-Content $appPath -Raw
$css = Get-Content $cssPath -Raw
$media = if (Test-Path $mediaPath) { Get-Content $mediaPath -Raw } else { '' }

# ---------------------------------------------------------------------------
# Restaura o botao rapido de mutar/desmutar o alerta de velocidade.
# ---------------------------------------------------------------------------
if ($index -notlike '*id="muteSpeedBtn"*') {
    $old = '<button id="settingsBtn" class="icon-btn" title="Configurações">⚙</button>'
    $new = '<div class="top-actions"><button id="muteSpeedBtn" class="icon-btn mute-speed-btn" title="Mutar alerta de velocidade"><span id="muteSpeedIcon">🔊</span><span class="mute-label"> ALERTA</span></button><button id="settingsBtn" class="icon-btn" title="Configurações">⚙</button></div>'
    if (-not $index.Contains($old)) { throw 'Botao de configuracoes do DASH nao encontrado.' }
    $index = $index.Replace($old, $new)
}

# Garante que Canal Web nao volte a aparecer em builds futuros.
$index = [regex]::Replace($index, '(?m)^\s*<button id="mediaWeb" class="media-tab">.*?</button>\r?\n', '')

if ($app -notlike '*function syncVoiceUi()*') {
    $marker = '  function openSettings(){'
    if (-not $app.Contains($marker)) { throw 'Marcador openSettings nao encontrado.' }
    $voice = @'
  function syncVoiceUi(){
    const muted=!state.voice, b=$('muteSpeedBtn'), icon=$('muteSpeedIcon'), badge=$('voiceBadge'), toggle=$('voiceToggle');
    if(b){
      b.classList.toggle('muted',muted);
      b.title=muted?'Ativar alerta de velocidade':'Mutar alerta de velocidade';
      const label=b.querySelector('.mute-label'); if(label)label.textContent=muted?' MUTADO':' ALERTA';
    }
    if(icon)icon.textContent=muted?'🔇':'🔊';
    if(badge){badge.textContent=muted?'🔇 VOZ MUTADA':'🔊 VOZ ATIVA';badge.classList.toggle('muted',muted);}
    if(toggle)toggle.checked=state.voice;
  }
  function toggleSpeedVoice(){
    state.voice=!state.voice;
    if(!state.voice)nativePost({type:'stopSpeech'});
    syncVoiceUi();
    nativePost({type:'setSettings',host:state.host,voice:state.voice,tolerance:state.tolerance});
  }

'@
    $app = $app.Replace($marker, $voice + $marker)
}

$oldHandlers = "  `$('settingsBtn').onclick=openSettings; `$('closeSettings').onclick=closeSettings;"
$newHandlers = "  if(`$('muteSpeedBtn')) `$('muteSpeedBtn').onclick=toggleSpeedVoice; `$('settingsBtn').onclick=openSettings; `$('closeSettings').onclick=closeSettings;"
if ($app.Contains($oldHandlers)) { $app = $app.Replace($oldHandlers, $newHandlers) }
elseif ($app -notlike '*toggleSpeedVoice*') { throw 'Handlers do mute nao encontrados.' }

$oldSave = "  `$('saveSettings').onclick=()=>{state.host=`$('telemetryHost').value.trim(); state.voice=`$('voiceToggle').checked; state.tolerance=Number(`$('speedTolerance').value||3); nativePost({type:'setSettings',host:state.host,voice:state.voice,tolerance:state.tolerance}); closeSettings();};"
$newSave = "  `$('saveSettings').onclick=()=>{state.host=`$('telemetryHost').value.trim(); state.voice=`$('voiceToggle').checked; state.tolerance=Number(`$('speedTolerance').value||3); syncVoiceUi(); nativePost({type:'setSettings',host:state.host,voice:state.voice,tolerance:state.tolerance}); closeSettings();};"
if ($app.Contains($oldSave)) { $app = $app.Replace($oldSave, $newSave) }

$oldSettings = "  window.gatDashSettings = function(s){if(!s)return;state.host=s.host||'';state.voice=s.voice!==false;state.tolerance=Number(s.tolerance??3);};"
$newSettings = "  window.gatDashSettings = function(s){if(!s)return;state.host=s.host||'';state.voice=s.voice!==false;state.tolerance=Number(s.tolerance??3);syncVoiceUi();};"
if ($app.Contains($oldSettings)) { $app = $app.Replace($oldSettings, $newSettings) }
elseif ($app -like '*window.gatDashSettings*' -and $app -notlike '*syncVoiceUi();*') { throw 'gatDashSettings existe mas nao foi atualizado.' }

# ---------------------------------------------------------------------------
# Remove logica residual de Canal Web do JS de midia.
# ---------------------------------------------------------------------------
if ($media) {
    $media = $media.Replace("if(state.mode==='web')return {name:'Canal Web',url:String((m.web&&m.web.url)||''),enabled:true};", '')
    $media = $media.Replace("$('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine'); $('mediaWeb').classList.toggle('active',state.mode==='web');", "$('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine');")
    $media = $media.Replace("const embed=state.mode==='web'?url:(youtubeEmbed(url)||url);", "const embed=(youtubeEmbed(url)||url);")
    $media = $media.Replace("$('mediaStatus').textContent=state.mode==='web'?'Canal Web • a página pode bloquear incorporação':'Fonte sincronizada pelo GAT Telemetria';", "$('mediaStatus').textContent='Fonte sincronizada pelo GAT Telemetria';")
    $media = $media.Replace("if(['gat','mine','web'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}", "if(['gat','mine'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}else if(String(m.activeMode||'')==='web'){state.mode='gat';localStorage.setItem(MODE_KEY,'gat')}")
    $media = $media.Replace("$('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine'); $('mediaWeb').onclick=()=>setMode('web');", "$('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine');")
}

# ---------------------------------------------------------------------------
# Layout responsivo 1.0.53.
# Em larguras normais mantem as 3 colunas visiveis. Em janelas estreitas passa
# para 2 colunas e coloca a Radio/TV abaixo, com rolagem somente dentro do DASH.
# ---------------------------------------------------------------------------
if ($css -notlike '*GAT_RESPONSIVE_053*') {
$css += @'

/* GAT_RESPONSIVE_053 */
.topbar{grid-template-columns:minmax(210px,1.15fr) minmax(150px,.75fr) minmax(190px,1fr) auto}
.top-actions{display:flex;gap:6px;justify-content:flex-end;align-items:center}.top-actions .icon-btn{min-width:40px;padding:0 9px}.mute-speed-btn{min-width:98px!important;font-size:12px!important;font-weight:800;color:#72f4ad!important;border-color:#138958!important}.mute-speed-btn.muted{color:#ff9aa4!important;border-color:#9d3443!important;background:#281018!important}.voice-badge.muted{color:#ff9aa4;border-color:#9d3443}.mute-label{font-size:10px;letter-spacing:.4px}
.dashboard-grid{display:grid!important;width:100%!important;min-width:0!important;grid-template-columns:minmax(220px,27%) minmax(340px,38%) minmax(250px,35%)!important;grid-template-rows:minmax(0,1fr) minmax(105px,24%)!important;grid-template-areas:"route center media" "condition center media"!important;overflow:hidden!important}
.route-panel{grid-area:route!important;grid-column:auto!important;grid-row:auto!important;min-width:0!important}.center-cluster{grid-area:center!important;grid-column:auto!important;grid-row:auto!important;min-width:0!important}.media-panel{grid-area:media!important;grid-column:auto!important;grid-row:auto!important;min-width:0!important;display:flex!important}.condition-panel{grid-area:condition!important;grid-column:auto!important;grid-row:auto!important;min-width:0!important}
.center-cluster,.panel{max-width:100%}.media-stage{min-width:0}.media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important}

@media(max-width:1180px){
  .dashboard-grid{grid-template-columns:minmax(205px,28%) minmax(330px,42%) minmax(220px,30%)!important}
  .topbar{grid-template-columns:minmax(190px,1fr) minmax(130px,.65fr) minmax(175px,.9fr) auto}
}

@media(max-width:980px){
  .topbar{grid-template-columns:minmax(190px,1fr) minmax(170px,.9fr) auto}.slogan{display:none}.mute-label{display:none}.mute-speed-btn{min-width:40px!important;padding:0 7px!important}
  .dashboard-grid{grid-template-columns:minmax(220px,34%) minmax(360px,66%)!important;grid-template-rows:minmax(310px,1fr) minmax(105px,120px) minmax(245px,55vh)!important;grid-template-areas:"route center" "condition center" "media media"!important;overflow:auto!important;padding-right:2px}
  .media-panel{min-height:245px!important}.media-footer{flex-direction:row!important;align-items:center!important}.media-actions{justify-content:flex-end}
}

@media(max-width:860px){
  .dashboard-grid{grid-template-columns:minmax(205px,35%) minmax(330px,65%)!important}
  .connection small{display:none}.media-title{font-size:11px}.voice-badge{display:inline-flex!important}
}

@media(max-height:650px) and (min-width:981px){
  .dashboard-grid{grid-template-rows:minmax(0,1fr) 96px!important}.speed-gauge{width:min(31vh,220px)!important}.mini-card{min-height:48px!important}.route-stat{padding-top:3px!important;padding-bottom:3px!important}.media-footer{min-height:38px!important}
}
'@
}

# Cache bust para o WebView pegar a versao nova imediatamente.
$index = $index.Replace('href="app.css"', 'href="app.css?v=1053"')
$index = $index.Replace('href="app.css?v=1046"', 'href="app.css?v=1053"')
$index = $index.Replace('src="app.js"', 'src="app.js?v=1053"')
$index = $index.Replace('src="app.js?v=1046"', 'src="app.js?v=1053"')
$index = $index.Replace('src="media.js"', 'src="media.js?v=1053"')

if ($index -notlike '*muteSpeedBtn*') { throw 'Botao de mute nao foi restaurado.' }
if ($index -like '*mediaWeb*') { throw 'Canal Web ainda aparece no index.' }
foreach ($m in @('syncVoiceUi','toggleSpeedVoice','stopSpeech')) { if ($app -notlike "*$m*") { throw "app.js sem $m" } }
foreach ($m in @('GAT_RESPONSIVE_053','grid-template-areas:"route center media" "condition center media"','grid-template-columns:repeat(2,minmax(0,1fr))')) { if ($css -notlike "*$m*") { throw "CSS 1.0.53 sem $m" } }

Set-Content $indexPath $index -Encoding UTF8
Set-Content $appPath $app -Encoding UTF8
Set-Content $cssPath $css -Encoding UTF8
if ($media) { Set-Content $mediaPath $media -Encoding UTF8 }
Write-Host 'GAT DASH 1.0.53: responsividade corrigida, mute restaurado e Canal Web removido.'
