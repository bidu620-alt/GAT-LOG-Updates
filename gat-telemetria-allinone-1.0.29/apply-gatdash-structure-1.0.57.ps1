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

if ($css -notlike '*GAT_DASH_STRUCTURE_057*') {
$css += @'

/* GAT_DASH_STRUCTURE_057
   Correcao estrutural EXCLUSIVA do GAT DASH embutido no Windows.
   Nao altera o layout geral do GAT Telemetria.

   3 modulos independentes:
   1) Rota + Condicao
   2) Velocimetro / painel central
   3) Radio / TV

   Largo  : ROTA | CENTRO | RADIO
   Medio  : ROTA | CENTRO
            COND | CENTRO
            RADIO ocupando toda a largura
   Estreito: tudo empilhado com rolagem vertical.
*/

html,body{min-width:0!important;overflow:hidden!important}
.dash-screen{min-width:0!important;min-height:0!important;overflow:hidden!important}
.dashboard-grid{
  flex:1!important;
  min-width:0!important;
  min-height:0!important;
  width:100%!important;
  height:auto!important;
  display:grid!important;
  gap:6px!important;
  overflow:hidden!important;
}
.route-panel,.center-cluster,.condition-panel,.media-panel{min-width:0!important;min-height:0!important}
.route-panel{grid-area:route!important}
.center-cluster{grid-area:center!important}
.condition-panel{grid-area:condition!important}
.media-panel{grid-area:media!important;display:flex!important;visibility:visible!important}

/* 1) JANELA LARGA: tres modulos lado a lado. */
@media(min-width:1320px){
  .dashboard-grid{
    grid-template-columns:minmax(250px,26%) minmax(430px,45%) minmax(280px,29%)!important;
    grid-template-rows:minmax(0,1fr) minmax(105px,20%)!important;
    grid-template-areas:
      "route center media"
      "condition center media"!important;
  }
}

/* 2) JANELA MEDIA: essa e a faixa usada no teste em janela nao maximizada.
      O terceiro modulo NAO fica espremido nem sai para fora da direita.
      A Radio/TV desce para uma terceira linha ocupando 100% da largura. */
@media(max-width:1319px) and (min-width:761px){
  .dashboard-grid{
    grid-template-columns:minmax(235px,42%) minmax(360px,58%)!important;
    grid-template-rows:minmax(250px,52%) minmax(78px,14%) minmax(155px,34%)!important;
    grid-template-areas:
      "route center"
      "condition center"
      "media media"!important;
    gap:5px!important;
  }

  .route-panel{overflow:hidden!important}
  .condition-panel{overflow:hidden!important}
  .center-cluster{overflow:hidden!important;padding:3px!important}
  .media-panel{overflow:hidden!important;min-height:0!important}

  .panel-title{height:30px!important;font-size:11px!important;padding:0 7px!important}
  .route-city{padding:4px 8px 2px!important;gap:7px!important}
  .route-city b{font-size:14px!important}.route-city small{font-size:8px!important}
  .route-line{height:13px!important;margin-left:15px!important}
  .route-stat{padding:4px 8px!important;gap:7px!important}
  .route-stat>span{font-size:15px!important;width:20px!important}
  .route-stat b{font-size:12px!important}.route-stat small{font-size:7px!important}

  .indicator-row{height:23px!important;font-size:20px!important}
  .speed-gauge{width:min(29vh,205px)!important;max-width:38%!important}
  .speed-value{font-size:clamp(38px,5.5vw,60px)!important}
  .speed-unit{font-size:13px!important}
  .gear{font-size:17px!important;padding:0 10px!important;margin:2px auto!important}
  .rpm-line{font-size:8px!important}.rpm-line b{font-size:11px!important}
  .cruise-box{margin-top:-13px!important;min-width:108px!important;padding:2px 5px!important}
  .cruise-box>span{font-size:15px!important}.cruise-box b{font-size:13px!important}
  .speed-notice{width:min(74%,300px)!important;margin-top:4px!important;padding:3px 5px!important;gap:5px!important}
  .speed-limit-small{width:31px!important;height:31px!important;min-width:31px!important;border-width:3px!important;font-size:10px!important}
  .speed-notice b{font-size:9px!important}.speed-notice small{font-size:7px!important}
  .mini-cards{gap:4px!important;margin-top:auto!important}
  .mini-card{min-height:38px!important;gap:3px!important}
  .mini-card>span{font-size:13px!important}.mini-card b{font-size:11px!important}.mini-card small{font-size:6px!important}

  .condition-summary{padding:3px 6px 1px!important}
  .condition-summary b{font-size:14px!important}.condition-summary span{font-size:8px!important}
  .wear-grid{grid-template-columns:repeat(5,minmax(0,1fr))!important;padding:0 3px!important;gap:2px!important}
  .wear-grid>div{padding:1px!important}.wear-grid span{font-size:10px!important}.wear-grid small{font-size:6px!important}.wear-grid b{font-size:7px!important}

  .media-title{height:28px!important}
  .media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important;padding:3px 5px!important;gap:4px!important}
  .media-tab{padding:4px 3px!important;font-size:8px!important}
  .media-stage{min-height:80px!important}
  .media-footer{min-height:34px!important;padding:3px 6px!important}
  .media-footer b{font-size:9px!important}.media-footer small{font-size:7px!important}
  .media-actions button{padding:4px 6px!important;font-size:7px!important}
  .voice-badge{font-size:7px!important;padding:2px 4px!important}
}

