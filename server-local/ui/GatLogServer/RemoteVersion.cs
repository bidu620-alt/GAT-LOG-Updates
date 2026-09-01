using Newtonsoft.Json;

namespace GatLogServer;

public sealed class RemoteVersion
{
	[JsonProperty("version")]
	public string Version { get; set; } = "";

	[JsonProperty("setup_url")]
	public string SetupUrl { get; set; } = "";

	[JsonProperty("sha256")]
	public string Sha256 { get; set; } = "";

	[JsonProperty("notas")]
	public string Notes { get; set; } = "";
}
