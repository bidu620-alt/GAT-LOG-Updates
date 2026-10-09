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

namespace GatTelemetry;

internal static class DashMediaBridge
{
	private const int Port = 31378;

	private const string RadioEndpoint = "https://api.gatlogets2.com.br/api/public/radio";

	private static readonly object Sync = new object();

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(6.0)
	};

	private static TcpListener _listener;

	private static Thread _thread;

	private static bool _running;

	private static DateTime _officialAt = DateTime.MinValue;

	private static JObject _official = new JObject
	{
		["enabled"] = false,
		["url"] = "",
		["sourceType"] = ""
	};

	internal static void Start()
	{
		lock (Sync)
		{
			if (_running)
			{
				return;
			}
			try
			{
				_listener = new TcpListener(IPAddress.Any, 31378);
				_listener.Start();
				_running = true;
				_thread = new Thread(ServerLoop)
				{
					IsBackground = true,
					Name = "ROADLIFE DASH Media Bridge"
				};
				_thread.Start();
			}
			catch
			{
				_running = false;
				try
				{
					_listener?.Stop();
				}
				catch
				{
				}
				_listener = null;
			}
		}
	}

	internal static void Stop()
	{
		lock (Sync)
		{
			_running = false;
			try
			{
				_listener?.Stop();
			}
			catch
			{
			}
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
				ThreadPool.QueueUserWorkItem((object _) =>
				{
					Handle(client);
				});
			}
			catch
			{
				if (!_running)
				{
					break;
				}
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
				using NetworkStream stream = client.GetStream();
				using StreamReader streamReader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
				string text = streamReader.ReadLine() ?? "";
				while (!string.IsNullOrEmpty(streamReader.ReadLine()))
				{
				}
				string[] array = text.Split(' ');
				string text2 = ((array.Length != 0) ? array[0].ToUpperInvariant() : "");
				string a = ((array.Length > 1) ? array[1].Split('?')[0] : "/");
				if (text2 == "OPTIONS")
				{
					Write(stream, 204, "", "text/plain");
					return;
				}
				if (text2 != "GET" || !string.Equals(a, "/api/gat/media", StringComparison.OrdinalIgnoreCase))
				{
					Write(stream, 404, "{\"ok\":false,\"error\":\"not_found\"}", "application/json; charset=utf-8");
					return;
				}
				JObject jObject = new JObject
				{
					["ok"] = true,
					["activeMode"] = ReadMode041(),
					["channelGat"] = GetOfficial(),
					["myVideo"] = new JObject { ["url"] = ReadLocal("radio-personal-source.txt") },
					["updatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
				};
				Write(stream, 200, jObject.ToString(Formatting.None), "application/json; charset=utf-8");
			}
			catch
			{
			}
		}
	}

	private static JObject GetOfficial()
	{
		lock (Sync)
		{
			if (DateTime.UtcNow - _officialAt < TimeSpan.FromSeconds(15.0))
			{
				return (JObject)_official.DeepClone();
			}
			try
			{
				JObject jObject = JObject.Parse(Http.GetStringAsync("https://api.gatlogets2.com.br/api/public/radio?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).GetAwaiter().GetResult());
				JObject jObject2 = jObject["radio"] as JObject;
				if (jObject.Value<bool?>("ok") == true && jObject2 != null)
				{
					bool valueOrDefault = jObject2.Value<bool?>("enabled") == true;
					string text = (Convert.ToString(jObject2["source_type"]) ?? "").Trim().ToLowerInvariant();
					string text2 = Convert.ToString(jObject2["source_url"]) ?? Convert.ToString(jObject2["playlist_url"]) ?? "";
					if (string.IsNullOrWhiteSpace(text2))
					{
						string text3 = Convert.ToString(jObject2["video_id"]) ?? "";
						string text4 = Convert.ToString(jObject2["playlist_id"]) ?? "";
						if (!string.IsNullOrWhiteSpace(text4))
						{
							text2 = "https://www.youtube.com/playlist?list=" + text4;
						}
						else if (!string.IsNullOrWhiteSpace(text3))
						{
							text2 = "https://www.youtube.com/watch?v=" + text3;
						}
					}
					_official = new JObject
					{
						["enabled"] = valueOrDefault,
						["url"] = text2,
						["sourceType"] = text,
						["revision"] = jObject2.Value<long?>("revision").GetValueOrDefault()
					};
				}
			}
			catch
			{
			}
			_officialAt = DateTime.UtcNow;
			return (JObject)_official.DeepClone();
		}
	}

	private static string ReadMode041()
	{
		try
		{
			string path = Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
			if (!File.Exists(path))
			{
				return "gat";
			}
			return ((File.ReadAllText(path) ?? string.Empty).Trim().ToLowerInvariant() == "mine") ? "mine" : "gat";
		}
		catch
		{
			return "gat";
		}
	}

	private static string ReadLocal(string name)
	{
		try
		{
			string path = Path.Combine(Application.LocalUserAppDataPath, name);
			if (!File.Exists(path))
			{
				return "";
			}
			if (!Uri.TryCreate(File.ReadAllText(path).Trim(), UriKind.Absolute, out var result))
			{
				return "";
			}
			if (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps)
			{
				return "";
			}
			return result.AbsoluteUri;
		}
		catch
		{
			return "";
		}
	}

	private static void Write(NetworkStream stream, int status, string body, string contentType)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
		string s = "HTTP/1.1 " + status + " " + StatusText(status) + "\r\nContent-Type: " + contentType + "\r\nContent-Length: " + bytes.Length + "\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
		byte[] bytes2 = Encoding.ASCII.GetBytes(s);
		stream.Write(bytes2, 0, bytes2.Length);
		if (bytes.Length != 0)
		{
			stream.Write(bytes, 0, bytes.Length);
		}
		stream.Flush();
	}

	private static string StatusText(int code)
	{
		return code switch
		{
			200 => "OK", 
			204 => "No Content", 
			404 => "Not Found", 
			_ => "Error", 
		};
	}
}
