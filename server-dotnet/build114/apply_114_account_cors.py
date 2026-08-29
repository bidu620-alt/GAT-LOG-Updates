from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.13"' in c:
    c=c.replace('InternalVersion = "1.0.13"','InternalVersion = "1.0.14"',1)
elif 'InternalVersion = "1.0.14"' not in c:
    raise SystemExit('versao 1.0.13 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')
helper=r'''
func gatAccountCors(w http.ResponseWriter, r *http.Request) bool {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Headers", "Authorization, Content-Type")
	w.Header().Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
	w.Header().Set("Access-Control-Max-Age", "600")
	if r.Method == http.MethodOptions {
		w.WriteHeader(http.StatusNoContent)
		return true
	}
	return false
}

'''
if 'func gatAccountCors(' not in s:
    marker='func (a *agent) accountRegister('
    pos=s.find(marker)
    if pos<0: raise SystemExit('accountRegister nao encontrado')
    s=s[:pos]+helper+s[pos:]

handlers=['accountRegister','accountLogin','accountSession','accountProfile','accountWorkCurrent','accountWorkTake','accountTelemetry']
for name in handlers:
    old=f'func (a *agent) {name}(w http.ResponseWriter, r *http.Request) {{\n'
    new=old+'\tif gatAccountCors(w, r) { return }\n'
    if old in s and new not in s:
        s=s.replace(old,new,1)

agent.write_text(s,encoding='utf-8')
print('GAT-LOG Server 1.0.14 account CORS applied')
