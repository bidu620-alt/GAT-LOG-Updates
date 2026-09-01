using System.Collections.Generic;
using Newtonsoft.Json;

namespace GatLogServer;

public sealed class ServerStatus
{
	[JsonProperty("ok")]
	public bool Ok { get; set; }

	[JsonProperty("agent_version")]
	public string AgentVersion { get; set; } = "";

	[JsonProperty("agent_uptime_sec")]
	public long AgentUptimeSec { get; set; }

	[JsonProperty("server_online")]
	public bool ServerOnline { get; set; }

	[JsonProperty("server_name")]
	public string ServerName { get; set; } = "";

	[JsonProperty("session_id")]
	public string SessionId { get; set; } = "";

	[JsonProperty("ports")]
	public string Ports { get; set; } = "";

	[JsonProperty("players")]
	public List<string> Players { get; set; } = new List<string>();

	[JsonProperty("player_count")]
	public int PlayerCount { get; set; }

	[JsonProperty("max_players")]
	public int MaxPlayers { get; set; } = 128;

	[JsonProperty("server_exe")]
	public string ServerExe { get; set; } = "";

	[JsonProperty("server_log")]
	public string ServerLog { get; set; } = "";

	[JsonProperty("packages_ok")]
	public bool PackagesOk { get; set; }

	[JsonProperty("packages_text")]
	public string PackagesText { get; set; } = "";

	[JsonProperty("funnel_url")]
	public string FunnelUrl { get; set; } = "";

	[JsonProperty("data_dir")]
	public string DataDir { get; set; } = "";

	[JsonProperty("telemetry")]
	public List<TelemetryRecord> Telemetry { get; set; } = new List<TelemetryRecord>();

	[JsonProperty("registration_open")]
	public bool RegistrationOpen { get; set; }

	[JsonProperty("last_refresh")]
	public string LastRefresh { get; set; } = "";
}
