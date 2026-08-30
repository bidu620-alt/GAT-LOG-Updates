from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.35"' in c:
    c=c.replace('InternalVersion = "1.0.35"','InternalVersion = "1.0.36"',1)
elif 'InternalVersion = "1.0.36"' not in c:
    raise SystemExit('versao 1.0.35 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Auditoria persistente da distancia realmente dirigida e do veiculo usado.
if 'PlannedDistanceKm float64 `json:"planned_distance_km,omitempty"`' not in s:
    needle='\tDistanceKm  float64 `json:"distance_km"`\n'
    extra='''\tPlannedDistanceKm float64 `json:"planned_distance_km,omitempty"`
\tStartOdometerKm float64 `json:"start_odometer_km,omitempty"`
\tEndOdometerKm float64 `json:"end_odometer_km,omitempty"`
\tTruckID string `json:"truck_id,omitempty"`
\tTruckMake string `json:"truck_make,omitempty"`
\tTruckModel string `json:"truck_model,omitempty"`
\tTruckPlate string `json:"truck_plate,omitempty"`
'''
    if needle not in s: raise SystemExit('DistanceKm de gatDelivery nao encontrado')
    s=s.replace(needle,needle+extra,1)

# O recibo novo do GAT Telemetria 1.0.17 traz odometro e identidade do caminhao.
if 'DrivenDistanceKm' not in s[s.find('type gatTripCompleteRequest struct'):s.find('func gatDamagePct')]:
    needle='\tTruckDamageMaxPct        float64 `json:"truck_damage_max_pct"`\n'
    extra='''\tTruckID                  string  `json:"truck_id"`
\tTruckMake                string  `json:"truck_make"`
\tTruckModel               string  `json:"truck_model"`
\tTruckPlate               string  `json:"truck_plate"`
\tTruckIdentity            string  `json:"truck_identity"`
\tStartOdometerKm          float64 `json:"start_odometer_km"`
\tEndOdometerKm            float64 `json:"end_odometer_km"`
\tDrivenDistanceKm         float64 `json:"driven_distance_km"`
\tOdometerVerified         bool    `json:"odometer_verified"`
\tVehicleChanged           bool    `json:"vehicle_changed"`
\tOdometerDiscontinuity    bool    `json:"odometer_discontinuity"`
'''
    if needle not in s: raise SystemExit('fim de gatTripCompleteRequest nao encontrado')
    s=s.replace(needle,needle+extra,1)

needle='''\tq.FirstObservedRemainingKm = gatClamp(q.FirstObservedRemainingKm, 0, 15000)
\tq.CargoDamagePct = gatClamp(gatDamagePct(q.CargoDamagePct), 0, 100)
'''
repl='''\tq.FirstObservedRemainingKm = gatClamp(q.FirstObservedRemainingKm, 0, 15000)
\tq.DrivenDistanceKm = gatClamp(q.DrivenDistanceKm, 0, 15000)
\tq.StartOdometerKm = gatClamp(q.StartOdometerKm, 0, 50000000)
\tq.EndOdometerKm = gatClamp(q.EndOdometerKm, 0, 50000000)
\tq.CargoDamagePct = gatClamp(gatDamagePct(q.CargoDamagePct), 0, 100)
'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'q.DrivenDistanceKm = gatClamp' not in s:
    raise SystemExit('sanitizacao do recibo nao encontrada')

# A telemetria ao vivo nao conclui mais a missao so porque o ETS2 informou JobDelivered.
# Ela mantem o Trabalho GAT e espera o recibo local com o odometro real.
old='''\t\tif m.State == "active" && deliveredEvent {
\t\t\tm.LastKm = 0
\t\t\tonJob = false
\t\t\tp.LastOnJob = true
\t\t} else if m.State == "active" && cancelledEvent {
\t\t\tm.LastKm = 999999
\t\t\tonJob = false
\t\t\tp.LastOnJob = true
\t\t}
'''
new='''\t\tif m.State == "active" && deliveredEvent {
\t\t\tonJob = false
\t\t\tvalidation["receipt_pending"] = true
\t\t} else if m.State == "active" && cancelledEvent {
\t\t\tgatClearMissionTrip(m)
\t\t\tonJob = false
\t\t\tvalidation["trip_cancelled"] = true
\t\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'validation["receipt_pending"] = true' not in s:
    raise SystemExit('tratamento de entrega/cancelamento ao vivo nao encontrado')

old='''\t\t} else if m.State == "active" && !onJob && p.LastOnJob {
\t\t\tif m.LastKm <= 15 && m.StartKm >= m.MinKm && !gatRouteUsedThisMonth(p,m.Source,m.Destination) {
\t\t\t\tnow := time.Now().UTC(); m.State = "completed"; m.CompletedAt = now.Format(time.RFC3339)
\t\t\t\txpNow := gatXPForDistance(m.StartKm)
\t\t\t\tdelivery := gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,XPAwarded:xpNow,CompletedAt:m.CompletedAt,Cargo:m.Cargo,Source:m.Source,Destination:m.Destination,WeightKg:m.WeightKg,DistanceKm:m.StartKm}
\t\t\t\tp.Deliveries = append(p.Deliveries, delivery); if len(p.Deliveries) > 250 { p.Deliveries = p.Deliveries[len(p.Deliveries)-250:] }
\t\t\t\tp.TotalDeliveries++; p.TotalKm += m.StartKm; p.MonthlyKm += m.StartKm; p.MonthlyCompleted++; p.XP = gatTotalXPFromHistory(p); p.CurrentMission = nil; completedNow = true
\t\t\t} else { gatClearMissionTrip(m) }
\t\t}
'''
new='''\t\t} else if m.State == "active" && !onJob && p.LastOnJob {
\t\t\t// Nunca conclui somente pelo desaparecimento do job. O recibo autenticado decide a entrega.
\t\t\tvalidation["receipt_pending"] = deliveredEvent
\t\t}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'Nunca conclui somente pelo desaparecimento do job' not in s:
    raise SystemExit('conclusao antiga da telemetria ao vivo nao encontrada')

# Substitui a validacao e contabilizacao do recibo: planejado valida a oferta; odometro valida o que foi realmente rodado.
old='''\tdistance:=q.PlannedDistanceKm
\tif q.FirstObservedRemainingKm>distance { distance=q.FirstObservedRemainingKm }
\trequired:=m.MinKm; if m.CatalogID!="" || required<=0 { required=500 }
\tif distance<required {
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"distance_below_minimum","required_km":required,"distance_km":distance,"receipt_id":q.TripID}); return
\t}

