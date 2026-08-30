from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'

if not journal.exists():
    raise SystemExit('TripJournal.cs nao encontrado; aplique build114/115/116 primeiro')

s=journal.read_text(encoding='utf-8')

# Auditoria da distancia realmente rodada e do caminhao usado.
if '[JsonProperty("driven_distance_km")]' not in s:
    needle='''        [JsonProperty("truck_damage_max_pct")]
        public double TruckDamageMaxPct { get; set; } = -1;
'''
    extra='''        [JsonProperty("truck_id")]
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
        public double StartOdometerKm { get; set; } = -1;
        [JsonProperty("end_odometer_km")]
        public double EndOdometerKm { get; set; } = -1;
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
'''
    if needle not in s: raise SystemExit('campos de dano do recibo nao encontrados')
    s=s.replace(needle,needle+extra,1)

# Le odometro e identidade do veiculo diretamente da telemetria do ETS2.
needle='''                double cargoDamage = CargoDamagePercent(telemetry);
                double truckDamage = TruckDamagePercent(telemetry);
'''
extra='''                double odometer = OdometerKm(telemetry);
                string truckId = TextAny(telemetry, "truck.id", "truck.unitId", "truck.unit_id", "truck.vehicleId", "truck.vehicle_id");
                string truckMake = TextAny(telemetry, "truck.make", "truck.manufacturer", "truck.brand", "truck.makeName");
                string truckModel = TextAny(telemetry, "truck.model", "truck.modelName", "truck.model_name");
                string truckPlate = TextAny(telemetry, "truck.licensePlate", "truck.license_plate", "truck.licensePlateNumber", "truck.registrationPlate", "truck.plate");
                string truckIdentity = BuildTruckIdentity(truckId, truckPlate, truckMake, truckModel);
'''
if 'double odometer = OdometerKm(telemetry);' not in s:
    if needle not in s: raise SystemExit('leitura de dano nao encontrada')
    s=s.replace(needle,needle+extra,1)

old='''                            TruckDamageStartPct = truckDamage,
                            TruckDamageMaxPct = truckDamage
                        };
                        UpdateSpeedFine(_state.ActiveTrip, speed);
'''
new='''                            TruckDamageStartPct = truckDamage,
                            TruckDamageMaxPct = truckDamage,
                            TruckId = truckId,
                            TruckMake = truckMake,
                            TruckModel = truckModel,
                            TruckPlate = truckPlate,
                            TruckIdentity = truckIdentity,
                            StartOdometerKm = odometer > 0 ? odometer : -1,
                            EndOdometerKm = odometer > 0 ? odometer : -1,
                            DrivenDistanceKm = 0,
                            OdometerVerified = odometer > 0,
                            LastOdometerObservedAt = DateTime.UtcNow.ToString("o")
                        };
                        UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                        UpdateSpeedFine(_state.ActiveTrip, speed);
'''
if old in s:
    s=s.replace(old,new,1)
elif 'StartOdometerKm = odometer > 0 ? odometer : -1' not in s:
    raise SystemExit('inicializacao do recibo nao encontrada')

old='''                        UpdateDamage(t, cargoDamage, truckDamage);
                        UpdateSpeedFine(t, speed);
                        Save(false);
'''
new='''                        UpdateDamage(t, cargoDamage, truckDamage);
                        UpdateOdometerAndVehicle(t, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                        UpdateSpeedFine(t, speed);
                        Save(false);
'''
if old in s:
    s=s.replace(old,new,1)
elif 'UpdateOdometerAndVehicle(t, odometer' not in s:
    raise SystemExit('atualizacao do odometro durante a viagem nao encontrada')

old='''                if (_state.ActiveTrip != null)
                {
                    UpdateDamage(_state.ActiveTrip, cargoDamage, truckDamage);
                    if (!onJob) ResetOverspeed();
                }
'''
new='''                if (_state.ActiveTrip != null)
                {
                    UpdateDamage(_state.ActiveTrip, cargoDamage, truckDamage);
                    UpdateOdometerAndVehicle(_state.ActiveTrip, odometer, truckId, truckPlate, truckMake, truckModel, truckIdentity);
                    if (!onJob) ResetOverspeed();
                }
'''
if old in s:
    s=s.replace(old,new,1)
elif 'UpdateOdometerAndVehicle(_state.ActiveTrip, odometer' not in s:
    raise SystemExit('captura final do odometro nao encontrada')

