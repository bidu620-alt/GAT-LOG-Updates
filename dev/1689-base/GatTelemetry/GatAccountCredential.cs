using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class GatAccountCredential
{
	[JsonProperty("user")]
	public string User { get; set; }

	[JsonProperty("token")]
	public string Token { get; set; }

	[JsonProperty("saved_at")]
	public string SavedAt { get; set; }
}
