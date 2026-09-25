namespace GatTelemetry;

internal sealed class TelemetryDisplay
{
	public string Cargo { get; set; } = "Sem carga";

	public string Route { get; set; } = "-";

	public string Distance { get; set; } = "-";

	public string Speed { get; set; } = "0 km/h";

	public string Weight { get; set; } = "-";

	public string CargoDamage { get; set; } = "—";

	public string EngineDamage { get; set; } = "—";

	public string TransmissionDamage { get; set; } = "—";

	public string CabinDamage { get; set; } = "—";

	public string ChassisDamage { get; set; } = "—";

	public string WheelsDamage { get; set; } = "—";

	public string TrailerDamage { get; set; } = "—";
}
