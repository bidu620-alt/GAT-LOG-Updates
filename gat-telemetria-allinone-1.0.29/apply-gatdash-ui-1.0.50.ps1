param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$index = Join-Path $rootPath 'index.html'
$media = Join-Path $rootPath 'media.js'
if (-not (Test-Path $index) -or -not (Test-Path $media)) { throw 'Arquivos do GAT DASH nao encontrados.' }

$html = Get-Content $index -Raw
$js = Get-Content $media -Raw

# Remove a terceira aba CANAL WEB do painel de midia.
$html = [regex]::Replace($html, '(?m)^\s*<button id="mediaWeb" class="media-tab">.*?</button>\r?\n', '')

# Migra qualquer preferencia antiga "web" para Canal GAT e deixa apenas dois modos.
$js = $js.Replace(
    "const state={mode:localStorage.getItem(MODE_KEY)||'gat',hidden:",
    "const savedMode=localStorage.getItem(MODE_KEY); const state={mode:savedMode==='mine'?'mine':'gat',hidden:"
)

$oldChoiceWeb = @'
    if(state.mode==='web')return {name:'Canal Web',url:String((m.web&&m.web.url)||''),enabled:true};
'@.TrimEnd()
$js = $js.Replace($oldChoiceWeb, '')

$oldTabs = @'
    $('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine'); $('mediaWeb').classList.toggle('active',state.mode==='web');
'@.TrimEnd()
$newTabs = @'
    $('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine');
'@.TrimEnd()
$js = $js.Replace($oldTabs, $newTabs)

$js = $js.Replace(
    "const embed=state.mode==='web'?url:(youtubeEmbed(url)||url);",
    "const embed=(youtubeEmbed(url)||url);"
)

$oldStatus = @'
    frame.classList.remove('hidden');empty.classList.add('hidden');$('mediaStatus').textContent=state.mode==='web'?'Canal Web • a página pode bloquear incorporação':'Fonte sincronizada pelo GAT Telemetria';
'@.TrimEnd()
$newStatus = @'
    frame.classList.remove('hidden');empty.classList.add('hidden');$('mediaStatus').textContent='Fonte sincronizada pelo GAT Telemetria';
'@.TrimEnd()
$js = $js.Replace($oldStatus, $newStatus)

$oldPush = @'
  window.gatDashPushMedia=payload=>{let m=payload;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}if(!m||typeof m!=='object')return;if(['gat','mine','web'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}state.media=m;state.error='';render()};
'@.TrimEnd()
$newPush = @'
  window.gatDashPushMedia=payload=>{let m=payload;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}if(!m||typeof m!=='object')return;const incoming=String(m.activeMode||'');if(['gat','mine'].includes(incoming)){state.mode=incoming;localStorage.setItem(MODE_KEY,state.mode)}else if(incoming==='web'){state.mode='gat';localStorage.setItem(MODE_KEY,'gat')}state.media=m;state.error='';render()};
'@.TrimEnd()
$js = $js.Replace($oldPush, $newPush)

$oldClick = @'
  $('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine'); $('mediaWeb').onclick=()=>setMode('web');
'@.TrimEnd()
$newClick = @'
  $('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine');
'@.TrimEnd()
$js = $js.Replace($oldClick, $newClick)

# Validacoes: nenhuma interface ou seletor do Canal Web pode sobrar.
if ($html.Contains('id="mediaWeb"') -or $html.Contains('CANAL WEB')) { throw 'index.html ainda contem Canal Web.' }
if ($js.Contains('mediaWeb') -or $js.Contains("['gat','mine','web']") -or $js.Contains("name:'Canal Web'") -or $js.Contains("state.mode==='web'?url")) {
    throw 'media.js ainda contem interface ativa do Canal Web.'
}

Set-Content $index $html -Encoding UTF8
Set-Content $media $js -Encoding UTF8
Write-Host 'GAT DASH 1.0.50: Canal Web removido; somente Canal GAT + Meu Video.'
