import http from 'node:http';
import {join,dirname,basename} from 'node:path';
import {fileURLToPath} from 'node:url';
import {existsSync,writeFileSync,mkdirSync,readFileSync,readdirSync} from 'node:fs';
import worker from './worker.js';
import {DatabaseSync} from 'node:sqlite';
import {LocalDatabase,validateDatabase,importDatabase,saveBackup} from './database.mjs';

const here=dirname(fileURLToPath(import.meta.url));
function ensureAutomaticCargoCatalog(db){
  // Reexecutar o schema e seguro: todas as tabelas/indices usam IF NOT EXISTS.
  // Isso cria a fila de classificacao tambem em bancos que ja existiam antes da 1.0.40.
  db.sql.exec(readFileSync(join(here,'schema.sql'),'utf8'));
  const initialized=db.sql.prepare("SELECT value FROM meta WHERE key='auto_cargo_catalog_v1'").get();
  if(initialized)return;
  // O proprietario pediu para recomecar as sugestoes de nomes do zero. Mantemos os
  // 30 trabalhos, historico, contas e progresso; limpamos somente os nomes sugeridos.
  db.sql.prepare("UPDATE work_catalog SET compatible_cargos_json='[]' WHERE active=1").run();
  const t=new Date().toISOString();
  db.sql.prepare("INSERT OR REPLACE INTO meta(key,value) VALUES('auto_cargo_catalog_v1',?)").run(t);
}

export function repairLapealMowerDelivery(db){
  const marker='repair_lapeal_mower_2026_09_03_v1';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(marker))return false;
  const user='lapeal67',cargo='Mower Conditioner Krone BiG M 450',source='Málaga',destination='A Coruña';
  if(!db.sql.prepare('SELECT 1 FROM profiles WHERE user=?').get(user))return false;
  const existing=db.sql.prepare(`SELECT id FROM deliveries WHERE user=? AND lower(cargo)=lower(?)
    AND source=? AND destination=? AND substr(delivered_at,1,7)='2026-09' LIMIT 1`).get(user,cargo,source,destination);
  const recordedAt='2026-09-03T01:14:44.000Z',repairAt=new Date().toISOString();
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    if(!existing){
      const distance=1135,weight=15500,xp=220,gatPoints=100;
      const receipt={revenue:57672,earnedXp:1637,cargoDamage:0,distanceKm:1135,deliveryTime:63,autoParked:false,autoLoaded:true};
      const mission={id:'repair-2026-09-lapeal67-mower-big-m-450',catalog_id:'__official_cargo__',title:cargo,category:'Trator e máquinas agrícolas',state:'repaired',cargo,source,destination,weight_kg:weight,planned_distance_km:distance,map_mode:'base',classification_mode:'official_cargo_repair'};
      const audit={base_xp:220,speed_penalty_xp:0,cargo_penalty_xp:0,truck_penalty_xp:0,perfect_bonus_xp:0,cargo_damage_pct:0,truck_damage_delta_pct:null,perfect_trip:false,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:0,gat_cargo_penalty_points:0,gat_truck_penalty_points:0,gat_penalty_points:0,gat_points:gatPoints,rank_verified:true,repair_verified_receipt:true};
      const raw=JSON.stringify({mission,delivery_details:receipt,audit,repair:{reason:'missed_delivery_recovered_from_next_job_receipt',evidence:'gat_telemetry_delivery_details_start',repaired_at:repairAt,recorded_delivery_at:recordedAt}});
      db.sql.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').run(user,null,source,destination,cargo,weight,distance,xp,0,0,0,recordedAt,raw);
      db.sql.prepare(`UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,updated_at=? WHERE user=?`).run(distance,xp,gatPoints,repairAt,user);
      db.sql.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').run(user,'2026-09','malaga>a coruna',source,destination,recordedAt);
      db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(repairAt,'system','repair_missed_delivery',user,JSON.stringify({cargo,source,destination,distance_km:distance,weight_kg:weight,gat_points:gatPoints,xp,receipt}));
    }
    db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(marker,repairAt);
    db.sql.exec('COMMIT');
    return !existing;
  }catch(e){
    try{db.sql.exec('ROLLBACK')}catch{}
    throw e;
  }
}

