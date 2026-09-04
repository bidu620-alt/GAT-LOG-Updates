from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
rank_path=root/'ranking-telemetry.js'
schema_path=root/'schema.sql'
worker=worker_path.read_text(encoding='utf-8')
rank=rank_path.read_text(encoding='utf-8')
schema=schema_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# ---------------------------------------------------------------------------
# GAT Server 1.0.54
# - a Central registra recibo idempotente por pacote;
# - mantem checkpoints recuperaveis da viagem;
# - valida a cadeia assinada gerada pelo GAT Telemetria 1.0.32;
# - pacote local adulterado nunca ganha Pontos GAT, mas a viagem continua historica.
# ---------------------------------------------------------------------------
worker=once(worker,"const VERSION='1.0.53-local';","const VERSION='1.0.54-local';",'versao 1.0.53')
worker=once(worker,"import {createHash,pbkdf2Sync} from 'node:crypto';","import {createHash,createHmac,pbkdf2Sync} from 'node:crypto';",'import node:crypto local')

schema_add=r'''

-- GAT 1.0.54: recibos idempotentes, cadeia local e caixa-preta recuperavel.
CREATE TABLE IF NOT EXISTS telemetry_packet_receipts (
  user TEXT NOT NULL,
  packet_id TEXT NOT NULL,
  device_id TEXT NOT NULL DEFAULT '',
  trip_id TEXT NOT NULL DEFAULT '',
  collected_at TEXT,
  received_at TEXT NOT NULL,
  journal_seq INTEGER,
  journal_chain TEXT,
  journal_verified INTEGER NOT NULL DEFAULT 0,
  journal_reason TEXT,
  mission_event_json TEXT,
  PRIMARY KEY(user,packet_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_packet_receipts_trip ON telemetry_packet_receipts(user,trip_id,received_at);

CREATE TABLE IF NOT EXISTS telemetry_journal_state (
  user TEXT NOT NULL,
  device_id TEXT NOT NULL,
  last_seq INTEGER NOT NULL,
  last_chain TEXT NOT NULL,
  last_packet_id TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY(user,device_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS trip_checkpoints (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user TEXT NOT NULL,
  trip_id TEXT NOT NULL,
  packet_id TEXT NOT NULL,
  phase TEXT NOT NULL,
  collected_at TEXT,
  received_at TEXT NOT NULL,
  journal_verified INTEGER NOT NULL DEFAULT 0,
  summary_json TEXT NOT NULL,
  raw_json TEXT NOT NULL,
  UNIQUE(user,packet_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_trip_checkpoints_trip ON trip_checkpoints(user,trip_id,received_at);
'''
if 'CREATE TABLE IF NOT EXISTS telemetry_packet_receipts' not in schema:
    schema += schema_add

