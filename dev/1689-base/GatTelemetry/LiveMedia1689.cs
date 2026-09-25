using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class LiveMediaForm1689 : Form
{
    private const string VirtualHost = "gatlive.gatlogets2.local";
    private readonly WebView2 _web = new WebView2();
    private readonly string _mode;
    private readonly string _kind;
    private readonly string _token;
    private readonly string _user;
    private readonly string _owner;
    private readonly string _streamId;
    private readonly string _quality;
    private readonly bool _silentWindow;
    private bool _ready;
    private bool _configured;

    internal event Action<string, JObject> LiveMessage1689;

    internal LiveMediaForm1689(
        string mode,
        string kind,
        string token,
        string user,
        string owner = "",
        string streamId = "",
        string quality = "normal",
        bool silentWindow = false)
    {
        _mode = mode ?? "";
        _kind = kind ?? "";
        _token = token ?? "";
        _user = user ?? "";
        _owner = owner ?? "";
        _streamId = streamId ?? "";
        _quality = quality ?? "normal";
        _silentWindow = silentWindow;

        Text = _kind == "radio_mic" ? "GAT Rádio • Locução ao vivo" : (_mode == "viewer" ? "GAT • Assistir rota" : "GAT • Transmitir rota");
        BackColor = Color.FromArgb(2, 9, 20);
        ForeColor = Color.White;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 300);
        Size = new Size(780, 500);
        TopMost = _mode == "viewer" && _kind == "route";
        ShowInTaskbar = !_silentWindow;

        if (_silentWindow)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            MinimumSize = new Size(1, 1);
            Size = new Size(2, 2);
            Opacity = 0.01;
            ShowInTaskbar = false;
        }
        else if (_mode == "viewer")
        {
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
        }

        _web.Dock = DockStyle.Fill;
        _web.BackColor = Color.Black;
        Controls.Add(_web);

        Shown += async delegate { await Initialize1689Async(); };
    }

    private async Task Initialize1689Async()
    {
        if (_ready || IsDisposed) return;
        try
        {
            string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transmissao");
            string page = Path.Combine(assets, "live.html");
            if (!File.Exists(page)) throw new FileNotFoundException("Módulo de transmissão não encontrado.", page);

            string data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GAT-LOG", "GAT-Telemetria", "LiveWebView2-1.0.68.9");
            Directory.CreateDirectory(data);

            var options = new CoreWebView2EnvironmentOptions();
            options.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required --enable-features=WebRtcHideLocalIpsWithMdns";
            var env = await CoreWebView2Environment.CreateAsync(null, data, options);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, assets, CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs e)
            {
                try
                {
                    if ((e.Uri ?? "").StartsWith("https://" + VirtualHost + "/", StringComparison.OrdinalIgnoreCase))
                        e.State = CoreWebView2PermissionState.Allow;
                }
                catch { }
            };
            _web.CoreWebView2.WebMessageReceived += WebMessage1689;
            _web.CoreWebView2.NavigationCompleted += async delegate
            {
                _ready = true;
                await Configure1689Async();
            };
            _web.Source = new Uri("https://" + VirtualHost + "/live.html?v=1689");
        }
        catch (Exception ex)
        {
            LiveMessage1689?.Invoke("error", new JObject { ["message"] = ex.Message });
            if (!_silentWindow)
                MessageBox.Show(this, "Não foi possível abrir a transmissão.\r\n\r\n" + ex.Message, "GAT Ao Vivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task Configure1689Async()
    {
        if (!_ready || _configured || _web.CoreWebView2 == null) return;
        _configured = true;
        var cfg = new JObject
        {
            ["mode"] = _mode,
            ["kind"] = _kind,
            ["token"] = _token,
            ["user"] = _user,
            ["owner"] = _owner,
            ["streamId"] = _streamId,
            ["quality"] = _quality
        };
        string js = "window.gatConfigure(" + cfg.ToString(Formatting.None) + ")";
        try { await _web.CoreWebView2.ExecuteScriptAsync(js); }
        catch (Exception ex) { LiveMessage1689?.Invoke("error", new JObject { ["message"] = ex.Message }); }
    }

    private void WebMessage1689(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            JObject msg = JObject.Parse(e.WebMessageAsJson);
            string type = Convert.ToString(msg["type"]) ?? "";
            LiveMessage1689?.Invoke(type, msg);
        }
        catch { }
    }

    internal async Task Stop1689Async()
    {
        try
        {
            if (_web.CoreWebView2 != null)
                await _web.CoreWebView2.ExecuteScriptAsync("if(window.stopBroadcast){window.stopBroadcast();}");
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _web.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }
}
