from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.23"' in c:
    c=c.replace('InternalVersion = "1.0.23"','InternalVersion = "1.0.24"',1)
elif 'InternalVersion = "1.0.24"' not in c:
    raise SystemExit('versao 1.0.23 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Calendario oficial GAT: horario de Brasilia (UTC-3).
# Homologacao ate 31/08/2026 23:59:59 BRT; oficial a partir de 01/09/2026 00:00 BRT.
old='''func gatProgressPath() string { return filepath.Join(core.DataDir(), "driver_progress.json") }
func gatMonth() string { return time.Now().UTC().Format("2006-01") }
'''
new='''func gatProgressPath() string { return filepath.Join(core.DataDir(), "driver_progress.json") }
func gatBrazilNow() time.Time { return time.Now().UTC().Add(-3 * time.Hour) }
func gatMonth() string { return gatBrazilNow().Format("2006-01") }
func gatRulesEnabled() bool { return !time.Now().UTC().Before(time.Date(2026, 9, 1, 3, 0, 0, 0, time.UTC)) }
func gatOperationMode() string { if gatRulesEnabled() { return "official" }; return "homologation" }
func gatOfficialStart() string { return "2026-09-01T00:00:00-03:00" }
func gatDeliveryMonth(v string) string {
    t, err := time.Parse(time.RFC3339, strings.TrimSpace(v))
    if err == nil { return t.UTC().Add(-3 * time.Hour).Format("2006-01") }
    if len(v) >= 7 { return v[:7] }
    return ""
}
func gatMonthlyKmFromHistory(p *gatDriverProgress) float64 {
    if p == nil { return 0 }
    total := 0.0
    for _, d := range p.Deliveries {
        if gatDeliveryMonth(d.CompletedAt) == p.Month && d.DistanceKm > 0 { total += d.DistanceKm }
    }
    return total
}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'func gatBrazilNow() time.Time' not in s:
    raise SystemExit('gatMonth patch point not found')

# KM mensal separado do KM total da carreira.
old='''\tMonthlyCompleted int           `json:"monthly_completed"`
\tTotalDeliveries  int           `json:"total_deliveries"`
'''
new='''\tMonthlyCompleted int           `json:"monthly_completed"`
\tMonthlyKm        float64       `json:"monthly_km"`
\tTotalDeliveries  int           `json:"total_deliveries"`
'''
if old in s:
    s=s.replace(old,new,1)
elif 'MonthlyKm        float64' not in s:
    raise SystemExit('MonthlyKm struct patch point not found')

# A missao existente acompanha automaticamente o modo atual.
old='''\tif p.CurrentMission != nil {
\t\tp.CurrentMission.MinKm = 100
\t\tp.CurrentMission.Market = "test_any"
\t\tp.CurrentMission.MinWeightKg = 0
\t\tp.CurrentMission.MaxWeightKg = 1000000
\t}
'''
new='''\tif p.CurrentMission != nil {
\t\tif gatRulesEnabled() {
\t\t\tif p.CurrentMission.Market != "world_of_trucks" || p.CurrentMission.MinKm < 800 {
\t\t\t\tminW, maxW := gatMissionBand()
\t\t\t\tp.CurrentMission.Market = "world_of_trucks"
\t\t\t\tp.CurrentMission.MinKm = 800
\t\t\t\tp.CurrentMission.MinWeightKg = minW
\t\t\t\tp.CurrentMission.MaxWeightKg = maxW
\t\t\t\tgatClearMissionTrip(p.CurrentMission)
\t\t\t}
\t\t} else {
\t\t\tp.CurrentMission.MinKm = 0
\t\t\tp.CurrentMission.Market = "test_any"
\t\t\tp.CurrentMission.MinWeightKg = 0
\t\t\tp.CurrentMission.MaxWeightKg = 0
\t\t}
\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'p.CurrentMission.MinKm = 0' not in s:
    raise SystemExit('current mission homologation patch point not found')

# Virada mensal local e reconstrução do KM mensal pelo histórico.
old='''\tif p.Month != nowMonth {
\t\tp.Month = nowMonth
\t\tp.MonthlyCompleted = 0
\t\tp.CurrentMission = nil
\t\tp.LastOnJob = false
\t}
\treturn p
'''
new='''\tif p.Month != nowMonth {
\t\tp.Month = nowMonth
\t\tp.MonthlyCompleted = 0
\t\tp.MonthlyKm = 0
\t\tp.CurrentMission = nil
\t\tp.LastOnJob = false
\t}
\tp.MonthlyKm = gatMonthlyKmFromHistory(p)
\treturn p
'''
if old in s:
    s=s.replace(old,new,1)
elif 'p.MonthlyKm = gatMonthlyKmFromHistory(p)' not in s:
    raise SystemExit('month rollover patch point not found')

# Perfil publico/site recebe modo e KM do mês.
old='''\t\t"user": p.User, "month": p.Month, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40,
\t\t"total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP,
'''
new='''\t\t"user": p.User, "month": p.Month, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "monthly_km": p.MonthlyKm,
\t\t"total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP,
\t\t"operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(),
'''
if old in s:
    s=s.replace(old,new,1)
elif '"monthly_km": p.MonthlyKm' not in s:
    raise SystemExit('public profile monthly km patch point not found')

# Ranking: missões do mês, depois KM DO MÊS; nunca KM total da carreira.
old='''\t\tif list[i].MonthlyCompleted != list[j].MonthlyCompleted { return list[i].MonthlyCompleted > list[j].MonthlyCompleted }
\t\tif list[i].TotalKm != list[j].TotalKm { return list[i].TotalKm > list[j].TotalKm }
'''
new='''\t\tif list[i].MonthlyCompleted != list[j].MonthlyCompleted { return list[i].MonthlyCompleted > list[j].MonthlyCompleted }
\t\tif list[i].MonthlyKm != list[j].MonthlyKm { return list[i].MonthlyKm > list[j].MonthlyKm }
'''
if old in s:
    s=s.replace(old,new,1)
elif 'list[i].MonthlyKm != list[j].MonthlyKm' not in s:
    raise SystemExit('ranking sort patch point not found')

old='''\tfor _, p := range list { out = append(out, map[string]any{"user": p.User, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP, "level": gatLevel(p.XP), "points": p.Points}) }
\tjsonOut(w, 200, map[string]any{"ok": true, "month": gatMonth(), "ranking": out})
'''
new='''\tfor _, p := range list { out = append(out, map[string]any{"user": p.User, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "monthly_km": p.MonthlyKm, "total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP, "level": gatLevel(p.XP), "points": p.Points}) }
\tjsonOut(w, 200, map[string]any{"ok": true, "month": gatMonth(), "operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(), "ranking": out})
'''
if old in s:
    s=s.replace(old,new,1)
elif '"operation_mode": gatOperationMode()' not in s:
    raise SystemExit('ranking response patch point not found')

# Botão PEGAR TRABALHO continua existindo. Homologação: sem regra. Oficial: regra padrão.
old='''\tif p.CurrentMission == nil {
\t\tseq := p.MonthlyCompleted + 1; now := time.Now().UTC()
\t\tp.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "test_any", MinKm: 100, MinWeightKg: 0, MaxWeightKg: 1000000, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
\t}
'''
new='''\tif p.CurrentMission == nil {
\t\tseq := p.MonthlyCompleted + 1; now := time.Now().UTC()
\t\tif gatRulesEnabled() {
\t\t\tminW, maxW := gatMissionBand()
\t\t\tp.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "world_of_trucks", MinKm: 800, MinWeightKg: minW, MaxWeightKg: maxW, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
\t\t} else {
\t\t\tp.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "test_any", MinKm: 0, MinWeightKg: 0, MaxWeightKg: 0, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
\t\t}
\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'Market: "test_any", MinKm: 0' not in s:
    raise SystemExit('work take mode patch point not found')

# Telemetria: na homologação qualquer job detectado inicia; as regras só valem no modo oficial.
old='''\tvalidation := map[string]any{"on_job": onJob, "world_of_trucks": true, "test_mode": true, "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}
'''
new='''\trulesEnabled := gatRulesEnabled()
\tvalidation := map[string]any{"on_job": onJob, "world_of_trucks": gatIsWorldOfTrucks(market), "test_mode": !rulesEnabled, "rules_enabled": rulesEnabled, "operation_mode": gatOperationMode(), "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'rulesEnabled := gatRulesEnabled()' not in s:
    raise SystemExit('validation mode patch point not found')

old='''\t\tvalidation["distance_ok"] = km >= m.MinKm; validation["weight_ok"] = mass > 0
'''
new='''\t\tif rulesEnabled {
\t\t\tvalidation["distance_ok"] = km >= m.MinKm
\t\t\tvalidation["weight_ok"] = mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0
\t\t} else {
\t\t\tvalidation["distance_ok"] = true
\t\t\tvalidation["weight_ok"] = true
\t\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'validation["distance_ok"] = true' not in s:
    raise SystemExit('validation rules patch point not found')

old='''\t\tif m.State == "assigned" && onJob && km >= m.MinKm && mass > 0 {
\t\t\tm.State = "active"; m.StartedAt = time.Now().UTC().Format(time.RFC3339); m.Cargo = cargo; m.Source = source; m.Destination = destination; m.WeightKg = mass; m.StartKm = km; m.LastKm = km; started = true
'''
new='''\t\tcanStart := onJob
\t\tif rulesEnabled { canStart = canStart && gatIsWorldOfTrucks(market) && km >= m.MinKm && mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0 }
\t\tif m.State == "assigned" && canStart {
\t\t\tm.State = "active"; m.StartedAt = time.Now().UTC().Format(time.RFC3339); m.Cargo = cargo; m.Source = source; m.Destination = destination; m.WeightKg = mass; m.StartKm = km; m.LastKm = km; started = true
'''
if old in s:
    s=s.replace(old,new,1)
elif 'canStart := onJob' not in s:
    raise SystemExit('mission start mode patch point not found')

# KM mensal entra no mesmo evento atômico da entrega concluída.
old='''p.TotalDeliveries++; p.XP = p.TotalDeliveries * 100; p.TotalKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true'''
new='''p.TotalDeliveries++; p.XP = p.TotalDeliveries * 100; p.TotalKm += m.StartKm; p.MonthlyKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true'''
if old in s:
    s=s.replace(old,new,1)
elif 'p.MonthlyKm += m.StartKm' not in s:
    raise SystemExit('completion monthly km patch point not found')

# Respostas das rotas de trabalho informam claramente o modo atual.
s=s.replace('''"finished_month": p.MonthlyCompleted >= 40, "mission": p.CurrentMission})''',
            '''"finished_month": p.MonthlyCompleted >= 40, "operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(), "mission": p.CurrentMission})''')
s=s.replace('''"finished_month": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": nil})''',
            '''"finished_month": true, "completed": p.MonthlyCompleted, "goal": 40, "operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(), "mission": nil})''')
s=s.replace('''"ok": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": p.CurrentMission})''',
            '''"ok": true, "completed": p.MonthlyCompleted, "goal": 40, "operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(), "mission": p.CurrentMission})''')

# Telemetria devolve KM mensal e modo para diagnóstico do cliente/site.
old='''"monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "mission": p.CurrentMission, "validation": validation, "xp_awarded": xpAwarded, "xp_total": p.XP, "level": gatLevel(p.XP), "xp_rule_pending": false})'''
new='''"monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "monthly_km": p.MonthlyKm, "operation_mode": gatOperationMode(), "rules_enabled": gatRulesEnabled(), "official_start": gatOfficialStart(), "mission": p.CurrentMission, "validation": validation, "xp_awarded": xpAwarded, "xp_total": p.XP, "level": gatLevel(p.XP), "xp_rule_pending": false})'''
if old in s:
    s=s.replace(old,new,1)
elif '"monthly_km": p.MonthlyKm, "operation_mode": gatOperationMode()' not in s:
    raise SystemExit('telemetry response patch point not found')

# Lista Admin também mostra KM mensal via API.
old='''"monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "total_deliveries": p.TotalDeliveries,'''
new='''"monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "monthly_km": p.MonthlyKm, "total_deliveries": p.TotalDeliveries,'''
if old in s:
    s=s.replace(old,new,1)

# Ao apagar entrega do mês, corrige também o KM mensal usando o mês de Brasília.
old='''\t\tif strings.HasPrefix(removed.CompletedAt, gatMonth()) && p.MonthlyCompleted > 0 { p.MonthlyCompleted--; p.CurrentMission = nil; p.LastOnJob = false }
'''
new='''\t\tif gatDeliveryMonth(removed.CompletedAt) == gatMonth() {
\t\t\tif p.MonthlyCompleted > 0 { p.MonthlyCompleted-- }
\t\t\tif removed.DistanceKm > 0 { p.MonthlyKm -= removed.DistanceKm; if p.MonthlyKm < 0 { p.MonthlyKm = 0 } }
\t\t\tp.CurrentMission = nil; p.LastOnJob = false
\t\t}
'''
if old in s:
    s=s.replace(old,new,1)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.24: horario Brasil, homologacao sem regras, ranking mensal e virada oficial preparados')
