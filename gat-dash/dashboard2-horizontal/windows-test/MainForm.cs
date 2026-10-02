using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RoadLife.DashboardHorizontal.Test
{
    internal sealed class MainForm : Form
    {
        private readonly WebView2 _web = new WebView2();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private readonly Timer _timer = new Timer { Interval = 350 };
        private readonly string _settingsPath;
        private bool _polling;
        private bool _ready;
        private Point _savedPos = new Point(260, 760);
        private Size _savedSize = new Size(940, 190);

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public MainForm()
        {
            Text = "RoadLife ETS2 • Dashboard Horizontal TESTE 0.5.0";
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(3, 6, 5);
            MinimumSize = new Size(760, 154);
            StartPosition = FormStartPosition.Manual;

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoadLife ETS2", "Dashboard Horizontal", "overlay-window.json");

            LoadWindowState();
            Size = _savedSize;
            Location = ClampToScreens(_savedPos, Size);

            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);

            Shown += async delegate { await InitAsync(); };
            _timer.Tick += async delegate { await PollTelemetryAsync(); };
            Move += delegate { SaveWindowState(); };
            ResizeEnd += delegate { SaveWindowState(); };
            FormClosing += delegate { SaveWindowState(); _timer.Stop(); };
            FormClosed += delegate { _timer.Dispose(); _http.Dispose(); _web.Dispose(); };
        }

        private async Task InitAsync()
        {
            try
            {
                string data = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RoadLife ETS2", "Dashboard Horizontal", "WebView2");
                Directory.CreateDirectory(data);

                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await _web.EnsureCoreWebView2Async(env);

                _web.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _web.CoreWebView2.WebMessageReceived += OnWebMessage;

                string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                if (!Directory.Exists(www))
                    throw new DirectoryNotFoundException("Pasta www do Dashboard Horizontal não encontrada.");

                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "horizontal.roadlife.local",
                    www,
                    CoreWebView2HostResourceAccessKind.Allow);

                _web.Source = new Uri("https://horizontal.roadlife.local/index.html");
                _ready = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível iniciar o Dashboard Horizontal.\r\n\r\n" + ex.Message +
                    "\r\n\r\nConfirme se o Microsoft Edge WebView2 Runtime está instalado.",
                    "RoadLife Dashboard Horizontal TESTE",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var j = JObject.Parse(e.WebMessageAsJson);
                string type = (string)j["type"] ?? "";

                if (type == "drag")
                {
                    BeginInvoke((Action)(() =>
                    {
                        ReleaseCapture();
                        SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                    }));
                }
                else if (type == "minimize")
                {
                    BeginInvoke((Action)(() => WindowState = FormWindowState.Minimized));
                }
                else if (type == "close")
                {
                    BeginInvoke((Action)Close);
                }
            }
            catch { }
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

                await JsAsync(
                    "window.dashboardHorizontalPushTelemetry(" +
                    JsonConvert.SerializeObject(json) +
                    ")");
            }
            catch { }
            finally { _polling = false; }
        }

        private async Task JsAsync(string script)
        {
            if (!_ready || _web.CoreWebView2 == null) return;
            try { await _web.CoreWebView2.ExecuteScriptAsync(script); }
            catch { }
        }

        private void LoadWindowState()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var j = JObject.Parse(File.ReadAllText(_settingsPath));
                _savedPos = new Point(
                    (int?)j["x"] ?? _savedPos.X,
                    (int?)j["y"] ?? _savedPos.Y);
                _savedSize = new Size(
                    Math.Max(760, (int?)j["width"] ?? _savedSize.Width),
                    Math.Max(154, (int?)j["height"] ?? _savedSize.Height));
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                if (WindowState != FormWindowState.Normal) return;
                _savedPos = Location;
                _savedSize = Size;
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
                var j = new JObject
                {
                    ["x"] = _savedPos.X,
                    ["y"] = _savedPos.Y,
                    ["width"] = _savedSize.Width,
                    ["height"] = _savedSize.Height
                };
                File.WriteAllText(_settingsPath, j.ToString(Formatting.Indented));
            }
            catch { }
        }

        private static Point ClampToScreens(Point wanted, Size size)
        {
            Rectangle area = Screen.FromPoint(wanted).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(wanted.X, area.Right - Math.Min(size.Width, area.Width)));
            int y = Math.Max(area.Top, Math.Min(wanted.Y, area.Bottom - Math.Min(size.Height, area.Height)));
            return new Point(x, y);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);

                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point(
                    unchecked((short)(lp & 0xffff)),
                    unchecked((short)((lp >> 16) & 0xffff))));

                const int grip = 6;
                bool left = p.X <= grip;
                bool right = p.X >= ClientSize.Width - grip;
                bool top = p.Y <= grip;
                bool bottom = p.Y >= ClientSize.Height - grip;

                if (left && top) { m.Result = (IntPtr)13; return; }
                if (right && top) { m.Result = (IntPtr)14; return; }
                if (left && bottom) { m.Result = (IntPtr)16; return; }
                if (right && bottom) { m.Result = (IntPtr)17; return; }
                if (left) { m.Result = (IntPtr)10; return; }
                if (right) { m.Result = (IntPtr)11; return; }
                if (top) { m.Result = (IntPtr)12; return; }
                if (bottom) { m.Result = (IntPtr)15; return; }
                return;
            }
            base.WndProc(ref m);
        }
    }
}