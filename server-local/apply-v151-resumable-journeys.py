from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
rank_path=root/'ranking-telemetry.js'
schema_path=root/'schema.sql'
host_path=root/'host.mjs'
worker=worker_path.read_text(encoding='utf-8')
rank=rank_path.read_text(encoding='utf-8')
schema=schema_path.read_text(encoding='utf-8')
host=host_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# ---------------------------------------------------------------------------
# 1.0.51 - a identidade da viagem pertence a Central, nao a sessao do app.
# ---------------------------------------------------------------------------
worker=once(worker,"const VERSION='1.0.50-local';","const VERSION='1.0.51-local';",'versao 1.0.50')

schema_add=r'''

-- GAT 1.0.51: viagens abertas sobrevivem a app/jogo/PC offline e troca de jogo/mapa.
CREATE TABLE IF NOT EXISTS open_trips (
  trip_id TEXT PRIMARY KEY,
  user TEXT NOT NULL,
  game_name TEXT NOT NULL DEFAULT '',
  context_key TEXT NOT NULL DEFAULT '',
  fingerprint TEXT NOT NULL,
  state TEXT NOT NULL DEFAULT 'suspended',
  mission_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  last_seen_at TEXT,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_open_trips_user_state ON open_trips(user,state,updated_at);
CREATE INDEX IF NOT EXISTS idx_open_trips_match ON open_trips(user,context_key,fingerprint,updated_at);

CREATE TABLE IF NOT EXISTS open_trip_aliases (
  observed_trip_id TEXT PRIMARY KEY,
  trip_id TEXT NOT NULL,
  user TEXT NOT NULL,
  first_seen_at TEXT NOT NULL,
  last_seen_at TEXT NOT NULL,
  FOREIGN KEY(trip_id) REFERENCES open_trips(trip_id) ON DELETE CASCADE,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_open_trip_alias_user ON open_trip_aliases(user,last_seen_at);
'''
if 'CREATE TABLE IF NOT EXISTS open_trips' not in schema:
    schema += schema_add

