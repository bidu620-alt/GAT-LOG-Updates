from pathlib import Path

agent = Path('/tmp/gat-src/cmd/agent/main.go')
core = Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.12"' in c:
    c = c.replace('InternalVersion = "1.0.12"', 'InternalVersion = "1.0.13"', 1)
elif 'InternalVersion = "1.0.13"' not in c:
    raise SystemExit('versao 1.0.12 do agente nao encontrada')
core.write_text(c, encoding='utf-8')

s = agent.read_text(encoding='utf-8')
for imp in ['"sort"', '"strconv"']:
    if imp not in s:
        s = s.replace('import (\n', 'import (\n\t' + imp + '\n', 1)

routes = (
    '\tm.HandleFunc("/api/public/ranking", a.publicRanking)\n'
    '\tm.HandleFunc("/api/public/driver", a.publicDriver)\n'
    '\tm.HandleFunc("/api/account/profile", a.accountProfile)\n'
    '\tm.HandleFunc("/api/account/work/current", a.accountWorkCurrent)\n'
    '\tm.HandleFunc("/api/account/work/take", a.accountWorkTake)\n'
    '\tm.HandleFunc("/api/account/telemetry", a.accountTelemetry)\n'
)
if '/api/account/work/take' not in s:
    needle = '\tm.HandleFunc("/api/account/session", a.accountSession)\n'
    if needle not in s:
        raise SystemExit('rota account/session nao encontrada')
    s = s.replace(needle, needle + routes, 1)

