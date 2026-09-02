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

function startPayload(cargo,source,destination,key){
 return {gat_client_version:'1.0.30',gat_job_event:'',job_latched:true,job_latch_key:key,on_job:true,cargo_name:cargo,mass_kg:15000,source_city:source,destination_city:destination,planned_distance_km:.2,remaining_km:.2,truck:{odometer:10000},game:{connected:true,paused:false},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:{revenue:1000,earnedXp:1,cargoDamage:0,distanceKm:10}}};
}
function endPayload(start){
 return {...start,gat_job_event:'delivered',job_latched:false,on_job:false,cargo_name:'',mass_kg:0,remaining_km:0,gameplay:{jobDelivered:true,jobCancelled:false,jobDeliveredDetails:{revenue:5000,earnedXp:1,cargoDamage:0,distanceKm:0,deliveryTime:1}}};
}

test('carga e classificada sem motorista escolher trabalho; desconhecida vai para moderacao e aprende',async t=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-auto-cargo-'));folders.push(dir);
 const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());db.sql.exec(schema);db.sql.exec(migration);
 const now=new Date().toISOString(),future=new Date(Date.now()+86400000).toISOString(),driverToken='driver-auto-token',modToken='moderator-auto-token';
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('biduzao','owner',?,?)").run(now,now);
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('modteste','moderator',?,?)").run(now,now);
 db.sql.prepare("INSERT INTO profiles(user,updated_at) VALUES('biduzao',?)").run(now);
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('fuel',1,'Combustíveis','Combustíveis','[]')").run();
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('chemical',2,'Produtos químicos','Produtos químicos','[]')").run();
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('heavy',3,'Cargas pesadas','Cargas pesadas','[]')").run();
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(sha(driverToken),'biduzao','biduzao','device-auto-123456',now,now);
 db.sql.prepare('INSERT INTO sessions(token_hash,user,created_at,expires_at) VALUES(?,?,?,?)').run(sha(modToken),'modteste',now,future);
 const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));const base='http://127.0.0.1:'+server.address().port;
 const send=async telemetry=>{const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-auto-123456',token:driverToken,telemetry})});assert.equal(r.status,200);return r.json();};
 const admin=async(path,body={})=>{const r=await fetch(base+path,{method:'POST',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:modToken,...body})});return{r,data:await r.json()};};
 const publicProfile=async()=>{const r=await fetch(base+'/api/public/driver?user=biduzao');assert.equal(r.status,200);return (await r.json()).profile;};
 const recordedPoints=()=>db.sql.prepare("SELECT COALESCE(SUM(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER)),0) points FROM deliveries WHERE user='biduzao'").get().points;

 // Diesel: nenhuma missao foi escolhida no site. A Central deve criar/associar Combustiveis sozinha.
 const diesel=startPayload('Diesel','Cidade A','Cidade B','diesel-1');const begin=await send(diesel);assert.equal(begin.mission_event?.mission?.catalog_id,'fuel',JSON.stringify(begin));assert.equal(begin.mission_event?.mission?.classification_mode,'automatic');
 const done=await send(endPayload(diesel));assert.equal(done.mission_event?.type,'delivery_completed',JSON.stringify(done));
 let p=db.sql.prepare("SELECT monthly_completed,total_deliveries,current_mission_json FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,1);assert.equal(p.total_deliveries,1);assert.equal(p.current_mission_json,null);assert.equal((await publicProfile()).points,100);assert.equal(recordedPoints(),100);
 let alias=db.sql.prepare("SELECT work_id,source FROM cargo_aliases WHERE cargo_key='diesel'").get();assert.equal(alias.work_id,'fuel');assert.equal(alias.source,'automatic');
 assert.ok(JSON.parse(db.sql.prepare("SELECT compatible_cargos_json FROM work_catalog WHERE id='fuel'").get().compatible_cargos_json).includes('Diesel'));

 // Nome sem correspondencia: viagem valida e salva, recebe pontos/XP, mas nao aumenta x/30 ainda.
 const unknown=startPayload('Objeto experimental ZX-91','Cidade C','Cidade D','unknown-1');const pendingStart=await send(unknown);assert.equal(pendingStart.mission_event?.mission?.pending_classification,true,JSON.stringify(pendingStart));
 const pendingDone=await send(endPayload(unknown));assert.equal(pendingDone.mission_event?.type,'delivery_completed_pending_classification',JSON.stringify(pendingDone));assert.equal(pendingDone.mission_event?.gat_points,100);
 p=db.sql.prepare("SELECT monthly_completed,total_deliveries,points FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,1);assert.equal(p.total_deliveries,2);assert.equal(p.points,100);assert.equal(recordedPoints(),200);
 const pendingRaw=JSON.parse(db.sql.prepare("SELECT raw_json FROM deliveries WHERE user='biduzao' ORDER BY id DESC LIMIT 1").get().raw_json);assert.equal(pendingRaw.audit.gat_points,100);assert.equal(pendingRaw.audit.classification_status,'pending');
 const queue=db.sql.prepare("SELECT * FROM cargo_classification_queue WHERE status='pending'").get();assert.ok(queue?.id);assert.equal(queue.cargo,'Objeto experimental ZX-91');

 // Moderador ve a fila e classifica. Nao duplica pontos/XP; apenas completa o trabalho e ensina o catalogo.
 const list=await admin('/api/site/admin/unclassified');assert.equal(list.r.status,200);assert.equal(list.data.viewer_role,'moderator');assert.ok(list.data.pending.some(x=>x.id===queue.id));
 const before=db.sql.prepare("SELECT points,xp,total_deliveries FROM profiles WHERE user='biduzao'").get(),pointsBefore=recordedPoints();const classified=await admin('/api/site/admin/classify',{queue_id:queue.id,work_id:'heavy'});assert.equal(classified.r.status,200,JSON.stringify(classified.data));assert.equal(classified.data.counted,true);
 p=db.sql.prepare("SELECT monthly_completed,total_deliveries,points,xp FROM profiles WHERE user='biduzao'").get();assert.equal(p.monthly_completed,2);assert.equal(p.total_deliveries,before.total_deliveries);assert.equal(p.points,before.points);assert.equal(p.xp,before.xp);assert.equal(recordedPoints(),pointsBefore);
 assert.equal(db.sql.prepare('SELECT status,classified_work_id,classified_by FROM cargo_classification_queue WHERE id=?').get(queue.id).classified_work_id,'heavy');
 alias=db.sql.prepare("SELECT work_id,source FROM cargo_aliases WHERE cargo_key='objeto experimental zx 91'").get();assert.equal(alias.work_id,'heavy');assert.equal(alias.source,'manual');
 assert.ok(JSON.parse(db.sql.prepare("SELECT compatible_cargos_json FROM work_catalog WHERE id='heavy'").get().compatible_cargos_json).includes('Objeto experimental ZX-91'));
});