journey_helpers=r'''
function journeyGame(raw){return clean(str(raw,'gat_game','game.gameName','gameName'))||'unknown'}
function journeyMap(raw){return clean(str(raw,'gat_map','map_mode','gatMap'))||'base'}
function journeyContext(raw){const explicit=clean(str(raw,'gat_context_id','gatContextId','profile_context_id','profileContextId'));return explicit||`${journeyGame(raw)}|${journeyMap(raw)}`}
function journeyFingerprintRaw(raw){
 const cargo=clean(str(raw,'cargo_id','job.cargoId','Job.CargoId'))||norm(str(raw,'cargo_name','job.cargoName','job.cargo'));
 const source=clean(str(raw,'source_city_id','job.sourceCityId','Job.SourceCityId'))||norm(str(raw,'source_city','job.sourceCity','Job.SourceCity'));
 const destination=clean(str(raw,'destination_city_id','job.destinationCityId','Job.DestinationCityId'))||norm(str(raw,'destination_city','job.destinationCity','Job.DestinationCity'));
 const planned=Math.max(0,Math.round(num(raw,'planned_distance_km','job.plannedDistanceKm','Job.PlannedDistanceKm'))),mass=Math.max(0,Math.round(num(raw,'mass_kg','cargoMass','cargo_mass','job.cargoMass','Job.CargoMass')));
 return `${journeyContext(raw)}|${cargo}|${source}|${destination}|${planned}|${mass}`;
}
function journeyFingerprintMission(m,raw={}){
 const context=String(m?.context_key||'').trim()||`${clean(m?.game_name)||journeyGame(raw)}|${clean(m?.map_mode)||journeyMap(raw)}`;
 const cargo=clean(m?.cargo_id)||norm(m?.cargo||'');
 const source=clean(m?.source_city_id)||norm(m?.source||'');
 const destination=clean(m?.destination_city_id)||norm(m?.destination||'');
 const planned=Math.max(0,Math.round(Number(m?.planned_distance_km)||0)),mass=Math.max(0,Math.round(Number(m?.weight_kg)||0));
 return `${context}|${cargo}|${source}|${destination}|${planned}|${mass}`;
}
async function openJourneyAlias(env,user,observed,canonical,t){
 observed=String(observed||'').trim();canonical=String(canonical||'').trim();if(!observed||!canonical)return;
 await env.DB.prepare("INSERT INTO open_trip_aliases(observed_trip_id,trip_id,user,first_seen_at,last_seen_at) VALUES(?,?,?,?,?) ON CONFLICT(observed_trip_id) DO UPDATE SET trip_id=excluded.trip_id,user=excluded.user,last_seen_at=excluded.last_seen_at").bind(observed,canonical,user,t,t).run();
}
async function saveOpenJourney(env,user,m,raw,t,state='suspended'){
 if(!m)return null;const canonical=String(m.canonical_trip_id||m.trip_id||m.job_latch_key||m.id||'').trim();if(!canonical)return null;
 const game=clean(m.game_name)||journeyGame(raw),context=String(m.context_key||'').trim()||journeyContext(raw),fingerprint=String(m.journey_fingerprint||'').trim()||journeyFingerprintMission({...m,game_name:game,context_key:context},raw);
 m.canonical_trip_id=canonical;m.game_name=game;m.context_key=context;m.journey_fingerprint=fingerprint;
 await env.DB.prepare("INSERT INTO open_trips(trip_id,user,game_name,context_key,fingerprint,state,mission_json,created_at,updated_at,last_seen_at) VALUES(?,?,?,?,?,?,?,?,?,?) ON CONFLICT(trip_id) DO UPDATE SET game_name=excluded.game_name,context_key=excluded.context_key,fingerprint=excluded.fingerprint,state=excluded.state,mission_json=excluded.mission_json,updated_at=excluded.updated_at,last_seen_at=excluded.last_seen_at").bind(canonical,user,game,context,fingerprint,state,JSON.stringify(m),String(m.started_at||m.created_at||t),t,t).run();
 return canonical;
}
async function prepareOpenJourney(env,user,raw,t,loaded,terminal){
 const observed=String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||'').trim(),rawFp=loaded?journeyFingerprintRaw(raw):'',rawContext=loaded?journeyContext(raw):'';
 const pr=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let current=null;try{current=pr?.current_mission_json?JSON.parse(pr.current_mission_json):null}catch{}
 let canonical=current?String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim():'';
 if(current){
   if(!current.game_name)current.game_name=journeyGame(raw);if(!current.context_key)current.context_key=journeyContext(raw);if(!current.journey_fingerprint)current.journey_fingerprint=journeyFingerprintMission(current,raw);
   const same=loaded&&current.journey_fingerprint===rawFp;
   if(!terminal&&(!loaded||!same)){
     canonical=await saveOpenJourney(env,user,current,raw,t,'suspended')||canonical;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(t,user).run();current=null;canonical='';
   }else if(same){
     canonical=String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim();current.canonical_trip_id=canonical;
     if(observed){current.trip_id=observed;current.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}
     current.game_name=journeyGame(raw);current.context_key=rawContext;current.journey_fingerprint=rawFp;current.state='active';current.resumed_at=current.resumed_at||t;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(current),t,user).run();await saveOpenJourney(env,user,current,raw,t,'active');
     return{canonical,observed,fingerprint:rawFp,resumed:true};
   }
 }
 if(loaded&&!current){
   let saved=null;
   if(observed){const a=await env.DB.prepare('SELECT trip_id FROM open_trip_aliases WHERE observed_trip_id=? AND user=?').bind(observed,user).first();if(a?.trip_id)saved=await env.DB.prepare("SELECT * FROM open_trips WHERE trip_id=? AND user=? AND state IN ('active','suspended')").bind(a.trip_id,user).first()}
   if(!saved)saved=await env.DB.prepare("SELECT * FROM open_trips WHERE user=? AND context_key=? AND fingerprint=? AND state IN ('active','suspended') ORDER BY updated_at DESC LIMIT 1").bind(user,rawContext,rawFp).first();
   if(saved){let m=null;try{m=JSON.parse(saved.mission_json||'{}')}catch{}if(m){canonical=String(saved.trip_id);m.canonical_trip_id=canonical;m.game_name=journeyGame(raw);m.context_key=rawContext;m.journey_fingerprint=rawFp;m.state='active';m.resumed_at=t;if(observed){m.trip_id=observed;m.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();await saveOpenJourney(env,user,m,raw,t,'active');return{canonical,observed,fingerprint:rawFp,resumed:true}}}
 }
 return{canonical,observed,fingerprint:rawFp,resumed:false};
}
async function finishOpenJourney(env,user,raw,t,state,event){
 const completed=event&&['delivery_completed','delivery_completed_pending_classification','delivery_completed_xp_only'].includes(String(event.type)),cancelled=event&&String(event.type)==='mission_cancelled';
 let canonical=String(state?.canonical||'').trim();if(!canonical&&state?.observed){const a=await env.DB.prepare('SELECT trip_id FROM open_trip_aliases WHERE observed_trip_id=? AND user=?').bind(state.observed,user).first();canonical=String(a?.trip_id||'').trim()}
 if(completed||cancelled){if(canonical)await env.DB.prepare('DELETE FROM open_trips WHERE trip_id=? AND user=?').bind(canonical,user).run();return}
 const pr=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;try{m=pr?.current_mission_json?JSON.parse(pr.current_mission_json):null}catch{}if(!m)return;
 if(!m.game_name)m.game_name=journeyGame(raw);if(!m.context_key)m.context_key=journeyContext(raw);if(!m.journey_fingerprint)m.journey_fingerprint=journeyFingerprintMission(m,raw);
 canonical=await saveOpenJourney(env,user,m,raw,t,m.state==='active'?'active':'suspended')||canonical;if(state?.observed&&canonical)await openJourneyAlias(env,user,state.observed,canonical,t);
 await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
}
'''
process_anchor="async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){"
worker=once(worker,process_anchor,journey_helpers+'\n'+process_anchor,'assinatura processMission para viagens persistentes')

