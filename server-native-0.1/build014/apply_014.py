from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'

s = core.read_text(encoding='utf-8')
s = s.replace('InternalVersion = "0.1.0"', 'InternalVersion = "0.1.4"')

old_re = r'(?im)^.*?\[MP\]\s+([^\r\n,]+?)\s+(connected|disconnected)(?:,|\s|$)'
new_re = r'(?im)^.*?\[MP\]\s+([^\[\r\n,]+?)\s+(connected|disconnected)(?:,|\s|$)'
if old_re not in s:
    raise SystemExit('player regex not found')
s = s.replace(old_re, new_re)

old_path = 'func TelemetryPath() string   { return filepath.Join(DataDir(), "telemetry", "current.json") }'
new_path = 'func TelemetryPath() string   { return filepath.Join(DataDir(), "telemetry.json") }'
if old_path not in s:
    raise SystemExit('telemetry path not found')
s = s.replace(old_path, new_path)

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

t = agent.read_text(encoding='utf-8')
marker = '\ta.refreshStatus()\n\tgo a.pollLoop()'
if marker not in t:
    raise SystemExit('agent startup marker not found')
t = t.replace(marker, '\ta.refreshStatus()\n\tgo a.ensureFunnel()\n\tgo a.pollLoop()', 1)

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
	a.refreshStatus()
	core.AppendLog("Funnel 5055 ativo")
}

'''
pos = t.index('func (a *agent) wrap(')
t = t[:pos] + method + t[pos:]
agent.write_text(t, encoding='utf-8')
