from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

s=main.read_text(encoding='utf-8')

# 1.0.23 aplicava o latch apenas na leitura usada pelo comboio opcional.
# A Central Cloudflare possui sua propria leitura em SendCentralTelemetryAsync;
# estabilizamos essa copia antes de envia-la ao site/API.
start=s.find('        private async Task SendCentralTelemetryAsync()')
end=s.find('        private void EnterClicked(', start)
if start < 0 or end < 0:
    raise SystemExit('SendCentralTelemetryAsync nao encontrado')
segment=s[start:end]
needle='''            JObject tele = await _telemetry.ReadAsync();
            _lastAccountTelemetry = DateTime.UtcNow;
'''
replacement='''            JObject tele = await _telemetry.ReadAsync();
            _lastAccountTelemetry = DateTime.UtcNow;
            tele = StabilizeJobTelemetry(tele);
'''
if 'tele = StabilizeJobTelemetry(tele);' not in segment:
    if needle not in segment:
        raise SystemExit('leitura central de telemetria nao encontrada')
    segment=segment.replace(needle,replacement,1)
    s=s[:start]+segment+s[end:]

# O login do comboio e opcional. Uma falha nele nao pode sobrescrever o estado
# do envio oficial para a Central GAT nem sugerir que a telemetria parou.
s=s.replace('            lblTelemetry.Text = "Envio: aguardando login";\n','')
s=s.replace('                lblTelemetry.Text = "Envio: aguardando login";\n','')
s=s.replace('                    lblTelemetry.Text = "Envio: aguardando login";\n','')

# Quando a Central aceitou um pacote com o latch ativo, deixe isso evidente.
old='''                else
                    lblTelemetry.Text = "Central GAT: ONLINE";
                return;
'''
new='''                else if (BoolAny(tele, "job_latched") || BoolAny(tele, "on_job"))
                    lblTelemetry.Text = "Central GAT: ONLINE • TRABALHO EM ANDAMENTO";
                else
                    lblTelemetry.Text = "Central GAT: ONLINE";
                return;
'''
if old in s:
    s=s.replace(old,new,1)
elif 'Central GAT: ONLINE • TRABALHO EM ANDAMENTO' not in s:
    raise SystemExit('status ONLINE da Central nao encontrado')

if 'private const string CurrentVersion = "1.0.23";' not in s:
    raise SystemExit('versao 1.0.23 nao encontrada')
s=s.replace('private const string CurrentVersion = "1.0.23";','private const string CurrentVersion = "1.0.24";',1)
s=s.replace('GAT Telemetria C# 1.0.23 TESTE','GAT Telemetria C# 1.0.24 TESTE')
s=s.replace('C# WinForms 1.0.23','C# WinForms 1.0.24')
main.write_text(s,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.23.0','1.0.24.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.23?','Atualizar GAT Telemetria para 1.0.24?')
i=i.replace('GAT Telemetria C# 1.0.23 atualizado.','GAT Telemetria C# 1.0.24 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.23_JOB_LATCH_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.24_CENTRAL_LATCH_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

checks=[
    'CurrentVersion = "1.0.24"',
    'Central GAT: ONLINE • TRABALHO EM ANDAMENTO',
    'job_latched',
    'private async Task SendCentralTelemetryAsync()',
]
text=main.read_text(encoding='utf-8')
for value in checks:
    if value not in text:
        raise SystemExit('patch 1.0.24 incompleto: '+value)
# Precisamos de duas chamadas: uma na Central e outra no fluxo opcional existente.
if text.count('StabilizeJobTelemetry(tele)') < 2:
    raise SystemExit('latch ainda nao esta aplicado no envio central e no fluxo opcional')

print('GAT Telemetria 1.0.24: job latch aplicado ao envio direto Cloudflare e status do comboio isolado')
