import test from 'node:test';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {createHash,createHmac,randomUUID} from 'node:crypto';
import {MONTHLY_PASS,PASS_CARGO_IDS,monthlyPassConfig,monthlyPassProduct,monthlyPassState,assertMonthlyPassPurchase} from '../monthly-pass.js';
import {ensureEconomy,economyAction,state} from '../economy-official.js';
import {createMpIntegration} from '../mercadopago.mjs';

const START=Date.parse(MONTHLY_PASS.starts_at),END=Date.parse(MONTHLY_PASS.ends_at),PRESALE=START-86400000;
const alice={user:'alice',role:'driver'},bob={user:'bob',role:'driver'};
function fixture(){
 const db={sql:new DatabaseSync(':memory:')};
 db.sql.exec(`CREATE TABLE accounts(user TEXT PRIMARY KEY,role TEXT,disabled INTEGER DEFAULT 0);
 CREATE TABLE profiles(user TEXT PRIMARY KEY,current_mission_json TEXT);
 CREATE TABLE audit(at TEXT,actor TEXT,action TEXT,target TEXT,details TEXT);
 CREATE TABLE telemetry_live(account_user TEXT,updated_at TEXT,telemetry_json TEXT);
 CREATE TABLE sessions(token_hash TEXT PRIMARY KEY,user TEXT,created_at TEXT,expires_at TEXT);
 CREATE TABLE deliveries(id INTEGER PRIMARY KEY,user TEXT,weight_kg REAL,distance_km REAL,delivered_at TEXT,raw_json TEXT);`);
 for(const s of [alice,bob]){db.sql.prepare('INSERT INTO accounts(user,role) VALUES(?,?)').run(s.user,s.role);db.sql.prepare('INSERT INTO profiles(user) VALUES(?)').run(s.user);db.sql.prepare('INSERT INTO sessions VALUES(?,?,?,?)').run(createHash('sha256').update(s.user+'-fixture-token').digest('hex'),s.user,'2026-01-01','2099-01-01');}
 ensureEconomy(db);state(db,alice);state(db,bob);
 db.sql.prepare('UPDATE eco_wallets SET gold_units=500000').run();
 return db;
}
const buy=(db,s=alice,extra={})=>economyAction(db,s,'purchase',{product_id:MONTHLY_PASS.product_id,currency:'gold',terms_accepted:true,op_id:randomUUID(),...extra});
function own(db,user='alice'){db.sql.prepare('INSERT INTO eco_entitlements VALUES(?,?,?,?,?,?)').run(user,MONTHLY_PASS.product_id,MONTHLY_PASS.title,'pass','2026-10-27T12:00:00Z','fixture');}
let deliverySeq=0;
function trip(db,{user='alice',at=START+1000,cargo='logs',weight=10000,km=100,status='delivered',raw={}}={}){
 db.sql.prepare('INSERT INTO deliveries VALUES(?,?,?,?,?,?)').run(++deliverySeq,user,weight,km,typeof at==='string'?at:new Date(at).toISOString(),JSON.stringify({mission:{state:status,cargo_id:cargo},...raw}));
}
async function clock(at,fn){const old=Date.now;Date.now=()=>at;try{return await fn()}finally{Date.now=old}}

