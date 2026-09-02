"""Instala o protocolo de viagem v2 na Central local.

Contrato:
- GAT Telemetria envia observacoes brutas + gat_trip_id.
- Central GAT e a unica autoridade para concluir/cancelar.
- Site le somente o resultado persistido pela Central.
"""
from pathlib import Path
import sys

path = Path(sys.argv[1])
worker = path.read_text(encoding='utf-8')

activation_old = "delivery_details_start:JSON.stringify(pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{}),started_at:t};"
activation_new = "delivery_details_start:JSON.stringify(pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{}),trip_id:String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||''),started_at:t};"
if activation_old not in worker:
    raise RuntimeError('Nao encontrei ativacao da missao para gravar trip_id.')
worker = worker.replace(activation_old, activation_new, 1)

event_anchor = " const gatJobEvent=clean(str(raw,'gat_job_event','gatJobEvent'));\n"
event_extra = " const packetTripId=String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||''),missionTripId=String(m.trip_id||m.job_latch_key||''),tripReplaced=!!(hasLoadedJob&&packetTripId&&missionTripId&&packetTripId!==missionTripId),observedIdle=clean(str(raw,'gat_job_state','gatJobState'))==='idle'&&!hasLoadedJob;\n"
if event_anchor not in worker:
    raise RuntimeError('Nao encontrei leitura do evento GAT em processMission.')
worker = worker.replace(event_anchor, event_anchor + event_extra, 1)

old_changed = "const deliveryDetailsChanged=!hasLoadedJob&&deliveryDetailsStart&&deliveryDetailsNow!==deliveryDetailsStart&&deliveryDetailsPositive;"
new_changed = "const deliveryDetailsChanged=(!hasLoadedJob||tripReplaced)&&deliveryDetailsStart&&deliveryDetailsNow!==deliveryDetailsStart&&deliveryDetailsPositive;"
if old_changed not in worker:
    raise RuntimeError('Nao encontrei comparacao do recibo final.')
worker = worker.replace(old_changed, new_changed, 1)

old_cancelled = "const cancelled=!delivered&&(gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));"
new_cancelled = "const cancelled=!delivered&&(observedIdle||tripReplaced||gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));"
if old_cancelled not in worker:
    raise RuntimeError('Nao encontrei decisao de cancelamento da Central.')
worker = worker.replace(old_cancelled, new_cancelled, 1)

old_cancel_block = "if(!delivered&&cancelled&&!hasLoadedJob&&(m.state==='active'||m.state==='suspended')){m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}"
new_cancel_block = "if(!delivered&&cancelled&&(!hasLoadedJob||tripReplaced)&&(m.state==='active'||m.state==='suspended')){if(m.classification_mode==='automatic'||m.classification_mode==='pending'){await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(t,user).run();return{type:'mission_cancelled',reason:tripReplaced?'trip_replaced':'observed_job_end',trip_id:missionTripId||null,mission:null}}m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}"
if old_cancel_block not in worker:
    raise RuntimeError('Nao encontrei bloco de cancelamento/suspensao da missao.')
worker = worker.replace(old_cancel_block, new_cancel_block, 1)

import_old = "import {cachedRead} from './read-cache.js';"
import_new = "import {cachedRead,invalidateRead} from './read-cache.js';"
if import_old not in worker:
    raise RuntimeError('Nao encontrei import do read-cache.')
worker = worker.replace(import_old, import_new, 1)

mission_call = "   const missionEvent=await processMission(env,account,raw,t,previousSampleAt);\n"
mission_call_new = """   let missionEvent=await processMission(env,account,raw,t,previousSampleAt);
   // Se um novo trip_id chegou enquanto a missao anterior ainda estava ativa,
   // o mesmo pacote encerra a antiga e ja abre a nova. Nao esperamos 15 s nem
   // dependemos de um segundo evento do cliente.
   if(loaded&&missionEvent&&['mission_cancelled','delivery_completed','delivery_completed_pending_classification','delivery_completed_xp_only'].includes(String(missionEvent.type))){
     const firstEvent=missionEvent,nextEvent=await processMission(env,account,raw,t,previousSampleAt);
     if(nextEvent)missionEvent={...firstEvent,next_mission_event:nextEvent};
   }
   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);invalidateRead('get:/api/public/work/catalog:'+account);if(String(missionEvent.type).startsWith('delivery_completed')){invalidateRead('get:/api/public/ranking:');invalidateRead('get:/api/public/safety-ranking:')}}
"""
if mission_call not in worker:
    raise RuntimeError('Nao encontrei chamada processMission no endpoint de telemetria.')
worker = worker.replace(mission_call, mission_call_new, 1)

cache_path = path.with_name('read-cache.js')
cache = cache_path.read_text(encoding='utf-8')
if 'export function invalidateRead' not in cache:
    cache += """
export function invalidateRead(key) {
  const fullKey = `gat-1.0.52:${new Date().toISOString().slice(0, 7)}:${key}`;
  local.delete(fullKey);
  pending.delete(fullKey);
}
"""
    cache_path.write_text(cache, encoding='utf-8')

required = [
    "trip_id:String(str(raw,'gat_trip_id'",
    "tripReplaced=!!(hasLoadedJob",
    "observedIdle=clean(str(raw,'gat_job_state'",
    "reason:tripReplaced?'trip_replaced':'observed_job_end'",
    "next_mission_event:nextEvent",
    "invalidateRead('profile:'+account)",
    "import {cachedRead,invalidateRead}"
]
for item in required:
    if item not in worker:
        raise RuntimeError('Protocolo de viagem v2 incompleto: ' + item)
if 'export function invalidateRead' not in cache_path.read_text(encoding='utf-8'):
    raise RuntimeError('Invalidacao de read-model nao instalada.')

path.write_text(worker, encoding='utf-8')
print('Central GAT: protocolo job-v2, troca imediata de viagem e invalidacao de perfil instalados.')
