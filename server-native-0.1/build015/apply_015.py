from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'
ui = root / 'cmd/ui/main.go'

# ---------- CORE: preserve 0.1.4 fixes + bump to 0.1.5 ----------
s = core.read_text(encoding='utf-8')
s = s.replace('InternalVersion = "0.1.0"', 'InternalVersion = "0.1.5"')
s = s.replace('InternalVersion = "0.1.4"', 'InternalVersion = "0.1.5"')

old_re = r'(?im)^.*?\[MP\]\s+([^\r\n,]+?)\s+(connected|disconnected)(?:,|\s|$)'
new_re = r'(?im)^.*?\[MP\]\s+([^\[\r\n,]+?)\s+(connected|disconnected)(?:,|\s|$)'
if old_re in s:
    s = s.replace(old_re, new_re)

old_path = 'func TelemetryPath() string   { return filepath.Join(DataDir(), "telemetry", "current.json") }'
new_path = 'func TelemetryPath() string   { return filepath.Join(DataDir(), "telemetry.json") }'
if old_path in s:
    s = s.replace(old_path, new_path)

# Funnel must NEVER fall back to foreground mode, otherwise it can block forever.
start = s.index('func StartFunnel() error {')
end = s.index('\nfunc AppendLog(', start)
replacement = r'''func tailscaleExe() string {
	exe := "tailscale.exe"
	candidates := []string{filepath.Join(os.Getenv("ProgramFiles"), "Tailscale", "tailscale.exe"), filepath.Join(os.Getenv("ProgramFiles(x86)"), "Tailscale", "tailscale.exe")}
	for _, p := range candidates {
		if _, e := os.Stat(p); e == nil {
			return p
		}
	}
	return exe
}

func StartFunnel() error {
	exe := tailscaleExe()
	if _, e := RunHidden(exe, "funnel", "--bg", "--yes", "5055"); e == nil {
		return nil
	}
	_, e := RunHidden(exe, "funnel", "--bg", "5055")
	return e
}

func TailscalePublicURL() string {
	out, e := RunHidden(tailscaleExe(), "status", "--json")
	if e != nil || strings.TrimSpace(string(out)) == "" {
		return ""
	}
	var st struct {
		Self struct {
			DNSName string `json:"DNSName"`
		} `json:"Self"`
	}
	if json.Unmarshal(out, &st) != nil {
		return ""
	}
	dns := strings.TrimSpace(strings.TrimSuffix(st.Self.DNSName, "."))
	if dns == "" {
		return ""
	}
	return "https://" + dns
}
'''
s = s[:start] + replacement + s[end:]
core.write_text(s, encoding='utf-8')

# ---------- AGENT: API becomes available immediately ----------
t = agent.read_text(encoding='utf-8')

old_start = '''\ta.loadBindings()\n\ta.loadTelemetry()\n\ta.refreshStatus()\n\tgo a.pollLoop()\n\tmux := http.NewServeMux()\n\ta.routes(mux)\n\ta.httpSrv = &http.Server{Addr: core.AgentAddr, Handler: a.wrap(mux), ReadHeaderTimeout: 5 * time.Second, IdleTimeout: 30 * time.Second}\n\tcore.AppendLog("Agent Native %s iniciado em %s", core.InternalVersion, core.AgentAddr)'''
new_start = '''\ta.loadBindings()\n\ta.loadTelemetry()\n\tmux := http.NewServeMux()\n\ta.routes(mux)\n\ta.httpSrv = &http.Server{Addr: core.AgentAddr, Handler: a.wrap(mux), ReadHeaderTimeout: 5 * time.Second, IdleTimeout: 30 * time.Second}\n\tgo a.refreshStatus()\n\tgo a.pollLoop()\n\tgo a.ensureFunnel()\n\tcore.AppendLog("Agent Native %s iniciado em %s", core.InternalVersion, core.AgentAddr)'''
if old_start not in t:
    raise SystemExit('agent startup block not found')
