param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$cssPath = Join-Path $rootPath 'app.css'
if (-not (Test-Path $cssPath)) { throw 'app.css do GAT DASH nao encontrado.' }

$css = Get-Content $cssPath -Raw
$marker = '/* GAT_RESPONSIVE_052 */'
if ($css -notlike "*$marker*") {
    $extra = @'

/* GAT_RESPONSIVE_052 */
/* O WebView permanece em zoom 100%. A propria pagina se reorganiza conforme a area disponivel. */
.dashboard-grid{width:100%;min-width:0}.panel,.center-cluster,.media-panel{min-width:0}.route-panel,.condition-panel{min-width:0;overflow:hidden}.center-cluster{min-height:0}.media-stage{min-height:0}

@media(max-height:720px){
  .dash-screen{padding:5px}
  .topbar{height:54px;min-height:54px;padding:0 12px}
  .brand-inline{font-size:22px}.truck-icon{font-size:27px}.brand-inline small{font-size:9px}
  .dashboard-grid{margin-top:5px;gap:5px;grid-template-rows:minmax(0,1fr) 108px}
  .panel-title{height:32px;font-size:12px;padding:0 9px}
  .route-city{padding:4px 9px 2px;gap:8px}.route-city b{font-size:15px}.route-city small{font-size:8px}
  .route-line{height:14px;margin-left:16px}.route-node{width:10px;height:10px;border-width:2px}
  .route-stat{padding:4px 9px;gap:7px}.route-stat>span{font-size:17px;width:22px}.route-stat b{font-size:13px}.route-stat small{font-size:8px}
  .indicator-row{height:27px;font-size:22px}
  .speed-gauge{width:min(35vh,245px)}.speed-value{font-size:clamp(42px,6vw,68px)}.speed-unit{font-size:15px}
  .gear{font-size:20px;padding:0 14px;margin:2px auto}.rpm-line{font-size:10px}.rpm-line b{font-size:14px}
  .cruise-box{margin-top:-18px;min-width:130px;padding:3px 8px}.cruise-box>span{font-size:18px}.cruise-box b{font-size:16px}.cruise-box small{font-size:8px}
  .speed-notice{margin-top:4px;padding:4px 7px;width:min(92%,360px)}.speed-limit-small{width:36px;height:36px;min-width:36px;border-width:4px;font-size:13px}.speed-notice b{font-size:10px}.speed-notice small{font-size:7px}
  .mini-card{min-height:56px;gap:4px}.mini-card>span{font-size:16px}.mini-card b{font-size:14px}.mini-card small{font-size:7px}
  .condition-summary{padding:3px 7px 1px}.condition-summary b{font-size:15px}.condition-summary span{font-size:9px}.wear-grid>div{padding:1px}.wear-grid span{font-size:14px}.wear-grid small{font-size:7px}.wear-grid b{font-size:9px}
  .media-tabs{padding:4px;gap:4px}.media-tab{padding:5px 2px;font-size:8px}.media-footer{min-height:43px;padding:4px 6px}.media-footer b{font-size:10px}.media-footer small{font-size:7px}.media-actions button{padding:5px;font-size:7px}
  .footerbar{height:20px;min-height:20px;margin-top:4px;font-size:7px}
}

@media(max-width:1180px){
  .dashboard-grid{grid-template-columns:27% 43% 30%}
  .topbar{grid-template-columns:1.15fr .8fr 1fr 44px}
  .slogan{letter-spacing:1.5px}
  .mini-cards{gap:5px}.mini-card{gap:5px}
}

@media(max-width:980px){
  .dashboard-grid{grid-template-columns:29% 43% 28%}
  .connection small{display:none}
  .media-footer{flex-direction:column;align-items:stretch}.media-actions{justify-content:flex-end}
}

@media(max-width:860px){
  .dashboard-grid{grid-template-columns:31% 45% 24%;gap:4px}
  .panel-title{padding:0 7px}.route-city,.route-stat{padding-left:7px;padding-right:7px}
  .media-title{font-size:10px}.voice-badge{display:none}
  .media-footer{padding:4px}.media-footer b{font-size:9px}.media-footer small{display:none}.media-actions button{padding:4px;font-size:7px}
}
'@
    $css += $extra
}

if ($css -notlike '*GAT_RESPONSIVE_052*' -or $css -notlike '*@media(max-height:720px)*' -or $css -notlike '*@media(max-width:860px)*') {
    throw 'CSS responsivo 1.0.52 nao foi aplicado corretamente.'
}

Set-Content $cssPath $css -Encoding UTF8
Write-Host 'GAT DASH 1.0.52: layout interno responsivo por largura e altura, sem reduzir a pagina inteira.'
