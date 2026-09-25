namespace GatTelemetry;

internal sealed class ModIntegrityResult
{
	public string Status { get; set; } = "unknown";

	public string Reason { get; set; } = "not_checked";

	public string[] Matches { get; set; } = new string[0];

	public string EvidenceHash { get; set; } = string.Empty;

	public string CheckedAt { get; set; } = string.Empty;
}
