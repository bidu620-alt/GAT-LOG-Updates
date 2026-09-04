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
const workerText=readFileSync(new URL('./runtime/worker.js',import.meta.url),'utf8');
const sha=v=>createHash('sha256').update(v).digest('hex');

function startPayload(cargo,source,destination,key){
 return {gat_client_version:'1.0.32',gat_job_event:'',job_latched:true,job_latch_key:key,on_job:true,cargo_name:cargo,mass_kg:15000,source_city:source,destination_city:destination,planned_distance_km:.2,remaining_km:.2,truck:{odometer:10000},game:{connected:true,paused:false},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{revenue:1000,earnedXp:1,cargoDamage:0,distanceKm:10}}};
}
function endPayload(start){
 return {...start,gat_job_event:'delivered',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,remaining_km:0,gameplay:{jobDelivered:true,jobCancelled:false,jobDeliveredDetails:{revenue:5000,earnedXp:1,cargoDamage:0,distanceKm:0,deliveryTime:1}}};
}

test('qualquer carga e aceita sem classificacao, inclusive carga inventada',async t=>{
 assert.ok(workerText.includes('API_OPEN_CARGO_V1'));
 assert.ok(!workerText.includes('cargo_not_compatible'),'o servidor nao pode rejeitar pelo nome da carga');
 assert.ok(!workerText.includes('delivery_completed_pending_classification'),'nao deve existir entrega pendente de classificacao');

 const dir=mkdtempSync(join(tmpdir(),'gat-open-cargo-'));folders.push(dir);
 const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
 const now=new Date().toISOString(),driverToken='driver-open-token';
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('biduzao','owner',?,?)").run(now,now);
 db.sql.prepare("INSERT INTO profiles(user,updated_at) VALUES('biduzao',?)").run(now);
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(sha(driverToken),'biduzao','biduzao','device-open-123456',now,now);
 const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));const base='http://127.0.0.1:'+server.address().port;
 const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-open-123456',token:driverToken,telemetry})});assert.equal(r.status,200);return r.json();};

 const invented=startPayload('Peixe Frito','Cidade A','Cidade B','peixe-frito-1');
 const begin=await send(invented);assert.equal(begin.mission_event?.mission?.open_cargo,true,JSON.stringify(begin));assert.equal(begin.mission_event?.mission?.catalog_id,'__open_cargo__');assert.equal(begin.mission_event?.mission?.title,'Peixe Frito');
 const done=await send(endPayload(invented));assert.equal(done.mission_event?.type,'delivery_completed',JSON.stringify(done));assert.equal(done.mission_event?.cargo_policy,'open');assert.equal(done.mission_event?.gat_points,100);
 let p=db.sql.prepare("SELECT monthly_completed,total_deliveries,points,current_mission_json FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,1);assert.equal(p.total_deliveries,1);assert.equal(p.points,100);assert.equal(p.current_mission_json,null);
 let d=db.sql.prepare("SELECT cargo,raw_json FROM deliveries WHERE user='biduzao' ORDER BY id DESC LIMIT 1").get();assert.equal(d.cargo,'Peixe Frito');assert.equal(JSON.parse(d.raw_json).audit.cargo_policy,'open');
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM cargo_classification_queue").get().n,0);

 const dlc=startPayload('Carga DLC Que Nunca Foi Cadastrada','Cidade C','Cidade D','dlc-livre-1');
 const begin2=await send(dlc);assert.equal(begin2.mission_event?.mission?.open_cargo,true,JSON.stringify(begin2));
 const done2=await send(endPayload(dlc));assert.equal(done2.mission_event?.type,'delivery_completed',JSON.stringify(done2));
 p=db.sql.prepare("SELECT monthly_completed,total_deliveries,points FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,2);assert.equal(p.total_deliveries,2);assert.equal(p.points,200);
 d=db.sql.prepare("SELECT cargo FROM deliveries WHERE user='biduzao' ORDER BY id DESC LIMIT 1").get();assert.equal(d.cargo,'Carga DLC Que Nunca Foi Cadastrada');
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM cargo_classification_queue").get().n,0);
});
