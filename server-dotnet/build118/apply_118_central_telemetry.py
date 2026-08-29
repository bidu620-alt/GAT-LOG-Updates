from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.17"' in c:
    c=c.replace('InternalVersion = "1.0.17"','InternalVersion = "1.0.18"',1)
elif 'InternalVersion = "1.0.18"' not in c:
    raise SystemExit('versao 1.0.17 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

route='\tm.HandleFunc("/api/public/account-live", a.publicAccountLive)\n'
if '/api/public/account-live' not in s:
    needle='\tm.HandleFunc("/api/public/driver", a.publicDriver)\n'
    if needle not in s: raise SystemExit('rota public/driver nao encontrada')
    s=s.replace(needle,needle+route,1)

old='\tLastOnJob        bool          `json:"last_on_job"`\n\tLastTelemetryAt  string        `json:"last_telemetry_at,omitempty"`\n'
new=('\tLastOnJob        bool           `json:"last_on_job"`\n'
     '\tLastTelemetryAt  string         `json:"last_telemetry_at,omitempty"`\n'
     '\tLiveDriver       string         `json:"live_driver,omitempty"`\n'
     '\tLiveTelemetry    map[string]any `json:"live_telemetry,omitempty"`\n')
if 'LiveTelemetry    map[string]any' not in s:
    if old not in s: raise SystemExit('gatDriverProgress live insertion point not found')
    s=s.replace(old,new,1)

old='\tp.LastTelemetryAt = time.Now().UTC().Format(time.RFC3339); m := p.CurrentMission; started := false; completedNow := false\n'
new=('\tp.LastTelemetryAt = time.Now().UTC().Format(time.RFC3339)\n'
     '\tp.LiveDriver = strings.TrimSpace(q.Driver)\n'
     '\tp.LiveTelemetry = tel\n'
     '\tm := p.CurrentMission; started := false; completedNow := false\n')
if 'p.LiveTelemetry = tel' not in s:
    if old not in s: raise SystemExit('accountTelemetry timestamp point not found')
    s=s.replace(old,new,1)

handler=r'''
func (a *agent) publicAccountLive(w http.ResponseWriter, r *http.Request) {
	if gatAccountCors(w, r) { return }
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock()
	all := loadGatProgress()
	out := make([]map[string]any, 0, len(all))
	for _, p := range all {
		if p == nil || p.LiveTelemetry == nil || strings.TrimSpace(p.LastTelemetryAt) == "" { continue }
		tel := p.LiveTelemetry
		driver := strings.TrimSpace(p.LiveDriver); if driver == "" { driver = p.User }
		mass := gatTelemetryFloat(tel, "mass_kg", "cargo_mass", "cargoMass", "job.cargoMass")
		km := gatTelemetryFloat(tel, "remaining_km")
		if km <= 0 { if dm := gatTelemetryFloat(tel, "distance_m", "navigation.estimatedDistance"); dm > 0 { km = dm / 1000.0 } }
		speed := gatTelemetryFloat(tel, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed")
		cargo := gatTelemetryString(tel, "cargo_name", "job.cargoName", "job.cargo")
		source := gatTelemetryString(tel, "source_city", "job.sourceCity")
		destination := gatTelemetryString(tel, "destination_city", "job.destinationCity")
		market := gatTelemetryString(tel, "job_market", "market", "job.market")
		rec := map[string]any{
			"driver": driver, "account_user": p.User, "updated_at": p.LastTelemetryAt,
			"on_job": gatTelemetryBool(tel, "on_job", "gameplay.onJob"),
			"speed_kmh": speed, "cargo_name": cargo, "mass_kg": mass, "remaining_km": km,
			"source_city": source, "destination_city": destination, "job_market": market,
			"map_x": gatTelemetryFloat(tel, "map_x", "truck.placement.x"),
			"map_z": gatTelemetryFloat(tel, "map_z", "truck.placement.z"),
			"map_heading": gatTelemetryFloat(tel, "map_heading", "truck.placement.heading"),
			"truck_make": gatTelemetryString(tel, "truck_make", "truck.make"),
			"truck_model": gatTelemetryString(tel, "truck_model", "truck.model"),
			"telemetry": tel,
		}
		out = append(out, rec)
	}
	jsonOut(w, 200, map[string]any{"ok": true, "source": "gat_central", "telemetry": out})
}

'''
if 'func (a *agent) publicAccountLive(' not in s:
    marker='func (a *agent) publicRanking('
    pos=s.find(marker)
    if pos<0: raise SystemExit('publicRanking nao encontrado')
    s=s[:pos]+handler+s[pos:]

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.18 central account telemetry applied')
