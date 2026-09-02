"""Build the local variant from the same assembled, tested production API."""
import pathlib, re, shutil, subprocess, sys
root = pathlib.Path(__file__).resolve().parents[1]
out = pathlib.Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'server-local'/'runtime'
shutil.copytree(root/'cloudflare-central', out, dirs_exist_ok=True)
workflow=(root/'.github/workflows/deploy-gat-api.yml').read_text()
for script in re.findall(r'python (scripts/apply-[^\s]+)', workflow):
    subprocess.run([sys.executable,script],cwd=out,check=True)
subprocess.run(['node','scripts/apply-suspended-job-fix.mjs'],cwd=out,check=True)
worker=(out/'worker.js').read_text()

# MODO DE TESTE TEMPORARIO DO PROPRIETARIO.
# Somente @biduzao precisa selecionar um Trabalho GAT; todas as demais regras de
# validacao da viagem ficam desativadas para permitir testes rapidos/camera zero.
# Todos os outros motoristas continuam exatamente nas regras oficiais.
mission_anchor="const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();if(!row?.current_mission_json)return null;let m;try{m=JSON.parse(row.current_mission_json)}catch{return null}"
mission_new=mission_anchor+"\n const adminTest=clean(user)==='biduzao';"
if mission_anchor not in worker:
    raise RuntimeError('Nao encontrei a abertura da missao para ativar o modo admin.')
worker=worker.replace(mission_anchor,mission_new,1)

old_min="const minKm=Math.max(1,Number(m.min_km)||MIN_KM);"
new_min="const minKm=adminTest?0:Math.max(1,Number(m.min_km)||MIN_KM);"
if old_min not in worker:
    raise RuntimeError('Nao encontrei a regra de distancia minima da missao para aplicar o teste @biduzao.')
worker=worker.replace(old_min,new_min,1)

# Trabalho repetido continua contando normalmente durante o teste do proprietario.
repeat_old="const repeatXpOnly=!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first());const custom="
repeat_new="const repeatXpOnly=clean(s.user)==='biduzao'?false:!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first());const custom="
if repeat_old not in worker:
    raise RuntimeError('Nao encontrei a regra XP-only de trabalho repetido.')
worker=worker.replace(repeat_old,repeat_new,1)

work_already_old="workAlreadyCompleted=Boolean(m.xp_only)||!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first())"
work_already_new="workAlreadyCompleted=adminTest?false:(Boolean(m.xp_only)||!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first()))"
if work_already_old not in worker:
    raise RuntimeError('Nao encontrei a deteccao de trabalho ja concluido.')
worker=worker.replace(work_already_old,work_already_new,1)

route_guard_old="if(!workAlreadyCompleted&&await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first())"
route_guard_new="if(!adminTest&&!workAlreadyCompleted&&await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first())"
if route_guard_old not in worker:
    raise RuntimeError('Nao encontrei o bloqueio de rota repetida.')
worker=worker.replace(route_guard_old,route_guard_new,1)

progress_old="if(delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};"
progress_new="if(!adminTest&&delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};"
if progress_old not in worker:
    raise RuntimeError('Nao encontrei a regra de progresso minimo da viagem.')
worker=worker.replace(progress_old,progress_new,1)

rank_old="if(m.rank_guard?.reason||!m.rank_guard){"
rank_new="if(!adminTest&&(m.rank_guard?.reason||!m.rank_guard)){"
if rank_old not in worker:
    raise RuntimeError('Nao encontrei a trava de verificacao continua da telemetria.')
worker=worker.replace(rank_old,rank_new,1)

state_old="if(m.state!=='active')return{type:'delivery_rejected',reason:'mission_not_active'};"
state_new="if(!adminTest&&m.state!=='active')return{type:'delivery_rejected',reason:'mission_not_active'};"
if state_old not in worker:
    raise RuntimeError('Nao encontrei a trava de missao ativa.')
worker=worker.replace(state_old,state_new,1)

old_delivery="if(distance<MIN_KM){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:MIN_KM}}"
new_delivery="if(!adminTest&&distance<minKm){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:minKm}}"
if old_delivery in worker:
    worker=worker.replace(old_delivery,new_delivery,1)
elif "if(distance<minKm)" in worker:
    worker=worker.replace("if(distance<minKm)","if(!adminTest&&distance<minKm)",1)