support = r'''
var gatProgressMu sync.Mutex

type gatMission struct {
	ID          string  `json:"id"`
	Month       string  `json:"month"`
	Sequence    int     `json:"sequence"`
	Market      string  `json:"market"`
	MinKm       float64 `json:"min_km"`
	MinWeightKg float64 `json:"min_weight_kg"`
	MaxWeightKg float64 `json:"max_weight_kg"`
	State       string  `json:"state"`
	AssignedAt  string  `json:"assigned_at"`
	StartedAt   string  `json:"started_at,omitempty"`
	CompletedAt string  `json:"completed_at,omitempty"`
	Cargo       string  `json:"cargo,omitempty"`
	Source      string  `json:"source,omitempty"`
	Destination string  `json:"destination,omitempty"`
	WeightKg    float64 `json:"weight_kg,omitempty"`
	StartKm     float64 `json:"start_km,omitempty"`
	LastKm      float64 `json:"last_km,omitempty"`
}

type gatDelivery struct {
	ID          string  `json:"id"`
	MissionID   string  `json:"mission_id"`
	Sequence    int     `json:"sequence"`
	CompletedAt string  `json:"completed_at"`
	Cargo       string  `json:"cargo"`
	Source      string  `json:"source"`
	Destination string  `json:"destination"`
	WeightKg    float64 `json:"weight_kg"`
	DistanceKm  float64 `json:"distance_km"`
}

type gatDriverProgress struct {
	User             string        `json:"user"`
	Month            string        `json:"month"`
	MonthlyCompleted int           `json:"monthly_completed"`
	TotalDeliveries  int           `json:"total_deliveries"`
	TotalKm          float64       `json:"total_km"`
	XP               int           `json:"xp"`
	Points           int           `json:"points"`
	CurrentMission   *gatMission   `json:"current_mission,omitempty"`
	Deliveries       []gatDelivery `json:"deliveries"`
	LastOnJob        bool          `json:"last_on_job"`
	LastTelemetryAt  string        `json:"last_telemetry_at,omitempty"`
}

type gatAccountTelemetryRequest struct {
	Driver    string         `json:"driver"`
	Telemetry map[string]any `json:"telemetry"`
}

func gatProgressPath() string { return filepath.Join(core.DataDir(), "driver_progress.json") }
func gatMonth() string { return time.Now().UTC().Format("2006-01") }

func loadGatProgress() map[string]*gatDriverProgress {
	out := map[string]*gatDriverProgress{}
	_ = core.LoadJSON(gatProgressPath(), &out)
	if out == nil { out = map[string]*gatDriverProgress{} }
	return out
}

func saveGatProgress(v map[string]*gatDriverProgress) error { return core.SaveJSON(gatProgressPath(), v) }

func ensureGatProgress(all map[string]*gatDriverProgress, user string) *gatDriverProgress {
	key := accountKey(user)
	p := all[key]
	if p == nil {
		p = &gatDriverProgress{User: strings.TrimSpace(user), Month: gatMonth(), Deliveries: []gatDelivery{}}
		all[key] = p
	}
	if strings.TrimSpace(p.User) == "" { p.User = strings.TrimSpace(user) }
	if p.Deliveries == nil { p.Deliveries = []gatDelivery{} }
	nowMonth := gatMonth()
	if p.Month != nowMonth {
		p.Month = nowMonth
		p.MonthlyCompleted = 0
		p.CurrentMission = nil
		p.LastOnJob = false
	}
	return p
}

func gatLevel(xp int) int { if xp < 0 { xp = 0 }; return 1 + xp/1000 }

func publicGatProfile(p *gatDriverProgress) map[string]any {
	var mission any = nil
	if p.CurrentMission != nil { mission = p.CurrentMission }
	history := p.Deliveries
	if len(history) > 50 { history = history[len(history)-50:] }
	return map[string]any{
		"user": p.User, "month": p.Month, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40,
		"total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP,
		"level": gatLevel(p.XP), "points": p.Points, "xp_rule_pending": true,
		"points_rule_pending": true, "current_mission": mission, "deliveries": history,
	}
}

func gatAuthUser(w http.ResponseWriter, r *http.Request) (string, bool) {
	user, ok := verifyLocalDriverAccountToken(accountBearer(r))
	if !ok { jsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "unauthorized"}); return "", false }
	return user, true
}

func gatTelemetryValue(m map[string]any, path string) any {
	var cur any = m
	for _, part := range strings.Split(path, ".") {
		obj, ok := cur.(map[string]any); if !ok { return nil }
		next, ok := obj[part]; if !ok { return nil }; cur = next
	}
	return cur
}

func gatTelemetryString(m map[string]any, paths ...string) string {
	for _, path := range paths {
		v := gatTelemetryValue(m, path); if v == nil { continue }
		x := strings.TrimSpace(fmt.Sprint(v)); if x != "" && x != "<nil>" { return x }
	}
	return ""
}

func gatTelemetryFloat(m map[string]any, paths ...string) float64 {
	for _, path := range paths {
		v := gatTelemetryValue(m, path)
		switch x := v.(type) {
		case float64: return x
		case float32: return float64(x)
		case int: return float64(x)
		case int64: return float64(x)
		case json.Number: if n, err := x.Float64(); err == nil { return n }
		case string: if n, err := strconv.ParseFloat(strings.TrimSpace(x), 64); err == nil { return n }
		}
	}
	return 0
}

func gatTelemetryBool(m map[string]any, paths ...string) bool {
	for _, path := range paths {
		v := gatTelemetryValue(m, path)
		switch x := v.(type) {
		case bool: return x
		case string: b, err := strconv.ParseBool(strings.TrimSpace(x)); if err == nil { return b }
		case float64: return x != 0
		}
	}
	return false
}

func gatMissionBand() (float64, float64) {
	bands := [][2]float64{{0,8000},{8000,16000},{16000,24000},{24000,32000},{32000,40000},{40000,48000},{48000,56000}}
	var b [1]byte; idx := 0
	if _, err := rand.Read(b[:]); err == nil { idx = int(b[0]) % len(bands) }
	return bands[idx][0], bands[idx][1]
}

func gatIsWorldOfTrucks(market string) bool {
	x := strings.ToLower(strings.TrimSpace(market))
	return strings.Contains(x, "external") || strings.Contains(x, "world") || strings.Contains(x, "wot")
}

func gatClearMissionTrip(m *gatMission) {
	m.State = "assigned"; m.StartedAt = ""; m.Cargo = ""; m.Source = ""; m.Destination = ""
	m.WeightKg = 0; m.StartKm = 0; m.LastKm = 0
}

func gatSameTrip(m *gatMission, cargo, source, destination string) bool {
	if m == nil { return false }
	if m.Cargo != "" && cargo != "" && !strings.EqualFold(m.Cargo, cargo) { return false }
	if m.Source != "" && source != "" && !strings.EqualFold(m.Source, source) { return false }
	if m.Destination != "" && destination != "" && !strings.EqualFold(m.Destination, destination) { return false }
	return true
}

func (a *agent) publicRanking(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock()
	all := loadGatProgress(); list := make([]*gatDriverProgress, 0, len(all)); changed := false
	for _, p := range all { before := p.Month; ensureGatProgress(all, p.User); if p.Month != before { changed = true }; list = append(list, p) }
	if changed { _ = saveGatProgress(all) }
	sort.Slice(list, func(i, j int) bool {
		if list[i].MonthlyCompleted != list[j].MonthlyCompleted { return list[i].MonthlyCompleted > list[j].MonthlyCompleted }
		if list[i].TotalKm != list[j].TotalKm { return list[i].TotalKm > list[j].TotalKm }
		return strings.ToLower(list[i].User) < strings.ToLower(list[j].User)
	})
	out := make([]map[string]any, 0, len(list))
	for _, p := range list { out = append(out, map[string]any{"user": p.User, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "total_deliveries": p.TotalDeliveries, "total_km": p.TotalKm, "xp": p.XP, "level": gatLevel(p.XP), "points": p.Points}) }
	jsonOut(w, 200, map[string]any{"ok": true, "month": gatMonth(), "ranking": out})
}

func (a *agent) publicDriver(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user := strings.TrimSpace(r.URL.Query().Get("user")); if user == "" { jsonOut(w, 400, map[string]any{"ok": false, "error": "user_required"}); return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := all[accountKey(user)]
	if p == nil { jsonOut(w, 404, map[string]any{"ok": false, "error": "driver_not_found"}); return }
	ensureGatProgress(all, p.User); _ = saveGatProgress(all)
	jsonOut(w, 200, map[string]any{"ok": true, "profile": publicGatProfile(p)})
}

func (a *agent) accountProfile(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user, ok := gatAuthUser(w, r); if !ok { return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user); _ = saveGatProgress(all)
	jsonOut(w, 200, map[string]any{"ok": true, "profile": publicGatProfile(p)})
}

func (a *agent) accountWorkCurrent(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user, ok := gatAuthUser(w, r); if !ok { return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user); _ = saveGatProgress(all)
	jsonOut(w, 200, map[string]any{"ok": true, "month": p.Month, "completed": p.MonthlyCompleted, "goal": 40, "finished_month": p.MonthlyCompleted >= 40, "mission": p.CurrentMission})
}

func (a *agent) accountWorkTake(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user, ok := gatAuthUser(w, r); if !ok { return }
	gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user)
	if p.MonthlyCompleted >= 40 { _ = saveGatProgress(all); jsonOut(w, 200, map[string]any{"ok": true, "finished_month": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": nil}); return }
	if p.CurrentMission == nil {
		minW, maxW := gatMissionBand(); seq := p.MonthlyCompleted + 1; now := time.Now().UTC()
		p.CurrentMission = &gatMission{ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq), Month: p.Month, Sequence: seq, Market: "world_of_trucks", MinKm: 800, MinWeightKg: minW, MaxWeightKg: maxW, State: "assigned", AssignedAt: now.Format(time.RFC3339)}
	}
	if err := saveGatProgress(all); err != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "save_error"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": p.CurrentMission})
}

func (a *agent) accountTelemetry(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user, ok := gatAuthUser(w, r); if !ok { return }
	var q gatAccountTelemetryRequest
	if decode(r, &q) != nil || q.Telemetry == nil { jsonOut(w, 400, map[string]any{"ok": false, "error": "bad_request"}); return }
	tel := q.Telemetry
	onJob := gatTelemetryBool(tel, "on_job", "gameplay.onJob")
	mass := gatTelemetryFloat(tel, "mass_kg", "cargo_mass", "cargoMass", "job.cargoMass")
	km := gatTelemetryFloat(tel, "remaining_km")
	if km <= 0 { distM := gatTelemetryFloat(tel, "distance_m", "navigation.estimatedDistance"); if distM > 0 { km = distM / 1000.0 } }
	cargo := gatTelemetryString(tel, "cargo_name", "job.cargoName", "job.cargo")
	source := gatTelemetryString(tel, "source_city", "job.sourceCity")
	destination := gatTelemetryString(tel, "destination_city", "job.destinationCity")
	market := gatTelemetryString(tel, "job_market", "market", "job.market")
	gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user)
	p.LastTelemetryAt = time.Now().UTC().Format(time.RFC3339); m := p.CurrentMission; started := false; completedNow := false
	validation := map[string]any{"on_job": onJob, "world_of_trucks": gatIsWorldOfTrucks(market), "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}
	if m != nil {
		validation["distance_ok"] = km >= m.MinKm; validation["weight_ok"] = mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0
		if m.State == "active" && onJob && !gatSameTrip(m, cargo, source, destination) { gatClearMissionTrip(m) }
		if m.State == "assigned" && onJob && gatIsWorldOfTrucks(market) && km >= m.MinKm && mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0 {
			m.State = "active"; m.StartedAt = time.Now().UTC().Format(time.RFC3339); m.Cargo = cargo; m.Source = source; m.Destination = destination; m.WeightKg = mass; m.StartKm = km; m.LastKm = km; started = true
		} else if m.State == "active" && onJob {
			if km >= 0 { m.LastKm = km }
		} else if m.State == "active" && !onJob && p.LastOnJob {
			if m.LastKm <= 15 && m.StartKm >= m.MinKm {
				now := time.Now().UTC(); m.State = "completed"; m.CompletedAt = now.Format(time.RFC3339)
				delivery := gatDelivery{ID: m.ID, MissionID: m.ID, Sequence: m.Sequence, CompletedAt: m.CompletedAt, Cargo: m.Cargo, Source: m.Source, Destination: m.Destination, WeightKg: m.WeightKg, DistanceKm: m.StartKm}
				p.Deliveries = append(p.Deliveries, delivery); if len(p.Deliveries) > 250 { p.Deliveries = p.Deliveries[len(p.Deliveries)-250:] }
				p.TotalDeliveries++; p.TotalKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true
			} else { gatClearMissionTrip(m) }
		}
	}
	p.LastOnJob = onJob
	if err := saveGatProgress(all); err != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "save_error"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "user": p.User, "driver": strings.TrimSpace(q.Driver), "started": started, "completed_now": completedNow, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "mission": p.CurrentMission, "validation": validation, "xp_awarded": 0, "xp_rule_pending": true})
}

'''
if 'type gatDriverProgress struct {' not in s:
    marker = 'func (a *agent) publicLive('
    pos = s.find(marker)
    if pos < 0:
        raise SystemExit('publicLive nao encontrado')
    s = s[:pos] + support + s[pos:]

agent.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.13 history and missions applied')
