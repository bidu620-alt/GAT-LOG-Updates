from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

s=main.read_text(encoding='utf-8')

old='''        private JObject StabilizeJobTelemetry(JObject tele)
        {
            if (tele == null) return null;
            bool delivered = BoolAny(tele, "gameplay.jobDelivered", "jobDelivered");
            bool cancelled = BoolAny(tele, "gameplay.jobCancelled", "jobCancelled", "gameplay.jobCanceled", "jobCanceled");
            if (delivered || cancelled)
            {
                if (_latchedJob != null) ClientStore.Log((delivered ? "JOB DELIVERED | " : "JOB CANCELLED | ") + JobSummary(_latchedJob));
                _latchedJob = null; _latchedJobKey = string.Empty; return tele;
            }
            string cargo = TextAny(tele, "cargo_name", "job.cargoName", "job.cargo");
            string cargoId = TextAny(tele, "cargo_id", "job.cargoId", "Job.CargoId");
            double mass = NumberAny(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
            double planned = NumberAny(tele, "planned_distance_km", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
            double remaining = NumberAny(tele, "remaining_km");
            if (_latchedJob == null && (!string.IsNullOrWhiteSpace(cargo) || !string.IsNullOrWhiteSpace(cargoId)) && mass > 0)
            {
                _latchedJob = new JObject();
                CopyValue(tele,_latchedJob,"cargo_name","cargo_name","job.cargoName","job.cargo");
                CopyValue(tele,_latchedJob,"cargo_id","cargo_id","job.cargoId","Job.CargoId");
                CopyValue(tele,_latchedJob,"mass_kg","mass_kg","cargoMass","cargo_mass","job.cargoMass","Job.CargoMass");
                CopyValue(tele,_latchedJob,"source_city","source_city","job.sourceCity");
                CopyValue(tele,_latchedJob,"destination_city","destination_city","job.destinationCity");
                _latchedJob["planned_distance_km"] = planned > 0 ? planned : remaining;
                _latchedJob["job_latched"] = true;
                _latchedJobKey = !string.IsNullOrWhiteSpace(cargoId) ? cargoId : cargo + "|" + mass.ToString("0");
                ClientStore.Log("JOB STARTED | " + JobSummary(_latchedJob));
            }
            if (_latchedJob != null)
            {
                foreach (var x in _latchedJob.Properties()) if (tele[x.Name] == null || string.IsNullOrWhiteSpace(tele[x.Name].ToString())) tele[x.Name] = x.Value.DeepClone();
                tele["on_job"] = true; tele["job_latched"] = true; tele["job_latch_key"] = _latchedJobKey;
            }
            return tele;
        }
'''

new='''        private JObject StabilizeJobTelemetry(JObject tele)
        {
            if (tele == null) return null;

            string cargo = TextAny(tele, "cargo_name", "job.cargoName", "job.cargo");
            string cargoId = TextAny(tele, "cargo_id", "job.cargoId", "Job.CargoId");
            double mass = NumberAny(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
            double planned = NumberAny(tele, "planned_distance_km", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
            double remaining = NumberAny(tele, "remaining_km");
            bool rawLoaded = (!string.IsNullOrWhiteSpace(cargo) || !string.IsNullOrWhiteSpace(cargoId)) && mass > 0;
            bool delivered = BoolAny(tele, "gameplay.jobDelivered", "jobDelivered");
            bool cancelled = BoolAny(tele, "gameplay.jobCancelled", "jobCancelled", "gameplay.jobCanceled", "jobCanceled");

            // TruckSim GPS pode deixar jobCancelled/jobDelivered presos em true.
            // Um pacote que ainda prova carga + peso sempre vence esses flags antigos.
            if (_latchedJob != null && !rawLoaded && (delivered || cancelled))
            {
                string eventKey = _latchedJobKey;
                string evt = delivered ? "delivered" : "cancelled";
                ClientStore.Log((delivered ? "JOB DELIVERED | " : "JOB CANCELLED | ") + JobSummary(_latchedJob));
                tele["gat_schema"] = "job-v1";
                tele["gat_job_state"] = "ended";
                tele["gat_job_event"] = evt;
                tele["gat_job_event_key"] = eventKey;
                tele["job_latched"] = false;
                _latchedJob = null;
                _latchedJobKey = string.Empty;
                return tele;
            }

            if (_latchedJob == null && rawLoaded)
            {
                _latchedJob = new JObject();
                CopyValue(tele,_latchedJob,"cargo_name","cargo_name","job.cargoName","job.cargo");
                CopyValue(tele,_latchedJob,"cargo_id","cargo_id","job.cargoId","Job.CargoId");
                CopyValue(tele,_latchedJob,"mass_kg","mass_kg","cargoMass","cargo_mass","job.cargoMass","Job.CargoMass");
                CopyValue(tele,_latchedJob,"source_city","source_city","job.sourceCity");
                CopyValue(tele,_latchedJob,"source_city_id","source_city_id","job.sourceCityId","Job.SourceCityId");
                CopyValue(tele,_latchedJob,"destination_city","destination_city","job.destinationCity");
                CopyValue(tele,_latchedJob,"destination_city_id","destination_city_id","job.destinationCityId","Job.DestinationCityId");
                _latchedJob["planned_distance_km"] = planned > 0 ? planned : remaining;
                _latchedJob["job_latched"] = true;
                _latchedJobKey = !string.IsNullOrWhiteSpace(cargoId) ? cargoId : cargo + "|" + mass.ToString("0");
                ClientStore.Log("JOB STARTED | " + JobSummary(_latchedJob));
            }

            if (_latchedJob != null)
            {
                foreach (var x in _latchedJob.Properties())
                    if (tele[x.Name] == null || string.IsNullOrWhiteSpace(tele[x.Name].ToString()))
                        tele[x.Name] = x.Value.DeepClone();
                tele["on_job"] = true;
                tele["job_latched"] = true;
                tele["job_latch_key"] = _latchedJobKey;
                tele["gat_schema"] = "job-v1";
                tele["gat_job_state"] = "active";
                tele["gat_job_event"] = "";
            }
            return tele;
        }
'''

if old not in s:
    raise SystemExit('StabilizeJobTelemetry 1.0.24 esperado nao encontrado')
s=s.replace(old,new,1)

if 'private const string CurrentVersion = "1.0.24";' not in s:
    raise SystemExit('versao 1.0.24 nao encontrada')
s=s.replace('private const string CurrentVersion = "1.0.24";','private const string CurrentVersion = "1.0.25";',1)
s=s.replace('GAT Telemetria C# 1.0.24 TESTE','GAT Telemetria C# 1.0.25 TESTE')
s=s.replace('C# WinForms 1.0.24','C# WinForms 1.0.25')
main.write_text(s,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.24.0','1.0.25.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.24?','Atualizar GAT Telemetria para 1.0.25?')
i=i.replace('GAT Telemetria C# 1.0.24 atualizado.','GAT Telemetria C# 1.0.25 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.24_CENTRAL_LATCH_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.25_JOB_CONTRACT_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

text=main.read_text(encoding='utf-8')
checks=[
    'CurrentVersion = "1.0.25"',
    'bool rawLoaded =',
    '&& !rawLoaded && (delivered || cancelled)',
    'tele["gat_schema"] = "job-v1"',
    'tele["gat_job_state"] = "active"',
    'tele["gat_job_event"] = evt',
    'tele["job_latched"] = true',
]
for value in checks:
    if value not in text:
        raise SystemExit('patch 1.0.25 incompleto: '+value)
print('GAT Telemetria 1.0.25: contrato canonico job-v1 e protecao contra flags sticky do TruckSim GPS')
