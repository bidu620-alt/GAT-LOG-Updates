import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,writeFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {pbkdf2Sync,createHash} from 'node:crypto';
import {DatabaseSync} from 'node:sqlite';
import {LocalDatabase,importDatabase,saveBackup,dumpStatements} from './runtime/database.mjs';
import {createCentral} from './runtime/host.mjs';

const schema=new URL('./runtime/schema.sql',import.meta.url),index=new URL('./runtime/migrations/0004_read_efficiency.sql',import.meta.url);
const password='test-password-only',salt='test-salt',hash=pbkdf2Sync(password,salt,140000,32,'sha256').toString('hex');
function dump(){return readFileSync(schema,'utf8')+`\nINSERT INTO accounts(user,password_salt,password_hash,role,created_at,updated_at) VALUES('owner','${salt}','${hash}','owner','2026-09-01','2026-09-01');\nINSERT INTO profiles(user,updated_at) VALUES('owner','2026-09-01');\n`;}
function setup(t){const dir=mkdtempSync(join(tmpdir(),'gat-local-'));t.after(()=>rmSync(dir,{recursive:true,force:true}));const input=join(dir,'full.sql');writeFileSync(input,dump());return{dir,input};}
test('full SQL import keeps password and profile; repeat or unsafe imports cannot replace data',async t=>{
  const {dir,input}=setup(t);const result=await importDatabase(input,dir,schema,index);assert.equal(result.accounts,1);
  const db=new LocalDatabase(join(dir,'central.sqlite'));assert.equal((await db.prepare("SELECT password_hash FROM accounts WHERE user='owner'").first()).password_hash,hash);db.close();
  await assert.rejects(importDatabase(input,dir,schema,index),/Ja existe/);
  const other=join(dir,'bad');writeFileSync(input,dump()+"ATTACH DATABASE 'evil.sqlite' AS evil;");
  await assert.rejects(importDatabase(input,other,schema,index),/nao permitido/);assert.equal(existsSync(join(other,'central.sqlite')),false);
  assert.deepEqual(dumpStatements("-- hi\nINSERT INTO t VALUES('hello; -- world','it''s ok');/*bye*/"),["INSERT INTO t VALUES('hello; -- world','it''s ok')"]);
});
test('HTTP login accepts imported hash, rejects wrong password, survives restart and backup restores',async t=>{
  const {dir,input}=setup(t);await importDatabase(input,dir,schema,index);
  const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const login=pass=>fetch(base+'/api/account/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({user:'owner',password:pass})});
  assert.equal((await login('wrong')).status,401);
  const response=await login(password);assert.equal(response.status,200);const session=await response.json();assert.ok(session.token);
  const health=await (await fetch(base+'/health')).json();assert.equal(health.agent_version,'1.0.39-local');
  const status=await (await fetch(base+'/api/public/service-status')).json();assert.equal(status.paused,false);assert.equal(status.storage,'local-sqlite');
  const large=await fetch(base+'/api/account/login',{method:'POST',body:'x'.repeat(270000)});assert.equal(large.status,413);
  const filename=await saveBackup(db,dir);const restored=new LocalDatabase(filename);
  assert.ok(await restored.prepare('SELECT 1 FROM sessions WHERE token_hash=?').bind(createHash('sha256').update(session.token).digest('hex')).first());restored.close();
});
test('batch failure rolls back every write and allows subsequent requests',async t=>{
  const {dir,input}=setup(t);await importDatabase(input,dir,schema,index);const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  await assert.rejects(db.batch([db.prepare("UPDATE profiles SET xp=500 WHERE user='owner'"),db.prepare('INSERT INTO nonexistent VALUES(1)')]));
  assert.equal((await db.prepare("SELECT xp FROM profiles WHERE user='owner'").first()).xp,0);
});
test('HTTP delivery rollback and simultaneous retries award a trip only once',async t=>{
  const {dir,input}=setup(t);await importDatabase(input,dir,schema,index);const db=new LocalDatabase(join(dir,'central.sqlite'));t.after(()=>db.close());
  db.sql.prepare('INSERT INTO work_catalog(id,position,title) VALUES(?,?,?)').run('custom',1,'Test');
  db.sql.prepare('UPDATE profiles SET current_mission_json=?').run(JSON.stringify({id:'trip-one',catalog_id:'custom',state:'assigned',min_km:500,sequence:1}));
  db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(createHash('sha256').update('device-token').digest('hex'),'owner','owner','test-device-123456',new Date().toISOString(),new Date().toISOString());
  const {server}=createCentral(db);await new Promise(r=>server.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>server.close(r)));
  const base='http://127.0.0.1:'+server.address().port;
  const raw={gat_client_version:'1.0.28',game:{connected:true,paused:false},job_latched:true,job_latch_key:'job-one',cargo_name:'Tijolos',mass_kg:20000,source_city:'A',destination_city:'B',planned_distance_km:600,remaining_km:600,truck:{odometer:10000},cargo_damage_pct:0,truck_engine_damage_pct:0,truck_transmission_damage_pct:0,truck_cabin_damage_pct:0,truck_chassis_damage_pct:0,truck_wheels_damage_pct:0,trailer_damage_pct:0};
  const send=telemetry=>fetch(base+'/api/client/telemetry',{method:'POST',body:JSON.stringify({driver:'owner',device_id:'test-device-123456',token:'device-token',telemetry})});
  assert.equal((await send(raw)).status,200);
  assert.equal((await send({...raw,remaining_km:500,truck_engine_damage_pct:1,truck:{odometer:10100}})).status,200);
  const end={...raw,gat_job_event:'delivered',job_latched:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:10600},gameplay:{jobDeliveredDetails:{distanceKm:600,cargoDamage:0}}};
  const prepare=db.prepare.bind(db);let fail=true;
  db.prepare=query=>{if(fail&&query.startsWith('INSERT INTO telemetry_live')){fail=false;throw Error('simulated disk write error');}return prepare(query);};
  assert.equal((await send(end)).status,500);
  assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM mission_completions').get().n,0);
  assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM deliveries').get().n,0);
  const results=await Promise.all([send(end),send(end)]);for(const response of results)assert.equal(response.status,200);
  assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM deliveries').get().n,1);
  assert.equal(db.sql.prepare('SELECT monthly_completed n FROM profiles').get().n,1);
});
