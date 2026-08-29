from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.20"' in c:
    c=c.replace('InternalVersion = "1.0.20"','InternalVersion = "1.0.21"',1)
elif 'InternalVersion = "1.0.21"' not in c:
    raise SystemExit('versao 1.0.20 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

route='\tm.HandleFunc("/api/site/admin/driver", a.siteAdminDriver)\n'
if '/api/site/admin/driver' not in s:
    needle='\tm.HandleFunc("/api/site/admin/drivers", a.siteAdminDrivers)\n'
    if needle not in s: raise SystemExit('rota admin/drivers nao encontrada')
    s=s.replace(needle,needle+route,1)

old='''type gatAdminRequest struct {
\tToken       string `json:"token"`
\tAction      string `json:"action,omitempty"`
\tTarget      string `json:"target,omitempty"`
\tPassword    string `json:"password,omitempty"`
\tRole        string `json:"role,omitempty"`
}'''
new='''type gatAdminRequest struct {
\tToken            string  `json:"token"`
\tAction           string  `json:"action,omitempty"`
\tTarget           string  `json:"target,omitempty"`
\tPassword         string  `json:"password,omitempty"`
\tRole             string  `json:"role,omitempty"`
\tMonthlyCompleted int     `json:"monthly_completed,omitempty"`
\tTotalDeliveries  int     `json:"total_deliveries,omitempty"`
\tTotalKm          float64 `json:"total_km,omitempty"`
\tDeliveryID       string  `json:"delivery_id,omitempty"`
}'''
if old in s:
    s=s.replace(old,new,1)
elif 'DeliveryID       string' not in s:
    raise SystemExit('gatAdminRequest patch point not found')

support=r'''
func gatAdminDriverLive(p *gatDriverProgress) map[string]any {
	out := map[string]any{"online": false, "updated_at": p.LastTelemetryAt, "driver": p.LiveDriver}
	if t, err := time.Parse(time.RFC3339, p.LastTelemetryAt); err == nil { out["online"] = time.Since(t) <= 25*time.Second }
	if p.LiveTelemetry == nil { return out }
	tel := p.LiveTelemetry
	mass := gatTelemetryFloat(tel, "mass_kg", "cargo_mass", "cargoMass", "job.cargoMass", "Job.CargoMass", "cargo_mass_kg", "job.mass_kg")
	km := gatTelemetryFloat(tel, "remaining_km")
	if km <= 0 { if dm := gatTelemetryFloat(tel, "distance_m", "navigation.estimatedDistance", "navigation.estimated_distance"); dm > 0 { km = dm / 1000.0 } }
	out["on_job"] = gatTelemetryBool(tel, "on_job", "gameplay.onJob")
	out["speed_kmh"] = gatTelemetryFloat(tel, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed", "Truck.Speed")
	out["cargo_name"] = gatTelemetryString(tel, "cargo_name", "cargo", "job.cargoName", "Job.CargoName", "job.cargo", "job.name")
	out["mass_kg"] = mass
	out["remaining_km"] = km
	out["source_city"] = gatTelemetryString(tel, "source_city", "source", "job.sourceCity", "Job.SourceCity")
	out["destination_city"] = gatTelemetryString(tel, "destination_city", "destination", "job.destinationCity", "Job.DestinationCity")
	out["job_market"] = gatTelemetryString(tel, "job_market", "market", "job.market")
	out["truck_make"] = gatTelemetryString(tel, "truck_make", "truck.make", "Truck.Make")
	out["truck_model"] = gatTelemetryString(tel, "truck_model", "truck.model", "Truck.Model")
	out["map_x"] = gatTelemetryFloat(tel, "map_x", "truck.placement.x")
	out["map_z"] = gatTelemetryFloat(tel, "map_z", "truck.placement.z")
	out["map_heading"] = gatTelemetryFloat(tel, "map_heading", "truck.placement.heading")
	out["telemetry"] = tel
	return out
}

func (a *agent) siteAdminDriver(w http.ResponseWriter, r *http.Request) {
	var q gatAdminRequest
	viewer, viewerRole, ok := gatSiteAdminAuth(w, r, &q); if !ok { return }
	target := strings.TrimSpace(q.Target)
	if target == "" { jsonOut(w, 400, map[string]any{"ok": false, "error": "target_required"}); return }
	gatAdminMu.Lock(); defer gatAdminMu.Unlock()
	accounts := ensurePrimaryAdmin()
	var acc *driverAccount
	for i := range accounts {
		if accounts[i].Key == accountKey(target) { acc = &accounts[i]; target = accounts[i].User; break }
	}
	if acc == nil { jsonOut(w, 404, map[string]any{"ok": false, "error": "user_not_found"}); return }
	gatProgressMu.Lock()
	all := loadGatProgress(); p := ensureGatProgress(all, target); _ = saveGatProgress(all)
	profile := publicGatProfile(p)
	history := p.Deliveries
	if len(history) > 100 { history = history[len(history)-100:] }
	profile["deliveries"] = history
	profile["last_telemetry_at"] = p.LastTelemetryAt
	profile["last_on_job"] = p.LastOnJob
	live := gatAdminDriverLive(p)
	gatProgressMu.Unlock()

	activeSessions := 0
	now := time.Now().UTC()
	for _, item := range loadDriverAccountSessions() {
		if accountKey(item.User) != accountKey(target) { continue }
		if expires, err := time.Parse(time.RFC3339, item.ExpiresAt); err == nil && expires.After(now) { activeSessions++ }
	}
	jsonOut(w, http.StatusOK, map[string]any{
		"ok": true, "viewer": viewer, "viewer_role": viewerRole, "agent_version": core.InternalVersion,
		"account": map[string]any{"user": acc.User, "created_at": acc.CreatedAt, "role": normalizedAdminRole(acc.Role), "disabled": acc.Disabled, "active_sessions": activeSessions},
		"profile": profile, "live": live,
	})
}

'''
if 'func (a *agent) siteAdminDriver(' not in s:
    marker='func (a *agent) siteAdminAction('
    pos=s.find(marker)
    if pos<0: raise SystemExit('siteAdminAction nao encontrado')
    s=s[:pos]+support+s[pos:]

needle='''\tcase "reset_mission":
\t\tgatProgressMu.Lock(); all := loadGatProgress(); p := ensureGatProgress(all, target); p.CurrentMission = nil; p.LastOnJob = false; err := saveGatProgress(all); gatProgressMu.Unlock()
\t\tif err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
\t\tappendGatAdminAudit(actor, action, target, "current mission cleared")
'''
replacement=needle+r'''\tcase "set_progress":
\t\tif actorRole == "moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
\t\tif q.MonthlyCompleted < 0 || q.MonthlyCompleted > 40 || q.TotalDeliveries < 0 || q.TotalDeliveries < q.MonthlyCompleted || q.TotalKm < 0 {
\t\t\tjsonOut(w,400,map[string]any{"ok":false,"error":"invalid_progress"}); return
\t\t}
\t\tgatProgressMu.Lock()
\t\tall := loadGatProgress(); p := ensureGatProgress(all, target)
\t\tp.Month = gatMonth(); p.MonthlyCompleted = q.MonthlyCompleted; p.TotalDeliveries = q.TotalDeliveries; p.TotalKm = q.TotalKm; p.XP = p.TotalDeliveries * 100
\t\tif p.CurrentMission != nil && p.CurrentMission.Sequence <= p.MonthlyCompleted { p.CurrentMission = nil; p.LastOnJob = false }
\t\terr := saveGatProgress(all); gatProgressMu.Unlock()
\t\tif err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
\t\tappendGatAdminAudit(actor, action, target, fmt.Sprintf("monthly=%d total=%d km=%.1f xp=%d", q.MonthlyCompleted, q.TotalDeliveries, q.TotalKm, q.TotalDeliveries*100))
\tcase "delete_delivery":
\t\tif actorRole == "moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
\t\tid := strings.TrimSpace(q.DeliveryID); if id == "" { jsonOut(w,400,map[string]any{"ok":false,"error":"delivery_id_required"}); return }
\t\tgatProgressMu.Lock()
\t\tall := loadGatProgress(); p := ensureGatProgress(all, target); found := -1; var removed gatDelivery
\t\tfor i := range p.Deliveries { if p.Deliveries[i].ID == id { found = i; removed = p.Deliveries[i]; break } }
\t\tif found < 0 { gatProgressMu.Unlock(); jsonOut(w,404,map[string]any{"ok":false,"error":"delivery_not_found"}); return }
\t\tp.Deliveries = append(p.Deliveries[:found], p.Deliveries[found+1:]...)
\t\tif p.TotalDeliveries > 0 { p.TotalDeliveries-- }; p.XP = p.TotalDeliveries * 100
\t\tif removed.DistanceKm > 0 { p.TotalKm -= removed.DistanceKm; if p.TotalKm < 0 { p.TotalKm = 0 } }
\t\tif strings.HasPrefix(removed.CompletedAt, gatMonth()) && p.MonthlyCompleted > 0 { p.MonthlyCompleted--; p.CurrentMission = nil; p.LastOnJob = false }
\t\terr := saveGatProgress(all); gatProgressMu.Unlock()
\t\tif err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
\t\tappendGatAdminAudit(actor, action, target, "delivery="+id+" cargo="+removed.Cargo)
'''
if 'case "set_progress":' not in s:
    if needle not in s: raise SystemExit('reset_mission switch point not found')
    s=s.replace(needle,replacement,1)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.21 Admin ficha detalhada e correcoes aplicado')