helpers=r'''
function journalPacketId(raw){return String(raw?.gat_packet_id||'').trim()}
function journalTripId(raw){return String(raw?.gat_trip_id||raw?.job_latch_key||'').trim()}
function journalPayloadHash(raw){
 const copy={...raw};for(const key of ['gat_journal_seq','gat_journal_prev','gat_journal_chain','gat_journal_payload_sha256','gat_journal_version','gat_journal_verified','gat_journal_invalid'])delete copy[key];
 return createHash('sha256').update(JSON.stringify(copy)).digest('hex');
}
function fixedHexEqual(a,b){
 a=String(a||'').toLowerCase();b=String(b||'').toLowerCase();if(a.length!==b.length||!a.length)return false;let diff=0;for(let i=0;i<a.length;i++)diff|=a.charCodeAt(i)^b.charCodeAt(i);return diff===0;
}
function journalMacKey(token,device){return createHash('sha256').update(`GAT-JOURNAL-V1|${String(token||'')}|${String(device||'')}`).digest()}
async function inspectClientPacket(env,user,device,token,raw,t){
 const packetId=journalPacketId(raw);if(!packetId)return{packet_id:'',duplicate:false,journal_present:false,journal_verified:false,journal_reason:'legacy_no_packet_id'};
 const prior=await env.DB.prepare('SELECT mission_event_json,journal_verified,journal_reason FROM telemetry_packet_receipts WHERE user=? AND packet_id=?').bind(user,packetId).first();
 if(prior)return{packet_id:packetId,duplicate:true,journal_present:true,journal_verified:!!prior.journal_verified,journal_reason:prior.journal_reason||null,mission_event_json:prior.mission_event_json||null};
 const seq=Math.trunc(Number(raw?.gat_journal_seq)||0),prev=String(raw?.gat_journal_prev||''),chain=String(raw?.gat_journal_chain||'').toLowerCase(),claimedPayload=String(raw?.gat_journal_payload_sha256||'').toLowerCase(),collected=String(raw?.gat_collected_at||''),tripId=journalTripId(raw);
 const present=seq>0&&chain&&claimedPayload&&String(raw?.gat_journal_version||'')==='1';
 if(!present)return{packet_id:packetId,duplicate:false,journal_present:false,journal_verified:false,journal_reason:'legacy_unsigned_packet',seq:null,chain:null,trip_id:tripId,collected_at:collected};
 const actualPayload=journalPayloadHash(raw);
 if(!fixedHexEqual(actualPayload,claimedPayload))return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:false,journal_reason:'journal_payload_changed',seq,chain,trip_id:tripId,collected_at:collected};
 const canonical=`${packetId}|${collected}|${tripId}|${seq}|${prev}|${claimedPayload}`;
 const expected=createHmac('sha256',journalMacKey(token,device)).update(canonical).digest('hex');
 if(!fixedHexEqual(expected,chain))return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:false,journal_reason:'journal_signature_invalid',seq,chain,trip_id:tripId,collected_at:collected};
 const state=await env.DB.prepare('SELECT last_seq,last_chain,last_packet_id FROM telemetry_journal_state WHERE user=? AND device_id=?').bind(user,device).first();
 const continuous=!state||(seq===Number(state.last_seq)+1&&prev===String(state.last_chain||''));
 return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:continuous,journal_reason:continuous?null:'journal_chain_gap',seq,chain,prev,trip_id:tripId,collected_at:collected};
}
async function persistClientPacket(env,user,device,raw,t,packetState,missionEvent,openJourneyState,phase){
 const packetId=String(packetState?.packet_id||journalPacketId(raw));if(!packetId)return;
 const tripId=String(openJourneyState?.canonical||packetState?.trip_id||journalTripId(raw)||'').trim();
 if(packetState?.journal_verified){
   await env.DB.prepare("INSERT INTO telemetry_journal_state(user,device_id,last_seq,last_chain,last_packet_id,updated_at) VALUES(?,?,?,?,?,?) ON CONFLICT(user,device_id) DO UPDATE SET last_seq=excluded.last_seq,last_chain=excluded.last_chain,last_packet_id=excluded.last_packet_id,updated_at=excluded.updated_at").bind(user,device,Number(packetState.seq)||0,String(packetState.chain||''),packetId,t).run();
 }
 await env.DB.prepare('INSERT INTO telemetry_packet_receipts(user,packet_id,device_id,trip_id,collected_at,received_at,journal_seq,journal_chain,journal_verified,journal_reason,mission_event_json) VALUES(?,?,?,?,?,?,?,?,?,?,?)').bind(user,packetId,device,tripId,String(packetState?.collected_at||raw?.gat_collected_at||''),t,packetState?.seq??null,packetState?.chain||null,packetState?.journal_verified?1:0,packetState?.journal_reason||null,missionEvent?JSON.stringify(missionEvent):null).run();
 if(!tripId)return;
 const terminal=phase==='delivered'||phase==='cancelled',last=await env.DB.prepare('SELECT received_at FROM trip_checkpoints WHERE user=? AND trip_id=? ORDER BY id DESC LIMIT 1').bind(user,tripId).first(),age=last?Date.parse(t)-Date.parse(last.received_at):Infinity;
 if(!terminal&&Number.isFinite(age)&&age<120000)return;
 const f=flat(user,user,t,raw),summary={phase,cargo:f.cargo_name,source:f.source_city,destination:f.destination_city,weight_kg:f.mass_kg,remaining_km:f.remaining_km,speed_kmh:f.speed_kmh,game_name:str(raw,'gat_game','game.gameName','gameName'),map_mode:f.gat_map,cargo_damage_pct:num(raw,'cargo_damage_pct'),truck_engine_damage_pct:num(raw,'truck_engine_damage_pct'),truck_transmission_damage_pct:num(raw,'truck_transmission_damage_pct'),truck_cabin_damage_pct:num(raw,'truck_cabin_damage_pct'),truck_chassis_damage_pct:num(raw,'truck_chassis_damage_pct'),truck_wheels_damage_pct:num(raw,'truck_wheels_damage_pct'),trailer_damage_pct:num(raw,'trailer_damage_pct'),journal_verified:!!packetState?.journal_verified,journal_reason:packetState?.journal_reason||null};
 await env.DB.prepare('INSERT OR IGNORE INTO trip_checkpoints(user,trip_id,packet_id,phase,collected_at,received_at,journal_verified,summary_json,raw_json) VALUES(?,?,?,?,?,?,?,?,?)').bind(user,tripId,packetId,phase,String(packetState?.collected_at||raw?.gat_collected_at||''),t,packetState?.journal_verified?1:0,JSON.stringify(summary),JSON.stringify(raw)).run();
}
'''
worker=once(worker,'function journeyGame(raw){',helpers+'\nfunction journeyGame(raw){','helpers de viagem 1.0.51')

