using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal sealed class ApiClient : IDisposable
    {
        private readonly HttpClient _http;

        public ApiClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("GAT-Telemetria-CSharp/1.0");
        }

        public async Task<ApiResponse> GetAsync(string url, int seconds = 6)
        {
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
                using (var response = await _http.GetAsync(url, cts.Token).ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return Build((int)response.StatusCode, text, null);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse { Error = ex, StatusCode = 0, Text = ex.Message };
            }
        }

        public async Task<ApiResponse> PostAsync(string url, object body, int seconds = 6)
        {
            try
            {
                string json = JsonConvert.SerializeObject(body);
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await _http.PostAsync(url, content, cts.Token).ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return Build((int)response.StatusCode, text, null);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse { Error = ex, StatusCode = 0, Text = ex.Message };
            }
        }

        private static ApiResponse Build(int status, string text, Exception error)
        {
            JObject obj = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { obj = JObject.Parse(text); } catch { }
            }
            return new ApiResponse { StatusCode = status, Text = text, Json = obj, Error = error };
        }

        public async Task<ServerInfo> GetServerInfoAsync(string endpoint)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            var r = await GetAsync(ep + "/api/client/server-info", 5).ConfigureAwait(false);
            if (r.StatusCode == 200 && r.Json != null && Bool(r.Json["ok"]))
            {
                return new ServerInfo
                {
                    Reachable = true,
                    Supported = true,
                    Online = Bool(r.Json["online"]),
                    ServerName = Str(r.Json["server_name"]),
                    SessionId = Str(r.Json["session_id"]),
                    Players = Int(r.Json["players"]),
                    MaxPlayers = Int(r.Json["max_players"])
                };
            }
            var h = await GetAsync(ep + "/health", 4).ConfigureAwait(false);
            return h.StatusCode == 200
                ? new ServerInfo { Reachable = true, Supported = false }
                : new ServerInfo { Reachable = false, Supported = false };
        }

        public async Task<PlayersResult> GetPlayersAsync(string endpoint)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            var r = await GetAsync(ep + "/api/client/players", 5).ConfigureAwait(false);
            var result = new PlayersResult();
            if (r.StatusCode != 200 || r.Json == null || !Bool(r.Json["ok"])) return result;
            var arr = r.Json["players"] as JArray;
            if (arr == null) return result;
            result.Ok = true;
            result.Players = arr.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return result;
        }

        public Task<ApiResponse> LoginAsync(string endpoint, string driver, string deviceId, string token)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            return PostAsync(ep + "/api/client/login", new { driver, device_id = deviceId, token = token ?? string.Empty }, 8);
        }

        public Task<ApiResponse> HeartbeatAsync(string endpoint, string driver, string deviceId, string token)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            return PostAsync(ep + "/api/client/heartbeat", new { driver, device_id = deviceId, token = token ?? string.Empty }, 4);
        }

        public Task<ApiResponse> SendTelemetryAsync(string endpoint, string driver, string deviceId, string token, JObject telemetry)
        {
            string ep = ClientStore.NormalizeEndpoint(endpoint);
            return PostAsync(ep + "/api/client/telemetry", new JObject
            {
                ["driver"] = driver,
                ["device_id"] = deviceId,
                ["token"] = token ?? string.Empty,
                ["telemetry"] = telemetry
            }, 5);
        }

        public static bool Bool(JToken token)
        {
            if (token == null) return false;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            bool b; return bool.TryParse(token.ToString(), out b) && b;
        }

        public static string Str(JToken token) => token?.ToString() ?? string.Empty;
        public static int Int(JToken token) { int n; return int.TryParse(token?.ToString(), out n) ? n : 0; }

        public void Dispose() => _http.Dispose();
    }
}
