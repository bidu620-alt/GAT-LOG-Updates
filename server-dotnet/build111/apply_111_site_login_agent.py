from pathlib import Path

agent = Path('/tmp/gat-src/cmd/agent/main.go')
core = Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

# Bump agent while preserving 1.0.10 public telemetry.
c = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.10"' in c:
    c = c.replace('InternalVersion = "1.0.10"', 'InternalVersion = "1.0.11"', 1)
elif 'InternalVersion = "1.0.11"' not in c:
    raise SystemExit('versao 1.0.10 do agente nao encontrada')
core.write_text(c, encoding='utf-8')

s = agent.read_text(encoding='utf-8')

# Imports required by site authentication/session support.
for imp in ['"crypto/rand"', '"crypto/sha256"', '"crypto/subtle"', '"encoding/hex"', '"encoding/json"', '"os"', '"path/filepath"', '"sync"']:
    if imp not in s:
        marker = 'import (\n'
        if marker not in s:
            raise SystemExit('bloco import nao encontrado')
        s = s.replace(marker, marker + '\t' + imp + '\n', 1)

# Browser sends a Bearer token after login.
s = s.replace('Content-Type, X-GAT-Admin', 'Content-Type, X-GAT-Admin, Authorization')

# New login/session endpoints.
login_routes = '\tm.HandleFunc("/api/public/site-login", a.publicSiteLogin)\n\tm.HandleFunc("/api/public/site-session", a.publicSiteSession)\n'
if '/api/public/site-login' not in s:
    needle = '\tm.HandleFunc("/api/public/live", a.publicLive)\n'
    if needle not in s:
        raise SystemExit('rota public/live nao encontrada')
    s = s.replace(needle, needle + login_routes, 1)

support = r'''
type siteAuthFile struct {
	User string `json:"user"`
	Salt string `json:"salt"`
	Hash string `json:"hash"`
}

type siteLoginRequest struct {
	User     string `json:"user"`
	Password string `json:"password"`
}

var siteSessionMu sync.Mutex
var siteSessions = map[string]time.Time{}

func siteAuthPath() string {
	return filepath.Join(core.DataDir(), "site_auth.json")
}

func loadSiteAuth() (siteAuthFile, error) {
	var a siteAuthFile
	b, err := os.ReadFile(siteAuthPath())
	if err != nil {
		return a, err
	}
	if err := json.Unmarshal(b, &a); err != nil {
		return a, err
	}
	if strings.TrimSpace(a.User) == "" || strings.TrimSpace(a.Salt) == "" || strings.TrimSpace(a.Hash) == "" {
		return a, fmt.Errorf("site auth incomplete")
	}
	return a, nil
}

func sitePasswordHash(password, salt string) string {
	x := []byte(salt + "\x00" + password)
	for i := 0; i < 120000; i++ {
		sum := sha256.Sum256(x)
		x = sum[:]
	}
	return hex.EncodeToString(x)
}

func verifySiteCredentials(user, password string) bool {
	a, err := loadSiteAuth()
	if err != nil {
		return false
	}
	if !strings.EqualFold(strings.TrimSpace(user), strings.TrimSpace(a.User)) {
		return false
	}
	got := sitePasswordHash(password, a.Salt)
	return subtle.ConstantTimeCompare([]byte(strings.ToLower(got)), []byte(strings.ToLower(a.Hash))) == 1
}

func issueSiteToken() (string, time.Time, error) {
	b := make([]byte, 32)
	if _, err := rand.Read(b); err != nil {
		return "", time.Time{}, err
	}
	token := hex.EncodeToString(b)
	expires := time.Now().Add(12 * time.Hour)
	siteSessionMu.Lock()
	for k, v := range siteSessions {
		if time.Now().After(v) {
			delete(siteSessions, k)
		}
	}
	siteSessions[token] = expires
	siteSessionMu.Unlock()
	return token, expires, nil
}

func siteTokenFromRequest(r *http.Request) string {
	h := strings.TrimSpace(r.Header.Get("Authorization"))
	if len(h) < 8 || !strings.EqualFold(h[:7], "Bearer ") {
		return ""
	}
	return strings.TrimSpace(h[7:])
}

func siteAuthorized(r *http.Request) bool {
	token := siteTokenFromRequest(r)
	if token == "" {
		return false
	}
	siteSessionMu.Lock()
	defer siteSessionMu.Unlock()
	expires, ok := siteSessions[token]
	if !ok || time.Now().After(expires) {
		if ok {
			delete(siteSessions, token)
		}
		return false
	}
	return true
}

func (a *agent) publicSiteLogin(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		jsonOut(w, http.StatusMethodNotAllowed, map[string]any{"ok": false, "error": "method_not_allowed"})
		return
	}
	if _, err := os.Stat(siteAuthPath()); err != nil {
		jsonOut(w, http.StatusServiceUnavailable, map[string]any{"ok": false, "error": "site_access_not_configured"})
		return
	}
	var q siteLoginRequest
	if decode(r, &q) != nil {
		jsonOut(w, http.StatusBadRequest, map[string]any{"ok": false, "error": "bad_request"})
		return
	}
	if !verifySiteCredentials(q.User, q.Password) {
		time.Sleep(250 * time.Millisecond)
		jsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "invalid_credentials"})
		return
	}
	token, expires, err := issueSiteToken()
	if err != nil {
		jsonOut(w, http.StatusInternalServerError, map[string]any{"ok": false, "error": "token_error"})
		return
	}
	jsonOut(w, http.StatusOK, map[string]any{"ok": true, "token": token, "expires_at": expires.UTC().Format(time.RFC3339)})
}

func (a *agent) publicSiteSession(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		jsonOut(w, http.StatusMethodNotAllowed, map[string]any{"ok": false, "error": "method_not_allowed"})
		return
	}
	if !siteAuthorized(r) {
		jsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "unauthorized"})
		return
	}
	jsonOut(w, http.StatusOK, map[string]any{"ok": true})
}

'''

if 'type siteAuthFile struct {' not in s:
    marker = 'func (a *agent) publicLive('
    pos = s.find(marker)
    if pos < 0:
        raise SystemExit('publicLive nao encontrado')
    s = s[:pos] + support + s[pos:]

# Protect the sanitized live feed itself, not only the HTML screen.
public_marker = 'func (a *agent) publicLive(w http.ResponseWriter, r *http.Request) {\n'
if public_marker not in s:
    raise SystemExit('inicio publicLive nao encontrado')
auth_block = '\tif !siteAuthorized(r) {\n\t\tjsonOut(w, http.StatusUnauthorized, map[string]any{"ok": false, "error": "unauthorized"})\n\t\treturn\n\t}\n'
if auth_block not in s:
    method_check = '\tif r.Method != http.MethodGet {\n'
    start = s.find(public_marker)
    pos = s.find(method_check, start)
    if pos < 0:
        raise SystemExit('validacao GET publicLive nao encontrada')
    # Keep method validation first, then require the site session.
    close = s.find('\t}\n', pos)
    if close < 0:
        raise SystemExit('fim validacao GET nao encontrado')
    close += len('\t}\n')
    s = s[:close] + auth_block + s[close:]

agent.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.11 site auth applied to agent')
