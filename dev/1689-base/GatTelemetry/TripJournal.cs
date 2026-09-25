using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class TripJournal
{
	private readonly object _sync = new object();

	private readonly string _file;

	private TripJournalState _state;

	private DateTime _lastDiskWrite = DateTime.MinValue;

	private sealed class DamageSnapshot
	{
		public double Cargo = -1.0;
		public double Engine = -1.0;
		public double Transmission = -1.0;
		public double Cabin = -1.0;
		public double Chassis = -1.0;
		public double Wheels = -1.0;
		public double Trailer = -1.0;

		public double TruckOverall
		{
			get
			{
				double max = -1.0;
				double[] values = { Engine, Transmission, Cabin, Chassis, Wheels };
				foreach (double value in values)
				{
					if (value > max)
					{
						max = value;
					}
				}
				return max;
			}
		}
	}

	public int PendingCount
	{
		get
		{
			lock (_sync)
			{
				return _state.Outbox.Count;
			}
		}
	}

	public TripJournal()
	{
		ClientStore.Ensure();
		_file = Path.Combine(ClientStore.DataDir, "trip_journal.json");
		_state = Load();
	}

	public void Observe(JObject telemetry)
	{
		if (telemetry == null)
		{
			return;
		}
		lock (_sync)
		{
			bool flag = BoolAny(telemetry, "gameplay.jobDelivered", "job_delivered");
			bool flag2 = BoolAny(telemetry, "gameplay.jobCancelled", "job_cancelled");
			bool flag3 = false;
			bool flag4 = false;
			if (!_state.EventsInitialized)
			{
				_state.EventsInitialized = true;
				_state.LastJobDelivered = flag;
				_state.LastJobCancelled = flag2;
				Save(force: true);
			}
			else
			{
				flag3 = flag != _state.LastJobDelivered;
				flag4 = flag2 != _state.LastJobCancelled;
				if (flag3 | flag4)
				{
					_state.LastJobDelivered = flag;
					_state.LastJobCancelled = flag2;
				}
			}
			bool flag5 = BoolAny(telemetry, "on_job", "gameplay.onJob");
			string text = TextAny(telemetry, "cargo_name", "job.cargoName", "job.cargo");
			string source = TextAny(telemetry, "source_city", "job.sourceCity");
			string destination = TextAny(telemetry, "destination_city", "job.destinationCity");
			string text2 = TextAny(telemetry, "job_market", "job.market", "market");
			double num = DoubleAny(telemetry, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass");
			double num2 = DoubleAny(telemetry, "job.plannedDistanceKm", "job.planned_distance_km", "planned_distance_km");
			double num3 = DoubleAny(telemetry, "remaining_km");
			if (num3 <= 0.0)
			{
				double num4 = DoubleAny(telemetry, "distance_m", "navigation.estimatedDistance");
				if (num4 > 0.0)
				{
					num3 = num4 / 1000.0;
				}
			}
			if (num3 > 15000.0)
			{
				num3 = -1.0;
			}
			double speedKmh = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));
			DamageSnapshot damage = ReadDamageSnapshot(telemetry);
			double num7 = OdometerKm(telemetry);
			string text3 = TextAny(telemetry, "truck.id", "truck.unitId", "truck.unit_id", "truck.vehicleId", "truck.vehicle_id");
			string text4 = TextAny(telemetry, "truck.make", "truck.manufacturer", "truck.brand", "truck.makeName");
			string text5 = TextAny(telemetry, "truck.model", "truck.modelName", "truck.model_name");
			string text6 = TextAny(telemetry, "truck.licensePlate", "truck.license_plate", "truck.licensePlateNumber", "truck.registrationPlate", "truck.plate");
			string text7 = BuildTruckIdentity(text3, text6, text4, text5);
			if (flag5 && !string.IsNullOrWhiteSpace(text))
			{
				if (_state.ActiveTrip == null || !SameTrip(_state.ActiveTrip, text, source, destination))
				{
					_state.ActiveTrip = new TripReceipt
					{
						TripId = Guid.NewGuid().ToString("N"),
						Cargo = text,
						Source = source,
						Destination = destination,
						Market = text2,
						WeightKg = num,
						PlannedDistanceKm = num2,
						FirstObservedRemainingKm = num3,
						LastObservedRemainingKm = num3,
						StartedObservedAt = DateTime.UtcNow.ToString("o"),
						CargoDamagePct = ((damage.Cargo >= 0.0) ? damage.Cargo : 0.0),
						CargoDamageStartPct = damage.Cargo,
						TruckDamageStartPct = damage.TruckOverall,
						TruckDamageMaxPct = damage.TruckOverall,
						TruckEngineDamageStartPct = damage.Engine,
						TruckEngineDamageMaxPct = damage.Engine,
						TruckTransmissionDamageStartPct = damage.Transmission,
						TruckTransmissionDamageMaxPct = damage.Transmission,
						TruckCabinDamageStartPct = damage.Cabin,
						TruckCabinDamageMaxPct = damage.Cabin,
						TruckChassisDamageStartPct = damage.Chassis,
						TruckChassisDamageMaxPct = damage.Chassis,
						TruckWheelsDamageStartPct = damage.Wheels,
						TruckWheelsDamageMaxPct = damage.Wheels,
						TrailerDamageStartPct = damage.Trailer,
						TrailerDamageMaxPct = damage.Trailer,
						TruckId = text3,
						TruckMake = text4,
						TruckModel = text5,
						TruckPlate = text6,
						TruckIdentity = text7,
						StartOdometerKm = ((num7 > 0.0) ? num7 : (-1.0)),
						EndOdometerKm = ((num7 > 0.0) ? num7 : (-1.0)),
						DrivenDistanceKm = 0.0,
						OdometerVerified = (num7 > 0.0),
						LastOdometerObservedAt = DateTime.UtcNow.ToString("o")
					};
					UpdateOdometerAndVehicle(_state.ActiveTrip, num7, text3, text6, text4, text5, text7);
					ApplyModIntegrity(_state.ActiveTrip);
					UpdateSpeedFine(_state.ActiveTrip, speedKmh);
					Save(force: true);
				}
				else
				{
					TripReceipt activeTrip = _state.ActiveTrip;
					if (num > 0.0)
					{
						activeTrip.WeightKg = num;
					}
					if (num2 > 0.0)
					{
						activeTrip.PlannedDistanceKm = num2;
					}
					if (num3 >= 0.0)
					{
						activeTrip.LastObservedRemainingKm = num3;
					}
					if (!string.IsNullOrWhiteSpace(text2))
					{
						activeTrip.Market = text2;
					}
					UpdateDamage(activeTrip, damage);
					UpdateOdometerAndVehicle(activeTrip, num7, text3, text6, text4, text5, text7);
					UpdateSpeedFine(activeTrip, speedKmh);
					Save(force: false);
				}
			}
			if (_state.ActiveTrip != null)
			{
				UpdateDamage(_state.ActiveTrip, damage);
				UpdateOdometerAndVehicle(_state.ActiveTrip, num7, text3, text6, text4, text5, text7);
				ApplyModIntegrity(_state.ActiveTrip);
				if (!flag5)
				{
					ResetOverspeed();
				}
			}
			if (flag3 && _state.ActiveTrip != null)
			{
				TripReceipt done = _state.ActiveTrip;
				done.CompletedAt = DateTime.UtcNow.ToString("o");
				if (done.PlannedDistanceKm <= 0.0)
				{
					done.PlannedDistanceKm = Math.Max(done.FirstObservedRemainingKm, done.LastObservedRemainingKm);
				}
				if (!_state.Outbox.Any((TripReceipt x) => string.Equals(x.TripId, done.TripId, StringComparison.OrdinalIgnoreCase)))
				{
					_state.Outbox.Add(done);
				}
				_state.ActiveTrip = null;
				ResetOverspeed();
				Save(force: true);
			}
			else if (flag4 && _state.ActiveTrip != null)
			{
				_state.ActiveTrip = null;
				ResetOverspeed();
				Save(force: true);
			}
			else if (flag3 | flag4)
			{
				Save(force: true);
			}
		}
	}

	public TripReceipt PeekPending()
	{
		lock (_sync)
		{
			return (_state.Outbox.Count == 0) ? null : Clone(_state.Outbox[0]);
		}
	}

	public void MarkSent(string tripId)
	{
		if (string.IsNullOrWhiteSpace(tripId))
		{
			return;
		}
		lock (_sync)
		{
			_state.Outbox.RemoveAll((TripReceipt x) => string.Equals(x.TripId, tripId, StringComparison.OrdinalIgnoreCase));
			Save(force: true);
		}
	}

	private TripJournalState Load()
	{
		try
		{
			if (!File.Exists(_file))
			{
				return new TripJournalState();
			}
			TripJournalState tripJournalState = JsonConvert.DeserializeObject<TripJournalState>(File.ReadAllText(_file, Encoding.UTF8));
			if (tripJournalState == null)
			{
				tripJournalState = new TripJournalState();
			}
			if (tripJournalState.Outbox == null)
			{
				tripJournalState.Outbox = new List<TripReceipt>();
			}
			return tripJournalState;
		}
		catch (Exception ex)
		{
			ClientStore.Log("trip journal load: " + ex.Message);
			return new TripJournalState();
		}
	}

	private void Save(bool force)
	{
		if (!force && (DateTime.UtcNow - _lastDiskWrite).TotalSeconds < 5.0)
		{
			return;
		}
		try
		{
			ClientStore.Ensure();
			string text = _file + ".tmp";
			File.WriteAllText(text, JsonConvert.SerializeObject(_state, Formatting.Indented), Encoding.UTF8);
			if (File.Exists(_file))
			{
				File.Delete(_file);
			}
			File.Move(text, _file);
			_lastDiskWrite = DateTime.UtcNow;
		}
		catch (Exception ex)
		{
			ClientStore.Log("trip journal save: " + ex.Message);
		}
	}

	private static TripReceipt Clone(TripReceipt t)
	{
		if (t == null)
		{
			return null;
		}
		return JsonConvert.DeserializeObject<TripReceipt>(JsonConvert.SerializeObject(t));
	}

	private static bool SameTrip(TripReceipt t, string cargo, string source, string destination)
	{
		if (t != null && string.Equals((t.Cargo ?? string.Empty).Trim(), (cargo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals((t.Source ?? string.Empty).Trim(), (source ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals((t.Destination ?? string.Empty).Trim(), (destination ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static void ApplyModIntegrity(TripReceipt trip)
	{
		if (trip != null)
		{
			ModIntegrityResult modIntegrityResult = ModIntegrityScanner.Check();
			if (modIntegrityResult == null)
			{
				trip.IntegrityStatus = "unknown";
				trip.IntegrityReason = "scanner_unavailable";
			}
			else if (!string.Equals(trip.IntegrityStatus, "blocked", StringComparison.OrdinalIgnoreCase) || string.Equals(modIntegrityResult.Status, "blocked", StringComparison.OrdinalIgnoreCase))
			{
				trip.IntegrityStatus = modIntegrityResult.Status ?? "unknown";
				trip.IntegrityReason = modIntegrityResult.Reason;
				trip.IntegrityMatches = modIntegrityResult.Matches ?? new string[0];
				trip.IntegrityEvidenceHash = modIntegrityResult.EvidenceHash;
				trip.IntegrityCheckedAt = modIntegrityResult.CheckedAt;
			}
		}
	}

	private static double OdometerKm(JObject m)
	{
		if (!TryDoubleAny(m, out var value, "truck.odometer", "truck.odometerKm", "truck.odometer_km", "odometer", "odometer_km"))
		{
			return -1.0;
		}
		if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0 || value > 50000000.0)
		{
			return -1.0;
		}
		return value;
	}

	private static string BuildTruckIdentity(string id, string plate, string make, string model)
	{
		if (!string.IsNullOrWhiteSpace(id))
		{
			return "id:" + id.Trim();
		}
		if (!string.IsNullOrWhiteSpace(plate))
		{
			return "plate:" + plate.Trim();
		}
		string text = ((make ?? string.Empty).Trim() + "|" + (model ?? string.Empty).Trim()).Trim('|');
		if (!string.IsNullOrWhiteSpace(text))
		{
			return "model:" + text;
		}
		return string.Empty;
	}

	private static void UpdateOdometerAndVehicle(TripReceipt trip, double odometerKm, string truckId, string plate, string make, string model, string identity)
	{
		if (trip == null)
		{
			return;
		}
		if (!string.IsNullOrWhiteSpace(truckId))
		{
			trip.TruckId = truckId;
		}
		if (!string.IsNullOrWhiteSpace(plate))
		{
			trip.TruckPlate = plate;
		}
		if (!string.IsNullOrWhiteSpace(make))
		{
			trip.TruckMake = make;
		}
		if (!string.IsNullOrWhiteSpace(model))
		{
			trip.TruckModel = model;
		}
		if (!string.IsNullOrWhiteSpace(identity))
		{
			if (string.IsNullOrWhiteSpace(trip.TruckIdentity))
			{
				trip.TruckIdentity = identity;
			}
			else if (!string.Equals(trip.TruckIdentity, identity, StringComparison.OrdinalIgnoreCase))
			{
				trip.VehicleChanged = true;
				trip.OdometerVerified = false;
			}
		}
		if (odometerKm <= 0.0)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (trip.StartOdometerKm <= 0.0 || trip.EndOdometerKm <= 0.0)
		{
			trip.StartOdometerKm = odometerKm;
			trip.EndOdometerKm = odometerKm;
			trip.DrivenDistanceKm = 0.0;
			trip.OdometerVerified = !trip.VehicleChanged;
			trip.LastOdometerObservedAt = utcNow.ToString("o");
			return;
		}
		double num = 1.0;
		if (!string.IsNullOrWhiteSpace(trip.LastOdometerObservedAt) && DateTime.TryParse(trip.LastOdometerObservedAt, null, DateTimeStyles.RoundtripKind, out var result))
		{
			num = Math.Max(0.2, (utcNow - result.ToUniversalTime()).TotalSeconds);
		}
		double num2 = odometerKm - trip.EndOdometerKm;
		double num3 = 220.0 * num / 3600.0 + 0.25;
		if (num2 < -0.2 || num2 > num3)
		{
			trip.OdometerDiscontinuity = true;
			trip.OdometerVerified = false;
		}
		else if (num2 > 0.0)
		{
			trip.DrivenDistanceKm += num2;
		}
		trip.EndOdometerKm = odometerKm;
		trip.LastOdometerObservedAt = utcNow.ToString("o");
	}

	private void UpdateSpeedFine(TripReceipt trip, double speedKmh)
	{
		if (trip != null)
		{
			DateTime utcNow = DateTime.UtcNow;
			DateTime result;
			DateTime result2;
			if (double.IsNaN(speedKmh) || double.IsInfinity(speedKmh) || speedKmh > 200.0)
			{
				ClientStore.Log("telemetria ignorada: pico de velocidade invalido");
			}
			else if (speedKmh <= 91.0)
			{
				ResetOverspeed();
			}
			else if (string.IsNullOrWhiteSpace(_state.OverspeedSince) || !DateTime.TryParse(_state.OverspeedSince, null, DateTimeStyles.RoundtripKind, out result))
			{
				_state.OverspeedSince = utcNow.ToString("o");
			}
			else if (!((utcNow - result.ToUniversalTime()).TotalSeconds < 5.0) && (string.IsNullOrWhiteSpace(_state.LastSpeedFineAt) || !DateTime.TryParse(_state.LastSpeedFineAt, null, DateTimeStyles.RoundtripKind, out result2) || (utcNow - result2.ToUniversalTime()).TotalMinutes >= 5.0))
			{
				trip.SpeedFines++;
				_state.LastSpeedFineAt = utcNow.ToString("o");
				ClientStore.Log("multa GAT por excesso de velocidade: " + speedKmh.ToString("0", CultureInfo.InvariantCulture) + " km/h");
			}
		}
	}

	private void ResetOverspeed()
	{
		_state.OverspeedSince = null;
		_state.LastSpeedFineAt = null;
	}

	private static void UpdateDamage(TripReceipt trip, DamageSnapshot damage)
	{
		if (trip == null || damage == null)
		{
			return;
		}

		trip.CargoDamageStartPct = DamageStart(damage.Cargo, trip.CargoDamageStartPct);
		trip.CargoDamagePct = DamageMax(damage.Cargo, trip.CargoDamagePct);
		trip.TruckDamageStartPct = DamageStart(damage.TruckOverall, trip.TruckDamageStartPct);
		trip.TruckDamageMaxPct = DamageMax(damage.TruckOverall, trip.TruckDamageMaxPct);
		trip.TruckEngineDamageStartPct = DamageStart(damage.Engine, trip.TruckEngineDamageStartPct);
		trip.TruckEngineDamageMaxPct = DamageMax(damage.Engine, trip.TruckEngineDamageMaxPct);
		trip.TruckTransmissionDamageStartPct = DamageStart(damage.Transmission, trip.TruckTransmissionDamageStartPct);
		trip.TruckTransmissionDamageMaxPct = DamageMax(damage.Transmission, trip.TruckTransmissionDamageMaxPct);
		trip.TruckCabinDamageStartPct = DamageStart(damage.Cabin, trip.TruckCabinDamageStartPct);
		trip.TruckCabinDamageMaxPct = DamageMax(damage.Cabin, trip.TruckCabinDamageMaxPct);
		trip.TruckChassisDamageStartPct = DamageStart(damage.Chassis, trip.TruckChassisDamageStartPct);
		trip.TruckChassisDamageMaxPct = DamageMax(damage.Chassis, trip.TruckChassisDamageMaxPct);
		trip.TruckWheelsDamageStartPct = DamageStart(damage.Wheels, trip.TruckWheelsDamageStartPct);
		trip.TruckWheelsDamageMaxPct = DamageMax(damage.Wheels, trip.TruckWheelsDamageMaxPct);
		trip.TrailerDamageStartPct = DamageStart(damage.Trailer, trip.TrailerDamageStartPct);
		trip.TrailerDamageMaxPct = DamageMax(damage.Trailer, trip.TrailerDamageMaxPct);
	}

	private static double DamageStart(double current, double start)
	{
		return current >= 0.0 && start < 0.0 ? current : start;
	}

	private static double DamageMax(double current, double max)
	{
		return current >= 0.0 && (max < 0.0 || current > max) ? current : max;
	}

	private static double NormalizePercent(double v)
	{
		if (double.IsNaN(v) || double.IsInfinity(v) || v < 0.0)
		{
			return -1.0;
		}
		if (v <= 1.01)
		{
			v *= 100.0;
		}
		if (v > 100.0)
		{
			return -1.0;
		}
		return v;
	}

	private static DamageSnapshot ReadDamageSnapshot(JObject m)
	{
		DamageSnapshot damage = new DamageSnapshot();
		if (m == null)
		{
			return damage;
		}

		damage.Engine = DamagePercentAny(m, "truck.wearEngine", "truck.wear_engine", "truck.wear.engine", "truck.engineWear", "truck.engineDamage");
		damage.Transmission = DamagePercentAny(m, "truck.wearTransmission", "truck.wear_transmission", "truck.wear.transmission", "truck.transmissionWear", "truck.transmissionDamage");
		damage.Cabin = DamagePercentAny(m, "truck.wearCabin", "truck.wear_cabin", "truck.wear.cabin", "truck.cabinWear", "truck.cabinDamage");
		damage.Chassis = DamagePercentAny(m, "truck.wearChassis", "truck.wear_chassis", "truck.wear.chassis", "truck.chassisWear", "truck.chassisDamage");
		damage.Wheels = DamagePercentAny(m, "truck.wearWheels", "truck.wear_wheels", "truck.wear.wheels", "truck.wheelsWear", "truck.wheelsDamage");

		JToken trailer = AttachedTrailer(m);
		if (trailer != null)
		{
			damage.Cargo = DamagePercentToken(trailer["cargoDamage"] ?? trailer["CargoDamage"]);
			double trailerBody = DamagePercentToken(trailer["wearBody"] ?? trailer["WearBody"]);
			double trailerChassis = DamagePercentToken(trailer["wearChassis"] ?? trailer["WearChassis"]);
			double trailerWheels = DamagePercentToken(trailer["wearWheels"] ?? trailer["WearWheels"]);
			damage.Trailer = MaxValid(trailerBody, trailerChassis, trailerWheels);
		}

		if (damage.Cargo < 0.0)
		{
			damage.Cargo = DamagePercentAny(m, "cargo_damage_pct", "cargoDamage", "cargo.damage", "job.cargoDamage", "job.cargo_damage", "trailer.cargoDamage", "trailer.cargo_damage", "trailer.damageCargo");
		}
		if (damage.Trailer < 0.0)
		{
			damage.Trailer = DamagePercentAny(m, "trailer.wear", "trailer.damage", "trailerDamage", "trailer_damage", "trailer.wearChassis");
		}
		return damage;
	}

	private static JToken AttachedTrailer(JObject m)
	{
		JArray trailers = m?["trailers"] as JArray ?? m?["Trailers"] as JArray;
		if (trailers == null)
		{
			return null;
		}
		foreach (JToken trailer in trailers)
		{
			JToken attached = trailer?["attached"] ?? trailer?["Attached"];
			if (attached != null && attached.Type == JTokenType.Boolean && attached.Value<bool>())
			{
				return trailer;
			}
		}
		return null;
	}

	private static double DamagePercentToken(JToken token)
	{
		if (token == null || token.Type == JTokenType.Null)
		{
			return -1.0;
		}
		double value;
		if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
		{
			value = token.Value<double>();
		}
		else if (!double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
		{
			return -1.0;
		}
		return NormalizePercent(value);
	}

	private static double DamagePercentAny(JObject m, params string[] paths)
	{
		if (TryDoubleAny(m, out var value, paths))
		{
			return NormalizePercent(value);
		}
		return -1.0;
	}

	private static double MaxValid(params double[] values)
	{
		double max = -1.0;
		foreach (double value in values)
		{
			if (value >= 0.0 && value > max)
			{
				max = value;
			}
		}
		return max;
	}

	private static bool TryDoubleAny(JObject m, out double value, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = m.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null)
			{
				if (jToken.Type == JTokenType.Float || jToken.Type == JTokenType.Integer)
				{
					value = jToken.Value<double>();
					return true;
				}
				if (double.TryParse(jToken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
				{
					value = result;
					return true;
				}
			}
		}
		value = 0.0;
		return false;
	}

	private static string TextAny(JObject m, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = m.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null)
			{
				string text = jToken.ToString();
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
			}
		}
		return string.Empty;
	}

	private static double DoubleAny(JObject m, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = m.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null)
			{
				if (jToken.Type == JTokenType.Float || jToken.Type == JTokenType.Integer)
				{
					return jToken.Value<double>();
				}
				if (double.TryParse(jToken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
				{
					return result;
				}
			}
		}
		return 0.0;
	}

	private static bool BoolAny(JObject m, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = m.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null)
			{
				if (jToken.Type == JTokenType.Boolean)
				{
					return jToken.Value<bool>();
				}
				if (bool.TryParse(jToken.ToString(), out var result))
				{
					return result;
				}
			}
		}
		return false;
	}
}