t = t.replace(old_start, new_start, 1)

old_status = '''func (a *agent) uiStatus(w http.ResponseWriter, r *http.Request) {\n\ta.refreshStatus()\n\ta.mu.RLock()\n\ts := a.status\n\ta.mu.RUnlock()\n\tjsonOut(w, 200, s)\n}'''
new_status = '''func (a *agent) uiStatus(w http.ResponseWriter, r *http.Request) {\n\ta.mu.RLock()\n\ts := a.status\n\ta.mu.RUnlock()\n\tjsonOut(w, 200, s)\n}'''
if old_status not in t:
    raise SystemExit('uiStatus block not found')
t = t.replace(old_status, new_status, 1)

# Persist telemetry without delaying the client's HTTP 200.
t = t.replace('\tif persist {\n\t\ta.saveTelemetry()\n\t}\n\ta.detectEvents(prev, rec)', '\tif persist {\n\t\tgo a.saveTelemetry()\n\t}\n\ta.detectEvents(prev, rec)', 1)

# Config/action responses should not wait for another session/log scan.
t = t.replace('\ta.refreshStatus()\n\tjsonOut(w, 200, map[string]any{"ok": true})', '\tgo a.refreshStatus()\n\tjsonOut(w, 200, map[string]any{"ok": true})', 1)
t = t.replace('\ta.refreshStatus()\n\tjsonOut(w, 200, map[string]any{"ok": true, "message": msg})', '\tgo a.refreshStatus()\n\tjsonOut(w, 200, map[string]any{"ok": true, "message": msg})', 1)

method = r'''func (a *agent) ensureFunnel() {
	if err := core.StartFunnel(); err != nil {
		core.AppendLog("Funnel 5055 nao ativado: %v", err)
		return
	}
	if u := core.TailscalePublicURL(); u != "" {
		a.mu.Lock()
		a.cfg.FunnelURL = u
		c := a.cfg
		a.mu.Unlock()
		_ = core.SaveConfig(c)
	}
	go a.refreshStatus()
	core.AppendLog("Funnel 5055 ativo")
}

'''
if 'func (a *agent) ensureFunnel() {' not in t:
    pos = t.index('func (a *agent) wrap(')
    t = t[:pos] + method + t[pos:]

agent.write_text(t, encoding='utf-8')

# ---------- UI: restore truncated tail and remove startup blocking ----------
u = ui.read_text(encoding='utf-8')
fw = u.find('func firewallElevated() {')
if fw < 0:
    raise SystemExit('firewallElevated start not found')

