from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker=root/'worker.js'
rank=root/'ranking-telemetry.js'
host=root/'host.mjs'

# 1) Prova de capacidade ANTES da carga. O pacote imediatamente anterior, ainda em
# idle, prova que o TruckSim ja enviava os cinco danos do caminhao antes da viagem.
# Assim um primeiro pacote de carga em transicao pode perder temporariamente todos os
# cinco aliases sem ser confundido com plugin antigo. Quem realmente iniciou a viagem
# com TruckSim antigo continua bloqueado, mesmo se atualizar o plugin no meio da rota.
w=worker.read_text(encoding='utf-8')
sig_old="async function processMission(env,user,raw,t,previousAt){"
sig_new="async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){"
if sig_old not in w:
    raise SystemExit('Nao encontrei assinatura processMission da Central 1.0.47.')
w=w.replace(sig_old,sig_new,1)

guard_old="rank_guard:adminTest?{reason:null,verified_at:t,last_sample_at:t,valid_samples:2}:{reason:'telemetry_not_verified_from_start',valid_samples:rankingReadiness(raw).eligible?1:0,startup_started_at:t,last_sample_at:t,last_invalid_reason:rankingReadiness(raw).reason||null},last_rejected_reason:undefined"
guard_new="rank_guard:adminTest?{reason:null,verified_at:t,last_sample_at:t,valid_samples:2,preflight_truck_damage_ready:true}:{reason:'telemetry_not_verified_from_start',valid_samples:rankingReadiness(raw).eligible?1:0,startup_started_at:t,last_sample_at:t,last_invalid_reason:rankingReadiness(raw).reason||null,preflight_truck_damage_ready:!!preflightTruckDamageReady},last_rejected_reason:undefined"
if guard_old not in w:
    raise SystemExit('Nao encontrei rank_guard inicial da Central 1.0.47.')
w=w.replace(guard_old,guard_new,1)

call_old="   let missionEvent=await processMission(env,account,raw,t,previousSampleAt);\n"
call_new="""   const previousIdle=!!prevRaw&&prevRaw?.game?.connected===true&&!bool(prevRaw,'job_latched','jobLatched')&&!bool(prevRaw,'on_job','onJob','gameplay.onJob');
   const preflightTruckDamageReady=previousIdle&&['truck_engine_damage_pct','truck_transmission_damage_pct','truck_cabin_damage_pct','truck_chassis_damage_pct','truck_wheels_damage_pct'].every(key=>typeof prevRaw[key]==='number'&&Number.isFinite(prevRaw[key])&&prevRaw[key]>=0&&prevRaw[key]<=100);
   let missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);
"""
if call_old not in w:
    raise SystemExit('Nao encontrei chamada processMission do endpoint de telemetria.')
w=w.replace(call_old,call_new,1)
worker.write_text(w,encoding='utf-8')

r=rank.read_text(encoding='utf-8')
old_sig="""    const oldTruckSimDamagePlugin = readiness?.reason === 'damage_data_incomplete' &&
      truckMissing.every(name => missingNow.includes(name)) &&
      !missingNow.includes('cargo') && !missingNow.includes('trailer');"""
new_sig="""    const oldTruckSimDamagePlugin = !next.preflight_truck_damage_ready &&
      readiness?.reason === 'damage_data_incomplete' &&
      truckMissing.every(name => missingNow.includes(name)) &&
      !missingNow.includes('cargo') && !missingNow.includes('trailer');"""
if old_sig not in r:
    raise SystemExit('Nao encontrei a assinatura de TruckSim antigo adicionada na 1.0.47.')
r=r.replace(old_sig,new_sig,1)
rank.write_text(r,encoding='utf-8')

# 2) Reparo idempotente da entrega rejeitada do JeanJC. A Central mantem 14 backups
# SQLite; procuramos neles a missao ativa e, quando disponivel, o recibo final. Isso
# permite recuperar carga/rota/peso/km reais sem inventar dados. Se nenhum backup tiver
# os detalhes, ainda contamos uma entrega transparente (100 pontos, 0 km/XP) em vez de
# deixar o motorista com uma viagem legitima perdida.
h=host.read_text(encoding='utf-8')
fs_old="import {existsSync,writeFileSync,mkdirSync,readFileSync} from 'node:fs';"
fs_new="import {existsSync,writeFileSync,mkdirSync,readFileSync,readdirSync} from 'node:fs';"
if fs_old not in h:
    raise SystemExit('Nao encontrei import fs do host local.')
h=h.replace(fs_old,fs_new,1)
if "import {DatabaseSync} from 'node:sqlite';" not in h:
    h=h.replace("import worker from './worker.js';", "import worker from './worker.js';\nimport {DatabaseSync} from 'node:sqlite';",1)

anchor="function reconcileMonthlyTripGoal(db){"
if anchor not in h:
    raise SystemExit('Nao encontrei reconcileMonthlyTripGoal no host local.')