function jeanNorm(value){
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

function reconcileMonthlyTripGoal(db){
  // A meta mensal e por VIAGENS VALIDAS, nao por classificacoes. Como toda entrega
  // aceita pelo ranking gera uma linha em deliveries, ela e a fonte definitiva do x/30.
  // Isso tambem corrige automaticamente a virada do mes e recupera entregas que foram
  // salvas como pendentes de classificacao antes da 1.0.46.
  const mk=new Date().toISOString().slice(0,7);
  db.sql.prepare(`UPDATE profiles SET monthly_completed=MIN(monthly_goal,(
    SELECT COUNT(*) FROM deliveries d
    WHERE d.user=profiles.user AND substr(d.delivered_at,1,7)=?
  ))`).run(mk);
}

async function ensureGoLiveBaseline(db,dataDir){
  const key='go_live_baseline_2026_09_02';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(key))return null;

  // Backup de arquivo completo antes de qualquer limpeza. Contas, senhas, tokens,
  // dispositivos, papeis, configuracao e catalogo nao sao apagados pelo reset.
  const backupPath=await saveBackup(db,dataDir);
  const t=new Date().toISOString();
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    // Remove somente dados competitivos/de teste. A fila depende de deliveries e
    // por isso e limpa primeiro. O catalogo e os aliases aprendidos permanecem.
    db.sql.prepare('DELETE FROM cargo_classification_queue').run();
    db.sql.prepare('DELETE FROM work_completed').run();
    db.sql.prepare('DELETE FROM routes_completed').run();
    db.sql.prepare('DELETE FROM mission_completions').run();
    db.sql.prepare('DELETE FROM deliveries').run();
    db.sql.prepare(`UPDATE profiles SET
      monthly_completed=0,
      total_deliveries=0,
      total_km=0,
      xp=0,
      points=0,
      perfect_trips=0,
      penalty_xp=0,
      speed_fines=0,
      safety_score=100,
      current_mission_json=NULL,
      updated_at=?`).run(t);
    db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(key,t);
    db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(
      t,'system','go_live_reset','all_drivers',JSON.stringify({started_at:t,backup:basename(backupPath),preserved:['accounts','sessions','client_tokens','client_pairings','work_catalog','cargo_aliases','telemetry_live']})
    );
    db.sql.exec('COMMIT');
    db.sql.exec('PRAGMA wal_checkpoint(TRUNCATE)');
    return backupPath;
  }catch(e){
    try{db.sql.exec('ROLLBACK')}catch{}
    throw e;
  }
}

export function createCentral(db,{onError=()=>{}}={}){
  let queue=Promise.resolve(),queued=0;
  function exclusive(fn){const work=queue.then(fn);queue=work.catch(()=>{});return work;}
  const server=http.createServer(async(req,res)=>{
    if(queued>=100){res.writeHead(503,{'Retry-After':'5'});res.end();return;}
    queued++;
    try{
      const parts=[];let size=0;
      for await(const chunk of req){size+=chunk.length;if(size>262144){res.writeHead(413);res.end();return;}parts.push(chunk);}
      const headers=new Headers();for(const[k,v]of Object.entries(req.headers))if(v!==undefined)headers.set(k,Array.isArray(v)?v.join(','):v);
      const request=new Request('https://api.gatlogets2.com.br'+req.url,{method:req.method,headers,...(!['GET','HEAD'].includes(req.method)?{body:Buffer.concat(parts)}:{})});
      const response=await exclusive(async()=>{
        const tasks=[];db.sql.exec('BEGIN IMMEDIATE');
        try{
          const result=await worker.fetch(request,{DB:db},{waitUntil:p=>tasks.push(p)});
          await Promise.all(tasks);
          db.sql.exec(result.status>=500?'ROLLBACK':'COMMIT');return result;
        }catch(e){db.sql.exec('ROLLBACK');throw e;}
      });
      res.writeHead(response.status,Object.fromEntries(response.headers));res.end(Buffer.from(await response.arrayBuffer()));
    }catch(e){onError(e);if(!res.headersSent)res.writeHead(500);res.end('{"ok":false,"error":"local_server_error"}');}
    finally{queued--;}
  });
  server.requestTimeout=15000;server.headersTimeout=10000;server.maxRequestsPerSocket=1000;
  return {server,exclusive};
}

