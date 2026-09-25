using System.Collections.Generic;
using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class TripJournalState
{
	[JsonProperty("active_trip")]
	public TripReceipt ActiveTrip { get; set; }

	[JsonProperty("outbox")]
	public List<TripReceipt> Outbox { get; set; } = new List<TripReceipt>();

	[JsonProperty("events_initialized")]
	public bool EventsInitialized { get; set; }

	[JsonProperty("last_job_delivered")]
	public bool LastJobDelivered { get; set; }

	[JsonProperty("last_job_cancelled")]
	public bool LastJobCancelled { get; set; }

	[JsonProperty("overspeed_since")]
	public string OverspeedSince { get; set; }

	[JsonProperty("last_speed_fine_at")]
	public string LastSpeedFineAt { get; set; }
}
