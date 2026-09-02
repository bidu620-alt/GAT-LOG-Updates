import test,{after} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {LocalDatabase} from './runtime/database.mjs';
import {createCentral} from './runtime/host.mjs';

const folders=[];after(()=>{for(const d of folders)rmSync(d,{recursive:true,force:true});});
const schema=readFileSync(new URL('./runtime/schema.sql',import.meta.url),'utf8');
const migration=readFileSync(new URL('./runtime/migrations/0004_read_efficiency.sql',import.meta.url),'utf8');
const tokenHash=v=>createHash('sha256').update(v).digest('hex');

async function centralFor(t,user){
  const dir=mkdtempSync(join(tmpdir(),'gat-trip-v2-'));folders.push(dir);
  const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
  const at=new Date().toISOString(),token='token-'+user,device='device-'+user+'-123456';
  db.sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run(user,at,at);
  db.sql.prepare('INSERT INTO profiles(user,updated_at) VALUES(?,?)').run(user,at);
  db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(tokenHash(token),user,user,device,at,at);
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:user,device_id:device,token,telemetry})});const text=await r.text();assert.equal(r.status,200,text);return JSON.parse(text);};
  return{db,base,send};
}

const loaded=(trip,cargo='Barris vazios')=>({
  gat_schema:'job-v2',gat_job_state:'active',gat_job_event:'',gat_trip_id:trip,job_latched:true,job_latch_key:trip,on_job:true,gat_client_version:'1.0.31',
  cargo_name:cargo,cargo_id:cargo==='Barris vazios'?'empty_barr':'used_packag',mass_kg:9226.8,source_city:'Brussel',destination_city:'Liege',planned_distance_km:10,remaining_km:10,
  truck:{odometer:1000},game:{connected:true,paused:false},
  gameplay:{onJob:true,jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{revenue:0,earnedXp:0,cargoDamage:0,distanceKm:0,deliveryTime:0,autoParked:false,autoLoaded:false}}
});
const idle=(trip,details)=>({
  gat_schema:'job-v2',gat_job_state:'idle',gat_job_event:'',gat_trip_id:trip,job_latched:false,job_latch_key:trip,on_job:false,gat_client_version:'1.0.31',
  cargo_name:'',cargo_id:'',mass_kg:0,source_city:'',destination_city:'',remaining_km:0,truck:{odometer:1001},game:{connected:true,paused:false},
  gameplay:{onJob:false,jobDelivered:false,jobCancelled:false,jobDeliveredDetails:details}
});

test('job-v2: Central conclui pelo recibo, limpa missao e invalida perfil imediatamente',async t=>{
  const {db,base,send}=await centralFor(t,'biduzao');
  const start=await send(loaded('trip-barris-001'));
  assert.equal(start.mission_event?.type,'mission_in_progress',JSON.stringify(start));
  const before=await (await fetch(base+'/api/public/driver?user=biduzao')).json();
  assert.equal(before.profile.total_deliveries,0);
  const end=await send(idle('trip-barris-001',{revenue:390,earnedXp:2,cargoDamage:0,distanceKm:1,deliveryTime:6,autoParked:true,autoLoaded:true}));
  assert.equal(end.mission_event?.type,'delivery_completed_pending_classification',JSON.stringify(end));
  const afterNow=await (await fetch(base+'/api/public/driver?user=biduzao')).json();
  assert.equal(afterNow.profile.total_deliveries,1);
  assert.equal(afterNow.profile.points,100);
  assert.equal(afterNow.profile.current_mission,null);
  const row=db.sql.prepare("SELECT raw_json FROM deliveries WHERE user='biduzao'").get();
  assert.equal(JSON.parse(row.raw_json).audit.gat_points,100);
});

test('job-v2: desaparecimento sem comprovante cancela e libera a proxima carga',async t=>{
  const {db,send}=await centralFor(t,'motorista_teste');
  const start={...loaded('trip-cancel-001'),planned_distance_km:600,remaining_km:600,
    truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,cargo_damage_pct:0,trailer_damage_pct:0};
  await send(start);
  const end={...idle('trip-cancel-001',{revenue:0,earnedXp:0,cargoDamage:0,distanceKm:0,deliveryTime:0,autoParked:false,autoLoaded:false}),
    truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,cargo_damage_pct:0,trailer_damage_pct:0};
  const result=await send(end);
  assert.equal(result.mission_event?.type,'mission_cancelled',JSON.stringify(result));
  assert.equal(result.mission_event?.reason,'observed_job_end');
  const p=db.sql.prepare("SELECT current_mission_json,total_deliveries FROM profiles WHERE user='motorista_teste'").get();
  assert.equal(p.current_mission_json,null);
  assert.equal(p.total_deliveries,0);
});

test('job-v2: nova trip nao fica presa na missao anterior',async t=>{
  const {db,send}=await centralFor(t,'biduzao');
  await send(loaded('trip-old-001','Barris vazios'));
  const replacement={...loaded('trip-new-002','Embalagens usadas'),gameplay:{onJob:true,jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{revenue:450,earnedXp:2,cargoDamage:0,distanceKm:2,deliveryTime:8,autoParked:true,autoLoaded:true}}};
  const closesOld=await send(replacement);
  assert.ok(String(closesOld.mission_event?.type||'').startsWith('delivery_completed'),JSON.stringify(closesOld));
  const beginsNew=await send(replacement);
  assert.equal(beginsNew.mission_event?.type,'mission_in_progress',JSON.stringify(beginsNew));
  const p=db.sql.prepare("SELECT current_mission_json,total_deliveries FROM profiles WHERE user='biduzao'").get();
  assert.equal(p.total_deliveries,1);
  const mission=JSON.parse(p.current_mission_json);
  assert.equal(mission.trip_id,'trip-new-002');
  assert.equal(mission.cargo,'Embalagens usadas');
});