namespace GatTelemetry;

internal sealed class ServerInfo
{
	public bool Reachable { get; set; }

	public bool Supported { get; set; }

	public bool Online { get; set; }

	public string ServerName { get; set; }

	public string SessionId { get; set; }

	public int Players { get; set; }

	public int MaxPlayers { get; set; }
}
