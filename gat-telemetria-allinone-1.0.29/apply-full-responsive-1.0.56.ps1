param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.55 incompleto para aplicar 1.0.56.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# ---------------------------------------------------------------------------
# 1.0.56 TESTE - foco exclusivo em layout responsivo do aplicativo inteiro.
# ---------------------------------------------------------------------------
$mainText = $mainText.Replace('CurrentVersion = "1.0.55.0"', 'CurrentVersion = "1.0.56.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.55"', 'Text = "Cliente 1.0.56"')
$mainText = $mainText.Replace('HUB 1.0.55:', 'HUB 1.0.56:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.55', 'GAT Telemetria BETA 1.0.56')
$hubText = $hubText.Replace('Cliente 1.0.55 TESTE', 'Cliente 1.0.56 TESTE')

# O minimo continua pequeno, mas todas as paginas passam a recalcular seu proprio
# conteudo antes de chegar ao limite. O scroll vira apenas uma rede de seguranca.
$hubText = $hubText.Replace('MinimumSize = new Size(720, 480);', 'MinimumSize = new Size(740, 500);')

# Reflow completo sempre que a janela muda.
$callNeedle = '            ApplyDashZoom051();'
if ($hubText.Contains($callNeedle) -and $hubText -notlike '*ApplyAllPages056();*') {
    $hubText = $hubText.Replace($callNeedle, "            ApplyAllPages056();`r`n" + $callNeedle)
}

# Ao trocar de aba, recalcula a aba recem exibida. Isso evita controles nascerem
# com largura zero/antiga enquanto estavam invisiveis.
$showNeedle = 'if (_hubPages041.ContainsKey(key)) _hubPages041[key].BringToFront();'
if ($hubText.Contains($showNeedle) -and $hubText -notlike '*LayoutPage056(key, _hubPages041[key])*') {
    $showNew = @'
if (_hubPages041.ContainsKey(key))
        {
            _hubPages041[key].BringToFront();
            try { LayoutPage056(key, _hubPages041[key]); } catch { }
        }
'@.TrimEnd()
    $hubText = $hubText.Replace($showNeedle, $showNew)
}

