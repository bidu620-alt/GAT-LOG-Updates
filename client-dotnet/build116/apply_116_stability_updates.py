from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'

if not journal.exists():
    raise SystemExit('TripJournal.cs nao encontrado; aplique build114/115 primeiro')

s=journal.read_text(encoding='utf-8')
# Picos impossíveis não podem gerar multa nem dano falso.
needle='''            var now = DateTime.UtcNow;
            if (speedKmh <= 91.0)'''
repl='''            var now = DateTime.UtcNow;
            if (double.IsNaN(speedKmh) || double.IsInfinity(speedKmh) || speedKmh > 200.0)
            {
                ClientStore.Log("telemetria ignorada: pico de velocidade invalido");
                return;
            }
            if (speedKmh <= 91.0)'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'speedKmh > 200.0' not in s:
    raise SystemExit('UpdateSpeedFine nao encontrado')

old='''        private static double NormalizePercent(double v)
        {
            if (v < 0) return -1;
            if (v <= 1.01) return v * 100.0;
            return v;
        }'''
new='''        private static double NormalizePercent(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) return -1;
            if (v <= 1.01) v = v * 100.0;
            if (v > 100.0) return -1;
            return v;
        }'''
if old in s:
    s=s.replace(old,new,1)
elif 'v > 100.0' not in s:
    raise SystemExit('NormalizePercent nao encontrado')

# Distância remanescente absurda não entra no diário.
needle='''                double speed = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));
                double cargoDamage'''
repl='''                if (remaining > 15000) remaining = -1;
                double speed = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));
                double cargoDamage'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'remaining > 15000' not in s:
    raise SystemExit('leitura de distancia nao encontrada')
journal.write_text(s,encoding='utf-8')

m=main.read_text(encoding='utf-8')
if 'private readonly Timer _updateTimer' not in m:
    m=m.replace('private readonly Timer _timer = new Timer { Interval = 1000 };','private readonly Timer _timer = new Timer { Interval = 1000 };\n        private readonly Timer _updateTimer = new Timer { Interval = 30 * 60 * 1000 };',1)
    m=m.replace('_timer.Start();','_timer.Start();\n            _updateTimer.Tick += async (s, e) => await CheckUpdateAsync(false);\n            _updateTimer.Start();',1)
    m=m.replace('_timer.Stop();\n                _api.Dispose();','_timer.Stop();\n                _updateTimer.Stop();\n                _api.Dispose();',1)

m=m.replace('private const string CurrentVersion = "1.0.15";', 'private const string CurrentVersion = "1.0.16";')
m=m.replace('GAT Telemetria C# 1.0.15 TESTE','GAT Telemetria C# 1.0.16 TESTE')
m=m.replace('C# WinForms 1.0.15','C# WinForms 1.0.16')
main.write_text(m,encoding='utf-8')

for path in (proj,installer,installer_proj):
    x=path.read_text(encoding='utf-8')
    x=x.replace('1.0.15.0','1.0.16.0')
    x=x.replace('1.0.15','1.0.16')
    # Depois da troca genérica, normaliza qualquer nome legado para o nome final ESTAVEL.
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_REGRAS_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_ESTAVEL_TESTE')
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_JOURNAL_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_ESTAVEL_TESTE')
    x=x.replace('GAT_TELEMETRIA_DOTNET_SETUP_1.0.16_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_ESTAVEL_TESTE')
    path.write_text(x,encoding='utf-8')

checks=[('journal','speedKmh > 200.0'),('journal','v > 100.0'),('journal','remaining > 15000'),('main','CurrentVersion = "1.0.16"'),('main','_updateTimer')]
for where,text in checks:
    target=journal.read_text(encoding='utf-8') if where=='journal' else main.read_text(encoding='utf-8')
    if text not in target: raise SystemExit('patch incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.16_ESTAVEL_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome final do atualizador 1.0.16 nao aplicado')
print('GAT Telemetria 1.0.16: proteção contra picos inválidos, atualização periódica e nome do atualizador corrigido')