old_call="""   const openJourneyState=await prepareOpenJourney(env,account,raw,t,loaded,delivered||cancelled);
   let missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);
"""
new_call="""   const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);
   if(packetState.journal_present&&!packetState.journal_verified)raw.gat_journal_invalid=true;
   let openJourneyState=null,missionEvent=null;
   if(packetState.duplicate){try{missionEvent=packetState.mission_event_json?JSON.parse(packetState.mission_event_json):null}catch{missionEvent=null}}
   else{
     openJourneyState=await prepareOpenJourney(env,account,raw,t,loaded,delivered||cancelled);
     missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);
"""
worker=once(worker,old_call,new_call,'inicio idempotente do endpoint')
old_finish="""   await finishOpenJourney(env,account,raw,t,openJourneyState,missionEvent);
   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);"""
new_finish="""     await finishOpenJourney(env,account,raw,t,openJourneyState,missionEvent);
     const checkpointPhase=delivered?'delivered':cancelled?'cancelled':loaded?'progress':'idle';
     await persistClientPacket(env,account,device,raw,t,packetState,missionEvent,openJourneyState,checkpointPhase);
   }
   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);"""
worker=once(worker,old_finish,new_finish,'persistencia de recibo/checkpoint')

# Ranking: um pacote assinado que teve payload/cadeia alterados nunca recebe pontos.
rank=once(rank,
"  const versionOK = versionAtLeast(raw.gat_client_version);\n  const connected = raw.game?.connected === true;\n  const reason = !versionOK ? 'client_update_required' : !connected ? 'telemetry_disconnected' : missing.length ? 'damage_data_incomplete' : null;",
"  const versionOK = versionAtLeast(raw.gat_client_version);\n  const connected = raw.game?.connected === true;\n  const journalInvalid = raw.gat_journal_invalid === true;\n  const reason = journalInvalid ? 'local_journal_invalid' : !versionOK ? 'client_update_required' : !connected ? 'telemetry_disconnected' : missing.length ? 'damage_data_incomplete' : null;",
'validacao journal no ranking')
rank=once(rank,
"  if (reason === 'damage_data_incomplete') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: faltaram dados de danos.';",
"  if (reason === 'damage_data_incomplete') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: faltaram dados de danos.';\n  if (reason === 'local_journal_invalid') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: a caixa-preta local nao passou na verificacao de integridade.';",
'mensagem journal invalido')

for marker in [
 "const VERSION='1.0.54-local'",
 'CREATE TABLE IF NOT EXISTS telemetry_packet_receipts',
 'CREATE TABLE IF NOT EXISTS telemetry_journal_state',
 'CREATE TABLE IF NOT EXISTS trip_checkpoints',
 'inspectClientPacket', 'persistClientPacket', 'journal_signature_invalid',
 'journal_chain_gap', 'gat_journal_invalid', 'local_journal_invalid',
 "import {createHash,createHmac,pbkdf2Sync} from 'node:crypto';"
]:
    body=worker+'\n'+rank+'\n'+schema
    if marker not in body:raise SystemExit('Patch 1.0.54 incompleto: '+marker)

worker_path.write_text(worker,encoding='utf-8')
rank_path.write_text(rank,encoding='utf-8')
schema_path.write_text(schema,encoding='utf-8')
print('GAT Server 1.0.54: recibos idempotentes + checkpoints + journal assinado.')
