using Newtonsoft.Json;

namespace GatLogServer;

public sealed class NativeAuth
{
	[JsonProperty("user")]
	public string User { get; set; } = "gatlog";

	[JsonProperty("salt")]
	public string Salt { get; set; } = "";

	[JsonProperty("hash")]
	public string Hash { get; set; } = "";

	[JsonProperty("updated_at")]
	public string UpdatedAt { get; set; } = "";
}