async function main(){
  const dataDir=join(process.env.LOCALAPPDATA||process.cwd(),'GAT-LOG','Central');
  if(process.argv[2]==='import'){
    const counts=await importDatabase(process.argv[3],dataDir,join(here,'schema.sql'),join(here,'migrations/0004_read_efficiency.sql'));
    console.log(JSON.stringify({ok:true,...counts}));return;
  }
  const path=join(dataDir,'central.sqlite');
  if(!existsSync(path))throw Error('Importe a exportacao completa do banco antes de iniciar a central.');
  const db=new LocalDatabase(path);validateDatabase(db.sql);ensureAutomaticCargoCatalog(db);
  if(process.argv[2]==='backup'){console.log(await saveBackup(db,dataDir));db.close();return;}
  const resetBackup=await ensureGoLiveBaseline(db,dataDir);
  if(resetBackup)console.log('GAT go-live: progresso de testes zerado. Backup: '+resetBackup);
  reconcileMonthlyTripGoal(db);
  let lastError='';
  const {server,exclusive}=createCentral(db,{onError:e=>{lastError=String(e.message);console.error(lastError);}});
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(5056,'127.0.0.1',resolve);});
  mkdirSync(dataDir,{recursive:true});
  const status=()=>writeFileSync(join(dataDir,'status.json'),JSON.stringify({pid:process.pid,updated_at:new Date().toISOString(),backend:'local-sqlite',port:5056,last_error:lastError}));
  status();const heartbeat=setInterval(status,5000);
  const backupNow=()=>exclusive(()=>saveBackup(db,dataDir)).catch(e=>{lastError='Backup: '+e.message;});
  await backupNow();
  const repaired=await exclusive(async()=>{const changed=repairLapealMowerDelivery(db);if(changed)reconcileMonthlyTripGoal(db);return changed;});
  if(repaired)console.log('GAT 1.0.47: entrega Mower Conditioner do lapeal67 recuperada no ranking.');
  const jeanRepair=await exclusive(async()=>{const result=repairJeanJcRejectedDelivery(db,dataDir);if(result.changed)reconcileMonthlyTripGoal(db);return result;});
  if(jeanRepair.changed)console.log('GAT 1.0.48: entrega rejeitada do jeanjc recuperada no ranking.',JSON.stringify(jeanRepair));
  const backups=setInterval(backupNow,6*3600000);
  const cleanup=setInterval(()=>exclusive(async()=>{reconcileMonthlyTripGoal(db);const tasks=[];await worker.scheduled({}, {DB:db},{waitUntil:p=>tasks.push(p)});await Promise.all(tasks);}).catch(e=>{lastError=e.message;}),3600000);
  async function stop(){clearInterval(heartbeat);clearInterval(backups);clearInterval(cleanup);server.close();await exclusive(async()=>{db.sql.exec('PRAGMA wal_checkpoint(TRUNCATE)');db.close();});process.exit(0);}
  process.on('SIGTERM',stop);process.on('SIGINT',stop);
  console.log('GAT Central local ativa em 127.0.0.1:5056.');
}
if(process.argv[1]&&fileURLToPath(import.meta.url)===process.argv[1])main().catch(e=>{console.error(e.message);process.exitCode=1;});
