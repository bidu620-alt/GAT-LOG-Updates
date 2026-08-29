from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
api=root/'client-dotnet/GatTelemetry/ApiClient.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'

journal_code=r'''using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
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
    }

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
    }

    // Diario local independente da internet e do servidor de comboio.
    // Ele observa o TruckSim GPS continuamente, guarda a viagem em disco e so remove
    // o recibo depois que a Central GAT confirmar o recebimento.
    internal sealed class TripJournal
    {
        private readonly object _sync = new object();
        private readonly string _file;
        private TripJournalState _state;
        private DateTime _lastDiskWrite = DateTime.MinValue;

        public TripJournal()
        {
            ClientStore.Ensure();
            _file = Path.Combine(ClientStore.DataDir, "trip_journal.json");
            _state = Load();
        }

        public void Observe(JObject telemetry)
        {
            if (telemetry == null) return;
            lock (_sync)
            {
                bool deliveredFlag = BoolAny(telemetry, "gameplay.jobDelivered", "job_delivered");
                bool cancelledFlag = BoolAny(telemetry, "gameplay.jobCancelled", "job_cancelled");
                bool deliveredEvent = false;
                bool cancelledEvent = false;

                if (!_state.EventsInitialized)
                {
                    _state.EventsInitialized = true;
                    _state.LastJobDelivered = deliveredFlag;
                    _state.LastJobCancelled = cancelledFlag;
                    Save(true);
                }
                else
                {
                    deliveredEvent = deliveredFlag != _state.LastJobDelivered;
                    cancelledEvent = cancelledFlag != _state.LastJobCancelled;
                    if (deliveredEvent || cancelledEvent)
                    {
                        _state.LastJobDelivered = deliveredFlag;
                        _state.LastJobCancelled = cancelledFlag;
                    }
                }

                bool onJob = BoolAny(telemetry, "on_job", "gameplay.onJob");
                string cargo = TextAny(telemetry, "cargo_name", "job.cargoName", "job.cargo");
                string source = TextAny(telemetry, "source_city", "job.sourceCity");
                string destination = TextAny(telemetry, "destination_city", "job.destinationCity");
                string market = TextAny(telemetry, "job_market", "job.market", "market");
                double mass = DoubleAny(telemetry, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass");
                double planned = DoubleAny(telemetry, "job.plannedDistanceKm", "job.planned_distance_km", "planned_distance_km");
                double remaining = DoubleAny(telemetry, "remaining_km");
                if (remaining <= 0)
                {
                    double meters = DoubleAny(telemetry, "distance_m", "navigation.estimatedDistance");
                    if (meters > 0) remaining = meters / 1000.0;
                }

                if (onJob && !string.IsNullOrWhiteSpace(cargo))
                {
                    if (_state.ActiveTrip == null || !SameTrip(_state.ActiveTrip, cargo, source, destination))
                    {
                        _state.ActiveTrip = new TripReceipt
                        {
                            TripId = Guid.NewGuid().ToString("N"),
                            Cargo = cargo,
                            Source = source,
                            Destination = destination,
                            Market = market,
                            WeightKg = mass,
                            PlannedDistanceKm = planned,
                            FirstObservedRemainingKm = remaining,
                            LastObservedRemainingKm = remaining,
                            StartedObservedAt = DateTime.UtcNow.ToString("o")
                        };
                        Save(true);
                    }
                    else
                    {
                        var t = _state.ActiveTrip;
                        if (mass > 0) t.WeightKg = mass;
                        if (planned > 0) t.PlannedDistanceKm = planned;
                        if (remaining >= 0) t.LastObservedRemainingKm = remaining;
                        if (!string.IsNullOrWhiteSpace(market)) t.Market = market;
                        Save(false);
                    }
                }

                // O evento oficial do ETS2 fecha a viagem mesmo se a Central estiver fora do ar.
                if (deliveredEvent && _state.ActiveTrip != null)
                {
                    var done = _state.ActiveTrip;
                    done.CompletedAt = DateTime.UtcNow.ToString("o");
                    if (done.PlannedDistanceKm <= 0)
                        done.PlannedDistanceKm = Math.Max(done.FirstObservedRemainingKm, done.LastObservedRemainingKm);
                    if (!_state.Outbox.Any(x => string.Equals(x.TripId, done.TripId, StringComparison.OrdinalIgnoreCase)))
                        _state.Outbox.Add(done);
                    _state.ActiveTrip = null;
                    Save(true);
                }
                else if (cancelledEvent && _state.ActiveTrip != null)
                {
                    // Cancelamento nunca vira entrega.
                    _state.ActiveTrip = null;
                    Save(true);
                }
                else if (deliveredEvent || cancelledEvent)
                {
                    Save(true);
                }
            }
        }

        public TripReceipt PeekPending()
        {
            lock (_sync)
            {
                return _state.Outbox.Count == 0 ? null : Clone(_state.Outbox[0]);
            }
        }

        public void MarkSent(string tripId)
        {
            if (string.IsNullOrWhiteSpace(tripId)) return;
            lock (_sync)
            {
                _state.Outbox.RemoveAll(x => string.Equals(x.TripId, tripId, StringComparison.OrdinalIgnoreCase));
                Save(true);
            }
        }

        public int PendingCount
        {
            get { lock (_sync) return _state.Outbox.Count; }
        }

        private TripJournalState Load()
        {
            try
            {
                if (!File.Exists(_file)) return new TripJournalState();
                var state = JsonConvert.DeserializeObject<TripJournalState>(File.ReadAllText(_file, Encoding.UTF8));
                if (state == null) state = new TripJournalState();
                if (state.Outbox == null) state.Outbox = new List<TripReceipt>();
                return state;
            }
            catch (Exception ex)
            {
                ClientStore.Log("trip journal load: " + ex.Message);
                return new TripJournalState();
            }
        }

        private void Save(bool force)
        {
            if (!force && (DateTime.UtcNow - _lastDiskWrite).TotalSeconds < 5) return;
            try
            {
                ClientStore.Ensure();
                string tmp = _file + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_state, Formatting.Indented), Encoding.UTF8);
                if (File.Exists(_file)) File.Delete(_file);
                File.Move(tmp, _file);
                _lastDiskWrite = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ClientStore.Log("trip journal save: " + ex.Message);
            }
        }

        private static TripReceipt Clone(TripReceipt t)
        {
            if (t == null) return null;
            return JsonConvert.DeserializeObject<TripReceipt>(JsonConvert.SerializeObject(t));
        }

        private static bool SameTrip(TripReceipt t, string cargo, string source, string destination)
        {
            return t != null &&
                string.Equals((t.Cargo ?? string.Empty).Trim(), (cargo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((t.Source ?? string.Empty).Trim(), (source ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals((t.Destination ?? string.Empty).Trim(), (destination ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string TextAny(JObject m, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = m.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                string s = t.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return string.Empty;
        }

        private static double DoubleAny(JObject m, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = m.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return t.Value<double>();
                double d;
                if (double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            }
            return 0;
        }

        private static bool BoolAny(JObject m, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = m.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                if (t.Type == JTokenType.Boolean) return t.Value<bool>();
                bool b;
                if (bool.TryParse(t.ToString(), out b)) return b;
            }
            return false;
        }
    }
}
'''
journal.write_text(journal_code,encoding='utf-8')

