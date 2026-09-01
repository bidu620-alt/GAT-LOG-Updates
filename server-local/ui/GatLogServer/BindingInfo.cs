using Newtonsoft.Json;

namespace GatLogServer;

public sealed class BindingInfo
{
	[JsonProperty("driver")]
	public string Driver { get; set; } = "";

	[JsonProperty("device_id")]
	public string DeviceId { get; set; } = "";

	[JsonProperty("blocked")]
	public bool Blocked { get; set; }

	[JsonProperty("disconnected")]
	public bool Disconnected { get; set; }

	[JsonProperty("last_seen")]
	public string LastSeen { get; set; } = "";
}
