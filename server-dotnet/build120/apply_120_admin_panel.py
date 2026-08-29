from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.19"' in c:
    c=c.replace('InternalVersion = "1.0.19"','InternalVersion = "1.0.20"',1)
elif 'InternalVersion = "1.0.20"' not in c:
    raise SystemExit('versao 1.0.19 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Extend driver accounts with role and disabled state while remaining backward compatible.
old='''type driverAccount struct {
\tUser      string `json:"user"`
\tKey       string `json:"key"`
\tSalt      string `json:"salt"`
\tHash      string `json:"hash"`
\tCreatedAt string `json:"created_at"`
}'''
new='''type driverAccount struct {
\tUser      string `json:"user"`
\tKey       string `json:"key"`
\tSalt      string `json:"salt"`
\tHash      string `json:"hash"`
\tCreatedAt string `json:"created_at"`
\tRole      string `json:"role,omitempty"`
\tDisabled  bool   `json:"disabled,omitempty"`
}'''
if old in s:
    s=s.replace(old,new,1)
elif 'Disabled  bool' not in s:
    raise SystemExit('driverAccount struct patch point not found')

# Block password login for disabled accounts.
old='''\tfor _, acc := range loadDriverAccounts() {
\t\tif acc.Key != key { continue }
\t\tgot := accountPasswordHash(password, acc.Salt)'''
new='''\tfor _, acc := range loadDriverAccounts() {
\t\tif acc.Key != key { continue }
\t\tif acc.Disabled { return driverAccount{}, false }
\t\tgot := accountPasswordHash(password, acc.Salt)'''
if old in s:
    s=s.replace(old,new,1)
elif 'if acc.Disabled { return driverAccount{}, false }' not in s:
    raise SystemExit('verifyDriverPassword patch point not found')

# Existing tokens are also invalid while an account is blocked.
old='''\t\texpires, err := time.Parse(time.RFC3339, item.ExpiresAt)
\t\tif err != nil || !expires.After(now) { return "", false }
\t\treturn item.User, true'''
new='''\t\texpires, err := time.Parse(time.RFC3339, item.ExpiresAt)
\t\tif err != nil || !expires.After(now) { return "", false }
\t\tfor _, acc := range loadDriverAccounts() {
\t\t\tif acc.Key == accountKey(item.User) {
\t\t\t\tif acc.Disabled { return "", false }
\t\t\t\treturn item.User, true
\t\t\t}
\t\t}
\t\treturn "", false'''
if old in s:
    s=s.replace(old,new,1)
elif 'if acc.Disabled { return "", false }' not in s:
    raise SystemExit('verifyLocalDriverAccountToken patch point not found')

routes=(
    '\tm.HandleFunc("/api/site/admin/session", a.siteAdminSession)\n'
    '\tm.HandleFunc("/api/site/admin/drivers", a.siteAdminDrivers)\n'
    '\tm.HandleFunc("/api/site/admin/action", a.siteAdminAction)\n'
    '\tm.HandleFunc("/api/site/admin/audit", a.siteAdminAudit)\n'
)
if '/api/site/admin/session' not in s:
    needle='\tm.HandleFunc("/api/site/work/take", a.siteWorkTake)\n'
    if needle not in s: raise SystemExit('site work route not found')
    s=s.replace(needle,needle+routes,1)

support=r'''
var gatAdminMu sync.Mutex

type gatAdminAuditEntry struct {
	At      string `json:"at"`
	Actor   string `json:"actor"`
	Action  string `json:"action"`
	Target  string `json:"target"`
	Details string `json:"details,omitempty"`
}

type gatAdminRequest struct {
	Token       string `json:"token"`
	Action      string `json:"action,omitempty"`
	Target      string `json:"target,omitempty"`
	Password    string `json:"password,omitempty"`
	Role        string `json:"role,omitempty"`
}

func gatAdminAuditPath() string { return filepath.Join(core.DataDir(), "admin_audit.json") }

func loadGatAdminAudit() []gatAdminAuditEntry {
	var list []gatAdminAuditEntry
	_ = core.LoadJSON(gatAdminAuditPath(), &list)
	if list == nil { list = []gatAdminAuditEntry{} }
	return list
}

func appendGatAdminAudit(actor, action, target, details string) {
	list := loadGatAdminAudit()
	list = append(list, gatAdminAuditEntry{At: time.Now().UTC().Format(time.RFC3339), Actor: actor, Action: action, Target: target, Details: details})
	if len(list) > 1000 { list = list[len(list)-1000:] }
	_ = core.SaveJSON(gatAdminAuditPath(), list)
}

func normalizedAdminRole(v string) string {
	switch strings.ToLower(strings.TrimSpace(v)) {
	case "owner": return "owner"
	case "admin": return "admin"
	case "moderator": return "moderator"
	default: return "driver"
	}
}

func ensurePrimaryAdmin() []driverAccount {
	list := loadDriverAccounts()
	for i := range list {
		if normalizedAdminRole(list[i].Role) == "owner" { return list }
	}
	if len(list) > 0 {
		list[0].Role = "owner"
		_ = saveDriverAccounts(list)
	}
	return list
}

func gatAdminRoleFor(user string) (string, bool) {
	list := ensurePrimaryAdmin()
	key := accountKey(user)
	for _, acc := range list {
		if acc.Key != key { continue }
		if acc.Disabled { return "driver", false }
		role := normalizedAdminRole(acc.Role)
		return role, role == "owner" || role == "admin" || role == "moderator"
	}
	return "driver", false
}

func revokeDriverSessions(user string) {
	key := accountKey(user)
	list := loadDriverAccountSessions()
	out := make([]driverAccountSession, 0, len(list))
	for _, item := range list {
		if accountKey(item.User) != key { out = append(out, item) }
	}
	_ = saveDriverAccountSessions(out)
}

func gatSiteAdminAuth(w http.ResponseWriter, r *http.Request, q *gatAdminRequest) (string, string, bool) {
	if gatAccountCors(w, r) { return "", "", false }
	if r.Method != http.MethodPost {
		jsonOut(w, http.StatusMethodNotAllowed, map[string]any{"ok": false, "error": "method_not_allowed"})
		return "", "", false
	}
	if decode(r, q) != nil || strings.TrimSpace(q.Token) == "" {
		jsonOut(w, http.StatusBadRequest, map[string]any{"ok": false, "error": "token_required"})
		return "", "", false
	}
	user, ok := verifyLocalDriverAccountToken(strings.TrimSpace(q.Token))
	if !ok {
		jsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "unauthorized"})
		return "", "", false
	}
	role, adminOK := gatAdminRoleFor(user)
	if !adminOK {
		jsonOut(w, http.StatusForbidden, map[string]any{"ok": false, "error": "admin_required"})
		return "", "", false
	}
	return user, role, true
}

func (a *agent) siteAdminSession(w http.ResponseWriter, r *http.Request) {
	var q gatAdminRequest
	user, role, ok := gatSiteAdminAuth(w, r, &q); if !ok { return }
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "user": user, "role": role, "agent_version": core.InternalVersion})
}

func (a *agent) siteAdminDrivers(w http.ResponseWriter, r *http.Request) {
	var q gatAdminRequest
	user, role, ok := gatSiteAdminAuth(w, r, &q); if !ok { return }
	_ = user
	gatAdminMu.Lock(); defer gatAdminMu.Unlock()
	accounts := ensurePrimaryAdmin()
	gatProgressMu.Lock()
	all := loadGatProgress()
	out := make([]map[string]any, 0, len(accounts))
	for _, acc := range accounts {
		p := ensureGatProgress(all, acc.User)
		online := false
		if t, err := time.Parse(time.RFC3339, p.LastTelemetryAt); err == nil { online = time.Since(t) <= 25*time.Second }
		truck := ""
		cargo := ""
		if p.LiveTelemetry != nil {
			truck = strings.TrimSpace(strings.Join([]string{gatTelemetryString(p.LiveTelemetry,"truck_make","truck.make"), gatTelemetryString(p.LiveTelemetry,"truck_model","truck.model")}, " "))
			cargo = gatTelemetryString(p.LiveTelemetry,"cargo_name","job.cargoName","job.cargo")
		}
		out = append(out, map[string]any{
			"user": acc.User, "created_at": acc.CreatedAt, "role": normalizedAdminRole(acc.Role), "disabled": acc.Disabled,
			"online": online, "last_telemetry_at": p.LastTelemetryAt, "truck": truck, "cargo": cargo,
			"monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "total_deliveries": p.TotalDeliveries,
			"total_km": p.TotalKm, "xp": p.XP, "level": gatLevel(p.XP), "points": p.Points,
			"current_mission": p.CurrentMission,
		})
	}
	_ = saveGatProgress(all)
	gatProgressMu.Unlock()
	sort.Slice(out, func(i,j int) bool { return strings.ToLower(fmt.Sprint(out[i]["user"])) < strings.ToLower(fmt.Sprint(out[j]["user"])) })
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "viewer_role": role, "drivers": out, "agent_version": core.InternalVersion})
}

func (a *agent) siteAdminAction(w http.ResponseWriter, r *http.Request) {
	var q gatAdminRequest
	actor, actorRole, ok := gatSiteAdminAuth(w, r, &q); if !ok { return }
	action := strings.ToLower(strings.TrimSpace(q.Action))
	target := strings.TrimSpace(q.Target)
	if action == "" || target == "" { jsonOut(w, 400, map[string]any{"ok": false, "error": "action_target_required"}); return }
	if actorRole == "moderator" && action != "reset_mission" {
		jsonOut(w, http.StatusForbidden, map[string]any{"ok": false, "error": "insufficient_role"}); return
	}
	gatAdminMu.Lock(); defer gatAdminMu.Unlock()
	accounts := ensurePrimaryAdmin()
	idx := -1
	for i := range accounts { if accounts[i].Key == accountKey(target) { idx = i; target = accounts[i].User; break } }
	if idx < 0 { jsonOut(w, 404, map[string]any{"ok": false, "error": "user_not_found"}); return }
	targetRole := normalizedAdminRole(accounts[idx].Role)
	if targetRole == "owner" && actorRole != "owner" { jsonOut(w, 403, map[string]any{"ok": false, "error": "owner_protected"}); return }
	if accountKey(target) == accountKey(actor) && (action == "block" || action == "delete") { jsonOut(w, 400, map[string]any{"ok": false, "error": "self_protected"}); return }

	switch action {
	case "block":
		accounts[idx].Disabled = true
		if saveDriverAccounts(accounts) != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		revokeDriverSessions(target)
		appendGatAdminAudit(actor, action, target, "account blocked")
	case "unblock":
		accounts[idx].Disabled = false
		if saveDriverAccounts(accounts) != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		appendGatAdminAudit(actor, action, target, "account unblocked")
	case "reset_password":
		if actorRole == "moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
		if len(q.Password) < 6 { jsonOut(w,400,map[string]any{"ok":false,"error":"weak_password"}); return }
		salt, err := randomHex(16); if err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"random_error"}); return }
		accounts[idx].Salt = salt; accounts[idx].Hash = accountPasswordHash(q.Password, salt)
		if saveDriverAccounts(accounts) != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		revokeDriverSessions(target)
		appendGatAdminAudit(actor, action, target, "password replaced and sessions revoked")
	case "reset_mission":
		gatProgressMu.Lock(); all := loadGatProgress(); p := ensureGatProgress(all, target); p.CurrentMission = nil; p.LastOnJob = false; err := saveGatProgress(all); gatProgressMu.Unlock()
		if err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		appendGatAdminAudit(actor, action, target, "current mission cleared")
	case "role":
		if actorRole != "owner" { jsonOut(w,403,map[string]any{"ok":false,"error":"owner_required"}); return }
		if targetRole == "owner" { jsonOut(w,400,map[string]any{"ok":false,"error":"owner_protected"}); return }
		newRole := normalizedAdminRole(q.Role)
		if newRole == "owner" { jsonOut(w,400,map[string]any{"ok":false,"error":"invalid_role"}); return }
		accounts[idx].Role = newRole
		if saveDriverAccounts(accounts) != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		appendGatAdminAudit(actor, action, target, "role="+newRole)
	case "delete":
		if actorRole != "owner" && actorRole != "admin" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
		if targetRole == "owner" { jsonOut(w,400,map[string]any{"ok":false,"error":"owner_protected"}); return }
		accounts = append(accounts[:idx], accounts[idx+1:]...)
		if saveDriverAccounts(accounts) != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
		revokeDriverSessions(target)
		gatProgressMu.Lock(); all := loadGatProgress(); delete(all, accountKey(target)); _ = saveGatProgress(all); gatProgressMu.Unlock()
		appendGatAdminAudit(actor, action, target, "account and progress deleted")
	default:
		jsonOut(w,400,map[string]any{"ok":false,"error":"unknown_action"}); return
	}
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "action": action, "target": target})
}

func (a *agent) siteAdminAudit(w http.ResponseWriter, r *http.Request) {
	var q gatAdminRequest
	_, role, ok := gatSiteAdminAuth(w, r, &q); if !ok { return }
	if role == "moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
	gatAdminMu.Lock(); list := loadGatAdminAudit(); gatAdminMu.Unlock()
	if len(list) > 100 { list = list[len(list)-100:] }
	for i,j := 0,len(list)-1; i<j; i,j=i+1,j-1 { list[i],list[j] = list[j],list[i] }
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "audit": list})
}

'''

if 'func (a *agent) siteAdminSession(' not in s:
    marker='func (a *agent) publicVersion('
    pos=s.find(marker)
    if pos<0: raise SystemExit('publicVersion marker not found')
    s=s[:pos]+support+s[pos:]

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.20 admin backend applied')
