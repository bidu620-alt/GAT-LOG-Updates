from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'

if not journal.exists():
    raise SystemExit('TripJournal.cs nao encontrado; aplique build114 primeiro')

s=journal.read_text(encoding='utf-8')

# Campos persistentes enviados no recibo para a Central calcular as penalidades.
if '[JsonProperty("speed_fines")]' not in s:
    needle='''        [JsonProperty("completed_at")]
        public string CompletedAt { get; set; }
'''
    extra='''        [JsonProperty("speed_fines")]
        public int SpeedFines { get; set; }
        [JsonProperty("cargo_damage_pct")]
        public double CargoDamagePct { get; set; }
        [JsonProperty("truck_damage_start_pct")]
        public double TruckDamageStartPct { get; set; } = -1;
        [JsonProperty("truck_damage_max_pct")]
        public double TruckDamageMaxPct { get; set; } = -1;
'''
    if needle not in s: raise SystemExit('CompletedAt do TripReceipt nao encontrado')
    s=s.replace(needle,needle+extra,1)

if '[JsonProperty("overspeed_since")]' not in s:
    needle='''        [JsonProperty("last_job_cancelled")]
        public bool LastJobCancelled { get; set; }
'''
    extra='''        [JsonProperty("overspeed_since")]
        public string OverspeedSince { get; set; }
        [JsonProperty("last_speed_fine_at")]
        public string LastSpeedFineAt { get; set; }
'''
    if needle not in s: raise SystemExit('TripJournalState marker nao encontrado')
    s=s.replace(needle,needle+extra,1)

# Le velocidade e danos em varios nomes usados pelo TruckSim GPS / SCS telemetry.
needle='''                if (remaining <= 0)
                {
                    double meters = DoubleAny(telemetry, "distance_m", "navigation.estimatedDistance");
                    if (meters > 0) remaining = meters / 1000.0;
                }
'''
extra='''                double speed = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));
                double cargoDamage = CargoDamagePercent(telemetry);
                double truckDamage = TruckDamagePercent(telemetry);
'''
if extra.strip() not in s:
    if needle not in s: raise SystemExit('bloco remaining nao encontrado')
    s=s.replace(needle,needle+'\n'+extra,1)

# Nova viagem: dano atual vira a linha de base do caminhao; dano de carga parte do valor observado.
old='''                            FirstObservedRemainingKm = remaining,
                            LastObservedRemainingKm = remaining,
                            StartedObservedAt = DateTime.UtcNow.ToString("o")
                        };
                        Save(true);
'''
new='''                            FirstObservedRemainingKm = remaining,
                            LastObservedRemainingKm = remaining,
                            StartedObservedAt = DateTime.UtcNow.ToString("o"),
                            CargoDamagePct = cargoDamage >= 0 ? cargoDamage : 0,
                            TruckDamageStartPct = truckDamage,
                            TruckDamageMaxPct = truckDamage
                        };
                        UpdateSpeedFine(_state.ActiveTrip, speed);
                        Save(true);
'''
if old in s:
    s=s.replace(old,new,1)
elif 'TruckDamageStartPct = truckDamage' not in s:
    raise SystemExit('inicializacao da viagem nao encontrada')

# Durante a viagem guarda o maior dano observado e as multas.
old='''                        if (remaining >= 0) t.LastObservedRemainingKm = remaining;
                        if (!string.IsNullOrWhiteSpace(market)) t.Market = market;
                        Save(false);
'''
new='''                        if (remaining >= 0) t.LastObservedRemainingKm = remaining;
                        if (!string.IsNullOrWhiteSpace(market)) t.Market = market;
                        UpdateDamage(t, cargoDamage, truckDamage);
                        UpdateSpeedFine(t, speed);
                        Save(false);
'''
if old in s:
    s=s.replace(old,new,1)
elif 'UpdateDamage(t, cargoDamage, truckDamage);' not in s:
    raise SystemExit('atualizacao da viagem nao encontrada')

