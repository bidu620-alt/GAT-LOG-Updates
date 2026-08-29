from pathlib import Path

proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.6.0</Version>','<Version>1.0.7.0</Version>')
s=s.replace('<FileVersion>1.0.6.0</FileVersion>','<FileVersion>1.0.7.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.6.0</AssemblyVersion>','<AssemblyVersion>1.0.7.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.6?','Atualizar GAT Telemetria para 1.0.7?')
s=s.replace('GAT Telemetria C# 1.0.6 atualizado.','GAT Telemetria C# 1.0.7 atualizado.')
installer.write_text(s,encoding='utf-8')

ip=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')
s=ip.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.6_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.7_TESTE')
ip.write_text(s,encoding='utf-8')
