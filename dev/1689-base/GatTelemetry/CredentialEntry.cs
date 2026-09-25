using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class CredentialEntry
{
	[JsonProperty("endpoint")]
	public string Endpoint { get; set; }

	[JsonProperty("driver")]
	public string Driver { get; set; }

	[JsonProperty("token")]
	public string Token { get; set; }

	[JsonProperty("saved_at")]
	public string SavedAt { get; set; }
}
