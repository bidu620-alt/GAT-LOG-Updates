import {createHash,createHmac,timingSafeEqual,randomUUID} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ensureEconomy,mpProduct,mpAlreadyOwned,mpFulfil} from './economy-official.js';
import {MONTHLY_PASS,assertMonthlyPassPurchase,monthlyPassPaymentReason} from './monthly-pass.js';

const ORIGINS=new Set(['https://bidulog.com.br','https://www.bidulog.com.br']);
const SITE='https://bidulog.com.br';
export const WEBHOOK='https://api.bidulog.com.br/webhooks/mercadopago';
const error=(status,code)=>{throw Object.assign(new Error(code),{status})};
const sha=s=>createHash('sha256').update(s).digest('hex');
const uuid=v=>typeof v==='string'&&/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(v);
export function cents(value){
 const m=String(value).match(/^(\d{1,9})(?:\.(\d{1,2}))?$/);
 if(!m)return null;const n=Number(m[1])*100+Number(((m[2]||'')+'00').slice(0,2));
 return Number.isSafeInteger(n)&&n>0?n:null;
}
export function verifySignature(headers,id,secret,now=Date.now()){
 const sig=headers.get('x-signature')||'',request=headers.get('x-request-id')||'';
 if(!secret||!request||request.length>200||!/^\d{1,30}$/.test(id))return false;
 const fields={};for(const part of sig.split(',')){const m=part.trim().match(/^(ts|v1)=([^=]+)$/);if(m){if(fields[m[1]])return false;fields[m[1]]=m[2];}}
 if(!/^\d{10,13}$/.test(fields.ts||'')||!/^[a-f0-9]{64}$/i.test(fields.v1||''))return false;
 const ts=Number(fields.ts)*(fields.ts.length===10?1000:1);
 if(Math.abs(now-ts)>10*60*1000)return false;
 const digest=createHmac('sha256',secret).update(`id:${id};request-id:${request};ts:${fields.ts};`).digest();
 return timingSafeEqual(digest,Buffer.from(fields.v1,'hex'));
}
export function readConfig(){
 try{
  const p=process.env.BIDULOG_MP_CONFIG||join(process.env.LOCALAPPDATA||'.','BiduLog','Secrets','mercadopago.json');
  return JSON.parse(readFileSync(p,'utf8').replace(/^\uFEFF/,''));
 }catch{return {enabled:false};}
}
function ready(c){return c.enabled===true&&c.mode==='production'&&typeof c.accessToken==='string'&&c.accessToken.length>20&&typeof c.webhookSecret==='string'&&c.webhookSecret.length>=16&&/^\d+$/.test(String(c.collectorId||''));}
function schema(db){
 ensureEconomy(db);db.sql.exec(`CREATE TABLE IF NOT EXISTS eco_mp_checkouts(order_id TEXT PRIMARY KEY REFERENCES eco_orders(id),op_id TEXT NOT NULL,user TEXT NOT NULL,preference_id TEXT UNIQUE,checkout_url TEXT,collector_id TEXT NOT NULL,live_mode INTEGER NOT NULL,created_at TEXT NOT NULL,UNIQUE(user,op_id));
 CREATE TABLE IF NOT EXISTS eco_mp_payments(payment_id TEXT PRIMARY KEY,order_id TEXT NOT NULL REFERENCES eco_orders(id),status TEXT NOT NULL,amount_centavos INTEGER NOT NULL,updated_at TEXT NOT NULL,reason TEXT);
 CREATE INDEX IF NOT EXISTS eco_mp_payments_order ON eco_mp_payments(order_id);`);
}
function session(db,req){
 const h=req.headers.get('authorization')||'';const token=/^Bearer\s+(.+)$/i.exec(h)?.[1];
 if(!token||token.length>512)error(401,'invalid_session');
 const s=db.sql.prepare('SELECT a.user,a.role,a.disabled,s.expires_at FROM sessions s JOIN accounts a ON a.user=s.user WHERE s.token_hash=?').get(sha(token));
 if(!s||s.disabled||Date.parse(s.expires_at)<=Date.now())error(401,'invalid_session');return s;
}
function transaction(db,fn){db.sql.exec('SAVEPOINT mp_payment');try{const r=fn();db.sql.exec('RELEASE mp_payment');return r;}catch(e){db.sql.exec('ROLLBACK TO mp_payment; RELEASE mp_payment');throw e;}}
function audit(db,order,action,details){db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(new Date().toISOString(),'mercadopago',action,order.user,JSON.stringify({order_id:order.id,...details}));}
function review(db,order,id,reason){db.sql.prepare('INSERT OR IGNORE INTO eco_support(id,user,order_id,reason,created_at) VALUES(?,?,?,?,?)').run('mp-review-'+id,order.user,order.id,'Mercado Pago: pagamento '+id+' requer atendimento ('+reason+'). Confira o extrato do provedor antes de qualquer ajuste.',new Date().toISOString());}
function checkoutUrl(raw){try{const u=new URL(raw);return u.protocol==='https:'&&['www.mercadopago.com.br','mercadopago.com.br'].includes(u.hostname)?u.href:null;}catch{return null;}}
function payable(db,id,user){const o=db.sql.prepare('SELECT * FROM eco_orders WHERE id=? AND user=?').get(id,user);if(!o)error(404,'order_not_found');if(o.currency!=='mercadopago'||o.status!=='pending_payment')error(409,'order_not_payable');return o;}