# Trocar jogo/perfil/mapa nao e cancelamento. Desligar o jogo tambem nao e cancelamento.
old_cancel="const cancelled=!delivered&&(observedIdle||tripReplaced||gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));"
new_cancel="const cancelled=!delivered&&raw?.game?.connected===true&&(gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));"
worker=once(worker,old_cancel,new_cancel,'cancelamento antigo por idle/troca de trip')
endpoint_cancel="const cancelled=!delivered&&(event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled')));"
endpoint_cancel_new="const cancelled=!delivered&&raw?.game?.connected===true&&(event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled')));"
worker=once(worker,endpoint_cancel,endpoint_cancel_new,'cancelamento do endpoint')

# Antes de processar, arquiva a viagem anterior ou restaura uma viagem conhecida.
call="   let missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);\n"
call_new="   const openJourneyState=await prepareOpenJourney(env,account,raw,t,loaded,delivered||cancelled);\n   let missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);\n"
worker=once(worker,call,call_new,'chamada processMission v1.48')
finalize_anchor="   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);"
finalize_new="   await finishOpenJourney(env,account,raw,t,openJourneyState,missionEvent);\n   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);"
worker=once(worker,finalize_anchor,finalize_new,'invalidacao apos processMission')

# ---------------------------------------------------------------------------
# Ranking: duas amostras completas no inicio continuam obrigatorias. Depois de
# validada, transicoes curtas do TruckSim nao zeram a viagem. Uma pausa longa entra
# em retomada e volta a pontuar apos duas amostras completas consecutivas.
# ---------------------------------------------------------------------------
rank=once(rank,"export const MAX_TELEMETRY_GAP_MS = 120000;","export const MAX_TELEMETRY_GAP_MS = 120000;\nexport const DAMAGE_TRANSIENT_GRACE_MS = 30000;\nexport const RESUME_VALID_SAMPLES = 2;",'constante de gap')
rank=once(rank,
"  if (reason === 'telemetry_gap') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: houve interrupção prolongada da telemetria.';",
"  if (reason === 'telemetry_gap') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: houve interrupção prolongada da telemetria.';\n  if (reason === 'telemetry_resume_pending') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: faltaram amostras válidas após a retomada da viagem.';",
'mensagem de retomada')
old_post="""  if (!next.reason) {
    if (!readiness.eligible) next.reason = readiness.reason;
    else if (!continuous) next.reason = 'telemetry_gap';
    next.last_sample_at = at;
  }
  return next;
}"""
new_post="""  // Uma missao ja verificada pode atravessar restart do app/jogo, queda de internet
  // ou pacote transitorio do TruckSim. Essas situacoes pedem nova prova, nao condenacao.
  if (next.verified_at && ['damage_data_incomplete','telemetry_disconnected','telemetry_gap'].includes(next.reason)) {
    next.reason = 'telemetry_resume_pending';
    next.resume_valid_samples = 0;
    next.resume_started_at = at;
    next.resume_trigger = 'migrated_'+String(next.last_invalid_reason || 'interruption');
  }
  if (next.reason === 'telemetry_resume_pending') {
    if (readiness.reason === 'client_update_required') { next.reason = 'client_update_required'; next.last_sample_at = at; return next; }
    const prior = Math.max(0, Number(next.resume_valid_samples) || 0);
    next.resume_valid_samples = readiness.eligible && (prior === 0 || continuous) ? prior + 1 : 0;
    next.last_sample_at = at;
    if (next.resume_valid_samples >= RESUME_VALID_SAMPLES) {
      next.reason = null;next.resumed_verified_at = at;delete next.resume_valid_samples;delete next.resume_started_at;delete next.resume_trigger;delete next.transient_invalid_started_at;delete next.transient_invalid_reason;
    }
    return next;
  }
  if (!next.reason) {
    if (!continuous) {
      next.reason = 'telemetry_resume_pending';next.resume_trigger = 'telemetry_gap';next.resume_started_at = at;next.resume_valid_samples = readiness.eligible ? 1 : 0;next.last_sample_at = at;return next;
    }
    if (!readiness.eligible) {
      if (readiness.reason === 'client_update_required') { next.reason = readiness.reason;next.last_sample_at = at;return next; }
      if (!next.transient_invalid_started_at) next.transient_invalid_started_at = at;
      next.transient_invalid_reason = readiness.reason;next.last_sample_at = at;
      const badSince=Date.parse(next.transient_invalid_started_at||at),badAge=Number.isFinite(badSince)&&Number.isFinite(currentTime)?currentTime-badSince:0;
      if (badAge > DAMAGE_TRANSIENT_GRACE_MS) { next.reason='telemetry_resume_pending';next.resume_trigger=readiness.reason;next.resume_started_at=at;next.resume_valid_samples=0; }
      return next;
    }
    delete next.transient_invalid_started_at;delete next.transient_invalid_reason;next.last_sample_at = at;
  }
  return next;
}"""
rank=once(rank,old_post,new_post,'regra sticky apos verificacao')

