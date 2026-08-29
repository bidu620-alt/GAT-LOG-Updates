from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.26"' in c:
    c=c.replace('InternalVersion = "1.0.26"','InternalVersion = "1.0.27"',1)
elif 'InternalVersion = "1.0.27"' not in c:
    raise SystemExit('versao 1.0.26 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')
changes=0
replacements=[
    ('km >= 500 && gatCargoMatch','km >= 250 && gatCargoMatch'),
    ('"min_km":500','"min_km":250'),
    ('"min_km": 500','"min_km": 250'),
    ('pelo menos 500 km','pelo menos 250 km'),
    ('Pelo menos 500 km','Pelo menos 250 km'),
    ('mínimo 500 km','mínimo 250 km'),
    ('minimo 500 km','minimo 250 km'),
    ('mínimo de 500 km','mínimo de 250 km'),
    ('minimo de 500 km','minimo de 250 km'),
]
for old,new in replacements:
    n=s.count(old)
    if n:
        s=s.replace(old,new)
        changes+=n
if 'km >= 250 && gatCargoMatch' not in s:
    raise SystemExit('validacao minima 250 km nao aplicada')
if '"min_km":250' not in s and '"min_km": 250' not in s:
    raise SystemExit('min_km 250 nao publicado')
agent.write_text(s,encoding='utf-8')
print('patch 1.0.27 aplicado; substituicoes:',changes)
