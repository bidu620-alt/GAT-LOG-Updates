import {EMOJI_PACK,isBiduOwner} from './emoji-access.js';
import {monthlyPassProduct,monthlyPassState,assertMonthlyPassPurchase} from './monthly-pass.js';
import {readFileSync} from 'node:fs';
import {randomUUID,createHash} from 'node:crypto';
// Used only by the local Mercado Pago adapter; preserves the existing delivery rules.
export {product as mpProduct,alreadyOwned as mpAlreadyOwned,fulfil as mpFulfil};

export const BRANDS={daf:'DAF',iveco:'Iveco',man:'MAN',mercedes:'Mercedes-Benz',renault:'Renault',scania:'Scania',volvo:'Volvo'};
const initialized=new WeakSet();
const now=()=>new Date().toISOString();
const clean=v=>String(v||'').trim().toLowerCase().replace(/^@/,'');
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const fail=(status,code)=>{throw Object.assign(new Error(code),{status})};
const sql=db=>db.sql;
const cfg=(db,key)=>sql(db).prepare('SELECT value FROM eco_config WHERE key=?').get(key)?.value;
const parse=v=>{try{return JSON.parse(v||'null')}catch{return null}};
const isAdmin=s=>['owner','admin'].includes(s.role);
export function ensureEconomy(db){if(!initialized.has(db)){sql(db).exec(readFileSync(new URL('./economy-schema.sql',import.meta.url),'utf8'));sql(db).exec(`CREATE TABLE IF NOT EXISTS eco_company_requests(id TEXT PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL REFERENCES accounts(user),status TEXT NOT NULL,created_at TEXT NOT NULL,decided_at TEXT,decided_by TEXT,UNIQUE(company_id,user)); CREATE INDEX IF NOT EXISTS eco_company_requests_pending ON eco_company_requests(company_id,status,created_at);`);sql(db).exec(`CREATE TABLE IF NOT EXISTS eco_brand_transfers(seq INTEGER PRIMARY KEY AUTOINCREMENT,id TEXT NOT NULL UNIQUE,brand_key TEXT NOT NULL,source_order_id TEXT NOT NULL,from_user TEXT NOT NULL,to_user TEXT NOT NULL,created_at TEXT NOT NULL); CREATE INDEX IF NOT EXISTS eco_brand_transfers_source ON eco_brand_transfers(source_order_id,seq DESC);`);initialized.add(db)}grantBiduCatalog(db)}
function grantBiduCatalog(db){if(!isBiduOwner(db,'biduzao'))return;tx(db,()=>{let count=0;for(const p of catalog(db)){if(p.kind==='brand')count+=sql(db).prepare('INSERT OR IGNORE INTO eco_brands VALUES(?,?,?,?)').run('biduzao',p.brand_key,now(),'owner-catalog-access').changes;else if(p.kind!=='bcoin')count+=sql(db).prepare('INSERT OR IGNORE INTO eco_entitlements VALUES(?,?,?,?,?,?)').run('biduzao',p.id,p.title,p.kind,now(),'owner-catalog-access').changes}if(count)audit(db,'biduzao','owner_catalog_access','biduzao',{items:count});})}
function eligibleAccount(db,s){ensureEconomy(db);if(!s?.user)fail(401,'invalid_session');if(cfg(db,'public_enabled')!=='1'&&!isAdmin(s))fail(403,'economy_not_released')}
function admin(s){if(!isAdmin(s))fail(403,'forbidden')}
export function units(v){const s=String(v??'').trim().replace(',','.');if(!/^\d{1,8}(?:\.\d{1,2})?$/.test(s))fail(400,'invalid_amount');const [a,b='']=s.split('.');const n=Number(a)*100+Number((b+'00').slice(0,2));if(!Number.isSafeInteger(n)||n<1||n>1000000000)fail(400,'invalid_amount');return n}
function wallet(db,user){sql(db).prepare('INSERT OR IGNORE INTO eco_wallets(user,updated_at) VALUES(?,?)').run(user,now());return sql(db).prepare('SELECT * FROM eco_wallets WHERE user=?').get(user)}
function ledger(db,user,currency,delta,kind,ref,details={}){
 const column=currency==='gold'?'gold_units':'bcoin_units';const w=wallet(db,user),next=w[column]+delta;
 if(!Number.isSafeInteger(next)||next<0)fail(409,'insufficient_balance');
 sql(db).prepare(`UPDATE eco_wallets SET ${column}=?,updated_at=? WHERE user=?`).run(next,now(),user);
 sql(db).prepare('INSERT INTO eco_ledger VALUES(?,?,?,?,?,?,?,?,?)').run(randomUUID(),user,kind,currency,delta,next,ref,now(),JSON.stringify(details));
 return next;
}
function audit(db,actor,action,target,details){sql(db).prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(now(),actor,action,target,JSON.stringify(details))}
function tx(db,fn){sql(db).exec('SAVEPOINT economy');try{const result=fn();sql(db).exec('RELEASE economy');return result}catch(e){sql(db).exec('ROLLBACK TO economy; RELEASE economy');throw e}}
function op(db,s,b,action,fn){
 const id=String(b.op_id||'');if(!/^[a-zA-Z0-9_-]{16,80}$/.test(id))fail(400,'operation_id_required');
 const request={...b};delete request.token;delete request.op_id;
 const hash=createHash('sha256').update(action+'|'+JSON.stringify(request)).digest('hex');
 return tx(db,()=>{const prior=sql(db).prepare('SELECT * FROM eco_ops WHERE user=? AND op_id=?').get(s.user,id);if(prior){if(prior.request_hash!==hash)fail(409,'operation_id_reused');return parse(prior.result_json)}const result=fn();sql(db).prepare('INSERT INTO eco_ops VALUES(?,?,?,?)').run(s.user,id,hash,JSON.stringify(result));return result});
}
function existsUser(db,value){const user=clean(value);if(!/^[a-z0-9._-]{3,32}$/.test(user))fail(400,'invalid_recipient');const a=sql(db).prepare('SELECT user,disabled FROM accounts WHERE user=?').get(user);if(!a||a.disabled)fail(404,'recipient_not_found');return user}
function catalog(db){return [EMOJI_PACK,monthlyPassProduct(),
 ...Object.entries(BRANDS).map(([key,title])=>({id:'brand:'+key,kind:'brand',brand_key:key,title:'Chave '+title,description:'Libera todos os modelos '+title+' para ganhar Gold em entregas válidas. Não altera o caminhão dentro do ETS2.',cash_centavos:1000,gold_units:2000000,bcoin_units:0})),
 ...sql(db).prepare('SELECT * FROM eco_products WHERE active=1 ORDER BY title').all().filter(p=>p.id!==monthlyPassProduct().id).map(p=>({...p,cash_centavos:0}))
 ]}
