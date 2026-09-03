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
const sha=v=>createHash('sha256').update(v).digest('hex');
const truckDamages={truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0};
const allDamages={cargo_damage_pct:0,...truckDamages,trailer_damage_pct:0};
function loaded(extra={}){return{gat_client_version:'1.0.31',gat_packet_id:Math.random().toString(16).slice(2),gat_job_event:'',gat_job_state:'active',gat_trip_id:'trip-preflight-1',job_latched:true,job_latch_key:'trip-preflight-1',on_job:true,cargo_name:'Empilhadeiras',mass_kg:12690,source_city:'Cidade A',destination_city:'Cidade B',planned_distance_km:20,remaining_km:20,truck:{odometer:10000},game:{connected:true,paused:false},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{}},...allDamages,...extra};}
function idle(){return{gat_client_version:'1.0.31',gat_packet_id:'idle-preflight',gat_job_event:'',gat_job_state:'idle',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,game:{connected:true,paused:false},truck:{odometer:9999},...truckDamages};}
function delivered(){return loaded({gat_job_event:'delivered',gat_job_state:'idle',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:10002},gameplay:{jobDelivered:true,jobCancelled:false,jobDeliveredDetails:{distanceKm:2,revenue:1000,earnedXp:10,cargoDamage:0}}});}

test('idle prova plugin atual e permite pacote inicial perder os cinco danos do caminhao sem condenar a viagem',async t=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-rank-preflight-'));folders.push(dir);const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
 const at=new Date().toISOString(),token='preflight-token';db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('driverpre','driver',?,?)").run(at,at);db.sql.prepare("INSERT INTO profiles(user,updated_at) VALUES('driverpre',?)").run(at);db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('heavy',1,'Cargas pesadas','Maquinas e equipamentos','[\"Empilhadeiras\"]')").run();db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(sha(token),'driverpre','driverpre','device-preflight-123456',at,at);
 const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));const base='http://127.0.0.1:'+server.address().port;
 const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'driverpre',device_id:'device-preflight-123456',token,telemetry})});assert.equal(r.status,200);return r.json();};
 await send(idle());
 const first=loaded();for(const key of Object.keys(truckDamages))delete first[key];const a=await send(first);assert.equal(a.rank_status.reason,'damage_data_incomplete');
 let mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='driverpre'").get().current_mission_json);assert.equal(mission.rank_guard.preflight_truck_damage_ready,true);assert.notEqual(mission.rank_guard.incompatible_damage_plugin,true);assert.equal(mission.rank_guard.reason,'telemetry_not_verified_from_start');
 await send(loaded({remaining_km:19}));mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='driverpre'").get().current_mission_json);assert.equal(mission.rank_guard.valid_samples,1);assert.notEqual(mission.rank_guard.incompatible_damage_plugin,true);
 await send(loaded({remaining_km:18,truck_engine_damage_pct:.1}));mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='driverpre'").get().current_mission_json);assert.equal(mission.rank_guard.reason,null);assert.equal(mission.rank_guard.valid_samples,2);assert.ok(mission.rank_guard.verified_at);
 const end=await send(delivered());assert.match(String(end.mission_event?.type),/^delivery_completed/);const profile=db.sql.prepare("SELECT monthly_completed,total_deliveries FROM profiles WHERE user='driverpre'").get();assert.equal(profile.monthly_completed,1);assert.equal(profile.total_deliveries,1);
});