# API: envia o recibo autenticado diretamente para a Central GAT.
s=api.read_text(encoding='utf-8')
methods=r'''
        public Task<ApiResponse> SendTripReceiptAsync(string authority, string accountToken, string driver, TripReceipt receipt)
        {
            string ep = ClientStore.NormalizeEndpoint(authority);
            var body = JObject.FromObject(receipt ?? new TripReceipt());
            body["driver"] = driver ?? string.Empty;
            return PostBearerAsync(ep + "/api/account/trip-complete", accountToken, body, 8);
        }

'''
if 'SendTripReceiptAsync' not in s:
    marker='        public static bool Bool(JToken token)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('ApiClient Bool marker not found')
    s=s[:pos]+methods+s[pos:]
api.write_text(s,encoding='utf-8')

# MainForm: captura localmente antes de qualquer dependencia de conta/servidor e esvazia a fila antes do live telemetry.
s=main.read_text(encoding='utf-8')
if 'private readonly TripJournal _tripJournal' not in s:
    marker='        private readonly TelemetryEngine _telemetry = new TelemetryEngine();\n'
    if marker not in s: raise SystemExit('TelemetryEngine field not found')
    s=s.replace(marker,marker+'        private readonly TripJournal _tripJournal = new TripJournal();\n',1)