test('published window is immutable; start included, 23:50 excluded; presale enabled',()=>{
 assert.equal(monthlyPassConfig(PRESALE).phase,'presale');assert.equal(monthlyPassConfig(PRESALE).purchase_available,true);
 assert.equal(monthlyPassConfig(START).phase,'active');assert.equal(monthlyPassConfig(END-1).phase,'active');
 assert.equal(monthlyPassConfig(END).phase,'closed');assert.equal(monthlyPassConfig(END).purchase_available,false);
 assert.throws(()=>assertMonthlyPassPurchase(monthlyPassProduct(),END),/pass_closed/);
 assert.equal(monthlyPassProduct().gold_units,200000);assert.equal(monthlyPassProduct().cash_centavos,500);
});
test('cargo lists are explicit and disjoint, never display-name fuzzy matching',()=>{
 const ids=Object.values(PASS_CARGO_IDS).flat();assert.equal(ids.length,new Set(ids).size);
 // The published catalog library explicitly assigns carbn_pwdr_c to general cargo.
 assert.equal(PASS_CARGO_IDS.carga_seca.includes('carbn_pwdr_c'),true);assert.equal(PASS_CARGO_IDS.granel.includes('carbn_pwdr_c'),false);assert.equal(PASS_CARGO_IDS.cegonheiro.includes('carrots'),false);
 assert.equal(PASS_CARGO_IDS.maquinario.includes('wood_chipper'),true);
});
test('presale Gold purchase charges exactly 2000 once, grants entitlement, no XP/reward',async()=>clock(PRESALE,()=>{
 const db=fixture(),op=randomUUID(),payload={op_id:op};const a=buy(db,alice,payload),again=buy(db,alice,payload);
 assert.equal(a.order_id,again.order_id);assert.equal(state(db,alice).gold_balance,3000);
 assert.equal(state(db,alice).entitlements.filter(x=>x.product_id===MONTHLY_PASS.product_id).length,1);
 assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM eco_ledger').get().n,1);
 assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM eco_rewards').get().n,0);
 assert.throws(()=>buy(db),/product_already_owned/);assert.throws(()=>buy(db,bob,{currency:'pix'}),/payment_method_unavailable/);
 assert.throws(()=>buy(db,bob,{currency:'gold',op_id:op,terms_accepted:false}),/terms_required/);
 const pre=monthlyPassState(db,alice,'2026-11',PRESALE);assert.equal(pre.owned,true);assert.equal(pre.progress.total_km,0);assert.equal(pre.rewards_defined,false);db.sql.close();
}));
test('Gold grant failure and insufficient balance roll back money and order',async()=>clock(PRESALE,()=>{
 const db=fixture();db.sql.exec("CREATE TRIGGER fixture_fail BEFORE INSERT ON eco_entitlements BEGIN SELECT RAISE(ABORT,'fixture grant failure'); END;");
 assert.throws(()=>buy(db),/fixture grant failure/);assert.equal(state(db,alice).gold_balance,5000);assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM eco_orders').get().n,0);assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM eco_ledger').get().n,0);
 db.sql.exec('DROP TRIGGER fixture_fail');db.sql.prepare('UPDATE eco_wallets SET gold_units=199999 WHERE user=?').run('alice');
 assert.throws(()=>buy(db),/insufficient_balance/);assert.equal(db.sql.prepare('SELECT COUNT(*) n FROM eco_entitlements').get().n,0);db.sql.close();
}));
test('closed pass rejects new charges, ownership persists and no period reset',async()=>clock(END,()=>{
 const db=fixture();assert.throws(()=>buy(db),/pass_closed/);assert.equal(state(db,alice).gold_balance,5000);own(db);assert.equal(monthlyPassState(db,alice,'2026-11',END+1).owned,true);assert.equal(monthlyPassState(db,alice,'2026-11',END+1).pass.phase,'closed');db.sql.close();
}));
test('all monthly history >100 deliveries, boundaries, offsets, >40t and unknown IDs',()=>{
 const db=fixture();own(db);
 // Early target trips must survive 150 newer trips.
 for(const cargo of ['logs','car_d','beef_meat','electronics','excavator','wheat','ore','large_cont'])for(let i=0;i<3;i++)trip(db,{at:START+1000+i,cargo,weight:i===0?40001:40000,km:100});
 for(let i=0;i<150;i++)trip(db,{at:START+5000+i,cargo:'unmapped-official',km:10});
 trip(db,{at:START,km:100,cargo:'cargo.logs',weight:40000});
 trip(db,{at:START-1,km:99999});trip(db,{at:END,km:99999});trip(db,{at:END+1000,km:99999});
 trip(db,{at:END-1,km:123,cargo:'logs'});trip(db,{at:'2026-11-01T00:00:00-03:00',km:111,cargo:'logs'});
 trip(db,{at:START+1,km:99999,status:'cancelled'});trip(db,{at:START+1,km:99999,user:'bob'});trip(db,{at:START+1,km:0});trip(db,{at:START+1,km:-1});
 const r=monthlyPassState(db,alice,'2026-11',END+1000);
 assert.equal(r.progress.total_deliveries,177);assert.equal(r.progress.total_km,4234);assert.equal(r.progress.completed_goals,9);
 assert.equal(r.progress.goals.find(g=>g.id==='rodagem_bruta').current,4234);assert.equal(r.progress.goals.find(g=>g.id==='peso_pesado').current,3);
 for(const id of Object.keys(PASS_CARGO_IDS))assert.equal(r.progress.goals.find(g=>g.id===id).completed,true);
 assert.equal(monthlyPassState(db,bob,'2026-11',END).progress.total_km,0);assert.throws(()=>monthlyPassState(db,alice,'2026-12',END),/pass_not_found/);
 assert.equal(monthlyPassState(db,alice,'2026-11',PRESALE).progress.total_deliveries,0);db.sql.close();
});
test('exact 40000kg does not count; future trips and display names cannot award goals',()=>{
 const db=fixture();own(db);for(let i=0;i<3;i++){trip(db,{km:50,at:START+1,weight:40000,cargo:'unknown'});trip(db,{km:50,at:END-1,weight:50000,cargo:'logs'});}
 const r=monthlyPassState(db,alice,'2026-11',START+2);assert.equal(r.progress.total_deliveries,3);assert.equal(r.progress.goals.find(g=>g.id==='peso_pesado').current,0);assert.equal(r.progress.goals.find(g=>g.id==='madeireiro').current,0);db.sql.close();
});

