using System.Collections.Generic;

namespace GatTelemetry;

internal sealed class PlayersResult
{
	public bool Ok { get; set; }

	public List<string> Players { get; set; } = new List<string>();
}