if 'private DateTime _lastTripCapture' not in s:
    marker='        private DateTime _lastAccountTelemetry = DateTime.MinValue;\n'
    if marker not in s: raise SystemExit('_lastAccountTelemetry not found')
    s=s.replace(marker,marker+'        private DateTime _lastTripCapture = DateTime.MinValue;\n        private DateTime _lastTripFlush = DateTime.MinValue;\n',1)

helpers=r'''        private async Task CaptureTripJournalAsync()
        {
            if ((DateTime.UtcNow - _lastTripCapture).TotalMilliseconds < 850) return;
            JObject tele = await _telemetry.ReadAsync();
            _lastTripCapture = DateTime.UtcNow;
            if (tele == null) return;
            _tripJournal.Observe(tele);
        }

        private async Task FlushTripReceiptsAsync()
        {
            if (!AccountReady) return;
            if ((DateTime.UtcNow - _lastTripFlush).TotalSeconds < 4) return;
            _lastTripFlush = DateTime.UtcNow;
            var receipt = _tripJournal.PeekPending();
            if (receipt == null) return;

            string driver = string.IsNullOrWhiteSpace(_driver) ? _accountUser : _driver;
            var r = await _api.SendTripReceiptAsync(AccountAuthority, _accountToken, driver, receipt);
            if (r.StatusCode == 200 && r.Json != null && ApiClient.Bool(r.Json["ok"]))
            {
                _tripJournal.MarkSent(receipt.TripId);
                if (ApiClient.Bool(r.Json["completed_now"]))
                    lblTelemetry.Text = "Central GAT: ENTREGA CONFIRMADA";
                ClientStore.Log("recibo de viagem confirmado: " + receipt.TripId);
                return;
            }

            // Nunca apaga o recibo por falha de internet/servidor. Ele fica no disco e tenta de novo.
            if (r.StatusCode != 0 && r.StatusCode != 401)
                ClientStore.Log("recibo pendente " + receipt.TripId + ": HTTP " + r.StatusCode + " " + r.Text);
        }

'''
if 'private async Task CaptureTripJournalAsync()' not in s:
    marker='        private async Task SendCentralTelemetryAsync()\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('SendCentralTelemetryAsync marker not found')
    s=s[:pos]+helpers+s[pos:]

# A captura local vem primeiro no Tick, inclusive sem internet e sem servidor de comboio.
if 'await CaptureTripJournalAsync();' not in s:
    start=s.find('        private async Task TickAsync()')
    if start<0: raise SystemExit('TickAsync not found')
    marker='            try\n            {\n'
    pos=s.find(marker,start)
    if pos<0: raise SystemExit('Tick try marker not found')
    pos+=len(marker)
    s=s[:pos]+'                await CaptureTripJournalAsync();\n\n'+s[pos:]

# A fila persistente tem prioridade sobre o snapshot ao vivo: se houve entrega, confirma primeiro.
if 'await FlushTripReceiptsAsync();' not in s:
    start=s.find('        private async Task SendCentralTelemetryAsync()')
    if start<0: raise SystemExit('SendCentralTelemetryAsync not found')
    needle='            if (!AccountReady) return;\n'
    pos=s.find(needle,start)
    if pos<0: raise SystemExit('AccountReady guard not found in central telemetry')
    pos+=len(needle)
    s=s[:pos]+'            await FlushTripReceiptsAsync();\n'+s[pos:]

s=s.replace('private const string CurrentVersion = "1.0.13";', 'private const string CurrentVersion = "1.0.14";')
s=s.replace('GAT Telemetria C# 1.0.13 TESTE','GAT Telemetria C# 1.0.14 TESTE')
s=s.replace('C# WinForms 1.0.13','C# WinForms 1.0.14')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.13.0</Version>','<Version>1.0.14.0</Version>')
s=s.replace('<FileVersion>1.0.13.0</FileVersion>','<FileVersion>1.0.14.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.13.0</AssemblyVersion>','<AssemblyVersion>1.0.14.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.13?','Atualizar GAT Telemetria para 1.0.14?')
s=s.replace('GAT Telemetria C# 1.0.13 atualizado.','GAT Telemetria C# 1.0.14 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.13_STATUS_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.14_JOURNAL_TESTE')
installer_proj.write_text(s,encoding='utf-8')

checks=[
    ('main',s),
]
print('GAT Telemetria 1.0.14 diario local de viagem aplicado')
