from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
if not agent.exists():
    raise SystemExit('fonte do agente nao encontrada')

s=agent.read_text(encoding='utf-8')
bad='\\tcase "set_progress":'
if bad in s:
    start=s.find(bad)
    end=s.find('\tcase "role":',start)
    if end < 0:
        raise SystemExit('fim do bloco Admin nao encontrado')
    block=s[start:end].replace('\\t','\t')
    s=s[:start]+block+s[end:]
elif '\tcase "set_progress":' not in s:
    raise SystemExit('bloco set_progress nao encontrado')

agent.write_text(s,encoding='utf-8')
print('indentacao do Admin 1.0.21 corrigida')
