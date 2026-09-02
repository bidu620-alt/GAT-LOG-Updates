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

function packet({trip='trip-1',cargo='Empilhadeiras',source='A',destination='B',remaining=20,odometer=10000,...extra}={}){
 return {gat_client_version:'1.0.31',gat_packet_id:Math.random().toString(16).slice(2),gat_job_event:'',gat_job_state:'active',gat_trip_id:trip,job_latched:true,job_latch_key:trip,on_job:true,cargo_name:cargo,mass_kg:12690,source_city:source,destination_city:destination,planned_distance_km:20,remaining_km:remaining,truck:{odometer},game:{connected:true,paused:false},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{}},...damages,...extra};
}
function delivered(opts={}){
 const base=packet(opts);return {...base,gat_job_event:'delivered',gat_job_state:'idle',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:(opts.odometer||10000)+2},gameplay:{jobDelivered:true,jobCancelled:false,jobDeliveredDetails:{distanceKm:2,revenue:1000,earnedXp:10,cargoDamage:0}}};
}

test('meta mensal conta viagens validas, repeticoes e pendentes de classificacao imediatamente',async t=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-monthly-trips-'));folders.push(dir);
 const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
 const at=new Date().toISOString(),token='monthly-token';
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('biduzao','owner',?,?)").run(at,at);
 db.sql.prepare("INSERT INTO profiles(user,updated_at) VALUES('biduzao',?)").run(at);
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('heavy',1,'Cargas pesadas','Maquinas e equipamentos','[\"Empilhadeiras\"]')").run();
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(sha(token),'biduzao','biduzao','device-monthly-123456',at,at);
 const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));const base='http://127.0.0.1:'+server.address().port;
 const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-monthly-123456',token,telemetry})});assert.equal(r.status,200);return r.json();};
 const publicProfile=async()=>{const r=await fetch(base+'/api/public/driver?user=biduzao');assert.equal(r.status,200);return (await r.json()).profile;};
 const run=async opts=>{await send(packet(opts));await send(packet({...opts,remaining:18,odometer:(opts.odometer||10000)+2}));return send(delivered({...opts,odometer:(opts.odometer||10000)+2}));};

 assert.equal((await run({trip:'trip-1',cargo:'Empilhadeiras',source:'A',destination:'B',odometer:10000})).mission_event?.type,'delivery_completed');
 let p=db.sql.prepare("SELECT monthly_completed,total_deliveries FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,1);assert.equal(p.total_deliveries,1);const points1=(await publicProfile()).points;assert.ok(points1>0);

 assert.equal((await run({trip:'trip-2',cargo:'Empilhadeiras',source:'C',destination:'D',odometer:10010})).mission_event?.type,'delivery_completed');
 p=db.sql.prepare("SELECT monthly_completed,total_deliveries FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,2);assert.equal(p.total_deliveries,2);assert.ok((await publicProfile()).points>points1,'repeticao valida deve continuar somando ranking');

 const pending=await run({trip:'trip-3',cargo:'Carga Experimental QZ 9182',source:'E',destination:'F',odometer:10020});
 assert.equal(pending.mission_event?.type,'delivery_completed_pending_classification',JSON.stringify(pending));
 p=db.sql.prepare("SELECT monthly_completed,total_deliveries FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,3);assert.equal(p.total_deliveries,3);
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM cargo_classification_queue WHERE status='pending'").get().n,1);
});