export function createMpIntegration(db,{exclusive,getConfig=readConfig,fetchImpl=fetch}={}){
 if(!exclusive)throw Error('exclusive required');
 const inflight=new Map(),rates=new Map();let initialized=false;
 async function locked(fn){return exclusive(()=>{if(!initialized){schema(db);initialized=true;}return fn();});}
 async function api(path,c,{method='GET',body,key}={}){
  let r;try{r=await fetchImpl('https://api.mercadopago.com'+path,{method,signal:AbortSignal.timeout(9000),headers:{Authorization:'Bearer '+c.accessToken,'Content-Type':'application/json',...(key?{'X-Idempotency-Key':key}:{})},...(body?{body:JSON.stringify(body)}:{})});}catch{error(503,'mp_unavailable');}
  if(!r.ok)error(503,'mp_unavailable');try{return await r.json();}catch{error(503,'mp_invalid_response');}
 }
 async function preference(id,user,c){
  if(inflight.has(id))return inflight.get(id);
  const task=(async()=>{
   const snap=await locked(()=>{const o=payable(db,id,user),ch=db.sql.prepare('SELECT * FROM eco_mp_checkouts WHERE order_id=?').get(id);assertMonthlyPassPurchase(mpProduct(db,o.product_id,true));if(!ch||ch.collector_id!==String(c.collectorId))error(409,'checkout_account_changed');return {o,ch};});
   if(snap.ch.preference_id&&checkoutUrl(snap.ch.checkout_url))return {ok:true,order_id:id,checkout_url:snap.ch.checkout_url};
   const returnPage=snap.o.product_id===MONTHLY_PASS.product_id?'/passe/2026-11/':'/economia.html';
   const returnHash=snap.o.product_id===MONTHLY_PASS.product_id?'':'#carteira';
   const pref=await api('/checkout/preferences',c,{method:'POST',key:id,body:{items:[{id:snap.o.product_id,title:snap.o.title,currency_id:'BRL',quantity:1,unit_price:snap.o.amount_centavos/100}],external_reference:id,metadata:{bidulog_order_id:id},back_urls:{success:SITE+returnPage+'?mp=success'+returnHash,pending:SITE+returnPage+'?mp=pending'+returnHash,failure:SITE+returnPage+'?mp=failure'+returnHash},auto_return:'approved',notification_url:WEBHOOK}});
   const url=checkoutUrl(pref.init_point);if(!pref.id||!url||String(pref.collector_id)!==String(c.collectorId))error(503,'mp_invalid_response');
   await locked(()=>{payable(db,id,user);db.sql.prepare('UPDATE eco_mp_checkouts SET preference_id=?,checkout_url=? WHERE order_id=?').run(String(pref.id),url,id);});
   return {ok:true,order_id:id,checkout_url:url};
  })();inflight.set(id,task);try{return await task;}finally{inflight.delete(id);}
 }
 async function payment(id,c){
  const p=await api('/v1/payments/'+id,c);
  if(String(p.id)!==id||p.live_mode!==true||String(p.collector_id)!==String(c.collectorId)||p.currency_id!=='BRL')error(422,'payment_mismatch');
  const oid=p.external_reference;if(!uuid(oid))error(422,'payment_mismatch');
  const ch=await locked(()=>db.sql.prepare('SELECT * FROM eco_mp_checkouts WHERE order_id=?').get(oid));
  if(!ch?.preference_id||ch.collector_id!==String(c.collectorId))error(422,'payment_mismatch');
  if(p.status==='approved'){
   if(!/^\d+$/.test(String(p.order?.id||'')))error(422,'payment_mismatch');
   const mo=await api('/merchant_orders/'+p.order.id,c);
   if(mo.preference_id!==ch.preference_id||mo.external_reference!==oid||!mo.payments?.some(x=>String(x.id)===id))error(422,'payment_mismatch');
  }
  return locked(()=>transaction(db,()=>{
   const o=db.sql.prepare('SELECT * FROM eco_orders WHERE id=?').get(oid);const amount=cents(p.transaction_amount);
   if(!o||o.currency!=='mercadopago'||o.user!==ch.user||amount!==o.amount_centavos)error(422,'payment_mismatch');
   const previous=db.sql.prepare('SELECT * FROM eco_mp_payments WHERE payment_id=?').get(id);if(previous&&previous.order_id!==oid)error(422,'payment_mismatch');
   if(o.status==='fulfilled'&&o.payment_id==='mp:'+id&&p.status==='approved')return {ok:true,duplicate:true};
   const write=(reason)=>{db.sql.prepare('INSERT INTO eco_mp_payments(payment_id,order_id,status,amount_centavos,updated_at,reason) VALUES(?,?,?,?,?,?) ON CONFLICT(payment_id) DO UPDATE SET status=excluded.status,updated_at=excluded.updated_at,reason=excluded.reason').run(id,oid,String(p.status),amount,new Date().toISOString(),reason);};
   if(p.status!=='approved'){write(null);if(['refunded','charged_back'].includes(p.status)){review(db,o,id,p.status);audit(db,o,'mp_payment_review',{payment_id:id,status:p.status});}return {ok:true};}
   if(o.status!=='pending_payment'||o.payment_id){write('order_not_payable');review(db,o,id,'order_not_payable');audit(db,o,'mp_payment_review',{payment_id:id,reason:'order_not_payable'});return {ok:true,review:true};}
   const pr=mpProduct(db,o.product_id,true);
   const passReason=monthlyPassPaymentReason(pr,p.date_approved);if(passReason){write(passReason);review(db,o,id,passReason);audit(db,o,'mp_payment_review',{payment_id:id,reason:passReason});return {ok:true,review:true};}
   if(mpAlreadyOwned(db,o.user,pr)){write('product_already_owned');review(db,o,id,'product_already_owned');audit(db,o,'mp_payment_review',{payment_id:id,reason:'product_already_owned'});return {ok:true,review:true};}
   db.sql.prepare('UPDATE eco_orders SET payment_id=?,confirmed_by=?,updated_at=? WHERE id=?').run('mp:'+id,'mercadopago',new Date().toISOString(),oid);
   mpFulfil(db,o,pr);write(null);audit(db,o,'mp_payment_approved',{payment_id:id,amount_centavos:amount});return {ok:true,fulfilled:true};
  }));
 }
 return async function handle(req){
  const u=new URL(req.url),path=u.pathname;if(!path.startsWith('/api/mp/')&&path!=='/webhooks/mercadopago')return null;
  const origin=req.headers.get('origin');const headers={'Content-Type':'application/json;charset=utf-8','Cache-Control':'no-store','Vary':'Origin','X-Content-Type-Options':'nosniff',...(ORIGINS.has(origin)?{'Access-Control-Allow-Origin':origin,'Access-Control-Allow-Headers':'Content-Type,Authorization','Access-Control-Allow-Methods':'GET,POST,OPTIONS'}:{})};
  const json=(data,status=200)=>new Response(JSON.stringify(data),{status,headers});
  try{
   if(origin&&!ORIGINS.has(origin))error(403,'origin_forbidden');
   if(req.method==='OPTIONS')return new Response(null,{status:204,headers});
   const c=getConfig();
   if(req.method==='GET'&&path==='/api/mp/status')return json({ok:true,enabled:ready(c),provider:'mercadopago',version:1});
   if(!ready(c))error(503,'mp_not_configured');
   if(path==='/webhooks/mercadopago'){
    if(req.method!=='POST')error(405,'method_not_allowed');
    const id=u.searchParams.get('data.id')||'';
    if(!verifySignature(req.headers,id,c.webhookSecret))error(401,'invalid_signature');
    let event;try{event=await req.json();}catch{error(400,'invalid_json');}
    if(event.type!=='payment'||String(event.data?.id)!==id)error(400,'invalid_event');
    return json(await payment(id,c));
   }
   if(req.method!=='POST')error(405,'method_not_allowed');
   const s=await locked(()=>session(db,req));
   const t=Date.now();for(const[k,v]of rates)if(t-v.time>60000)rates.delete(k);const rate=rates.get(s.user)||{time:t,n:0};rate.n++;rates.set(s.user,rate);if(rate.n>20)error(429,'rate_limited');
   let b;try{b=await req.json();if(!b||typeof b!=='object'||Array.isArray(b))throw Error();}catch{error(400,'invalid_json');}
   if(path==='/api/mp/preference'){if(!uuid(b.orderId))error(400,'invalid_order');return json(await preference(b.orderId,s.user,c));}
   if(path!=='/api/mp/purchase')error(404,'not_found');
   if(b.terms_accepted!==true||!uuid(b.op_id)||typeof b.product_id!=='string')error(400,'invalid_purchase');
   const id=await locked(()=>transaction(db,()=>{
    const publicOn=db.sql.prepare("SELECT value FROM eco_config WHERE key='public_enabled'").get()?.value;if(publicOn!=='1'&&!['owner','admin'].includes(s.role))error(403,'economy_not_released');
    const prior=db.sql.prepare('SELECT c.*,o.product_id FROM eco_mp_checkouts c JOIN eco_orders o ON o.id=c.order_id WHERE c.user=? AND c.op_id=?').get(s.user,b.op_id);if(prior){if(prior.product_id!==b.product_id)error(409,'operation_id_reused');return prior.order_id;}
    const pr=mpProduct(db,b.product_id);assertMonthlyPassPurchase(pr);if(mpAlreadyOwned(db,s.user,pr))error(409,'product_already_owned');if(!Number.isSafeInteger(pr.cash_centavos)||pr.cash_centavos<=0)error(400,'payment_method_unavailable');
    const pending=db.sql.prepare("SELECT o.id FROM eco_orders o JOIN eco_mp_checkouts c ON c.order_id=o.id WHERE o.user=? AND o.product_id=? AND o.status='pending_payment' AND c.collector_id=?").get(s.user,pr.id,String(c.collectorId));if(pending)return pending.id;
    const oid=randomUUID(),now=new Date().toISOString();db.sql.prepare('INSERT INTO eco_orders(id,user,product_id,title,status,amount_centavos,currency,price_units,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?)').run(oid,s.user,pr.id,pr.title,'pending_payment',pr.cash_centavos,'mercadopago',0,now,now);
    db.sql.prepare('INSERT INTO eco_mp_checkouts(order_id,op_id,user,collector_id,live_mode,created_at) VALUES(?,?,?,?,?,?)').run(oid,b.op_id,s.user,String(c.collectorId),1,now);audit(db,{id:oid,user:s.user},'mp_checkout_created',{product_id:pr.id,amount_centavos:pr.cash_centavos});return oid;
   }));return json(await preference(id,s.user,c));
  }catch(e){return json({ok:false,error:e.status?e.message:'mp_internal_error'},e.status||500);}
 };
}
