import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {DatabaseSync} from 'node:sqlite';
import {createHash} from 'node:crypto';
import {DAMAGE_FIELDS,rankingReadiness,advanceRankGuard,restoreDeliveredTrailer} from '../ranking-telemetry.js';
import {cachedRead} from '../read-cache.js';
import {budgetState} from '../budget-guard.js';

// Exercise the assembled production handler against SQLite, without npm dependencies.
// Password hashing imports are unused by these telemetry/read-route tests.
let source=readFileSync(new URL('../worker.js',import.meta.url),'utf8')
  .replace(/^import .* from '@noble\/[^\n]+\n/gm,'')
  .replace(/from '(\.\/[^']+)'/g,(_,p)=>`from '${new URL('../'+p,import.meta.url).href}'`);
const worker=(await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'))).default;
const OriginalDate=Date;
let clock=OriginalDate.parse('2026-09-01T12:00:00Z');
globalThis.Date=class extends OriginalDate{constructor(...args){super(...(args.length?args:[clock]));}static now(){return clock;}};
let sequence=0;
const hash=s=>createHash('sha256').update(s).digest('hex');
function fixture(){
  clock+=1000000;
  const sql=new DatabaseSync(':memory:');
  sql.exec(readFileSync(new URL('../schema.sql',import.meta.url),'utf8'));
  sql.exec(readFileSync(new URL('../migrations/0004_read_efficiency.sql',import.meta.url),'utf8'));
  const user='driver'+(++sequence), token='test-client-'+user, at=new Date().toISOString();
  const mission={id:'mission-'+user,catalog_id:'test',state:'assigned',min_km:500,sequence:1};
  sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run(user,at,at);
  sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run(user,JSON.stringify(mission),at);
  sql.prepare('INSERT INTO work_catalog(id,position,title) VALUES(?,?,?)').run('test',1,'Test');
  sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(hash(token),user,user,'test-device-123456',at,at);
  const queries=[];
  const env={DB:{prepare(query){const statement={args:[],bind(...args){this.args=args;return this;},async first(){queries.push(query);return sql.prepare(query).get(...this.args)||null;},async all(){queries.push(query);return{results:sql.prepare(query).all(...this.args)};},async run(){queries.push(query);const result=sql.prepare(query).run(...this.args);return{meta:{changes:Number(result.changes)}};}};return statement;},async batch(statements){sql.exec('BEGIN');try{const r=[];for(const s of statements)r.push(await s.run());sql.exec('COMMIT');return r;}catch(e){sql.exec('ROLLBACK');throw e;}}}};
  env.GAT_D1_BUDGET=JSON.stringify({date_utc:at.slice(0,10),checked_at:at,rows_read:0,rows_written:0});
  const waits=[];const ctx={waitUntil(p){waits.push(p);}};
  async function send(raw, advance=16000){clock+=advance;const response=await worker.fetch(new Request('https://api.gatlogets2.com.br/api/client/telemetry',{method:'POST',body:JSON.stringify({driver:user,device_id:'test-device-123456',token,telemetry:raw})}),env,ctx);assert.equal(response.status,200);return response.json();}
  return {sql,user,token,env,ctx,queries,waits,send,mission,profile:()=>sql.prepare('SELECT * FROM profiles WHERE user=?').get(user)};
}
function sample(extra={}){
  return {gat_client_version:'1.0.28',game:{connected:true,paused:false},job_latched:true,job_latch_key:'job-one',cargo_name:'Tijolos',mass_kg:20000,source_city:'A',destination_city:'B',planned_distance_km:600,remaining_km:600,truck:{odometer:10000},...Object.fromEntries(Object.values(DAMAGE_FIELDS).map(key=>[key,0])),...extra};
}
function delivery(extra={}){return sample({gat_job_event:'delivered',job_latched:false,cargo_name:'',mass_kg:0,remaining_km:0,truck:{odometer:10600},gameplay:{jobDeliveredDetails:{distanceKm:600,cargoDamage:0}},...extra});}

test('all seven real zero readings pass; missing, null, strings, booleans and negatives do not',()=>{
  assert.equal(rankingReadiness(sample()).eligible,true);
  for(const key of Object.values(DAMAGE_FIELDS))for(const value of [undefined,null,'',false,-1,NaN,Infinity,101,'0']){const r=rankingReadiness(sample({[key]:value}));assert.equal(r.eligible,false,key+':'+value);assert.equal(r.reason,'damage_data_incomplete');}
  for(const v of ['',undefined,'1.0.27','1.0.28fake','999'])assert.equal(rankingReadiness(sample({gat_client_version:v})).eligible,false);
  assert.equal(rankingReadiness(sample({gat_client_version:'1.0.29'})).eligible,true);
  assert.equal(rankingReadiness(sample({game:{connected:false}})).eligible,false);
});
test('current GAT with old TruckSim never earns points, XP or monthly credit, even after updating mid-trip',async()=>{
  const f=fixture(),old=sample();for(const key of Object.values(DAMAGE_FIELDS).filter(k=>k.startsWith('truck_')))delete old[key];
  await f.send(old);await f.send(sample({remaining_km:500}));
  const result=await f.send(delivery());assert.equal(result.mission_event.type,'delivery_rejected');assert.equal(result.mission_event.reason,'damage_data_incomplete');assert.equal(f.profile().xp,0);assert.equal(f.profile().monthly_completed,0);assert.equal(f.sql.prepare('SELECT COUNT(*) n FROM deliveries').get().n,0);
  assert.equal(f.sql.prepare('SELECT COUNT(*) n FROM mission_completions').get().n,0);
  const reset=JSON.parse(f.profile().current_mission_json);assert.equal(reset.state,'assigned');assert.equal(reset.rank_guard,undefined);assert.equal(reset.truck_engine_damage_start_pct,undefined);
  // A new complete trip can now succeed using the same selected work.
  await f.send(sample({job_latch_key:'job-two'}));await f.send(sample({job_latch_key:'job-two',remaining_km:500}));
  assert.equal((await f.send(delivery({job_latch_key:'job-two'}))).mission_event.type,'delivery_completed');assert.equal(f.profile().monthly_completed,1);
});
test('valid delivery awards expected points and detects a single missing mid-trip sample',async()=>{
  const f=fixture();await f.send(sample());await f.send(sample({remaining_km:500}));assert.equal((await f.send(delivery())).mission_event.type,'delivery_completed');
  const audit=JSON.parse(f.sql.prepare('SELECT raw_json FROM deliveries').get().raw_json).audit;assert.equal(audit.gat_points,100);assert.equal(audit.rank_verified,true);
  const g=fixture();await g.send(sample());await g.send(sample({truck_wheels_damage_pct:null,remaining_km:500}),1000);await g.send(sample({remaining_km:400}));assert.equal((await g.send(delivery())).mission_event.type,'delivery_rejected');assert.equal(g.profile().xp,0);
});
test('a real two-minute telemetry gap fails, while a verified legacy trip survives a central update',async()=>{
  const f=fixture();await f.send(sample());await f.send(sample({remaining_km:500}),121000);assert.equal((await f.send(delivery())).mission_event.reason,'telemetry_gap');
  const g=fixture();
  const legacy={...g.mission,state:'active',started_at:'2026-09-01T00:00:00Z',trip_progress_confirmed:true,cargo:'Tijolos',source:'A',destination:'B',weight_kg:20000,planned_distance_km:600,job_latch_key:'job-one'};
  g.sql.prepare('UPDATE profiles SET current_mission_json=? WHERE user=?').run(JSON.stringify(legacy),g.user);
  await g.send(sample({remaining_km:500}),121000);
  const migrated=JSON.parse(g.profile().current_mission_json);assert.equal(migrated.rank_guard?.reason,null);assert.equal(migrated.rank_guard?.migrated_after_server_update,true);
  assert.equal((await g.send(delivery())).mission_event.type,'delivery_completed');assert.equal(g.profile().monthly_completed,1);
});
test('unloaded delivery uses a recent verified trailer and final cargo reading only',async()=>{
  const f=fixture();await f.send(sample());await f.send(sample({remaining_km:500}));const end=delivery();delete end.trailer_damage_pct;delete end.cargo_damage_pct;
  assert.equal((await f.send(end)).mission_event.type,'delivery_completed');
  const g=fixture();await g.send(sample());await g.send(sample({remaining_km:500}));assert.equal((await g.send(end,31000)).mission_event.type,'delivery_rejected');
});
test('unchanged packets are deferred but damage changes and delivery are processed immediately',async()=>{
  const f=fixture();await f.send(sample());f.queries.length=0;
  for(let i=0;i<5;i++)assert.equal((await f.send(sample(),1000)).telemetry_deferred,true);
  assert.equal(f.queries.some(q=>/^(INSERT|UPDATE|DELETE)/.test(q)),false);
  const changed=await f.send(sample({truck_engine_damage_pct:11,remaining_km:500}),1000);assert.notEqual(changed.telemetry_deferred,true);
  await f.send(delivery({truck_engine_damage_pct:11}),1000);
  const audit=JSON.parse(f.sql.prepare('SELECT raw_json FROM deliveries').get().raw_json).audit;assert.equal(audit.truck_engine_damage_delta_pct,11);assert.equal(audit.gat_points,90);
});
test('health and preflight run zero queries; expired sessions are removed only by bounded scheduled cleanup',async()=>{
  const f=fixture();f.sql.prepare('INSERT INTO sessions(token_hash,user,created_at,expires_at) VALUES(?,?,?,?)').run('expired',f.user,'2020-01-01','2020-01-02');
  for(const method of ['GET','OPTIONS'])await worker.fetch(new Request('https://api.gatlogets2.com.br/health',{method}),f.env,f.ctx);
  assert.equal(f.queries.length,0);await worker.scheduled({},f.env,f.ctx);await Promise.all(f.waits);assert.equal(f.sql.prepare('SELECT COUNT(*) n FROM sessions').get().n,0);
});
test('revoked client credentials are rejected even when recent telemetry can be deferred',async()=>{
  const f=fixture();await f.send(sample());f.sql.prepare('UPDATE client_tokens SET revoked_at=?').run(new Date().toISOString());
  const response=await worker.fetch(new Request('https://api.gatlogets2.com.br/api/client/telemetry',{method:'POST',body:JSON.stringify({driver:f.user,device_id:'test-device-123456',token:f.token,telemetry:sample()})}),f.env,f.ctx);
  assert.equal(response.status,401);
});
test('ranking query and profile history use supporting indexes',()=>{
  const f=fixture();const plan=q=>f.sql.prepare('EXPLAIN QUERY PLAN '+q).all().map(x=>x.detail).join(' ');
  assert.match(plan("SELECT * FROM deliveries WHERE substr(delivered_at,1,7)='2026-09'"),/idx_deliveries_month_user/);
  assert.match(plan("SELECT * FROM deliveries WHERE user='x' ORDER BY id DESC LIMIT 100"),/idx_deliveries_user_id/);
  assert.match(plan("SELECT token_hash FROM sessions WHERE expires_at<'2026' ORDER BY expires_at LIMIT 1000"),/idx_sessions_expiry/);
});
test('cached reads coalesce simultaneous requests, isolate users and expire without caching failures',async()=>{
  let n=0;const key='test-'+sequence;const loader=async()=>{n++;return{n};};
  const values=await Promise.all(Array.from({length:20},()=>cachedRead(key,15,loader)));assert.equal(n,1);values[0].n=900;assert.equal((await cachedRead(key,15,loader)).n,1);
  await cachedRead(key+'other',15,loader);assert.equal(n,2);clock+=16000;await cachedRead(key,15,loader);assert.equal(n,3);
  await assert.rejects(cachedRead('failure',10,async()=>{throw Error('test')}));assert.equal(await cachedRead('failure',10,async()=>42),42);
});
test('budget guard pauses before quota and after missing or stale checks, including at UTC rollover',async()=>{
  const f=fixture(),at=new Date().toISOString(),snapshot={date_utc:at.slice(0,10),checked_at:at,rows_read:4000000,rows_written:0};
  f.env.GAT_D1_BUDGET=JSON.stringify(snapshot);
  assert.equal(budgetState(f.env).reason,'daily_budget');
  for(const path of ['/api/public/ranking','/api/account/login','/api/client/telemetry']){
    const r=await worker.fetch(new Request('https://example.com'+path,{method:path.includes('/public/')?'GET':'POST',body:path.includes('/public/')?undefined:'{}'}),f.env,f.ctx);
    assert.equal(r.status,503);
  }
  await worker.scheduled({},f.env,f.ctx);await Promise.all(f.waits);assert.equal(f.queries.length,0);
  const notice=await worker.fetch(new Request('https://example.com/api/public/notice'),f.env,f.ctx);assert.equal((await notice.json()).enabled,true);assert.equal(f.queries.length,0);
  snapshot.rows_read=0;snapshot.rows_written=80000;f.env.GAT_D1_BUDGET=JSON.stringify(snapshot);assert.equal(budgetState(f.env).paused,true);
  snapshot.rows_written=0;f.env.GAT_D1_BUDGET=JSON.stringify(snapshot);assert.equal(budgetState(f.env).paused,false);
  clock+=20*60000;assert.equal(budgetState(f.env).reason,'budget_check_pending');
  delete f.env.GAT_D1_BUDGET;assert.equal(budgetState(f.env).paused,true);
  f.env.GAT_D1_BUDGET=JSON.stringify(snapshot);clock=Date.parse(snapshot.date_utc+'T00:00:00Z')+86400000;assert.equal(budgetState(f.env).paused,true);
});
