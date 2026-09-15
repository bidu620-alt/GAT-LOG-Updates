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
if (-not $main -or -not $hub -or -not $truck -or -not $video) { throw 'Fonte 1.0.52 incompleto para aplicar 1.0.53.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# Versao 1.0.53 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.52.0"', 'CurrentVersion = "1.0.53.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.52"', 'Text = "Cliente 1.0.53"')
$mainText = $mainText.Replace('HUB 1.0.52:', 'HUB 1.0.53:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.52', 'GAT Telemetria BETA 1.0.53')
$hubText = $hubText.Replace('Cliente 1.0.52 TESTE', 'Cliente 1.0.53 TESTE')

# ---------------------------------------------------------------------------
# Persistencia de posicao/tamanho da sobreposicao do caminhao.
# ---------------------------------------------------------------------------
if ($truckText -notlike '*GAT_OVERLAY_BOUNDS_053*') {
    $locNeedle = '        Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 690), 70);'
    if (-not $truckText.Contains($locNeedle)) { throw 'Localizacao inicial do overlay do caminhao nao encontrada.' }
    $restoreCall = @'
        RestoreOverlayBounds053("truck-overlay-bounds-v1.txt");
'@.TrimEnd().Replace('\"','"')
    $truckText = $truckText.Replace($locNeedle, $locNeedle + "`r`n" + $restoreCall)

    $closeNeedle = '        FormClosed += delegate { _timer.Stop(); _timer.Dispose(); _http.Dispose(); };'
    if (-not $truckText.Contains($closeNeedle)) { throw 'Fechamento do overlay do caminhao nao encontrado.' }
    $saveCall = @'
        FormClosing += delegate { SaveOverlayBounds053("truck-overlay-bounds-v1.txt"); };
'@.TrimEnd().Replace('\"','"')
    $truckText = $truckText.Replace($closeNeedle, $saveCall + "`r`n" + $closeNeedle)

    $marker = '    private void BuildUi()'
    $idx = $truckText.IndexOf($marker)
    if ($idx -lt 0) { throw 'BuildUi do overlay do caminhao nao encontrado.' }
    $helper = @'
    // GAT_OVERLAY_BOUNDS_053
    private void RestoreOverlayBounds053(string fileName)
    {
        try
        {
            string path = Path.Combine(Application.LocalUserAppDataPath, fileName);
            if (!File.Exists(path)) return;
            string[] p = File.ReadAllText(path).Split('|');
            if (p.Length != 4) return;
            int x, y, w, h;
            if (!int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
            w = Math.Max(MinimumSize.Width, w);
            h = Math.Max(MinimumSize.Height, h);
            Rectangle wanted = new Rectangle(x, y, w, h);
            bool visible = false;
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle hit = Rectangle.Intersect(s.WorkingArea, wanted);
                if (hit.Width >= 80 && hit.Height >= 60) { visible = true; break; }
            }
            if (visible) Bounds = wanted;
        }
        catch { }
    }

    private void SaveOverlayBounds053(string fileName)
    {
        try
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width < MinimumSize.Width || b.Height < MinimumSize.Height) return;
            string path = Path.Combine(Application.LocalUserAppDataPath, fileName);
            File.WriteAllText(path, string.Join("|", b.X, b.Y, b.Width, b.Height));
        }
        catch { }
    }

'@
    $truckText = $truckText.Insert($idx, $helper)
}

# ---------------------------------------------------------------------------
# Persistencia de posicao/tamanho da sobreposicao de video.
# ---------------------------------------------------------------------------
if ($videoText -notlike '*GAT_VIDEO_BOUNDS_053*') {
    $locNeedle = '        Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 530), 80);'
    if (-not $videoText.Contains($locNeedle)) { throw 'Localizacao inicial do overlay de video nao encontrada.' }
    $videoText = $videoText.Replace($locNeedle, $locNeedle + "`r`n        RestoreVideoBounds053();")

    $closeNeedle = '        FormClosed += delegate { try { _web.Dispose(); _http.Dispose(); } catch { } };'
    if (-not $videoText.Contains($closeNeedle)) { throw 'Fechamento do overlay de video nao encontrado.' }
    $videoText = $videoText.Replace($closeNeedle, '        FormClosing += delegate { SaveVideoBounds053(); };' + "`r`n" + $closeNeedle)

    $marker = '    private async Task InitAsync()'
    $idx = $videoText.IndexOf($marker)
    if ($idx -lt 0) { throw 'InitAsync do overlay de video nao encontrado.' }
    $helper = @'
    // GAT_VIDEO_BOUNDS_053
    private void RestoreVideoBounds053()
    {
        try
        {
            string path = Path.Combine(Application.LocalUserAppDataPath, "video-overlay-bounds-v1.txt");
            if (!File.Exists(path)) return;
            string[] p = File.ReadAllText(path).Split('|');
            if (p.Length != 4) return;
            int x, y, w, h;
            if (!int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
            w = Math.Max(MinimumSize.Width, w);
            h = Math.Max(MinimumSize.Height, h);
            Rectangle wanted = new Rectangle(x, y, w, h);
            bool visible = false;
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle hit = Rectangle.Intersect(s.WorkingArea, wanted);
                if (hit.Width >= 80 && hit.Height >= 60) { visible = true; break; }
            }
            if (visible) Bounds = wanted;
        }
        catch { }
    }

    private void SaveVideoBounds053()
    {
        try
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width < MinimumSize.Width || b.Height < MinimumSize.Height) return;
            string path = Path.Combine(Application.LocalUserAppDataPath, "video-overlay-bounds-v1.txt");
            File.WriteAllText(path, string.Join("|", b.X, b.Y, b.Width, b.Height));
        }
        catch { }
    }

'@
    $videoText = $videoText.Insert($idx, $helper)
}

if ($mainText -notlike '*CurrentVersion = "1.0.53.0"*') { throw 'Versao 1.0.53 nao aplicada.' }
foreach ($m in @('GAT_OVERLAY_BOUNDS_053','RestoreOverlayBounds053','SaveOverlayBounds053')) {
    if ($truckText -notlike "*$m*") { throw "Overlay do caminhao 1.0.53 sem $m" }
}
foreach ($m in @('GAT_VIDEO_BOUNDS_053','RestoreVideoBounds053','SaveVideoBounds053')) {
    if ($videoText -notlike "*$m*") { throw "Overlay de video 1.0.53 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.53: posicao/tamanho dos overlays persistentes.'
