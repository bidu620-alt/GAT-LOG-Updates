from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.36"' in c:
    c=c.replace('InternalVersion = "1.0.36"','InternalVersion = "1.0.37"',1)
elif 'InternalVersion = "1.0.37"' not in c:
    raise SystemExit('versao 1.0.36 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Guarda no historico que a entrega foi validada com a camada de integridade de mods.
if 'IntegrityStatus string `json:"integrity_status,omitempty"`' not in s[s.find('type gatDelivery struct'):s.find('type gatTripCompleteRequest struct')]:
    needle='\tTruckPlate string `json:"truck_plate,omitempty"`\n'
    extra='\tIntegrityStatus string `json:"integrity_status,omitempty"`\n\tIntegrityEvidenceHash string `json:"integrity_evidence_hash,omitempty"`\n'
    if needle not in s: raise SystemExit('TruckPlate de gatDelivery nao encontrado')
    s=s.replace(needle,needle+extra,1)

# Recibo do GAT Telemetria 1.0.18 inclui a verificacao do game.log da sessao atual.
req_start=s.find('type gatTripCompleteRequest struct')
req_end=s.find('func gatDamagePct',req_start)
req=s[req_start:req_end]
if 'IntegrityStatus' not in req:
    needle='\tOdometerDiscontinuity    bool    `json:"odometer_discontinuity"`\n'
    extra='''\tIntegrityStatus          string   `json:"integrity_status"`
\tIntegrityReason          string   `json:"integrity_reason"`
\tIntegrityMatches         []string `json:"integrity_matches"`
\tIntegrityEvidenceHash    string   `json:"integrity_evidence_hash"`
\tIntegrityCheckedAt       string   `json:"integrity_checked_at"`
'''
    if needle not in s: raise SystemExit('fim do gatTripCompleteRequest 1.0.36 nao encontrado')
    s=s.replace(needle,needle+extra,1)

# Entrega oficial so pode ser contabilizada quando a verificacao local foi executada e nenhum mod de dano foi detectado.
needle='\tplanned:=q.PlannedDistanceKm\n'
block='''\tintegrity:=strings.ToLower(strings.TrimSpace(q.IntegrityStatus))
\tif integrity=="blocked" {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"integrity_mod_blocked","integrity_reason":q.IntegrityReason,"integrity_matches":q.IntegrityMatches,"receipt_id":q.TripID}); return
\t}
\tif integrity!="ok" {
\t\tgatClearMissionTrip(m); _=saveGatProgress(all)
\t\tjsonOut(w,409,map[string]any{"ok":false,"error":"integrity_not_verified","integrity_reason":q.IntegrityReason,"receipt_id":q.TripID}); return
\t}

'''
if 'integrity_mod_blocked' not in s:
    if needle not in s: raise SystemExit('inicio da validacao por distancia nao encontrado')
    s=s.replace(needle,block+needle,1)

old='''delivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance,PlannedDistanceKm:planned,StartOdometerKm:q.StartOdometerKm,EndOdometerKm:q.EndOdometerKm,TruckID:q.TruckID,TruckMake:q.TruckMake,TruckModel:q.TruckModel,TruckPlate:q.TruckPlate}'''
new='''delivery:=gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,ReceiptID:q.TripID,CompletedAt:completed,Cargo:q.Cargo,Source:q.Source,Destination:q.Destination,WeightKg:q.WeightKg,DistanceKm:distance,PlannedDistanceKm:planned,StartOdometerKm:q.StartOdometerKm,EndOdometerKm:q.EndOdometerKm,TruckID:q.TruckID,TruckMake:q.TruckMake,TruckModel:q.TruckModel,TruckPlate:q.TruckPlate,IntegrityStatus:"ok",IntegrityEvidenceHash:q.IntegrityEvidenceHash}'''
if old in s:
    s=s.replace(old,new,1)
elif 'IntegrityEvidenceHash:q.IntegrityEvidenceHash' not in s:
    raise SystemExit('delivery 1.0.36 para integridade nao encontrado')

old='''"odometer_verified":true,"monthly_completed":p.MonthlyCompleted'''
new='''"odometer_verified":true,"integrity_status":"ok","monthly_completed":p.MonthlyCompleted'''
if old in s:
    s=s.replace(old,new,1)

checks=[
    'InternalVersion = "1.0.37"','integrity_mod_blocked','integrity_not_verified',
    'IntegrityEvidenceHash:q.IntegrityEvidenceHash','json:"integrity_status"'
]
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch 1.0.37 incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.37: integridade de mods obrigatoria para entregas oficiais')
