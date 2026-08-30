from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.34"' in c:
    c=c.replace('InternalVersion = "1.0.34"','InternalVersion = "1.0.35"',1)
elif 'InternalVersion = "1.0.35"' not in c:
    raise SystemExit('versao 1.0.34 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Regra oficial: 30 trabalhos, minimo 500 km em qualquer mercado.
repls=[
    ('"min_km":250','"min_km":500'),
    ('"min_km": 250','"min_km": 500'),
    ('MinKm:250','MinKm:500'),
    ('MinKm: 250','MinKm: 500'),
    ('m.MinKm=250','m.MinKm=500'),
    ('m.MinKm = 250','m.MinKm = 500'),
    ('required=250','required=500'),
    ('required = 250','required = 500'),
    ('km >= 250 && gatCargoMatch','km >= 500 && gatCargoMatch'),
    ('km < 250','km < 500'),
    ('distance_below_250','distance_below_500'),
    ('MinKm != 250','MinKm != 500'),
    ('MinKm = 250','MinKm = 500'),
]
for old,new in repls:
    s=s.replace(old,new)

# Bonus perfeito acompanha a distancia minima oficial.
s=s.replace('if distance >= 250 && fines == 0 && q.CargoDamagePct <= 0.5 && truckDelta <= 0.5 { perfectBonus = 5 }',
            'if distance >= 500 && fines == 0 && q.CargoDamagePct <= 0.5 && truckDelta <= 0.5 { perfectBonus = 5 }')

# Rotas unicas no mes: A->B e B->A contam como o mesmo par de cidades.
route_support=r'''
func gatNormalizeCity(v string) string {
	return strings.ToLower(strings.TrimSpace(v))
}

func gatRouteKey(source, destination string) string {
	a := gatNormalizeCity(source); b := gatNormalizeCity(destination)
	if a == "" || b == "" { return "" }
	if a > b { a,b = b,a }
	return a + "|" + b
}

func gatRouteUsedThisMonth(p *gatDriverProgress, source, destination string) bool {
	if p == nil { return false }
	key := gatRouteKey(source,destination); if key == "" { return false }
	for _,d := range p.Deliveries {
		if gatDeliveryMonth(d.CompletedAt) != p.Month { continue }
		if gatRouteKey(d.Source,d.Destination) == key { return true }
	}
	return false
}

'''
if 'func gatRouteUsedThisMonth(' not in s:
    marker='func gatMissionBand() (float64, float64) {'
    pos=s.find(marker)
    if pos<0: raise SystemExit('gatMissionBand nao encontrado')
    s=s[:pos]+route_support+s[pos:]

# Estatisticas mensais usadas exclusivamente no ranking oficial.
monthly_support=r'''
func gatMonthlySafetyStats(p *gatDriverProgress) map[string]any {
	total,fines,penalty,perfect,clean := 0,0,0,0,0
	cargoSum,truckSum := 0.0,0.0; cargoCount,truckCount := 0,0
	if p != nil {
		for _,d := range p.Deliveries {
			if gatDeliveryMonth(d.CompletedAt) != p.Month { continue }
			total++
			f:=d.SpeedFines; if f<0 { f=0 }; fines+=f
			pen:=d.PenaltyXP; if pen<0 { pen=0 }; penalty+=pen
			if d.PerfectTrip || d.PerfectBonusXP>0 { perfect++ }
			if pen==0 { clean++ }
			cargoSum+=gatClamp(d.CargoDamagePct,0,100); cargoCount++
			truckSum+=gatClamp(d.TruckDamagePct,0,100); truckCount++
		}
	}
	avgCargo,avgTruck:=0.0,0.0
	if cargoCount>0 { avgCargo=cargoSum/float64(cargoCount) }
	if truckCount>0 { avgTruck=truckSum/float64(truckCount) }
	return map[string]any{"deliveries":total,"speed_fines":fines,"penalty_xp":penalty,"perfect_trips":perfect,"clean_trips":clean,"avg_cargo_damage_pct":avgCargo,"avg_truck_damage_pct":avgTruck}
}

'''
if 'func gatMonthlySafetyStats(' not in s:
    marker='func gatAchievements(p *gatDriverProgress) []map[string]any {'
    pos=s.find(marker)
    if pos<0: raise SystemExit('gatAchievements nao encontrado')
    s=s[:pos]+monthly_support+s[pos:]

# Perfil informa a regra oficial e os numeros mensais do ranking.
old='"safety": gatSafetyStats(p), "achievements": gatAchievements(p),'
new='"safety": gatSafetyStats(p), "monthly_safety": gatMonthlySafetyStats(p), "achievements": gatAchievements(p),'
if old in s:
    s=s.replace(old,new,1)
elif '"monthly_safety": gatMonthlySafetyStats(p)' not in s:
    raise SystemExit('perfil mensal de seguranca nao aplicado')

# Ranking principal: trabalhos -> viagens perfeitas -> menor penalidade -> menos multas.
start=s.find('func (a *agent) publicRanking(')
if start<0: raise SystemExit('publicRanking nao encontrado')
next_func=s.find('\nfunc (a *agent)',start+10)
if next_func<0: raise SystemExit('fim de publicRanking nao encontrado')
ranking=r'''func (a *agent) publicRanking(w http.ResponseWriter, r *http.Request) {
	if gatAccountCors(w, r) { return }
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock()
	all := loadGatProgress(); list := make([]*gatDriverProgress, 0, len(all)); changed := false
	for _, p := range all { before := p.Month; ensureGatProgress(all, p.User); if p.Month != before { changed = true }; list = append(list, p) }
	if changed { _ = saveGatProgress(all) }
	sort.Slice(list, func(i, j int) bool {
		if list[i].MonthlyCompleted != list[j].MonthlyCompleted { return list[i].MonthlyCompleted > list[j].MonthlyCompleted }
		si,sj := gatMonthlySafetyStats(list[i]),gatMonthlySafetyStats(list[j])
		pi,_:=si["perfect_trips"].(int); pj,_:=sj["perfect_trips"].(int); if pi!=pj { return pi>pj }
		peni,_:=si["penalty_xp"].(int); penj,_:=sj["penalty_xp"].(int); if peni!=penj { return peni<penj }
		fi,_:=si["speed_fines"].(int); fj,_:=sj["speed_fines"].(int); if fi!=fj { return fi<fj }
		return strings.ToLower(list[i].User) < strings.ToLower(list[j].User)
	})
	out := make([]map[string]any, 0, len(list))
	for _, p := range list {
		st:=gatMonthlySafetyStats(p)
		out=append(out,map[string]any{
			"user":p.User,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"monthly_km":p.MonthlyKm,
			"perfect_trips":st["perfect_trips"],"clean_trips":st["clean_trips"],"penalty_xp":st["penalty_xp"],"speed_fines":st["speed_fines"],
			"avg_cargo_damage_pct":st["avg_cargo_damage_pct"],"avg_truck_damage_pct":st["avg_truck_damage_pct"],
			"total_deliveries":p.TotalDeliveries,"total_km":p.TotalKm,"xp":p.XP,"level":gatLevel(p.XP),"points":p.Points,
		})
	}
	jsonOut(w,200,map[string]any{"ok":true,"month":gatMonth(),"operation_mode":gatOperationMode(),"rules_enabled":gatRulesEnabled(),"official_start":gatOfficialStart(),"min_km":500,"ranking_rule":"missions_perfect_penalties","ranking":out})
}
'''
s=s[:start]+ranking+s[next_func:]

# Recibo oficial rejeita rota repetida antes de contar a entrega.
needle='''\tif !gatCargoMatch(m,q.Cargo) {
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"cargo_mismatch","receipt_id":q.TripID,"cargo":q.Cargo,"mission":m}); return
\t}

\tdistance:=q.PlannedDistanceKm'''
repl='''\tif !gatCargoMatch(m,q.Cargo) {
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"cargo_mismatch","receipt_id":q.TripID,"cargo":q.Cargo,"mission":m}); return
\t}
\tif gatRouteUsedThisMonth(p,q.Source,q.Destination) {
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"route_already_used","source":q.Source,"destination":q.Destination,"receipt_id":q.TripID}); return
\t}

\tdistance:=q.PlannedDistanceKm'''
if needle in s:
    s=s.replace(needle,repl,1)
elif '"error":"route_already_used"' not in s:
    raise SystemExit('bloqueio de rota no recibo nao aplicado')

# Telemetria ao vivo aplica a mesma regra: 500 km, carga compativel e rota inedita.
old='''\t\tcanStart := onJob && km >= 500 && gatCargoMatch(m,cargo)
\t\tvalidation["distance_ok"] = km >= 500
\t\tvalidation["weight_ok"] = true
\t\tvalidation["cargo_ok"] = gatCargoMatch(m,cargo)'''
new='''\t\trouteOK := !gatRouteUsedThisMonth(p,source,destination)
\t\tcanStart := onJob && km >= 500 && gatCargoMatch(m,cargo) && routeOK
\t\tvalidation["distance_ok"] = km >= 500
\t\tvalidation["weight_ok"] = true
\t\tvalidation["cargo_ok"] = gatCargoMatch(m,cargo)
\t\tvalidation["route_ok"] = routeOK'''
if old in s:
    s=s.replace(old,new,1)
elif 'validation["route_ok"] = routeOK' not in s:
    raise SystemExit('bloqueio de rota na telemetria nao aplicado')

old='''\t\t\tif m.LastKm <= 15 && m.StartKm >= m.MinKm {
\t\t\t\tnow := time.Now().UTC(); m.State = "completed"; m.CompletedAt = now.Format(time.RFC3339)'''
new='''\t\t\tif m.LastKm <= 15 && m.StartKm >= m.MinKm && !gatRouteUsedThisMonth(p,m.Source,m.Destination) {
\t\t\t\tnow := time.Now().UTC(); m.State = "completed"; m.CompletedAt = now.Format(time.RFC3339)'''
if old in s:
    s=s.replace(old,new,1)
elif '!gatRouteUsedThisMonth(p,m.Source,m.Destination)' not in s:
    raise SystemExit('bloqueio de rota na conclusao ao vivo nao aplicado')

# Respostas e catalogo deixam explicitas as regras atuais.
s=s.replace('"xp_per_100_km":20,"catalog":gatPublicCatalog(p)', '"xp_per_100_km":20,"route_unique":true,"ranking_rule":"missions_perfect_penalties","catalog":gatPublicCatalog(p)')
s=s.replace('"goal":30,"mission":p.CurrentMission,"catalog":gatPublicCatalog(p),"xp_per_100_km":20', '"goal":30,"mission":p.CurrentMission,"catalog":gatPublicCatalog(p),"xp_per_100_km":20,"min_km":500,"route_unique":true')

checks=[
    'InternalVersion = "1.0.35"',
    'func gatRouteUsedThisMonth(',
    'func gatMonthlySafetyStats(',
    '"ranking_rule":"missions_perfect_penalties"',
    '"error":"route_already_used"',
    'canStart := onJob && km >= 500',
    'validation["route_ok"] = routeOK',
    'if distance >= 500 && fines == 0',
]
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch 1.0.35 incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.35: regras oficiais 500 km, rota unica e ranking por qualidade')