else:
    raise RuntimeError('Nao encontrei a validacao final de distancia para aplicar o teste @biduzao.')

# Repetir trabalho/rota durante o teste nao pode quebrar a transacao por chave unica.
worker=worker.replace("INSERT INTO work_completed(user,work_id,month_key,completed_at)","INSERT OR IGNORE INTO work_completed(user,work_id,month_key,completed_at)")
worker=worker.replace("INSERT INTO routes_completed(user,month_key,route_key,source,destination,completed_at)","INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at)")

# TruckSim GPS pode manter gameplay.jobCancelled=true mesmo apos uma entrega real.
# A prova confiavel e a mudanca de jobDeliveredDetails entre o inicio e o fim da viagem.
# Assim clientes 1.0.30 atuais continuam validos sem exigir nova instalacao imediata.
activation_old="trip_progress_confirmed:false,rank_guard:{reason:rankingReadiness(raw).reason},started_at:t};"
activation_new="trip_progress_confirmed:false,rank_guard:{reason:adminTest?null:rankingReadiness(raw).reason},delivery_details_start:JSON.stringify(pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{}),started_at:t};"
if activation_old not in worker:
    raise RuntimeError('Nao encontrei o ponto de ativacao da missao para salvar o recibo inicial da entrega.')
worker=worker.replace(activation_old,activation_new,1)

event_old=""" const gatJobEvent=clean(str(raw,'gat_job_event','gatJobEvent'));
 const delivered=gatJobEvent==='delivered'||(!hasLoadedJob&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
 const cancelled=gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled'));"""
event_new=""" const gatJobEvent=clean(str(raw,'gat_job_event','gatJobEvent'));
 const deliveryDetails=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},deliveryDetailsNow=JSON.stringify(deliveryDetails),deliveryDetailsStart=String(m.delivery_details_start||'');
 const deliveryDetailsPositive=num(deliveryDetails,'distanceKm','distance_km')>0||num(deliveryDetails,'revenue')>0||num(deliveryDetails,'earnedXp','earned_xp')>0;
 const deliveryDetailsChanged=!hasLoadedJob&&deliveryDetailsStart&&deliveryDetailsNow!==deliveryDetailsStart&&deliveryDetailsPositive;
 const legacyDeliveryFallback=!hasLoadedJob&&!deliveryDetailsStart&&(adminTest||m.trip_progress_confirmed===true)&&teleKm>=0&&teleKm<=2&&deliveryDetailsPositive;
 const delivered=gatJobEvent==='delivered'||deliveryDetailsChanged||legacyDeliveryFallback||(!hasLoadedJob&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
 const cancelled=!delivered&&(gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));"""
if event_old not in worker:
    raise RuntimeError('Nao encontrei a classificacao entregue/cancelada para aplicar o hotfix.')
worker=worker.replace(event_old,event_new,1)

# O endpoint precisa reconhecer a entrega ANTES de validar os sete danos, para poder
# restaurar cargo/reboque do ultimo pacote carregado. Isso corrige exatamente o pacote
# observado: jobCancelled=true, jobDelivered=false, mas jobDeliveredDetails novo e valido.
endpoint_old="""   const event=clean(str(raw,'gat_job_event','gatJobEvent'));
   const delivered=event==='delivered'||(!loaded&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
   const cancelled=event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled'));
   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);"""
endpoint_new="""   const event=clean(str(raw,'gat_job_event','gatJobEvent'));
   const deliveryDetailsRaw=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},previousDeliveryDetailsRaw=pick(prevRaw||{},'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{};
   const deliveryDetailsPositive=num(deliveryDetailsRaw,'distanceKm','distance_km')>0||num(deliveryDetailsRaw,'revenue')>0||num(deliveryDetailsRaw,'earnedXp','earned_xp')>0;
   const deliveryDetailsChanged=!loaded&&prevRaw&&JSON.stringify(deliveryDetailsRaw)!==JSON.stringify(previousDeliveryDetailsRaw)&&deliveryDetailsPositive;
   const delivered=event==='delivered'||deliveryDetailsChanged||(!loaded&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
   const cancelled=!delivered&&(event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled')));
   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);"""
if endpoint_old not in worker:
    raise RuntimeError('Nao encontrei a classificacao do endpoint de telemetria para restaurar os danos finais.')