# Mesmo no tick final em que on_job ja caiu, captura o ultimo dano antes de fechar o recibo.
marker='''                // O evento oficial do ETS2 fecha a viagem mesmo se a Central estiver fora do ar.
                if (deliveredEvent && _state.ActiveTrip != null)
'''
repl='''                if (_state.ActiveTrip != null)
                {
                    UpdateDamage(_state.ActiveTrip, cargoDamage, truckDamage);
                    if (!onJob) ResetOverspeed();
                }

                // O evento oficial do ETS2 fecha a viagem mesmo se a Central estiver fora do ar.
                if (deliveredEvent && _state.ActiveTrip != null)
'''
if 'UpdateDamage(_state.ActiveTrip, cargoDamage, truckDamage);' not in s:
    if marker not in s: raise SystemExit('marcador de JobDelivered nao encontrado')
    s=s.replace(marker,repl,1)

# Limpa estado temporario ao encerrar/cancelar para a proxima viagem comecar limpa.
s=s.replace('''                    _state.ActiveTrip = null;
                    Save(true);
                }
                else if (cancelledEvent''','''                    _state.ActiveTrip = null;
                    ResetOverspeed();
                    Save(true);
                }
                else if (cancelledEvent''',1)
s=s.replace('''                    // Cancelamento nunca vira entrega.
                    _state.ActiveTrip = null;
                    Save(true);
''','''                    // Cancelamento nunca vira entrega.
                    _state.ActiveTrip = null;
                    ResetOverspeed();
                    Save(true);
''',1)

helpers=r'''
        private void UpdateSpeedFine(TripReceipt trip, double speedKmh)
        {
            if (trip == null) return;
            var now = DateTime.UtcNow;
            if (speedKmh <= 91.0)
            {
                ResetOverspeed();
                return;
            }

            DateTime since;
            if (string.IsNullOrWhiteSpace(_state.OverspeedSince) || !DateTime.TryParse(_state.OverspeedSince, null, DateTimeStyles.RoundtripKind, out since))
            {
                _state.OverspeedSince = now.ToString("o");
                return;
            }
            if ((now - since.ToUniversalTime()).TotalSeconds < 5.0) return;

            DateTime last;
            bool canFine = string.IsNullOrWhiteSpace(_state.LastSpeedFineAt) ||
                !DateTime.TryParse(_state.LastSpeedFineAt, null, DateTimeStyles.RoundtripKind, out last) ||
                (now - last.ToUniversalTime()).TotalMinutes >= 5.0;
            if (!canFine) return;

            trip.SpeedFines++;
            _state.LastSpeedFineAt = now.ToString("o");
            ClientStore.Log("multa GAT por excesso de velocidade: " + speedKmh.ToString("0", CultureInfo.InvariantCulture) + " km/h");
        }

        private void ResetOverspeed()
        {
            _state.OverspeedSince = null;
            _state.LastSpeedFineAt = null;
        }

        private static void UpdateDamage(TripReceipt trip, double cargoDamage, double truckDamage)
        {
            if (trip == null) return;
            if (cargoDamage >= 0 && cargoDamage > trip.CargoDamagePct) trip.CargoDamagePct = cargoDamage;
            if (truckDamage >= 0)
            {
                if (trip.TruckDamageStartPct < 0)
                {
                    trip.TruckDamageStartPct = truckDamage;
                    trip.TruckDamageMaxPct = truckDamage;
                }
                else if (truckDamage > trip.TruckDamageMaxPct)
                {
                    trip.TruckDamageMaxPct = truckDamage;
                }
            }
        }

        private static double NormalizePercent(double v)
        {
            if (v < 0) return -1;
            if (v <= 1.01) return v * 100.0;
            return v;
        }

        private static double CargoDamagePercent(JObject m)
        {
            double v;
            if (TryDoubleAny(m, out v,
                "cargo_damage_pct", "cargoDamage", "cargo.damage", "job.cargoDamage", "job.cargo_damage",
                "trailer.cargoDamage", "trailer.cargo_damage", "trailer.damageCargo"))
                return NormalizePercent(v);
            return -1;
        }

        private static double TruckDamagePercent(JObject m)
        {
            double direct;
            if (TryDoubleAny(m, out direct,
                "truck_damage_pct", "truck.damagePercent", "truck.damage", "truckDamage", "truck.damage_pct"))
                return NormalizePercent(direct);

            string[] parts = {
                "truck.wearEngine", "truck.wear_engine", "truck.wear.engine",
                "truck.wearTransmission", "truck.wear_transmission", "truck.wear.transmission",
                "truck.wearCabin", "truck.wear_cabin", "truck.wear.cabin",
                "truck.wearChassis", "truck.wear_chassis", "truck.wear.chassis",
                "truck.wearWheels", "truck.wear_wheels", "truck.wear.wheels"
            };
            double max = -1;
            foreach (var path in parts)
            {
                double v;
                if (!TryDoubleAny(m, out v, path)) continue;
                v = NormalizePercent(v);
                if (v > max) max = v;
            }
            return max;
        }

        private static bool TryDoubleAny(JObject m, out double value, params string[] paths)
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
                double d;
                if (double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                {
                    value = d;
                    return true;
                }
            }
            value = 0;
            return false;
        }

'''
if 'private void UpdateSpeedFine(' not in s:
    marker='        private static string TextAny(JObject m, params string[] paths)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('TextAny marker nao encontrado')
    s=s[:pos]+helpers+s[pos:]

