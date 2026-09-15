param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $truck -or -not $video) { throw 'Fonte 1.0.54 incompleto para aplicar 1.0.55.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# ---------------------------------------------------------------------------
# Versao 1.0.55 TESTE.
# ---------------------------------------------------------------------------
$mainText = $mainText.Replace('CurrentVersion = "1.0.54.0"', 'CurrentVersion = "1.0.55.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.54"', 'Text = "Cliente 1.0.55"')
$mainText = $mainText.Replace('HUB 1.0.54:', 'HUB 1.0.55:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.54', 'GAT Telemetria BETA 1.0.55')
$hubText = $hubText.Replace('Cliente 1.0.54 TESTE', 'Cliente 1.0.55 TESTE')

# ---------------------------------------------------------------------------
# OVERLAYS COMO JANELAS INDEPENDENTES NORMAIS.
# Caminhao e video passam a ter barra do Windows, minimizar e fechar.
# Eles continuam independentes do GAT Telemetria e TopMost quando restaurados.
# ---------------------------------------------------------------------------
$truckText = $truckText.Replace('ShowInTaskbar = false;', 'ShowInTaskbar = true;')
$truckText = $truckText.Replace('FormBorderStyle = FormBorderStyle.None;', 'FormBorderStyle = FormBorderStyle.Sizable;`r`n        MinimizeBox = true;`r`n        MaximizeBox = false;`r`n        ControlBox = true;')
# O botao X customizado nao e mais necessario porque a barra da janela tem Fechar.
$truckText = $truckText.Replace('_header.Controls.Add(_close);', '_close.Visible = false;')
$truckText = $truckText.Replace('Arraste pelo topo • redimensione pelas bordas.', 'Mova pela barra da janela • redimensione pelas bordas.')

$videoText = $videoText.Replace('ShowInTaskbar = false;', 'ShowInTaskbar = true;')
$videoText = $videoText.Replace('FormBorderStyle = FormBorderStyle.SizableToolWindow;', 'FormBorderStyle = FormBorderStyle.Sizable;`r`n        MinimizeBox = true;`r`n        MaximizeBox = false;`r`n        ControlBox = true;')

# 1.0.54 reforcava ShowInTaskbar=false ao abrir. Agora cada overlay e uma janela real.
$hubText = $hubText.Replace('_truckOverlay041.TopMost = true; _truckOverlay041.ShowInTaskbar = false;', '_truckOverlay041.TopMost = true; _truckOverlay041.ShowInTaskbar = true;')
$hubText = $hubText.Replace('_videoOverlay041.TopMost = true; _videoOverlay041.ShowInTaskbar = false;', '_videoOverlay041.TopMost = true; _videoOverlay041.ShowInTaskbar = true;')

# ---------------------------------------------------------------------------
# Remove FECHAR OVERLAYS. Cada janela agora controla minimizar/fechar sozinha.
# ---------------------------------------------------------------------------
$closeLinePattern = 'var close = HubButton041\("FECHAR OVERLAYS",\s*150\);\s*close\.Left = 360;\s*close\.Top = 9;\s*close\.Click \+= delegate \{ CloseOverlays041\(\); \};\s*tools\.Controls\.Add\(close\);\s*'
$hubText = [regex]::Replace($hubText, $closeLinePattern, '', 1)

# Reposiciona transparencia para aproveitar o espaco liberado.
$hubText = $hubText.Replace('Text = "Transparência", Left = 530, Top = 19, Width = 95', 'Text = "Transparência", Left = 365, Top = 19, Width = 90')
$hubText = $hubText.Replace('Left = 625, Top = 6, Width = 180', 'Left = 455, Top = 6, Width = 180')
$hubText = $hubText.Replace('tools.ClientSize.Width - 640', 'tools.ClientSize.Width - 470')

# ---------------------------------------------------------------------------
# RESPONSIVIDADE DO APLICATIVO INTEIRO.
# Menor minimo, tamanho inicial baseado na area util real do monitor, paginas com
# rolagem de seguranca, menu e cabecalho compactos e botoes proporcionais.
# ---------------------------------------------------------------------------
$hubText = $hubText.Replace('MinimumSize = new Size(820, 540);', 'MinimumSize = new Size(720, 480);')
$hubText = $hubText.Replace('Math.Min(1220, Math.Max(820, work051.Width - 32))', 'Math.Min(1220, Math.Max(720, work051.Width - 24))')
$hubText = $hubText.Replace('Math.Min(820, Math.Max(540, work051.Height - 48))', 'Math.Min(820, Math.Max(480, work051.Height - 36))')

# Toda pagina passa a ter scroll de seguranca: nada some em monitor menor.
$oldPage = 'private Panel Page041() { return new Panel { BackColor = Color.FromArgb(3, 11, 22) }; }'
$newPage = 'private Panel Page041() { return new Panel { BackColor = Color.FromArgb(3, 11, 22), AutoScroll = true, AutoScrollMinSize = new Size(680, 400) }; }'
if ($hubText.Contains($oldPage)) { $hubText = $hubText.Replace($oldPage, $newPage) }
elseif ($hubText -notlike '*AutoScrollMinSize = new Size(680, 400)*') { throw 'Page041 nao encontrado para responsividade 1.0.55.' }

# Nomeia cabecalho para poder compactar seus textos em telas menores.
$hubText = $hubText.Replace('var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 29) };', 'var p = new Panel { Name = "hubHeader055", Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 15, 29) };')
$hubText = $hubText.Replace('Text = "GAT", Left = 22, Top = 10, AutoSize = true', 'Name = "hubBrand055", Text = "GAT", Left = 22, Top = 10, AutoSize = true')
$hubText = $hubText.Replace('Text = "GAT TELEMETRIA BETA", Left = 118, Top = 13, AutoSize = true', 'Name = "hubTitle055", Text = "GAT TELEMETRIA BETA", Left = 118, Top = 13, AutoSize = true')
$hubText = $hubText.Replace('Text = "Central principal do ecossistema GAT LOG ETS2 • tudo em um só lugar", Left = 121, Top = 47, AutoSize = true', 'Name = "hubSub055", Text = "Central principal do ecossistema GAT LOG ETS2 • tudo em um só lugar", Left = 121, Top = 47, AutoSize = true')

# Substitui o ArrangeDashTools da 1.0.51 para considerar somente os dois botoes.
$startMarker = '    private void ArrangeDashTools051(Panel tools, bool veryCompact)'
$endMarker = '    private void ApplyDashZoom051()'
$start = $hubText.IndexOf($startMarker)
$end = if ($start -ge 0) { $hubText.IndexOf($endMarker, $start) } else { -1 }
if ($start -lt 0 -or $end -lt 0 -or $end -le $start) { throw 'ArrangeDashTools051 nao encontrado.' }
$newArrange = @'
    private void ArrangeDashTools051(Panel tools, bool veryCompact)
    {
        var buttons = tools.Controls.OfType<Button>().ToList();
        var label = tools.Controls.OfType<Label>().FirstOrDefault(x => string.Equals(x.Text, "Transparência", StringComparison.Ordinal));
        int width = Math.Max(1, tools.ClientSize.Width);
        int gap = 8;
        int x = 8;
        int bw1 = veryCompact ? 112 : 150;
        int bw2 = veryCompact ? 136 : 180;
        if (buttons.Count > 0) { buttons[0].Left = x; buttons[0].Width = bw1; buttons[0].Font = new Font("Segoe UI Semibold", veryCompact ? 7.0f : 8.0f, FontStyle.Bold); x += bw1 + gap; }
        if (buttons.Count > 1) { buttons[1].Left = x; buttons[1].Width = bw2; buttons[1].Font = new Font("Segoe UI Semibold", veryCompact ? 7.0f : 8.0f, FontStyle.Bold); x += bw2 + gap; }
        if (label != null)
        {
            label.Visible = width >= 700;
            label.Left = x + 4;
            label.Width = 88;
            if (label.Visible) x += 94;
        }
        if (_overlayOpacity041 != null)
        {
            _overlayOpacity041.Left = x;
            _overlayOpacity041.Width = Math.Max(90, width - x - 8);
        }
    }

'@
$hubText = $hubText.Substring(0, $start) + $newArrange + $hubText.Substring($end)

# Injeta melhorias no metodo responsivo ja existente.
$needle = 'bool veryCompact = w < 930 || h < 640;'
if ($hubText.Contains($needle)) {
    $hubText = $hubText.Replace($needle, 'bool veryCompact = w < 1024 || h < 650;`r`n            bool ultraCompact = w < 850 || h < 560;')
} elseif ($hubText -notlike '*bool ultraCompact = w < 850 || h < 560;*') { throw 'Flags responsivas nao encontradas.' }

$hubText = $hubText.Replace('_hubBody041.Padding = compact ? new Padding(10, 8, 10, 8) : new Padding(18, 14, 18, 14);', '_hubBody041.Padding = ultraCompact ? new Padding(5, 4, 5, 4) : (compact ? new Padding(9, 7, 9, 7) : new Padding(18, 14, 18, 14));')
$hubText = $hubText.Replace('shell.RowStyles[0].Height = veryCompact ? 58 : (compact ? 66 : 76);', 'shell.RowStyles[0].Height = ultraCompact ? 50 : (veryCompact ? 56 : (compact ? 64 : 76));')
$hubText = $hubText.Replace('shell.RowStyles[1].Height = veryCompact ? 42 : (compact ? 46 : 50);', 'shell.RowStyles[1].Height = ultraCompact ? 36 : (veryCompact ? 40 : (compact ? 44 : 50));')
$hubText = $hubText.Replace('shell.RowStyles[3].Height = compact ? 23 : 28;', 'shell.RowStyles[3].Height = ultraCompact ? 19 : (compact ? 22 : 28);')

# Depois de ajustar altura/fontes do nav, ajusta tambem as larguras proporcionalmente.
$navNeedle = 'b.Font = new Font("Segoe UI Semibold", veryCompact ? 7.2f : (compact ? 8f : 8.5f), FontStyle.Bold);'
if ($hubText.Contains($navNeedle) -and $hubText -notlike '*GAT_NAV_WIDTH_055*') {
    $navPatch = @'
b.Font = new Font("Segoe UI Semibold", ultraCompact ? 6.5f : (veryCompact ? 7.2f : (compact ? 8f : 8.5f)), FontStyle.Bold);
                }
                // GAT_NAV_WIDTH_055: encaixa todas as abas proporcionalmente na largura disponivel.
                var navButtons055 = nav.Controls.OfType<Button>().ToList();
                int baseTotal055 = 105 + 120 + 125 + 80 + 175 + 135 + 145;
                int avail055 = Math.Max(560, nav.ClientSize.Width - nav.Padding.Horizontal - 42);
                double navScale055 = Math.Min(1.0, avail055 / (double)baseTotal055);
                int[] base055 = { 105, 120, 125, 80, 175, 135, 145 };
                for (int i055 = 0; i055 < navButtons055.Count && i055 < base055.Length; i055++)
                    navButtons055[i055].Width = Math.Max(66, (int)Math.Round(base055[i055] * navScale055));
                foreach (Button b055 in navButtons055) b055.Margin = new Padding(0, 0, ultraCompact ? 3 : 6, 0);
                // mantem o fluxo horizontal; se a janela for extrema ainda ha AutoScroll como seguranca.
                if (ultraCompact) nav.Padding = new Padding(5, 3, 0, 2);
                foreach (Button dummy055 in new Button[0])
                {
'@
    $hubText = $hubText.Replace($navNeedle, $navPatch.TrimEnd())
}

# Ajuste do cabecalho em resolucao menor.
$applyNeedle = 'var tools = FindControl051<Panel>(this, "dashTools051");'
if ($hubText.Contains($applyNeedle) -and $hubText -notlike '*GAT_HEADER_055*') {
    $headerCode = @'
            // GAT_HEADER_055
            var brand055 = FindControl051<Label>(this, "hubBrand055");
            var title055 = FindControl051<Label>(this, "hubTitle055");
            var sub055 = FindControl051<Label>(this, "hubSub055");
            if (brand055 != null)
            {
                brand055.Left = ultraCompact ? 10 : 22; brand055.Top = ultraCompact ? 5 : 10;
                brand055.Font = new Font("Segoe UI Black", ultraCompact ? 18f : (veryCompact ? 21f : 25f), FontStyle.Bold | FontStyle.Italic);
            }
            if (title055 != null)
            {
                title055.Left = ultraCompact ? 78 : (veryCompact ? 98 : 118); title055.Top = ultraCompact ? 7 : 13;
                title055.Font = new Font("Segoe UI Semibold", ultraCompact ? 12f : (veryCompact ? 15f : 18f), FontStyle.Bold);
            }
            if (sub055 != null)
            {
                sub055.Visible = !ultraCompact;
                sub055.Left = veryCompact ? 101 : 121; sub055.Top = veryCompact ? 37 : 47;
                sub055.Font = new Font("Segoe UI", veryCompact ? 7.5f : 9f);
            }

'@
    $hubText = $hubText.Replace($applyNeedle, $headerCode + $applyNeedle)
}

# Em resolucao menor, abre quase ocupando a area util, mas sem forcar maximizado.
$shownNeedle = 'Shown += async delegate { SyncHub041(); await InitDash041(); ApplyResponsive051(); };'
if ($hubText.Contains($shownNeedle) -and $hubText -notlike '*GAT_FIT_SCREEN_055*') {
    $shownNew = @'
Shown += async delegate
        {
            // GAT_FIT_SCREEN_055
            try
            {
                var wa055 = Screen.FromControl(this).WorkingArea;
                if (wa055.Width < 1400 || wa055.Height < 800)
                {
                    int ww055 = Math.Max(MinimumSize.Width, Math.Min(1220, wa055.Width - 20));
                    int hh055 = Math.Max(MinimumSize.Height, Math.Min(820, wa055.Height - 24));
                    Size = new Size(ww055, hh055);
                    Location = new Point(wa055.Left + Math.Max(0, (wa055.Width - ww055) / 2), wa055.Top + Math.Max(0, (wa055.Height - hh055) / 2));
                }
            }
            catch { }
            SyncHub041(); await InitDash041(); ApplyResponsive051();
        };
'@
    $hubText = $hubText.Replace($shownNeedle, $shownNew.TrimEnd())
}

# Validacoes.
if ($mainText -notlike '*CurrentVersion = "1.0.55.0"*') { throw 'Versao 1.0.55 nao aplicada.' }
if ($hubText -like '*FECHAR OVERLAYS*') { throw 'Botao FECHAR OVERLAYS ainda presente.' }
foreach ($m in @('MinimumSize = new Size(720, 480)','AutoScrollMinSize = new Size(680, 400)','GAT_NAV_WIDTH_055','GAT_HEADER_055','GAT_FIT_SCREEN_055','ShowInTaskbar = true')) {
    if (($m -eq 'ShowInTaskbar = true' -and ($truckText -notlike "*$m*" -or $videoText -notlike "*$m*")) -or ($m -ne 'ShowInTaskbar = true' -and $hubText -notlike "*$m*")) { throw "1.0.55 sem $m" }
}
foreach ($m in @('FormBorderStyle = FormBorderStyle.Sizable','MinimizeBox = true','ControlBox = true')) {
    if ($truckText -notlike "*$m*" -or $videoText -notlike "*$m*") { throw "Janelas de overlay 1.0.55 sem $m" }
}
if ($hubText -like '*ShowInTaskbar = false*') { throw 'Hub ainda forca overlay fora da barra de tarefas.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.55: overlays como janelas independentes com minimizar/fechar; botao fechar overlays removido; aplicativo inteiro responsivo.'
