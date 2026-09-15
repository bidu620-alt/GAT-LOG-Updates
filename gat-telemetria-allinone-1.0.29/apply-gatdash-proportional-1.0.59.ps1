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

if ($css -notlike '*GAT_DASH_PROPORTIONAL_059*') {
$css += @'

/* GAT_DASH_PROPORTIONAL_059
   Correcao final focada APENAS no GAT DASH embutido no Windows.

   Diagnostico pelas capturas 1.0.58:
   - maximizado (~1900 px) ficava correto porque o CSS usava 3 colunas;
   - janela normal (~1200 px de viewport interno) entrava no breakpoint <=1319,
     mudava para 2 colunas e jogava Radio/TV para uma terceira linha;
   - o WebView tem pouca altura por causa do cabecalho/menu/ferramentas do
     Telemetria, entao essa terceira linha ficava fora da area visivel.

   Solucao: enquanto houver largura de desktop (>=900 CSS px), MANTER A MESMA
   composicao do modo maximizado: ROTA | CENTRO | RADIO, com Condicao embaixo
   da Rota. Nada de mandar Radio para baixo. As colunas usam minmax(0,fr), sem
   largura minima que possa estourar o WebView. Somente abaixo de 900 px o DASH
   passa a empilhar e usar rolagem vertical.
*/

/* Desktop e janela normal: sempre o mesmo desenho do modo maximizado. */
@media(min-width:900px){
  html,body{overflow:hidden!important;min-width:0!important}
  .dash-screen{height:100%!important;min-height:0!important;overflow:hidden!important;display:flex!important;flex-direction:column!important}
  .dashboard-grid{
    flex:1 1 auto!important;
    width:100%!important;
    height:auto!important;
    min-width:0!important;
    min-height:0!important;
    display:grid!important;
    grid-template-columns:minmax(0,27%) minmax(0,45%) minmax(0,28%)!important;
    grid-template-rows:minmax(0,1fr) 96px!important;
    grid-template-areas:
      "route center media"
      "condition center media"!important;
    gap:6px!important;
    overflow:hidden!important;
  }

  .route-panel{grid-area:route!important}
  .center-cluster{grid-area:center!important}
  .condition-panel{grid-area:condition!important}
  .media-panel{grid-area:media!important;display:flex!important;visibility:visible!important}
  .route-panel,.center-cluster,.condition-panel,.media-panel{
    width:auto!important;
    min-width:0!important;
    min-height:0!important;
    max-width:100%!important;
    max-height:100%!important;
    overflow:hidden!important;
  }
  .media-stage{flex:1 1 auto!important;min-width:0!important;min-height:0!important}
  .media-tabs{grid-template-columns:repeat(2,minmax(0,1fr))!important}
  .dashboard-grid>*{max-width:100%!important}
}

/* Faixa equivalente a janela normal das capturas: nao muda a estrutura,
   apenas compacta elementos internos para caber sem deformacao. */
@media(min-width:900px) and (max-width:1399px){
  .topbar{height:50px!important;min-height:50px!important;padding:0 8px!important}
  .brand-inline{font-size:18px!important;gap:6px!important}.truck-icon{font-size:24px!important}.slogan{font-size:7px!important;letter-spacing:2px!important}.slogan b{font-size:9px!important}
  .connection{font-size:10px!important}.connection small{font-size:8px!important}.icon-btn{height:34px!important;font-size:17px!important}

  .dashboard-grid{grid-template-columns:minmax(0,27%) minmax(0,45%) minmax(0,28%)!important;grid-template-rows:minmax(0,1fr) 86px!important;gap:5px!important}
  .panel-title{height:28px!important;min-height:28px!important;font-size:10px!important;padding:0 6px!important}
  .route-city{padding:4px 7px 2px!important;gap:6px!important}.route-city b{font-size:13px!important}.route-city small{font-size:7px!important}
  .route-line{height:10px!important;margin-left:14px!important}
  .route-stat{padding:3px 7px!important;gap:6px!important}.route-stat>span{font-size:13px!important;width:18px!important}.route-stat b{font-size:11px!important}.route-stat small{font-size:6px!important}

  .center-cluster{padding:3px!important}.indicator-row{height:21px!important;font-size:18px!important}
  .speed-gauge{width:min(27vh,185px)!important;max-width:46%!important}.speed-value{font-size:clamp(34px,5vw,52px)!important}.speed-unit{font-size:11px!important}
  .gear{font-size:15px!important;padding:0 8px!important;margin:1px auto!important}.rpm-line{font-size:7px!important}.rpm-line b{font-size:10px!important}
  .cruise-box{margin-top:-11px!important;min-width:98px!important;padding:1px 4px!important}.cruise-box>span{font-size:13px!important}.cruise-box b{font-size:11px!important}.cruise-box small{font-size:6px!important}
  .speed-notice{width:min(78%,280px)!important;margin-top:3px!important;padding:2px 4px!important;gap:5px!important}.speed-limit-small{width:28px!important;height:28px!important;min-width:28px!important;border-width:3px!important;font-size:9px!important}
  .speed-notice b{font-size:8px!important}.speed-notice small{font-size:6px!important}
  .mini-cards{gap:3px!important}.mini-card{min-height:34px!important;gap:3px!important}.mini-card>span{font-size:11px!important}.mini-card b{font-size:9px!important}.mini-card small{font-size:5.5px!important}

  .condition-summary{padding:2px 5px 0!important}.condition-summary b{font-size:13px!important}.condition-summary span{font-size:7px!important}
  .wear-grid{grid-template-columns:repeat(5,minmax(0,1fr))!important;padding:0 3px!important;gap:2px!important}.wear-grid>div{padding:1px!important}.wear-grid span{font-size:9px!important}.wear-grid small{font-size:5.5px!important}.wear-grid b{font-size:6.5px!important}

  .media-title{height:28px!important;min-height:28px!important}.voice-badge{font-size:7px!important;padding:2px 3px!important}
  .media-tabs{padding:3px!important;gap:3px!important}.media-tab{padding:4px 2px!important;font-size:7px!important}
  .media-footer{min-height:34px!important;padding:3px 5px!important}.media-footer b{font-size:8px!important}.media-footer small{font-size:6px!important}.media-actions button{padding:3px 5px!important;font-size:6.5px!important}
  .footerbar{height:18px!important;min-height:18px!important;margin-top:2px!important;font-size:6px!important}
}

/* Quando a janela fica baixa, reduzimos SOMENTE alturas internas. O layout
   continua em 3 colunas e nunca joga a Radio/TV para fora da tela. */
@media(min-width:900px) and (max-height:590px){
  .topbar{height:42px!important;min-height:42px!important}
  .dashboard-grid{grid-template-rows:minmax(0,1fr) 72px!important;margin-top:4px!important}
  .panel-title{height:24px!important;min-height:24px!important}
  .route-city{padding-top:2px!important;padding-bottom:1px!important}.route-line{height:7px!important}.route-stat{padding-top:2px!important;padding-bottom:2px!important}
  .indicator-row{height:17px!important}.speed-gauge{width:min(22vh,145px)!important}.cruise-box{margin-top:-9px!important}.speed-notice{margin-top:2px!important}.mini-card{min-height:27px!important}
  .media-title{height:23px!important;min-height:23px!important}.media-tab{padding:3px 1px!important}.media-footer{min-height:27px!important;padding:2px 4px!important}
  .condition-summary{padding:1px 4px 0!important}.wear-grid>div{padding:0!important}
  .footerbar{height:16px!important;min-height:16px!important}
}

/* So em largura realmente estreita saimos das 3 colunas. Aqui o objetivo e
   utilizabilidade, com rolagem vertical explicita e sem corte horizontal. */
@media(max-width:899px){
  html,body{overflow:auto!important}
  .dash-screen{height:auto!important;min-height:100%!important;overflow:auto!important}
  .dashboard-grid{
    flex:none!important;
    width:100%!important;
    min-width:0!important;
    min-height:0!important;
    display:grid!important;
    grid-template-columns:minmax(0,1fr)!important;
    grid-template-rows:auto auto auto auto!important;
    grid-template-areas:"route" "center" "condition" "media"!important;
    gap:5px!important;
    overflow:visible!important;
  }
  .route-panel{grid-area:route!important;min-height:220px!important}
  .center-cluster{grid-area:center!important;min-height:340px!important}
  .condition-panel{grid-area:condition!important;min-height:90px!important}
  .media-panel{grid-area:media!important;min-height:220px!important;display:flex!important;visibility:visible!important}
  .footerbar{position:relative!important}
}

#mediaWeb{display:none!important}
.rotate-notice{display:none!important}
'@
}

$index = [regex]::Replace($index, 'href="app\.css(?:\?v=[^"]*)?"', 'href="app.css?v=1059"')
$index = [regex]::Replace($index, 'src="app\.js(?:\?v=[^"]*)?"', 'src="app.js?v=1059"')
$index = [regex]::Replace($index, 'src="media\.js(?:\?v=[^"]*)?"', 'src="media.js?v=1059"')

foreach($m in @('GAT_DASH_PROPORTIONAL_059','@media(min-width:900px)','grid-template-columns:minmax(0,27%) minmax(0,45%) minmax(0,28%)','"route center media"','"condition center media"','@media(max-width:899px)')) {
    if ($css -notlike "*$m*") { throw "GAT DASH 1.0.59 sem $m" }
}

Set-Content $cssPath $css -Encoding UTF8
Set-Content $indexPath $index -Encoding UTF8
Write-Host 'GAT DASH 1.0.59: desktop/janela normal mantem as mesmas 3 colunas do modo maximizado; empilhamento somente abaixo de 900 px.'