\tnow:=time.Now().UTC(); completed:=q.CompletedAt
'''
new='''\tplanned:=q.PlannedDistanceKm
\tif q.FirstObservedRemainingKm>planned { planned=q.FirstObservedRemainingKm }
\trequired:=m.MinKm; if m.CatalogID!="" || required<=0 { required=500 }
\tif planned<required {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"distance_below_minimum","required_km":required,"planned_distance_km":planned,"receipt_id":q.TripID}); return
\t}
\tif q.VehicleChanged {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"vehicle_changed","receipt_id":q.TripID}); return
\t}
\tif q.OdometerDiscontinuity {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"odometer_discontinuity","receipt_id":q.TripID}); return
\t}
\tactual:=q.DrivenDistanceKm
\tif actual<=0 && q.OdometerVerified && q.EndOdometerKm>=q.StartOdometerKm { actual=q.EndOdometerKm-q.StartOdometerKm }
\tactual=gatClamp(actual,0,15000)
\tif !q.OdometerVerified || q.StartOdometerKm<=0 || q.EndOdometerKm<=0 {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"distance_not_verified","required_km":required,"actual_distance_km":actual,"receipt_id":q.TripID}); return
\t}
\tif actual<required {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"actual_distance_below_minimum","required_km":required,"actual_distance_km":actual,"planned_distance_km":planned,"receipt_id":q.TripID}); return
\t}
\tdistance:=actual

\tnow:=time.Now().UTC(); completed:=q.CompletedAt
'''
if old in s:
    s=s.replace(old,new,1)
elif 'actual_distance_below_minimum' not in s:
    raise SystemExit('validacao de distancia do recibo nao encontrada')

old='''\tm.State="completed"; m.CompletedAt=completed; m.Cargo=q.Cargo; m.Source=q.Source; m.Destination=q.Destination; m.WeightKg=q.WeightKg; m.StartKm=distance; m.LastKm=0
'''
new='''\tm.State="completed"; m.CompletedAt=completed; m.Cargo=q.Cargo; m.Source=q.Source; m.Destination=q.Destination; m.WeightKg=q.WeightKg; m.StartKm=planned; m.LastKm=0
'''
if old in s:
    s=s.replace(old,new,1)

old='''\tdelivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance}
'''
new='''\tdelivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance,PlannedDistanceKm:planned,StartOdometerKm:q.StartOdometerKm,EndOdometerKm:q.EndOdometerKm,TruckID:q.TruckID,TruckMake:q.TruckMake,TruckModel:q.TruckModel,TruckPlate:q.TruckPlate}
'''
if old in s:
    s=s.replace(old,new,1)
elif 'PlannedDistanceKm:planned' not in s:
    raise SystemExit('auditoria do delivery nao encontrada')

old='''\tresp:=map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP}
'''
new='''\tresp:=map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"actual_distance_km":distance,"planned_distance_km":planned,"odometer_verified":true,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP}
'''
if old in s:
    s=s.replace(old,new,1)

# Caso rarissimo de entrega ja contada por versao antiga: recibo novo nao pode reescrever KM sem validacao.
old='''\t\t\tdistance:=d.DistanceKm; if distance<=0 { distance=q.PlannedDistanceKm; if q.FirstObservedRemainingKm>distance { distance=q.FirstObservedRemainingKm } }
\t\t\tgatApplyRulesToDelivery(d,distance,q); p.XP=gatTotalXPFromHistory(p); _=saveGatProgress(all)
'''
new='''\t\t\tdistance:=d.DistanceKm
\t\t\tif q.OdometerVerified && !q.VehicleChanged && !q.OdometerDiscontinuity && q.DrivenDistanceKm>=500 { distance=q.DrivenDistanceKm }
\t\t\tgatApplyRulesToDelivery(d,distance,q); p.XP=gatTotalXPFromHistory(p); _=saveGatProgress(all)
'''
if old in s:
    s=s.replace(old,new,1)

checks=[
    'InternalVersion = "1.0.36"','actual_distance_below_minimum','distance_not_verified','odometer_discontinuity',
    'validation["receipt_pending"]','Nunca conclui somente pelo desaparecimento do job','PlannedDistanceKm:planned','q.DrivenDistanceKm'
]
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch 1.0.36 incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.36: distancia real por odometro, recibo obrigatorio e protecao contra teleporte/troca de caminhao')
