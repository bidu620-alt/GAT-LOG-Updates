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
