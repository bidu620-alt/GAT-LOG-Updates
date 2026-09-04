import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {LocalDatabase} from './runtime/database.mjs';
import {createCentral} from './runtime/host.mjs';

const runtime=path.resolve('server-local/runtime');
const worker=fs.readFileSync(path.join(runtime,'worker.js'),'utf8');
const schema=fs.readFileSync(path.join(runtime,'schema.sql'),'utf8');
const host=fs.readFileSync(path.join(runtime,'host.mjs'),'utf8');

test('1.0.53 grava open_trips antes do alias com foreign key',()=>{
  assert.match(worker,/const VERSION='1\.0\.53-local'/);
  assert.match(worker,/canonical=await saveOpenJourney\(env,user,current,raw,t,'active'\)\|\|canonical;\s*if\(observed&&canonical\)await openJourneyAlias\(env,user,observed,canonical,t\);/);
  assert.doesNotMatch(worker,/if\(observed\)\{current\.trip_id=observed;current\.job_latch_key=observed;await openJourneyAlias\(env,user,observed,canonical,t\)\}/);
});

test('schema e startup garantem tabelas de viagens em banco existente',()=>{
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trips/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trip_aliases/);
  assert.match(schema,/FOREIGN KEY\(trip_id\) REFERENCES open_trips\(trip_id\) ON DELETE CASCADE/);
  assert.match(host,/db\.sql\.exec\(readFileSync\(join\(here,'schema\.sql'\),'utf8'\)\)/);
});

test('pacote de viagem ja ativa nao cai em FK e atualiza telemetry_live',async t=>{
  const dir=mkdtempSync(join(tmpdir(),'gat-v153-'));
  t.after(()=>rmSync(dir,{recursive:true,force:true}));
  const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  db.sql.exec(schema);
  const now='2026-09-04T01:00:00.000Z';
  db.sql.prepare("INSERT INTO accounts(user,role,disabled,created_at,updated_at) VALUES(?,?,?,?,?)").run('xuxa','driver',0,now,now);
  const mission={id:'mission-fk',catalog_id:'custom',sequence:1,state:'active',cargo:'Chocolate',source:'A',destination:'B',weight_kg:20000,planned_distance_km:600,game_name:'ets2',context_key:'ets2|base',journey_fingerprint:'ets2|base|chocolate|a|b|600|20000',started_at:now};
  db.sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run('xuxa',JSON.stringify(mission),now);
  const token='device-token-v153',device='device-v153-123456';
  db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(createHash('sha256').update(token).digest('hex'),'xuxa','xuxa',device,now,now);
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const raw={gat_client_version:'1.0.31',gat_game:'ets2',gat_map:'base',gat_trip_id:'observed-trip-v153',game:{connected:true,paused:false},job_latched:true,job_latch_key:'observed-trip-v153',cargo_name:'Chocolate',mass_kg:20000,source_city:'A',destination_city:'B',planned_distance_km:600,remaining_km:590,cargo_damage_pct:0,truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,trailer_damage_pct:0};
  const response=await fetch(base+'/api/client/telemetry',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({driver:'xuxa',device_id:device,token,telemetry:raw})});
  assert.equal(response.status,200,await response.text());
  assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM telemetry_live WHERE driver='xuxa'").get().n,1);
  const parent=db.sql.prepare("SELECT trip_id,user,state FROM open_trips WHERE trip_id='mission-fk'").get();
  assert.equal(parent?.user,'xuxa');
  const alias=db.sql.prepare("SELECT trip_id,user FROM open_trip_aliases WHERE observed_trip_id='observed-trip-v153'").get();
  assert.equal(alias?.trip_id,'mission-fk');
  assert.equal(alias?.user,'xuxa');
});
