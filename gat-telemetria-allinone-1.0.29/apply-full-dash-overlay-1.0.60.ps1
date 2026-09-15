param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub) { throw 'Fonte 1.0.59 incompleto para aplicar 1.0.60.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw

# ---------------------------------------------------------------------------
# 1.0.60 TESTE: muda a estrategia do GAT DASH.
# O DASH completo deixa de depender do espaco interno da aba e passa a abrir
# como janela independente com uma tela logica fixa de 1600x700. A janela real
# pode ter qualquer tamanho; WebView + ZoomFactor escalam juntos, portanto o
# navegador continua enxergando exatamente 1600x700 CSS e a proporcao nunca muda.
# ---------------------------------------------------------------------------
$mainText = $mainText.Replace('CurrentVersion = "1.0.59.0"', 'CurrentVersion = "1.0.60.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.59"', 'Text = "Cliente 1.0.60"')
$mainText = $mainText.Replace('HUB 1.0.59:', 'HUB 1.0.60:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.\d+"', 'Text = "GAT Telemetria BETA 1.0.60"', 1)
$hubText = [regex]::Replace($hubText, 'Cliente 1\.0\.\d+ TESTE', 'Cliente 1.0.60 TESTE')

# Campo da nova janela.
$fieldNeedle = '    private VideoOverlay041 _videoOverlay041;'
if ($hubText.Contains($fieldNeedle) -and $hubText -notlike '*private DashOverlay060 _dashOverlay060;*') {
    $hubText = $hubText.Replace($fieldNeedle, $fieldNeedle + "`r`n    private DashOverlay060 _dashOverlay060;")
}

# Fecha a janela completa quando o Telemetria realmente for encerrado.
$hubText = $hubText.Replace('try { _truckOverlay041?.Close(); _videoOverlay041?.Close(); } catch { }', 'try { _truckOverlay041?.Close(); _videoOverlay041?.Close(); _dashOverlay060?.Close(); } catch { }')

# ---------------------------------------------------------------------------
# Substitui SOMENTE a pagina GAT DASH. Nao mexe na Home nem nas demais abas.
# A aba passa a ser um launcher estavel; o DASH real abre na janela escalavel.
# ---------------------------------------------------------------------------
$dashPattern = '(?s)    private Panel Dash041\(\)\s*\{.*?\r?\n    \}\s*\r?\n    private Panel Radio041\(\)'
if (-not [regex]::IsMatch($hubText, $dashPattern)) { throw 'Metodo Dash041 nao encontrado para 1.0.60.' }
$dashReplacement = @'
    private Panel Dash041()
    {
        var p = Page041();
        p.Controls.Add(Head041("GAT DASH", "Dashboard completo em janela independente, com proporção fixa em qualquer resolução."));

        var tools = new Panel
        {
            Name = "dashTools060",
            Left = 0, Top = 55, Height = 58, Width = p.Width,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };

        var full = HubButton041("ABRIR DASH COMPLETO", 190);
        full.Top = 10;
        full.Click += async delegate { await OpenDashOverlay060(); };
        tools.Controls.Add(full);

        var video = HubButton041("SOBREPOR VÍDEO", 145);
        video.Top = 10;
        video.Click += delegate { OpenVideoOverlay041(); };
        tools.Controls.Add(video);

        var truck = HubButton041("SOBREPOR CAMINHÃO", 175);
        truck.Top = 10;
        truck.Click += delegate { OpenTruckOverlay041(); };
        tools.Controls.Add(truck);

        var opacityLabel = new Label
        {
            Text = "Transparência",
            Top = 20, Width = 88,
            ForeColor = Color.FromArgb(155, 179, 207)
        };
        tools.Controls.Add(opacityLabel);

        _overlayOpacity041 = new TrackBar
        {
            Top = 7, Width = 180,
            Minimum = 55, Maximum = 100, Value = 94, TickFrequency = 5
        };
        _overlayOpacity041.Scroll += delegate { ApplyOverlayOpacity041(); };
        tools.Controls.Add(_overlayOpacity041);

        Action arrange = delegate
        {
            int w = Math.Max(1, tools.ClientSize.Width);
            int gap = w < 900 ? 6 : 9;
            int x = 8;
            int fullW = w < 900 ? 150 : 190;
            int videoW = w < 900 ? 112 : 145;
            int truckW = w < 900 ? 136 : 175;
            full.Left = x; full.Width = fullW; x += fullW + gap;
            video.Left = x; video.Width = videoW; x += videoW + gap;
            truck.Left = x; truck.Width = truckW; x += truckW + gap;
            opacityLabel.Visible = w >= 760;
            if (opacityLabel.Visible) { opacityLabel.Left = x + 4; x += 96; }
            _overlayOpacity041.Left = x;
            _overlayOpacity041.Width = Math.Max(90, w - x - 8);
            float fs = w < 900 ? 6.8f : 8.0f;
            full.Font = video.Font = truck.Font = new Font("Segoe UI Semibold", fs, FontStyle.Bold);
        };
        tools.Resize += delegate { arrange(); };
        p.Controls.Add(tools);

        var info = new Panel
        {
            Left = 0, Top = 125, Width = p.Width,
            Height = Math.Max(260, p.Height - 125),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(3, 13, 25)
        };
        var title = new Label
        {
            Text = "GAT DASH COMPLETO",
            AutoSize = true, Left = 28, Top = 32,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold)
        };
        var desc = new Label
        {
            Text = "A tela completa agora abre separada do GAT Telemetria.\r\n" +
                   "Ao redimensionar, TODO o dashboard diminui ou aumenta junto, sem mudar a proporção.\r\n" +
                   "Se a janela ficar em outro formato, aparecem apenas margens escuras — nada é cortado ou espremido.",
            Left = 30, Top = 82, Width = 820, Height = 84,
            ForeColor = Color.FromArgb(167, 199, 230),
            Font = new Font("Segoe UI", 10.5f)
        };
        var open = HubButton041("ABRIR / SOBREPOR DASH COMPLETO", 285);
        open.Left = 30; open.Top = 182; open.Height = 42;
        open.Click += async delegate { await OpenDashOverlay060(); };
        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open);
        p.Controls.Add(info);

        arrange();
        return p;
    }

    private Panel Radio041()
