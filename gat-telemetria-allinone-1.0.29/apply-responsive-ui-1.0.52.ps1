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

# Substitui por inteiro o metodo de zoom criado na 1.0.51.
# Na 1.0.51 o menor eixo da janela reduzia o WebView inteiro, fazendo o painel
# interno ficar minusculo em janelas largas/baixas. Na 1.0.52 o WebView fica 100%
# e o proprio HTML/CSS faz o ajuste responsivo.
$startMarker = '    private void ApplyDashZoom051()'
$endMarker = '    private Control HubHeader041()'
$start = $hubText.IndexOf($startMarker)
$end = if ($start -ge 0) { $hubText.IndexOf($endMarker, $start) } else { -1 }
if ($start -lt 0 -or $end -lt 0 -or $end -le $start) {
    throw 'Metodo ApplyDashZoom051 da 1.0.51 nao encontrado.'
}
$newMethod = @'
    // GAT_RESPONSIVE_052: conteudo interno responsivo sem miniaturizar o WebView.
    private void ApplyDashZoom051()
    {
        try
        {
            if (_hubDash041 == null || _hubDash041.IsDisposed) return;
            const double zoom = 1.0;
            if (Math.Abs(_hubDash041.ZoomFactor - zoom) > 0.001)
                _hubDash041.ZoomFactor = zoom;
        }
        catch { }
    }

'@
$hubText = $hubText.Substring(0, $start) + $newMethod + $hubText.Substring($end)

if ($mainText -notlike '*CurrentVersion = "1.0.52.0"*') { throw 'Versao 1.0.52 nao aplicada.' }
foreach ($m in @('GAT_RESPONSIVE_052','const double zoom = 1.0','MinimumSize = new Size(820, 540)','ArrangeDashTools051')) {
    if ($hubText -notlike "*$m*") { throw "Responsividade 1.0.52 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.52: WebView em 100%; responsividade interna delegada ao GAT DASH.'
