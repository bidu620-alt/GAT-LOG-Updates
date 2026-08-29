from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.21"' in c:
    c=c.replace('InternalVersion = "1.0.21"','InternalVersion = "1.0.22"',1)
elif 'InternalVersion = "1.0.22"' not in c:
    raise SystemExit('versao 1.0.21 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')
route='\tm.HandleFunc("/api/site/admin/driver", a.siteAdminDriver)\n'
needle='\tm.HandleFunc("/api/site/admin/drivers", a.siteAdminDrivers)\n'
if route not in s:
    if needle not in s:
        raise SystemExit('rota admin/drivers nao encontrada')
    s=s.replace(needle,needle+route,1)

if route not in s:
    raise SystemExit('rota admin/driver nao foi registrada')
if 'func (a *agent) siteAdminDriver(' not in s:
    raise SystemExit('handler siteAdminDriver nao encontrado')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.22: rota exata /api/site/admin/driver registrada')