'@
$hubText = [regex]::Replace($hubText, $dashPattern, $dashReplacement, 1)

# ---------------------------------------------------------------------------
# Direciona a telemetria/Radio/controles nativos para a janela completa.
# ---------------------------------------------------------------------------
$hubText = $hubText.Replace('if (!_hubDashReady041) return;', 'if (!DashTargetReady060()) return;')
$hubText = $hubText.Replace('if (!_hubDashReady041 || _hubDashBusy041 || _hubDash041?.CoreWebView2 == null) return; _hubDashBusy041 = true;', 'if (!DashTargetReady060() || _hubDashBusy041) return; _hubDashBusy041 = true;')

$oldJs = '    private async Task DashJs041(string js) { try { if (_hubDashReady041 && _hubDash041?.CoreWebView2 != null) await _hubDash041.CoreWebView2.ExecuteScriptAsync(js); } catch { } }'
$newJs = @'
    private async Task DashJs041(string js)
    {
        try
        {
            if (_hubDashReady041 && _hubDash041?.CoreWebView2 != null)
                await _hubDash041.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch { }
        try
        {
            if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady)
                await _dashOverlay060.ExecuteScriptAsync(js);
        }
        catch { }
    }
'@.TrimEnd()
if ($hubText.Contains($oldJs)) { $hubText = $hubText.Replace($oldJs, $newJs) }
elseif ($hubText -notlike '*_dashOverlay060.ExecuteScriptAsync(js)*') { throw 'DashJs041 nao encontrado.' }