function product(db,id,legacy=false){if(legacy&&/^bcoin:(10|20|50)$/.test(id)){const n=Number(id.split(':')[1]);return{id,kind:'bcoin',title:n+' Bcoin',bcoin_credit:n*100};}const p=catalog(db).find(x=>x.id===id);if(!p)fail(404,'product_not_found');return p}
function owned(db,user,key){return sql(db).prepare('SELECT * FROM eco_brands WHERE user=? AND brand_key=?').get(user,key)}
function pixTransferable(db,brand){
 if(!brand||!Object.hasOwn(BRANDS,brand.brand_key))return false;
 const o=sql(db).prepare("SELECT * FROM eco_orders WHERE id=? AND currency='pix' AND status='fulfilled' AND product_id=? AND amount_centavos>0").get(brand.source,'brand:'+brand.brand_key);
 if(!o?.payment_id)return false;
 const receipt=sql(db).prepare('SELECT 1 FROM eco_pix_receipts WHERE order_id=? AND payment_id=? AND amount_centavos=?').get(o.id,o.payment_id,o.amount_centavos);
 if(!receipt)return false;
 const last=sql(db).prepare('SELECT to_user,brand_key FROM eco_brand_transfers WHERE source_order_id=? ORDER BY seq DESC LIMIT 1').get(o.id);
 return last?last.to_user===brand.user&&last.brand_key===brand.brand_key:o.user===brand.user;
}
function alreadyOwned(db,user,p){return p.kind==='brand'?owned(db,user,p.brand_key):p.kind!=='bcoin'?sql(db).prepare('SELECT 1 FROM eco_entitlements WHERE user=? AND product_id=?').get(user,p.id):false}
function fulfil(db,order,p){
 if(alreadyOwned(db,order.user,p))fail(409,'product_already_owned');
 if(p.kind==='brand')sql(db).prepare('INSERT INTO eco_brands VALUES(?,?,?,?)').run(order.user,p.brand_key,now(),order.id);
 else if(p.kind==='bcoin')ledger(db,order.user,'bcoin',p.bcoin_credit,'bcoin_purchase',order.id,{product:p.id});
 else sql(db).prepare('INSERT INTO eco_entitlements VALUES(?,?,?,?,?,?)').run(order.user,p.id,p.title,p.kind,now(),order.id);
 sql(db).prepare("UPDATE eco_orders SET status='fulfilled',fulfilled_at=?,updated_at=? WHERE id=?").run(now(),now(),order.id);
}
function mission(db,user){return parse(sql(db).prepare('SELECT current_mission_json FROM profiles WHERE user=?').get(user)?.current_mission_json)}
export function evidence(raw){const make=String(raw?.truck?.make||raw?.truck?.manufacturer||raw?.truck?.brand||'').trim();const n=norm(make),key=({'mercedes benz':'mercedes','renault trucks':'renault'}[n]||n);return{brand:key,make,model:String(raw?.truck?.model||''),signature:raw?.game?.connected===true&&key?'brand|'+key:'',connected:raw?.game?.connected===true,paused:raw?.game?.paused===true}}
function currentValidation(db,user){
 const live=sql(db).prepare('SELECT updated_at,telemetry_json FROM telemetry_live WHERE account_user=? ORDER BY updated_at DESC LIMIT 1').get(user);
 const raw=parse(live?.telemetry_json)||{},ev=evidence(raw),t=Date.parse(raw.gat_collected_at||live?.updated_at||'');
 const reason=!ev.signature||!Number.isFinite(t)||Date.now()-t>15000||t>Date.now()+30000?'truck_not_identified':!Object.hasOwn(BRANDS,ev.brand)?'brand_unknown':!owned(db,user,ev.brand)?'brand_not_owned':null;
 const messages={truck_not_identified:'Aguardando identificação do caminhão no ETS2.',brand_unknown:'Marca não reconhecida.',brand_not_owned:'Esta marca não está habilitada na sua garagem.'};
 return {signature:ev.signature,model_key:ev.brand,brand_key:ev.brand,make:ev.make,model:ev.model,eligible:!reason,reason,message:reason?messages[reason]:'Marca habilitada. Gold calculado sobre os Pontos GAT na entrega.'};
}
function companyState(db,user){const member=sql(db).prepare('SELECT * FROM eco_company_members WHERE user=?').get(user);if(!member)return null;const c=sql(db).prepare('SELECT * FROM eco_companies WHERE id=?').get(member.company_id);return {...c,my_role:member.role,requests:member.role==='owner'?sql(db).prepare("SELECT id,user,created_at FROM eco_company_requests WHERE company_id=? AND status='pending' ORDER BY created_at").all(c.id):[],members:sql(db).prepare('SELECT user,role,joined_at FROM eco_company_members WHERE company_id=? ORDER BY joined_at').all(c.id)}}
export function state(db,s){eligibleAccount(db,s);const w=wallet(db,s.user),sel=sql(db).prepare('SELECT brand_key FROM eco_selected WHERE user=?').get(s.user)?.brand_key;const last=sql(db).prepare('SELECT * FROM eco_rewards WHERE user=? ORDER BY delivery_id DESC LIMIT 1').get(s.user);return{
 ok:true,brand_transfer_rules:'pix-only-v1',company_rules:'free-request-v1',mode:'official',rule:'brand-v1',user:s.user,is_admin:isAdmin(s),gold_balance:w.gold_units/100,bcoin_balance:w.bcoin_units/100,
 gold_starts_at:cfg(db,'gold_starts_at'),selection_locked:['active','suspended'].includes(mission(db,s.user)?.state),
 garage:sql(db).prepare('SELECT * FROM eco_brands WHERE user=? ORDER BY brand_key').all(s.user).map(x=>({...x,transferable:pixTransferable(db,x),model_key:x.brand_key,label:BRANDS[x.brand_key]+' • todos os modelos',selected:x.brand_key===sel})),validation:currentValidation(db,s.user),
 last_reward_message:last?`Entrega #${last.delivery_id}: ${last.gold_units/100} Gold (${last.gat_points} Pontos GAT). ${last.reason==='validated'?'Creditado na carteira.':'Motivo: '+last.reason}`:'Nenhuma entrega gerou Gold nesta carteira ainda.',
 ledger:sql(db).prepare('SELECT * FROM eco_ledger WHERE user=? ORDER BY created_at DESC,id DESC LIMIT 100').all(s.user).map(x=>({...x,details:parse(x.details_json),details_json:undefined})),
 orders:sql(db).prepare("SELECT * FROM eco_orders WHERE user=? AND status<>'cancelled' ORDER BY created_at DESC LIMIT 50").all(s.user).map(x=>({...x,payment_claim_json:undefined})),
 entitlements:sql(db).prepare('SELECT * FROM eco_entitlements WHERE user=?').all(s.user),company:companyState(db,s.user),
 invites:[],
 companies:sql(db).prepare('SELECT c.id,c.name,c.tag,c.description,COUNT(m.user) AS member_count FROM eco_companies c LEFT JOIN eco_company_members m ON m.company_id=c.id GROUP BY c.id ORDER BY c.name,c.id LIMIT 200').all(),
 company_requests:sql(db).prepare("SELECT r.id,r.company_id,c.name,c.tag,r.status FROM eco_company_requests r JOIN eco_companies c ON c.id=r.company_id WHERE r.user=? AND r.status='pending' ORDER BY r.created_at").all(s.user),
 catalog:catalog(db),pix_enabled:cfg(db,'pix_enabled')==='1',pix_url:cfg(db,'pix_url'),supplier:parse(cfg(db,'supplier_json'))||{},support_discord:'bidu0576',transfer_fee_pct:5
}}
export function economyTrack(db,user,raw,t){
 ensureEconomy(db);const a=sql(db).prepare('SELECT user,role FROM accounts WHERE user=? AND disabled=0').get(user);if(!a||cfg(db,'public_enabled')!=='1'&&!isAdmin(a))return;
 const m=mission(db,user);if(m?.state!=='active'||!m.id)return;
 const old=sql(db).prepare('SELECT evidence_json FROM eco_journeys WHERE user=? AND mission_id=?').get(user,m.id);
 const e=parse(old?.evidence_json)||{started_at:m.started_at,last_at:null,brand:null,reason:null,samples:0};
 if(e.last_at&&Date.parse(t)<=Date.parse(e.last_at))return;
 // Only brand and ownership affect Gold evidence. Packet sequence remains diagnostic.
 if(['journal_not_verified','telemetry_gap_review','brand_not_selected','model_missing_while_driving','journey_not_observed_from_start'].includes(e.reason))e.reason=null;
 e.rule='delivery-points-brand-v3';
 const ev=evidence(raw),loaded=raw.job_latched===true||raw.on_job===true||raw.gameplay?.onJob===true;
 if(ev.connected&&loaded&&ev.signature){
  if(e.brand&&e.brand!==ev.brand)e.reason='truck_changed';
  if(!e.brand)e.brand=ev.brand;
  if(!Object.hasOwn(BRANDS,ev.brand))e.reason||='brand_unknown';
  const own=owned(db,user,ev.brand);
  if(!own||Date.parse(own.acquired_at)>Date.parse(m.started_at))e.reason||='brand_not_owned_at_start';
  e.samples=(e.samples||0)+1;
 }
 if(Date.parse(m.started_at)<Date.parse(cfg(db,'gold_starts_at')))e.reason||='delivery_before_launch';
 e.last_at=t;
 sql(db).prepare('INSERT INTO eco_journeys VALUES(?,?,?) ON CONFLICT(user,mission_id) DO UPDATE SET evidence_json=excluded.evidence_json').run(user,m.id,JSON.stringify(e));
}
export function economyFinish(db,user,t,event){
 if(!String(event?.type||'').startsWith('delivery_completed'))return;ensureEconomy(db);
 const d=sql(db).prepare('SELECT id,raw_json FROM deliveries WHERE user=? AND delivered_at=? ORDER BY id DESC LIMIT 1').get(user,t);if(!d)return;
 const r=parse(d.raw_json)||{},m=r.mission||{},a=r.audit||{},e=parse(sql(db).prepare('SELECT evidence_json FROM eco_journeys WHERE user=? AND mission_id=?').get(user,m.id||'')?.evidence_json);
 // Old evidence is not silently trusted: the new rule must observe this trip live.
 let reason=e?.rule==='delivery-points-brand-v3'?(e.reason||null):'brand_evidence_not_updated';
 const points=Number.isSafeInteger(a.gat_points)&&a.gat_points>=0?a.gat_points:0;
 if(!Number.isSafeInteger(a.gat_points)||a.gat_points<0)reason||='invalid_delivery_points';
 if(!e?.brand||e.samples<1)reason||='insufficient_brand_evidence';
 if(!(a.rank_eligible===true||a.ranking_eligible===true)||a.admin_test_mode===true)reason||='delivery_not_eligible';
 const own=owned(db,user,e?.brand||'');
 if(!own)reason||='brand_not_owned';
 else if(Date.parse(own.acquired_at)>Date.parse(m.started_at))reason||='brand_not_owned_at_start';
 if(Date.parse(m.started_at)<Date.parse(cfg(db,'gold_starts_at')))reason||='delivery_before_launch';
 tx(db,()=>{if(sql(db).prepare('SELECT 1 FROM eco_rewards WHERE user=? AND delivery_id=?').get(user,d.id))return;const amount=reason?0:points*100;sql(db).prepare('INSERT OR IGNORE INTO eco_rewards VALUES(?,?,?,?,?,?,?)').run(user,d.id,m.id||'',points,amount,reason||'validated',t);if(sql(db).prepare('SELECT changes() n').get().n&&amount)ledger(db,user,'gold',amount,'delivery_reward','delivery:'+d.id,{gat_points:points,brand:e.brand,rule:'delivery-points-brand-v3'})});
}
export function economySelect(db,user,key){ensureEconomy(db);if(!owned(db,user,key))fail(403,'brand_not_owned');if(['active','suspended'].includes(mission(db,user)?.state))fail(409,'selection_locked_during_delivery');sql(db).prepare('INSERT INTO eco_selected VALUES(?,?) ON CONFLICT(user) DO UPDATE SET brand_key=excluded.brand_key').run(user,key);return{ok:true,mode:'official'}}
export function economyGrantTest(){fail(403,'test_grants_disabled')}

