import test,{after} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {DatabaseSync} from 'node:sqlite';
import {LocalDatabase} from './runtime/database.mjs';
import {repairJeanJcRejectedDelivery} from './runtime/host.mjs';
import {readFileSync} from 'node:fs';

const folders=[];after(()=>{for(const d of folders)rmSync(d,{recursive:true,force:true});});
const schema=readFileSync(new URL('./runtime/schema.sql',import.meta.url),'utf8');
const migration=readFileSync(new URL('./runtime/migrations/0004_read_efficiency.sql',import.meta.url),'utf8');
const tripId='90be671a94074e8e900ef83e892b6a41';

function baseDb(dir){
 const db=new LocalDatabase(join(dir,'central.sqlite'));db.sql.exec(schema);db.sql.exec(migration);
 const at='2026-09-03T16:00:00.000Z';
 db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('jeanjc','driver',?,?)").run(at,at);
 db.sql.prepare("INSERT INTO work_catalog(id,position,title,category,compatible_cargos_json) VALUES('tractor',1,'Tratores','Maquinas agricolas','[]')").run();
 const stale={id:'2026-09-jeanjc-unclassified-057b7e0fec67',catalog_id:'__unclassified__',state:'assigned',classification_mode:'pending',classification_suggested_work_id:'tractor',trip_id:tripId,last_rejected_at:'2026-09-03T15:21:46.869Z',last_rejected_reason:'telemetry_not_verified_from_start'};
 db.sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run('jeanjc',JSON.stringify(stale),at);
 return db;
}
function makeBackup(dataDir){
 const folder=join(dataDir,'backups');mkdirSync(folder,{recursive:true});const path=join(folder,'central-2026-09-03T14-00-00-000Z.sqlite');
 const sql=new DatabaseSync(path);sql.exec(schema);sql.exec(migration);
 const at='2026-09-03T14:00:00.000Z';sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('jeanjc','driver',?,?)").run(at,at);
 const mission={id:'mission-jeanjc-real',catalog_id:'__unclassified__',state:'active',classification_mode:'pending',classification_suggested_work_id:'tractor',classification_confidence:.72,trip_id:tripId,cargo:'Trator John Deere 8R',source:'Berlin',destination:'Paris',weight_kg:18000,planned_distance_km:742};
 sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run('jeanjc',JSON.stringify(mission),at);
 const raw={gat_trip_id:tripId,gat_job_event:'delivered',game:{connected:true},gameplay:{jobDeliveredDetails:{revenue:42000,earnedXp:900,cargoDamage:0,distanceKm:735,deliveryTime:70,autoParked:false,autoLoaded:true}}};
 sql.prepare('INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json) VALUES(?,?,?,?,?)').run('jeanjc','jeanjc','device-test',at,JSON.stringify(raw));sql.close();
}

test('reparo JeanJC recupera dados reais do backup, conta uma vez e limpa somente a missao rejeitada',()=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-jeanjc-repair-'));folders.push(dir);const db=baseDb(dir);makeBackup(dir);
 const first=repairJeanJcRejectedDelivery(db,dir);assert.equal(first.changed,true);assert.equal(first.exact,true);assert.equal(first.distance_km,735);assert.equal(first.xp,140);assert.equal(first.gat_points,100);
 const delivery=db.sql.prepare("SELECT * FROM deliveries WHERE user='jeanjc'").get();assert.equal(delivery.cargo,'Trator John Deere 8R');assert.equal(delivery.source,'Berlin');assert.equal(delivery.destination,'Paris');assert.equal(delivery.weight_kg,18000);assert.equal(delivery.distance_km,735);assert.equal(delivery.xp,140);assert.equal(delivery.perfect,0);
 const raw=JSON.parse(delivery.raw_json);assert.equal(raw.audit.gat_points,100);assert.equal(raw.audit.rank_verified,true);assert.equal(raw.audit.repair_verified_pre_rejection_backup,true);assert.equal(raw.audit.perfect_trip,false);assert.match(raw.repair.backup,/central-2026-09-03/);
 const profile=db.sql.prepare("SELECT monthly_completed,total_deliveries,total_km,xp,points,current_mission_json FROM profiles WHERE user='jeanjc'").get();assert.equal(profile.monthly_completed,1);assert.equal(profile.total_deliveries,1);assert.equal(profile.total_km,735);assert.equal(profile.xp,140);assert.equal(profile.points,100);assert.equal(profile.current_mission_json,null);
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM routes_completed WHERE user='jeanjc'").get().n,1);assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM cargo_classification_queue WHERE user='jeanjc' AND status='pending'").get().n,1);assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM mission_completions WHERE user='jeanjc'").get().n,1);
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM meta WHERE key='repair_jeanjc_2026_09_03_v1'").get().n,1);
 const second=repairJeanJcRejectedDelivery(db,dir);assert.equal(second.changed,false);assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM deliveries WHERE user='jeanjc'").get().n,1);db.close();
});

test('sem backup o JeanJC recebe a viagem e pontos sem inventar km, XP, rota ou carga real',()=>{
 const dir=mkdtempSync(join(tmpdir(),'gat-jeanjc-fallback-'));folders.push(dir);const db=baseDb(dir);
 const result=repairJeanJcRejectedDelivery(db,dir);assert.equal(result.changed,true);assert.equal(result.exact,false);assert.equal(result.distance_km,0);assert.equal(result.xp,0);assert.equal(result.gat_points,100);
 const delivery=db.sql.prepare("SELECT * FROM deliveries WHERE user='jeanjc'").get(),raw=JSON.parse(delivery.raw_json);assert.equal(delivery.cargo,'Carga recuperada do JeanJC');assert.equal(delivery.distance_km,0);assert.equal(raw.audit.repair_fallback_no_backup,true);assert.equal(raw.audit.repair_verified_pre_rejection_backup,false);
 const profile=db.sql.prepare("SELECT monthly_completed,total_deliveries,total_km,xp,points,current_mission_json FROM profiles WHERE user='jeanjc'").get();assert.equal(profile.monthly_completed,1);assert.equal(profile.total_deliveries,1);assert.equal(profile.total_km,0);assert.equal(profile.xp,0);assert.equal(profile.points,100);assert.equal(profile.current_mission_json,null);
 assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM routes_completed WHERE user='jeanjc'").get().n,0);assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM cargo_classification_queue WHERE user='jeanjc'").get().n,0);db.close();
});