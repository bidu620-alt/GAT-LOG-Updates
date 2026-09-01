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

namespace GatLogServer;

internal sealed class AgentClient : IDisposable
{
	private const string BaseUrl = "http://127.0.0.1:5055";

	private readonly HttpClient _http;

	private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);

	private string Secret
	{
		get
		{
			try
			{
				return File.Exists(AuthService.AgentSecretPath) ? File.ReadAllText(AuthService.AgentSecretPath).Trim() : "";
			}
			catch
			{
				return "";
			}
		}
	}

	public AgentClient()
	{
		_http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(2.0)
		};
	}

	public async Task<bool> HealthAsync()
	{
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(900.0));
			using HttpResponseMessage httpResponseMessage = await _http.GetAsync("http://127.0.0.1:5055/health", cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			return httpResponseMessage.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	public async Task<bool> EnsureAgentAsync()
	{
		string exe = FindAgentExe();
		if (exe == null)
		{
			return false;
		}
		StopForeignAgents(exe);
		bool flag = IsAgentRunningFrom(exe);
		if (flag)
		{
			flag = await HealthAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		if (flag)
		{
			return true;
		}
		StopAgentFrom(exe);
		await Task.Delay(200).ConfigureAwait(continueOnCapturedContext: false);
		StopForeignAgents(exe);
		try
		{
			Process.Start(new ProcessStartInfo(exe, "--background")
			{
				WorkingDirectory = Path.GetDirectoryName(exe),
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
		}
		catch
		{
			return false;
		}
		for (int i = 0; i < 16; i++)
		{
			await Task.Delay(250).ConfigureAwait(continueOnCapturedContext: false);
			StopForeignAgents(exe);
			flag = IsAgentRunningFrom(exe);
			if (flag)
			{
				flag = await HealthAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	private string FindAgentExe()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
		string[] array = new string[3]
		{
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GAT_LOG_AGENT.exe"),
			Path.Combine(folderPath2, "GAT-LOG Server", "GAT_LOG_AGENT.exe"),
			Path.Combine(folderPath, "Programs", "GAT-LOG Server", "GAT_LOG_AGENT.exe")
		};
		foreach (string path in array)
		{
			if (File.Exists(path))
			{
				return Path.GetFullPath(path);
			}
		}
		return null;
	}

	private static bool SamePath(string a, string b)
	{
		try
		{
			return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static string ProcessPath(Process p)
	{
		try
		{
			return p.MainModule?.FileName ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static bool IsAgentRunningFrom(string expectedExe)
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("GAT_LOG_AGENT");
			foreach (Process process in processesByName)
			{
				try
				{
					if (SamePath(ProcessPath(process), expectedExe))
					{
						return true;
					}
				}
				finally
				{
					process.Dispose();
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void StopForeignAgents(string expectedExe)
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("GAT_LOG_AGENT");
			foreach (Process process in processesByName)
			{
				try
				{
					string text = ProcessPath(process);
					if (!string.IsNullOrWhiteSpace(text) && !SamePath(text, expectedExe))
					{
						process.Kill();
						process.WaitForExit(1200);
					}
				}
				catch
				{
				}
				finally
				{
					process.Dispose();
				}
			}
		}
		catch
		{
		}
	}

	private static void StopAgentFrom(string expectedExe)
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("GAT_LOG_AGENT");
			foreach (Process process in processesByName)
			{
				try
				{
					if (SamePath(ProcessPath(process), expectedExe))
					{
						process.Kill();
						process.WaitForExit(1200);
					}
				}
				catch
				{
				}
				finally
				{
					process.Dispose();
				}
			}
		}
		catch
		{
		}
	}

	private async Task<string> SendAsync(HttpMethod method, string path, object body = null)
	{
		await _requestGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(method, "http://127.0.0.1:5055" + path);
			string secret = Secret;
			if (!string.IsNullOrWhiteSpace(secret))
			{
				req.Headers.TryAddWithoutValidation("X-GAT-Admin", secret);
			}
			if (body != null)
			{
				string content = JsonConvert.SerializeObject(body);
				req.Content = new StringContent(content, Encoding.UTF8, "application/json");
			}
			using HttpResponseMessage resp = await _http.SendAsync(req).ConfigureAwait(continueOnCapturedContext: false);
			string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (!resp.IsSuccessStatusCode)
			{
				throw new InvalidOperationException("Agente HTTP " + (int)resp.StatusCode + (string.IsNullOrWhiteSpace(text) ? "" : (": " + text)));
			}
			return text;
		}
		finally
		{
			_requestGate.Release();
		}
	}

	public async Task<ServerStatus> GetStatusAsync()
	{
		return JsonConvert.DeserializeObject<ServerStatus>(await SendAsync(HttpMethod.Get, "/api/ui/status").ConfigureAwait(continueOnCapturedContext: false)) ?? new ServerStatus();
	}

	public async Task<ServerConfig> GetConfigAsync()
	{
		JToken obj = JObject.Parse(await SendAsync(HttpMethod.Get, "/api/ui/config").ConfigureAwait(continueOnCapturedContext: false))["config"];
		return ((obj != null) ? obj.ToObject<ServerConfig>() : null) ?? new ServerConfig();
	}

	public Task SaveConfigAsync(ServerConfig cfg)
	{
		return SendAsync(HttpMethod.Post, "/api/ui/config", cfg);
	}

	public async Task<string> ActionAsync(string action, string driver = "")
	{
		string text = await SendAsync(HttpMethod.Post, "/api/ui/action", new { action, driver }).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			return ((object)JObject.Parse(text)["message"])?.ToString() ?? "OK";
		}
		catch
		{
			return "OK";
		}
	}

	public async Task<List<string>> GetModsAsync()
	{
		JToken obj = JObject.Parse(await SendAsync(HttpMethod.Get, "/api/ui/mods").ConfigureAwait(continueOnCapturedContext: false))["mods"];
		return ((obj != null) ? obj.ToObject<List<string>>() : null) ?? new List<string>();
	}

	public async Task<List<BindingInfo>> GetBindingsAsync()
	{
		JToken obj = JObject.Parse(await SendAsync(HttpMethod.Get, "/api/ui/bindings").ConfigureAwait(continueOnCapturedContext: false))["bindings"];
		return ((obj != null) ? obj.ToObject<List<BindingInfo>>() : null) ?? new List<BindingInfo>();
	}

	public void Dispose()
	{
		_http.Dispose();
		_requestGate.Dispose();
	}
}
