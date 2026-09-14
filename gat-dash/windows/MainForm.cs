using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatDash.Windows
{
    internal sealed class MainForm : Form
    {
        private const string CentralLogin = "https://api.gatlogets2.com.br/api/account/login";
        private readonly WebView2 _web = new WebView2();
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        private readonly Timer _telemetryTimer = new Timer { Interval = 350 };
        private readonly SpeechSynthesizer _voice = new SpeechSynthesizer();
        private bool _polling;
        private bool _ready;
        private string _host = "";
        private bool _voiceEnabled = true;
        private int _tolerance = 3;
        private readonly string _settingsPath;

        public MainForm()
        {
            Text = "GAT DASH 1.0";
            MinimumSize = new Size(1024, 620);
            Size = new Size(1450, 850);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(2, 11, 22);
            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-DASH", "settings.json");
            LoadSettings();
            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);
            Shown += async delegate { await InitAsync(); };
            FormClosing += delegate { _telemetryTimer.Stop(); try { _voice.SpeakAsyncCancelAll(); } catch { } };
            FormClosed += delegate { _telemetryTimer.Dispose(); _voice.Dispose(); _http.Dispose(); _web.Dispose(); };
            _telemetryTimer.Tick += async delegate { await PollTelemetryAsync(); };
        }

        private async Task InitAsync()
        {
            try
            {
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-DASH", "WebView2");
                Directory.CreateDirectory(data);
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await _web.EnsureCoreWebView2Async(env);
                _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _web.CoreWebView2.WebMessageReceived += OnMessage;
                string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
                _web.CoreWebView2.SetVirtualHostNameToFolderMapping("gatdash.local", www, CoreWebView2HostResourceAccessKind.Allow);
                _web.Source = new Uri("https://gatdash.local/index.html");
                _ready = true;
                _telemetryTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível iniciar o GAT DASH.\r\n\r\n" + ex.Message + "\r\n\r\nConfirme se o Microsoft Edge WebView2 Runtime está instalado.", "GAT DASH", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JObject.Parse(e.WebMessageAsJson);
                string type = (string)msg["type"] ?? "";
                if (type == "ready")
                {
                    await JsAsync("window.gatDashNativeReady('windows'," + SettingsJson() + ")");
                    return;
                }
                if (type == "login")
                {
                    await LoginAsync((string)msg["user"] ?? "", (string)msg["password"] ?? "");
                    return;
                }
                if (type == "setSettings")
                {
                    _host = SanitizeHost((string)msg["host"] ?? "");
                    _voiceEnabled = (bool?)msg["voice"] != false;
                    _tolerance = Math.Max(0, Math.Min(30, (int?)msg["tolerance"] ?? 3));
                    SaveSettings();
                    await JsAsync("window.gatDashSettings(" + SettingsJson() + ")");
                    return;
                }
                if (type == "speak" && _voiceEnabled)
                {
                    string text = ((string)msg["text"] ?? "").Trim();
                    if (text.Length > 0 && text.Length < 240)
                    {
                        try { _voice.SpeakAsyncCancelAll(); _voice.SpeakAsync(text); } catch { }
                    }
                }
            }
            catch { }
        }

        private async Task LoginAsync(string user, string password)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new { user = (user ?? "").Trim().ToLowerInvariant(), password = password ?? "" });
                using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                using (var response = await _http.PostAsync(CentralLogin, content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(body)) body = "{\"ok\":false,\"error\":\"empty_response\"}";
                    await JsAsync("window.gatDashLoginResult(" + JsonConvert.SerializeObject(body) + ")");
                }
            }
            catch (Exception ex)
            {
                await JsAsync("window.gatDashLoginTransportError(" + JsonConvert.SerializeObject(ex.Message) + ")");
            }
        }

        private async Task PollTelemetryAsync()
        {
            if (!_ready || _polling || _web.CoreWebView2 == null) return;
            _polling = true;
            try
            {
                string host = string.IsNullOrWhiteSpace(_host) ? "127.0.0.1" : _host;
                string url = "http://" + host + (host.Contains(":") ? "" : ":31377") + "/api/ets2/telemetry";
                string json = await _http.GetStringAsync(url);
                await JsAsync("window.gatDashPushTelemetry(" + JsonConvert.SerializeObject(json) + ")");
            }
            catch (Exception ex)
            {
                await JsAsync("window.gatDashTelemetryError(" + JsonConvert.SerializeObject(ShortError(ex.Message)) + ")");
            }
            finally { _polling = false; }
        }

        private async Task JsAsync(string script)
        {
            if (!_ready || _web.CoreWebView2 == null) return;
            try { await _web.CoreWebView2.ExecuteScriptAsync(script); } catch { }
        }

        private static string SanitizeHost(string value)
        {
            string h = (value ?? "").Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            int slash = h.IndexOf('/'); if (slash >= 0) h = h.Substring(0, slash);
            return h.Length <= 120 ? h : "";
        }

        private string SettingsJson()
        {
            return JsonConvert.SerializeObject(new { host = _host, voice = _voiceEnabled, tolerance = _tolerance });
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var j = JObject.Parse(File.ReadAllText(_settingsPath));
                _host = SanitizeHost((string)j["host"] ?? "");
                _voiceEnabled = (bool?)j["voice"] != false;
                _tolerance = Math.Max(0, Math.Min(30, (int?)j["tolerance"] ?? 3));
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
                File.WriteAllText(_settingsPath, SettingsJson());
            }
            catch { }
        }

        private static string ShortError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "sem conexão";
            return message.Length > 90 ? message.Substring(0, 90) + "…" : message;
        }
    }
}
