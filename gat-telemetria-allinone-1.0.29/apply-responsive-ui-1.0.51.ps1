param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte do GAT Telemetria nao encontrado para 1.0.51.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# Versao 1.0.51 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.50.0"', 'CurrentVersion = "1.0.51.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.50"', 'Text = "Cliente 1.0.51"')
$mainText = $mainText.Replace('HUB 1.0.50:', 'HUB 1.0.51:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.50', 'GAT Telemetria BETA 1.0.51')
$hubText = $hubText.Replace('Cliente 1.0.42 TESTE', 'Cliente 1.0.51 TESTE')
$hubText = $hubText.Replace('Cliente 1.0.50 TESTE', 'Cliente 1.0.51 TESTE')

# Janela inicial respeita a area util do monitor e permite resolucoes menores.
$oldSize = 'MinimumSize = new Size(1040, 690);`r`n        Size = new Size(1220, 820);'
if (-not $hubText.Contains($oldSize)) {
    $oldSize = "MinimumSize = new Size(1040, 690);`n        Size = new Size(1220, 820);"
}
$newSize = @'
MinimumSize = new Size(820, 540);
        AutoScaleMode = AutoScaleMode.Dpi;
        var work051 = Screen.FromControl(this).WorkingArea;
        Size = new Size(
            Math.Min(1220, Math.Max(820, work051.Width - 32)),
            Math.Min(820, Math.Max(540, work051.Height - 48)));
'@.TrimEnd()
if ($hubText.Contains($oldSize)) {
    $hubText = $hubText.Replace($oldSize, $newSize)
} elseif ($hubText -notlike '*MinimumSize = new Size(820, 540)*') {
    throw 'Bloco de tamanho inicial do Hub nao encontrado.'
}

# Marca os containers principais para o layout responsivo.
$hubText = $hubText.Replace(
    'var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor, Margin = Padding.Empty, Padding = Padding.Empty };',
    'var shell = new TableLayoutPanel { Name = "hubShell051", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor, Margin = Padding.Empty, Padding = Padding.Empty };'
)
$hubText = $hubText.Replace(
    'var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 6, 0, 4), BackColor = Color.FromArgb(5, 18, 33) };',
    'var p = new FlowLayoutPanel { Name = "hubNav051", Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 6, 0, 4), BackColor = Color.FromArgb(5, 18, 33) };'
)
$hubText = $hubText.Replace(
    'var tools = new Panel { Left = 0, Top = 55, Height = 55, Width = p.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(5, 20, 36) };',
    'var tools = new Panel { Name = "dashTools051", Left = 0, Top = 55, Height = 55, Width = p.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(5, 20, 36) };'
)

# Recalcula o zoom do DASH sempre que o WebView muda de tamanho.
$dashNeedle = 'p.Controls.Add(_hubDash041); return p;'
if ($hubText.Contains($dashNeedle)) {
    $hubText = $hubText.Replace($dashNeedle, '_hubDash041.Resize += delegate { ApplyDashZoom051(); }; p.Controls.Add(_hubDash041); return p;')
} elseif ($hubText -notlike '*ApplyDashZoom051()*') {
    throw 'Criacao do WebView do GAT DASH nao encontrada.'
}

# Aplica responsividade apos abrir e enquanto o usuario redimensiona a janela.
$shownPattern = 'Shown\s*\+=\s*async\s+delegate\s*\{\s*SyncHub041\(\);\s*await\s+InitDash041\(\);\s*\};'
if ([regex]::IsMatch($hubText, $shownPattern)) {
    $hubText = [regex]::Replace($hubText, $shownPattern, 'Shown += async delegate { SyncHub041(); await InitDash041(); ApplyResponsive051(); };`r`n        Resize += delegate { ApplyResponsive051(); };', 1)
} elseif ($hubText -notlike '*Resize += delegate { ApplyResponsive051(); }*') {
    throw 'Evento Shown do Hub nao encontrado.'
}

