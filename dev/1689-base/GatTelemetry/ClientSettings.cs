using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class ClientSettings
{
	[JsonProperty("auto_connect")]
	public bool AutoConnect { get; set; }

	[JsonProperty("last_server")]
	public string LastServer { get; set; }

	[JsonProperty("last_driver")]
	public string LastDriver { get; set; }

	[JsonProperty("updated_at")]
	public string UpdatedAt { get; set; }
}
