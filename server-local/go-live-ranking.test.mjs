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
const damages={cargo_damage_pct:0,truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,trailer_damage_pct:0};

function packet(extra={}){
 return {gat_client_version:'1.0.31',gat_packet_id:Math.random().toString(16).slice(2),gat_job_event:'',gat_job_state:'active',gat_trip_id:'trip-live-1',job_latched:true,job_latch_key:'trip-live-1',on_job:true,cargo_name:'Empilhadeiras',mass_kg:12690,source_city:'Cidade A',destination_city:'Cidade B',planned_distance_km:20,remaining_km:20,truck:{odometer:10000},game:{connected:true,paused:false},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{}},...damages,...extra};
}
function delivered(){return packet({gat_job_event:'delivered',gat_job_state:'idle',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:10002},gameplay:{jobDelivered:true,jobCancelled:false,jobDeliveredDetails:{distanceKm:2,revenue:1000,earnedXp:10,cargoDamage:0}}});}

test('go-live: primeiro pacote incompleto e transitorio recupera com duas amostras validas',async t=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-go-live-rank-'));folders.push(dir);
 const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
 const at=new Date().toISOString(),token='go-live-token';
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('biduzao','owner',?,?)").run(at,at);
 db.sql.prepare("INSERT INTO profiles(user,updated_at) VALUES('biduzao',?)").run(at);
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('heavy',1,'Cargas pesadas','Maquinas e equipamentos','[\"Empilhadeiras\"]')").run();
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(sha(token),'biduzao','biduzao','device-go-live-123456',at,at);
 const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));const base='http://127.0.0.1:'+server.address().port;
 const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-go-live-123456',token,telemetry})});assert.equal(r.status,200);return r.json();};

 // Reproduz o caso real: a carga nasce enquanto um campo de dano ainda nao chegou.
 const first=await send(packet({trailer_damage_pct:undefined}));assert.equal(first.rank_status?.reason,'damage_data_incomplete',JSON.stringify(first));
 let mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='biduzao'").get().current_mission_json);
 assert.equal(mission.rank_guard?.reason,'telemetry_not_verified_from_start',JSON.stringify(mission));assert.equal(mission.rank_guard?.valid_samples,0);assert.equal(mission.rank_guard?.last_invalid_reason,'damage_data_incomplete');

 // Duas leituras completas e continuas dentro da janela inicial confirmam o ranking.
 const second=await send(packet({remaining_km:19,truck:{odometer:10001}}));assert.equal(second.rank_status?.eligible,true,JSON.stringify(second));
 mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='biduzao'").get().current_mission_json);assert.equal(mission.rank_guard?.reason,'telemetry_not_verified_from_start',JSON.stringify(mission));assert.equal(mission.rank_guard?.valid_samples,1);
 const third=await send(packet({remaining_km:18,truck:{odometer:10002},truck_engine_damage_pct:.1}));assert.equal(third.rank_status?.eligible,true,JSON.stringify(third));
 mission=JSON.parse(db.sql.prepare("SELECT current_mission_json FROM profiles WHERE user='biduzao'").get().current_mission_json);assert.equal(mission.rank_guard?.reason,null,JSON.stringify(mission));assert.equal(mission.rank_guard?.valid_samples,2);assert.ok(mission.rank_guard?.verified_at);

 const end=await send(delivered());assert.equal(end.mission_event?.type,'delivery_completed',JSON.stringify(end));
 const profile=db.sql.prepare("SELECT monthly_completed,total_deliveries FROM profiles WHERE user='biduzao'").get();assert.equal(profile.monthly_completed,1);assert.equal(profile.total_deliveries,1);
});
