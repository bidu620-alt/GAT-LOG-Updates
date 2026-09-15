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

if ($css -notlike '*GAT_DASH_STRUCTURE_FIX_058*') {
$css += @'

/* GAT_DASH_STRUCTURE_FIX_058
   Diagnostico confirmado na 1.0.57:
   o layout medio criava 3 linhas com minimos de 250px + 78px + 155px
   (e no modo baixo 215px + 65px + 115px). Dentro do WebView do Telemetria
   havia menos altura util do que essa soma. Como .dash-screen e .dashboard-grid
   estavam com overflow:hidden, a terceira linha (Radio/TV) era empurrada para
   baixo e simplesmente cortada da tela.

   Esta correcao mexe SOMENTE no GAT DASH embutido. O restante do Telemetria
   permanece igual. Remove os minimos verticais excessivos e garante que os 3
   modulos sempre tenham espaco real antes de qualquer corte.
*/

@media(max-width:1319px) and (min-width:761px){
  .dashboard-grid{
    /* Rota / Condicao + Centro nas duas primeiras linhas; Radio na terceira. */
    grid-template-columns:minmax(0,42%) minmax(0,58%)!important;
    grid-template-rows:minmax(0,1fr) 68px minmax(138px,.48fr)!important;
    grid-template-areas:
      "route center"
      "condition center"
      "media media"!important;
    overflow:hidden!important;
  }

  .route-panel,.condition-panel,.center-cluster,.media-panel{
    min-width:0!important;
    min-height:0!important;
    max-width:100%!important;
    max-height:100%!important;
  }

  /* O player pode encolher, mas nunca desaparece. */
  .media-panel{display:flex!important;visibility:visible!important;overflow:hidden!important}
  .media-title{height:26px!important;min-height:26px!important}
  .media-tabs{flex:0 0 auto!important;padding:3px 5px!important}
  .media-stage{flex:1 1 auto!important;min-height:42px!important}
  .media-footer{flex:0 0 auto!important;min-height:31px!important;padding:3px 6px!important}
}

/* A faixa da captura enviada pelo usuario cai aqui: viewport do DASH baixo,
   mesmo com a janela externa tendo ~739px. O WebView perde altura por causa do
   cabecalho/menu/ferramentas do Telemetria. Os antigos minimos somavam mais do
   que a area disponivel. Agora a soma fixa cabe e o restante vai para Rota. */
@media(max-width:1319px) and (min-width:761px) and (max-height:590px){
  .dashboard-grid{
    grid-template-rows:minmax(142px,1fr) 62px 132px!important;
    overflow:hidden!important;
  }

  .panel-title{height:25px!important;min-height:25px!important;font-size:10px!important}
  .route-city{padding:3px 7px 1px!important}.route-city b{font-size:13px!important}.route-city small{font-size:7px!important}
  .route-line{height:8px!important;margin-left:14px!important}
  .route-stat{padding:3px 7px!important}.route-stat>span{font-size:13px!important;width:18px!important}.route-stat b{font-size:11px!important}.route-stat small{font-size:6px!important}

  .condition-summary{padding:2px 5px 0!important}.condition-summary b{font-size:13px!important}.condition-summary span{font-size:7px!important}
  .wear-grid{padding:0 3px!important;gap:2px!important}.wear-grid>div{padding:1px!important}.wear-grid span{font-size:9px!important}.wear-grid small{font-size:5.5px!important}.wear-grid b{font-size:6.5px!important}

  .indicator-row{height:19px!important;font-size:17px!important}
  .speed-gauge{width:min(23vh,158px)!important;max-width:44%!important}
  .speed-value{font-size:clamp(32px,5vw,48px)!important}.speed-unit{font-size:11px!important}
  .gear{font-size:15px!important;padding:0 8px!important;margin:1px auto!important}.rpm-line{font-size:7px!important}.rpm-line b{font-size:10px!important}
  .cruise-box{margin-top:-10px!important;min-width:96px!important;padding:1px 4px!important}.cruise-box>span{font-size:13px!important}.cruise-box b{font-size:11px!important}
  .speed-notice{width:min(72%,270px)!important;margin-top:3px!important;padding:2px 4px!important}.speed-limit-small{width:27px!important;height:27px!important;min-width:27px!important;border-width:3px!important;font-size:9px!important}
  .speed-notice b{font-size:8px!important}.speed-notice small{font-size:6px!important}
  .mini-cards{gap:3px!important}.mini-card{min-height:31px!important}.mini-card>span{font-size:11px!important}.mini-card b{font-size:9px!important}.mini-card small{font-size:5.5px!important}

  .media-title{height:24px!important;min-height:24px!important}
  .media-tab{padding:3px 2px!important;font-size:7px!important}
  .media-stage{min-height:38px!important}
  .media-footer{min-height:28px!important;padding:2px 5px!important}
  .media-footer b{font-size:8px!important}.media-footer small{font-size:6px!important}.media-actions button{padding:3px 5px!important;font-size:6.5px!important}
}

/* Se o usuario for alem e deixar a janela realmente baixa, nao cortamos a
   Radio/TV: o DASH ganha rolagem vertical apenas nesse caso extremo. */
@media(max-width:1319px) and (min-width:761px) and (max-height:455px){
  html,body{overflow:auto!important}
  .dash-screen{height:auto!important;min-height:100%!important;overflow:auto!important}
  .dashboard-grid{
    flex:none!important;
    min-height:430px!important;
    overflow:visible!important;
  }
}
'@
}

$index = [regex]::Replace($index, 'href="app\.css(?:\?v=[^"]*)?"', 'href="app.css?v=1058"')
$index = [regex]::Replace($index, 'src="app\.js(?:\?v=[^"]*)?"', 'src="app.js?v=1058"')
$index = [regex]::Replace($index, 'src="media\.js(?:\?v=[^"]*)?"', 'src="media.js?v=1058"')

foreach($m in @('GAT_DASH_STRUCTURE_FIX_058','grid-template-rows:minmax(0,1fr) 68px minmax(138px,.48fr)','grid-template-rows:minmax(142px,1fr) 62px 132px','media-stage{flex:1 1 auto!important;min-height:42px!important}','@media(max-width:1319px) and (min-width:761px) and (max-height:455px)')) {
    if ($css -notlike "*$m*") { throw "GAT DASH 1.0.58 sem $m" }
}

Set-Content $cssPath $css -Encoding UTF8
Set-Content $indexPath $index -Encoding UTF8
Write-Host 'GAT DASH 1.0.58: corrigido corte vertical da Radio/TV causado pelos min-height excessivos da 1.0.57.'