# Injeta o motor responsivo antes do cabecalho.
if ($hubText -notlike '*GAT_FULL_RESPONSIVE_056*') {
    $marker = '    private Control HubHeader041()'
    $idx = $hubText.IndexOf($marker)
    if ($idx -lt 0) { throw 'HubHeader041 nao encontrado para 1.0.56.' }

    $code = @'
    // GAT_FULL_RESPONSIVE_056
    private void ApplyAllPages056()
    {
        try
        {
            foreach (var pair056 in _hubPages041.ToList())
                LayoutPage056(pair056.Key, pair056.Value);
        }
        catch { }
    }

    private void LayoutPage056(string key056, Panel page056)
    {
        if (page056 == null || page056.IsDisposed) return;
        int w056 = Math.Max(1, page056.ClientSize.Width);
        int h056 = Math.Max(1, page056.ClientSize.Height);
        bool compact056 = w056 < 980 || h056 < 620;
        bool narrow056 = w056 < 820 || h056 < 540;

        // Cabecalho de cada pagina sempre acompanha a largura real.
        foreach (Label head056 in page056.Controls.OfType<Label>().Where(x => x.Top <= 8 && x.Height >= 45))
        {
            head056.Width = Math.Max(120, w056 - 6);
            head056.Font = new Font("Segoe UI Semibold", narrow056 ? 9.5f : (compact056 ? 10.5f : 12f), FontStyle.Bold);
        }

        if (string.Equals(key056, "home", StringComparison.OrdinalIgnoreCase))
        {
            // A Home moderna ja usa TableLayout/Dock. Apenas compacta a faixa superior
            // e os dados do perfil para resolucoes menores, sem quebrar o grid.
            page056.AutoScroll = false;
            var root056 = page056.Controls.OfType<TableLayoutPanel>().FirstOrDefault(x => x.Dock == DockStyle.Fill);
            if (root056 != null && root056.RowStyles.Count >= 2)
                root056.RowStyles[0].Height = narrow056 ? 158 : (compact056 ? 178 : 205);

            if (_homeAvatar045 != null)
            {
                int av056 = narrow056 ? 56 : (compact056 ? 70 : 86);
                _homeAvatar045.SetBounds(narrow056 ? 9 : 16, narrow056 ? 48 : 54, av056, av056);
            }
            if (_homeProfileName045 != null)
            {
                _homeProfileName045.Left = narrow056 ? 76 : (compact056 ? 98 : 116);
                _homeProfileName045.Top = narrow056 ? 48 : 55;
                _homeProfileName045.Width = Math.Max(90, (_homeProfileName045.Parent?.ClientSize.Width ?? 260) - _homeProfileName045.Left - 8);
                _homeProfileName045.Font = new Font("Segoe UI Semibold", narrow056 ? 10f : (compact056 ? 12f : 14f), FontStyle.Bold);
            }
            if (_homeProfileMeta045 != null)
            {
                _homeProfileMeta045.Left = narrow056 ? 76 : (compact056 ? 98 : 116);
                _homeProfileMeta045.Top = narrow056 ? 76 : 89;
                _homeProfileMeta045.Width = Math.Max(90, (_homeProfileMeta045.Parent?.ClientSize.Width ?? 280) - _homeProfileMeta045.Left - 8);
                _homeProfileMeta045.Height = narrow056 ? 70 : 92;
                _homeProfileMeta045.Font = new Font("Segoe UI", narrow056 ? 7.3f : (compact056 ? 8.2f : 9.2f));
            }
            if (_homeTrip045 != null) _homeTrip045.Font = new Font("Segoe UI Semibold", narrow056 ? 7.5f : (compact056 ? 8.5f : 9.7f));
            if (_homeSystem045 != null) _homeSystem045.Font = new Font("Segoe UI Semibold", narrow056 ? 7.5f : (compact056 ? 8.5f : 9.6f));
            if (_homeDrivers045 != null)
            {
                _homeDrivers045.ColumnHeadersHeight = narrow056 ? 26 : (compact056 ? 30 : 35);
                _homeDrivers045.RowTemplate.Height = narrow056 ? 28 : (compact056 ? 32 : 36);
                _homeDrivers045.DefaultCellStyle.Font = new Font("Segoe UI", narrow056 ? 7.2f : (compact056 ? 8f : 8.7f));
                _homeDrivers045.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", narrow056 ? 6.8f : (compact056 ? 7.5f : 8.2f), FontStyle.Bold);
            }
            return;
        }

        if (string.Equals(key056, "dash", StringComparison.OrdinalIgnoreCase))
        {
            // DASH nunca usa scroll do host. Ferramentas e WebView ocupam exatamente
            // o retangulo disponivel, independentemente do tamanho em que a pagina nasceu.
            page056.AutoScroll = false;
            page056.AutoScrollMinSize = Size.Empty;
            var tools056 = FindControl051<Panel>(page056, "dashTools051");
            if (tools056 != null)
            {
                int top056 = narrow056 ? 49 : 55;
                int toolH056 = narrow056 ? 49 : 55;
                tools056.SetBounds(0, top056, w056, toolH056);
                ArrangeDashTools051(tools056, compact056 || narrow056);
                if (_hubDash041 != null && !_hubDash041.IsDisposed)
                    _hubDash041.SetBounds(0, top056 + toolH056 + 8, w056, Math.Max(1, h056 - (top056 + toolH056 + 8)));
            }
            else if (_hubDash041 != null && !_hubDash041.IsDisposed)
            {
                _hubDash041.SetBounds(0, 112, w056, Math.Max(1, h056 - 112));
            }
            return;
        }

        if (string.Equals(key056, "radio", StringComparison.OrdinalIgnoreCase))
        {
            // Radio ocupa tudo abaixo do titulo. O formulario incorporado recebe o
            // tamanho real da pagina; se algum controle antigo nao couber, o proprio
            // formulario ganha rolagem apenas como fallback.
            page056.AutoScroll = false;
            int top056 = narrow056 ? 48 : 55;
            if (_hubRadioHost041 != null && !_hubRadioHost041.IsDisposed)
                _hubRadioHost041.SetBounds(0, top056, w056, Math.Max(1, h056 - top056));
            if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
            {
                _hubRadio041.AutoScroll = true;
                _hubRadio041.AutoScrollMinSize = new Size(Math.Min(720, Math.Max(560, w056 - 8)), Math.Min(520, Math.Max(390, h056 - top056 - 8)));
                _hubRadio041.Dock = DockStyle.Fill;
            }
            return;
        }

        // Nas demais paginas, usa scroll somente se o conteudo realmente precisar.
        int minH056 = 240;
        if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase)) minH056 = 390;
        else if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase)) minH056 = 330;
        else if (string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase)) minH056 = 190;
        else if (string.Equals(key056, "gps", StringComparison.OrdinalIgnoreCase)) minH056 = 150;
        page056.AutoScroll = h056 < minH056;
        page056.AutoScrollMinSize = page056.AutoScroll ? new Size(0, minH056) : Size.Empty;

        if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase) && _hubServerCard041 != null)
        {
            _hubServerCard041.Left = 0;
            _hubServerCard041.Top = narrow056 ? 52 : 65;
            _hubServerCard041.Width = Math.Max(220, w056 - (page056.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
        }

        if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase))
        {
            if (_hubAccountCard041 != null)
            {
                _hubAccountCard041.Left = 0;
                _hubAccountCard041.Top = narrow056 ? 52 : 65;
                _hubAccountCard041.Width = Math.Max(220, w056 - (page056.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth + 4 : 0));
            }
            foreach (Label l056 in page056.Controls.OfType<Label>().Where(x => x.Top > 120))
            {
                l056.Width = Math.Max(200, w056 - 12);
                l056.Font = new Font("Segoe UI", narrow056 ? 8f : 9f);
            }
        }

        if (string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Button b056 in page056.Controls.OfType<Button>())
            {
                b056.Left = 0;
                b056.Top = narrow056 ? 82 : 100;
                b056.Width = Math.Min(260, Math.Max(180, w056 - 12));
                b056.Height = narrow056 ? 32 : 36;
            }
        }
    }

'@
    $hubText = $hubText.Insert($idx, $code)
}

# Garante que o evento Resize chama o novo motor mesmo se patches antigos forem alterados.
if ($hubText -notlike '*Resize += delegate { ApplyResponsive051(); }*') {
    throw 'Evento Resize responsivo nao encontrado antes da 1.0.56.'
}

# O titulo visual deve refletir a versao de teste atual.
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+";', 'Text = "GAT Telemetria BETA 1.0.56";', 1)

foreach ($m in @('CurrentVersion = "1.0.56.0"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.56 sem $m" }
}
foreach ($m in @('GAT_FULL_RESPONSIVE_056','ApplyAllPages056()','LayoutPage056(','MinimumSize = new Size(740, 500)','_hubRadio041.Dock = DockStyle.Fill','_hubServerCard041.Width','_hubAccountCard041.Width')) {
    if ($hubText -notlike "*$m*") { throw "Layout completo 1.0.56 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.56: layout responsivo completo aplicado em todas as abas.'
