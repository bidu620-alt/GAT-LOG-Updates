from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.27"' in c:
    c=c.replace('InternalVersion = "1.0.27"','InternalVersion = "1.0.28"',1)
elif 'InternalVersion = "1.0.28"' not in c:
    raise SystemExit('versao 1.0.27 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

old='''\tif actorRole == "moderator" && action != "reset_mission" {
\t\tjsonOut(w, http.StatusForbidden, map[string]any{"ok": false, "error": "insufficient_role"}); return
\t}'''
new='''\tif actorRole == "moderator" && action != "reset_mission" && action != "confirm_mission" {
\t\tjsonOut(w, http.StatusForbidden, map[string]any{"ok": false, "error": "insufficient_role"}); return
\t}'''
if old in s:
    s=s.replace(old,new,1)
elif 'action != "confirm_mission"' not in s:
    raise SystemExit('regra de permissao do moderador nao encontrada')

confirm_case=r'''
	case "confirm_mission":
		gatProgressMu.Lock()
		all := loadGatProgress()
		p := ensureGatProgress(all, target)
		if p.CurrentMission == nil {
			gatProgressMu.Unlock()
			jsonOut(w,404,map[string]any{"ok":false,"error":"mission_not_found"}); return
		}
		if p.LiveTelemetry == nil {
			gatProgressMu.Unlock()
			jsonOut(w,400,map[string]any{"ok":false,"error":"telemetry_required"}); return
		}
		onJob := gatTelemetryBool(p.LiveTelemetry,"on_job","job.onJob","job.active")
		cargo := gatTelemetryString(p.LiveTelemetry,"cargo_name","job.cargoName","job.cargo")
		source := gatTelemetryString(p.LiveTelemetry,"source_city","job.sourceCity","job.source")
		destination := gatTelemetryString(p.LiveTelemetry,"destination_city","job.destinationCity","job.destination")
		km := gatTelemetryFloat(p.LiveTelemetry,"remaining_km","job.remainingKm","job.remaining_km","job.plannedDistanceKm","job.distanceKm")
		mass := gatTelemetryFloat(p.LiveTelemetry,"mass_kg","job.massKg","job.mass_kg","job.cargoMass")
		if !onJob || strings.TrimSpace(cargo) == "" {
			gatProgressMu.Unlock()
			jsonOut(w,400,map[string]any{"ok":false,"error":"active_job_required"}); return
		}
		if km < 250 {
			gatProgressMu.Unlock()
			jsonOut(w,400,map[string]any{"ok":false,"error":"distance_below_250","remaining_km":km}); return
		}
		m := p.CurrentMission
		m.State = "active"
		m.StartedAt = time.Now().UTC().Format(time.RFC3339)
		m.Cargo = cargo
		m.Source = source
		m.Destination = destination
		m.WeightKg = mass
		m.StartKm = km
		m.LastKm = km
		p.LastOnJob = true
		err := saveGatProgress(all)
		gatProgressMu.Unlock()
		if err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		appendGatAdminAudit(actor, action, target, "manual approval: "+cargo+" | "+fmt.Sprintf("%.0f km",km))
'''

if 'case "confirm_mission":' not in s:
    needle='\tcase "reset_mission":\n'
    pos=s.find(needle)
    if pos<0: raise SystemExit('case reset_mission nao encontrado')
    s=s[:pos]+confirm_case+s[pos:]

if 'case "confirm_mission":' not in s or 'distance_below_250' not in s:
    raise SystemExit('confirmacao manual nao aplicada')

agent.write_text(s,encoding='utf-8')
print('patch 1.0.28 aplicado: confirmacao manual de carga no Admin')
