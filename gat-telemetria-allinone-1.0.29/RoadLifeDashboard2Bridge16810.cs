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

    private static string RoadLifeDashboard2DataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GAT Telemetria",
            "RoadLifeDashboard2");
    }

    private static string RoadLifeDashboard2TelemetryFile()
    {
        return Path.Combine(RoadLifeDashboard2DataRoot(), "telemetry.json");
    }

    private static string RoadLifeDashboard2LogFile()
    {
        return Path.Combine(RoadLifeDashboard2DataRoot(), "dashboard2-launch.log");
    }

    private void RoadLifeDashboard2Initialize()
    {
        FormClosed += delegate { CloseRoadLifeDashboard2(); };
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
                    "Dashboard2.ps1 não encontrado.\r\n\r\n" + script,
                    "ROADLIFE DASHBOARD2",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(RoadLifeDashboard2DataRoot());

            string telemetryFile = RoadLifeDashboard2TelemetryFile();
            if (!File.Exists(telemetryFile))
                File.WriteAllText(telemetryFile, "{\"online\":false}", new UTF8Encoding(false));

            string psExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");

            if (!File.Exists(psExe))
                psExe = "powershell.exe";

            string logFile = RoadLifeDashboard2LogFile();

            _roadLifeDashboard2Process = Process.Start(new ProcessStartInfo
            {
                FileName = psExe,
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + script + "\" -TelemetryPath \"" + telemetryFile + "\"",
                WorkingDirectory = RoadLifeDashboard2Root(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (_roadLifeDashboard2Process == null)
                throw new InvalidOperationException("O Windows não iniciou o processo do RoadLife Dashboard2.");

            if (_roadLifeDashboard2Process.WaitForExit(1200))
            {
                string err = _roadLifeDashboard2Process.StandardError.ReadToEnd();
                if (string.IsNullOrWhiteSpace(err))
                    err = "O Dashboard2 encerrou logo após iniciar, sem mensagem do PowerShell.";

                try { File.WriteAllText(logFile, err, new UTF8Encoding(true)); } catch { }

                throw new InvalidOperationException(
                    "O RoadLife Dashboard2 não conseguiu abrir.\r\n\r\n" +
                    "Detalhes salvos em:\r\n" + logFile + "\r\n\r\n" +
                    err);
            }

            ClientStore.Log("RoadLife Dashboard2 PS1 aberto: " + script);
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
                _roadLifeDashboard2Process.CloseMainWindow();
        }
        catch { }

        try { _roadLifeDashboard2Process?.Dispose(); } catch { }
        _roadLifeDashboard2Process = null;
    }

    // O GAT é o único captador. O PS1 apenas lê este JSON local e desenha o painel.
    private void RoadLifeDashboard2Push(JObject tele)
    {
        if (tele == null || !RoadLifeDashboard2Running()) return;

        try
        {
            JObject payload = RoadLifeDashboard2BuildPayload(tele);
            string file = RoadLifeDashboard2TelemetryFile();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, payload.ToString(Formatting.Indented), new UTF8Encoding(false));
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

        double fuel = RoadLifeNumber(tele, "truck.fuel", "Truck.Fuel", "fuel", "fuelAmount");
        double fuelCapacity = RoadLifeNumber(tele, "truck.fuelCapacity", "Truck.FuelCapacity", "fuelCapacity");
        double fuelPct = !double.IsNaN(fuel) && !double.IsNaN(fuelCapacity) && fuelCapacity > 0
            ? fuel / fuelCapacity * 100.0
            : RoadLifeNumber(tele, "truck.fuelPercent", "fuelPercent");

        double speed = RoadLifeNumber(tele, "truck.speed", "Truck.Speed", "speed");
        double temperature = RoadLifeNumber(tele, "truck.waterTemperature", "Truck.WaterTemperature", "waterTemperature");
        double speedLimit = RoadLifeNumber(tele, "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit");
        double cruiseSpeed = RoadLifeNumber(tele, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed", "cruiseControlSpeed");
        bool cruise = RoadLifeBool(tele, "truck.cruiseControl", "truck.cruiseControlOn", "cruiseControl")
            || (!double.IsNaN(cruiseSpeed) && cruiseSpeed > 0.1);

        string model = RoadLifeText(tele, "truck.model", "Truck.Model", "truck.name", "Truck.Name");
        string make = RoadLifeText(tele, "truck.make", "Truck.Make", "truck.brand", "Truck.Brand");
        if (string.IsNullOrWhiteSpace(model)) model = make;
        else if (!string.IsNullOrWhiteSpace(make) && model.IndexOf(make, StringComparison.OrdinalIgnoreCase) < 0)
            model = make + " " + model;

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
                ["cruise"] = cruise
            },
            ["eta"] = RoadLifeEta(tele),
            ["damage"] = new JObject
            {
                ["truck_pct"] = RoadLifeTruckDamagePct(tele),
                ["trailer_pct"] = RoadLifeDamagePct(RoadLifeNumber(tele, "trailerDamage", "trailer_damage", "trailer.wear")),
                ["cargo_pct"] = RoadLifeDamagePct(RoadLifeNumber(tele, "cargoDamage", "cargo_damage", "job.cargoDamage"))
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
        double v;
        return double.TryParse(RoadLifeText(root, paths), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
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
        double max = 0;
        string[][] fields =
        {
            new[] { "wearEngine", "engineWear", "engineDamage", "truck.wearEngine" },
            new[] { "wearTransmission", "transmissionWear", "transmissionDamage", "truck.wearTransmission" },
            new[] { "wearCabin", "cabinWear", "cabinDamage", "truck.wearCabin" },
            new[] { "wearChassis", "chassisWear", "chassisDamage", "truck.wearChassis" },
            new[] { "wearWheels", "wheelsWear", "wheelsDamage", "truck.wearWheels" }
        };
        foreach (string[] f in fields)
            max = Math.Max(max, RoadLifeDamagePct(RoadLifeNumber(tele, f)));
        return max;
    }

    private static string RoadLifeGear(JObject tele)
    {
        string raw = RoadLifeText(tele, "truck.displayedGear", "Truck.DisplayedGear", "truck.gearDashboard", "gearDashboard", "truck.gear", "Truck.Gear");
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
        string raw = RoadLifeText(tele, "eta", "navigation.eta", "navigation.estimatedTime", "Navigation.EstimatedTime", "timeRemaining");
        if (string.IsNullOrWhiteSpace(raw)) return "—";

        double seconds;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds) && seconds > 0 && seconds < 604800)
            return DateTime.Now.AddSeconds(seconds).ToString("HH:mm", CultureInfo.InvariantCulture);

        return raw.Length > 12 ? raw.Substring(0, 12) : raw;
    }
}
