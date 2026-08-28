from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'

if not core.exists() or not agent.exists():
    raise SystemExit('fontes do agente nao encontradas')

# Bump only the agent version; preserve all previous behavior.
s = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.8"' in s:
    s = s.replace('InternalVersion = "1.0.8"', 'InternalVersion = "1.0.10"', 1)
elif 'InternalVersion = "1.0.10"' not in s:
    raise SystemExit('versao esperada do agente nao encontrada')
core.write_text(s, encoding='utf-8')

s = agent.read_text(encoding='utf-8')

# Public, read-only endpoint used by GitHub Pages. The global wrapper already
# adds Access-Control-Allow-Origin: *. No admin secret is required here.
route = '\tm.HandleFunc("/api/public/live", a.publicLive)\n'
if route not in s:
    needle = '\tm.HandleFunc("/api/client/telemetry", a.clientTelemetry)\n'
    if needle not in s:
        raise SystemExit('rota client telemetry nao encontrada')
    s = s.replace(needle, needle + route, 1)

method = r'''func (a *agent) publicLive(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		jsonOut(w, http.StatusMethodNotAllowed, map[string]any{"ok": false, "error": "method_not_allowed"})
		return
	}

	a.mu.RLock()
	st := a.status
	players := append([]string(nil), st.Players...)
	tel := make([]core.TelemetryRecord, 0, len(a.telemetry))
	for _, v := range a.telemetry {
		tel = append(tel, v)
	}
	a.mu.RUnlock()

	sortTelemetry(tel)
	publicTelemetry := make([]map[string]any, 0, len(tel))
	for _, v := range tel {
		if strings.TrimSpace(v.Driver) == "" {
			continue
		}
		publicTelemetry = append(publicTelemetry, map[string]any{
			"driver":        v.Driver,
			"updated_at":    v.UpdatedAt,
			"status":        v.Status,
			"cargo":         v.Cargo,
			"cargo_mass_kg": v.CargoMassKg,
			"source":        v.Source,
			"destination":   v.Destination,
			"remaining_km":  v.RemainingKm,
			"speed_kmh":     v.SpeedKmh,
			"on_job":        v.OnJob,
		})
	}

	jsonOut(w, http.StatusOK, map[string]any{
		"ok":          true,
		"online":      st.ServerOnline,
		"server_name": st.ServerName,
		"session_id":  st.SessionID,
		"players":     players,
		"player_count": st.PlayerCount,
		"max_players": st.MaxPlayers,
		"telemetry":   publicTelemetry,
		"generated_at": time.Now().UTC().Format(time.RFC3339),
	})
}

'''

if 'func (a *agent) publicLive(' not in s:
    marker = 'func (a *agent) uiStatus('
    pos = s.find(marker)
    if pos < 0:
        raise SystemExit('uiStatus nao encontrado para inserir publicLive')
    s = s[:pos] + method + s[pos:]

agent.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.10 public live endpoint applied')