export function economyAction(db,s,path,b){
 eligibleAccount(db,s);
 if(path==='state')return state(db,s);
 if(path==='pass-state')return monthlyPassState(db,s,String(b.month||'2026-11'));
 if(path==='select-truck')return op(db,s,b,path,()=>economySelect(db,s.user,String(b.model_key||'')));
 if(path==='recipient'){const recipient=existsUser(db,b.user);return{ok:true,user:recipient,is_self:recipient===s.user}}
 if(path==='transfer-brand')return op(db,s,b,path,()=>{
  const key=String(b.brand_key||'');if(!Object.hasOwn(BRANDS,key))fail(400,'invalid_brand');
  const to=existsUser(db,b.recipient);if(to===s.user)fail(400,'self_transfer');
  if(b.recipient_confirmed!==true)fail(400,'recipient_confirmation_required');
  const brand=owned(db,s.user,key);if(!brand)fail(403,'brand_not_owned');
  if(!pixTransferable(db,brand))fail(403,'brand_transfer_pix_only');
  if(owned(db,to,key))fail(409,'recipient_brand_owned');
  if([s.user,to].some(user=>['active','suspended'].includes(mission(db,user)?.state)))fail(409,'brand_transfer_delivery_locked');
  const id=randomUUID(),at=now();
  sql(db).prepare('INSERT INTO eco_brand_transfers(id,brand_key,source_order_id,from_user,to_user,created_at) VALUES(?,?,?,?,?,?)').run(id,key,brand.source,s.user,to,at);
  sql(db).prepare('DELETE FROM eco_selected WHERE user=? AND brand_key=?').run(s.user,key);
  const removed=sql(db).prepare('DELETE FROM eco_brands WHERE user=? AND brand_key=? AND source=?').run(s.user,key,brand.source);
  if(removed.changes!==1)fail(409,'brand_not_owned');
  sql(db).prepare('INSERT INTO eco_brands VALUES(?,?,?,?)').run(to,key,at,brand.source);
  audit(db,s.user,'brand_transferred',to,{transfer_id:id,brand_key:key,source_order_id:brand.source});
  return{ok:true,transfer_id:id,recipient:to,brand_key:key,message:'Marca transferida. O destinatário pode selecioná-la na garagem.'};
 });
 if(path==='transfer')return op(db,s,b,path,()=>{
  const to=existsUser(db,b.recipient);if(to===s.user)fail(400,'self_transfer');if(b.recipient_confirmed!==true)fail(400,'recipient_confirmation_required');
  const amount=units(b.amount),fee=Math.ceil(amount*5/100),ref=randomUUID();
  ledger(db,s.user,'gold',-(amount+fee),'transfer_sent',ref,{recipient:to,amount_units:amount,fee_units:fee});ledger(db,to,'gold',amount,'transfer_received',ref,{sender:s.user});audit(db,s.user,'gold_transfer',to,{ref,amount_units:amount,fee_units:fee});return{ok:true,amount:amount/100,fee:fee/100,total:(amount+fee)/100}
 });
 if(path==='purchase'&&String(b.product_id||'').startsWith('bcoin:'))fail(403,'bcoin_purchase_disabled');
 if(path==='purchase')return op(db,s,b,path,()=>{
  if(b.terms_accepted!==true)fail(400,'terms_required');const p=product(db,String(b.product_id||''));assertMonthlyPassPurchase(p);if(p.id===monthlyPassProduct().id&&b.currency!=='gold')fail(400,'payment_method_unavailable');if(alreadyOwned(db,s.user,p))fail(409,'product_already_owned');
  const currency=String(b.currency||'');let price=currency==='gold'?p.gold_units:currency==='bcoin'?p.bcoin_units:currency==='pix'?p.cash_centavos:0;if(!price)fail(400,'payment_method_unavailable');
  if(currency==='pix'&&cfg(db,'pix_enabled')!=='1')fail(409,'pix_supplier_setup_required');
  if(currency==='pix'){const existing=sql(db).prepare("SELECT id,amount_centavos FROM eco_orders WHERE user=? AND product_id=? AND status IN ('pending_payment','awaiting_review') ORDER BY created_at DESC LIMIT 1").get(s.user,p.id);if(existing)return{ok:true,order_id:existing.id,pending_payment:true,pix_url:cfg(db,'pix_url'),amount_centavos:existing.amount_centavos};}
  const id=randomUUID(),order={id,user:s.user,product_id:p.id,title:p.title,status:currency==='pix'?'pending_payment':'processing',amount_centavos:currency==='pix'?price:0,currency,price_units:currency==='pix'?0:price};
  if(currency!=='pix')ledger(db,s.user,currency,-price,'purchase',id,{product:p.id});
  sql(db).prepare('INSERT INTO eco_orders(id,user,product_id,title,status,amount_centavos,currency,price_units,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?)').run(id,s.user,p.id,p.title,order.status,order.amount_centavos,currency,order.price_units,now(),now());
  if(currency!=='pix')fulfil(db,order,p);audit(db,s.user,'economy_purchase',s.user,{id,product:p.id,currency});return{ok:true,order_id:id,pending_payment:currency==='pix',pix_url:currency==='pix'?cfg(db,'pix_url'):null,amount_centavos:order.amount_centavos}
 });
 if(path==='payment-claim')return op(db,s,b,path,()=>{const order=sql(db).prepare('SELECT * FROM eco_orders WHERE id=? AND user=?').get(String(b.order_id||''),s.user);if(!order||!['pending_payment','awaiting_review'].includes(order.status))fail(409,'order_not_pending');const claim={payment_id:String(b.payment_id||'').trim().slice(0,80),payer_name:String(b.payer_name||'').trim().slice(0,120),at:now()};sql(db).prepare("UPDATE eco_orders SET status='awaiting_review',payment_claim_json=?,updated_at=? WHERE id=?").run(JSON.stringify(claim),now(),order.id);return{ok:true,message:'Pagamento informado. O produto será liberado após conferência do extrato pelo administrador.'}});
 if(path==='order-cancel')return op(db,s,b,path,()=>{
  const id=String(b.order_id||'');
  const r=sql(db).prepare("UPDATE eco_orders SET status='cancelled',updated_at=? WHERE id=? AND user=? AND currency='pix' AND status='pending_payment' AND payment_id IS NULL AND confirmed_by IS NULL AND fulfilled_at IS NULL AND NOT EXISTS(SELECT 1 FROM eco_pix_receipts r WHERE r.order_id=eco_orders.id)").run(now(),id,s.user);
  if(!r.changes)fail(409,'order_cannot_cancel');audit(db,s.user,'order_cancelled',s.user,{order_id:id});
  return{ok:true,message:'Pedido cancelado e removido do seu histórico de compras.'};
 });
 if(path==='refund-request')return op(db,s,b,path,()=>{const order=sql(db).prepare('SELECT * FROM eco_orders WHERE id=? AND user=?').get(String(b.order_id||''),s.user);if(!order)fail(404,'order_not_found');const reason=String(b.reason||'').trim();if(reason.length<5)fail(400,'refund_reason_required');const id=randomUUID();sql(db).prepare('INSERT INTO eco_support(id,user,order_id,reason,created_at) VALUES(?,?,?,?,?)').run(id,s.user,order.id,reason.slice(0,1500),now());return{ok:true,request_id:id,message:'Solicitação registrada. Atendimento pelo Discord bidu0576.'}});
 if(path==='company-create')return op(db,s,b,path,()=>{
  if(!sql(db).prepare('SELECT 1 FROM eco_brands WHERE user=?').get(s.user))fail(403,'brand_required');if(sql(db).prepare('SELECT 1 FROM eco_company_members WHERE user=?').get(s.user))fail(409,'already_in_company');
  const name=String(b.name||'').trim(),tag=String(b.tag||'').trim().toUpperCase();if(name.length<3||name.length>60||!/^[A-Z0-9]{2,8}$/.test(tag))fail(400,'invalid_company');if(sql(db).prepare('SELECT 1 FROM eco_companies WHERE tag=?').get(tag))fail(409,'company_tag_taken');
  const id=randomUUID();sql(db).prepare('INSERT INTO eco_companies VALUES(?,?,?,?,?,?,?)').run(id,name,tag,String(b.description||'').trim().slice(0,500),s.user,1,now());sql(db).prepare('INSERT INTO eco_company_members VALUES(?,?,?,?)').run(s.user,id,'owner',now());sql(db).prepare("UPDATE eco_company_requests SET status='cancelled',decided_at=?,decided_by=? WHERE user=? AND status='pending'").run(now(),s.user,s.user);return{ok:true,company_id:id}
 });
 if(path.startsWith('company-'))return op(db,s,b,path,()=>companyAction(db,s,path,b));
 if(path.startsWith('admin/')){admin(s);return adminAction(db,s,path.slice(6),b)}
 fail(404,'not_found');
}
function companyAction(db,s,path,b){
 if(path==='company-accept'||path==='company-invite')fail(409,'company_request_required');
 if(path==='company-request'){
  if(sql(db).prepare('SELECT 1 FROM eco_company_members WHERE user=?').get(s.user))fail(409,'already_in_company');
  if(!sql(db).prepare('SELECT 1 FROM eco_brands WHERE user=?').get(s.user))fail(403,'brand_required');
  const company=sql(db).prepare('SELECT id FROM eco_companies WHERE id=?').get(String(b.company_id||''));if(!company)fail(404,'company_not_found');
  const previous=sql(db).prepare('SELECT * FROM eco_company_requests WHERE company_id=? AND user=?').get(company.id,s.user);
  if(previous?.status==='pending')return{ok:true,request_id:previous.id,already_pending:true};
  if(sql(db).prepare("SELECT COUNT(*) n FROM eco_company_requests WHERE user=? AND status='pending'").get(s.user).n>=10)fail(409,'company_request_limit');
  const id=randomUUID();sql(db).prepare("INSERT INTO eco_company_requests(id,company_id,user,status,created_at) VALUES(?,?,?,'pending',?) ON CONFLICT(company_id,user) DO UPDATE SET id=excluded.id,status='pending',created_at=excluded.created_at,decided_at=NULL,decided_by=NULL").run(id,company.id,s.user,now());
  audit(db,s.user,'company_request',company.id,{request_id:id});return{ok:true,request_id:id};
 }
 if(path==='company-request-cancel'){
  const changed=sql(db).prepare("UPDATE eco_company_requests SET status='cancelled',decided_at=?,decided_by=? WHERE id=? AND user=? AND status='pending'").run(now(),s.user,String(b.request_id||''),s.user);
  if(!changed.changes)fail(409,'company_request_not_pending');return{ok:true};
 }
 if(path==='company-request-decide'){
  const r=sql(db).prepare("SELECT * FROM eco_company_requests WHERE id=? AND status='pending'").get(String(b.request_id||''));if(!r)fail(409,'company_request_not_pending');
  const company=sql(db).prepare('SELECT * FROM eco_companies WHERE id=? AND owner_user=?').get(r.company_id,s.user);if(!company)fail(403,'company_owner_required');
  if(!['accept','reject'].includes(b.decision))fail(400,'invalid_company_decision');
  if(b.decision==='accept'){
   const account=sql(db).prepare('SELECT disabled FROM accounts WHERE user=?').get(r.user);if(!account||account.disabled)fail(409,'company_applicant_unavailable');
   if(sql(db).prepare('SELECT 1 FROM eco_company_members WHERE user=?').get(r.user))fail(409,'already_in_company');
   if(!sql(db).prepare('SELECT 1 FROM eco_brands WHERE user=?').get(r.user))fail(403,'brand_required');
   const count=sql(db).prepare('SELECT COUNT(*) n FROM eco_company_members WHERE company_id=?').get(company.id).n;
   if(count>=company.capacity)sql(db).prepare('UPDATE eco_companies SET capacity=? WHERE id=?').run(count+1,company.id);
   sql(db).prepare('INSERT INTO eco_company_members VALUES(?,?,?,?)').run(r.user,company.id,'driver',now());
   sql(db).prepare("UPDATE eco_company_requests SET status='cancelled',decided_at=?,decided_by=? WHERE user=? AND status='pending' AND id<>?").run(now(),s.user,r.user,r.id);
  }
  sql(db).prepare('UPDATE eco_company_requests SET status=?,decided_at=?,decided_by=? WHERE id=?').run(b.decision==='accept'?'accepted':'rejected',now(),s.user,r.id);
  audit(db,s.user,'company_request_decided',r.user,{company_id:company.id,request_id:r.id,decision:b.decision});return{ok:true};
 }
 const m=sql(db).prepare('SELECT * FROM eco_company_members WHERE user=?').get(s.user);if(!m)fail(404,'company_not_found');
 if(path==='company-leave'){if(m.role==='owner')fail(409,'owner_cannot_leave');sql(db).prepare('DELETE FROM eco_company_members WHERE user=?').run(s.user);return{ok:true}}
 if(!['owner','manager'].includes(m.role))fail(403,'company_management_required');
 if(path==='company-slot'){if(m.role!=='owner')fail(403,'company_owner_required');sql(db).prepare('UPDATE eco_companies SET capacity=capacity+1 WHERE id=?').run(m.company_id);return{ok:true}}
 if(path==='company-edit'){if(m.role!=='owner')fail(403,'company_owner_required');const name=String(b.name||'').trim();if(name.length<3||name.length>60)fail(400,'invalid_company');sql(db).prepare('UPDATE eco_companies SET name=?,description=? WHERE id=?').run(name,String(b.description||'').trim().slice(0,500),m.company_id);return{ok:true}}
 if(path==='company-invite'){const user=existsUser(db,b.user);if(sql(db).prepare('SELECT 1 FROM eco_company_members WHERE user=?').get(user))fail(409,'already_in_company');const id=randomUUID();sql(db).prepare('INSERT INTO eco_company_invites VALUES(?,?,?,?,?,?,?)').run(id,m.company_id,user,s.user,'pending',new Date(Date.now()+7*86400000).toISOString(),now());return{ok:true}}
 const target=existsUser(db,b.user),tm=sql(db).prepare('SELECT * FROM eco_company_members WHERE user=? AND company_id=?').get(target,m.company_id);if(!tm||tm.role==='owner')fail(403,'invalid_company_member');
 if(path==='company-role'){if(m.role!=='owner'||!['manager','driver'].includes(b.role))fail(403,'company_owner_required');sql(db).prepare('UPDATE eco_company_members SET role=? WHERE user=?').run(b.role,target);return{ok:true}}
 if(path==='company-remove'){if(m.role!=='owner'&&tm.role!=='driver')fail(403,'company_owner_required');sql(db).prepare('DELETE FROM eco_company_members WHERE user=?').run(target);return{ok:true}}
 fail(404,'not_found');
}
function adminAction(db,s,path,b){
 if(path==='delivery-rewards'){
  // Somente owner/admin: economyAction valida a sessao e executa admin(s).
  // Consulta somente entregas e credito correspondente, nunca carteira de terceiros.
  const target=clean(b.user);
  if(!/^[a-z0-9._-]{3,32}$/.test(target))fail(400,'invalid_user');
  if(!sql(db).prepare('SELECT 1 FROM accounts WHERE user=?').get(target))fail(404,'user_not_found');
  const rows=sql(db).prepare("SELECT r.delivery_id,r.gat_points,r.gold_units,r.reason,r.created_at,COALESCE((SELECT SUM(l.delta_units) FROM eco_ledger l WHERE l.user=r.user AND l.kind='delivery_reward' AND l.currency='gold' AND l.reference='delivery:' || r.delivery_id),0) AS credited_ledger_units FROM eco_rewards r WHERE r.user=? ORDER BY r.delivery_id DESC LIMIT 500").all(target);
  return {ok:true,mode:'official',is_admin:true,user:target,delivery_rewards:rows};
 }

 if(path==='overview')return{ok:true,can_confirm_without_id:isBiduOwner(db,s.user),orders:sql(db).prepare("SELECT * FROM eco_orders WHERE currency='pix' AND status!='cancelled' ORDER BY created_at DESC LIMIT 200").all().map(x=>({...x,payment_claim:parse(x.payment_claim_json),payment_claim_json:undefined})),support:sql(db).prepare('SELECT * FROM eco_support ORDER BY created_at DESC LIMIT 100').all(),supplier:parse(cfg(db,'supplier_json'))||{},pix_enabled:cfg(db,'pix_enabled')==='1',products:sql(db).prepare('SELECT * FROM eco_products ORDER BY title').all()};
 if(path==='cancel-order')return op(db,s,b,path,()=>{const order=sql(db).prepare('SELECT * FROM eco_orders WHERE id=?').get(String(b.order_id||''));if(!order||order.currency!=='pix'||!['pending_payment','awaiting_review'].includes(order.status)||order.payment_id||sql(db).prepare('SELECT 1 FROM eco_pix_receipts WHERE order_id=?').get(order.id))fail(409,'order_not_pending');if(b.unpaid_confirmed!==true)fail(400,'unpaid_confirmation_required');const note=String(b.note||'').trim();if(note.length<5||note.length>500)fail(400,'manual_note_required');sql(db).prepare("UPDATE eco_orders SET status='cancelled',updated_at=? WHERE id=?").run(now(),order.id);audit(db,s.user,'unpaid_order_cancelled',order.user,{order_id:order.id,note});return{ok:true}});
 if(path==='confirm-payment')return op(db,s,b,path,()=>{
  const order=sql(db).prepare('SELECT * FROM eco_orders WHERE id=?').get(String(b.order_id||''));if(!order)fail(404,'order_not_found');if(order.status==='fulfilled')return{ok:true,already_confirmed:true};if(!['pending_payment','awaiting_review'].includes(order.status))fail(409,'order_not_pending');
  if(order.currency!=='pix')fail(409,'order_not_pending');
  if(b.manual_without_id===true){if(!isBiduOwner(db,s.user))fail(403,'forbidden');if(b.bank_statement_checked!==true)fail(400,'bank_confirmation_required');const amount=units(b.received_reais),note=String(b.manual_note||'').trim();if(note.length<8||note.length>500)fail(400,'manual_note_required');if(amount!==order.amount_centavos)fail(409,'payment_amount_mismatch');const p=product(db,order.product_id,true);sql(db).prepare('UPDATE eco_orders SET confirmed_by=?,updated_at=? WHERE id=?').run(s.user,now(),order.id);fulfil(db,order,p);audit(db,s.user,'pix_confirmed_manual',order.user,{order_id:order.id,amount_centavos:amount,note,bank_transaction_id:null});return{ok:true,manual_confirmation:true}}
  if(b.bank_statement_checked!==true)fail(400,'bank_confirmation_required');const payment=String(b.payment_id||'').trim();if(!/^[a-zA-Z0-9._:-]{8,80}$/.test(payment))fail(400,'bank_payment_id_required');const amount=units(b.received_reais);if(amount!==order.amount_centavos)fail(409,'payment_amount_mismatch');if(sql(db).prepare('SELECT 1 FROM eco_pix_receipts WHERE payment_id=?').get(payment))fail(409,'payment_already_used');
  const p=product(db,order.product_id,true);sql(db).prepare('INSERT INTO eco_pix_receipts VALUES(?,?,?,?,?)').run(payment,order.id,amount,s.user,now());sql(db).prepare('UPDATE eco_orders SET payment_id=?,confirmed_by=?,updated_at=? WHERE id=?').run(payment,s.user,now(),order.id);fulfil(db,order,p);audit(db,s.user,'pix_confirmed',order.user,{order_id:order.id,payment_id:payment,amount_centavos:amount});return{ok:true}
 });
 if(path==='supplier')return op(db,s,b,path,()=>{
  const supplier={name:String(b.name||'').trim().slice(0,150),document:String(b.document||'').replace(/\D/g,''),address:String(b.address||'').trim().slice(0,300),support_discord:'bidu0576'};
  const enable=b.enable_pix===true;if(enable&&(supplier.name.length<3||![11,14].includes(supplier.document.length)||supplier.address.length<10))fail(400,'supplier_identification_required');
  sql(db).prepare("UPDATE eco_config SET value=? WHERE key='supplier_json'").run(JSON.stringify(supplier));sql(db).prepare("UPDATE eco_config SET value=? WHERE key='pix_enabled'").run(enable?'1':'0');audit(db,s.user,'supplier_config',s.user,{pix_enabled:enable});return{ok:true}
 });
 if(path==='product')return op(db,s,b,path,()=>{const id=String(b.id||'').trim(),kind=String(b.kind||''),title=String(b.title||'').trim(),description=String(b.description||'').trim();if(!/^[a-z0-9_-]{3,60}$/.test(id)||!['event','pass','seal'].includes(kind)||title.length<3||description.length<10)fail(400,'invalid_product');const gold=b.gold_price?units(b.gold_price):0,bc=b.bcoin_price?units(b.bcoin_price):0;if(!gold&&!bc)fail(400,'product_price_required');sql(db).prepare('INSERT INTO eco_products VALUES(?,?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET title=excluded.title,description=excluded.description,gold_units=excluded.gold_units,bcoin_units=excluded.bcoin_units,active=excluded.active').run(id,kind,title.slice(0,100),description.slice(0,1000),gold,bc,b.active===false?0:1);audit(db,s.user,'economy_product',id,{kind,gold_units:gold,bcoin_units:bc});return{ok:true}});
 fail(404,'not_found');
}
