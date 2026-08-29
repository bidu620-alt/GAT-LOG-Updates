from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
tele=Path('client-dotnet/GatTelemetry/TelemetryEngine.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=tele.read_text(encoding='utf-8')
needle='            CopyAlias(m, "job.market", "job_market");\n'
fields=('            CopyAlias(m, "truck.placement.x", "map_x");\n'
        '            CopyAlias(m, "truck.placement.z", "map_z");\n'
        '            CopyAlias(m, "truck.placement.heading", "map_heading");\n'
        '            CopyAlias(m, "truck.make", "truck_make");\n'
        '            CopyAlias(m, "truck.model", "truck_model");\n')
if '"map_heading"' not in s:
    if needle not in s: raise SystemExit('job_market alias nao encontrado; aplique 1.0.7 antes')
    s=s.replace(needle, needle+fields, 1)
tele.write_text(s,encoding='utf-8')

s=main.read_text(encoding='utf-8')
s=s.replace('private const string CurrentVersion = "1.0.7";', 'private const string CurrentVersion = "1.0.8";')
s=s.replace('GAT Telemetria C# 1.0.7 TESTE','GAT Telemetria C# 1.0.8 TESTE')
s=s.replace('C# WinForms 1.0.7','C# WinForms 1.0.8')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.7.0</Version>','<Version>1.0.8.0</Version>')
s=s.replace('<FileVersion>1.0.7.0</FileVersion>','<FileVersion>1.0.8.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.7.0</AssemblyVersion>','<AssemblyVersion>1.0.8.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.7?','Atualizar GAT Telemetria para 1.0.8?')
s=s.replace('GAT Telemetria C# 1.0.7 atualizado.','GAT Telemetria C# 1.0.8 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.7_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.8_TESTE')
installer_proj.write_text(s,encoding='utf-8')
print('GAT Telemetria 1.0.8 live map and truck fields applied')
