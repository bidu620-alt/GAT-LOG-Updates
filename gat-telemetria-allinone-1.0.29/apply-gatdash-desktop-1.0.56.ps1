param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$cssPath = Join-Path $rootPath 'app.css'
$indexPath = Join-Path $rootPath 'index.html'
if (-not (Test-Path $cssPath) -or -not (Test-Path $indexPath)) { throw 'Assets do GAT DASH nao encontrados.' }

$css = Get-Content $cssPath -Raw
$index = Get-Content $indexPath -Raw

if ($css -notlike '*GAT_DESKTOP_RESPONSIVE_056*') {
$css += @'

/* GAT_DESKTOP_RESPONSIVE_056
   Regras exclusivas do DASH empacotado no GAT Telemetria Windows.
   O Android continua usando os assets originais do repositorio. */
.rotate-notice{display:none!important}
@media(orientation:portrait) and (max-width:800px){
  .rotate-notice{display:none!important}
  .screen,.modal{visibility:visible!important}
}
html,body{min-width:0!important;min-height:0!important;overflow:hidden!important}
.dash-screen{min-width:0!important;min-height:0!important}
.dashboard-grid{min-width:0!important;min-height:0!important;width:100%!important;height:auto!important}

/* Desktop largo */
@media(min-width:1001px){
  .dashboard-grid{
    grid-template-columns:minmax(220px,29%) minmax(360px,46%) minmax(220px,25%)!important;
    grid-template-rows:minmax(0,1fr) minmax(90px,20%)!important;
    grid-template-areas:"route center media" "condition center media"!important;
  }
}

/* Janela media / monitor 1366x768 / DPI alto. Continua com tres areas, mas
   comprime tipografia e controles sem transformar tudo numa miniatura. */
@media(max-width:1000px) and (min-width:761px){
  .topbar{height:50px!important;min-height:50px!important;padding:0 8px!important}
  .brand-inline{font-size:18px!important;gap:6px!important}.truck-icon{font-size:24px!important}
  .connection{font-size:10px!important}.icon-btn{height:34px!important;font-size:18px!important}
  .dashboard-grid{
    grid-template-columns:minmax(190px,30%) minmax(310px,45%) minmax(175px,25%)!important;
    grid-template-rows:minmax(0,1fr) 88px!important;
    grid-template-areas:"route center media" "condition center media"!important;
    gap:5px!important;
  }
  .panel-title{height:30px!important;font-size:11px!important;padding:0 6px!important}
  .route-city{padding:5px 7px 2px!important;gap:7px!important}.route-city b{font-size:14px!important}
  .route-stat{padding:4px 7px!important;gap:7px!important}.route-stat>span{font-size:15px!important;width:20px!important}.route-stat b{font-size:12px!important}.route-stat small{font-size:7px!important}
  .center-cluster{padding:3px!important}.indicator-row{height:25px!important;font-size:20px!important}
  .speed-gauge{width:min(31vh,220px)!important}.speed-value{font-size:clamp(40px,6vw,62px)!important}.speed-unit{font-size:13px!important}
  .gear{font-size:18px!important;padding:0 11px!important;margin:2px auto!important}.rpm-line{font-size:8px!important}.rpm-line b{font-size:12px!important}
  .cruise-box{margin-top:-14px!important;min-width:110px!important;padding:2px 5px!important}.cruise-box>span{font-size:15px!important}.cruise-box b{font-size:14px!important}
  .speed-notice{margin-top:4px!important;padding:3px 5px!important;gap:6px!important}.speed-limit-small{width:31px!important;height:31px!important;min-width:31px!important;border-width:3px!important;font-size:10px!important}
  .speed-notice b{font-size:9px!important}.speed-notice small{font-size:7px!important}
  .mini-cards{gap:4px!important}.mini-card{min-height:44px!important;gap:3px!important}.mini-card>span{font-size:13px!important}.mini-card b{font-size:11px!important}.mini-card small{font-size:6px!important}
  .media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important;padding:4px!important;gap:3px!important}.media-tab{padding:5px 1px!important;font-size:7px!important}
  .media-footer{min-height:42px!important;padding:4px!important}.media-footer b{font-size:9px!important}.media-footer small{font-size:7px!important}.media-actions button{padding:4px!important;font-size:7px!important}
  .voice-badge{font-size:7px!important;padding:2px 3px!important}
  .condition-summary{padding:3px 5px 1px!important}.condition-summary b{font-size:14px!important}.condition-summary span{font-size:8px!important}
  .wear-grid{padding:0 3px!important;gap:2px!important}.wear-grid>div{padding:2px 1px!important}.wear-grid span{font-size:11px!important}.wear-grid small{font-size:6px!important}.wear-grid b{font-size:7px!important}
  .footerbar{height:20px!important;min-height:20px!important;font-size:6px!important;margin-top:3px!important}
}

