from pathlib import Path

p=Path('server-local/runtime/worker.js')
s=p.read_text(encoding='utf-8')

replacements=[
    ("const adminTest=clean(user)==='biduzao';","const adminTest=false;"),
    ("const repeatXpOnly=clean(s.user)==='biduzao'?false:!!(","const repeatXpOnly=!!("),
    ("if(pr?.current_mission_json&&clean(s.user)!=='biduzao')throw new HttpError(409,'mission_already_active');","if(pr?.current_mission_json)throw new HttpError(409,'mission_already_active');"),
    ("rank_status:clean(account)==='biduzao'?{eligible:true,reason:null,admin_test_mode:true}:rankingReadiness(raw),telemetry:raw","rank_status:rankingReadiness(raw),telemetry:raw"),
    ("const readiness=clean(account)==='biduzao'?{eligible:true,reason:null,admin_test_mode:true}:rankingReadiness(raw);","const readiness=rankingReadiness(raw);"),
    ("rules_enabled:clean(s.user)!=='biduzao',admin_test_mode:clean(s.user)==='biduzao'","rules_enabled:true,admin_test_mode:false"),
    ("const VERSION='1.0.40-local'","const VERSION='1.0.41-local'")
]

for old,new in replacements:
    if old not in s:
        raise SystemExit('Normalizacao de go-live nao encontrou: '+old[:90])
    s=s.replace(old,new,1)

# As condicionais que usam adminTest podem permanecer: agora ele e sempre false,
# portanto distancia, danos, ranking, repeticao e estado usam as regras oficiais.
for forbidden in [
    "clean(user)==='biduzao'",
    "clean(s.user)==='biduzao'?false",
    "clean(s.user)!=='biduzao'",
    "clean(account)==='biduzao'?{eligible:true",
    "admin_test_mode:clean(s.user)==='biduzao'"
]:
    if forbidden in s:
        raise SystemExit('Ainda existe excecao temporaria do proprietario: '+forbidden)

if "const adminTest=false;" not in s or "const VERSION='1.0.41-local'" not in s:
    raise SystemExit('Central normal nao foi preparada corretamente.')

p.write_text(s,encoding='utf-8')
print('Go-live: @biduzao voltou para as mesmas regras oficiais dos demais motoristas.')
