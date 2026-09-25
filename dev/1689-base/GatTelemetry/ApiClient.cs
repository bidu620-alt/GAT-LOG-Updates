using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class ApiClient : IDisposable
{
	private readonly HttpClient _http;

	public ApiClient()
	{
		_http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(6.0)
		};
		_http.DefaultRequestHeaders.UserAgent.ParseAdd("GAT-Telemetria-CSharp/1.0");
	}

	public async Task<ApiResponse> GetAsync(string url, int seconds = 6)
	{
		_ = 1;
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
			using HttpResponseMessage response = await _http.GetAsync(url, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			return Build((int)response.StatusCode, text, null);
		}
		catch (Exception ex)
		{
			return new ApiResponse
			{
				Error = ex,
				StatusCode = 0,
				Text = ex.Message
			};
		}
	}

	public async Task<ApiResponse> GetBearerAsync(string url, string token, int seconds = 6)
	{
		_ = 1;
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? string.Empty);
			using HttpResponseMessage response = await _http.SendAsync(request, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			return Build((int)response.StatusCode, text, null);
		}
		catch (Exception ex)
		{
			return new ApiResponse
			{
				Error = ex,
				StatusCode = 0,
				Text = ex.Message
			};
		}
	}

	public async Task<ApiResponse> PostAsync(string url, object body, int seconds = 6)
	{
		_ = 1;
		try
		{
			string content = JsonConvert.SerializeObject(body);
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
			using StringContent content2 = new StringContent(content, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await _http.PostAsync(url, content2, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			return Build((int)response.StatusCode, text, null);
		}
		catch (Exception ex)
		{
			return new ApiResponse
			{
				Error = ex,
				StatusCode = 0,
				Text = ex.Message
			};
		}
	}

	private static ApiResponse Build(int status, string text, Exception error)
	{
		JObject json = null;
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				json = JObject.Parse(text);
			}
			catch
			{
			}
		}
		return new ApiResponse
		{
			StatusCode = status,
			Text = text,
			Json = json,
			Error = error
		};
	}

	public async Task<ServerInfo> GetServerInfoAsync(string endpoint)
	{
		string ep = ClientStore.NormalizeEndpoint(endpoint);
		ApiResponse apiResponse = await GetAsync(ep + "/api/client/server-info", 5).ConfigureAwait(continueOnCapturedContext: false);
		if (apiResponse.StatusCode == 200 && apiResponse.Json != null && Bool(apiResponse.Json["ok"]))
		{
			return new ServerInfo
			{
				Reachable = true,
				Supported = true,
				Online = Bool(apiResponse.Json["online"]),
				ServerName = Str(apiResponse.Json["server_name"]),
				SessionId = Str(apiResponse.Json["session_id"]),
				Players = Int(apiResponse.Json["players"]),
				MaxPlayers = Int(apiResponse.Json["max_players"])
			};
		}
		return ((await GetAsync(ep + "/health", 4).ConfigureAwait(continueOnCapturedContext: false)).StatusCode == 200) ? new ServerInfo
		{
			Reachable = true,
			Supported = false
		} : new ServerInfo
		{
			Reachable = false,
			Supported = false
		};
	}

	public async Task<PlayersResult> GetPlayersAsync(string endpoint)
	{
		string text = ClientStore.NormalizeEndpoint(endpoint);
		ApiResponse apiResponse = await GetAsync(text + "/api/client/players", 5).ConfigureAwait(continueOnCapturedContext: false);
		PlayersResult playersResult = new PlayersResult();
		if (apiResponse.StatusCode != 200 || apiResponse.Json == null || !Bool(apiResponse.Json["ok"]))
		{
			return playersResult;
		}
		if (!(apiResponse.Json["players"] is JArray source))
		{
			return playersResult;
		}
		playersResult.Ok = true;
		playersResult.Players = (from x in source
			select x?.ToString() into x
			where !string.IsNullOrWhiteSpace(x)
			select x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		return playersResult;
	}

	public Task<ApiResponse> LoginAsync(string endpoint, string driver, string deviceId, string token, string accountUser, string accountToken)
	{
		string text = ClientStore.NormalizeEndpoint(endpoint);
		return PostAsync(text + "/api/client/login", new
		{
			driver = driver,
			device_id = deviceId,
			token = (token ?? string.Empty),
			account_user = (accountUser ?? string.Empty),
			account_token = (accountToken ?? string.Empty)
		}, 8);
	}

	public Task<ApiResponse> AccountLoginAsync(string authority, string user, string password)
	{
		string text = ClientStore.NormalizeEndpoint(authority);
		return PostAsync(text + "/api/account/login", new
		{
			user = (user ?? string.Empty),
			password = (password ?? string.Empty)
		}, 8);
	}

	public Task<ApiResponse> AccountSessionAsync(string authority, string token)
	{
		string text = ClientStore.NormalizeEndpoint(authority);
		return PostAsync(text + "/api/account/session", new
		{
			token = (token ?? string.Empty)
		});
	}

	public Task<ApiResponse> HeartbeatAsync(string endpoint, string driver, string deviceId, string token)
	{
		string text = ClientStore.NormalizeEndpoint(endpoint);
		return PostAsync(text + "/api/client/heartbeat", new
		{
			driver = driver,
			device_id = deviceId,
			token = (token ?? string.Empty)
		}, 4);
	}

	public Task<ApiResponse> SendTelemetryAsync(string endpoint, string driver, string deviceId, string token, JObject telemetry)
	{
		string text = ClientStore.NormalizeEndpoint(endpoint);
		return PostAsync(text + "/api/client/telemetry", new JObject
		{
			["driver"] = driver,
			["device_id"] = deviceId,
			["token"] = token ?? string.Empty,
			["telemetry"] = telemetry
		}, 5);
	}

	public async Task<ApiResponse> PostBearerAsync(string url, string token, object body, int seconds = 6)
	{
		_ = 1;
		try
		{
			string content = JsonConvert.SerializeObject(body);
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
			using StringContent content2 = new StringContent(content, Encoding.UTF8, "application/json");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? string.Empty);
			request.Content = content2;
			using HttpResponseMessage response = await _http.SendAsync(request, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			return Build((int)response.StatusCode, text, null);
		}
		catch (Exception ex)
		{
			return new ApiResponse
			{
				Error = ex,
				StatusCode = 0,
				Text = ex.Message
			};
		}
	}

	public Task<ApiResponse> SendAccountTelemetryAsync(string authority, string accountToken, string driver, JObject telemetry)
	{
		string text = ClientStore.NormalizeEndpoint(authority);
		return PostBearerAsync(text + "/api/account/telemetry", accountToken, new JObject
		{
			["driver"] = driver ?? string.Empty,
			["telemetry"] = telemetry
		}, 5);
	}

	public Task<ApiResponse> SendTripReceiptAsync(string authority, string accountToken, string driver, TripReceipt receipt)
	{
		string text = ClientStore.NormalizeEndpoint(authority);
		JObject jObject = JObject.FromObject(receipt ?? new TripReceipt());
		jObject["driver"] = driver ?? string.Empty;
		return PostBearerAsync(text + "/api/account/trip-complete", accountToken, jObject, 8);
	}

	public static bool Bool(JToken token)
	{
		if (token == null)
		{
			return false;
		}
		if (token.Type == JTokenType.Boolean)
		{
			return token.Value<bool>();
		}
		bool result;
		return bool.TryParse(token.ToString(), out result) & result;
	}

	public static string Str(JToken token)
	{
		return token?.ToString() ?? string.Empty;
	}

	public static int Int(JToken token)
	{
		if (!int.TryParse(token?.ToString(), out var result))
		{
			return 0;
		}
		return result;
	}

	public void Dispose()
	{
		_http.Dispose();
	}
}
