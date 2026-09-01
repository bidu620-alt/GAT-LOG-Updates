using Newtonsoft.Json;

namespace GatLogServer;

public sealed class ServerConfig
{
	[JsonProperty("server_name")]
	public string ServerName { get; set; } = "GAT AMIGOS";

	[JsonProperty("description")]
	public string Description { get; set; } = "";

	[JsonProperty("welcome_message")]
	public string WelcomeMessage { get; set; } = "";

	[JsonProperty("server_password")]
	public string ServerPassword { get; set; } = "";

	[JsonProperty("max_players")]
	public int MaxPlayers { get; set; } = 128;

	[JsonProperty("traffic")]
	public bool Traffic { get; set; }

	[JsonProperty("player_damage")]
	public bool PlayerDamage { get; set; }

	[JsonProperty("moderator_steam_id")]
	public string ModeratorSteamId { get; set; } = "";

	[JsonProperty("funnel_url")]
	public string FunnelUrl { get; set; } = "";

	[JsonProperty("server_exe")]
	public string ServerExe { get; set; } = "";

	[JsonProperty("documents_home")]
	public string DocumentsHome { get; set; } = "";

	[JsonProperty("registration_open")]
	public bool RegistrationOpen { get; set; } = true;

	[JsonProperty("updated_at")]
	public string UpdatedAt { get; set; } = "";
}
