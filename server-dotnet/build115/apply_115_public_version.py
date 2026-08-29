from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.14"' in c:
    c=c.replace('InternalVersion = "1.0.14"','InternalVersion = "1.0.15"',1)
elif 'InternalVersion = "1.0.15"' not in c:
    raise SystemExit('versao 1.0.14 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')
route='\tm.HandleFunc("/api/public/version", a.publicVersion)\n'
if '/api/public/version' not in s:
    needle='\tm.HandleFunc("/api/public/ranking", a.publicRanking)\n'
    if needle not in s: raise SystemExit('rota public/ranking nao encontrada')
    s=s.replace(needle, route+needle,1)

handler=r'''
func (a *agent) publicVersion(w http.ResponseWriter, r *http.Request) {
	if gatAccountCors(w, r) { return }
	if r.Method != http.MethodGet { jsonOut(w, 405, map[string]any{"ok": false, "error": "method_not_allowed"}); return }
	jsonOut(w, 200, map[string]any{"ok": true, "agent_version": core.InternalVersion})
}

'''
if 'func (a *agent) publicVersion(' not in s:
    marker='func (a *agent) publicRanking('
    pos=s.find(marker)
    if pos<0: raise SystemExit('publicRanking nao encontrado')
    s=s[:pos]+handler+s[pos:]

for name in ['publicRanking','publicDriver']:
    old=f'func (a *agent) {name}(w http.ResponseWriter, r *http.Request) {{\n'
    new=old+'\tif gatAccountCors(w, r) { return }\n'
    if old in s and new not in s:
        s=s.replace(old,new,1)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.15 public version and public CORS applied')
