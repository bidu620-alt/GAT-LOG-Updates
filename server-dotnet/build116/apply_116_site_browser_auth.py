from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.15"' in c:
    c=c.replace('InternalVersion = "1.0.15"','InternalVersion = "1.0.16"',1)
elif 'InternalVersion = "1.0.16"' not in c:
    raise SystemExit('versao 1.0.15 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')
routes=(
    '\tm.HandleFunc("/api/site/session", a.siteSession)\n'
    '\tm.HandleFunc("/api/site/profile", a.siteProfile)\n'
    '\tm.HandleFunc("/api/site/work/take", a.siteWorkTake)\n'
)
if '/api/site/work/take' not in s:
    needle='\tm.HandleFunc("/api/public/version", a.publicVersion)\n'
    if needle not in s: raise SystemExit('rota public/version nao encontrada')
    s=s.replace(needle, needle+routes,1)

support=r'''
type gatSiteTokenRequest struct {
	Token string `json:"token"`
}

func gatSiteToken(w http.ResponseWriter, r *http.Request) (string, string, bool) {
	if gatAccountCors(w, r) { return "", "", false }
	if r.Method != http.MethodPost {
		jsonOut(w, http.StatusMethodNotAllowed, map[string]any{"ok": false, "error": "method_not_allowed"})
		return "", "", false
	}
	var q gatSiteTokenRequest
	if decode(r, &q) != nil || strings.TrimSpace(q.Token) == "" {
		jsonOut(w, http.StatusBadRequest, map[string]any{"ok": false, "error": "token_required"})
		return "", "", false
	}
	user, ok := verifyLocalDriverAccountToken(strings.TrimSpace(q.Token))
	if !ok {
		jsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "unauthorized"})
		return "", "", false
	}
	return strings.TrimSpace(q.Token), user, true
}

func (a *agent) siteSession(w http.ResponseWriter, r *http.Request) {
	_, user, ok := gatSiteToken(w, r)
	if !ok { return }
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "user": user, "agent_version": core.InternalVersion})
}

func (a *agent) siteProfile(w http.ResponseWriter, r *http.Request) {
	_, user, ok := gatSiteToken(w, r)
	if !ok { return }
	gatProgressMu.Lock()
	defer gatProgressMu.Unlock()
	all := loadGatProgress()
	p := ensureGatProgress(all, user)
	_ = saveGatProgress(all)
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "profile": publicGatProfile(p), "agent_version": core.InternalVersion})
}

func (a *agent) siteWorkTake(w http.ResponseWriter, r *http.Request) {
	_, user, ok := gatSiteToken(w, r)
	if !ok { return }
	gatProgressMu.Lock()
	defer gatProgressMu.Unlock()
	all := loadGatProgress()
	p := ensureGatProgress(all, user)
	if p.MonthlyCompleted >= 40 {
		_ = saveGatProgress(all)
		jsonOut(w, http.StatusOK, map[string]any{"ok": true, "finished_month": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": nil, "agent_version": core.InternalVersion})
		return
	}
	if p.CurrentMission == nil {
		minW, maxW := gatMissionBand()
		seq := p.MonthlyCompleted + 1
		now := time.Now().UTC()
		p.CurrentMission = &gatMission{
			ID: fmt.Sprintf("%s-%s-%02d", p.Month, accountKey(user), seq),
			Month: p.Month, Sequence: seq, Market: "world_of_trucks", MinKm: 800,
			MinWeightKg: minW, MaxWeightKg: maxW, State: "assigned", AssignedAt: now.Format(time.RFC3339),
		}
	}
	if err := saveGatProgress(all); err != nil {
		jsonOut(w, http.StatusInternalServerError, map[string]any{"ok": false, "error": "save_error"})
		return
	}
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "completed": p.MonthlyCompleted, "goal": 40, "mission": p.CurrentMission, "agent_version": core.InternalVersion})
}

'''
if 'func (a *agent) siteWorkTake(' not in s:
    marker='func (a *agent) publicVersion('
    pos=s.find(marker)
    if pos<0: raise SystemExit('publicVersion nao encontrado')
    s=s[:pos]+support+s[pos:]

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.16 browser auth routes applied')
