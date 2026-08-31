from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

m=main.read_text(encoding='utf-8')
old='''                if (string.Equals(integrity.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: MOD PROIBIDO - ENTREGA NAO VAI CONTAR";
                    return;
                }
                if (!string.Equals(integrity.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: INTEGRIDADE DE MODS NAO VERIFICADA";
                    return;
                }
'''
new='''                // A integridade de mods continua sendo enviada para auditoria, mas nao
                // bloqueia nem esconde o estado da carga. O GAT aceita qualquer mercado
                // do ETS2/World of Trucks; a validacao oficial fica apenas nas regras do
                // trabalho selecionado (carga compativel, rota e distancia minima).
'''
if old not in m:
    raise SystemExit('bloco de integridade bloqueante nao encontrado')
m=m.replace(old,new,1)

# Mantem a coleta de gat_integrity_* para auditoria, sem impedir a telemetria oficial.
m=m.replace('private const string CurrentVersion = "1.0.20";', 'private const string CurrentVersion = "1.0.21";')
m=m.replace('GAT Telemetria C# 1.0.20 TESTE','GAT Telemetria C# 1.0.21 TESTE')
m=m.replace('C# WinForms 1.0.20','C# WinForms 1.0.21')
main.write_text(m,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.20.0','1.0.21.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.20?','Atualizar GAT Telemetria para 1.0.21?')
i=i.replace('GAT Telemetria C# 1.0.20 atualizado.','GAT Telemetria C# 1.0.21 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.20_VINCULO_CLOUDFLARE_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.21_MERCADO_LIVRE_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

checks=[
    (main,'CurrentVersion = "1.0.21"'),
    (main,'gat_integrity_status'),
    (main,'SendTelemetryAsync(AccountAuthority'),
    (main,'mission_event'),
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'):
        raise SystemExit('patch 1.0.21 incompleto: '+text)
if 'INTEGRIDADE DE MODS NAO VERIFICADA' in main.read_text(encoding='utf-8'):
    raise SystemExit('mensagem bloqueante de integridade ainda presente')
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.21_MERCADO_LIVRE_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome do atualizador 1.0.21 nao aplicado')
print('GAT Telemetria 1.0.21: mercados livres e integridade apenas informativa')
