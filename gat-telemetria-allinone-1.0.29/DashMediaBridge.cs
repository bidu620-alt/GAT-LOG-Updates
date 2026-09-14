using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal static class DashMediaBridge
    {
        private const int Port = 31378;
        private const string RadioEndpoint = "https://api.gatlogets2.com.br/api/public/radio";
        private static readonly object Sync = new object();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        private static TcpListener _listener;
        private static Thread _thread;
        private static bool _running;
        private static DateTime _officialAt = DateTime.MinValue;
        private static JObject _official = new JObject { ["enabled"] = false, ["url"] = "", ["sourceType"] = "" };

        internal static void Start()
        {
            lock (Sync)
            {
                if (_running) return;
                try
                {
                    _listener = new TcpListener(IPAddress.Any, Port);
                    _listener.Start();
                    _running = true;
                    _thread = new Thread(ServerLoop) { IsBackground = true, Name = "GAT DASH Media Bridge" };
                    _thread.Start();
                }
                catch
                {
                    _running = false;
                    try { _listener?.Stop(); } catch { }
                    _listener = null;
                }
            }
        }

        internal static void Stop()
        {
            lock (Sync)
            {
                _running = false;
                try { _listener?.Stop(); } catch { }
                _listener = null;
            }
        }

        private static void ServerLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => Handle(client));
                }
                catch
                {
                    if (!_running) return;
                    Thread.Sleep(150);
                }
            }
        }

        private static void Handle(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3500;
                    client.SendTimeout = 3500;
                    using (NetworkStream stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                    {
                        string first = reader.ReadLine() ?? "";
                        string line;
                        while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }
                        string[] parts = first.Split(' ');
                        string method = parts.Length > 0 ? parts[0].ToUpperInvariant() : "";
                        string path = parts.Length > 1 ? parts[1].Split('?')[0] : "/";

                        if (method == "OPTIONS") { Write(stream, 204, "", "text/plain"); return; }
                        if (method != "GET" || !string.Equals(path, "/api/gat/media", StringComparison.OrdinalIgnoreCase))
                        {
                            Write(stream, 404, "{\"ok\":false,\"error\":\"not_found\"}", "application/json; charset=utf-8");
                            return;
                        }

                        JObject payload = new JObject
                        {
                            ["ok"] = true,
                            ["channelGat"] = GetOfficial(),
                            ["myVideo"] = new JObject { ["url"] = ReadLocal("radio-personal-source.txt") },
                            ["web"] = new JObject { ["url"] = ReadLocal("radio-web-url.txt") },
                            ["updatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        Write(stream, 200, payload.ToString(Formatting.None), "application/json; charset=utf-8");
                    }
                }
                catch { }
            }
        }

        private static JObject GetOfficial()
        {
            lock (Sync)
            {
                if (DateTime.UtcNow - _officialAt < TimeSpan.FromSeconds(15)) return (JObject)_official.DeepClone();
                try
                {
                    string text = Http.GetStringAsync(RadioEndpoint + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).GetAwaiter().GetResult();
                    JObject root = JObject.Parse(text);
                    JObject radio = root["radio"] as JObject;
                    if (root.Value<bool?>("ok") == true && radio != null)
                    {
                        bool enabled = radio.Value<bool?>("enabled") == true;
                        string type = (Convert.ToString(radio["source_type"]) ?? "").Trim().ToLowerInvariant();
                        string url = Convert.ToString(radio["source_url"]) ?? Convert.ToString(radio["playlist_url"]) ?? "";
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            string video = Convert.ToString(radio["video_id"]) ?? "";
                            string list = Convert.ToString(radio["playlist_id"]) ?? "";
                            if (!string.IsNullOrWhiteSpace(list)) url = "https://www.youtube.com/playlist?list=" + list;
                            else if (!string.IsNullOrWhiteSpace(video)) url = "https://www.youtube.com/watch?v=" + video;
                        }
                        _official = new JObject
                        {
                            ["enabled"] = enabled,
                            ["url"] = url,
                            ["sourceType"] = type,
                            ["revision"] = radio.Value<long?>("revision") ?? 0
                        };
                    }
                }
                catch { }
                _officialAt = DateTime.UtcNow;
                return (JObject)_official.DeepClone();
            }
        }

        private static string ReadLocal(string name)
        {
            try
            {
                string path = Path.Combine(Application.LocalUserAppDataPath, name);
                if (!File.Exists(path)) return "";
                string value = File.ReadAllText(path).Trim();
                Uri uri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return "";
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return "";
                return uri.AbsoluteUri;
            }
            catch { return ""; }
        }

        private static void Write(NetworkStream stream, int status, string body, string contentType)
        {
            byte[] data = Encoding.UTF8.GetBytes(body ?? "");
            string text = "HTTP/1.1 " + status + " " + StatusText(status) + "\r\n" +
                          "Content-Type: " + contentType + "\r\n" +
                          "Content-Length: " + data.Length + "\r\n" +
                          "Access-Control-Allow-Origin: *\r\n" +
                          "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
                          "Access-Control-Allow-Headers: Content-Type\r\n" +
                          "Cache-Control: no-store\r\n" +
                          "Connection: close\r\n\r\n";
            byte[] headers = Encoding.ASCII.GetBytes(text);
            stream.Write(headers, 0, headers.Length);
            if (data.Length > 0) stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        private static string StatusText(int code)
        {
            if (code == 200) return "OK";
            if (code == 204) return "No Content";
            if (code == 404) return "Not Found";
            return "Error";
        }
    }
}
