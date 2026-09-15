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
$js = $js.Replace("const state={mode:localStorage.getItem(MODE_KEY)||'gat',hidden:", "const savedMode=localStorage.getItem(MODE_KEY); const state={mode:savedMode==='mine'?'mine':'gat',hidden:")
$js = $js.Replace("if(state.mode==='web')return {name:'Canal Web',url:String((m.web&&m.web.url)||''),enabled:true};", '')
$js = $js.Replace("$('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine'); $('mediaWeb').classList.toggle('active',state.mode==='web');", "$('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine');")
$js = $js.Replace("const embed=state.mode==='web'?url:(youtubeEmbed(url)||url);", "const embed=(youtubeEmbed(url)||url);")
$js = $js.Replace("$('mediaStatus').textContent=state.mode==='web'?'Canal Web • a página pode bloquear incorporação':'Fonte sincronizada pelo GAT Telemetria';", "$('mediaStatus').textContent='Fonte sincronizada pelo GAT Telemetria';")
$js = $js.Replace("if(['gat','mine','web'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}", "if(['gat','mine'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}else if(String(m.activeMode||'')==='web'){state.mode='gat';localStorage.setItem(MODE_KEY,'gat')}")
$js = $js.Replace("$('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine'); $('mediaWeb').onclick=()=>setMode('web');", "$('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine');")

if ($html -like '*id="mediaWeb"*' -or $html -like '*CANAL WEB*') { throw 'index.html ainda contem Canal Web.' }
if ($js -like "*$('mediaWeb')*" -or $js -like "*state.mode==='web'*" -or $js -like "*['gat','mine','web']*") { throw 'media.js ainda contem logica ativa do Canal Web.' }

Set-Content $index $html -Encoding UTF8
Set-Content $media $js -Encoding UTF8
Write-Host 'GAT DASH 1.0.50: Canal Web removido; somente Canal GAT + Meu Video.'
