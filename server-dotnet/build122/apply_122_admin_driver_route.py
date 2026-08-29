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

# Corrige o bloco administrativo que a 1.0.21 inseriu com sequencias literais "\\t".
# Fazemos a limpeza apenas entre set_progress e o proximo case real para nao tocar em strings Go legitimas.
lines=s.splitlines()
out=[]
in_bad_block=False
found_bad=False
for line in lines:
    if line.startswith('\\tcase "set_progress":'):
        in_bad_block=True
        found_bad=True
    if in_bad_block and line.startswith('\\t'):
        level=0
        while line.startswith('\\t'):
            level += 1
            line = line[2:]
        line = ('\t' * level) + line
    elif in_bad_block and line.startswith('\tcase "role":'):
        in_bad_block=False
    out.append(line)
s='\n'.join(out)+'\n'

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
if '\\tcase "set_progress":' in s or '\\tcase "delete_delivery":' in s:
    raise SystemExit('bloco administrativo ainda possui tabulacao literal')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.22: bloco Admin normalizado e rota /api/site/admin/driver registrada')
