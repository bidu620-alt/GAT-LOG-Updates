from pathlib import Path

agent = Path('/tmp/gat-src/cmd/agent/main.go')
core = Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.10"' in c:
    c = c.replace('InternalVersion = "1.0.10"', 'InternalVersion = "1.0.12"', 1)
elif 'InternalVersion = "1.0.12"' not in c:
    raise SystemExit('versao 1.0.10 do agente nao encontrada')

# Keep the existing binding structure and only add the account identity.
if 'AccountUser string' not in c:
    marker = 'type Binding struct {'
    pos = c.find(marker)
    if pos < 0:
        raise SystemExit('Binding struct nao encontrado')
    line_end = c.find('\n', pos)
    c = c[:line_end+1] + '\tAccountUser string `json:"account_user,omitempty"`\n' + c[line_end+1:]
core.write_text(c, encoding='utf-8')

s = agent.read_text(encoding='utf-8')
for imp in ['"crypto/rand"', '"crypto/sha256"', '"crypto/subtle"', '"encoding/hex"', '"encoding/json"', '"os"', '"path/filepath"', '"unicode"']:
    if imp not in s:
        s = s.replace('import (\n', 'import (\n\t' + imp + '\n', 1)

# Browser/client account endpoints. The account authority is the main Douglas GAT host.
routes = '\tm.HandleFunc("/api/account/register", a.accountRegister)\n\tm.HandleFunc("/api/account/login", a.accountLogin)\n\tm.HandleFunc("/api/account/session", a.accountSession)\n'
if '/api/account/register' not in s:
    needle = '\tm.HandleFunc("/api/public/live", a.publicLive)\n'
    if needle not in s:
        raise SystemExit('public live route not found')
    s = s.replace(needle, needle + routes, 1)

