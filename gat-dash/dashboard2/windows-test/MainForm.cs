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

namespace RoadLife.Dashboard2.Test
{
    internal sealed class MainForm : Form
    {
        private readonly WebView2 _web = new WebView2();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private readonly Timer _timer = new Timer { Interval = 350 };
        private readonly string _settingsPath;
        private bool _polling;
        private bool _ready;
        private string _layout = "vertical";
        private Point _verticalPos = new Point(20, 120);
        private Point _horizontalPos = new Point(350, 700);
        private Size _verticalSize = new Size(350, 720);
        private Size _horizontalSize = new Size(980, 175);

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public MainForm()
        {
            Text = "RoadLife ETS2 • Dashboard2 Overlay TESTE 0.2.0";
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(3, 5, 4);
            MinimumSize = new Size(300, 135);
            StartPosition = FormStartPosition.Manual;

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoadLife ETS2", "Dashboard2", "overlay-window.json");

            LoadWindowState();
            ApplyLayout(_layout, false);

            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);

            Shown += async delegate { await InitAsync(); };
            _timer.Tick += async delegate { await PollTelemetryAsync(); };
            FormClosing += delegate { SaveWindowState(); _timer.Stop(); };
            FormClosed += delegate { _timer.Dispose(); _http.Dispose(); _web.Dispose(); };
        }

        private async Task InitAsync()
        {
            try
            {
                string data = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RoadLife ETS2", "Dashboard2", "WebView2");

                Directory.CreateDirectory(data);
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await _web.EnsureCoreWebView2Async(env);

                _web.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _web.CoreWebView2.WebMessageReceived += OnWebMessage;

                string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                if (!Directory.Exists(www))
                    throw new DirectoryNotFoundException("Pasta www do Dashboard2 não encontrada.");

                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "dashboard2.roadlife.local",
                    www,
                    CoreWebView2HostResourceAccessKind.Allow);

                _web.Source = new Uri("https://dashboard2.roadlife.local/index.html");
                _ready = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível iniciar o Dashboard2.\r\n\r\n" + ex.Message +
                    "\r\n\r\nConfirme se o Microsoft Edge WebView2 Runtime está instalado.",
                    "RoadLife Dashboard2 Overlay TESTE",
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
                else if (type == "layout")
                {
                    string layout = ((string)j["layout"] ?? "vertical").ToLowerInvariant();
                    BeginInvoke((Action)(() => ApplyLayout(layout, true)));
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

        private void ApplyLayout(string layout, bool saveCurrent)
        {
            if (saveCurrent) RememberCurrentBounds();

            _layout = layout == "horizontal" ? "horizontal" : "vertical";

            if (_layout == "horizontal")
            {
                Size = _horizontalSize;
                Location = ClampToScreens(_horizontalPos, Size);
            }
            else
            {
                Size = _verticalSize;
                Location = ClampToScreens(_verticalPos, Size);
            }

            SaveWindowState();
        }

        private static Point ClampToScreens(Point wanted, Size size)
        {
            Rectangle area = Screen.FromPoint(wanted).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(wanted.X, area.Right - Math.Min(size.Width, area.Width)));
            int y = Math.Max(area.Top, Math.Min(wanted.Y, area.Bottom - Math.Min(size.Height, area.Height)));
            return new Point(x, y);
        }

        private void RememberCurrentBounds()
        {
            if (WindowState != FormWindowState.Normal) return;

            if (_layout == "horizontal")
            {
                _horizontalPos = Location;
                _horizontalSize = Size;
            }
            else
            {
                _verticalPos = Location;
                _verticalSize = Size;
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

                await JsAsync(
                    "window.dashboard2PushTelemetry(" +
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
                _layout = (string)j["layout"] ?? "vertical";
                _verticalPos = ReadPoint(j["vertical"], _verticalPos);
                _horizontalPos = ReadPoint(j["horizontal"], _horizontalPos);
                _verticalSize = ReadSize(j["vertical"], _verticalSize);
                _horizontalSize = ReadSize(j["horizontal"], _horizontalSize);
            }
            catch { }
        }

        private static Point ReadPoint(JToken t, Point fallback)
        {
            if (t == null) return fallback;
            return new Point(
                (int?)t["x"] ?? fallback.X,
                (int?)t["y"] ?? fallback.Y);
        }

        private static Size ReadSize(JToken t, Size fallback)
        {
            if (t == null) return fallback;
            return new Size(
                Math.Max(300, (int?)t["width"] ?? fallback.Width),
                Math.Max(135, (int?)t["height"] ?? fallback.Height));
        }

        private void SaveWindowState()
        {
            try
            {
                RememberCurrentBounds();
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));

                var j = new JObject
                {
                    ["layout"] = _layout,
                    ["vertical"] = new JObject
                    {
                        ["x"] = _verticalPos.X,
                        ["y"] = _verticalPos.Y,
                        ["width"] = _verticalSize.Width,
                        ["height"] = _verticalSize.Height
                    },
                    ["horizontal"] = new JObject
                    {
                        ["x"] = _horizontalPos.X,
                        ["y"] = _horizontalPos.Y,
                        ["width"] = _horizontalSize.Width,
                        ["height"] = _horizontalSize.Height
                    }
                };

                File.WriteAllText(_settingsPath, j.ToString(Formatting.Indented));
            }
            catch { }
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