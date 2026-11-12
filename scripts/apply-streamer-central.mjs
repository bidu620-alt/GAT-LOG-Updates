import fs from 'node:fs';
import path from 'node:path';
const runtime=process.argv[2],stage=process.argv[3];
if(!runtime||!stage)throw new Error('Informe runtime e pasta temporaria.');
const list=['worker.js','economy-official.js','economy-schema.sql'];
function one(src,oldValue,newValue,label){
 if(src.includes(newValue))return src;
 const n=src.split(oldValue).length-1;
 if(n!==1)throw new Error('Versao nao compativel em '+label+' (marcador '+n+'). Nenhum arquivo sera aplicado.');
 return src.replace(oldValue,newValue);
}
const inputs=Object.fromEntries(list.map(name=>[name,fs.readFileSync(path.join(runtime,name),'utf8')]));
let worker=inputs['worker.js'],economy=inputs['economy-official.js'],schema=inputs['economy-schema.sql'];
const importsOld="import {economyTrack,economyFinish,economyAction,ensureEconomy} from './economy-official.js';";
const importsNew="import {economyTrack,economyFinish,economyAction,ensureEconomy,syncStreamerBenefits,revokeStreamerBenefits} from './economy-official.js';";
worker=one(worker,importsOld,importsNew,'importacao da economia');
const roleOld="else if(action==='role'){if(actor.role!=='owner'||a.role==='owner')throw new HttpError(403,'forbidden');const role=String(b.role||'');if(!['driver','moderator','admin'].includes(role))throw new HttpError(400,'invalid_role');await env.DB.prepare('UPDATE accounts SET role=?,updated_at=? WHERE user=?').bind(role,now(),target).run()}";
const roleNew="else if(action==='role'){if(actor.role!=='owner'||a.role==='owner')throw new HttpError(403,'forbidden');const role=String(b.role||'');if(!['driver','streamer','moderator','admin'].includes(role))throw new HttpError(400,'invalid_role');await env.DB.prepare('UPDATE accounts SET role=?,updated_at=? WHERE user=?').bind(role,now(),target).run();if(role==='streamer')syncStreamerBenefits(env.DB,target);else if(a.role==='streamer')revokeStreamerBenefits(env.DB,target);invalidateRead('profile:'+target)}";
worker=one(worker,roleOld,roleNew,'cargo e autorizacao');
worker=one(worker,
 "const p=await env.DB.prepare('SELECT * FROM profiles WHERE user=?').bind(user).first();if(!p)return null;",
 "const p=await env.DB.prepare(\"SELECT p.*,COALESCE(a.role,'driver') AS role FROM profiles p LEFT JOIN accounts a ON a.user=p.user WHERE p.user=?\").bind(user).first();if(!p)return null;",
 'perfil do motorista');
worker=one(worker,
 "return{user,gold_balance:Number(goldWallet?.gold_units||0)/100,event_deliveries:",
 "return{user,role:p.role||'driver',gold_balance:Number(goldWallet?.gold_units||0)/100,event_deliveries:",
 'resposta do perfil');
worker=one(worker,
 "if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}",
 "if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role,streamer_role_supported:true})}",
 'versao da Central');