repair=r'''function jeanNorm(value){
  return String(value||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
}
function jeanReceipt(raw){
  const value=raw?.gameplay?.jobDeliveredDetails||raw?.jobDeliveredDetails||{};
  const distance=Number(value.distanceKm??value.distance_km)||0,revenue=Number(value.revenue)||0,earnedXp=Number(value.earnedXp??value.earned_xp)||0;
  return distance>0||revenue>0||earnedXp>0?{...value,distanceKm:distance,revenue,earnedXp}:null;
}
export function recoverJeanJcEvidence(dataDir){
  const tripId='90be671a94074e8e900ef83e892b6a41',folder=join(dataDir,'backups');
  const result={mission:null,receipt:null,backup:null,telemetry_at:null};
  if(!existsSync(folder))return result;
  const files=readdirSync(folder).filter(x=>/^central-.*\.sqlite$/.test(x)).sort().reverse();
  for(const file of files){
    let sourceDb=null;
    try{
      sourceDb=new DatabaseSync(join(folder,file));
      const profile=sourceDb.prepare("SELECT current_mission_json FROM profiles WHERE user='jeanjc'").get();
      let mission=null;try{mission=profile?.current_mission_json?JSON.parse(profile.current_mission_json):null}catch{}
      const missionTrip=String(mission?.trip_id||mission?.job_latch_key||'');
      if(!result.mission&&mission&&missionTrip===tripId&&String(mission.cargo||'').trim()&&String(mission.source||'').trim()&&String(mission.destination||'').trim()){
        result.mission=mission;result.backup=file;
      }
      let rows=[];try{rows=sourceDb.prepare("SELECT telemetry_json,updated_at FROM telemetry_live WHERE account_user='jeanjc' OR driver='jeanjc' ORDER BY updated_at DESC LIMIT 4").all()}catch{}
      for(const row of rows){
        let raw=null;try{raw=JSON.parse(row.telemetry_json||'{}')}catch{}
        if(!raw)continue;
        const rawTrip=String(raw.gat_trip_id||raw.gatTripId||raw.job_latch_key||raw.jobLatchKey||'');
        const receipt=jeanReceipt(raw);
        if(!result.receipt&&receipt&&(rawTrip===tripId||(!rawTrip&&missionTrip===tripId))){result.receipt=receipt;result.telemetry_at=row.updated_at;if(!result.backup)result.backup=file;}
      }
    }catch{}finally{try{sourceDb?.close()}catch{}}
    if(result.mission&&result.receipt)break;
  }
  return result;
}

export function repairJeanJcRejectedDelivery(db,dataDir){
  const marker='repair_jeanjc_2026_09_03_v1',user='jeanjc',tripId='90be671a94074e8e900ef83e892b6a41';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(marker))return{changed:false,reason:'already_repaired'};
  const profile=db.sql.prepare('SELECT current_mission_json FROM profiles WHERE user=?').get(user);if(!profile)return{changed:false,reason:'profile_missing'};
  const existing=db.sql.prepare("SELECT id FROM deliveries WHERE user=? AND (raw_json LIKE ? OR (delivered_at>=? AND delivered_at<=?)) ORDER BY id DESC LIMIT 1").get(user,'%'+tripId+'%','2026-09-03T15:20:00.000Z','2026-09-03T15:23:59.999Z');
  const repairAt=new Date().toISOString(),recordedAt='2026-09-03T15:21:46.869Z';
  if(existing){db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(marker,repairAt);return{changed:false,reason:'delivery_exists',delivery_id:Number(existing.id)};}
  const evidence=recoverJeanJcEvidence(dataDir),mission=evidence.mission||{},receipt=evidence.receipt||{};
  const exact=!!(String(mission.cargo||'').trim()&&String(mission.source||'').trim()&&String(mission.destination||'').trim());
  const cargo=exact?String(mission.cargo).trim():'Carga recuperada do JeanJC';
  const source=exact?String(mission.source).trim():'Origem nao recuperada';
  const destination=exact?String(mission.destination).trim():'Destino nao recuperado';
  const weight=exact?Math.max(0,Number(mission.weight_kg)||0):0;
  const distance=Math.max(0,Number(receipt.distanceKm)||Number(mission.planned_distance_km)||Number(mission.rbr_start_remaining_km)||0);
  const xp=Math.floor(distance/100)*20,gatPoints=100;
  let current=null;try{current=profile.current_mission_json?JSON.parse(profile.current_mission_json):null}catch{}
  const currentTrip=String(current?.trip_id||current?.job_latch_key||''),clearRejected=currentTrip===tripId||String(current?.id||'')==='2026-09-jeanjc-unclassified-057b7e0fec67';
  const audit={base_xp:xp,speed_penalty_xp:0,cargo_penalty_xp:0,truck_penalty_xp:0,perfect_bonus_xp:0,cargo_damage_pct:receipt.cargoDamage??null,truck_damage_delta_pct:null,perfect_trip:false,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:0,gat_cargo_penalty_points:0,gat_truck_penalty_points:0,gat_penalty_points:0,gat_points:gatPoints,rank_verified:true,repair_verified_pre_rejection_backup:exact,repair_fallback_no_backup:!exact};
  const raw=JSON.stringify({mission:exact?mission:{id:'repair-2026-09-jeanjc',trip_id:tripId,state:'repaired',classification_mode:'pending'},delivery_details:receipt,audit,repair:{reason:'telemetry_start_false_rejection',original_rejection:'telemetry_not_verified_from_start',trip_id:tripId,backup:evidence.backup||null,telemetry_at:evidence.telemetry_at||null,repaired_at:repairAt,recorded_delivery_at:recordedAt,exact_data_recovered:exact}});
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    const inserted=db.sql.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').run(user,Number(mission.sequence)||null,source,destination,cargo,weight,distance,xp,0,0,0,recordedAt,raw);
    const deliveryId=Number(inserted.lastInsertRowid);
    db.sql.prepare('UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,current_mission_json=CASE WHEN ? THEN NULL ELSE current_mission_json END,updated_at=? WHERE user=?').run(distance,xp,gatPoints,clearRejected?1:0,repairAt,user);
    db.sql.prepare('INSERT OR IGNORE INTO mission_completions(mission_id,user,completed_at) VALUES(?,?,?)').run(String(mission.id||('repair-'+tripId)),user,recordedAt);
    if(exact&&jeanNorm(source)&&jeanNorm(destination)&&jeanNorm(source)!==jeanNorm(destination))db.sql.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').run(user,'2026-09',jeanNorm(source)+'>'+jeanNorm(destination),source,destination,recordedAt);
    if(exact){
      const suggested=String(mission.classification_suggested_work_id||current?.classification_suggested_work_id||'').trim(),validSuggested=suggested&&db.sql.prepare('SELECT 1 FROM work_catalog WHERE id=? AND active=1').get(suggested)?suggested:null;
      db.sql.prepare("INSERT OR IGNORE INTO cargo_classification_queue(delivery_id,user,cargo,cargo_key,source,destination,weight_kg,distance_km,delivered_at,status,suggested_work_id,suggested_confidence) VALUES(?,?,?,?,?,?,?,?,?,'pending',?,?)").run(deliveryId,user,cargo,jeanNorm(cargo),source,destination,weight,distance,recordedAt,validSuggested,Math.max(0,Math.min(1,Number(mission.classification_confidence||current?.classification_confidence)||0)));
    }
    db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(repairAt,'system','repair_missed_delivery',user,JSON.stringify({trip_id:tripId,cargo,source,destination,distance_km:distance,weight_kg:weight,xp,gat_points:gatPoints,backup:evidence.backup||null,exact_data_recovered:exact}));
    db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(marker,repairAt);
    db.sql.exec('COMMIT');
    return{changed:true,delivery_id:deliveryId,exact,cargo,source,destination,distance_km:distance,xp,gat_points:gatPoints,backup:evidence.backup||null};
  }catch(e){try{db.sql.exec('ROLLBACK')}catch{}throw e;}
}

'''
h=h.replace(anchor,repair+anchor,1)

