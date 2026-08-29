from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=main.read_text(encoding='utf-8')

s=s.replace('private const string CurrentVersion = "1.0.10";', 'private const string CurrentVersion = "1.0.11";')
s=s.replace('GAT Telemetria C# 1.0.10 TESTE','GAT Telemetria C# 1.0.11 TESTE')
s=s.replace('C# WinForms 1.0.10','C# WinForms 1.0.11')

old='cmbMapMode.Items.AddRange(new object[] { "Mapa Base", "ProMods", "RBR", "Rotas Brasil", "Outro mapa" });'
new='cmbMapMode.Items.AddRange(new object[] { "Mapa Base", "ProMods", "RBR", "Rotas Brasil", "EAA", "Outro mapa" });'
if old in s:
    s=s.replace(old,new,1)
elif '"EAA"' not in s:
    raise SystemExit('Lista de mapas 1.0.10 não encontrada')

old='''                if (string.Equals(text, "Rotas Brasil", StringComparison.OrdinalIgnoreCase)) return "rotas_brasil";
                if (string.Equals(text, "Outro mapa", StringComparison.OrdinalIgnoreCase)) return "other";
'''
new='''                if (string.Equals(text, "Rotas Brasil", StringComparison.OrdinalIgnoreCase)) return "rotas_brasil";
                if (string.Equals(text, "EAA", StringComparison.OrdinalIgnoreCase)) return "eaa";
                if (string.Equals(text, "Outro mapa", StringComparison.OrdinalIgnoreCase)) return "other";
'''
if old in s:
    s=s.replace(old,new,1)
elif 'return "eaa";' not in s:
    raise SystemExit('CurrentMapModeKey não encontrado')

old='int index = key == "promods" ? 1 : key == "rbr" ? 2 : key == "rotas_brasil" ? 3 : key == "other" ? 4 : 0;'
new='int index = key == "promods" ? 1 : key == "rbr" ? 2 : key == "rotas_brasil" ? 3 : key == "eaa" ? 4 : key == "other" ? 5 : 0;'
if old in s:
    s=s.replace(old,new,1)
elif 'key == "eaa"' not in s:
    raise SystemExit('LoadMapMode 1.0.10 não encontrado')

main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.10.0</Version>','<Version>1.0.11.0</Version>')
s=s.replace('<FileVersion>1.0.10.0</FileVersion>','<FileVersion>1.0.11.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.10.0</AssemblyVersion>','<AssemblyVersion>1.0.11.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.10?','Atualizar GAT Telemetria para 1.0.11?')
s=s.replace('GAT Telemetria C# 1.0.10 atualizado.','GAT Telemetria C# 1.0.11 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.10_MAPAS_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.11_MAPAS_EAA_TESTE')
installer_proj.write_text(s,encoding='utf-8')

print('GAT Telemetria 1.0.11 EAA map applied')