support = r'''
const accountAuthority = "https://douglas.tail4577e8.ts.net"

type driverAccount struct {
	User      string `json:"user"`
	Key       string `json:"key"`
	Salt      string `json:"salt"`
	Hash      string `json:"hash"`
	CreatedAt string `json:"created_at"`
}

type driverAccountSession struct {
	User      string `json:"user"`
	TokenHash string `json:"token_hash"`
	ExpiresAt string `json:"expires_at"`
}

type driverAccountRequest struct {
	User     string `json:"user"`
	Password string `json:"password"`
}

type accountSessionReply struct {
	OK   bool   `json:"ok"`
	User string `json:"user"`
}

func driverAccountsPath() string { return filepath.Join(core.DataDir(), "driver_accounts.json") }
func driverAccountSessionsPath() string { return filepath.Join(core.DataDir(), "driver_account_sessions.json") }

func accountKey(v string) string { return strings.ToLower(strings.TrimSpace(v)) }

func validAccountUser(v string) bool {
	v = strings.TrimSpace(v)
	if len([]rune(v)) < 3 || len([]rune(v)) > 32 { return false }
	for _, ch := range v {
		if unicode.IsLetter(ch) || unicode.IsDigit(ch) || ch == '_' || ch == '-' || ch == '.' { continue }
		return false
	}
	return true
}

func accountPasswordHash(password, salt string) string {
	x := []byte(salt + "\x00" + password)
	for i := 0; i < 120000; i++ {
		sum := sha256.Sum256(x)
		x = sum[:]
	}
	return hex.EncodeToString(x)
}

func randomHex(n int) (string, error) {
	b := make([]byte, n)
	if _, err := rand.Read(b); err != nil { return "", err }
	return hex.EncodeToString(b), nil
}

func tokenHash(v string) string {
	s := sha256.Sum256([]byte(v))
	return hex.EncodeToString(s[:])
}

func loadDriverAccounts() []driverAccount {
	var list []driverAccount
	_ = core.LoadJSON(driverAccountsPath(), &list)
	if list == nil { list = []driverAccount{} }
	return list
}

func saveDriverAccounts(list []driverAccount) error { return core.SaveJSON(driverAccountsPath(), list) }

func loadDriverAccountSessions() []driverAccountSession {
	var list []driverAccountSession
	_ = core.LoadJSON(driverAccountSessionsPath(), &list)
	if list == nil { list = []driverAccountSession{} }
	return list
}

func saveDriverAccountSessions(list []driverAccountSession) error { return core.SaveJSON(driverAccountSessionsPath(), list) }

func verifyDriverPassword(user, password string) (driverAccount, bool) {
	key := accountKey(user)
	for _, acc := range loadDriverAccounts() {
		if acc.Key != key { continue }
		got := accountPasswordHash(password, acc.Salt)
		if subtle.ConstantTimeCompare([]byte(strings.ToLower(got)), []byte(strings.ToLower(acc.Hash))) == 1 { return acc, true }
		return driverAccount{}, false
	}
	return driverAccount{}, false
}

func issueDriverAccountToken(user string) (string, time.Time, error) {
	token, err := randomHex(32)
	if err != nil { return "", time.Time{}, err }
	now := time.Now().UTC()
	expires := now.Add(30 * 24 * time.Hour)
	list := loadDriverAccountSessions()
	clean := make([]driverAccountSession, 0, len(list)+1)
	for _, item := range list {
		t, e := time.Parse(time.RFC3339, item.ExpiresAt)
		if e == nil && t.After(now) { clean = append(clean, item) }
	}
	clean = append(clean, driverAccountSession{User: user, TokenHash: tokenHash(token), ExpiresAt: expires.Format(time.RFC3339)})
	if err := saveDriverAccountSessions(clean); err != nil { return "", time.Time{}, err }
	return token, expires, nil
}

func accountBearer(r *http.Request) string {
	h := strings.TrimSpace(r.Header.Get("Authorization"))
	if len(h) < 8 || !strings.EqualFold(h[:7], "Bearer ") { return "" }
	return strings.TrimSpace(h[7:])
}

func verifyLocalDriverAccountToken(token string) (string, bool) {
	if strings.TrimSpace(token) == "" { return "", false }
	want := tokenHash(token)
	now := time.Now().UTC()
	for _, item := range loadDriverAccountSessions() {
		if !strings.EqualFold(item.TokenHash, want) { continue }
		expires, err := time.Parse(time.RFC3339, item.ExpiresAt)
		if err != nil || !expires.After(now) { return "", false }
		return item.User, true
	}
	return "", false
}

func verifyDriverAccountAuthority(user, token string) (string, bool) {
	if local, ok := verifyLocalDriverAccountToken(token); ok {
		if user == "" || strings.EqualFold(strings.TrimSpace(user), strings.TrimSpace(local)) { return local, true }
		return "", false
	}

	req, err := http.NewRequest(http.MethodGet, accountAuthority+"/api/account/session", nil)
	if err != nil { return "", false }
	req.Header.Set("Authorization", "Bearer "+token)
	client := &http.Client{Timeout: 4 * time.Second}
	resp, err := client.Do(req)
	if err != nil { return "", false }
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK { return "", false }
	var out accountSessionReply
	if json.NewDecoder(resp.Body).Decode(&out) != nil || !out.OK || strings.TrimSpace(out.User) == "" { return "", false }
	if strings.TrimSpace(user) != "" && !strings.EqualFold(strings.TrimSpace(user), strings.TrimSpace(out.User)) { return "", false }
	return out.User, true
}

func (a *agent) accountRegister(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	var q driverAccountRequest
	if decode(r, &q) != nil { jsonOut(w, 400, map[string]any{"ok": false, "error": "bad_request"}); return }
	q.User = strings.TrimSpace(q.User)
	if !validAccountUser(q.User) { jsonOut(w, 400, map[string]any{"ok": false, "error": "invalid_user"}); return }
	if len(q.Password) < 6 { jsonOut(w, 400, map[string]any{"ok": false, "error": "weak_password"}); return }
	list := loadDriverAccounts()
	key := accountKey(q.User)
	for _, acc := range list {
		if acc.Key == key { jsonOut(w, 409, map[string]any{"ok": false, "error": "user_exists"}); return }
	}
	salt, err := randomHex(16)
	if err != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "random_error"}); return }
	list = append(list, driverAccount{User: q.User, Key: key, Salt: salt, Hash: accountPasswordHash(q.Password, salt), CreatedAt: time.Now().UTC().Format(time.RFC3339)})
	if saveDriverAccounts(list) != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "save_error"}); return }
	token, expires, err := issueDriverAccountToken(q.User)
	if err != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "token_error"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "user": q.User, "token": token, "expires_at": expires.Format(time.RFC3339)})
}

func (a *agent) accountLogin(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	var q driverAccountRequest
	if decode(r, &q) != nil { jsonOut(w, 400, map[string]any{"ok": false, "error": "bad_request"}); return }
	acc, ok := verifyDriverPassword(q.User, q.Password)
	if !ok { time.Sleep(250*time.Millisecond); jsonOut(w, 401, map[string]any{"ok": false, "error": "invalid_credentials"}); return }
	token, expires, err := issueDriverAccountToken(acc.User)
	if err != nil { jsonOut(w, 500, map[string]any{"ok": false, "error": "token_error"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "user": acc.User, "token": token, "expires_at": expires.Format(time.RFC3339)})
}

func (a *agent) accountSession(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	user, ok := verifyLocalDriverAccountToken(accountBearer(r))
	if !ok { jsonOut(w, 401, map[string]any{"ok": false, "error": "unauthorized"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "user": user})
}

'''