ui_tail = r'''func firewallElevated() {
	args := `/c netsh advfirewall firewall add rule name="GAT-LOG ETS2 27015 TCP" dir=in action=allow protocol=TCP localport=27015 & netsh advfirewall firewall add rule name="GAT-LOG ETS2 27016 UDP" dir=in action=allow protocol=UDP localport=27016 & netsh advfirewall firewall add rule name="GAT-LOG API 5055 TCP" dir=in action=allow protocol=TCP localport=5055`
	r, _, _ := pShellExecute.Call(
		uintptr(app.hwnd),
		uintptr(unsafe.Pointer(u16("runas"))),
		uintptr(unsafe.Pointer(u16("cmd.exe"))),
		uintptr(unsafe.Pointer(u16(args))),
		0,
		SW_HIDE,
	)
	if r <= 32 {
		msgbox("Não foi possível solicitar permissão de administrador para o Firewall.", "GAT-LOG | Firewall", MB_OK|MB_ICONERROR)
		return
	}
	msgbox("Regras do Firewall solicitadas. Confirme a janela do Windows se ela aparecer.", "GAT-LOG | Firewall", MB_OK|MB_ICONINFORMATION)
}

func checkUpdate() {
	const manifestURL = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/server_native_version.json"
	type remoteVersion struct {
		Version  string `json:"version"`
		Notes    string `json:"notas"`
		SetupURL string `json:"setup_url"`
		SHA256   string `json:"sha256"`
	}

	cl := &http.Client{Timeout: 15 * time.Second}
	resp, err := cl.Get(manifestURL)
	if err != nil {
		msgbox("Não foi possível consultar atualizações no GitHub.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		msgbox(fmt.Sprintf("GitHub respondeu HTTP %d ao verificar a atualização.", resp.StatusCode), "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	var rv remoteVersion
	if json.NewDecoder(io.LimitReader(resp.Body, 1<<20)).Decode(&rv) != nil || strings.TrimSpace(rv.Version) == "" {
		msgbox("O manifesto de atualização é inválido.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	if strings.TrimSpace(rv.Version) == core.InternalVersion {
		msgbox("Você já está usando a versão "+core.InternalVersion+".", "GAT-LOG | Atualização", MB_OK|MB_ICONINFORMATION)
		return
	}
	if strings.TrimSpace(rv.SetupURL) == "" {
		msgbox("A atualização encontrada não possui instalador publicado.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	question := fmt.Sprintf("Nova versão disponível: %s\r\n\r\n%s\r\n\r\nDeseja atualizar agora?", rv.Version, rv.Notes)
	if msgbox(question, "GAT-LOG | Atualização", MB_YESNO|MB_ICONINFORMATION) != IDYES {
		return
	}

	dl, err := cl.Get(rv.SetupURL)
	if err != nil {
		msgbox("Falha ao baixar a atualização.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	defer dl.Body.Close()
	if dl.StatusCode != http.StatusOK {
		msgbox(fmt.Sprintf("Falha no download: HTTP %d.", dl.StatusCode), "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	b, err := io.ReadAll(io.LimitReader(dl.Body, 96<<20))
	if err != nil || len(b) == 0 {
		msgbox("Falha ao receber o arquivo da atualização.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	if expected := strings.ToLower(strings.TrimSpace(rv.SHA256)); expected != "" {
		got := fmt.Sprintf("%x", sha256.Sum256(b))
		if got != expected {
			msgbox("O SHA-256 da atualização não confere. Instalação cancelada.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
			return
		}
	}
	tmp := filepath.Join(os.TempDir(), "GAT_LOG_SERVER_UPDATE_"+strings.ReplaceAll(rv.Version, "/", "_")+".exe")
	if err := os.WriteFile(tmp, b, 0755); err != nil {
		msgbox("Não foi possível salvar o atualizador temporário.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
	cmd := exec.Command(tmp)
	cmd.Dir = filepath.Dir(tmp)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	if err := cmd.Start(); err != nil {
		msgbox("Não foi possível iniciar o atualizador.", "GAT-LOG | Atualização", MB_OK|MB_ICONERROR)
		return
	}
}
'''
u = u[:fw] + ui_tail

# sha256 is required by the rebuilt self-update function.
if '"crypto/sha256"' not in u:
    u = u.replace('\t"bytes"\n', '\t"bytes"\n\t"crypto/sha256"\n', 1)

u = u.replace('GAT-LOG SERVER NATIVE 0.1 |', 'GAT-LOG SERVER NATIVE 0.1.5 |')
u = u.replace('0.1.0', '0.1.5')
u = u.replace('0.1.4', '0.1.5')

old_main = '\tensureAgent()\n\tgo app.pollAgent()\n\trunWindow()'
new_main = '\tgo app.pollAgent()\n\trunWindow()'
if old_main not in u:
    raise SystemExit('UI synchronous ensureAgent startup not found')
u = u.replace(old_main, new_main, 1)

u = u.replace('&http.Client{Timeout: 8 * time.Second}', '&http.Client{Timeout: 2 * time.Second}')
u = u.replace('&http.Client{Timeout: 600 * time.Millisecond}', '&http.Client{Timeout: 400 * time.Millisecond}')

ui.write_text(u, encoding='utf-8')
