using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;

namespace RoadLife.Dashboard2.Test
{
    internal sealed class MainForm : Form
    {
        private readonly WebView2 _web = new WebView2();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private readonly Timer _timer = new Timer { Interval = 350 };
        private bool _polling;
        private bool _ready;

        public MainForm()
        {
            Text = "RoadLife ETS2 • Dashboard2 TESTE 0.1.0";
            MinimumSize = new Size(520, 720);
            Size = new Size(1450, 850);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(7, 10, 8);
            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);
            Shown += async delegate { await InitAsync(); };
            _timer.Tick += async delegate { await PollTelemetryAsync(); };
            FormClosing += delegate { _timer.Stop(); };
            FormClosed += delegate { _timer.Dispose(); _http.Dispose(); _web.Dispose(); };
        }

        private async Task InitAsync()
        {
            try
            {
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RoadLife ETS2", "Dashboard2", "WebView2");
                Directory.CreateDirectory(data);
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await _web.EnsureCoreWebView2Async(env);
                _web.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _web.CoreWebView2.Settings.IsZoomControlEnabled = true;

                string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                if (!Directory.Exists(www))
                    throw new DirectoryNotFoundException("Pasta www do Dashboard2 não encontrada.");

                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "dashboard2.roadlife.local", www, CoreWebView2HostResourceAccessKind.Allow);
                _web.Source = new Uri("https://dashboard2.roadlife.local/index.html");
                _ready = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Não foi possível iniciar o Dashboard2.\r\n\r\n" + ex.Message +
                    "\r\n\r\nConfirme se o Microsoft Edge WebView2 Runtime está instalado.",
                    "RoadLife Dashboard2 TESTE", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task PollTelemetryAsync()
        {
            if (!_ready || _polling || _web.CoreWebView2 == null) return;
            _polling = true;
            try
            {
                string json = await _http.GetStringAsync(
                    "http://127.0.0.1:31377/api/ets2/telemetry?t=" +
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                await JsAsync("window.dashboard2PushTelemetry(" +
                    JsonConvert.SerializeObject(json) + ")");
            }
            catch { }
            finally { _polling = false; }
        }

        private async Task JsAsync(string script)
        {
            if (!_ready || _web.CoreWebView2 == null) return;
            try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
        }
    }
}
