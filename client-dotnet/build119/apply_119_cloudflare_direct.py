from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

m=main.read_text(encoding='utf-8')

# A Conta GAT e a telemetria oficial passam a usar exclusivamente a Central Cloudflare.
old='private const string AccountAuthority = "https://douglas.tail4577e8.ts.net";'
new='private const string AccountAuthority = "https://api.gatlogets2.com.br";'
if old in m:
    m=m.replace(old,new,1)
elif new not in m:
    raise SystemExit('AccountAuthority nao encontrado')

# Atualiza versao sem alterar classificacao/catalogo de cargas.
m=m.replace('private const string CurrentVersion = "1.0.18";', 'private const string CurrentVersion = "1.0.19";')
m=m.replace('GAT Telemetria C# 1.0.18 TESTE','GAT Telemetria C# 1.0.19 TESTE')
m=m.replace('C# WinForms 1.0.18','C# WinForms 1.0.19')
main.write_text(m,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.18.0','1.0.19.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.18?','Atualizar GAT Telemetria para 1.0.19?')
i=i.replace('GAT Telemetria C# 1.0.18 atualizado.','GAT Telemetria C# 1.0.19 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_INTEGRIDADE_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.19_CLOUDFLARE_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

checks=[
    (main,'AccountAuthority = "https://api.gatlogets2.com.br"'),
    (main,'CurrentVersion = "1.0.19"'),
    (main,'SendCentralTelemetryAsync'),
    (main,'gat_integrity_status'),
    (main,'gat_map'),
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'):
        raise SystemExit('patch 1.0.19 incompleto: '+text)

if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.19_CLOUDFLARE_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome do atualizador 1.0.19 nao aplicado')

print('GAT Telemetria 1.0.19: Conta GAT e telemetria oficial apontando direto para Cloudflare')
