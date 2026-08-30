from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'
scanner=root/'client-dotnet/GatTelemetry/ModIntegrity.cs'

if not journal.exists():
    raise SystemExit('TripJournal.cs nao encontrado; aplique build114-117 primeiro')

scanner_code=r'''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GatTelemetry
{
    internal sealed class ModIntegrityResult
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "not_checked";
        public string[] Matches { get; set; } = new string[0];
        public string EvidenceHash { get; set; } = string.Empty;
        public string CheckedAt { get; set; } = string.Empty;
    }

    internal static class ModIntegrityScanner
    {
        private static readonly object Sync = new object();
        private static DateTime _lastCheck = DateTime.MinValue;
        private static ModIntegrityResult _last = new ModIntegrityResult();

        private static readonly string[] SuspiciousPhrases = new[]
        {
            "no damage", "zero damage", "0 damage", "damage 0", "damage disabled", "disable damage",
            "no cargo damage", "cargo no damage", "zero cargo damage", "no trailer damage", "trailer no damage",
            "no truck damage", "truck no damage", "zero truck damage", "no wear", "zero wear",
            "sem dano", "dano zero", "sem danos", "indestructible", "invincible truck"
        };

        public static ModIntegrityResult Check()
        {
            lock (Sync)
            {
                if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 12 && _last != null)
                    return Clone(_last);
                _lastCheck = DateTime.UtcNow;
                _last = ScanNow();
                return Clone(_last);
            }
        }

        private static ModIntegrityResult ScanNow()
        {
            var result = new ModIntegrityResult { CheckedAt = DateTime.UtcNow.ToString("o") };
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string ets = Path.Combine(docs, "Euro Truck Simulator 2");
                string log = Path.Combine(ets, "game.log.txt");
                if (!File.Exists(log))
                {
                    result.Status = "unknown";
                    result.Reason = "game_log_missing";
                    return result;
                }

                var hits = new List<string>();
                var evidence = new List<string>();
                foreach (string raw in File.ReadLines(log))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string line = raw.Trim();
                    string low = line.ToLowerInvariant();
                    bool modContext = low.Contains("[mod") || low.Contains("mod_package") || low.Contains("mod package") ||
                                      low.Contains("workshop") || low.Contains(".scs") || low.Contains("active mod") ||
                                      low.Contains("loaded mod") || low.Contains("mounting mod") || low.Contains("mod manager");
                    if (!modContext) continue;
                    if (evidence.Count < 250) evidence.Add(Sanitize(line, docs));
                    foreach (string phrase in SuspiciousPhrases)
                    {
                        if (!low.Contains(phrase)) continue;
                        string safe = Sanitize(line, docs);
                        if (!hits.Any(x => string.Equals(x, safe, StringComparison.OrdinalIgnoreCase))) hits.Add(safe);
                        break;
                    }
                    if (hits.Count >= 10) break;
                }

                result.EvidenceHash = HashLines(evidence);
                if (hits.Count > 0)
                {
                    result.Status = "blocked";
                    result.Reason = "damage_mod_detected";
                    result.Matches = hits.ToArray();
                }
                else
                {
                    result.Status = "ok";
                    result.Reason = "active_mod_log_scanned";
                    result.Matches = new string[0];
                }
            }
            catch (Exception ex)
            {
                result.Status = "unknown";
                result.Reason = "scan_failed:" + ex.GetType().Name;
                result.Matches = new string[0];
            }
            return result;
        }

        private static string Sanitize(string line, string docs)
        {
            string s = line ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(docs))
                s = s.Replace(docs, "%DOCUMENTS%");
            if (s.Length > 180) s = s.Substring(0, 180);
            return s;
        }

        private static string HashLines(List<string> lines)
        {
            try
            {
                string text = string.Join("\n", lines ?? new List<string>());
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                    return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch { return string.Empty; }
        }

        private static ModIntegrityResult Clone(ModIntegrityResult x)
        {
            if (x == null) return new ModIntegrityResult();
            return new ModIntegrityResult
            {
                Status = x.Status,
                Reason = x.Reason,
                Matches = x.Matches == null ? new string[0] : x.Matches.ToArray(),
                EvidenceHash = x.EvidenceHash,
                CheckedAt = x.CheckedAt
            };
        }
    }
}
'''
scanner.parent.mkdir(parents=True,exist_ok=True)
scanner.write_text(scanner_code,encoding='utf-8')

s=journal.read_text(encoding='utf-8')
if '[JsonProperty("integrity_status")]' not in s:
    needle='''        [JsonProperty("last_odometer_observed_at")]
        public string LastOdometerObservedAt { get; set; }
'''
    extra='''        [JsonProperty("integrity_status")]
        public string IntegrityStatus { get; set; } = "unknown";
        [JsonProperty("integrity_reason")]
        public string IntegrityReason { get; set; }
        [JsonProperty("integrity_matches")]
        public string[] IntegrityMatches { get; set; } = new string[0];
        [JsonProperty("integrity_evidence_hash")]
        public string IntegrityEvidenceHash { get; set; }
        [JsonProperty("integrity_checked_at")]
        public string IntegrityCheckedAt { get; set; }
'''
    if needle not in s: raise SystemExit('campos finais do odometro nao encontrados')
    s=s.replace(needle,needle+extra,1)

needle='''                        UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                        UpdateSpeedFine(_state.ActiveTrip, speed);
'''
repl='''                        UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                        ApplyModIntegrity(_state.ActiveTrip);
                        UpdateSpeedFine(_state.ActiveTrip, speed);
'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'ApplyModIntegrity(_state.ActiveTrip);' not in s:
    raise SystemExit('inicio da viagem para integridade nao encontrado')

needle='''                    UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                    if (!onJob) ResetOverspeed();
'''
repl='''                    UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                    ApplyModIntegrity(_state.ActiveTrip);
                    if (!onJob) ResetOverspeed();
