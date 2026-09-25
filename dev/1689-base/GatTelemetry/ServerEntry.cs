using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class ServerEntry
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("endpoint")]
	public string Endpoint { get; set; }

	public override string ToString()
	{
		if (!string.IsNullOrWhiteSpace(Name))
		{
			return Name;
		}
		return Endpoint;
	}
}