journal.write_text(s,encoding='utf-8')

# Mostra no cliente o XP final e quanto foi descontado quando o recibo for confirmado.
s=main.read_text(encoding='utf-8')
old='''                if (ApiClient.Bool(r.Json["completed_now"]))
                    lblTelemetry.Text = "Central GAT: ENTREGA CONFIRMADA";
'''
new='''                if (ApiClient.Bool(r.Json["completed_now"]) || ApiClient.Bool(r.Json["already_counted"]))
                {
                    int xpFinal = r.Json["xp_awarded"] == null ? 0 : r.Json["xp_awarded"].Value<int>();
                    int penalty = r.Json["penalty_xp"] == null ? 0 : r.Json["penalty_xp"].Value<int>();
                    lblTelemetry.Text = penalty > 0
                        ? "Central GAT: ENTREGA " + xpFinal + " XP (-" + penalty + ")"
                        : "Central GAT: ENTREGA " + xpFinal + " XP";
                }
'''
if old in s:
    s=s.replace(old,new,1)
elif 'Central GAT: ENTREGA " + xpFinal' not in s:
    raise SystemExit('status de entrega do MainForm nao encontrado')

s=s.replace('private const string CurrentVersion = "1.0.14";', 'private const string CurrentVersion = "1.0.15";')
s=s.replace('GAT Telemetria C# 1.0.14 TESTE','GAT Telemetria C# 1.0.15 TESTE')
s=s.replace('C# WinForms 1.0.14','C# WinForms 1.0.15')
main.write_text(s,encoding='utf-8')

# Atualiza metadados e nome do atualizador gerado pelo build114.
for path in (proj,installer,installer_proj):
    x=path.read_text(encoding='utf-8')
    x=x.replace('1.0.14','1.0.15')
    x=x.replace('1.0.14.0','1.0.15.0')
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.14_JOURNAL_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.15_REGRAS_TESTE')
    path.write_text(x,encoding='utf-8')

checks=[
    ('journal','speed_fines'),('journal','CargoDamagePercent'),('journal','TruckDamagePercent'),('journal','speedKmh <= 91.0'),
    ('journal','TotalSeconds < 5.0'),('journal','TotalMinutes >= 5.0'),('main','CurrentVersion = "1.0.15"'),('main','penalty_xp')
]
for where,text in checks:
    target=journal.read_text(encoding='utf-8') if where=='journal' else main.read_text(encoding='utf-8')
    if text not in target: raise SystemExit('patch incompleto: '+text)

print('GAT Telemetria 1.0.15: multas, dano da carga e dano do caminhao registrados no diario')
