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

test('Biduzao admin: qualquer carga, menos de 1 km, sem danos/version guard e rota/trabalho repetidos contam',async t=>{
  const dir=mkdtempSync(join(tmpdir(),'gat-admin-test-'));folders.push(dir);
  const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  db.sql.exec(schema);db.sql.exec(migration);
  const now=new Date().toISOString(),token='test-biduzao-token';
  db.sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run('biduzao',now,now);
  db.sql.prepare('INSERT INTO work_catalog(id,position,title,compatible_cargos_json) VALUES(?,?,?,?)').run('timber',5,'Madeira e toras','["Toras"]');
  db.sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run('biduzao',JSON.stringify({id:'admin-short-trip',catalog_id:'timber',state:'assigned',min_km:500,sequence:5,title:'Madeira e toras',xp_only:false}),now);
  db.sql.prepare('INSERT INTO work_completed(user,work_id,month_key,completed_at) VALUES(?,?,?,?)').run('biduzao','timber',now.slice(0,7),now);
  db.sql.prepare('INSERT INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').run('biduzao',now.slice(0,7),'a>a','A','A',now);
  db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(tokenHash(token),'biduzao','biduzao','device-biduzao-123456',now,now);
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const send=async telemetry=>{
    const r=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'biduzao',device_id:'device-biduzao-123456',token,telemetry})});
    assert.equal(r.status,200);return r.json();
  };

  const staleDetails={revenue:12000,earnedXp:100,cargoDamage:0,distanceKm:600,deliveryTime:30};
  const start={
    gat_client_version:'1.0.20',game:{connected:true,paused:false},job_latched:true,job_latch_key:'admin-one',gat_job_event:'',
    cargo_name:'Carga totalmente fora do catalogo',mass_kg:123,source_city:'A',destination_city:'A',planned_distance_km:0.2,remaining_km:0.2,
    truck:{odometer:10000},gameplay:{jobDelivered:false,jobCancelled:false,jobDeliveredDetails:staleDetails}
  };
  const started=await send(start);
  assert.notEqual(started.mission_event?.reason,'distance_below_minimum',JSON.stringify(started));

  // Camera zero: nenhuma progressao real, pacote final ainda vem como cancelado pelo TruckSim,
  // sem os sete danos e com versao abaixo do minimo. O comprovante da entrega mudou.
  const deliveredDetails={revenue:8260,earnedXp:1,cargoDamage:0,distanceKm:0,deliveryTime:1,autoParked:true,autoLoaded:true};
  const end={...start,gat_job_event:'cancelled',job_latched:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:10000},gameplay:{jobDelivered:false,jobCancelled:true,jobDeliveredDetails:deliveredDetails}};
  const result=await send(end);
  assert.equal(result.mission_event?.type,'delivery_completed',JSON.stringify(result));
  const profile=db.sql.prepare("SELECT monthly_completed,total_deliveries,points,current_mission_json FROM profiles WHERE user='biduzao'").get();
  assert.equal(profile.monthly_completed,1);
  assert.equal(profile.total_deliveries,1);
  assert.equal(profile.points,100);
  assert.equal(profile.current_mission_json,null);
  const row=db.sql.prepare("SELECT distance_km,raw_json FROM deliveries WHERE user='biduzao'").get();
  assert.ok(row.distance_km<1,row.distance_km);
  const raw=JSON.parse(row.raw_json);
  assert.equal(raw.audit.admin_test_mode,true);
  assert.equal(raw.audit.rank_verified,false);
  assert.equal(raw.audit.gat_points,100);
});
