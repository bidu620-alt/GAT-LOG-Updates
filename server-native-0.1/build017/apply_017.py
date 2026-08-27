from pathlib import Path
import re

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'
ui = root / 'cmd/ui/main.go'

# Version only. All functional fixes from 0.1.5 are applied before this patch.
s = core.read_text(encoding='utf-8')
s = s.replace('InternalVersion = "0.1.5"', 'InternalVersion = "0.1.7"')
core.write_text(s, encoding='utf-8')

# ---------------- UI stability ----------------
u = ui.read_text(encoding='utf-8')
u = u.replace('GAT-LOG SERVER NATIVE 0.1.5 |', 'GAT-LOG SERVER NATIVE 0.1.7 |')

# A separate mutex prevents overlapping refreshAll calls from login, polling and actions.
m = re.search(r'(type App struct \{\n\s*mu\s+sync\.RWMutex\n)', u)
if not m:
    raise SystemExit('App mutex field not found')
u = u[:m.end()] + '\trefreshMu                                          sync.Mutex\n' + u[m.end():]

# Use PostMessage so worker goroutines never ask Windows to repaint directly.
needle = '\tpInvalidateRect         = user32.NewProc("InvalidateRect")\n'
if needle not in u:
    raise SystemExit('InvalidateRect proc not found')
u = u.replace(needle, needle + '\tpPostMessage            = user32.NewProc("PostMessageW")\n', 1)

# Do not repaint the whole application every second. Repaint only when data/page changes.
u = u.replace('\tpSetTimer.Call(hw, 1, 1000, 0)\n', '', 1)
u = u.replace('\tcase WM_TIMER:\n\t\tinvalidate()\n\t\treturn 0\n', '', 1)

# Handle posted repaint messages on the window thread.
needle = '\tcase WM_DESTROY:\n'
if needle not in u:
    raise SystemExit('WM_DESTROY case not found')
u = u.replace(needle, '\tcase WM_APP_REFRESH:\n\t\tinvalidate()\n\t\treturn 0\n' + needle, 1)

# ensureAgent must not mutate UI state from its worker path; pollAgent owns that state.
u = u.replace('\t\tapp.agentReachable = true\n\t\treturn\n', '\t\treturn\n', 1)
u = u.replace('\t\t\tapp.agentReachable = true\n\t\t\treturn\n', '\t\t\treturn\n', 1)

# Replace poll loop: slower cadence, restart cooldown, locked state and no overlapping refresh.
start = u.index('func (a *App) pollAgent() {')
end = u.index('\nfunc (a *App) refreshAll() {', start)
new_poll = r'''func (a *App) pollAgent() {
	var lastAgentStart time.Time
	for {
		reachable := health()
		if !reachable && (lastAgentStart.IsZero() || time.Since(lastAgentStart) >= 15*time.Second) {
			lastAgentStart = time.Now()
			ensureAgent()
			reachable = health()
		}

		a.mu.Lock()
		changed := a.agentReachable != reachable
		a.agentReachable = reachable
		logged := a.logged
		a.mu.Unlock()

		if changed {
			a.postRefresh()
		}
		if reachable && logged {
			a.refreshAll()
		}
		time.Sleep(4 * time.Second)
	}
}

func (a *App) postRefresh() {
	if a.hwnd != 0 {
		pPostMessage.Call(uintptr(a.hwnd), WM_APP_REFRESH, 0, 0)
	}
}
'''
u = u[:start] + new_poll + u[end:]

# Serialize refreshes and post repaint back to the window thread.
needle = 'func (a *App) refreshAll() {\n'
if needle not in u:
    raise SystemExit('refreshAll not found')
u = u.replace(needle, needle + '\tif !a.refreshMu.TryLock() {\n\t\treturn\n\t}\n\tdefer a.refreshMu.Unlock()\n', 1)
u = u.replace('\tpInvalidateRect.Call(uintptr(a.hwnd), 0, 0)\n}\nfunc (a *App) refreshExtras()', '\ta.postRefresh()\n}\nfunc (a *App) refreshExtras()', 1)
# Extras is always fetched from a worker goroutine; repaint safely as well.
u = u.replace('\tinvalidate()\n}\nfunc saveConfig()', '\ta.postRefresh()\n}\nfunc saveConfig()', 1)

# Lock the logged flag because pollAgent reads it from another goroutine.
u = u.replace('\tapp.logged = true\n\tapp.page = "home"', '\tapp.mu.Lock()\n\tapp.logged = true\n\tapp.mu.Unlock()\n\tapp.page = "home"', 1)

# Snapshot reachability under the same mutex when painting login.
needle = '\tstatus := "Agente de telemetria conectado"\n'
if needle not in u:
    raise SystemExit('login status block not found')
u = u.replace(needle, '\tapp.mu.RLock()\n\tagentReachable := app.agentReachable\n\tapp.mu.RUnlock()\n' + needle, 1)
u = u.replace('\tif !app.agentReachable {\n', '\tif !agentReachable {\n', 1)

ui.write_text(u, encoding='utf-8')

# ---------------- Agent stability ----------------
a = agent.read_text(encoding='utf-8')
# Prevent expensive ParseSession scans from overlapping when actions/funnel/poll all request refreshes.
m = re.search(r'(type agent struct \{\n)', a)
if not m:
    raise SystemExit('agent struct not found')
a = a[:m.end()] + '\trefreshMu sync.Mutex\n' + a[m.end():]
needle = 'func (a *agent) refreshStatus() {\n'
if needle not in a:
    raise SystemExit('agent refreshStatus not found')
a = a.replace(needle, needle + '\tif !a.refreshMu.TryLock() {\n\t\treturn\n\t}\n\tdefer a.refreshMu.Unlock()\n', 1)
# Four seconds is fast enough for the dashboard while avoiding constant 3 MiB log scans.
a = a.replace('t := time.NewTicker(2 * time.Second)', 't := time.NewTicker(4 * time.Second)', 1)
agent.write_text(a, encoding='utf-8')
