from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.23"' in c:
    c=c.replace('InternalVersion = "1.0.23"','InternalVersion = "1.0.24"',1)
elif 'InternalVersion = "1.0.24"' not in c:
    raise SystemExit('versao 1.0.23 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# MODO TEMPORARIO DE TESTE SEM REGRAS:
# O botao PEGAR TRABALHO continua criando a missao.
# Qualquer trabalho detectado pela telemetria pode iniciar a missao:
# sem limite minimo de km, sem faixa de peso e sem exigir World of Trucks.
# A conclusao continua usando a transicao real de trabalho ativo -> finalizado,
# preservando o teste do fluxo de entrega, 1/40 e +100 XP.
s=s.replace('p.CurrentMission.MinKm = 100', 'p.CurrentMission.MinKm = 0', 1)
s=s.replace('p.CurrentMission.Market = "test_any"', 'p.CurrentMission.Market = "test_no_rules"', 1)
s=s.replace('p.CurrentMission.MaxWeightKg = 1000000', 'p.CurrentMission.MaxWeightKg = 1000000000', 1)

old='Market: "test_any", MinKm: 100, MinWeightKg: 0, MaxWeightKg: 1000000'
new='Market: "test_no_rules", MinKm: 0, MinWeightKg: 0, MaxWeightKg: 1000000000'
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('criacao da missao teste 1.0.23 nao encontrada')

old='validation := map[string]any{"on_job": onJob, "world_of_trucks": true, "test_mode": true, "distance_ok": false, "weight_ok": false, "market": market, "distance_km": km, "weight_kg": mass}'
new='validation := map[string]any{"on_job": onJob, "world_of_trucks": gatIsWorldOfTrucks(market), "test_mode": true, "rules_disabled": true, "distance_ok": true, "weight_ok": true, "market": market, "distance_km": km, "weight_kg": mass}'
if old in s:
    s=s.replace(old,new,1)
elif '"rules_disabled": true' not in s:
    raise SystemExit('mapa de validacao do teste nao encontrado')

old='validation["distance_ok"] = km >= m.MinKm; validation["weight_ok"] = mass > 0'
new='validation["distance_ok"] = true; validation["weight_ok"] = true'
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('validacao de distancia/peso do teste nao encontrada')

old='if m.State == "assigned" && onJob && km >= m.MinKm && mass > 0 {'
new='if m.State == "assigned" && onJob {'
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('condicao de inicio da missao teste nao encontrada')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.24: botao PEGAR TRABALHO mantido e regras temporariamente desativadas')
