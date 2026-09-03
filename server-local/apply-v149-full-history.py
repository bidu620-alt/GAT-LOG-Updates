from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
p=root/'worker.js'
s=p.read_text(encoding='utf-8')
old="FROM deliveries WHERE user=? ORDER BY id DESC LIMIT 100').bind(user).all();const c="
new="FROM deliveries WHERE user=? ORDER BY id DESC').bind(user).all();const c="
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('Nao encontrei o limite antigo do historico do perfil.')
if 'ORDER BY id DESC LIMIT 100' in s[s.find('async function readProfile'):s.find('async function resetAssigned')]:
    raise SystemExit('Historico do perfil ainda esta limitado a 100 viagens.')
if 'cargo_history:c.results||[]' not in s:
    raise SystemExit('Colecao de cargas da carreira nao encontrada.')
p.write_text(s,encoding='utf-8')
print('GAT Server 1.0.49: perfil expõe o histórico completo da carreira; nenhuma viagem antiga some da lista.')
