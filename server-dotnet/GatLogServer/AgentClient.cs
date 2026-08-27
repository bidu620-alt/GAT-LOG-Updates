using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatLogServer
{
    internal sealed class AgentClient : IDisposable
    {
        private const string BaseUrl = "http://127.0.0.1:5055";
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);

        public AgentClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        }

        private string Secret
        {
            get
            {
                try { return File.Exists(AuthService.AgentSecretPath) ? File.ReadAllText(AuthService.AgentSecretPath).Trim() : ""; }
                catch { return ""; }
            }
        }

        public async Task<bool> HealthAsync()
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(900)))
                using (var r = await _http.GetAsync(BaseUrl + "/health", cts.Token).ConfigureAwait(false))
                    return r.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> EnsureAgentAsync()
        {
            if (await HealthAsync().ConfigureAwait(false)) return true;
            var exe = FindAgentExe();
            if (exe == null) return false;
            try
            {
                var psi = new ProcessStartInfo(exe, "--background")
                {
                    WorkingDirectory = Path.GetDirectoryName(exe),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch { return false; }

            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (await HealthAsync().ConfigureAwait(false)) return true;
            }
            return false;
        }

        private string FindAgentExe()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GAT_LOG_AGENT.exe"),
                Path.Combine(common, "GAT-LOG Server", "GAT_LOG_AGENT.exe"),
                Path.Combine(local, "Programs", "GAT-LOG Server", "GAT_LOG_AGENT.exe")
            };
            foreach (var p in candidates) if (File.Exists(p)) return p;
            return null;
        }

        private async Task<string> SendAsync(HttpMethod method, string path, object body = null)
        {
            await _requestGate.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var req = new HttpRequestMessage(method, BaseUrl + path))
                {
                    var secret = Secret;
                    if (!string.IsNullOrWhiteSpace(secret)) req.Headers.TryAddWithoutValidation("X-GAT-Admin", secret);
                    if (body != null)
                    {
                        var json = JsonConvert.SerializeObject(body);
                        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }
                    using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                    {
                        var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                            throw new InvalidOperationException("Agente HTTP " + (int)resp.StatusCode + (string.IsNullOrWhiteSpace(text) ? "" : ": " + text));
                        return text;
                    }
                }
            }
            finally { _requestGate.Release(); }
        }

        public async Task<ServerStatus> GetStatusAsync()
        {
            var text = await SendAsync(HttpMethod.Get, "/api/ui/status").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<ServerStatus>(text) ?? new ServerStatus();
        }

        public async Task<ServerConfig> GetConfigAsync()
        {
            var text = await SendAsync(HttpMethod.Get, "/api/ui/config").ConfigureAwait(false);
            var j = JObject.Parse(text);
            return j["config"]?.ToObject<ServerConfig>() ?? new ServerConfig();
        }

        public Task SaveConfigAsync(ServerConfig cfg) => SendAsync(HttpMethod.Post, "/api/ui/config", cfg);

        public async Task<string> ActionAsync(string action, string driver = "")
        {
            var text = await SendAsync(HttpMethod.Post, "/api/ui/action", new { action, driver }).ConfigureAwait(false);
            try { return JObject.Parse(text)["message"]?.ToString() ?? "OK"; }
            catch { return "OK"; }
        }

        public async Task<List<string>> GetModsAsync()
        {
            var text = await SendAsync(HttpMethod.Get, "/api/ui/mods").ConfigureAwait(false);
            var j = JObject.Parse(text);
            return j["mods"]?.ToObject<List<string>>() ?? new List<string>();
        }

        public async Task<List<BindingInfo>> GetBindingsAsync()
        {
            var text = await SendAsync(HttpMethod.Get, "/api/ui/bindings").ConfigureAwait(false);
            var j = JObject.Parse(text);
            return j["bindings"]?.ToObject<List<BindingInfo>>() ?? new List<BindingInfo>();
        }

        public void Dispose()
        {
            _http.Dispose();
            _requestGate.Dispose();
        }
    }
}
