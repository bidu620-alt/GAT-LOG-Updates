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

namespace RoadLife.Dashboard
{
    internal sealed class MainForm : Form
    {
        private const double Aspect = 1000.0 / 205.0;
        private readonly WebView2 _web = new WebView2();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private readonly Timer _timer = new Timer { Interval = 350 };
        private readonly string _settingsPath;
        private bool _polling;
        private bool _ready;
        private bool _applyingAspect;
        private Point _savedPos = new Point(250, 720);
        private Size _savedSize = new Size(1000, 205);

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_SIZING = 0x0214;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public MainForm()
        {
            Text = "RoadLife ETS2 Dashboard 0.1.0";
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(2, 4, 3);
            MinimumSize = new Size(760, 156);
            MaximumSize = new Size(1800, 369);

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoadLife ETS2", "RoadLife Dashboard", "settings.json");

            LoadWindowState();
            _savedSize = NormalizeSize(_savedSize);
            Size = _savedSize;
            Location = ClampToScreens(_savedPos, Size);

            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);

            Shown += async delegate { await InitAsync(); };
            ResizeEnd += delegate { SnapAspectAndSave(); };
            Move += delegate { SaveWindowState(); };
            FormClosing += delegate { SaveWindowState(); _timer.Stop(); };
            FormClosed += delegate { _timer.Dispose(); _http.Dispose(); _web.Dispose(); };
        }

        private async Task InitAsync()
        {
            try
            {
                string data = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RoadLife ETS2", "RoadLife Dashboard", "WebView2");
                Directory.CreateDirectory(data);

                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await _web.EnsureCoreWebView2Async(env);

                _web.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _web.CoreWebView2.WebMessageReceived += OnWebMessage;

                string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                if (!Directory.Exists(www))
                    throw new DirectoryNotFoundException("Pasta www do RoadLife Dashboard não encontrada.");

                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "roadlife.dashboard.local",
                    www,
                    CoreWebView2HostResourceAccessKind.Allow);

                _web.Source = new Uri("https://roadlife.dashboard.local/app/index.html");
                _ready = true;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível iniciar o RoadLife Dashboard.\r\n\r\n" + ex.Message +
                    "\r\n\r\nConfirme se o Microsoft Edge WebView2 Runtime está instalado.",
                    "RoadLife ETS2 Dashboard",
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

                await _web.CoreWebView2.ExecuteScriptAsync(
                    "window.roadLifePushTelemetry(" +
                    JsonConvert.SerializeObject(json) +
                    ")");
            }
            catch { }
            finally { _polling = false; }
        }

        private void LoadWindowState()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var j = JObject.Parse(File.ReadAllText(_settingsPath));
                _savedPos = new Point((int?)j["x"] ?? _savedPos.X, (int?)j["y"] ?? _savedPos.Y);
                _savedSize = new Size((int?)j["width"] ?? _savedSize.Width, (int?)j["height"] ?? _savedSize.Height);
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                if (WindowState != FormWindowState.Normal) return;
                _savedPos = Location;
                _savedSize = NormalizeSize(Size);
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

        private void SnapAspectAndSave()
        {
            if (_applyingAspect || WindowState != FormWindowState.Normal) return;
            _applyingAspect = true;
            try { Size = NormalizeSize(Size); }
            finally { _applyingAspect = false; }
            SaveWindowState();
        }

        private static Size NormalizeSize(Size input)
        {
            int w = Math.Max(760, Math.Min(1800, input.Width));
            int h = (int)Math.Round(w / Aspect);
            return new Size(w, h);
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
            if (m.Msg == WM_SIZING)
            {
                var r = Marshal.PtrToStructure<RECT>(m.LParam);
                int width = Math.Max(760, Math.Min(1800, r.Right - r.Left));
                int height = (int)Math.Round(width / Aspect);

                int edge = m.WParam.ToInt32();
                if (edge == 1 || edge == 4 || edge == 7) r.Left = r.Right - width;
                else r.Right = r.Left + width;

                if (edge == 3 || edge == 4 || edge == 5) r.Top = r.Bottom - height;
                else r.Bottom = r.Top + height;

                Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = (IntPtr)1;
                return;
            }

            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point(
                    unchecked((short)(lp & 0xffff)),
                    unchecked((short)((lp >> 16) & 0xffff))));

                const int grip = 6;
                bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
                bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;

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