$methodMarker = '    private void OpenTruckOverlay041()'
$methodIndex = $hubText.IndexOf($methodMarker)
if ($methodIndex -lt 0) { throw 'OpenTruckOverlay041 nao encontrado.' }
if ($hubText -notlike '*private async Task OpenDashOverlay060()*') {
$helpers = @'
    private bool DashTargetReady060()
    {
        bool embedded = _hubDashReady041 && _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
        bool full = _dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady;
        return embedded || full;
    }

    private async Task OpenDashOverlay060()
    {
        try
        {
            if (_dashOverlay060 == null || _dashOverlay060.IsDisposed)
            {
                _dashOverlay060 = new DashOverlay060(DashMessage041);
                _dashOverlay060.FormClosed += delegate
                {
                    _dashOverlay060 = null;
                    _hubDashReady041 = _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
                    if (!_hubDashReady041) _hubDashTimer041.Stop();
                };
            }

            if (!_dashOverlay060.Visible) _dashOverlay060.Show();
            await _dashOverlay060.InitializeAsync();
            _hubDashReady041 = true;
            _hubDashTimer041.Start();
            ApplyOverlayOpacity041();
            _dashOverlay060.BringToFront();
            await DashSession041();
            await PollDash041();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir o GAT DASH completo.\r\n" + ex.Message, "GAT DASH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

'@
    $hubText = $hubText.Insert($methodIndex, $helpers)
}

# Transparencia passa a valer tambem para o DASH completo.
$opacityOld = '    private void ApplyOverlayOpacity041() { double o = (_overlayOpacity041 == null ? 94 : _overlayOpacity041.Value) / 100.0; try { if (_truckOverlay041 != null && !_truckOverlay041.IsDisposed) _truckOverlay041.Opacity = o; } catch { } try { if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed) _videoOverlay041.Opacity = o; } catch { } }'
$opacityNew = '    private void ApplyOverlayOpacity041() { double o = (_overlayOpacity041 == null ? 94 : _overlayOpacity041.Value) / 100.0; try { if (_truckOverlay041 != null && !_truckOverlay041.IsDisposed) _truckOverlay041.Opacity = o; } catch { } try { if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed) _videoOverlay041.Opacity = o; } catch { } try { if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed) _dashOverlay060.Opacity = o; } catch { } }'
if ($hubText.Contains($opacityOld)) { $hubText = $hubText.Replace($opacityOld, $opacityNew) }

# ---------------------------------------------------------------------------
# Nova janela: viewport CSS logico SEMPRE 1600x700.
# WebView fisico = 1600x700 * escala; ZoomFactor = mesma escala.
# Logo: viewport CSS = fisico / zoom = 1600x700 em qualquer tamanho.
# ---------------------------------------------------------------------------
$srcDir = Split-Path $hub.FullName -Parent
$overlayPath = Join-Path $srcDir 'DashOverlay060.cs'
$overlaySource = @'
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GatTelemetry;

internal sealed class DashOverlay060 : Form
{
    private const int DesignWidth = 1600;
    private const int DesignHeight = 700;
    private readonly WebView2 _web;
    private readonly EventHandler<CoreWebView2WebMessageReceivedEventArgs> _messageHandler;
    private bool _initializing;

    public bool IsReady { get; private set; }

    public DashOverlay060(EventHandler<CoreWebView2WebMessageReceivedEventArgs> messageHandler)
    {
        _messageHandler = messageHandler;
        Text = "GAT DASH • Completo";
        BackColor = Color.FromArgb(2, 10, 20);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        ShowInTaskbar = true;
        TopMost = true;
        MinimumSize = new Size(660, 350);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 560);

        _web = new WebView2
        {
            BackColor = Color.FromArgb(2, 10, 20)
        };
        Controls.Add(_web);

        Resize += delegate { LayoutDashboard(); };
        ResizeEnd += delegate { SaveWindowState(); };
        FormClosing += delegate { SaveWindowState(); };
        LoadWindowState();
        LayoutDashboard();
    }

    public async Task InitializeAsync()
    {
        if (IsReady || _initializing) return;
        _initializing = true;
        try
        {
            string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gatdash-www");
            if (!File.Exists(Path.Combine(www, "index.html")))
                throw new FileNotFoundException("Arquivos do GAT DASH não foram encontrados.");

            string data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GAT-LOG", "GAT-Telemetria", "DashFullWebView2-1.0.60");
            Directory.CreateDirectory(data);

            var env = await CoreWebView2Environment.CreateAsync(null, data);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "gatdash-full.local", www, CoreWebView2HostResourceAccessKind.Allow);
            if (_messageHandler != null)
                _web.CoreWebView2.WebMessageReceived += _messageHandler;

            IsReady = true;
            LayoutDashboard();
            _web.Source = new Uri("https://gatdash-full.local/index.html?full=1060");
        }
        finally
        {
            _initializing = false;
        }
    }

    public async Task ExecuteScriptAsync(string js)
    {
        if (!IsReady || _web?.CoreWebView2 == null || string.IsNullOrWhiteSpace(js)) return;
        await _web.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void LayoutDashboard()
    {
        if (WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        double sx = ClientSize.Width / (double)DesignWidth;
        double sy = ClientSize.Height / (double)DesignHeight;
        double scale = Math.Min(sx, sy);
        scale = Math.Max(0.25, Math.Min(2.0, scale));

        int width = Math.Max(1, (int)Math.Round(DesignWidth * scale));
        int height = Math.Max(1, (int)Math.Round(DesignHeight * scale));
        int left = Math.Max(0, (ClientSize.Width - width) / 2);
        int top = Math.Max(0, (ClientSize.Height - height) / 2);

        _web.Bounds = new Rectangle(left, top, width, height);
        try
        {
            if (Math.Abs(_web.ZoomFactor - scale) > 0.002)
                _web.ZoomFactor = scale;
        }
        catch { }
    }

    private string StatePath
    {
        get { return Path.Combine(Application.LocalUserAppDataPath, "dash-full-overlay-1.0.60.txt"); }
    }

    private void SaveWindowState()
    {
        try
        {
            if (WindowState != FormWindowState.Normal) return;
            var b = Bounds;
            File.WriteAllText(StatePath, string.Join("|", b.X, b.Y, b.Width, b.Height));
        }
        catch { }
    }

    private void LoadWindowState()
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            var p = File.ReadAllText(StatePath).Split('|');
            if (p.Length != 4) return;
            int x, y, w, h;
            if (!int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
            w = Math.Max(MinimumSize.Width, w);
            h = Math.Max(MinimumSize.Height, h);
            var rect = new Rectangle(x, y, w, h);
            bool visible = false;
            foreach (var s in Screen.AllScreens)
            {
                if (Rectangle.Intersect(rect, s.WorkingArea).Width >= 120 && Rectangle.Intersect(rect, s.WorkingArea).Height >= 80)
                {
                    visible = true;
                    break;
                }
            }
            if (!visible) return;
            StartPosition = FormStartPosition.Manual;
            Bounds = rect;
        }
        catch { }
    }
}
'@
Set-Content $overlayPath $overlaySource -Encoding UTF8

# Validacoes de fonte.
if ($mainText -notlike '*CurrentVersion = "1.0.60.0"*') { throw 'Versao 1.0.60 nao aplicada.' }
foreach ($m in @('ABRIR DASH COMPLETO','OpenDashOverlay060','DashTargetReady060','DashOverlay060','_dashOverlay060.ExecuteScriptAsync(js)')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.60 sem $m" }
}
foreach ($m in @('DesignWidth = 1600','DesignHeight = 700','double scale = Math.Min(sx, sy)','_web.ZoomFactor = scale','dash-full-overlay-1.0.60.txt')) {
    if ($overlaySource -notlike "*$m*") { throw "Overlay completo 1.0.60 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.60: GAT DASH completo em janela independente com escala uniforme 1600x700 e proporcao preservada.'
