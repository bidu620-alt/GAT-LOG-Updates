from pathlib import Path

root=Path('.')
engine=root/'client-dotnet/GatTelemetry/TelemetryEngine.cs'
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

e=engine.read_text(encoding='utf-8')
needle='''            CopyAlias(m, "job.cargo", "cargo_name");
            CopyAlias(m, "job.cargoName", "cargo_name", true);
            CopyAlias(m, "job.sourceCity", "source_city");
            CopyAlias(m, "job.destinationCity", "destination_city");
            CopyAlias(m, "gameplay.onJob", "on_job");
'''
repl='''            CopyAlias(m, "job.cargo", "cargo_name");
            CopyAlias(m, "job.cargoName", "cargo_name", true);
            // IDs da SCS sao estaveis entre idiomas e mercados. A Central usa cargo_id
            // para validar a categoria; nomes/cidades ficam apenas para exibicao/historico.
            CopyAlias(m, "job.cargoId", "cargo_id");
            CopyAlias(m, "Job.CargoId", "cargo_id", true);
            CopyAlias(m, "job.cargo.id", "cargo_id", true);
            CopyAlias(m, "job.sourceCity", "source_city");
            CopyAlias(m, "job.sourceCityId", "source_city_id");
            CopyAlias(m, "Job.SourceCityId", "source_city_id", true);
            CopyAlias(m, "job.destinationCity", "destination_city");
            CopyAlias(m, "job.destinationCityId", "destination_city_id");
            CopyAlias(m, "Job.DestinationCityId", "destination_city_id", true);
            CopyAlias(m, "job.plannedDistanceKm", "planned_distance_km");
            CopyAlias(m, "Job.PlannedDistanceKm", "planned_distance_km", true);
            CopyAlias(m, "gameplay.onJob", "on_job");
'''
if needle not in e:
    raise SystemExit('bloco Normalize esperado nao encontrado')
e=e.replace(needle,repl,1)
engine.write_text(e,encoding='utf-8')

m=main.read_text(encoding='utf-8')
m=m.replace('private const string CurrentVersion = "1.0.21";', 'private const string CurrentVersion = "1.0.22";')
m=m.replace('GAT Telemetria C# 1.0.21 TESTE','GAT Telemetria C# 1.0.22 TESTE')
m=m.replace('C# WinForms 1.0.21','C# WinForms 1.0.22')
main.write_text(m,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.21.0','1.0.22.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.21?','Atualizar GAT Telemetria para 1.0.22?')
i=i.replace('GAT Telemetria C# 1.0.21 atualizado.','GAT Telemetria C# 1.0.22 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.21_MERCADO_LIVRE_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.22_CARGOID_KM_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

checks=[
    (engine,'"job.cargoId", "cargo_id"'),
    (engine,'"job.sourceCityId", "source_city_id"'),
    (engine,'"job.destinationCityId", "destination_city_id"'),
    (engine,'"job.plannedDistanceKm", "planned_distance_km"'),
    (main,'CurrentVersion = "1.0.22"'),
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'):
        raise SystemExit('patch 1.0.22 incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.22_CARGOID_KM_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome do atualizador 1.0.22 nao aplicado')
print('GAT Telemetria 1.0.22: cargoId/cityIds/plannedDistance normalizados para a Cloudflare')