'''
if needle in s:
    s=s.replace(needle,repl,1)
elif s.count('ApplyModIntegrity(_state.ActiveTrip);') < 2:
    raise SystemExit('fechamento da viagem para integridade nao encontrado')

helper=r'''
        private static void ApplyModIntegrity(TripReceipt trip)
        {
            if (trip == null) return;
            var check = ModIntegrityScanner.Check();
            if (check == null)
            {
                trip.IntegrityStatus = "unknown";
                trip.IntegrityReason = "scanner_unavailable";
                return;
            }

            // Uma deteccao bloqueada em qualquer momento da viagem nunca e apagada por uma leitura posterior.
            if (string.Equals(trip.IntegrityStatus, "blocked", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(check.Status, "blocked", StringComparison.OrdinalIgnoreCase)) return;

            trip.IntegrityStatus = check.Status ?? "unknown";
            trip.IntegrityReason = check.Reason;
            trip.IntegrityMatches = check.Matches ?? new string[0];
            trip.IntegrityEvidenceHash = check.EvidenceHash;
            trip.IntegrityCheckedAt = check.CheckedAt;
        }

'''
if 'private static void ApplyModIntegrity(TripReceipt trip)' not in s:
    marker='        private static double OdometerKm(JObject m)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('OdometerKm marker nao encontrado')
    s=s[:pos]+helper+s[pos:]
journal.write_text(s,encoding='utf-8')

m=main.read_text(encoding='utf-8')
needle='''            tele["gat_account_user"] = _accountUser;
            tele["gat_client_version"] = CurrentVersion;
            lblTruck.Text = "TruckSim GPS: CONECTADO";
'''
repl='''            tele["gat_account_user"] = _accountUser;
            tele["gat_client_version"] = CurrentVersion;
            var integrity = ModIntegrityScanner.Check();
            tele["gat_integrity_status"] = integrity.Status ?? "unknown";
            tele["gat_integrity_reason"] = integrity.Reason ?? string.Empty;
            tele["gat_integrity_evidence_hash"] = integrity.EvidenceHash ?? string.Empty;
            if (integrity.Matches != null && integrity.Matches.Length > 0)
                tele["gat_integrity_matches"] = JArray.FromObject(integrity.Matches);
            lblTruck.Text = "TruckSim GPS: CONECTADO";
'''
if needle in m:
    m=m.replace(needle,repl,1)
elif 'gat_integrity_status' not in m:
    raise SystemExit('telemetria central para integridade nao encontrada')

needle='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                if (ApiClient.Bool(progress.Json["completed_now"]))
'''
repl='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                if (string.Equals(integrity.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: MOD PROIBIDO - ENTREGA NAO VAI CONTAR";
                    return;
                }
                if (!string.Equals(integrity.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: INTEGRIDADE DE MODS NAO VERIFICADA";
                    return;
                }
                if (ApiClient.Bool(progress.Json["completed_now"]))
'''
if needle in m:
    m=m.replace(needle,repl,1)
elif 'MOD PROIBIDO - ENTREGA NAO VAI CONTAR' not in m:
    raise SystemExit('status visual de integridade nao encontrado')

old='''                if (err == "actual_distance_below_minimum" || err == "distance_not_verified" || err == "vehicle_changed" || err == "odometer_discontinuity")
                {
                    _tripJournal.MarkSent(receipt.TripId);
                    lblTelemetry.Text = err == "actual_distance_below_minimum"
                        ? "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE"
                        : "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO";
'''
new='''                if (err == "actual_distance_below_minimum" || err == "distance_not_verified" || err == "vehicle_changed" || err == "odometer_discontinuity" || err == "integrity_mod_blocked" || err == "integrity_not_verified")
                {
                    _tripJournal.MarkSent(receipt.TripId);
                    lblTelemetry.Text = err == "actual_distance_below_minimum"
                        ? "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE"
                        : err == "integrity_mod_blocked"
                            ? "Central GAT: ENTREGA NAO VALIDADA - MOD PROIBIDO"
                            : err == "integrity_not_verified"
                                ? "Central GAT: ENTREGA NAO VALIDADA - INTEGRIDADE"
                                : "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO";
'''
if old in m:
    m=m.replace(old,new,1)
elif 'integrity_mod_blocked' not in m:
    raise SystemExit('tratamento do recibo bloqueado nao encontrado')

m=m.replace('private const string CurrentVersion = "1.0.17";', 'private const string CurrentVersion = "1.0.18";')
m=m.replace('GAT Telemetria C# 1.0.17 TESTE','GAT Telemetria C# 1.0.18 TESTE')
m=m.replace('C# WinForms 1.0.17','C# WinForms 1.0.18')
main.write_text(m,encoding='utf-8')

for path in (proj,installer,installer_proj):
    x=path.read_text(encoding='utf-8')
    x=x.replace('1.0.17.0','1.0.18.0')
    x=x.replace('1.0.17','1.0.18')
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_ANTIBURLA_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_INTEGRIDADE_TESTE')
    path.write_text(x,encoding='utf-8')

checks=[
    (journal,'integrity_status'),(journal,'ApplyModIntegrity'),(main,'gat_integrity_status'),
    (main,'MOD PROIBIDO - ENTREGA NAO VAI CONTAR'),(main,'CurrentVersion = "1.0.18"'),
    (scanner,'damage_mod_detected'),(scanner,'game.log.txt')
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'): raise SystemExit('patch 1.0.18 incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_INTEGRIDADE_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome final do atualizador 1.0.18 nao aplicado')

print('GAT Telemetria 1.0.18: integridade de mods ativos, bloqueio de mods de dano e auditoria no recibo')
