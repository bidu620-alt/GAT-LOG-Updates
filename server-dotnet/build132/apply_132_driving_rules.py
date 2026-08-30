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

# XP zero por penalidade e valido; somente historico legado sem BaseXP/PenaltyXP e reconstruido.
pat=r'if x <= 0 \{ x = gatXPForDistance\(p\.Deliveries\[i\]\.DistanceKm\); p\.Deliveries\[i\]\.XPAwarded = x \}'
repl='if x <= 0 && p.Deliveries[i].BaseXP == 0 && p.Deliveries[i].PenaltyXP == 0 { x = gatXPForDistance(p.Deliveries[i].DistanceKm); p.Deliveries[i].XPAwarded = x }'
s,n=re.subn(pat,repl,s,count=1)
if n==0 and repl not in s:
    raise SystemExit('gatTotalXPFromHistory nao encontrado')

req_start=s.find('type gatTripCompleteRequest struct {')
handler_start=s.find('func (a *agent) accountTripComplete(',req_start)
telemetry_start=s.find('func (a *agent) accountTelemetry(',handler_start)
if req_start<0 or handler_start<0 or telemetry_start<0:
    raise SystemExit('bloco de recibo 1.0.30 nao encontrado')

block=r'''type gatTripCompleteRequest struct {
	Driver                   string  `json:"driver"`
	TripID                   string  `json:"trip_id"`
	Cargo                    string  `json:"cargo"`
	Source                   string  `json:"source"`
	Destination              string  `json:"destination"`
	Market                   string  `json:"market"`
	WeightKg                 float64 `json:"weight_kg"`
	PlannedDistanceKm        float64 `json:"planned_distance_km"`
	FirstObservedRemainingKm float64 `json:"first_observed_remaining_km"`
	StartedObservedAt        string  `json:"started_observed_at"`
	CompletedAt              string  `json:"completed_at"`
	SpeedFines               int     `json:"speed_fines"`
	CargoDamagePct           float64 `json:"cargo_damage_pct"`
	TruckDamageStartPct      float64 `json:"truck_damage_start_pct"`
	TruckDamageMaxPct        float64 `json:"truck_damage_max_pct"`
}

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

func gatTripRuleResponse(d *gatDelivery) map[string]any {
	if d==nil { return map[string]any{} }
	return map[string]any{
		"base_xp":d.BaseXP,"penalty_xp":d.PenaltyXP,"xp_awarded":d.XPAwarded,
		"speed_fines":d.SpeedFines,"speed_penalty_xp":d.SpeedPenaltyXP,
		"cargo_damage_pct":d.CargoDamagePct,"cargo_penalty_xp":d.CargoPenaltyXP,
		"truck_damage_delta_pct":d.TruckDamagePct,"truck_penalty_xp":d.TruckPenaltyXP,
	}
}

func (a *agent) accountTripComplete(w http.ResponseWriter, r *http.Request) {
	if gatAccountCors(w,r) { return }
	if r.Method != http.MethodPost { jsonOut(w,405,map[string]any{"ok":false,"error":"method_not_allowed"}); return }
	user,ok:=gatAuthUser(w,r); if !ok { return }
	var q gatTripCompleteRequest
	if decode(r,&q)!=nil { jsonOut(w,400,map[string]any{"ok":false,"error":"bad_request"}); return }
	q.TripID=strings.TrimSpace(q.TripID); q.Cargo=strings.TrimSpace(q.Cargo); q.Source=strings.TrimSpace(q.Source); q.Destination=strings.TrimSpace(q.Destination)
	if q.TripID=="" || q.Cargo=="" { jsonOut(w,400,map[string]any{"ok":false,"error":"trip_required"}); return }
	if strings.TrimSpace(q.CompletedAt)=="" { q.CompletedAt=time.Now().UTC().Format(time.RFC3339) }

	gatProgressMu.Lock(); defer gatProgressMu.Unlock()
	gatLearnCargoName(q.Cargo)
	all:=loadGatProgress(); p:=ensureGatProgress(all,user)

	// Idempotencia forte: reenvio do mesmo recibo nunca duplica a entrega.
	for i:=range p.Deliveries {
		d:=&p.Deliveries[i]
		if strings.EqualFold(strings.TrimSpace(d.ReceiptID),q.TripID) {
			resp:=map[string]any{"ok":true,"duplicate":true,"completed_now":false,"receipt_id":q.TripID,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}
			for k,v:=range gatTripRuleResponse(d) { resp[k]=v }
			jsonOut(w,200,resp); return
		}
	}

	// Se a telemetria ao vivo contou primeiro, o recibo posterior anexa o ID e aplica as penalidades.
	if p.CurrentMission==nil && len(p.Deliveries)>0 {
		qt,qerr:=time.Parse(time.RFC3339,q.CompletedAt)
		for i:=len(p.Deliveries)-1; i>=0 && i>=len(p.Deliveries)-5; i-- {
			d:=&p.Deliveries[i]
			if !strings.EqualFold(strings.TrimSpace(d.Cargo),q.Cargo) || !strings.EqualFold(strings.TrimSpace(d.Source),q.Source) || !strings.EqualFold(strings.TrimSpace(d.Destination),q.Destination) { continue }
			dt,derr:=time.Parse(time.RFC3339,d.CompletedAt)
			if qerr==nil && derr==nil { diff:=qt.Sub(dt); if diff<0 { diff=-diff }; if diff>15*time.Minute { continue } }
			d.ReceiptID=q.TripID
			distance:=d.DistanceKm; if distance<=0 { distance=q.PlannedDistanceKm; if q.FirstObservedRemainingKm>distance { distance=q.FirstObservedRemainingKm } }
			gatApplyRulesToDelivery(d,distance,q); p.XP=gatTotalXPFromHistory(p); _=saveGatProgress(all)
			resp:=map[string]any{"ok":true,"duplicate":true,"already_counted":true,"completed_now":false,"receipt_id":q.TripID,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}
			for k,v:=range gatTripRuleResponse(d) { resp[k]=v }
			jsonOut(w,200,resp); return
		}
	}

	m:=p.CurrentMission
	if m==nil { jsonOut(w,409,map[string]any{"ok":false,"error":"mission_not_found","receipt_id":q.TripID}); return }
	if m.CatalogID!="" { m.MinKm=250 }

	if qt,err:=time.Parse(time.RFC3339,q.CompletedAt); err==nil {
		if at,err2:=time.Parse(time.RFC3339,m.AssignedAt); err2==nil && qt.Before(at.Add(-5*time.Second)) {
			jsonOut(w,409,map[string]any{"ok":false,"error":"trip_before_mission","receipt_id":q.TripID}); return
		}
	}
	if !gatCargoMatch(m,q.Cargo) {
		jsonOut(w,409,map[string]any{"ok":false,"error":"cargo_mismatch","receipt_id":q.TripID,"cargo":q.Cargo,"mission":m}); return
	}

	distance:=q.PlannedDistanceKm
	if q.FirstObservedRemainingKm>distance { distance=q.FirstObservedRemainingKm }
	required:=m.MinKm; if m.CatalogID!="" || required<=0 { required=250 }
	if distance<required {
		jsonOut(w,409,map[string]any{"ok":false,"error":"distance_below_minimum","required_km":required,"distance_km":distance,"receipt_id":q.TripID}); return
	}

	now:=time.Now().UTC(); completed:=q.CompletedAt
	if _,err:=time.Parse(time.RFC3339,completed); err!=nil { completed=now.Format(time.RFC3339) }
	m.State="completed"; m.CompletedAt=completed; m.Cargo=q.Cargo; m.Source=q.Source; m.Destination=q.Destination; m.WeightKg=q.WeightKg; m.StartKm=distance; m.LastKm=0
	if strings.TrimSpace(m.StartedAt)=="" { m.StartedAt=strings.TrimSpace(q.StartedObservedAt); if m.StartedAt=="" { m.StartedAt=m.AssignedAt } }
	delivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance}
	gatApplyRulesToDelivery(&delivery,distance,q)
	p.Deliveries=append(p.Deliveries,delivery); if len(p.Deliveries)>250 { p.Deliveries=p.Deliveries[len(p.Deliveries)-250:] }
	p.TotalDeliveries++; p.TotalKm+=distance; p.MonthlyKm+=distance; p.MonthlyCompleted++; p.XP=gatTotalXPFromHistory(p); p.CurrentMission=nil; p.LastOnJob=false
	if err:=saveGatProgress(all); err!=nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
	resp:=map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP}
	for k,v:=range gatTripRuleResponse(&delivery) { resp[k]=v }
	jsonOut(w,200,resp)
}

'''

s=s[:req_start]+block+s[telemetry_start:]

checks=['InternalVersion = "1.0.32"','func gatTripRuleBreakdown(','SpeedPenaltyXP','CargoPenaltyXP','TruckPenaltyXP','gatApplyRulesToDelivery(&delivery,distance,q)','already_counted','gatLearnCargoName(q.Cargo)']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.32 aplicado: regras de dano e excesso de velocidade')
