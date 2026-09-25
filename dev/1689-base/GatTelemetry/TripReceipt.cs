using Newtonsoft.Json;

namespace GatTelemetry;

internal sealed class TripReceipt
{
	[JsonProperty("trip_id")]
	public string TripId { get; set; }

	[JsonProperty("cargo")]
	public string Cargo { get; set; }

	[JsonProperty("source")]
	public string Source { get; set; }

	[JsonProperty("destination")]
	public string Destination { get; set; }

	[JsonProperty("market")]
	public string Market { get; set; }

	[JsonProperty("weight_kg")]
	public double WeightKg { get; set; }

	[JsonProperty("planned_distance_km")]
	public double PlannedDistanceKm { get; set; }

	[JsonProperty("first_observed_remaining_km")]
	public double FirstObservedRemainingKm { get; set; }

	[JsonProperty("last_observed_remaining_km")]
	public double LastObservedRemainingKm { get; set; }

	[JsonProperty("started_observed_at")]
	public string StartedObservedAt { get; set; }

	[JsonProperty("completed_at")]
	public string CompletedAt { get; set; }

	[JsonProperty("speed_fines")]
	public int SpeedFines { get; set; }

	[JsonProperty("cargo_damage_pct")]
	public double CargoDamagePct { get; set; }

	[JsonProperty("cargo_damage_start_pct")]
	public double CargoDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_damage_start_pct")]
	public double TruckDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_damage_max_pct")]
	public double TruckDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_engine_damage_start_pct")]
	public double TruckEngineDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_engine_damage_max_pct")]
	public double TruckEngineDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_transmission_damage_start_pct")]
	public double TruckTransmissionDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_transmission_damage_max_pct")]
	public double TruckTransmissionDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_cabin_damage_start_pct")]
	public double TruckCabinDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_cabin_damage_max_pct")]
	public double TruckCabinDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_chassis_damage_start_pct")]
	public double TruckChassisDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_chassis_damage_max_pct")]
	public double TruckChassisDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_wheels_damage_start_pct")]
	public double TruckWheelsDamageStartPct { get; set; } = -1.0;

	[JsonProperty("truck_wheels_damage_max_pct")]
	public double TruckWheelsDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("trailer_damage_start_pct")]
	public double TrailerDamageStartPct { get; set; } = -1.0;

	[JsonProperty("trailer_damage_max_pct")]
	public double TrailerDamageMaxPct { get; set; } = -1.0;

	[JsonProperty("truck_id")]
	public string TruckId { get; set; }

	[JsonProperty("truck_make")]
	public string TruckMake { get; set; }

	[JsonProperty("truck_model")]
	public string TruckModel { get; set; }

	[JsonProperty("truck_plate")]
	public string TruckPlate { get; set; }

	[JsonProperty("truck_identity")]
	public string TruckIdentity { get; set; }

	[JsonProperty("start_odometer_km")]
	public double StartOdometerKm { get; set; } = -1.0;

	[JsonProperty("end_odometer_km")]
	public double EndOdometerKm { get; set; } = -1.0;

	[JsonProperty("driven_distance_km")]
	public double DrivenDistanceKm { get; set; }

	[JsonProperty("odometer_verified")]
	public bool OdometerVerified { get; set; }

	[JsonProperty("vehicle_changed")]
	public bool VehicleChanged { get; set; }

	[JsonProperty("odometer_discontinuity")]
	public bool OdometerDiscontinuity { get; set; }

	[JsonProperty("last_odometer_observed_at")]
	public string LastOdometerObservedAt { get; set; }

	[JsonProperty("integrity_status")]
	public string IntegrityStatus { get; set; } = "unknown";

	[JsonProperty("integrity_reason")]
	public string IntegrityReason { get; set; }

	[JsonProperty("integrity_matches")]
	public string[] IntegrityMatches { get; set; } = new string[0];

	[JsonProperty("integrity_evidence_hash")]
	public string IntegrityEvidenceHash { get; set; }

	[JsonProperty("integrity_checked_at")]
	public string IntegrityCheckedAt { get; set; }
}
