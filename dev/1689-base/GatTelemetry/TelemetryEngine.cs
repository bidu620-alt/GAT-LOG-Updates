using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class TelemetryEngine : IDisposable
{
	private const string TruckUrl = "http://127.0.0.1:31377/api/ets2/telemetry";

	private readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromMilliseconds(1400.0)
	};

	private DateTime _lastStartAttempt = DateTime.MinValue;

	public async Task<JObject> ReadAsync()
	{
		try
		{
			JObject jObject = JObject.Parse(await _http.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry").ConfigureAwait(continueOnCapturedContext: false));
			Normalize(jObject);
			return jObject;
		}
		catch
		{
			TryStartTruckSimGps();
			return null;
		}
	}

	private void TryStartTruckSimGps()
	{
		if ((DateTime.UtcNow - _lastStartAttempt).TotalSeconds < 10.0)
		{
			return;
		}
		_lastStartAttempt = DateTime.UtcNow;
		try
		{
			Process[] processesByName = Process.GetProcessesByName("TruckSimGPS_Server");
			try
			{
				if (processesByName.Length != 0)
				{
					return;
				}
			}
			finally
			{
				Process[] array = processesByName;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].Dispose();
				}
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string folderPath3 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string[] array2 = new string[4]
			{
				Path.Combine(folderPath, "Programs", "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
				Path.Combine(folderPath, "Programs", "TruckSim GPS", "TruckSimGPS_Server.exe"),
				Path.Combine(folderPath2, "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
				Path.Combine(folderPath3, "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe")
			};
			foreach (string text in array2)
			{
				if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
				{
					Process.Start(new ProcessStartInfo(text)
					{
						UseShellExecute = true,
						WorkingDirectory = Path.GetDirectoryName(text)
					});
					ClientStore.Log("TruckSim GPS iniciado automaticamente: " + text);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("Falha ao iniciar TruckSim GPS: " + ex.Message);
		}
	}

	public static void Normalize(JObject m)
	{
		if (m != null)
		{
			if (TryAny(m, out var value, "job.cargoMass", "Job.CargoMass", "mass_kg", "cargoMass", "cargo_mass", "cargoMassKg", "cargo_mass_kg", "cargoWeight", "cargo_weight", "weight_kg", "job.mass", "job.mass_kg", "job.cargo_mass", "job.cargoMassKg", "job.cargoWeight", "job.weight", "job.cargo.mass", "job.cargo.mass_kg", "job.cargo.massKg", "job.cargo.weight", "cargo.mass", "cargo.mass_kg", "cargo.massKg", "cargo.weight", "trailer.mass", "trailerMass", "trailer.cargoMass", "trailer.cargo_mass", "game.job.cargoMass", "game.job.mass") && value > 0.0)
			{
				m["mass_kg"] = value;
				m["cargoMass"] = value;
				m["cargo_mass"] = value;
			}
			if (TryAny(m, out var value2, "navigation.estimatedDistance", "navigation.estimated_distance"))
			{
				m["distance_m"] = value2;
				m["remaining_km"] = value2 / 1000.0;
			}
			if (TryAny(m, out var value3, "truck.speedKmh", "truck.speed_kmh", "speed_kmh", "truck.speed"))
			{
				m["speed_kmh"] = Math.Abs(value3);
			}
			CopyAlias(m, "job.cargo", "cargo_name");
			CopyAlias(m, "job.cargoName", "cargo_name", onlyIfMissing: true);
			CopyAlias(m, "job.cargoId", "cargo_id");
			CopyAlias(m, "Job.CargoId", "cargo_id", onlyIfMissing: true);
			CopyAlias(m, "job.cargo.id", "cargo_id", onlyIfMissing: true);
			CopyAlias(m, "job.sourceCity", "source_city");
			CopyAlias(m, "job.sourceCityId", "source_city_id");
			CopyAlias(m, "Job.SourceCityId", "source_city_id", onlyIfMissing: true);
			CopyAlias(m, "job.destinationCity", "destination_city");
			CopyAlias(m, "job.destinationCityId", "destination_city_id");
			CopyAlias(m, "Job.DestinationCityId", "destination_city_id", onlyIfMissing: true);
			CopyAlias(m, "job.market", "job_market");
			CopyAlias(m, "truck.placement.x", "map_x");
			CopyAlias(m, "truck.placement.z", "map_z");
			CopyAlias(m, "truck.placement.heading", "map_heading");
			CopyAlias(m, "truck.make", "truck_make");
			CopyAlias(m, "truck.model", "truck_model");
			CopyAlias(m, "job.plannedDistanceKm", "planned_distance_km");
			CopyAlias(m, "Job.PlannedDistanceKm", "planned_distance_km", onlyIfMissing: true);
			CopyAlias(m, "gameplay.onJob", "on_job");
			AddDamageAliases(m);
		}
	}

	private static void AddDamageAliases(JObject m)
	{
		double truckMax = -1.0;
		AddTruckDamageAlias(m, "truck_engine_damage_pct", ref truckMax, "truck.wearEngine", "truck.engineWear", "truck.engineDamage");
		AddTruckDamageAlias(m, "truck_transmission_damage_pct", ref truckMax, "truck.wearTransmission", "truck.transmissionWear", "truck.transmissionDamage");
		AddTruckDamageAlias(m, "truck_cabin_damage_pct", ref truckMax, "truck.wearCabin", "truck.cabinWear", "truck.cabinDamage");
		AddTruckDamageAlias(m, "truck_chassis_damage_pct", ref truckMax, "truck.wearChassis", "truck.chassisWear", "truck.chassisDamage");
		AddTruckDamageAlias(m, "truck_wheels_damage_pct", ref truckMax, "truck.wearWheels", "truck.wheelsWear", "truck.wheelsDamage");
		if (truckMax >= 0.0)
		{
			m["truck_damage_pct"] = truckMax;
		}

		JToken trailer = AttachedTrailer(m);
		if (trailer != null)
		{
			double cargo = DamagePercentNumber(trailer["cargoDamage"] ?? trailer["CargoDamage"]);
			if (cargo >= 0.0)
			{
				m["cargo_damage_pct"] = cargo;
			}
			double body = DamagePercentNumber(trailer["wearBody"] ?? trailer["WearBody"]);
			double chassis = DamagePercentNumber(trailer["wearChassis"] ?? trailer["WearChassis"]);
			double wheels = DamagePercentNumber(trailer["wearWheels"] ?? trailer["WearWheels"]);
			double trailerMax = Math.Max(body, Math.Max(chassis, wheels));
			if (trailerMax >= 0.0)
			{
				m["trailer_damage_pct"] = trailerMax;
			}
		}
	}

	private static void AddTruckDamageAlias(JObject m, string alias, ref double max, params string[] paths)
	{
		if (!TryAny(m, out var raw, paths))
		{
			return;
		}
		double pct = DamagePercentNumber(raw);
		if (pct < 0.0)
		{
			return;
		}
		m[alias] = pct;
		if (pct > max)
		{
			max = pct;
		}
	}

	private static double DamagePercentNumber(double raw)
	{
		if (double.IsNaN(raw) || double.IsInfinity(raw) || raw < 0.0)
		{
			return -1.0;
		}
		if (raw <= 1.0001)
		{
			raw *= 100.0;
		}
		return Math.Max(0.0, Math.Min(100.0, raw));
	}

	private static double DamagePercentNumber(JToken token)
	{
		return TryDamageValue(token, out var raw) ? DamagePercentNumber(raw) : -1.0;
	}

	public static TelemetryDisplay BuildDisplay(JObject m)
	{
		TelemetryDisplay telemetryDisplay = new TelemetryDisplay();
		if (m == null)
		{
			return telemetryDisplay;
		}
		string text = TextAny(m, "cargo_name", "job.cargo", "job.cargoName");
		if (!string.IsNullOrWhiteSpace(text))
		{
			telemetryDisplay.Cargo = text;
		}
		string text2 = TextAny(m, "source_city", "job.sourceCity");
		string text3 = TextAny(m, "destination_city", "job.destinationCity");
		if (!string.IsNullOrWhiteSpace(text2) || !string.IsNullOrWhiteSpace(text3))
		{
			telemetryDisplay.Route = (text2 ?? "?") + " → " + (text3 ?? "?");
		}
		if (TryAny(m, out var value, "remaining_km"))
		{
			telemetryDisplay.Distance = value.ToString("0.0", CultureInfo.InvariantCulture) + " km";
		}
		else if (TryAny(m, out value, "distance_m"))
		{
			telemetryDisplay.Distance = (value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " km";
		}
		if (TryAny(m, out var value2, "speed_kmh"))
		{
			telemetryDisplay.Speed = Math.Abs(value2).ToString("0", CultureInfo.InvariantCulture) + " km/h";
		}
		if (TryAny(m, out var value3, "mass_kg", "cargo_mass", "cargoMass") && value3 > 0.0)
		{
			telemetryDisplay.Weight = (value3 / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " t";
		}

		// TruckSim GPS REST v5: carga e desgaste do reboque ficam em trailers[].
		// Mantemos os aliases do caminhão para aceitar a extensão do servidor que expõe
		// WearEngine/WearTransmission/WearCabin/WearChassis/WearWheels.
		telemetryDisplay.CargoDamage = DamageTextFromAttachedTrailer(m, "cargoDamage");
		if (telemetryDisplay.CargoDamage == "—")
		{
			telemetryDisplay.CargoDamage = DamageText(m, "job.cargoDamage", "job.cargo_damage", "cargoDamage", "cargo_damage", "damageCargo");
		}
		telemetryDisplay.EngineDamage = DamageText(m, "truck.wearEngine", "truck.engineWear", "truck.engineDamage", "truck.engine_damage", "engineWear", "engineDamage");
		telemetryDisplay.TransmissionDamage = DamageText(m, "truck.wearTransmission", "truck.transmissionWear", "truck.transmissionDamage", "truck.transmission_damage", "transmissionWear", "transmissionDamage");
		telemetryDisplay.CabinDamage = DamageText(m, "truck.wearCabin", "truck.cabinWear", "truck.cabinDamage", "truck.cabin_damage", "cabinWear", "cabinDamage");
		telemetryDisplay.ChassisDamage = DamageText(m, "truck.wearChassis", "truck.chassisWear", "truck.chassisDamage", "truck.chassis_damage", "chassisWear", "chassisDamage");
		telemetryDisplay.WheelsDamage = DamageText(m, "truck.wearWheels", "truck.wheelsWear", "truck.wheelsDamage", "truck.wheels_damage", "wheelsWear", "wheelsDamage");
		telemetryDisplay.TrailerDamage = TrailerOverallDamageText(m);
		return telemetryDisplay;
	}

	private static JToken AttachedTrailer(JObject m)
	{
		JArray trailers = m["trailers"] as JArray ?? m["Trailers"] as JArray;
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

	private static string DamageTextFromAttachedTrailer(JObject m, string field)
	{
		JToken trailer = AttachedTrailer(m);
		if (trailer == null)
		{
			return "—";
		}
		JToken token = trailer[field] ?? trailer[char.ToUpperInvariant(field[0]) + field.Substring(1)];
		if (!TryDamageValue(token, out var value))
		{
			return "—";
		}
		return FormatDamage(value);
	}

	private static string TrailerOverallDamageText(JObject m)
	{
		JToken trailer = AttachedTrailer(m);
		if (trailer == null)
		{
			return DamageText(m, "trailer.wear", "trailer.damage", "trailerDamage", "trailer_damage", "trailer.wearChassis");
		}
		double max = -1.0;
		string[] fields = { "wearBody", "wearChassis", "wearWheels" };
		foreach (string field in fields)
		{
			JToken token = trailer[field] ?? trailer[char.ToUpperInvariant(field[0]) + field.Substring(1)];
			if (TryDamageValue(token, out var value) && value > max)
			{
				max = value;
			}
		}
		return max < 0.0 ? "—" : FormatDamage(max);
	}

	private static bool TryDamageValue(JToken token, out double value)
	{
		value = 0.0;
		if (token == null || token.Type == JTokenType.Null)
		{
			return false;
		}
		if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
		{
			value = token.Value<double>();
			return true;
		}
		return double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
	}

	private static string FormatDamage(double value)
	{
		// SCS usa 0..1; alguns adaptadores já usam 0..100.
		if (value >= 0.0 && value <= 1.0001)
		{
			value *= 100.0;
		}
		value = Math.Max(0.0, Math.Min(100.0, value));
		string format = value > 0.0 && value < 1.0 ? "0.00" : "0.0";
		return value.ToString(format, CultureInfo.InvariantCulture) + "%";
	}

	private static string DamageText(JObject m, params string[] paths)
	{
		if (!TryAny(m, out var value, paths))
		{
			return "—";
		}
		return FormatDamage(value);
	}

	private static void CopyAlias(JObject m, string source, string destination, bool onlyIfMissing = false)
	{
		if (!onlyIfMissing || m[destination] == null)
		{
			JToken jToken = m.SelectToken(source, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null)
			{
				m[destination] = jToken.DeepClone();
			}
		}
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

	private static bool TryAny(JObject m, out double value, params string[] paths)
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

	public void Dispose()
	{
		_http.Dispose();
	}
}
