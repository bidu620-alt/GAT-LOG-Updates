from pathlib import Path
import re

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.31"' in c:
    c=c.replace('InternalVersion = "1.0.31"','InternalVersion = "1.0.32"',1)
elif 'InternalVersion = "1.0.32"' not in c:
    raise SystemExit('versao 1.0.31 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Guarda o detalhamento das penalidades junto da entrega.
if 'BaseXP' not in s:
    m=re.search(r'(\tReceiptID\s+string\s+`json:"receipt_id,omitempty"`\n)',s)
    if not m: raise SystemExit('ReceiptID de gatDelivery nao encontrado')
    extra=(
        '\tBaseXP           int     `json:"base_xp,omitempty"`\n'
        '\tPenaltyXP        int     `json:"penalty_xp,omitempty"`\n'
        '\tSpeedFines       int     `json:"speed_fines,omitempty"`\n'
        '\tSpeedPenaltyXP   int     `json:"speed_penalty_xp,omitempty"`\n'
        '\tCargoPenaltyXP   int     `json:"cargo_penalty_xp,omitempty"`\n'
        '\tTruckPenaltyXP   int     `json:"truck_penalty_xp,omitempty"`\n'
        '\tCargoDamagePct   float64 `json:"cargo_damage_pct,omitempty"`\n'
        '\tTruckDamagePct   float64 `json:"truck_damage_delta_pct,omitempty"`\n'
    )
    s=s[:m.end()]+extra+s[m.end():]

# O recibo do cliente passa a carregar os dados de conducao observados durante a viagem.
if 'SpeedFines' not in s[s.find('type gatTripCompleteRequest struct'):s.find('func (a *agent) accountTripComplete(')]:
    needle='\tCompletedAt              string  `json:"completed_at"`\n'
    pos=s.find(needle,s.find('type gatTripCompleteRequest struct'))
    if pos<0: raise SystemExit('CompletedAt do recibo nao encontrado')
    end=pos+len(needle)
    extra=(
        '\tSpeedFines               int     `json:"speed_fines"`\n'
        '\tCargoDamagePct           float64 `json:"cargo_damage_pct"`\n'
        '\tTruckDamageStartPct      float64 `json:"truck_damage_start_pct"`\n'
        '\tTruckDamageMaxPct        float64 `json:"truck_damage_max_pct"`\n'
    )
    s=s[:end]+extra+s[end:]

rules=r'''
func gatDamagePct(v float64) float64 {
	if v < 0 { return v }
	if v <= 1.01 { return v * 100.0 }
	return v
}

func gatCargoDamagePenalty(pct float64) int {
	pct=gatDamagePct(pct)
	if pct <= 0 { return 0 }
	if pct <= 3 { return 3 }
	if pct <= 7 { return 5 }
	if pct <= 15 { return 10 }
	return 15
}

func gatTruckDamagePenalty(delta float64) int {
	if delta <= 0 { return 0 }
	if delta <= 5 { return 3 }
	if delta <= 10 { return 5 }
	if delta <= 20 { return 10 }
	return 15
}

func gatTripRuleBreakdown(distance float64, q gatTripCompleteRequest) (base int, cargoPenalty int, truckPenalty int, speedPenalty int, penalty int, final int, truckDelta float64) {
	base=gatXPForDistance(distance)
	cargoPenalty=gatCargoDamagePenalty(q.CargoDamagePct)
	start:=gatDamagePct(q.TruckDamageStartPct); maxd:=gatDamagePct(q.TruckDamageMaxPct)
	if q.TruckDamageStartPct >= 0 && q.TruckDamageMaxPct >= 0 && maxd > start { truckDelta=maxd-start }
	truckPenalty=gatTruckDamagePenalty(truckDelta)
	fines:=q.SpeedFines; if fines<0 { fines=0 }; if fines>100 { fines=100 }
	speedPenalty=fines*3
	penalty=cargoPenalty+truckPenalty+speedPenalty
	final=base-penalty; if final<0 { final=0 }
	return
}

func gatApplyRulesToDelivery(d *gatDelivery, distance float64, q gatTripCompleteRequest) {
	if d==nil { return }
	base,cargoPenalty,truckPenalty,speedPenalty,penalty,final,delta:=gatTripRuleBreakdown(distance,q)
	d.BaseXP=base; d.CargoPenaltyXP=cargoPenalty; d.TruckPenaltyXP=truckPenalty; d.SpeedPenaltyXP=speedPenalty
	d.PenaltyXP=penalty; d.XPAwarded=final; d.SpeedFines=q.SpeedFines
	d.CargoDamagePct=gatDamagePct(q.CargoDamagePct); d.TruckDamagePct=delta
}

'''
if 'func gatTripRuleBreakdown(' not in s:
    marker='func (a *agent) accountTripComplete('
    pos=s.find(marker)
    if pos<0: raise SystemExit('accountTripComplete nao encontrado')
    s=s[:pos]+rules+s[pos:]

# XP zero por penalidade e valido; somente historico legado sem BaseXP/PenaltyXP pode ser reconstruido.
old='\t\tif x <= 0 { x = gatXPForDistance(p.Deliveries[i].DistanceKm); p.Deliveries[i].XPAwarded = x }\n'
new='\t\tif x <= 0 && p.Deliveries[i].BaseXP == 0 && p.Deliveries[i].PenaltyXP == 0 { x = gatXPForDistance(p.Deliveries[i].DistanceKm); p.Deliveries[i].XPAwarded = x }\n'
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('gatTotalXPFromHistory nao encontrado')

# Se a telemetria ao vivo ja contou a entrega, o recibo posterior ainda aplica as penalidades.
old='\t\td.ReceiptID=q.TripID; _=saveGatProgress(all)\n\t\tjsonOut(w,200,map[string]any{"ok":true,"duplicate":true,"already_counted":true,"completed_now":false,"receipt_id":q.TripID,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}); return\n'
new='''\t\td.ReceiptID=q.TripID
\t\tdistance:=d.DistanceKm; if distance<=0 { distance=q.PlannedDistanceKm; if q.FirstObservedRemainingKm>distance { distance=q.FirstObservedRemainingKm } }
\t\tgatApplyRulesToDelivery(d,distance,q); p.XP=gatTotalXPFromHistory(p); _=saveGatProgress(all)
\t\tjsonOut(w,200,map[string]any{"ok":true,"duplicate":true,"already_counted":true,"completed_now":false,"receipt_id":q.TripID,"base_xp":d.BaseXP,"penalty_xp":d.PenaltyXP,"xp_awarded":d.XPAwarded,"speed_fines":d.SpeedFines,"cargo_damage_pct":d.CargoDamagePct,"truck_damage_delta_pct":d.TruckDamagePct,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}); return
'''
if old in s:
    s=s.replace(old,new,1)
elif 'already_counted' in s and 'gatApplyRulesToDelivery(d,distance,q)' not in s:
    raise SystemExit('bloco already_counted nao encontrado')

# Entrega normal: calcula 20 XP/100 km e subtrai multas/danos em pontos fixos.
old='''\txpNow:=gatXPForDistance(distance)
\tdelivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,XPAwarded:xpNow,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance}
'''
new='''\tdelivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance}
\tgatApplyRulesToDelivery(&delivery,distance,q)
\txpNow:=delivery.XPAwarded
'''
if old in s:
    s=s.replace(old,new,1)
elif 'gatApplyRulesToDelivery(&delivery,distance,q)' not in s:
    raise SystemExit('criacao da delivery nao encontrada')

old='\tjsonOut(w,200,map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"xp_awarded":xpNow,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP})\n'
new='\tjsonOut(w,200,map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"base_xp":delivery.BaseXP,"penalty_xp":delivery.PenaltyXP,"xp_awarded":xpNow,"speed_fines":delivery.SpeedFines,"speed_penalty_xp":delivery.SpeedPenaltyXP,"cargo_damage_pct":delivery.CargoDamagePct,"cargo_penalty_xp":delivery.CargoPenaltyXP,"truck_damage_delta_pct":delivery.TruckDamagePct,"truck_penalty_xp":delivery.TruckPenaltyXP,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP})\n'
if old in s:
    s=s.replace(old,new,1)
elif '"penalty_xp":delivery.PenaltyXP' not in s:
    raise SystemExit('resposta final do recibo nao encontrada')

checks=['InternalVersion = "1.0.32"','func gatTripRuleBreakdown(','SpeedPenaltyXP','CargoPenaltyXP','TruckPenaltyXP','gatApplyRulesToDelivery(&delivery,distance,q)','penalty_xp']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.32 aplicado: regras de dano e excesso de velocidade')
