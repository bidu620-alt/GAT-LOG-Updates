import {createHash,randomUUID} from 'node:crypto';
import {priceFor,pricingSchema} from './economy-pricing.js';
import {readPassTier} from './pass-tiers.js';
export const CUSTOMIZATION_ID='profile_customization';
export const customizationProduct=()=>({id:CUSTOMIZATION_ID,kind:'personalization',title:'Personalização do perfil',description:'Compra única. Capa, fundo da página e mensagem de até 600 caracteres. Primeira edição incluída. Alterações posteriores cobradas em Gold. Posição e escurecimento. Incluído para quem já adquiriu a personalização. Temas dos passes continuam vinculados às temporadas concluídas.',cash_centavos:0,gold_units:1000000,bcoin_units:0});
const fail=(status,code)=>{throw Object.assign(new Error(code),{status,code})};
const parse=value=>{try{return JSON.parse(value||'{}')||{}}catch{return {}}};
function account(user){if(typeof user!=='string'||!/^[a-z0-9._-]{3,32}$/.test(user))fail(401,'invalid_session');return user;}
function owns(db,user){return !!db.sql.prepare("SELECT 1 FROM sqlite_master WHERE name='eco_entitlements' AND type='table'").get()&&!!db.sql.prepare('SELECT 1 FROM eco_entitlements WHERE user=? AND product_id=?').get(user,CUSTOMIZATION_ID)&&!!db.sql.prepare("SELECT 1 FROM eco_entitlements e WHERE e.user=? AND e.product_id=? AND (e.source<>'streamer-benefit' OR EXISTS(SELECT 1 FROM accounts a WHERE a.user=e.user AND a.role='streamer' AND a.disabled=0))").get(user,CUSTOMIZATION_ID);}
export function availableThemes(db,user){return [...new Set(readPassTier(db,user).seasons.map(s=>/^\d{4}-(0[1-9]|1[0-2])$/.exec(s.season_id)?.[1]).filter(Boolean))].sort();}
export function readCustomization(db,user){
 const value=parse(db.sql.prepare('SELECT value FROM meta WHERE key=?').get('profile_customization:'+user)?.value);
 const paid=owns(db,user);
 return {theme:typeof value.theme==='string'?value.theme:'auto',cover:paid?String(value.cover||''):'',position:Number.isFinite(value.position)?value.position:50,darkness:Number.isFinite(value.darkness)?value.darkness:65,background:paid?String(value.background||''):'',background_position:Number.isFinite(value.background_position)?value.background_position:50,background_darkness:Number.isFinite(value.background_darkness)?value.background_darkness:70,background_fixed:value.background_fixed!==false,bio:paid&&typeof value.bio==='string'?Array.from(value.bio).slice(0,600).join(''):'',updated_at:value.updated_at||null};
}
export function customizationState(db,user){account(user);pricingSchema(db);return {ok:true,enabled:true,version:3,edit_fee_units:priceFor(db,'profile_edit').gold_units,first_edit_included:!!db.sql.prepare('SELECT 1 FROM eco_profile_edit_credit WHERE user=?').get(user),gold_balance:(db.sql.prepare('SELECT gold_units FROM eco_wallets WHERE user=?').get(user)?.gold_units||0)/100,revision:hash(readCustomization(db,user)),owned:owns(db,user),product:{...customizationProduct(),...priceFor(db,'personalization')},themes:availableThemes(db,user),customization:readCustomization(db,user)};}
function image(value){
 if(typeof value!=='string'||value.length>230000)fail(413,'cover_too_large');if(!value)return '';
 const m=/^data:image\/(webp|png|jpeg);base64,([A-Za-z0-9+/]+={0,2})$/.exec(value);if(!m||m[2].length%4!==0)fail(400,'invalid_cover');
 const bytes=Buffer.from(m[2],'base64');if(bytes.length<12||bytes.toString('base64')!==m[2])fail(400,'invalid_cover');
 const valid=m[1]==='webp'?bytes.toString('ascii',0,4)==='RIFF'&&bytes.toString('ascii',8,12)==='WEBP':m[1]==='png'?bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])):bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
 if(!valid)fail(400,'invalid_cover');return value;
}
const hash=v=>createHash('sha256').update(JSON.stringify({...v,updated_at:undefined})).digest('hex');
export function saveCustomization(db,session,body){
 pricingSchema(db);
 const user=account(session?.user);if(body.user!==undefined&&body.user!==user)fail(403,'own_profile_only');
 const previous=readCustomization(db,user);
 const theme=String(body.theme??previous.theme);if(!['auto','default',...availableThemes(db,user)].includes(theme))fail(403,'theme_not_unlocked');
 const cover=image(body.cover??previous.cover),background=image(body.background??previous.background),bio=body.bio??previous.bio;
 if(typeof bio!=='string'||Array.from(bio).length>600)fail(400,'bio_too_long');
 if((cover||background||bio)&&!owns(db,user))fail(403,'customization_not_owned');
 const position=body.position??previous.position,darkness=body.darkness??previous.darkness,background_position=body.background_position??previous.background_position,background_darkness=body.background_darkness??previous.background_darkness,background_fixed=body.background_fixed??previous.background_fixed;
 if(!Number.isInteger(position)||position<0||position>100||!Number.isInteger(darkness)||darkness<30||darkness>90||!Number.isInteger(background_position)||background_position<0||background_position>100||!Number.isInteger(background_darkness)||background_darkness<30||background_darkness>90||typeof background_fixed!=='boolean')fail(400,'invalid_cover_settings');
 const value={theme,cover,position,darkness,background,background_position,background_darkness,background_fixed,bio,updated_at:new Date().toISOString()};
 db.sql.exec('SAVEPOINT profile_customization');try{
  const beforeHash=hash(previous),afterHash=hash(value),op=String(body.op_id||''),requestHash=afterHash;
  if(!/^[a-zA-Z0-9_-]{16,80}$/.test(op))fail(400,'operation_id_required');
  const prior=db.sql.prepare('SELECT request_hash FROM eco_profile_edits WHERE user=? AND op_id=?').get(user,op);
  if(prior){if(prior.request_hash!==requestHash)fail(409,'operation_id_reused');db.sql.exec('RELEASE profile_customization');return customizationState(db,user)}
  if(beforeHash===afterHash){db.sql.exec('RELEASE profile_customization');return customizationState(db,user)}
  if(body.revision!==beforeHash)fail(409,'profile_changed');
  const paid=owns(db,user),credit=paid&&db.sql.prepare('SELECT 1 FROM eco_profile_edit_credit WHERE user=?').get(user);
  const config=priceFor(db,'profile_edit'),fee=paid&&!credit?config.gold_units:0;
  if(paid&&config.active===false)fail(409,'product_disabled');
  if(body.confirmed_fee_units!==fee)fail(409,'price_changed');
  const id=randomUUID();
  if(fee){const result=db.sql.prepare('UPDATE eco_wallets SET gold_units=gold_units-?,updated_at=? WHERE user=? AND gold_units>=?').run(fee,value.updated_at,user,fee);if(!result.changes)fail(409,'insufficient_balance');const balance=db.sql.prepare('SELECT gold_units FROM eco_wallets WHERE user=?').get(user).gold_units;db.sql.prepare('INSERT INTO eco_ledger VALUES(?,?,?,?,?,?,?,?,?)').run(id,user,'profile_edit','gold',-fee,balance,id,value.updated_at,JSON.stringify({product:'profile_edit'}));}
  if(credit)db.sql.prepare('DELETE FROM eco_profile_edit_credit WHERE user=?').run(user);
  db.sql.prepare('INSERT INTO eco_profile_edits VALUES(?,?,?,?,?,?,?,?)').run(id,user,op,requestHash,beforeHash,afterHash,fee,value.updated_at);

  db.sql.prepare('INSERT INTO meta(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value').run('profile_customization:'+user,JSON.stringify(value));
  db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(value.updated_at,user,'profile_customization_save',user,JSON.stringify({theme,has_cover:!!cover,has_background:!!background,bio_length:Array.from(bio).length,position,darkness,background_position,background_darkness,background_fixed}));
  db.sql.exec('RELEASE profile_customization');
 }catch(e){db.sql.exec('ROLLBACK TO profile_customization; RELEASE profile_customization');throw e;}
 return customizationState(db,user);
}
