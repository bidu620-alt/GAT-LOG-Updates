using Newtonsoft.Json;

namespace GatLogServer;

public sealed class TelemetryRecord
{
	[JsonProperty("driver")]
	public string Driver { get; set; } = "";

	[JsonProperty("device_id")]
	public string DeviceId { get; set; } = "";

	[JsonProperty("updated_at")]
	public string UpdatedAt { get; set; } = "";

	[JsonProperty("status")]
	public string Status { get; set; } = "";

	[JsonProperty("cargo")]
	public string Cargo { get; set; } = "";

	[JsonProperty("cargo_mass_kg")]
	public double CargoMassKg { get; set; }

	[JsonProperty("source")]
	public string Source { get; set; } = "";

	[JsonProperty("destination")]
	public string Destination { get; set; } = "";

	[JsonProperty("remaining_km")]
	public double RemainingKm { get; set; }

	[JsonProperty("speed_kmh")]
	public double SpeedKmh { get; set; }

	[JsonProperty("on_job")]
	public bool OnJob { get; set; }
}