const feature=[
 "const STREAMER_SOURCE='streamer-benefit';",
 "export function syncStreamerBenefits(db,user){",
 " ensureEconomy(db);",
 " if(sql(db).prepare('SELECT role FROM accounts WHERE user=?').get(user)?.role!=='streamer')return;",
 " tx(db,()=>{",
 "  const t=now(),gifts=[{id:'event_iceland_2026',title:'Islândia — Terra de Fogo e Gelo',kind:'event'},",
 "   {id:'profile_customization',title:'Personalização do perfil',kind:'personalization'},",
 "   ...sql(db).prepare(\"SELECT id,title,kind FROM eco_products WHERE kind='event' AND active=1\").all()];",
 "  for(const g of gifts)sql(db).prepare('INSERT OR IGNORE INTO eco_entitlements(user,product_id,title,kind,acquired_at,source) VALUES(?,?,?,?,?,?)').run(user,g.id,g.title,g.kind,t,STREAMER_SOURCE);",
 "  const claim=sql(db).prepare('SELECT brand_key FROM eco_streamer_rewards WHERE user=?').get(user);",
 "  if(claim?.brand_key&&Object.hasOwn(BRANDS,claim.brand_key))sql(db).prepare('INSERT OR IGNORE INTO eco_brands(user,brand_key,acquired_at,source) VALUES(?,?,?,?)').run(user,claim.brand_key,t,STREAMER_SOURCE);",
 " });",
 "}",
 "export function revokeStreamerBenefits(db,user){",
 " ensureEconomy(db);",
 " tx(db,()=>{",
 "  sql(db).prepare(\"DELETE FROM eco_selected WHERE user=? AND brand_key IN (SELECT brand_key FROM eco_brands WHERE user=? AND source=?)\").run(user,user,STREAMER_SOURCE);",
 "  sql(db).prepare('DELETE FROM eco_brands WHERE user=? AND source=?').run(user,STREAMER_SOURCE);",
 "  sql(db).prepare('DELETE FROM eco_entitlements WHERE user=? AND source=?').run(user,STREAMER_SOURCE);",
 " });",
 "}",
 ""
].join('\n');
if(!economy.includes("const STREAMER_SOURCE='streamer-benefit';")){
 economy=one(economy,"function eligibleAccount(db,s){ensureEconomy(db);if(!s?.user)",feature+"function eligibleAccount(db,s){ensureEconomy(db);if(!s?.user)",'registro das vantagens');
}
economy=one(economy,
 "function eligibleAccount(db,s){ensureEconomy(db);if(!s?.user)fail(401,'invalid_session');",
 "function eligibleAccount(db,s){ensureEconomy(db);if(!s?.user)fail(401,'invalid_session');if(s.role==='streamer')syncStreamerBenefits(db,s.user);",
 'ativacao das vantagens');
economy=one(economy,
 "ok:true,mode:'official',rule:'brand-v1',user:s.user,is_admin:isAdmin(s),gold_balance:w.gold_units/100,bcoin_balance:w.bcoin_units/100,",
 "ok:true,mode:'official',rule:'brand-v1',user:s.user,is_admin:isAdmin(s),streamer:{eligible:s.role==='streamer',brand_key:s.role==='streamer'?(sql(db).prepare('SELECT brand_key FROM eco_streamer_rewards WHERE user=?').get(s.user)?.brand_key||null):null,free_events:s.role==='streamer',profile_customization:s.role==='streamer'},gold_balance:w.gold_units/100,bcoin_balance:w.bcoin_units/100,",
 'estado da garagem');
const action=[
 " if(path==='streamer-brand')return op(db,s,b,path,()=>{",
 "  if(s.role!=='streamer')fail(403,'streamer_required');",
 "  const key=clean(b.brand_key);if(!Object.hasOwn(BRANDS,key))fail(400,'invalid_brand');",
 "  if(sql(db).prepare('SELECT 1 FROM eco_streamer_rewards WHERE user=?').get(s.user))fail(409,'streamer_brand_claimed');",
 "  if(owned(db,s.user,key))fail(409,'brand_already_owned');",
 "  const t=now();",
 "  sql(db).prepare('INSERT INTO eco_streamer_rewards(user,brand_key,claimed_at) VALUES(?,?,?)').run(s.user,key,t);",
 "  sql(db).prepare('INSERT INTO eco_brands(user,brand_key,acquired_at,source) VALUES(?,?,?,?)').run(s.user,key,t,STREAMER_SOURCE);",
 "  audit(db,s.user,'streamer_brand_claimed',s.user,{brand_key:key});",
 "  return{ok:true,brand_key:key,message:'Marca '+BRANDS[key]+' liberada gratuitamente para sua conta.'};",
 " });",
 ""
].join('\n');
if(!economy.includes("if(path==='streamer-brand')"))
 economy=one(economy," if(path==='select-truck')return op(db,s,b,path,()=>economySelect(db,s.user,String(b.model_key||'')));",action+" if(path==='select-truck')return op(db,s,b,path,()=>economySelect(db,s.user,String(b.model_key||'')));",'escolha da marca');
if(!schema.includes('CREATE TABLE IF NOT EXISTS eco_streamer_rewards'))
 schema+='\nCREATE TABLE IF NOT EXISTS eco_streamer_rewards(user TEXT PRIMARY KEY REFERENCES accounts(user),brand_key TEXT NOT NULL,claimed_at TEXT NOT NULL);\n';
fs.mkdirSync(stage,{recursive:true});
for(const [name,contents] of Object.entries({'worker.js':worker,'economy-official.js':economy,'economy-schema.sql':schema}))
 fs.writeFileSync(path.join(stage,name),contents,'utf8');
console.log('Arquivos Streamer preparados, mantendo o restante do servidor.');
