param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$cssPath = Join-Path $rootPath 'app.css'
$indexPath = Join-Path $rootPath 'index.html'
if (-not (Test-Path $cssPath) -or -not (Test-Path $indexPath)) { throw 'Arquivos do GAT DASH nao encontrados.' }

$css = Get-Content $cssPath -Raw
$index = Get-Content $indexPath -Raw

# GAT_RESPONSIVE_054
# Mantem ROTA + PAINEL CENTRAL + RADIO/TV visiveis mesmo quando o WebView fica
# menor por causa de DPI/escala do Windows. A 1.0.53 mudava para duas colunas em
# <=980 CSS px, deixando a Radio/TV abaixo da dobra e parecendo que sumiu.
if ($css -notlike '*GAT_RESPONSIVE_054*') {
$css += @'

/* GAT_RESPONSIVE_054 */
.dashboard-grid{
  display:grid!important;
  width:100%!important;
  min-width:0!important;
  grid-template-columns:minmax(0,30%) minmax(0,45%) minmax(0,25%)!important;
  grid-template-rows:minmax(0,1fr) minmax(92px,21%)!important;
  grid-template-areas:"route center media" "condition center media"!important;
  overflow:hidden!important;
  gap:6px!important;
}
.route-panel{grid-area:route!important;min-width:0!important;overflow:hidden!important}
.center-cluster{grid-area:center!important;min-width:0!important;overflow:hidden!important}
.media-panel{grid-area:media!important;min-width:0!important;display:flex!important;visibility:visible!important;overflow:hidden!important}
.condition-panel{grid-area:condition!important;min-width:0!important;overflow:hidden!important}
.media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important}

@media(max-width:980px){
  .dashboard-grid{
    grid-template-columns:minmax(0,31%) minmax(0,44%) minmax(0,25%)!important;
    grid-template-rows:minmax(0,1fr) 92px!important;
    grid-template-areas:"route center media" "condition center media"!important;
    overflow:hidden!important;
    gap:5px!important;
  }
  .media-panel{display:flex!important;visibility:visible!important;min-height:0!important}
  .media-footer{flex-direction:column!important;align-items:stretch!important;gap:3px!important;padding:4px!important}
  .media-footer small{display:none!important}
  .media-actions{justify-content:stretch!important}
  .media-actions button{flex:1!important;padding:4px 2px!important;font-size:7px!important}
  .voice-badge{font-size:8px!important;padding:3px 4px!important}
  .media-title{font-size:10px!important}
  .media-tab{padding:5px 1px!important;font-size:7px!important}
  .route-city{padding-left:6px!important;padding-right:6px!important}
  .route-stat{padding-left:6px!important;padding-right:6px!important}
  .panel-title{padding-left:6px!important;padding-right:6px!important}
  .mini-cards{gap:4px!important}
  .mini-card{gap:3px!important}
}

@media(max-width:760px){
  .dashboard-grid{grid-template-columns:minmax(0,32%) minmax(0,43%) minmax(0,25%)!important;gap:4px!important}
  .panel-title{font-size:10px!important;height:30px!important}
  .route-city b{font-size:13px!important}.route-city small{font-size:7px!important}
  .route-stat b{font-size:11px!important}.route-stat small{font-size:7px!important}.route-stat>span{font-size:14px!important;width:18px!important}
  .condition-summary b{font-size:13px!important}.condition-summary span{font-size:8px!important}
  .wear-grid{grid-template-columns:repeat(5,minmax(0,1fr))!important;padding:0 3px!important;gap:2px!important}
  .wear-grid span{font-size:11px!important}.wear-grid small{font-size:6px!important}.wear-grid b{font-size:7px!important}
  .speed-gauge{width:min(30vh,205px)!important}.speed-value{font-size:clamp(38px,7vw,58px)!important}.speed-unit{font-size:12px!important}
  .gear{font-size:17px!important;padding:0 10px!important}.rpm-line{font-size:8px!important}.rpm-line b{font-size:11px!important}
  .cruise-box{min-width:105px!important;padding:2px 5px!important}.cruise-box b{font-size:13px!important}.cruise-box>span{font-size:15px!important}
  .speed-notice{padding:3px 5px!important;gap:5px!important}.speed-limit-small{width:30px!important;height:30px!important;min-width:30px!important;border-width:3px!important;font-size:10px!important}
  .speed-notice b{font-size:8px!important}.speed-notice small{font-size:6px!important}
  .mini-card{min-height:46px!important}.mini-card>span{font-size:13px!important}.mini-card b{font-size:10px!important}.mini-card small{font-size:6px!important}
}

@media(max-width:560px){
  .dashboard-grid{
    grid-template-columns:minmax(0,36%) minmax(0,64%)!important;
    grid-template-rows:minmax(280px,1fr) 90px minmax(190px,42vh)!important;
    grid-template-areas:"route center" "condition center" "media media"!important;
    overflow:auto!important;
  }
  .media-panel{min-height:190px!important}
}

/* GAT_DESKTOP_EMBED_055
   Dentro do GAT Telemetria o viewport e uma janela do PC, nao um celular.
   Portanto a mensagem de girar o telefone nunca deve bloquear o dashboard. */
.rotate-notice{display:none!important}
@media(orientation:portrait) and (max-width:800px){
  .rotate-notice{display:none!important}
  .screen,.modal{visibility:visible!important}
}
'@
}

# Cache bust para obrigar o WebView a pegar o CSS novo.
$index = [regex]::Replace($index, 'href="app\.css(?:\?v=[^"]*)?"', 'href="app.css?v=1055"')
$index = [regex]::Replace($index, 'src="app\.js(?:\?v=[^"]*)?"', 'src="app.js?v=1055"')
$index = [regex]::Replace($index, 'src="media\.js(?:\?v=[^"]*)?"', 'src="media.js?v=1055"')

foreach($m in @('GAT_RESPONSIVE_054','GAT_DESKTOP_EMBED_055','grid-template-columns:minmax(0,31%) minmax(0,44%) minmax(0,25%)','@media(max-width:560px)')) {
    if ($css -notlike "*$m*") { throw "CSS responsivo desktop sem $m" }
}

Set-Content $cssPath $css -Encoding UTF8
Set-Content $indexPath $index -Encoding UTF8
Write-Host 'GAT DASH desktop: tres paineis responsivos e bloqueio de rotacao de celular desativado dentro do GAT Telemetria.'
