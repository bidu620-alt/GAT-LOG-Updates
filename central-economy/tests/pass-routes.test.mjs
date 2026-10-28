import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash,randomUUID} from 'node:crypto';
import worker from '../worker.js';
import {LocalDatabase} from '../database.mjs';
import {ensureEconomy} from '../economy-official.js';

test('public config + authenticated monthly state and Gold purchase use real worker routes',async()=>{
 const realNow=Date.now;Date.now=()=>Date.parse('2026-10-27T12:00:00Z');
 const db=new LocalDatabase(':memory:');db.sql.exec(readFileSync(new URL('../schema.sql',import.meta.url),'utf8'));
 const token='fixture-pass-session',user='route_tester',at=new Date().toISOString();
 db.sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run(user,at,at);
 db.sql.prepare('INSERT INTO profiles(user,updated_at) VALUES(?,?)').run(user,at);
 db.sql.prepare('INSERT INTO sessions VALUES(?,?,?,?)').run(createHash('sha256').update(token).digest('hex'),user,at,'2099-01-01T00:00:00Z');
 ensureEconomy(db);db.sql.prepare('INSERT INTO eco_wallets(user,gold_units,updated_at) VALUES(?,?,?)').run(user,200000,at);
 const call=(path,body)=>worker.fetch(new Request('https://api.bidulog.com.br'+path,{method:body?'POST':'GET',headers:{Origin:'https://bidulog.com.br',...(body?{'Content-Type':'text/plain;charset=UTF-8'}:{})},...(body?{body:JSON.stringify(body)}:{})}),{DB:db},{waitUntil:()=>{}});
 try{
  const pub=await call('/api/passe/config');assert.equal(pub.status,200);assert.equal(pub.headers.get('Access-Control-Allow-Origin'),'https://bidulog.com.br');assert.equal((await pub.json()).pass.product_id,'pass_2026_11');
  const missing=await call('/api/economy/pass-state',{});assert.equal(missing.status,401);
  const before=await call('/api/economy/pass-state',{token,month:'2026-11'});assert.equal(before.status,200);assert.equal((await before.json()).owned,false);
  const buy=await call('/api/economy/purchase',{token,op_id:randomUUID(),product_id:'pass_2026_11',currency:'gold',terms_accepted:true});assert.equal(buy.status,200);assert.equal((await buy.json()).pending_payment,false);
  const after=await(await call('/api/economy/pass-state',{token,month:'2026-11',user:'some_other_user'})).json();assert.equal(after.owned,true);assert.equal(after.progress.total_km,0);assert.equal(db.sql.prepare('SELECT gold_units FROM eco_wallets WHERE user=?').get(user).gold_units,0);
  const wrong=await call('/api/economy/pass-state',{token,month:'2026-12'});assert.equal(wrong.status,404);
 }finally{db.close();Date.now=realNow;}
});