if 'type driverAccount struct {' not in s:
    marker = 'func (a *agent) publicLive('
    pos = s.find(marker)
    if pos < 0: raise SystemExit('publicLive nao encontrado')
    s = s[:pos] + support + s[pos:]

# Extend client identity request without breaking old clients.
old_req = '''type clientReq struct {\n\tDriver    string         `json:"driver"`\n\tDeviceID  string         `json:"device_id"`\n\tToken     string         `json:"token"`\n\tTelemetry map[string]any `json:"telemetry"`\n}'''
new_req = '''type clientReq struct {\n\tDriver       string         `json:"driver"`\n\tDeviceID     string         `json:"device_id"`\n\tToken        string         `json:"token"`\n\tAccountUser  string         `json:"account_user"`\n\tAccountToken string         `json:"account_token"`\n\tTelemetry    map[string]any `json:"telemetry"`\n}'''
if old_req in s:
    s = s.replace(old_req, new_req, 1)
elif 'AccountUser  string' not in s:
    raise SystemExit('clientReq nao encontrado')

# During /api/client/login verify the optional GAT account and bind it to the session player.
needle = '\tkey := bindKey(canonical)\n\ta.mu.Lock()\n'
if needle not in s:
    raise SystemExit('clientLogin binding point not found')
account_check = '''\tverifiedAccount := ""\n\tif strings.TrimSpace(q.AccountUser) != "" || strings.TrimSpace(q.AccountToken) != "" {\n\t\tvar accountOK bool\n\t\tverifiedAccount, accountOK = verifyDriverAccountAuthority(q.AccountUser, q.AccountToken)\n\t\tif !accountOK {\n\t\t\tjsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "account_invalid"})\n\t\t\treturn\n\t\t}\n\t}\n'''
if account_check not in s:
    # only replace the first occurrence, which is clientLogin before validateClient
    s = s.replace(needle, account_check + needle, 1)

# Persist account on Binding when authenticated.
needle2 = '\tb.Driver = canonical\n\tb.DeviceID = q.DeviceID\n'
replace2 = '\tb.Driver = canonical\n\tb.DeviceID = q.DeviceID\n\tif verifiedAccount != "" { b.AccountUser = verifiedAccount }\n'
if needle2 in s:
    s = s.replace(needle2, replace2, 1)

old_reply = 'jsonOut(w, 200, map[string]any{"ok": true, "driver": canonical, "token": b.Token, "session_id": a.status.SessionID})'
new_reply = 'jsonOut(w, 200, map[string]any{"ok": true, "driver": canonical, "account_user": b.AccountUser, "token": b.Token, "session_id": a.status.SessionID})'
if old_reply in s:
    s = s.replace(old_reply, new_reply, 1)

# Add account_user to the sanitized public feed by looking up the existing binding.
old_public = '''\t\tpublicTelemetry = append(publicTelemetry, map[string]any{\n\t\t\t"driver":        v.Driver,'''
new_public = '''\t\taccountUser := ""\n\t\ta.mu.RLock()\n\t\tif b, ok := a.bindings[bindKey(v.Driver)]; ok { accountUser = b.AccountUser }\n\t\ta.mu.RUnlock()\n\t\tpublicTelemetry = append(publicTelemetry, map[string]any{\n\t\t\t"driver":        v.Driver,\n\t\t\t"account_user":  accountUser,'''
if old_public in s:
    s = s.replace(old_public, new_public, 1)
elif '"account_user":  accountUser' not in s:
    raise SystemExit('public telemetry map not found')

agent.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.12 driver accounts applied')