/* Janela media mas muito baixa: preserva os 3 modulos reduzindo a linha inferior. */
@media(max-width:1319px) and (min-width:761px) and (max-height:590px){
  .dashboard-grid{
    grid-template-rows:minmax(215px,55%) minmax(65px,13%) minmax(115px,32%)!important;
  }
  .media-stage{min-height:55px!important}
  .footerbar{height:18px!important;min-height:18px!important;margin-top:2px!important}
}

/* 3) JANELA ESTREITA: nao existe corte horizontal. Tudo fica em uma coluna
      e o usuario rola apenas na vertical. */
@media(max-width:760px){
  html,body{overflow:auto!important}
  .dash-screen{height:auto!important;min-height:100%!important;overflow:auto!important}
  .dashboard-grid{
    flex:none!important;
    display:grid!important;
    grid-template-columns:minmax(0,1fr)!important;
    grid-template-rows:auto auto auto auto!important;
    grid-template-areas:
      "route"
      "center"
      "condition"
      "media"!important;
    min-height:0!important;
    overflow:visible!important;
    gap:5px!important;
  }
  .route-panel{min-height:240px!important}
  .center-cluster{min-height:390px!important}
  .condition-panel{min-height:100px!important}
  .media-panel{min-height:230px!important}
  .footerbar{position:relative!important}
}

/* Nenhuma largura fixa antiga pode empurrar o terceiro modulo para fora. */
.dashboard-grid>*{max-width:100%!important}
.media-panel,.center-cluster,.route-panel,.condition-panel{width:auto!important}
#mediaWeb{display:none!important}
.rotate-notice{display:none!important}
@media(orientation:portrait) and (max-width:800px){.rotate-notice{display:none!important}.screen,.modal{visibility:visible!important}}
'@
}

# Cache bust apenas dos assets do DASH.
$index = [regex]::Replace($index, 'href="app\.css(?:\?v=[^"]*)?"', 'href="app.css?v=1057"')
$index = [regex]::Replace($index, 'src="app\.js(?:\?v=[^"]*)?"', 'src="app.js?v=1057"')
$index = [regex]::Replace($index, 'src="media\.js(?:\?v=[^"]*)?"', 'src="media.js?v=1057"')

foreach($m in @('GAT_DASH_STRUCTURE_057','@media(min-width:1320px)','@media(max-width:1319px) and (min-width:761px)','"media media"','@media(max-width:760px)')) {
    if ($css -notlike "*$m*") { throw "GAT DASH 1.0.57 sem $m" }
}

Set-Content $cssPath $css -Encoding UTF8
Set-Content $indexPath $index -Encoding UTF8
Write-Host 'GAT DASH 1.0.57: tres modulos responsivos; Radio/TV desce em janela media; sem alteracao no layout geral do Telemetria.'
