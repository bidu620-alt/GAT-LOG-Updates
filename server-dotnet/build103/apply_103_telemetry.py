from pathlib import Path

agent = Path('/tmp/gat-src/cmd/agent/main.go')
if not agent.exists():
    raise SystemExit('agent source not found')

s = agent.read_text(encoding='utf-8')
old = '''func (a *agent) uiStatus(w http.ResponseWriter, r *http.Request) {
\ta.mu.RLock()
\ts := a.status
\ta.mu.RUnlock()
\tjsonOut(w, 200, s)
}
'''
new = '''func (a *agent) uiStatus(w http.ResponseWriter, r *http.Request) {
\t// Keep expensive server/session parsing cached, but always expose the newest
\t// telemetry already received in memory. This makes speed/route updates fresh
\t// without increasing server.log scans.
\ta.mu.RLock()
\ts := a.status
\ttel := make([]core.TelemetryRecord, 0, len(a.telemetry))
\tfor _, v := range a.telemetry {
\t\ttel = append(tel, v)
\t}
\ta.mu.RUnlock()
\tsortTelemetry(tel)
\ts.Telemetry = tel
\tjsonOut(w, 200, s)
}
'''
if old not in s:
    raise SystemExit('uiStatus block not found')
s = s.replace(old, new, 1)
agent.write_text(s, encoding='utf-8')