startup_old="""  if(repaired)console.log('GAT 1.0.47: entrega Mower Conditioner do lapeal67 recuperada no ranking.');
  const backups=setInterval(backupNow,6*3600000);
"""
startup_new="""  if(repaired)console.log('GAT 1.0.47: entrega Mower Conditioner do lapeal67 recuperada no ranking.');
  const jeanRepair=await exclusive(async()=>{const result=repairJeanJcRejectedDelivery(db,dataDir);if(result.changed)reconcileMonthlyTripGoal(db);return result;});
  if(jeanRepair.changed)console.log('GAT 1.0.48: entrega rejeitada do jeanjc recuperada no ranking.',JSON.stringify(jeanRepair));
  const backups=setInterval(backupNow,6*3600000);
"""
if startup_old not in h:
    raise SystemExit('Nao encontrei o ponto de startup apos o reparo Lapeal.')
h=h.replace(startup_old,startup_new,1)
host.write_text(h,encoding='utf-8')

body=worker.read_text(encoding='utf-8')+'\n'+rank.read_text(encoding='utf-8')+'\n'+host.read_text(encoding='utf-8')
for marker in ['preflightTruckDamageReady=false','preflight_truck_damage_ready','!next.preflight_truck_damage_ready','repairJeanJcRejectedDelivery','recoverJeanJcEvidence','repair_jeanjc_2026_09_03_v1','90be671a94074e8e900ef83e892b6a41','repair_fallback_no_backup']:
    if marker not in body:raise SystemExit('Patch 1.0.48 incompleto: '+marker)
print('GAT 1.0.48 preparado: prova pre-carga de danos + reparo idempotente do JeanJC.')