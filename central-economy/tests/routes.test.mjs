import assert from 'node:assert/strict';
import {createHash,randomUUID} from 'node:crypto';
import {LocalDatabase} from '../database.mjs';
import worker from '../worker.js';
const db=new LocalDatabase(':memory:');
db.sql.exec(`CREATE TABLE accounts(user TEXT PRIMARY KEY,role TEXT,disabled INTEGER DEFAULT 0);
CREATE TABLE sessions(token_hash TEXT PRIMARY KEY,user TEXT,created_at TEXT,expires_at TEXT);
CREATE TABLE profiles(user TEXT PRIMARY KEY,current_mission_json TEXT);
CREATE TABLE audit(at TEXT,actor TEXT,action TEXT,target TEXT,details TEXT);
CREATE TABLE telemetry_live(account_user TEXT,updated_at TEXT,telemetry_json TEXT);
CREATE TABLE deliveries(id INTEGER PRIMARY KEY,user TEXT,delivered_at TEXT,raw_json TEXT);`);
for(const [user,role] of [['owner','owner'],['driver','driver'],['other','driver']]){
 db.sql.prepare('INSERT INTO accounts VALUES(?,?,0)').run(user,role);
 db.sql.prepare('INSERT INTO profiles VALUES(?,NULL)').run(user);
 db.sql.prepare('INSERT INTO sessions VALUES(?,?,?,?)').run(createHash('sha256').update(user+'-fixture-token').digest('hex'),user,new Date().toISOString(),new Date(Date.now()+60000).toISOString());
}
async function call(path,user,payload={}){const res=await worker.fetch(new Request('https://api.gatlogets2.com.br/api/economy/'+path,{method:'POST',headers:{'Content-Type':'text/plain',...(user?{Authorization:'Bearer '+user+'-fixture-token'}:{})},body:JSON.stringify(payload)}),{DB:db},{});return {status:res.status,data:await res.json()};}
assert.equal((await call('state')).status,401);
assert.equal((await call('admin/overview','driver')).status,403);
assert.equal((await call('admin/overview','owner')).status,200);
const driver=await call('state','driver');assert.equal(driver.status,200);assert.equal(driver.data.mode,'official');assert.equal(driver.data.gold_balance,0);assert.equal(driver.data.garage.length,0);assert.equal(driver.data.pix_enabled,false);
assert.equal((await call('admin/test-garage','owner',{user:'driver',model_keys:['volvo']})).status,404);
assert.equal((await call('purchase','driver',{op_id:randomUUID(),terms_accepted:true,product_id:'brand:volvo',currency:'pix'})).status,409);
assert.equal((await call('select-truck','driver',{op_id:randomUUID(),model_key:'volvo'})).status,403);
assert.equal((await call('state','other')).data.gold_balance,0);
assert.equal((await call('admin/supplier','driver',{op_id:randomUUID(),enable_pix:true})).status,403);
assert.equal((await call('admin/supplier','owner',{op_id:randomUUID(),enable_pix:true,name:'Fixture'})).status,400);
assert.equal((await call('transfer','driver',{op_id:randomUUID(),recipient:'other',amount:100,recipient_confirmed:true})).status,409);
db.sql.prepare('UPDATE accounts SET disabled=1 WHERE user=?').run('driver');assert.equal((await call('state','driver')).status,401);
console.log('PASS: actual worker HTTP routes enforce login, owner privileges, disabled accounts, supplier gate, ownership, no test grants and wallet isolation.');db.close();