helpers=r'''
        private static double OdometerKm(JObject m)
        {
            double v;
            if (!TryDoubleAny(m, out v,
                "truck.odometer", "truck.odometerKm", "truck.odometer_km", "odometer", "odometer_km")) return -1;
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0 || v > 50000000) return -1;
            return v;
        }

        private static string BuildTruckIdentity(string id, string plate, string make, string model)
        {
            if (!string.IsNullOrWhiteSpace(id)) return "id:" + id.Trim();
            if (!string.IsNullOrWhiteSpace(plate)) return "plate:" + plate.Trim();
            string mm = ((make ?? string.Empty).Trim() + "|" + (model ?? string.Empty).Trim()).Trim('|');
            return string.IsNullOrWhiteSpace(mm) ? string.Empty : "model:" + mm;
        }

        private static void UpdateOdometerAndVehicle(TripReceipt trip, double odometerKm, string truckId, string plate, string make, string model, string identity)
        {
            if (trip == null) return;
            if (!string.IsNullOrWhiteSpace(truckId)) trip.TruckId = truckId;
            if (!string.IsNullOrWhiteSpace(plate)) trip.TruckPlate = plate;
            if (!string.IsNullOrWhiteSpace(make)) trip.TruckMake = make;
            if (!string.IsNullOrWhiteSpace(model)) trip.TruckModel = model;

            if (!string.IsNullOrWhiteSpace(identity))
            {
                if (string.IsNullOrWhiteSpace(trip.TruckIdentity)) trip.TruckIdentity = identity;
                else if (!string.Equals(trip.TruckIdentity, identity, StringComparison.OrdinalIgnoreCase))
                {
                    trip.VehicleChanged = true;
                    trip.OdometerVerified = false;
                }
            }

            if (odometerKm <= 0) return;
            var now = DateTime.UtcNow;
            if (trip.StartOdometerKm <= 0 || trip.EndOdometerKm <= 0)
            {
                trip.StartOdometerKm = odometerKm;
                trip.EndOdometerKm = odometerKm;
                trip.DrivenDistanceKm = 0;
                trip.OdometerVerified = !trip.VehicleChanged;
                trip.LastOdometerObservedAt = now.ToString("o");
                return;
            }

            DateTime previousAt;
            double seconds = 1.0;
            if (!string.IsNullOrWhiteSpace(trip.LastOdometerObservedAt) &&
                DateTime.TryParse(trip.LastOdometerObservedAt, null, DateTimeStyles.RoundtripKind, out previousAt))
                seconds = Math.Max(0.2, (now - previousAt.ToUniversalTime()).TotalSeconds);

            double delta = odometerKm - trip.EndOdometerKm;
            // Tolerancia ampla: ate 220 km/h mais 250 m por amostra. Saltos maiores sao troca de veiculo/odometro adulterado.
            double maxDelta = (220.0 * seconds / 3600.0) + 0.25;
            if (delta < -0.20 || delta > maxDelta)
            {
                trip.OdometerDiscontinuity = true;
                trip.OdometerVerified = false;
            }
            else if (delta > 0)
            {
                trip.DrivenDistanceKm += delta;
            }
            trip.EndOdometerKm = odometerKm;
            trip.LastOdometerObservedAt = now.ToString("o");
        }

'''
if 'private static double OdometerKm(JObject m)' not in s:
    marker='        private void UpdateSpeedFine(TripReceipt trip, double speedKmh)\n'
    pos=s.find(marker)
    if pos<0: raise SystemExit('UpdateSpeedFine marker nao encontrado')
    s=s[:pos]+helpers+s[pos:]

journal.write_text(s,encoding='utf-8')

m=main.read_text(encoding='utf-8')
# Entrega rejeitada por distancia real/troca de caminhao e uma tentativa encerrada, nao fica presa na fila para sempre.
needle='''            // Nunca apaga o recibo por falha de internet/servidor. Ele fica no disco e tenta de novo.
            if (r.StatusCode != 0 && r.StatusCode != 401)
                ClientStore.Log("recibo pendente " + receipt.TripId + ": HTTP " + r.StatusCode + " " + r.Text);
'''
repl='''            if (r.StatusCode == 409 && r.Json != null)
            {
                string err = ApiClient.Str(r.Json["error"]);
                if (err == "actual_distance_below_minimum" || err == "distance_not_verified" || err == "vehicle_changed" || err == "odometer_discontinuity")
                {
                    _tripJournal.MarkSent(receipt.TripId);
                    lblTelemetry.Text = err == "actual_distance_below_minimum"
                        ? "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE"
                        : "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO";
                    ClientStore.Log("entrega nao validada pela Central GAT: " + err + " / " + receipt.TripId);
                    return;
                }
            }

            // Nunca apaga o recibo por falha de internet/servidor. Ele fica no disco e tenta de novo.
            if (r.StatusCode != 0 && r.StatusCode != 401)
                ClientStore.Log("recibo pendente " + receipt.TripId + ": HTTP " + r.StatusCode + " " + r.Text);
'''
if needle in m:
    m=m.replace(needle,repl,1)
elif 'KM REAL INSUFICIENTE' not in m:
    raise SystemExit('bloco de flush do recibo nao encontrado')

m=m.replace('private const string CurrentVersion = "1.0.16";', 'private const string CurrentVersion = "1.0.17";')
m=m.replace('GAT Telemetria C# 1.0.16 TESTE','GAT Telemetria C# 1.0.17 TESTE')
m=m.replace('C# WinForms 1.0.16','C# WinForms 1.0.17')
main.write_text(m,encoding='utf-8')

for path in (proj,installer,installer_proj):
    x=path.read_text(encoding='utf-8')
    x=x.replace('1.0.16.0','1.0.17.0')
    x=x.replace('1.0.16','1.0.17')
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.17_ESTAVEL_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.17_ANTIBURLA_TESTE')
    path.write_text(x,encoding='utf-8')

checks=[
    ('journal','driven_distance_km'),('journal','truck.odometer'),('journal','OdometerDiscontinuity'),
    ('journal','VehicleChanged'),('journal','220.0 * seconds'),('main','CurrentVersion = "1.0.17"'),('main','KM REAL INSUFICIENTE')
]
for where,text in checks:
    target=journal.read_text(encoding='utf-8') if where=='journal' else main.read_text(encoding='utf-8')
    if text not in target: raise SystemExit('patch 1.0.17 incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.17_ANTIBURLA_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome final do atualizador 1.0.17 nao aplicado')

print('GAT Telemetria 1.0.17: odometro real, identidade do caminhao e protecao contra teleporte/troca de veiculo')