# Injeta os helpers antes do primeiro metodo util apos a construcao do Hub.
if ($hubText -notlike '*GAT_RESPONSIVE_051*') {
    $marker = '    private Control HubHeader041()'
    $idx = $hubText.IndexOf($marker)
    if ($idx -lt 0) { throw 'HubHeader041 nao encontrado.' }
    $responsiveCode = @'
    // GAT_RESPONSIVE_051
    private void ApplyResponsive051()
    {
        try
        {
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);
            bool compact = w < 1180 || h < 760;
            bool veryCompact = w < 930 || h < 640;

            if (_hubBody041 != null)
                _hubBody041.Padding = compact ? new Padding(10, 8, 10, 8) : new Padding(18, 14, 18, 14);

            var shell = FindControl051<TableLayoutPanel>(this, "hubShell051");
            if (shell != null && shell.RowStyles.Count >= 4)
            {
                shell.RowStyles[0].Height = veryCompact ? 58 : (compact ? 66 : 76);
                shell.RowStyles[1].Height = veryCompact ? 42 : (compact ? 46 : 50);
                shell.RowStyles[3].Height = compact ? 23 : 28;
            }

            var nav = FindControl051<FlowLayoutPanel>(this, "hubNav051");
            if (nav != null)
            {
                nav.Padding = veryCompact ? new Padding(8, 4, 0, 3) : (compact ? new Padding(12, 5, 0, 4) : new Padding(18, 6, 0, 4));
                foreach (Button b in nav.Controls.OfType<Button>())
                {
                    b.Height = veryCompact ? 30 : (compact ? 33 : 36);
                    b.Font = new Font("Segoe UI Semibold", veryCompact ? 7.2f : (compact ? 8f : 8.5f), FontStyle.Bold);
                }
            }

            var tools = FindControl051<Panel>(this, "dashTools051");
            if (tools != null)
                ArrangeDashTools051(tools, veryCompact);

            ApplyDashZoom051();
        }
        catch { }
    }

    private static T FindControl051<T>(Control root, string name) where T : Control
    {
        if (root == null) return null;
        foreach (Control c in root.Controls)
        {
            if (c is T match && string.Equals(c.Name, name, StringComparison.Ordinal)) return match;
            var nested = FindControl051<T>(c, name);
            if (nested != null) return nested;
        }
        return null;
    }

    private void ArrangeDashTools051(Panel tools, bool veryCompact)
    {
        var buttons = tools.Controls.OfType<Button>().ToList();
        var label = tools.Controls.OfType<Label>().FirstOrDefault(x => string.Equals(x.Text, "Transparência", StringComparison.Ordinal));
        int width = Math.Max(1, tools.ClientSize.Width);

        if (!veryCompact && width >= 1000)
        {
            if (buttons.Count > 0) { buttons[0].Left = 10; buttons[0].Width = 150; }
            if (buttons.Count > 1) { buttons[1].Left = 170; buttons[1].Width = 180; }
            if (buttons.Count > 2) { buttons[2].Left = 360; buttons[2].Width = 150; }
            if (label != null) { label.Visible = true; label.Left = 530; label.Width = 95; }
            if (_overlayOpacity041 != null) { _overlayOpacity041.Left = 625; _overlayOpacity041.Width = Math.Max(120, width - 640); }
        }
        else
        {
            int gap = 6;
            int x = 8;
            int bw1 = width < 860 ? 104 : 120;
            int bw2 = width < 860 ? 126 : 145;
            int bw3 = width < 860 ? 104 : 120;
            if (buttons.Count > 0) { buttons[0].Left = x; buttons[0].Width = bw1; buttons[0].Font = new Font("Segoe UI Semibold", 7.1f, FontStyle.Bold); x += bw1 + gap; }
            if (buttons.Count > 1) { buttons[1].Left = x; buttons[1].Width = bw2; buttons[1].Font = new Font("Segoe UI Semibold", 7.1f, FontStyle.Bold); x += bw2 + gap; }
            if (buttons.Count > 2) { buttons[2].Left = x; buttons[2].Width = bw3; buttons[2].Font = new Font("Segoe UI Semibold", 7.1f, FontStyle.Bold); x += bw3 + gap; }
            if (label != null) label.Visible = false;
            if (_overlayOpacity041 != null)
            {
                _overlayOpacity041.Left = x;
                _overlayOpacity041.Width = Math.Max(90, width - x - 8);
            }
        }
    }

    private void ApplyDashZoom051()
    {
        try
        {
            if (_hubDash041 == null || _hubDash041.IsDisposed) return;
            int w = Math.Max(1, _hubDash041.ClientSize.Width);
            int h = Math.Max(1, _hubDash041.ClientSize.Height);

            // O layout visual foi desenhado para aproximadamente 1280x720.
            // Reduz ou amplia proporcionalmente para caber no espaco real do monitor/janela.
            double zoom = Math.Min(w / 1280.0, h / 720.0);
            zoom = Math.Max(0.62, Math.Min(1.15, zoom));
            if (Math.Abs(_hubDash041.ZoomFactor - zoom) > 0.015)
                _hubDash041.ZoomFactor = zoom;
        }
        catch { }
    }

'@
    $hubText = $hubText.Insert($idx, $responsiveCode)
}

# Validacoes.
if ($mainText -notlike '*CurrentVersion = "1.0.51.0"*') { throw 'Versao 1.0.51 nao aplicada.' }
foreach ($m in @('GAT_RESPONSIVE_051','MinimumSize = new Size(820, 540)','ApplyDashZoom051()','zoom = Math.Max(0.62, Math.Min(1.15, zoom))','ArrangeDashTools051')) {
    if ($hubText -notlike "*$m*") { throw "Layout responsivo 1.0.51 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.51: janela e GAT DASH com escala responsiva por tamanho/resolucao.'