worker=worker.replace(endpoint_old,endpoint_new,1)

# Nenhuma multa/dano desconta Pontos GAT durante o teste do proprietario.
worker=worker.replace("gatSpeedPenalty=fines*3,gatCargoPenalty=scoreTier(damage,3,7,15),gatTruckPenalty=scoreTier(truckDamage,5,10,20)","gatSpeedPenalty=adminTest?0:fines*3,gatCargoPenalty=adminTest?0:scoreTier(damage,3,7,15),gatTruckPenalty=adminTest?0:scoreTier(truckDamage,5,10,20)",1)
worker=worker.replace("perfect=damage<=0.5&&truckDamage<=0.5&&fines===0?1:0","perfect=adminTest?1:(damage<=0.5&&truckDamage<=0.5&&fines===0?1:0)",1)

# Marca claramente os registros de teste para podermos limpar depois sem tocar nos demais.
worker=worker.replace("rank_verified:true,rank_client_version:raw.gat_client_version","rank_verified:!adminTest,admin_test_mode:adminTest,rank_client_version:raw.gat_client_version",1)

# Status ao vivo do proprietario fica verde durante o modo de teste, mesmo sem os sete danos.
worker=worker.replace("rank_status:rankingReadiness(raw),telemetry:raw","rank_status:clean(account)==='biduzao'?{eligible:true,reason:null,admin_test_mode:true}:rankingReadiness(raw),telemetry:raw",1)
worker=worker.replace("const readiness=rankingReadiness(raw);","const readiness=clean(account)==='biduzao'?{eligible:true,reason:null,admin_test_mode:true}:rankingReadiness(raw);",1)

# A resposta de selecao informa ao site que somente este perfil esta em modo de teste.
selection_old="return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),xp_only:repeatXpOnly,rules_enabled:true,operation_mode:'official'})"
selection_new="return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),xp_only:repeatXpOnly,rules_enabled:clean(s.user)!=='biduzao',admin_test_mode:clean(s.user)==='biduzao',operation_mode:'official'})"
if selection_old not in worker:
    raise RuntimeError('Nao encontrei o retorno da selecao do trabalho.')
worker=worker.replace(selection_old,selection_new,1)

reset_old="'trip_progress_confirmed','rank_guard']"
reset_new="'trip_progress_confirmed','rank_guard','delivery_details_start']"
if reset_old not in worker:
    raise RuntimeError('Nao encontrei a limpeza da tentativa para remover o recibo inicial.')
worker=worker.replace(reset_old,reset_new,1)

worker=re.sub(r"import .* from '@noble/[^\n]+\n",'',worker)
worker="""import {createHash,pbkdf2Sync} from 'node:crypto';
const sha256=x=>createHash('sha256').update(x).digest();
const bytesToHex=x=>Buffer.from(x).toString('hex');
const pbkdf2=(_,password,salt,options)=>pbkdf2Sync(password,salt,options.c,options.dkLen,'sha256');
"""+worker
assert "const VERSION='1.0.52-cloudflare'" in worker
assert "const adminTest=clean(user)==='biduzao'" in worker
assert "const minKm=adminTest?0" in worker
assert "!adminTest&&delivered&&m.state==='active'" in worker
assert "if(!adminTest&&(m.rank_guard?.reason||!m.rank_guard))" in worker
assert "admin_test_mode:adminTest" in worker
assert worker.count("deliveryDetailsChanged")>=2 and "cancelled=!delivered" in worker
worker=worker.replace("const VERSION='1.0.52-cloudflare'","const VERSION='1.0.40-local'").replace("service:'GAT Central Cloud'","service:'GAT Central Local'")
(out/'worker.js').write_text(worker)
# Local ranking hotfix is copied from cloudflare-central/ranking-telemetry.js and
# validated by the same production contract tests before packaging.
# This file exists only in the Windows local build. The Cloudflare source and
# its fail-closed quota protection are never edited or bypassed at runtime.
(out/'budget-guard.js').write_text("export function budgetState(){return {paused:false,reason:null,resumes_at:null,storage:'local-sqlite'};}\n")
for name in ['host.mjs','database.mjs']:
    shutil.copy2(root/'server-local'/name,out/name)
print('Local API assembled:',out)
