param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
Get-ChildItem $rootPath -Filter '*.cs' -Recurse | ForEach-Object {
    $text = Get-Content $_.FullName -Raw
    if ($text.Contains('`r`n')) {
        $text = $text.Replace('`r`n', [Environment]::NewLine)
        Set-Content $_.FullName $text -Encoding UTF8
    }
}

$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $hub -or -not $truck -or -not $video) { throw 'Arquivos 1.0.55 nao encontrados apos hotfix.' }
$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

if ($hubText.Contains('`r`n') -or $truckText.Contains('`r`n') -or $videoText.Contains('`r`n')) { throw 'Ainda existem quebras literais apos hotfix 1.0.55.' }

# HOTFIX_DASH_HOST_055
# Page041 ganhou AutoScroll na 1.0.55 para monitores menores. Isso interfere no
# Anchor dos controles do GAT DASH quando a pagina nasce pequena e depois e
# redimensionada. O resultado era exatamente o visto no teste: barra de ferramentas
# e WebView ficavam estreitos no canto esquerdo e o DASH acreditava estar em modo
# retrato de celular. Na pagina DASH, desliga o AutoScroll e dimensiona explicitamente.
$dashHead = 'var p = Page041(); p.Controls.Add(Head041("GAT DASH", "O dashboard que já usamos, agora dentro do GAT Telemetria."));'
$dashHeadNew = 'var p = Page041(); p.AutoScroll = false; p.AutoScrollMinSize = Size.Empty; p.Controls.Add(Head041("GAT DASH", "O dashboard que já usamos, agora dentro do GAT Telemetria."));'
if ($hubText.Contains($dashHead)) {
    $hubText = $hubText.Replace($dashHead, $dashHeadNew)
} elseif ($hubText -notlike '*p.AutoScroll = false; p.AutoScrollMinSize = Size.Empty*') {
    throw 'Inicio da pagina GAT DASH nao encontrado para hotfix 1.0.55.'
}

$webPattern = '_hubDash041\s*=\s*new WebView2\s*\{\s*Left\s*=\s*0,\s*Top\s*=\s*120,\s*Width\s*=\s*p\.Width,\s*Height\s*=\s*Math\.Max\(300,\s*p\.Height\s*-\s*120\),\s*Anchor\s*=\s*AnchorStyles\.Top\s*\|\s*AnchorStyles\.Bottom\s*\|\s*AnchorStyles\.Left\s*\|\s*AnchorStyles\.Right,\s*BackColor\s*=\s*Color\.FromArgb\(2,\s*10,\s*20\)\s*\};'
$webReplacement = @'
_hubDash041 = new WebView2 { Left = 0, Top = 120, Width = Math.Max(1, p.ClientSize.Width), Height = Math.Max(1, p.ClientSize.Height - 120), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(2, 10, 20) };
        p.Resize += delegate
        {
            try
            {
                tools.SetBounds(0, 55, Math.Max(1, p.ClientSize.Width), 55);
                if (_hubDash041 != null && !_hubDash041.IsDisposed)
                    _hubDash041.SetBounds(0, 120, Math.Max(1, p.ClientSize.Width), Math.Max(1, p.ClientSize.Height - 120));
            }
            catch { }
        };
'@.TrimEnd()

if ([regex]::IsMatch($hubText, $webPattern)) {
    $hubText = [regex]::Replace($hubText, $webPattern, $webReplacement, 1)
} elseif ($hubText -notlike '*tools.SetBounds(0, 55, Math.Max(1, p.ClientSize.Width), 55)*') {
    throw 'Criacao do WebView do GAT DASH nao encontrada para hotfix 1.0.55.'
}

# A barra de titulo ainda podia herdar 1.0.50 do instalador-base. Corrige o texto
# visual sem alterar o canal/versao interna do cliente.
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+";', 'Text = "GAT Telemetria BETA 1.0.55";', 1)

foreach ($marker in @('HOTFIX_DASH_HOST_055','p.AutoScroll = false; p.AutoScrollMinSize = Size.Empty','tools.SetBounds(0, 55, Math.Max(1, p.ClientSize.Width), 55)','GAT Telemetria BETA 1.0.55')) {
    if ($marker -eq 'HOTFIX_DASH_HOST_055') { continue }
    if ($hubText -notlike "*$marker*") { throw "Hotfix GAT DASH 1.0.55 sem $marker" }
}

Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'Hotfix 1.0.55: quebras normalizadas; pagina GAT DASH ocupa toda a largura/altura e titulo corrigido.'
