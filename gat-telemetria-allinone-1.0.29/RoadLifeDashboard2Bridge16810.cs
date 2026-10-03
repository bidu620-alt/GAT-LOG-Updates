using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private Process _roadLifeDashboard2Process;

    private static string RoadLifeDashboard2Root()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoadLifeDashboard2");
    }

    private static string RoadLifeDashboard2Script()
    {
        return Path.Combine(RoadLifeDashboard2Root(), "Dashboard2.ps1");
    }

    private static string RoadLifeDashboard2TelemetryFile()
    {
        return Path.Combine(RoadLifeDashboard2Root(), "data", "telemetry.json");
    }

    private void RoadLifeDashboard2Initialize()
    {
        FormClosed += delegate { try { CloseRoadLifeDashboard2(); } catch { } };
    }

    private bool RoadLifeDashboard2Running()
    {
        try { return _roadLifeDashboard2Process != null && !_roadLifeDashboard2Process.HasExited; }
        catch { return false; }
    }

    private void ToggleRoadLifeDashboard2()
    {
        if (RoadLifeDashboard2Running()) CloseRoadLifeDashboard2();
        else OpenRoadLifeDashboard2();
    }

    private void OpenRoadLifeDashboard2()
    {
        try
        {
            if (RoadLifeDashboard2Running()) return;
            string script = RoadLifeDashboard2Script();
            if (!File.Exists(script))
            {
                MessageBox.Show(
                    "RoadLife Dashboard2 não foi encontrado.\r\n\r\n" + RoadLifeDashboard2Root(),
                    "ROADLIFE DASHBOARD2",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RoadLifeDashboard2TelemetryFile()));
            _roadLifeDashboard2Process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"",
                WorkingDirectory = RoadLifeDashboard2Root(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            ClientStore.Log("RoadLife Dashboard2 aberto pelo GAT Telemetria.");
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 abrir: " + ex.Message);
            MessageBox.Show(ex.Message, "ROADLIFE DASHBOARD2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CloseRoadLifeDashboard2()
    {
        try
        {
            if (_roadLifeDashboard2Process != null && !_roadLifeDashboard2Process.HasExited)
            {
                try { _roadLifeDashboard2Process.CloseMainWindow(); } catch { }
                try
                {
                    if (!_roadLifeDashboard2Process.WaitForExit(700))
                        _roadLifeDashboard2Process.Kill();
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            try { _roadLifeDashboard2Process?.Dispose(); } catch { }
            _roadLifeDashboard2Process = null;
        }
    }

    // O JObject chega do mesmo ciclo de captura usado pelo GAT Telemetria.
    // O Dashboard2 não acessa TruckSim GPS, não abre servidor e não capta telemetria.
    private void RoadLifeDashboard2Push(JObject tele)
    {
        if (tele == null || !RoadLifeDashboard2Running()) return;
        try
        {
            JObject payload = RoadLifeDashboard2BuildPayload(tele);
            string file = RoadLifeDashboard2TelemetryFile();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp";
            File.WriteAllText(temp, payload.ToString(Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(file))
            {
                try
                {
                    File.Replace(temp, file, null);
                    return;
                }
                catch { }
            }
            File.Copy(temp, file, true);
            try { File.Delete(temp); } catch { }
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 dados: " + ex.Message);
        }
    }

    private static JObject RoadLifeDashboard2BuildPayload(JObject tele)
    {
        string cargo = RoadLifeText(tele, "cargo_name", "job.cargoName", "job.cargo", "Job.CargoName");
        string source = RoadLifeText(tele, "source_city", "job.sourceCity", "Job.SourceCity");
        string destination = RoadLifeText(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");

        double massKg = RoadLifeNumber(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
        double remaining = RoadLifeNumber(tele, "remaining_km", "remainingKm", "navigation.estimatedDistance", "Navigation.EstimatedDistance");
        double planned = RoadLifeNumber(tele, "planned_distance_km", "plannedDistanceKm", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
        double travelled = !double.IsNaN(planned) && !double.IsNaN(remaining) ? Math.Max(0, planned - remaining) : 0;

        double fuel = RoadLifeNumber(tele, "truck.fuel", "Truck.Fuel", "fuel", "fuelAmount", "fuelLiters");
        double fuelCapacity = RoadLifeNumber(tele, "truck.fuelCapacity", "Truck.FuelCapacity", "fuelCapacity", "fuel_capacity");
        double fuelPct = !double.IsNaN(fuel) && !double.IsNaN(fuelCapacity) && fuelCapacity > 0
            ? fuel / fuelCapacity * 100.0
            : RoadLifeNumber(tele, "truck.fuelPercent", "fuelPercent", "fuel_percent");

        double speed = RoadLifeNumber(tele, "truck.speed", "Truck.Speed", "speed", "speed_kmh");
        double temperature = RoadLifeNumber(tele, "truck.waterTemperature", "Truck.WaterTemperature", "waterTemperature", "water_temperature", "truck.engineTemperature");
        double speedLimit = RoadLifeNumber(tele, "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit", "speed_limit_kmh");
        double cruiseSpeed = RoadLifeNumber(tele, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed", "cruiseControlSpeed", "cruise_speed");
        bool cruise = RoadLifeBool(tele, "truck.cruiseControl", "truck.cruiseControlOn", "cruiseControl", "cruise")
            || (!double.IsNaN(cruiseSpeed) && cruiseSpeed > 0.1);
        double odometer = RoadLifeNumber(tele, "truck.odometer", "Truck.Odometer", "odometer", "odometerKm", "odometer_km");

        string model = RoadLifeText(tele, "truck.model", "Truck.Model", "truck.name", "Truck.Name", "truckModel", "truck_model");
        string make = RoadLifeText(tele, "truck.make", "Truck.Make", "truck.brand", "Truck.Brand", "truckMake");
        if (string.IsNullOrWhiteSpace(model)) model = make;
        else if (!string.IsNullOrWhiteSpace(make) && model.IndexOf(make, StringComparison.OrdinalIgnoreCase) < 0) model = make + " " + model;

        double cargoDamage = RoadLifeDamagePct(RoadLifeNumber(tele, "cargoDamage", "cargo_damage", "job.cargoDamage", "Job.CargoDamage"));
        double trailerDamage = RoadLifeDamagePct(RoadLifeNumber(tele, "trailerDamage", "trailer_damage", "trailer.wear", "trailer.damage"));
        double truckDamage = RoadLifeTruckDamagePct(tele);

        return new JObject
        {
            ["online"] = true,
            ["route"] = new JObject
            {
                ["source"] = string.IsNullOrWhiteSpace(source) ? "—" : source,
                ["destination"] = string.IsNullOrWhiteSpace(destination) ? "—" : destination,
                ["distance_remaining_km"] = RoadLifeZero(remaining),
                ["distance_travelled_km"] = RoadLifeZero(travelled)
            },
            ["cargo"] = new JObject
            {
                ["name"] = string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo,
                ["weight_t"] = double.IsNaN(massKg) ? 0.0 : Math.Max(0, massKg / 1000.0)
            },
            ["fuel"] = new JObject
            {
                ["current_l"] = RoadLifeZero(fuel),
                ["capacity_l"] = RoadLifeZero(fuelCapacity),
                ["percent"] = double.IsNaN(fuelPct) ? 0.0 : Math.Max(0, Math.Min(100, fuelPct))
            },
            ["truck"] = new JObject
            {
                ["model"] = string.IsNullOrWhiteSpace(model) ? "—" : model,
                ["gear"] = RoadLifeGear(tele),
                ["temperature_c"] = RoadLifeZero(temperature),
                ["speed_kmh"] = RoadLifeZero(Math.Abs(speed)),
                ["speed_limit_kmh"] = RoadLifeZero(speedLimit),
                ["cruise"] = cruise,
                ["odometer_km"] = RoadLifeZero(odometer)
            },
            ["eta"] = RoadLifeEta(tele),
            ["damage"] = new JObject
            {
                ["truck_pct"] = truckDamage,
                ["trailer_pct"] = trailerDamage,
                ["cargo_pct"] = cargoDamage
            }
        };
    }

    private static string RoadLifeText(JObject root, params string[] paths)
    {
        if (root == null) return string.Empty;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken t = root.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                string s = (Convert.ToString(t, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                if (s.Length > 0) return s;
            }
            catch { }
        }
        return string.Empty;
    }

    private static double RoadLifeNumber(JObject root, params string[] paths)
    {
        double value;
        return double.TryParse(RoadLifeText(root, paths), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            ? value : double.NaN;
    }

    private static bool RoadLifeBool(JObject root, params string[] paths)
    {
        string s = RoadLifeText(root, paths).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes" || s == "sim" || s == "on";
    }

    private static double RoadLifeZero(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
    }

    private static double RoadLifeDamagePct(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) return 0.0;
        if (value <= 1.5) value *= 100.0;
        return Math.Max(0, Math.Min(100, value));
    }

    private static double RoadLifeTruckDamagePct(JObject tele)
    {
        string[][] fields =
        {
            new[] { "wearEngine", "engineWear", "engineDamage", "engine_damage", "truck.wearEngine" },
            new[] { "wearTransmission", "transmissionWear", "transmissionDamage", "transmission_damage", "truck.wearTransmission" },
            new[] { "wearCabin", "cabinWear", "cabinDamage", "cabin_damage", "truck.wearCabin" },
            new[] { "wearChassis", "chassisWear", "chassisDamage", "chassis_damage", "truck.wearChassis" },
            new[] { "wearWheels", "wheelsWear", "wheelsDamage", "wheels_damage", "truck.wearWheels" }
        };
        double max = 0;
        foreach (string[] f in fields)
            max = Math.Max(max, RoadLifeDamagePct(RoadLifeNumber(tele, f)));
        return max;
    }

    private static string RoadLifeGear(JObject tele)
    {
        string raw = RoadLifeText(tele, "truck.displayedGear", "Truck.DisplayedGear", "truck.gearDashboard", "gearDashboard", "truck.gear", "Truck.Gear", "gear");
        if (string.IsNullOrWhiteSpace(raw)) return "N";
        int n;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            return raw.ToUpperInvariant();
        if (n < 0) return "R" + Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        if (n == 0) return "N";
        return "D" + n.ToString(CultureInfo.InvariantCulture);
    }

    private static string RoadLifeEta(JObject tele)
    {
        string raw = RoadLifeText(tele, "eta", "navigation.eta", "navigation.estimatedTime", "Navigation.EstimatedTime", "job.estimatedTime", "timeRemaining");
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        double seconds;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds) && seconds > 0 && seconds < 604800)
            return DateTime.Now.AddSeconds(seconds).ToString("HH:mm", CultureInfo.InvariantCulture);
        return raw.Length > 12 ? raw.Substring(0, 12) : raw;
    }
}
