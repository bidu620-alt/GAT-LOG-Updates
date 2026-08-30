from pathlib import Path

root=Path('.')
base=root/'client-dotnet/build118/apply_118_mod_integrity.py'
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'
journal=root/'client-dotnet/GatTelemetry/TripJournal.cs'
scanner=root/'client-dotnet/GatTelemetry/ModIntegrity.cs'

try:
    exec(compile(base.read_text(encoding='utf-8'),str(base),'exec'))
except SystemExit as ex:
    # A 1.0.10 inseriu os campos gat_map entre gat_client_version e lblTruck.
    # A base 1.0.18 ja deixou scanner + recibo prontos antes de chegar nesse ponto.
    if 'telemetria central para integridade nao encontrada' not in str(ex):
        raise

m=main.read_text(encoding='utf-8')
if 'gat_integrity_status' not in m:
    marker='            tele["gat_client_version"] = CurrentVersion;\n'
    insert='''            var integrity = ModIntegrityScanner.Check();
            tele["gat_integrity_status"] = integrity.Status ?? "unknown";
            tele["gat_integrity_reason"] = integrity.Reason ?? string.Empty;
            tele["gat_integrity_evidence_hash"] = integrity.EvidenceHash ?? string.Empty;
            if (integrity.Matches != null && integrity.Matches.Length > 0)
                tele["gat_integrity_matches"] = JArray.FromObject(integrity.Matches);
'''
    if marker not in m: raise SystemExit('gat_client_version nao encontrado no envio central')
    m=m.replace(marker,marker+insert,1)

if 'MOD PROIBIDO - ENTREGA NAO VAI CONTAR' not in m:
    needle='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                if (ApiClient.Bool(progress.Json["completed_now"]))
'''
    repl='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))
            {
                if (string.Equals(integrity.Status, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: MOD PROIBIDO - ENTREGA NAO VAI CONTAR";
                    return;
                }
                if (!string.Equals(integrity.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    lblTelemetry.Text = "Central GAT: INTEGRIDADE DE MODS NAO VERIFICADA";
                    return;
                }
                if (ApiClient.Bool(progress.Json["completed_now"]))
'''
    if needle not in m: raise SystemExit('retorno do envio central nao encontrado')
    m=m.replace(needle,repl,1)

if 'integrity_mod_blocked' not in m:
    old='''                if (err == "actual_distance_below_minimum" || err == "distance_not_verified" || err == "vehicle_changed" || err == "odometer_discontinuity")
                {
                    _tripJournal.MarkSent(receipt.TripId);
                    lblTelemetry.Text = err == "actual_distance_below_minimum"
                        ? "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE"
                        : "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO";
'''
    new='''                if (err == "actual_distance_below_minimum" || err == "distance_not_verified" || err == "vehicle_changed" || err == "odometer_discontinuity" || err == "integrity_mod_blocked" || err == "integrity_not_verified")
                {
                    _tripJournal.MarkSent(receipt.TripId);
                    lblTelemetry.Text = err == "actual_distance_below_minimum"
                        ? "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE"
                        : err == "integrity_mod_blocked"
                            ? "Central GAT: ENTREGA NAO VALIDADA - MOD PROIBIDO"
                            : err == "integrity_not_verified"
                                ? "Central GAT: ENTREGA NAO VALIDADA - INTEGRIDADE"
                                : "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO";
'''
    if old not in m: raise SystemExit('tratamento de recibo 1.0.17 nao encontrado')
    m=m.replace(old,new,1)

m=m.replace('private const string CurrentVersion = "1.0.17";', 'private const string CurrentVersion = "1.0.18";')
m=m.replace('GAT Telemetria C# 1.0.17 TESTE','GAT Telemetria C# 1.0.18 TESTE')
m=m.replace('C# WinForms 1.0.17','C# WinForms 1.0.18')
main.write_text(m,encoding='utf-8')

for path in (proj,installer,installer_proj):
    x=path.read_text(encoding='utf-8')
    x=x.replace('1.0.17.0','1.0.18.0')
    x=x.replace('1.0.17','1.0.18')
    x=x.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_ANTIBURLA_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_INTEGRIDADE_TESTE')
    path.write_text(x,encoding='utf-8')

checks=[
    (journal,'integrity_status'),(journal,'ApplyModIntegrity'),(main,'gat_integrity_status'),
    (main,'MOD PROIBIDO - ENTREGA NAO VAI CONTAR'),(main,'CurrentVersion = "1.0.18"'),
    (scanner,'damage_mod_detected'),(scanner,'game.log.txt'),(main,'gat_map')
]
for path,text in checks:
    if text not in path.read_text(encoding='utf-8'): raise SystemExit('patch 1.0.18 v2 incompleto: '+text)
if 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.18_INTEGRIDADE_TESTE' not in installer_proj.read_text(encoding='utf-8'):
    raise SystemExit('nome final do atualizador 1.0.18 nao aplicado')

print('GAT Telemetria 1.0.18 v2: integridade de mods aplicada preservando selecao de mapa')
