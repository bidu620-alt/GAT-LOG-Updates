using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal sealed class TelemetryEngine : IDisposable
    {
        private const string TruckUrl = "http://127.0.0.1:31377/api/ets2/telemetry";
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(1400) };
        private DateTime _lastStartAttempt = DateTime.MinValue;

        public async Task<JObject> ReadAsync()
        {
            try
            {
                string text = await _http.GetStringAsync(TruckUrl).ConfigureAwait(false);
                var json = JObject.Parse(text);
                Normalize(json);
                return json;
            }
            catch
            {
                TryStartTruckSimGps();
                return null;
            }
        }

        private void TryStartTruckSimGps()
        {
            if ((DateTime.UtcNow - _lastStartAttempt).TotalSeconds < 10) return;
            _lastStartAttempt = DateTime.UtcNow;

            try
            {
                var running = Process.GetProcessesByName("TruckSimGPS_Server");
                try
                {
                    if (running.Length > 0) return;
                }
                finally
                {
                    foreach (var p in running) p.Dispose();
                }

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string[] candidates =
                {
                    Path.Combine(local, "Programs", "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
                    Path.Combine(local, "Programs", "TruckSim GPS", "TruckSimGPS_Server.exe"),
                    Path.Combine(pf, "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
                    Path.Combine(pfx86, "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe")
                };

                foreach (string exe in candidates)
                {
                    if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) continue;
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exe)
                    });
                    ClientStore.Log("TruckSim GPS iniciado automaticamente: " + exe);
                    return;
                }
            }
            catch (Exception ex)
            {
                ClientStore.Log("Falha ao iniciar TruckSim GPS: " + ex.Message);
            }
        }

        public static void Normalize(JObject m)
        {
            if (m == null) return;

            double mass;
            if (TryAny(m, out mass,
                "job.cargoMass", "Job.CargoMass", "mass_kg", "cargoMass", "cargo_mass", "cargoMassKg", "cargo_mass_kg",
                "cargoWeight", "cargo_weight", "weight_kg", "job.mass", "job.mass_kg", "job.cargo_mass", "job.cargoMassKg",
                "job.cargoWeight", "job.weight", "job.cargo.mass", "job.cargo.mass_kg", "job.cargo.massKg", "job.cargo.weight",
                "cargo.mass", "cargo.mass_kg", "cargo.massKg", "cargo.weight", "trailer.mass", "trailerMass", "trailer.cargoMass",
                "trailer.cargo_mass", "game.job.cargoMass", "game.job.mass") && mass > 0)
            {
                m["mass_kg"] = mass;
                m["cargoMass"] = mass;
                m["cargo_mass"] = mass;
            }

            double distance;
            if (TryAny(m, out distance, "navigation.estimatedDistance", "navigation.estimated_distance"))
            {
                m["distance_m"] = distance;
                m["remaining_km"] = distance / 1000.0;
            }

            // TruckSim GPS 1.4.1 usado pelo projeto já entrega truck.speed em km/h.
            // NÃO multiplicar por 3.6. Esse era o bug do cliente Go 2.0.6.
            double speed;
            if (TryAny(m, out speed, "truck.speedKmh", "truck.speed_kmh", "speed_kmh", "truck.speed"))
            {
                m["speed_kmh"] = Math.Abs(speed);
            }

            CopyAlias(m, "job.cargo", "cargo_name");
            CopyAlias(m, "job.cargoName", "cargo_name", true);
            CopyAlias(m, "job.sourceCity", "source_city");
            CopyAlias(m, "job.destinationCity", "destination_city");
            CopyAlias(m, "gameplay.onJob", "on_job");
        }

        public static TelemetryDisplay BuildDisplay(JObject m)
        {
            var d = new TelemetryDisplay();
            if (m == null) return d;

            string cargo = TextAny(m, "cargo_name", "job.cargo", "job.cargoName");
            if (!string.IsNullOrWhiteSpace(cargo)) d.Cargo = cargo;

            string src = TextAny(m, "source_city", "job.sourceCity");
            string dst = TextAny(m, "destination_city", "job.destinationCity");
            if (!string.IsNullOrWhiteSpace(src) || !string.IsNullOrWhiteSpace(dst)) d.Route = (src ?? "?") + " → " + (dst ?? "?");

            double km;
            if (TryAny(m, out km, "remaining_km")) d.Distance = km.ToString("0.0", CultureInfo.InvariantCulture) + " km";
            else if (TryAny(m, out km, "distance_m")) d.Distance = (km / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " km";

            double speed;
            if (TryAny(m, out speed, "speed_kmh")) d.Speed = Math.Abs(speed).ToString("0", CultureInfo.InvariantCulture) + " km/h";

            double mass;
            if (TryAny(m, out mass, "mass_kg", "cargo_mass", "cargoMass") && mass > 0)
                d.Weight = (mass / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " t";

            return d;
        }

        private static void CopyAlias(JObject m, string source, string destination, bool onlyIfMissing = false)
        {
            if (onlyIfMissing && m[destination] != null) return;
            var token = m.SelectToken(source, false);
            if (token != null && token.Type != JTokenType.Null) m[destination] = token.DeepClone();
        }

        private static string TextAny(JObject m, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = m.SelectToken(path, false);
                if (t != null && t.Type != JTokenType.Null)
                {
                    string s = t.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return string.Empty;
        }

        private static bool TryAny(JObject m, out double value, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = m.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                {
                    value = t.Value<double>();
                    return true;
                }
                double parsed;
                if (double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                {
                    value = parsed;
                    return true;
                }
            }
            value = 0;
            return false;
        }

        public void Dispose() => _http.Dispose();
    }
}