/* Janela estreita: em vez de deformar as tres colunas, reorganiza o DASH.
   Rota + velocimetro ficam em cima e Radio/TV ocupa toda a largura abaixo. */
@media(max-width:760px){
  html,body{overflow:auto!important}
  .dash-screen{height:auto!important;min-height:100%!important;overflow:auto!important}
  .topbar{height:46px!important;min-height:46px!important;padding:0 6px!important}.brand-inline{font-size:16px!important}.connection{font-size:9px!important}.slogan{display:none!important}
  .dashboard-grid{
    flex:none!important;
    min-height:620px!important;
    grid-template-columns:minmax(180px,35%) minmax(300px,65%)!important;
    grid-template-rows:minmax(330px,1fr) 85px minmax(220px,38vh)!important;
    grid-template-areas:"route center" "condition center" "media media"!important;
    gap:5px!important;
    overflow:visible!important;
  }
  .route-panel{grid-area:route!important}.center-cluster{grid-area:center!important}.condition-panel{grid-area:condition!important}.media-panel{grid-area:media!important;min-height:220px!important}
  .media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important}
  .footerbar{position:relative!important}
}

/* Janela baixa: reduz somente espacamentos verticais; nunca aplica zoom global. */
@media(max-height:610px) and (min-width:761px){
  .topbar{height:44px!important;min-height:44px!important}.dashboard-grid{margin-top:4px!important;grid-template-rows:minmax(0,1fr) 76px!important}
  .panel-title{height:28px!important}.route-city{padding-top:3px!important;padding-bottom:1px!important}.route-line{height:12px!important}.route-stat{padding-top:3px!important;padding-bottom:3px!important}
  .indicator-row{height:22px!important}.speed-gauge{width:min(27vh,190px)!important}.cruise-box{margin-top:-12px!important}.mini-card{min-height:38px!important}
  .media-footer{min-height:36px!important}.footerbar{height:18px!important;min-height:18px!important}
}
'@
}

# Remove visualmente qualquer Canal Web residual do pacote Windows mesmo se um
# cache antigo de HTML sobreviver; a logica ja foi removida na 1.0.50.
$css += "`r`n#mediaWeb{display:none!important}`r`n"
$index = [regex]::Replace($index, 'href="app\.css(?:\?v=[^"]*)?"', 'href="app.css?v=1056"')
$index = [regex]::Replace($index, 'src="app\.js(?:\?v=[^"]*)?"', 'src="app.js?v=1056"')
$index = [regex]::Replace($index, 'src="media\.js(?:\?v=[^"]*)?"', 'src="media.js?v=1056"')

foreach ($m in @('GAT_DESKTOP_RESPONSIVE_056','.rotate-notice{display:none!important}','@media(max-width:760px)','grid-template-areas:"route center" "condition center" "media media"')) {
    if ($css -notlike "*$m*") { throw "GAT DASH Windows 1.0.56 sem $m" }
}

Set-Content $cssPath $css -Encoding UTF8
Set-Content $indexPath $index -Encoding UTF8
Write-Host 'GAT DASH Windows 1.0.56: responsividade por largura/altura sem zoom global e sem aviso de girar celular.'