# ---------------------------------------------------------------------------
# Reparo exato da Pá-carregadeira do EduardoVidal. A propria Central preservou:
# rank verificado, prova pre-carga e os deltas de todos os danos. O 0 ocorreu porque
# um pacote de transicao final ficou incompleto. Pontuacao correta: 100 - 5 = 95.
# ---------------------------------------------------------------------------
repair=r'''
function repairEduardoVidalLoaderV151(db){
  const marker='repair_eduardovidal_loader_2026_09_03_v151',user='eduardovidal',trip='409c02f5f63748899760a64d6a10fffe';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(marker))return{changed:false,reason:'already_repaired'};
  const row=db.sql.prepare('SELECT id,raw_json FROM deliveries WHERE user=? AND raw_json LIKE ? ORDER BY id DESC LIMIT 1').get(user,'%'+trip+'%');
  if(!row)return{changed:false,reason:'delivery_missing'};let raw={};try{raw=JSON.parse(row.raw_json||'{}')}catch{return{changed:false,reason:'invalid_raw'}};
  const m=raw.mission||{},a=raw.audit||{},g=m.rank_guard||{};
  const proof=!!g.verified_at&&g.preflight_truck_damage_ready===true&&['truck_damage_start_pct','truck_damage_max_pct','truck_engine_damage_start_pct','truck_engine_damage_max_pct','truck_transmission_damage_start_pct','truck_transmission_damage_max_pct','truck_cabin_damage_start_pct','truck_cabin_damage_max_pct','truck_chassis_damage_start_pct','truck_chassis_damage_max_pct','truck_wheels_damage_start_pct','truck_wheels_damage_max_pct','trailer_damage_start_pct','trailer_damage_max_pct'].every(k=>Number.isFinite(Number(m[k])))&&Number.isFinite(Number(a.cargo_damage_pct));
  if(!proof||String(a.ranking_reason||'')!=='damage_data_incomplete'||Number(a.gat_points||0)!==0)return{changed:false,reason:'evidence_not_sufficient'};
  const points=Math.max(0,100-Math.max(0,Number(a.gat_speed_penalty_points)||0)-Math.max(0,Number(a.gat_cargo_penalty_points)||0)-Math.max(0,Number(a.gat_truck_penalty_points)||0));
  a.rank_verified=true;a.rank_eligible=true;a.ranking_eligible=true;a.ranking_reason=null;a.ranking_message='';a.gat_points=points;a.repair_reason='v151_transient_final_damage_packet';raw.audit=a;raw.repair={...(raw.repair||{}),v151_rank_recovered_at:new Date().toISOString(),reason:'transient_final_damage_packet',trip_id:trip};
  const t=new Date().toISOString();db.sql.exec('BEGIN IMMEDIATE');try{db.sql.prepare('UPDATE deliveries SET raw_json=? WHERE id=?').run(JSON.stringify(raw),row.id);db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(t,'system','repair_rank_points',user,JSON.stringify({delivery_id:Number(row.id),trip_id:trip,gat_points:points,reason:'transient_final_damage_packet'}));db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(marker,t);db.sql.exec('COMMIT');return{changed:true,delivery_id:Number(row.id),gat_points:points}}catch(e){try{db.sql.exec('ROLLBACK')}catch{}throw e}
}

'''
host=once(host,'async function main(){',repair+'async function main(){','main do host para reparo Eduardo')
host=once(host,"  reconcileMonthlyTripCount(db);\n  let lastError='';","  reconcileMonthlyTripCount(db);\n  const eduRepair=repairEduardoVidalLoaderV151(db);if(eduRepair.changed)console.log('GAT 1.0.51: Pontos GAT do EduardoVidal recuperados.',JSON.stringify(eduRepair));\n  let lastError='';",'startup para reparo Eduardo')

# Contratos finais.
for marker in ["const VERSION='1.0.51-local'",'prepareOpenJourney','finishOpenJourney','CREATE TABLE IF NOT EXISTS open_trips','telemetry_resume_pending','DAMAGE_TRANSIENT_GRACE_MS = 30000','repair_eduardovidal_loader_2026_09_03_v151','gat_points:points']:
    body=worker+'\n'+rank+'\n'+schema+'\n'+host
    if marker not in body:raise SystemExit('Patch 1.0.51 incompleto: '+marker)
if "observedIdle||tripReplaced||gatJobEvent==='cancelled'" in worker:raise SystemExit('Troca/idle ainda cancela viagem')

worker_path.write_text(worker,encoding='utf-8')
rank_path.write_text(rank,encoding='utf-8')
schema_path.write_text(schema,encoding='utf-8')
host_path.write_text(host,encoding='utf-8')
print('GAT Server 1.0.51: viagens persistentes + retomada de ranking + reparo EduardoVidal preparados.')
