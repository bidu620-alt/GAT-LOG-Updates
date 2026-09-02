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

function tokenHash(v){return createHash('sha256').update(v).digest('hex');}
function damages(){return {cargo_damage_pct:0,truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,trailer_damage_pct:0};}

test('Biduzao: entrega real vence falso jobCancelled do TruckSim e conta com 1 km no modo temporario',async t=>{
  const dir=mkdtempSync(join(tmpdir(),'gat-delivery-regression-'));folders.push(dir);
  const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  db.sql.exec(schema);db.sql.exec(migration);
  const now=new Date().toISOString(),token='test-biduzão-token';
  db.sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run('biduzao',now,now);
  db.sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run('biduzao',JSON.stringify({id:'short-trip',catalog_id:'timber',state:'assigned',min_km:500,sequence:5,title:'Madeira e toras'}),now);
  db.sql.prepare('INSERT INTO work_catalog(id,position,title) VALUES(?,?,?)').run('timber',5,'Madeira e toras');
  db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(tokenHash(token),'biduzao','biduzao','device-biduzão-123456',now,now);
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const send=async telemetry=>{
    const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-biduzão-123456',token,telemetry})});
    assert.equal(r.status,200);return r.json();
  };
  const staleDetails={revenue:12000,earnedXp:100,cargoDamage:0,distanceKm:600,deliveryTime:30};
  const start={gat_client_version:'1.0.30',game:{connected:true,paused:false},job_latched:true,job_latch_key:'short-one',gat_job_event:'',cargo_name:'Madeira',mass_kg:20000,source_city:'A',destination_city:'B',planned_distance_km:1.2,remaining_km:1.2,truck:{odometer:10000},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:staleDetails},...damages()};
  await send(start);
  await send({...start,remaining_km:0.1,truck:{odometer:10001.1}});
  const deliveredDetails={revenue:8260,earnedXp:1,cargoDamage:0,distanceKm:1,deliveryTime:7,autoParked:true,autoLoaded:true};
  const end={...start,gat_job_event:'cancelled',job_latched:false,cargo_name:'',mass_kg:0,remaining_km:0.044,truck:{odometer:10001.2},gameplay:{jobDelivered:false,jobCancelled:true,jobDeliveredDetails:deliveredDetails}};
  delete end.cargo_damage_pct;delete end.trailer_damage_pct;
  const result=await send(end);
  assert.equal(result.mission_event?.type,'delivery_completed',JSON.stringify(result));
  assert.equal(db.sql.prepare("SELECT monthly_completed n FROM profiles WHERE user='biduzao'").get().n,1);
  assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM deliveries WHERE user='biduzao'").get().n,1);
  const row=db.sql.prepare("SELECT distance_km,xp,raw_json FROM deliveries WHERE user='biduzao'").get();
  assert.equal(row.distance_km,1);
  const raw=JSON.parse(row.raw_json);assert.equal(raw.audit.rank_verified,true);
});
