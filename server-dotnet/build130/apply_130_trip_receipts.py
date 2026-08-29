from pathlib import Path
import re

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.29"' in c:
    c=c.replace('InternalVersion = "1.0.29"','InternalVersion = "1.0.30"',1)
elif 'InternalVersion = "1.0.30"' not in c:
    raise SystemExit('versao 1.0.29 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# O catalogo oficial passa a gravar 250 km tambem dentro da missao, e nao apenas na validacao visual.
s=s.replace('Market:"any",MinKm:500,MinWeightKg:0,MaxWeightKg:0','Market:"any",MinKm:250,MinWeightKg:0,MaxWeightKg:0')

# Guarda o ID do recibo local para impedir contagem duplicada quando o cliente reenviar.
if 'ReceiptID' not in s:
    m=re.search(r'(\tXPAwarded\s+int\s+`json:"xp_awarded,omitempty"`\n)',s)
    if not m: raise SystemExit('campo XPAwarded de gatDelivery nao encontrado')
    s=s[:m.end()]+ '\tReceiptID   string  `json:"receipt_id,omitempty"`\n' + s[m.end():]

# Nova rota autenticada da Central GAT.
route='\tm.HandleFunc("/api/account/trip-complete", a.accountTripComplete)\n'
if '/api/account/trip-complete' not in s:
    needle='\tm.HandleFunc("/api/account/telemetry", a.accountTelemetry)\n'
    if needle not in s: raise SystemExit('rota /api/account/telemetry nao encontrada')
    s=s.replace(needle,needle+route,1)

handler=r'''
type gatTripCompleteRequest struct {
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
	all:=loadGatProgress(); p:=ensureGatProgress(all,user)

	// Idempotencia forte: o cliente pode ficar offline e reenviar o mesmo recibo quantas vezes for preciso.
	for i:=range p.Deliveries {
		if strings.EqualFold(strings.TrimSpace(p.Deliveries[i].ReceiptID),q.TripID) {
			jsonOut(w,200,map[string]any{"ok":true,"duplicate":true,"completed_now":false,"receipt_id":q.TripID,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}); return
		}
	}

	// Se a telemetria ao vivo venceu a corrida e ja contou a mesma entrega, anexa o receipt_id ao registro existente.
	if p.CurrentMission==nil && len(p.Deliveries)>0 {
		qt,qerr:=time.Parse(time.RFC3339,q.CompletedAt)
		for i:=len(p.Deliveries)-1; i>=0 && i>=len(p.Deliveries)-5; i-- {
			d:=&p.Deliveries[i]
			if !strings.EqualFold(strings.TrimSpace(d.Cargo),q.Cargo) || !strings.EqualFold(strings.TrimSpace(d.Source),q.Source) || !strings.EqualFold(strings.TrimSpace(d.Destination),q.Destination) { continue }
			dt,derr:=time.Parse(time.RFC3339,d.CompletedAt)
			if qerr==nil && derr==nil { diff:=qt.Sub(dt); if diff<0 { diff=-diff }; if diff>15*time.Minute { continue } }
			d.ReceiptID=q.TripID; _=saveGatProgress(all)
			jsonOut(w,200,map[string]any{"ok":true,"duplicate":true,"already_counted":true,"completed_now":false,"receipt_id":q.TripID,"monthly_completed":p.MonthlyCompleted,"xp":p.XP}); return
		}
	}

	m:=p.CurrentMission
	if m==nil { jsonOut(w,409,map[string]any{"ok":false,"error":"mission_not_found","receipt_id":q.TripID}); return }
	if m.CatalogID!="" { m.MinKm=250 }

	// Nao permite usar uma viagem concluida antes de o trabalho ter sido escolhido no site.
	if qt,err:=time.Parse(time.RFC3339,q.CompletedAt); err==nil {
		if at,err2:=time.Parse(time.RFC3339,m.AssignedAt); err2==nil && qt.Before(at.Add(-5*time.Second)) {
			jsonOut(w,409,map[string]any{"ok":false,"error":"trip_before_mission","receipt_id":q.TripID}); return
		}
	}
	if !gatCargoMatch(m,q.Cargo) {
		jsonOut(w,409,map[string]any{"ok":false,"error":"cargo_mismatch","receipt_id":q.TripID,"cargo":q.Cargo,"mission":m}); return
	}

	// planned_distance_km vem do contrato do ETS2 e continua sendo a distancia original mesmo se o GAT abriu no meio da viagem.
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
	xpNow:=gatXPForDistance(distance)
	delivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,XPAwarded:xpNow,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance}
	p.Deliveries=append(p.Deliveries,delivery); if len(p.Deliveries)>250 { p.Deliveries=p.Deliveries[len(p.Deliveries)-250:] }
	p.TotalDeliveries++; p.TotalKm+=distance; p.MonthlyKm+=distance; p.MonthlyCompleted++; p.XP=gatTotalXPFromHistory(p); p.CurrentMission=nil; p.LastOnJob=false
	if err:=saveGatProgress(all); err!=nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
	jsonOut(w,200,map[string]any{"ok":true,"completed_now":true,"receipt_id":q.TripID,"distance_km":distance,"xp_awarded":xpNow,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,"xp":p.XP})
}

'''
if 'func (a *agent) accountTripComplete(' not in s:
    marker='func (a *agent) accountTelemetry('
    pos=s.find(marker)
    if pos<0: raise SystemExit('accountTelemetry nao encontrado')
    s=s[:pos]+handler+s[pos:]

# Migra em memoria missao de catalogo ja escolhida na versao anterior para a regra de 250 km.
needle='\tp.LastTelemetryAt = time.Now().UTC().Format(time.RFC3339)\n'
if 'm.CatalogID != "" && m.MinKm != 250' not in s:
    pos=s.find(needle,s.find('func (a *agent) accountTelemetry('))
    if pos<0: raise SystemExit('timestamp da accountTelemetry nao encontrado')
    end=pos+len(needle)
    extra='\tif p.CurrentMission != nil && p.CurrentMission.CatalogID != "" && p.CurrentMission.MinKm != 250 { p.CurrentMission.MinKm = 250 }\n'
    s=s[:end]+extra+s[end:]

checks=['/api/account/trip-complete','func (a *agent) accountTripComplete(','ReceiptID','planned_distance_km','InternalVersion = "1.0.30"']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.30 aplicado: recibos persistentes de viagem')