const config={enabled:true,mode:'production',accessToken:'fixture-only-not-a-real-token',webhookSecret:'fixture-secret-for-testing',collectorId:'123'};
function mpFixture(){
 const db=fixture(),calls=[],payments={},merchants={};let queue=Promise.resolve();
 const exclusive=fn=>{const r=queue.then(fn);queue=r.catch(()=>{});return r;};
 const fetchImpl=async(url,opt)=>{assert.equal(db.sql.isTransaction,false);calls.push({url,body:opt.body?JSON.parse(opt.body):null});
  if(url.endsWith('/checkout/preferences')){const b=JSON.parse(opt.body);assert.equal(b.items[0].unit_price,5);assert.match(b.back_urls.success,/\/passe\/2026-11\/\?mp=success$/);assert.equal(opt.headers['X-Idempotency-Key'],b.external_reference);const id='pref-'+b.external_reference;return Response.json({id,collector_id:123,init_point:'https://www.mercadopago.com.br/checkout/v1/redirect?pref_id='+id});}
  const id=url.split('/').at(-1);const obj=url.includes('/merchant_orders/')?merchants[id]:payments[id];return Response.json(obj||{},{status:obj?200:404});
 };
 const handle=createMpIntegration(db,{exclusive,fetchImpl,getConfig:()=>config});
 const request=(path,body={},user='alice')=>handle(new Request('https://api.bidulog.com.br'+path,{method:'POST',headers:{Authorization:'Bearer '+user+'-fixture-token',Origin:'https://bidulog.com.br','Content-Type':'application/json'},body:JSON.stringify(body)}));
 const buy=(op=randomUUID(),user='alice')=>request('/api/mp/purchase',{product_id:MONTHLY_PASS.product_id,terms_accepted:true,op_id:op},user);
 const signed=(id='555',over={})=>{const ts=String(Date.now()),rid='fixture-request',sig=createHmac('sha256',config.webhookSecret).update(`id:${id};request-id:${rid};ts:${ts};`).digest('hex');return handle(new Request('https://api.bidulog.com.br/webhooks/mercadopago?data.id='+id,{method:'POST',headers:{'x-signature':`ts=${ts},v1=${sig}`,'x-request-id':rid,...over},body:JSON.stringify({type:'payment',data:{id}})}));};
 const approve=(oid,over={})=>{payments['555']={id:555,live_mode:true,collector_id:123,currency_id:'BRL',external_reference:oid,status:'approved',transaction_amount:5,date_approved:new Date(PRESALE).toISOString(),order:{id:'777'},...over};merchants['777']={preference_id:'pref-'+oid,external_reference:oid,payments:[{id:555}]};};
 return{db,calls,request,buy,signed,approve,payments,merchants,handle};
}
test('MP presale uses fixed R$5, concurrent/idempotent order, approved entitlement once',async()=>clock(PRESALE,async()=>{
 const f=mpFixture(),op=randomUUID();const rs=await Promise.all([f.buy(op),f.buy(op)]);const a=await rs[0].json(),b=await rs[1].json();assert.equal(a.order_id,b.order_id);assert.equal(f.calls.length,1);
 assert.equal(f.db.sql.prepare('SELECT amount_centavos FROM eco_orders').get().amount_centavos,500);assert.equal(state(f.db,alice).gold_balance,5000);
 f.approve(a.order_id);assert.equal((await f.signed()).status,200);assert.equal((await f.signed()).status,200);assert.equal(state(f.db,alice).entitlements.filter(x=>x.product_id===MONTHLY_PASS.product_id).length,1);
 assert.equal(f.db.sql.prepare('SELECT COUNT(*) n FROM eco_mp_payments').get().n,1);assert.equal((await f.buy()).status,409);f.db.sql.close();
}));
for(const [name,override] of Object.entries({amount:{transaction_amount:4.99},currency:{currency_id:'USD'},collector:{collector_id:456},sandbox:{live_mode:false},pending:{status:'pending'}}))test('MP '+name+' cannot activate pass',async()=>clock(PRESALE,async()=>{
 const f=mpFixture(),b=await(await f.buy()).json();f.approve(b.order_id,override);await f.signed();assert.equal(state(f.db,alice).entitlements.length,0);f.db.sql.close();
}));
test('MP forged webhook and wrong preference cannot activate pass',async()=>clock(PRESALE,async()=>{
 const f=mpFixture(),b=await(await f.buy()).json();f.approve(b.order_id);assert.equal((await f.signed('555',{'x-signature':'ts=1,v1=bad'})).status,401);f.merchants['777'].preference_id='wrong';assert.equal((await f.signed()).status,422);assert.equal(state(f.db,alice).entitlements.length,0);f.db.sql.close();
}));
test('MP late webhook honors approval time, late payment requires support; expired links fail',async()=>{
 const f=mpFixture(),b=await clock(PRESALE,async()=>await(await f.buy()).json());f.approve(b.order_id,{date_approved:new Date(END-1).toISOString()});
 await clock(END+1000,async()=>{assert.equal((await f.signed()).status,200);assert.equal(state(f.db,alice).entitlements.length,1);assert.equal((await f.buy(randomUUID(),'bob')).status,409);});f.db.sql.close();
 const g=mpFixture(),o=await clock(PRESALE,async()=>await(await g.buy()).json());g.approve(o.order_id,{date_approved:new Date(END).toISOString()});
 await clock(END+1000,async()=>{assert.equal((await(await g.signed()).json()).review,true);assert.equal(state(g.db,alice).entitlements.length,0);assert.equal(g.db.sql.prepare('SELECT COUNT(*) n FROM eco_support').get().n,1);assert.equal((await g.request('/api/mp/preference',{orderId:o.order_id})).status,409);});g.db.sql.close();
});
test('MP entitlement failure rolls back and retried webhook succeeds',async()=>clock(PRESALE,async()=>{
 const f=mpFixture(),b=await(await f.buy()).json();f.approve(b.order_id);f.db.sql.exec("CREATE TRIGGER fixture_fail BEFORE INSERT ON eco_entitlements BEGIN SELECT RAISE(ABORT,'fixture grant failure'); END;");
 assert.equal((await f.signed()).status,500);assert.equal(f.db.sql.prepare('SELECT payment_id FROM eco_orders').get().payment_id,null);assert.equal(f.db.sql.prepare('SELECT COUNT(*) n FROM eco_mp_payments').get().n,0);
 f.db.sql.exec('DROP TRIGGER fixture_fail');assert.equal((await f.signed()).status,200);assert.equal(state(f.db,alice).entitlements.length,1);f.db.sql.close();
}));
test('pass-state route uses authenticated user only; other month rejected',()=>{
 const db=fixture();own(db);assert.equal(economyAction(db,alice,'pass-state',{month:'2026-11',user:'bob'}).owned,true);assert.equal(economyAction(db,bob,'pass-state',{month:'2026-11',user:'alice'}).owned,false);assert.throws(()=>economyAction(db,alice,'pass-state',{month:'2026-12'}),/pass_not_found/);db.sql.close();
});
