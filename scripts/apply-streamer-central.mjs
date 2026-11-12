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
// O retorno do perfil muda entre atualizacoes da Central (Gold, estatisticas e
// historico). Inserir somente role, sem substituir a lista de campos existente.
const profileStart=worker.indexOf('async function readProfile(env,user){');
if(profileStart<0)throw new Error('Funcao readProfile ausente. Nenhum arquivo sera aplicado.');
const profileEnd=worker.indexOf('\n}\n',profileStart);
if(profileEnd<0)throw new Error('Fim da funcao readProfile nao encontrado. Nenhum arquivo sera aplicado.');
const segment=worker.slice(profileStart,profileEnd);
if(!/\brole\s*:\s*p\.role/.test(segment)){
 const returns=[...segment.matchAll(/return\s*\{\s*user\s*,/g)];
 if(returns.length!==1)throw new Error('Retorno do perfil tem formato inesperado ('+returns.length+'). Nenhum arquivo sera aplicado.');
 const insertion=profileStart+returns[0].index+returns[0][0].length;
 worker=worker.slice(0,insertion)+"role:p.role||'driver',"+worker.slice(insertion);
}
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
// Localizar o objeto retornado por state() em vez de depender de todos os campos
// de garagem, que variam entre versoes do BiduLog.
const stateStart=economy.indexOf('function state(db,s){');
if(stateStart<0)throw new Error('Funcao state() nao encontrada; Central inalterada.');
const stateEnd=economy.indexOf('function economyAction(',stateStart);
if(stateEnd<0)throw new Error('Limite da funcao state() nao encontrado; Central inalterada.');
const stateSlice=economy.slice(stateStart,stateEnd);
if(!/\bstreamer\s*:\s*\{\s*eligible\s*:/.test(stateSlice)){
 const ret=[...stateSlice.matchAll(/\breturn\s*\{/g)];
 if(ret.length!==1)throw new Error('state() com retorno inesperado ('+ret.length+'); Central inalterada.');
 const at=stateStart+ret[0].index+ret[0][0].length;
 const value="streamer:{eligible:s.role==='streamer',brand_key:s.role==='streamer'?(sql(db).prepare('SELECT brand_key FROM eco_streamer_rewards WHERE user=?').get(s.user)?.brand_key||null):null,free_events:s.role==='streamer',profile_customization:s.role==='streamer'},";
 economy=economy.slice(0,at)+value+economy.slice(at);
}
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
// Inserir o resgate antes das rotas existentes da economia, sem alterar
// as formas atuais de selecao de caminhão e compras.
if(!economy.includes("if(path==='streamer-brand')")){
 const start=economy.indexOf('function economyAction(db,s,path,b){');
 if(start<0)throw new Error('economyAction() ausente; Central inalterada.');
 const end=economy.indexOf("if(path==='pass-state')",start);
 if(end<0)throw new Error('Rotas da economia nao reconhecidas; Central inalterada.');
 const section=economy.slice(start,end);
 const match=[...section.matchAll(/\beligibleAccount\(db,s\)\s*;/g)];
 if(match.length!==1)throw new Error('Entrada de economyAction() inesperada ('+match.length+'); Central inalterada.');
 const at=start+match[0].index+match[0][0].length;
 economy=economy.slice(0,at)+'\n'+action+economy.slice(at);
}
if(!schema.includes('CREATE TABLE IF NOT EXISTS eco_streamer_rewards'))
 schema+='\nCREATE TABLE IF NOT EXISTS eco_streamer_rewards(user TEXT PRIMARY KEY REFERENCES accounts(user),brand_key TEXT NOT NULL,claimed_at TEXT NOT NULL);\n';
fs.mkdirSync(stage,{recursive:true});
for(const [name,contents] of Object.entries({'worker.js':worker,'economy-official.js':economy,'economy-schema.sql':schema}))
 fs.writeFileSync(path.join(stage,name),contents,'utf8');
console.log('Arquivos Streamer preparados, mantendo o restante do servidor.');
