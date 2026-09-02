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
    ("const VERSION='1.0.40-local'","const VERSION='1.0.44-local'")
]

for old,new in replacements:
    if old not in s:
        raise SystemExit('Normalizacao de go-live nao encontrou: '+old[:90])
    s=s.replace(old,new,1)

# Ranking completo sem distancia minima durante os testes oficiais.
# Continua sendo obrigatorio haver uma viagem real, telemetria valida e pelo menos
# 1 km de progresso observado pelo guarda anti-entrega-instantanea.
if "const MIN_KM=500;" not in s:
    raise SystemExit('Nao encontrei MIN_KM=500 para desativar a distancia minima.')
s=s.replace("const MIN_KM=500;","const MIN_KM=0;",1)

old_min="const minKm=adminTest?0:Math.max(1,Number(m.min_km)||MIN_KM);"
if old_min in s:
    s=s.replace(old_min,"const minKm=0;",1)
elif "const minKm=adminTest?0:" in s:
    raise SystemExit('Expressao minKm mudou; revise antes de publicar.')

for forbidden in [
    "clean(user)==='biduzao'",
    "clean(s.user)==='biduzao'?false",
    "clean(s.user)!=='biduzao'",
    "clean(account)==='biduzao'?{eligible:true",
    "admin_test_mode:clean(s.user)==='biduzao'"
]:
    if forbidden in s:
        raise SystemExit('Ainda existe excecao temporaria do proprietario: '+forbidden)

required=["const adminTest=false;","const VERSION='1.0.44-local'","const MIN_KM=0;"]
for marker in required:
    if marker not in s:
        raise SystemExit('Central 1.0.44 nao foi preparada corretamente: '+marker)

p.write_text(s,encoding='utf-8')
print('Go-live 1.0.44: sem minimo de km e com verificacao continua da telemetria corrigida.')
