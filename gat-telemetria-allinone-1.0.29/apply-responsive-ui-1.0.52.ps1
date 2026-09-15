param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte do GAT Telemetria nao encontrado para 1.0.52.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# Versao 1.0.52 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.51.0"', 'CurrentVersion = "1.0.52.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.51"', 'Text = "Cliente 1.0.52"')
$mainText = $mainText.Replace('HUB 1.0.51:', 'HUB 1.0.52:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.51', 'GAT Telemetria BETA 1.0.52')
$hubText = $hubText.Replace('Cliente 1.0.51 TESTE', 'Cliente 1.0.52 TESTE')

# 1.0.51 diminuia o WebView inteiro pelo menor eixo (inclusive a altura),
# deixando o dashboard minusculo em uma janela larga e baixa. Em 1.0.52 o
# WebView fica sempre em 100%; a responsividade passa a ser feita pelo CSS do DASH.
$zoomPattern = '(?s)\s*// O layout visual foi desenhado para aproximadamente 1280x720\.\s*// Reduz ou amplia proporcionalmente para caber no espaco real do monitor/janela\.\s*double zoom = Math\.Min\(w / 1280\.0, h / 720\.0\);\s*zoom = Math\.Max\(0\.62, Math\.Min\(1\.15, zoom\)\);\s*if \(Math\.Abs\(_hubDash041\.ZoomFactor - zoom\) > 0\.015\)\s*_hubDash041\.ZoomFactor = zoom;'
$newZoom = @'

            // GAT_RESPONSIVE_052: nao escala a pagina inteira. O CSS reorganiza
            // o conteudo conforme largura/altura do WebView, mantendo textos legiveis.
            const double zoom = 1.0;
            if (Math.Abs(_hubDash041.ZoomFactor - zoom) > 0.001)
                _hubDash041.ZoomFactor = zoom;
'@
$replaced = [regex]::Replace($hubText, $zoomPattern, $newZoom.TrimEnd(), 1)
if ($replaced -eq $hubText -and $hubText -notlike '*GAT_RESPONSIVE_052*') {
    throw 'Bloco de zoom da 1.0.51 nao encontrado.'
}
$hubText = $replaced

# Mantem chamada no Resize apenas para garantir zoom 100% apos WebView2 recriar viewport.
if ($hubText -notlike '*GAT_RESPONSIVE_052: conteudo interno responsivo*') {
    $hubText = $hubText.Replace('// GAT_RESPONSIVE_051', "// GAT_RESPONSIVE_051`r`n    // GAT_RESPONSIVE_052: conteudo interno responsivo sem miniaturizar o WebView")
}

if ($mainText -notlike '*CurrentVersion = "1.0.52.0"*') { throw 'Versao 1.0.52 nao aplicada.' }
foreach ($m in @('GAT_RESPONSIVE_052','const double zoom = 1.0','MinimumSize = new Size(820, 540)','ArrangeDashTools051')) {
    if ($hubText -notlike "*$m*") { throw "Responsividade 1.0.52 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.52: WebView em 100%; responsividade interna delegada ao GAT DASH.'
