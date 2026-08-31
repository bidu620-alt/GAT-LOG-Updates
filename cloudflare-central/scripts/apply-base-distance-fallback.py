from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')
old="planned=isRbr?(teleKm||baseKm):baseKm;"
new="planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);"
if old not in s:
    raise SystemExit('expressao de distancia base esperada nao encontrada')
s=s.replace(old,new,1)
s=s.replace("const VERSION='1.0.44-cloudflare';","const VERSION='1.0.45-cloudflare';",1)
p.write_text(s,encoding='utf-8')
print('Mapa base: usa plannedDistanceKm quando disponivel e restante da telemetria como fallback.')
