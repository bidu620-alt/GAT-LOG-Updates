from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.22"' in c:
    c=c.replace('InternalVersion = "1.0.22"','InternalVersion = "1.0.23"',1)
elif 'InternalVersion = "1.0.23"' not in c:
    raise SystemExit('versao 1.0.22 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# MODO TEMPORARIO DE TESTE:
# - qualquer mercado/carga
# - peso livre, desde que exista carga (>0 kg)
# - minimo 100 km
# Tambem converte a missao ja atribuida para nao exigir reset de conta.
needle='''\tif p.TotalDeliveries < 0 { p.TotalDeliveries = 0 }
\tp.XP = p.TotalDeliveries * 100
\tnowMonth := gatMonth()
'''
replacement='''\tif p.TotalDeliveries < 0 { p.TotalDeliveries = 0 }
\tp.XP = p.TotalDeliveries * 100
\tif p.CurrentMission != nil {
\t\tp.CurrentMission.MinKm = 100
\t\tp.CurrentMission.Market = "test_any"
\t\tp.CurrentMission.MinWeightKg = 0
\t\tp.CurrentMission.MaxWeightKg = 1000000
\t}
\tnowMonth := gatMonth()
'''
if 'p.CurrentMission.Market = "test_any"' not in s:
    if needle not in s: raise SystemExit('ensureGatProgress test patch point not found')
    s=s.replace(needle,replacement,1)

old='''\tif p.CurrentMission == nil {
\t\tminW, maxW := gatMissionBand(); seq := p.MonthlyCompleted + 1; now := time.Now().UTC()
\t\tp.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "world_of_trucks", MinKm: 800, MinWeightKg: minW, MaxWeightKg: maxW, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
\t}
'''
new='''\tif p.CurrentMission == nil {
\t\tseq := p.MonthlyCompleted + 1; now := time.Now().UTC()
\t\tp.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "test_any", MinKm: 100, MinWeightKg: 0, MaxWeightKg: 1000000, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'Market: "test_any", MinKm: 100' not in s:
    raise SystemExit('accountWorkTake mission creation point not found')

old='''\tvalidation := map[string]any{"on_job": onJob, "world_of_trucks": gatIsWorldOfTrucks(market), "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}
'''
new='''\tvalidation := map[string]any{"on_job": onJob, "world_of_trucks": true, "test_mode": true, "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}
'''
if old in s:
    s=s.replace(old,new,1)
elif '"test_mode": true' not in s:
    raise SystemExit('validation test mode point not found')

old='''\t\tvalidation["distance_ok"] = km >= m.MinKm; validation["weight_ok"] = mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0
'''
new='''\t\tvalidation["distance_ok"] = km >= m.MinKm; validation["weight_ok"] = mass > 0
'''
if old in s:
    s=s.replace(old,new,1)
elif 'validation["weight_ok"] = mass > 0' not in s:
    raise SystemExit('weight validation test point not found')

old='''\t\tif m.State == "assigned" && onJob && gatIsWorldOfTrucks(market) && km >= m.MinKm && mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0 {
'''
new='''\t\tif m.State == "assigned" && onJob && km >= m.MinKm && mass > 0 {
'''
if old in s:
    s=s.replace(old,new,1)
elif 'm.State == "assigned" && onJob && km >= m.MinKm && mass > 0' not in s:
    raise SystemExit('mission start test point not found')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.23: modo temporario de teste 100 km, qualquer carga')
