import {createHash,pbkdf2Sync} from 'node:crypto';
const sha256=x=>createHash('sha256').update(x).digest();
const bytesToHex=x=>Buffer.from(x).toString('hex');
const pbkdf2=(_,password,salt,options)=>pbkdf2Sync(password,salt,options.c,options.dkLen,'sha256');
import {cachedRead,invalidateRead} from './read-cache.js';
import {budgetState} from './budget-guard.js';
import {rankingReadiness, rankingMessage, advanceRankGuard, restoreDeliveredTrailer} from './ranking-telemetry.js';

const VERSION='1.0.48-local';
const MIN_KM=0;
const MAX_BODY=262144;
const ADMIN=new Set(['owner','admin','moderator']);
const POWER=new Set(['owner','admin']);
const ORIGINS=new Set(['https://gatlogets2.com.br','https://www.gatlogets2.com.br','https://bidu620-alt.github.io']);
const enc=new TextEncoder();
const now=()=>new Date().toISOString();
const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
const hex=b=>[...new Uint8Array(b)].map(x=>x.toString(16).padStart(2,'0')).join('');
const randomHex=n=>{const a=new Uint8Array(n);crypto.getRandomValues(a);return hex(a)};
const sha=async v=>hex(await crypto.subtle.digest('SHA-256',enc.encode(String(v))));
const month=v=>String(v||now()).slice(0,7);
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const compact=v=>norm(v).replace(/[^a-z0-9]/g,'');
const deep=(o,p)=>String(p).split('.').reduce((v,k)=>v&&typeof v==='object'?v[k]:undefined,o);
const pick=(o,...ps)=>{for(const p of ps){const v=deep(o,p);if(v!==undefined&&v!==null)return v}};
const num=(o,...ps)=>{const v=Number(pick(o,...ps));return Number.isFinite(v)?v:0};
const str=(o,...ps)=>String(pick(o,...ps)||'').trim();
const bool=(o,...ps)=>{const v=pick(o,...ps);return v===true||v===1||String(v).toLowerCase()==='true'};
const level=x=>Math.max(1,Math.floor((Number(x)||0)/2000)+1);
const scoreTier=(v,a,b,c)=>{v=Math.max(0,Number(v)||0);return v<=0?0:v<=a?3:v<=b?5:v<=c?10:15};
const damageAliasPct=(raw,key)=>{const v=deep(raw,key),n=Number(v);return v!==undefined&&v!==null&&Number.isFinite(n)?Math.max(0,n):0};
const damageRawPct=(raw,...keys)=>{for(const key of keys){const v=deep(raw,key),n=Number(v);if(v!==undefined&&v!==null&&Number.isFinite(n))return Math.max(0,n<=1.0001?n*100:n)}return 0};
const truckDamageParts=raw=>({engine:Math.max(damageAliasPct(raw,'truck_engine_damage_pct'),damageRawPct(raw,'truck.wearEngine','truck.engineWear','truck.engineDamage')),transmission:Math.max(damageAliasPct(raw,'truck_transmission_damage_pct'),damageRawPct(raw,'truck.wearTransmission','truck.transmissionWear','truck.transmissionDamage')),cabin:Math.max(damageAliasPct(raw,'truck_cabin_damage_pct'),damageRawPct(raw,'truck.wearCabin','truck.cabinWear','truck.cabinDamage')),chassis:Math.max(damageAliasPct(raw,'truck_chassis_damage_pct'),damageRawPct(raw,'truck.wearChassis','truck.chassisWear','truck.chassisDamage')),wheels:Math.max(damageAliasPct(raw,'truck_wheels_damage_pct'),damageRawPct(raw,'truck.wearWheels','truck.wheelsWear','truck.wheelsDamage'))});
const truckDamageOf=raw=>{const p=truckDamageParts(raw);return Math.max(damageAliasPct(raw,'truck_damage_pct'),p.engine,p.transmission,p.cabin,p.chassis,p.wheels)};
const trailerDamageOf=raw=>Math.max(damageAliasPct(raw,'trailer_damage_pct'),damageRawPct(raw,'trailers.0.wearChassis'),damageRawPct(raw,'trailers.0.wearWheels'),damageRawPct(raw,'trailers.0.wearBody'));
const damageDelta=(max,start)=>Math.max(0,(Number(max)||0)-(Number(start)||0));
const monthPoints=list=>(list||[]).filter(x=>String(x?.delivered_at||'').slice(0,7)===month()).reduce((sum,x)=>{const gp=Number(x?.gat_points);if(Number.isFinite(gp))return sum+Math.max(0,gp);return sum+Math.max(0,100-(Number(x?.penalty_xp)||0))},0);

class HttpError extends Error{constructor(status,code){super(code);this.status=status;this.code=code}}
function headers(req){const o=req.headers.get('Origin');return{'Access-Control-Allow-Origin':o&&ORIGINS.has(o)?o:'https://gatlogets2.com.br','Access-Control-Allow-Methods':'GET,POST,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Authorization','Cache-Control':'no-store','Vary':'Origin','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer'}}
function json(req,data,status=200){return new Response(JSON.stringify(data),{status,headers:{...headers(req),'Content-Type':'application/json; charset=utf-8'}})}
async function body(req){const declared=Number(req.headers.get('Content-Length')||0);if(declared>MAX_BODY)throw new HttpError(413,'payload_too_large');const text=await req.text();if(enc.encode(text).byteLength>MAX_BODY)throw new HttpError(413,'payload_too_large');try{const v=JSON.parse(text||'{}');return v&&typeof v==='object'&&!Array.isArray(v)?v:{}}catch{throw new HttpError(400,'invalid_json')}}
async function equal(a,b){const[x,y]=await Promise.all([crypto.subtle.digest('SHA-256',enc.encode(String(a))),crypto.subtle.digest('SHA-256',enc.encode(String(b)))]),xa=new Uint8Array(x),ya=new Uint8Array(y);let diff=xa.length^ya.length;for(let i=0;i<xa.length;i++)diff|=xa[i]^ya[i%ya.length];return diff===0}
// Keep the established cost so existing Cloudflare account hashes remain valid.
async function passHash(password,salt){return bytesToHex(pbkdf2(sha256,enc.encode(String(password)),enc.encode(String(salt)),{c:140000,dkLen:32}))}
// Legacy GAT Server accounts used 120,000 consecutive SHA-256 rounds over salt + NUL + password.
function legacyPassHash(password,salt){let x=enc.encode(String(salt)+'\x00'+String(password));for(let i=0;i<120000;i++)x=sha256(x);return bytesToHex(x)}
async function verifyPassword(env,account,password){if(!account?.password_hash||!account?.password_salt)return false;const current=await passHash(password,account.password_salt);if(await equal(current,account.password_hash))return true;const legacy=legacyPassHash(password,account.password_salt);if(!(await equal(legacy,account.password_hash)))return false;const salt=randomHex(16),hash=await passHash(password,salt);await env.DB.prepare('UPDATE accounts SET password_salt=?,password_hash=?,updated_at=? WHERE user=?').bind(salt,hash,now(),account.user).run();return true}
function bearer(req){const v=req.headers.get('Authorization')||'';return v.toLowerCase().startsWith('bearer ')?v.slice(7).trim():''}
async function makeSession(env,user){const token=randomHex(32),t=Date.now();await env.DB.prepare('INSERT INTO sessions(token_hash,user,created_at,expires_at) VALUES(?,?,?,?)').bind(await sha(token),user,new Date(t).toISOString(),new Date(t+30*86400000).toISOString()).run();return token}
async function sessionUser(env,token){if(!token)return null;const r=await env.DB.prepare('SELECT a.user,a.role,a.disabled,s.expires_at FROM sessions s JOIN accounts a ON a.user=s.user WHERE s.token_hash=?').bind(await sha(token)).first();return!r||r.disabled||Date.parse(r.expires_at)<=Date.now()?null:r}
async function requireSession(req,env,b){const s=await sessionUser(env,String(b.token||bearer(req)));if(!s)throw new HttpError(401,'invalid_session');return s}
async function requireAdmin(req,env,b,power=false){const s=await requireSession(req,env,b);if(!(power?POWER:ADMIN).has(s.role))throw new HttpError(403,'forbidden');return s}
async function ensureProfile(env,user){await env.DB.prepare('INSERT OR IGNORE INTO profiles(user,updated_at) VALUES(?,?)').bind(user,now()).run()}
async function audit(env,actor,action,target,details={}){await env.DB.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').bind(now(),actor,action,target,JSON.stringify(details)).run()}

function flat(driver,account,updated,raw){return{driver,account_user:account||'',updated_at:updated,rank_status:rankingReadiness(raw),telemetry:raw,on_job:bool(raw,'on_job','onJob','gameplay.onJob','job.onJob','Job.OnJob','job.active','Job.Active'),cargo_name:str(raw,'cargo_name','cargo','job.cargo','job.cargoName','Job.CargoName','job.cargo.name','job.name'),cargo_id:str(raw,'cargo_id','cargoId','job.cargoId','job.cargo.id','game.job.cargoId'),source_city:str(raw,'source_city','source','job.sourceCity','job.source.cityName'),source_city_id:str(raw,'source_city_id','sourceCityId','job.sourceCityId','job.source.id','game.job.sourceCityId'),destination_city:str(raw,'destination_city','destination','job.destinationCity','job.destination.cityName'),destination_city_id:str(raw,'destination_city_id','destinationCityId','job.destinationCityId','job.destination.id','game.job.destinationCityId'),mass_kg:num(raw,'mass_kg','cargo_mass','cargoMass','cargo_mass_kg','job.cargoMass','Job.CargoMass','job.mass_kg','job.cargo.mass_kg'),remaining_km:num(raw,'remaining_km')||num(raw,'distance_m','navigation.estimatedDistance')/1000,speed_kmh:Math.abs(num(raw,'speed_kmh','truck.speedKmh','truck.speed_kmh','truck.speed')),truck_make:str(raw,'truck_make','truck.make'),truck_model:str(raw,'truck_model','truck.model'),job_market:str(raw,'job_market','job.market'),map_x:num(raw,'map_x','truck.placement.x'),map_z:num(raw,'map_z','truck.placement.z'),map_heading:num(raw,'map_heading','truck.placement.heading'),gat_map:str(raw,'gat_map','map_mode','gatMap')||'base',job_latched:bool(raw,'job_latched','jobLatched'),job_latch_key:str(raw,'job_latch_key','jobLatchKey')}}
async function live(env){return cachedRead('live',5,()=>readLive(env))}
async function readLive(env){const r=await env.DB.prepare('SELECT driver,account_user,updated_at,telemetry_json FROM telemetry_live WHERE updated_at>=? ORDER BY updated_at DESC').bind(new Date(Date.now()-45000).toISOString()).all();return(r.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.telemetry_json||'{}')}catch{}return flat(x.driver,x.account_user,x.updated_at,raw)})}
async function profile(env,user){return cachedRead('profile:'+user,15,()=>readProfile(env,user))}
async function readProfile(env,user){const p=await env.DB.prepare('SELECT * FROM profiles WHERE user=?').bind(user).first();if(!p)return null;const d=await env.DB.prepare('SELECT id,sequence_no AS sequence,source,destination,cargo,weight_kg,distance_km,xp,xp AS xp_awarded,perfect,perfect AS perfect_trip,penalty_xp,speed_fines,delivered_at,delivered_at AS completed_at,raw_json FROM deliveries WHERE user=? ORDER BY id DESC LIMIT 100').bind(user).all();let mission=null;try{mission=p.current_mission_json?JSON.parse(p.current_mission_json):null}catch{}const deliveries=(d.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.raw_json||'{}')}catch{}return{...x,...raw.audit}}).reverse(),points=monthPoints(deliveries);return{user,monthly_completed:p.monthly_completed,monthly_goal:p.monthly_goal,total_deliveries:p.total_deliveries,total_km:p.total_km,xp:p.xp,level:level(p.xp),points,perfect_trips:p.perfect_trips,penalty_xp:p.penalty_xp,speed_fines:p.speed_fines,safety_score:p.safety_score,avatar_url:p.avatar_url||'',current_mission:mission,deliveries}}

async function resetAssigned(env,user,mission,reason,extra={}){const m={...mission,state:'assigned',min_km:MIN_KM,last_rejected_at:now(),last_rejected_reason:reason,...extra};for(const k of['cargo','cargo_id','source','source_city_id','destination','destination_city_id','weight_kg','planned_distance_km','rbr_start_remaining_km','map_mode','distance_source','started_at','suspended_at','resumed_at','job_latch_key','start_remaining_km','start_odometer_km','trip_progress_confirmed','rank_guard','delivery_details_start'])delete m[k];for(const k of Object.keys(m))if(/^(truck_|trailer_).*damage_(start|max)_pct$/.test(k))delete m[k];await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),now(),user).run();return m}
async function cargoOK(env,mission,cargo,cargoId){
 if(mission.catalog_id==='custom')return true;
 const r=await env.DB.prepare('SELECT compatible_cargos_json FROM work_catalog WHERE id=?').bind(String(mission.catalog_id||'')).first();let names=[];try{names=JSON.parse(r?.compatible_cargos_json||'[]')}catch{}
 const idRaw=String(cargoId||'').trim();
 if(idRaw){
   const base=idRaw.split('.')[0],id=compact(base);
   if(id&&names.some(n=>{const x=compact(n);return x&&(x===id||x.includes(id)||id.includes(x))}))return true;
 }
 const actual=norm(cargo);if(!actual)return false;
 return names.map(norm).some(n=>n&&(actual===n||actual.includes(n)||n.includes(actual)));
}

function cargoWordSet(v){
 const words=norm(v).split(' ').filter(x=>x.length>1).map(x=>x.length>4&&x.endsWith('s')?x.slice(0,-1):x);
 return new Set(words);
}
function cargoTextScore(a,b){
 const x=norm(a),y=norm(b);if(!x||!y)return 0;if(x===y)return 1;if(x.includes(y)||y.includes(x))return .92;
 const xs=cargoWordSet(x),ys=cargoWordSet(y);let hit=0;for(const w of xs)if(ys.has(w))hit++;
 return hit?Math.min(.78,.56+(hit/Math.max(1,Math.min(xs.size,ys.size)))*.22):0;
}
const AUTO_CARGO_RULES=[
 {cargo:/\b(diesel|gasolina|gasoline|petrol|benzina|querosene|kerosene|etanol|ethanol|propano|propane|lpg|lng|fuel|oleo combustivel)\b/,target:/(combust|fuel|petrol|inflam)/,score:.90},
 {cargo:/\b(solvente|quimic|chemical|acido|acid|cloro|chlor|sodio|sodium|hidroxido|hydroxide)\b/,target:/(quim|chemical)/,score:.90},
 {cargo:/\b(trator|tractor|escavadeira|excavator|bulldozer|dozer|locomotiva|locomotive|guindaste|crane|carregadeira|loader|colheitadeira|harvester|empilhadeira|forklift)\b/,target:/(pesad|maquin|equip)/,score:.88},
 {cargo:/\b(tora|toras|madeira|timber|logs?|lumber)\b/,target:/(madeir|tora|florest|timber)/,score:.90},
 {cargo:/\b(tijolo|brick|cimento|cement|concreto|concrete|material de construcao|construction material)\b/,target:/(construc|material)/,score:.88},
 {cargo:/\b(container|conteiner|cont[eê]iner)\b/,target:/(container|conteiner)/,score:.92},
 {cargo:/\b(carro|carros|automovel|automoveis|cars?|motocicleta|motorcycle|veiculo|vehicle|suvs?|sedans?|hatchbacks?)\b/,target:/(veicul|automov|carro)/,score:.90},
 {cargo:/\b(gado|cattle|vacas?|cows?|ovelhas?|sheep|porcos?|pigs?|animais?|animals?)\b/,target:/(animal|gado|pecuar|livestock)/,score:.88}
];
function catalogCargoScore(item,cargo){
 let best=0,names=[];try{names=JSON.parse(item.compatible_cargos_json||'[]')}catch{}
 for(const name of names)best=Math.max(best,cargoTextScore(cargo,name));
 best=Math.max(best,Math.min(.72,cargoTextScore(cargo,item.title||'')),Math.min(.68,cargoTextScore(cargo,item.category||'')));
 const c=norm(cargo),label=norm((item.title||'')+' '+(item.category||''));
 for(const rule of AUTO_CARGO_RULES)if(rule.cargo.test(c)&&rule.target.test(label))best=Math.max(best,rule.score);
 return best;
}
async function autoClassifyCargo(env,cargo){
 const key=norm(cargo);if(!key)return{work:null,confidence:0,suggested_work_id:null};
 const rows=await env.DB.prepare('SELECT id,position,title,category,icon,compatible_cargos_json FROM work_catalog WHERE active=1 ORDER BY position').all(),items=rows.results||[];
 const semanticRule=AUTO_CARGO_RULES.find(rule=>rule.cargo.test(key));
 if(semanticRule){
  const semanticMatches=items.filter(item=>semanticRule.target.test(norm((item.title||'')+' '+(item.category||''))));
  if(semanticMatches.length===1)return{work:semanticMatches[0],confidence:Number(semanticRule.score||0),suggested_work_id:semanticMatches[0].id,source:'automatic'};
 }
 const alias=await env.DB.prepare('SELECT ca.work_id,ca.confidence,ca.source,wc.id,wc.position,wc.title,wc.category,wc.icon,wc.compatible_cargos_json FROM cargo_aliases ca JOIN work_catalog wc ON wc.id=ca.work_id WHERE ca.cargo_key=? AND wc.active=1').bind(key).first();
 if(alias&&(String(alias.source||'')==='manual'||Number(alias.confidence||0)>=.85))return{work:alias,confidence:Math.max(.99,Number(alias.confidence)||0),suggested_work_id:alias.id,source:'learned'};
 const ranked=items.map(item=>({item,score:catalogCargoScore(item,cargo)})).sort((a,b)=>b.score-a.score||Number(a.item.position)-Number(b.item.position));
 const first=ranked[0]||{item:null,score:0};
 const safe=first.item&&first.score>=.85;
 return{work:safe?first.item:null,confidence:Number(first.score||0),suggested_work_id:first.item?.id||null,source:safe?'automatic':'pending'};
}
async function learnCargoAlias(env,cargo,workId,confidence=1,source='automatic'){
 const key=norm(cargo),name=String(cargo||'').trim();if(!key||!name||!workId)return;
 const t=now();
 await env.DB.prepare("INSERT INTO cargo_aliases(cargo_key,cargo_name,work_id,confidence,source,created_at,updated_at) VALUES(?,?,?,?,?,?,?) ON CONFLICT(cargo_key) DO UPDATE SET cargo_name=excluded.cargo_name,work_id=excluded.work_id,confidence=excluded.confidence,source=excluded.source,updated_at=excluded.updated_at").bind(key,name,String(workId),Math.max(0,Math.min(1,Number(confidence)||1)),String(source||'automatic'),t,t).run();
 const row=await env.DB.prepare('SELECT compatible_cargos_json FROM work_catalog WHERE id=?').bind(String(workId)).first();if(!row)return;
 let names=[];try{names=JSON.parse(row.compatible_cargos_json||'[]')}catch{}if(!Array.isArray(names))names=[];
 if(!names.some(x=>norm(x)===key)){names.push(name);await env.DB.prepare('UPDATE work_catalog SET compatible_cargos_json=? WHERE id=?').bind(JSON.stringify(names),String(workId)).run();}
}

async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){
 const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;if(row?.current_mission_json){try{m=JSON.parse(row.current_mission_json)}catch{m=null}}
 const adminTest=false;
 const observed=flat(user,user,t,raw);
 if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){
  const classification=await autoClassifyCargo(env,observed.cargo_name),item=classification.work,already=item?!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,item.id,month(t)).first()):false;
  m={id:`${month(t)}-${user}-${item?.id||'unclassified'}-${randomHex(6)}`,catalog_id:item?.id||'__unclassified__',sequence:item?.position||null,title:item?.title||'Carga a classificar',category:item?.category||'Trabalho aleatorio',state:'assigned',min_km:adminTest?0:MIN_KM,classification_mode:item?'automatic':'pending',classification_confidence:Number(classification.confidence||0),classification_suggested_work_id:classification.suggested_work_id||null,pending_classification:!item,xp_only:!!(item&&already&&!adminTest),created_at:t,assigned_at:t};
  await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 if(!m)return null;
 const f=flat(user,user,t,raw),isRbr=clean(f.gat_map).includes('rbr'),baseKm=num(raw,'job.plannedDistanceKm','planned_distance_km'),teleKm=f.remaining_km||num(raw,'distance_m','navigation.estimatedDistance')/1000,planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);
 const odoNow=num(raw,'truck.odometer','truck.odometerKm','truck.odometer_km');
 const minKm=0;
 const hasLoadedJob=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&Number(f.mass_kg)>0;
 if(hasLoadedJob){
   if(m.state==='suspended'){
     m={...m,state:'active',resumed_at:t,job_latch_key:f.job_latch_key||m.job_latch_key||''};delete m.suspended_at;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }else if(m.state!=='active'){
     if(planned<minKm)return{type:'mission_waiting',reason:'distance_below_minimum',distance_km:planned,min_km:minKm};
     m={...m,state:'active',min_km:minKm,cargo:f.cargo_name||m.cargo||'Carga',cargo_id:f.cargo_id||m.cargo_id||'',source:f.source_city||m.source||'',source_city_id:f.source_city_id||m.source_city_id||'',destination:f.destination_city||m.destination||'',destination_city_id:f.destination_city_id||m.destination_city_id||'',weight_kg:f.mass_kg||m.weight_kg||0,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,job_latch_key:f.job_latch_key||'',start_remaining_km:teleKm>0?teleKm:0,start_odometer_km:odoNow>0?odoNow:0,trip_progress_confirmed:false,rank_guard:adminTest?{reason:null,verified_at:t,last_sample_at:t,valid_samples:2,preflight_truck_damage_ready:true}:{reason:'telemetry_not_verified_from_start',valid_samples:rankingReadiness(raw).eligible?1:0,startup_started_at:t,last_sample_at:t,last_invalid_reason:rankingReadiness(raw).reason||null,preflight_truck_damage_ready:!!preflightTruckDamageReady},last_rejected_reason:undefined,last_rejected_at:undefined,delivery_details_start:JSON.stringify(pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{}),trip_id:String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||''),started_at:t};
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }
 }
 if(m.state==='active'||m.state==='suspended'){
   const readiness=rankingReadiness(raw),next=advanceRankGuard(m.rank_guard,readiness,m.started_at===t?t:previousAt,t,Boolean(m.trip_progress_confirmed));
   if(JSON.stringify(next)!==JSON.stringify(m.rank_guard)){
     m.rank_guard=next;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }
 }
 const truckNow=truckDamageOf(raw),truckPartsNow=truckDamageParts(raw),trailerNow=trailerDamageOf(raw);
 if(hasLoadedJob&&m.state==='active'){
   let damageChanged=false;
   const initDamage=(key,value)=>{if(!Number.isFinite(Number(m[key]))){m[key]=Math.max(0,Number(value)||0);damageChanged=true}};
   const maxDamage=(key,value)=>{const v=Math.max(0,Number(value)||0),old=Number(m[key]);if(!Number.isFinite(old)||v>old+0.0001){m[key]=Math.max(v,Number.isFinite(old)?old:0);damageChanged=true}};
   initDamage('truck_damage_start_pct',truckNow);
   initDamage('truck_engine_damage_start_pct',truckPartsNow.engine);
   initDamage('truck_transmission_damage_start_pct',truckPartsNow.transmission);
   initDamage('truck_cabin_damage_start_pct',truckPartsNow.cabin);
   initDamage('truck_chassis_damage_start_pct',truckPartsNow.chassis);
   initDamage('truck_wheels_damage_start_pct',truckPartsNow.wheels);
   initDamage('trailer_damage_start_pct',trailerNow);
   maxDamage('truck_damage_max_pct',truckNow);
   maxDamage('truck_engine_damage_max_pct',truckPartsNow.engine);
   maxDamage('truck_transmission_damage_max_pct',truckPartsNow.transmission);
   maxDamage('truck_cabin_damage_max_pct',truckPartsNow.cabin);
   maxDamage('truck_chassis_damage_max_pct',truckPartsNow.chassis);
   maxDamage('truck_wheels_damage_max_pct',truckPartsNow.wheels);
   maxDamage('trailer_damage_max_pct',trailerNow);
   if(damageChanged)await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 const gatJobEvent=clean(str(raw,'gat_job_event','gatJobEvent'));
 const packetTripId=String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||''),missionTripId=String(m.trip_id||m.job_latch_key||''),tripReplaced=!!(hasLoadedJob&&packetTripId&&missionTripId&&packetTripId!==missionTripId),observedIdle=clean(str(raw,'gat_job_state','gatJobState'))==='idle'&&!hasLoadedJob;
 const deliveryDetails=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},deliveryDetailsNow=JSON.stringify(deliveryDetails),deliveryDetailsStart=String(m.delivery_details_start||'');
 const deliveryDetailsPositive=num(deliveryDetails,'distanceKm','distance_km')>0||num(deliveryDetails,'revenue')>0||num(deliveryDetails,'earnedXp','earned_xp')>0;
 const deliveryDetailsChanged=(!hasLoadedJob||tripReplaced)&&deliveryDetailsStart&&deliveryDetailsNow!==deliveryDetailsStart&&deliveryDetailsPositive;
 const legacyDeliveryFallback=!hasLoadedJob&&!deliveryDetailsStart&&(adminTest||m.trip_progress_confirmed===true)&&teleKm>=0&&teleKm<=2&&deliveryDetailsPositive;
 const delivered=gatJobEvent==='delivered'||deliveryDetailsChanged||legacyDeliveryFallback||(!hasLoadedJob&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
 const cancelled=!delivered&&(observedIdle||tripReplaced||gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled')));
 if(m.state==='active'){
   let guardChanged=false;
   if(!(Number(m.start_remaining_km)>0)&&teleKm>0){m.start_remaining_km=teleKm;guardChanged=true}
   if(!(Number(m.start_odometer_km)>0)&&odoNow>0){m.start_odometer_km=odoNow;guardChanged=true}
   if(m.trip_progress_confirmed!==true){
     const startRem=Number(m.start_remaining_km)||0,startOdo=Number(m.start_odometer_km)||0;
     const byRemaining=startRem>0&&teleKm>=0&&(startRem-teleKm)>=1;
     const byOdometer=startOdo>0&&odoNow>0&&Math.abs(odoNow-startOdo)>=1;
     if(byRemaining||byOdometer){m.trip_progress_confirmed=true;guardChanged=true}
   }
   if(guardChanged)await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 if(!adminTest&&delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};
 if(!delivered&&cancelled&&(!hasLoadedJob||tripReplaced)&&(m.state==='active'||m.state==='suspended')){if(m.classification_mode==='automatic'||m.classification_mode==='pending'){await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(t,user).run();return{type:'mission_cancelled',reason:tripReplaced?'trip_replaced':'observed_job_end',trip_id:missionTripId||null,mission:null}}m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}
 if(!delivered&&!hasLoadedJob&&m.state==='active'){m={...m,state:'suspended',suspended_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();return{type:'mission_suspended',mission:m}}
 if(!delivered&&!hasLoadedJob&&m.state==='suspended')return{type:'mission_suspended',mission:m};
 if(!delivered)return hasLoadedJob?{type:'mission_in_progress',mission:m,distance_km:planned}:null;
 if(!adminTest&&(m.rank_guard?.reason||!m.rank_guard)){
   const reason=m.rank_guard?.reason||'telemetry_not_verified_from_start';
   await resetAssigned(env,user,m,reason);
   return{type:'delivery_rejected',reason,rank_eligible:false,gat_points:0,xp:0,message:rankingMessage(reason)};
 }
 if(!adminTest&&m.state!=='active')return{type:'delivery_rejected',reason:'mission_not_active'};const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr,distance=rbr?(Number(m.rbr_start_remaining_km)||Number(m.planned_distance_km)||teleKm||0):(Number(details.distanceKm)||Number(m.planned_distance_km)||baseKm||0);
 if(!adminTest&&distance<minKm){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:minKm}}
 const source=String(m.source||f.source_city||'Origem nao informada').trim()||'Origem nao informada',destination=String(m.destination||f.destination_city||'Destino nao informado').trim()||'Destino nao informado';
 const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||''),workAlreadyCompleted=adminTest?false:(Boolean(m.xp_only)||!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first()));if(!adminTest&&!workAlreadyCompleted&&await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first()){await resetAssigned(env,user,m,'route_already_used');return{type:'delivery_rejected',reason:'route_already_used'}}
 const missionId=String(m.id||`${mk}-${user}-${workId}-${m.created_at||m.assigned_at||''}`),guard=await env.DB.prepare('INSERT OR IGNORE INTO mission_completions(mission_id,user,completed_at) VALUES(?,?,?)').bind(missionId,user,t).run();if(!guard.meta?.changes)return{type:'delivery_ignored',reason:'duplicate_event'};
 const damageEventRaw=Math.max(0,Number(details.cargoDamage)||0),damageEventPct=damageEventRaw<=1.0001?damageEventRaw*100:damageEventRaw,damage=Math.max(damageEventPct,damageAliasPct(raw,'cargo_damage_pct')),
 truckStart=Math.max(0,Number(m.truck_damage_start_pct)||0),truckMax=Math.max(truckStart,Number(m.truck_damage_max_pct)||0,truckNow),
 engineStart=Math.max(0,Number(m.truck_engine_damage_start_pct)||0),engineMax=Math.max(engineStart,Number(m.truck_engine_damage_max_pct)||0,truckPartsNow.engine),engineDelta=damageDelta(engineMax,engineStart),
 transmissionStart=Math.max(0,Number(m.truck_transmission_damage_start_pct)||0),transmissionMax=Math.max(transmissionStart,Number(m.truck_transmission_damage_max_pct)||0,truckPartsNow.transmission),transmissionDelta=damageDelta(transmissionMax,transmissionStart),
 cabinStart=Math.max(0,Number(m.truck_cabin_damage_start_pct)||0),cabinMax=Math.max(cabinStart,Number(m.truck_cabin_damage_max_pct)||0,truckPartsNow.cabin),cabinDelta=damageDelta(cabinMax,cabinStart),
 chassisStart=Math.max(0,Number(m.truck_chassis_damage_start_pct)||0),chassisMax=Math.max(chassisStart,Number(m.truck_chassis_damage_max_pct)||0,truckPartsNow.chassis),chassisDelta=damageDelta(chassisMax,chassisStart),
 wheelsStart=Math.max(0,Number(m.truck_wheels_damage_start_pct)||0),wheelsMax=Math.max(wheelsStart,Number(m.truck_wheels_damage_max_pct)||0,truckPartsNow.wheels),wheelsDelta=damageDelta(wheelsMax,wheelsStart),
 aggregateTruckDelta=damageDelta(truckMax,truckStart),truckDamage=Math.max(aggregateTruckDelta,engineDelta,transmissionDelta,cabinDelta,chassisDelta,wheelsDelta),
 trailerStart=Math.max(0,Number(m.trailer_damage_start_pct)||0),trailerMax=Math.max(trailerStart,Number(m.trailer_damage_max_pct)||0,trailerNow),trailerDelta=damageDelta(trailerMax,trailerStart),
 fines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),baseXP=Math.floor(distance/100)*20,gatSpeedPenalty=adminTest?0:fines*3,gatCargoPenalty=adminTest?0:scoreTier(damage,3,7,15),gatTruckPenalty=adminTest?0:scoreTier(truckDamage,5,10,20),xpPenalty=gatSpeedPenalty+gatCargoPenalty+gatTruckPenalty,pointPenalty=Math.min(100,xpPenalty),gatPoints=Math.max(0,100-pointPenalty),perfect=adminTest?1:(damage<=0.5&&truckDamage<=0.5&&fines===0?1:0),bonus=perfect?5:0,penalty=xpPenalty,xp=Math.max(0,baseXP-penalty+bonus),cargo=m.cargo||f.cargo_name||m.custom_cargo||m.title||'Carga',weight=Number(m.weight_kg)||f.mass_kg||0,auditData={base_xp:baseXP,speed_penalty_xp:gatSpeedPenalty,cargo_penalty_xp:gatCargoPenalty,truck_penalty_xp:gatTruckPenalty,perfect_bonus_xp:bonus,cargo_damage_pct:damage,truck_damage_start_pct:truckStart,truck_damage_max_pct:truckMax,truck_damage_delta_pct:truckDamage,truck_overall_delta_pct:aggregateTruckDelta,truck_engine_damage_start_pct:engineStart,truck_engine_damage_max_pct:engineMax,truck_engine_damage_delta_pct:engineDelta,truck_transmission_damage_start_pct:transmissionStart,truck_transmission_damage_max_pct:transmissionMax,truck_transmission_damage_delta_pct:transmissionDelta,truck_cabin_damage_start_pct:cabinStart,truck_cabin_damage_max_pct:cabinMax,truck_cabin_damage_delta_pct:cabinDelta,truck_chassis_damage_start_pct:chassisStart,truck_chassis_damage_max_pct:chassisMax,truck_chassis_damage_delta_pct:chassisDelta,truck_wheels_damage_start_pct:wheelsStart,truck_wheels_damage_max_pct:wheelsMax,truck_wheels_damage_delta_pct:wheelsDelta,trailer_damage_start_pct:trailerStart,trailer_damage_max_pct:trailerMax,trailer_damage_delta_pct:trailerDelta,rank_verified:!adminTest,admin_test_mode:adminTest,rank_client_version:raw.gat_client_version,perfect_trip:!!perfect,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:gatSpeedPenalty,gat_cargo_penalty_points:gatCargoPenalty,gat_truck_penalty_points:gatTruckPenalty,gat_penalty_points:pointPenalty,gat_points:gatPoints};
 if(m.pending_classification){
  const pendingPoints=adminTest?100:gatPoints,pendingAudit={...auditData,gat_points:pendingPoints,classification_status:'pending',classification_confidence:Number(m.classification_confidence||0),classification_suggested_work_id:m.classification_suggested_work_id||null};
  await env.DB.batch([
   env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:pendingAudit,map_mode:rbr?'rbr':'base'})),
   env.DB.prepare('UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?').bind(distance,xp,pendingPoints,perfect,penalty,fines,penalty,t,user),
   env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)
  ]);
  const saved=await env.DB.prepare('SELECT id FROM deliveries WHERE user=? AND delivered_at=? ORDER BY id DESC LIMIT 1').bind(user,t).first();
  if(saved?.id)await env.DB.prepare("INSERT OR IGNORE INTO cargo_classification_queue(delivery_id,user,cargo,cargo_key,source,destination,weight_kg,distance_km,delivered_at,status,suggested_work_id,suggested_confidence) VALUES(?,?,?,?,?,?,?,?,?,'pending',?,?)").bind(saved.id,user,cargo,norm(cargo),source,destination,weight,distance,t,m.classification_suggested_work_id||null,Number(m.classification_confidence||0)).run();
  return{type:'delivery_completed_pending_classification',mission:m,distance_km:distance,xp_awarded:xp,gat_points:pendingPoints,classification_status:'pending',monthly_increment:1};
 }
 if(m.classification_mode==='automatic'&&workId)await learnCargoAlias(env,cargo,workId,m.classification_confidence,'automatic');
 if(false&&workAlreadyCompleted){
   await env.DB.prepare('UPDATE profiles SET xp=xp+?,current_mission_json=NULL,updated_at=? WHERE user=?').bind(xp,t,user).run();
   return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,perfect:!!perfect,gat_points:0,monthly_increment:0,rank_eligible:false,xp_only:true,min_km:MIN_KM}
 }
 await env.DB.batch([env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,Number(m.sequence)||null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:auditData,map_mode:rbr?'rbr':'base'})),env.DB.prepare('UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?').bind(distance,xp,perfect,penalty,fines,penalty,t,user),env.DB.prepare('INSERT OR IGNORE INTO work_completed(user,work_id,month_key,completed_at) VALUES(?,?,?,?)').bind(user,workId,mk,t),env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)]);
 return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,perfect:!!perfect,min_km:MIN_KM}
}

async function clientCredential(env,driver,device,token){if(!token)return{error:'token_required'};const hash=await sha(token),r=await env.DB.prepare('SELECT ct.token_hash,ct.driver,ct.account_user,ct.device_id,ct.revoked_at,ct.last_seen_at,a.disabled FROM client_tokens ct LEFT JOIN accounts a ON a.user=ct.account_user WHERE ct.token_hash=?').bind(hash).first();if(!r||r.revoked_at||!r.account_user||r.disabled)return{error:'link_required'};if(clean(r.driver)!==clean(driver))return{error:'driver_mismatch'};if(r.device_id&&device&&r.device_id!==device)return{error:'device_mismatch'};if(!r.last_seen_at||Date.now()-Date.parse(r.last_seen_at)>=60000)await env.DB.prepare('UPDATE client_tokens SET last_seen_at=? WHERE token_hash=?').bind(now(),hash).run();return{row:r}}
async function pairing(env,driver,device){const old=await env.DB.prepare('SELECT code_plain,expires_at FROM client_pairings WHERE driver=? AND device_id=? AND claimed_user IS NULL').bind(driver,device).first();if(old&&Date.parse(old.expires_at)>Date.now())return old;const code=randomHex(4).toUpperCase(),expires=new Date(Date.now()+15*60000).toISOString();await env.DB.prepare('INSERT INTO client_pairings(code_hash,code_plain,driver,device_id,created_at,expires_at,claimed_user) VALUES(?,?,?,?,?,?,NULL) ON CONFLICT(driver,device_id) DO UPDATE SET code_hash=excluded.code_hash,code_plain=excluded.code_plain,created_at=excluded.created_at,expires_at=excluded.expires_at,claimed_user=NULL').bind(await sha(code),code,driver,device,now(),expires).run();return{code_plain:code,expires_at:expires}}
async function finishPair(env,driver,device){const p=await env.DB.prepare('SELECT code_hash,claimed_user,expires_at FROM client_pairings WHERE driver=? AND device_id=?').bind(driver,device).first();if(!p?.claimed_user||Date.parse(p.expires_at)<=Date.now())return null;const token=randomHex(32);await env.DB.batch([env.DB.prepare('UPDATE client_tokens SET revoked_at=? WHERE device_id=? AND revoked_at IS NULL').bind(now(),device),env.DB.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at,revoked_at) VALUES(?,?,?,?,?,?,NULL)').bind(await sha(token),driver,p.claimed_user,device,now(),now()),env.DB.prepare('DELETE FROM client_pairings WHERE code_hash=?').bind(p.code_hash)]);return{token,account_user:p.claimed_user}}

async function adminDriver(env,target){const a=await env.DB.prepare('SELECT user,role,disabled,created_at,updated_at FROM accounts WHERE user=?').bind(target).first();if(!a)return null;const p=await profile(env,target),sessions=await env.DB.prepare('SELECT COUNT(*) total FROM sessions WHERE user=? AND expires_at>?').bind(target,now()).first(),tel=await env.DB.prepare('SELECT driver,account_user,updated_at,telemetry_json FROM telemetry_live WHERE account_user=? ORDER BY updated_at DESC LIMIT 1').bind(target).first();let liveData={online:false};if(tel){let raw={};try{raw=JSON.parse(tel.telemetry_json||'{}')}catch{}liveData={...flat(tel.driver,tel.account_user,tel.updated_at,raw),online:Date.now()-Date.parse(tel.updated_at)<30000}}return{account:{...a,disabled:!!a.disabled,active_sessions:Number(sessions?.total||0)},profile:p,live:liveData}}
async function recalc(env,user){const s=await env.DB.prepare('SELECT COUNT(*) total,COALESCE(SUM(distance_km),0) km,COALESCE(SUM(xp),0) xp,COALESCE(SUM(perfect),0) perfect,COALESCE(SUM(penalty_xp),0) penalties,COALESCE(SUM(speed_fines),0) fines FROM deliveries WHERE user=?').bind(user).first(),m=await env.DB.prepare('SELECT COUNT(*) total FROM deliveries WHERE user=? AND substr(delivered_at,1,7)=?').bind(user,month()).first();await env.DB.prepare('UPDATE profiles SET monthly_completed=?,total_deliveries=?,total_km=?,xp=?,perfect_trips=?,penalty_xp=?,speed_fines=?,safety_score=MAX(0,100-(?*0.1)),updated_at=? WHERE user=?').bind(Number(m?.total||0),Number(s?.total||0),Number(s?.km||0),Number(s?.xp||0),Number(s?.perfect||0),Number(s?.penalties||0),Number(s?.fines||0),Number(s?.penalties||0),now(),user).run()}
async function adminAction(req,env,b,actor){const action=String(b.action||''),target=clean(b.target);if(!target)throw new HttpError(400,'target_required');const a=await env.DB.prepare('SELECT user,role,disabled FROM accounts WHERE user=?').bind(target).first();if(!a)throw new HttpError(404,'not_found');if(a.role==='owner'&&actor.role!=='owner')throw new HttpError(403,'owner_protected');if(actor.role==='moderator'&&action!=='reset_mission')throw new HttpError(403,'forbidden');
 if(action==='reset_mission')await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(now(),target).run();
 else if(action==='block'||action==='unblock'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const disabled=action==='block'?1:0;await env.DB.batch([env.DB.prepare('UPDATE accounts SET disabled=?,updated_at=? WHERE user=?').bind(disabled,now(),target),...(disabled?[env.DB.prepare('DELETE FROM sessions WHERE user=?').bind(target),env.DB.prepare('UPDATE client_tokens SET revoked_at=? WHERE account_user=? AND revoked_at IS NULL').bind(now(),target)]:[])])}
 else if(action==='reset_password'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const password=String(b.password||'');if(password.length<8||password.length>128)throw new HttpError(400,'weak_password');const salt=randomHex(16);await env.DB.batch([env.DB.prepare('UPDATE accounts SET password_salt=?,password_hash=?,updated_at=? WHERE user=?').bind(salt,await passHash(password,salt),now(),target),env.DB.prepare('DELETE FROM sessions WHERE user=?').bind(target)])}
 else if(action==='role'){if(actor.role!=='owner'||a.role==='owner')throw new HttpError(403,'forbidden');const role=String(b.role||'');if(!['driver','moderator','admin'].includes(role))throw new HttpError(400,'invalid_role');await env.DB.prepare('UPDATE accounts SET role=?,updated_at=? WHERE user=?').bind(role,now(),target).run()}
 else if(action==='delete'){if(actor.role!=='owner'||a.role==='owner'||actor.user===target)throw new HttpError(403,'forbidden');await env.DB.prepare('DELETE FROM accounts WHERE user=?').bind(target).run()}
 else if(action==='set_progress'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const monthly=Math.trunc(Number(b.monthly_completed)),total=Math.trunc(Number(b.total_deliveries)),km=Number(b.total_km);if(!Number.isFinite(monthly)||!Number.isFinite(total)||!Number.isFinite(km)||monthly<0||monthly>40||total<monthly||km<0)throw new HttpError(400,'invalid_progress');await env.DB.prepare('UPDATE profiles SET monthly_completed=?,total_deliveries=?,total_km=?,updated_at=? WHERE user=?').bind(monthly,total,km,now(),target).run()}
 else if(action==='delete_delivery'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const id=Math.trunc(Number(b.delivery_id)),d=await env.DB.prepare('SELECT id,raw_json,delivered_at FROM deliveries WHERE id=? AND user=?').bind(id,target).first();if(!d)throw new HttpError(404,'delivery_not_found');let raw={};try{raw=JSON.parse(d.raw_json||'{}')}catch{}const work=String(raw?.mission?.catalog_id||'');await env.DB.batch([env.DB.prepare('DELETE FROM deliveries WHERE id=? AND user=?').bind(id,target),...(work?[env.DB.prepare('DELETE FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(target,work,month(d.delivered_at))]:[])]);await recalc(env,target)}
 else if(action==='set_delivery_xp'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const id=Math.trunc(Number(b.delivery_id)),xp=Math.trunc(Number(b.delivery_xp));if(!Number.isFinite(xp)||xp<0||xp>100000)throw new HttpError(400,'invalid_xp');const r=await env.DB.prepare('UPDATE deliveries SET xp=? WHERE id=? AND user=?').bind(xp,id,target).run();if(!r.meta?.changes)throw new HttpError(404,'delivery_not_found');await recalc(env,target)}else throw new HttpError(400,'invalid_action');await audit(env,actor.user,action,target,{role:b.role,delivery_id:b.delivery_id});return json(req,{ok:true,action,target})}

async function route(req,env){const u=new URL(req.url),p=u.pathname,m=req.method;if(m==='OPTIONS')return new Response(null,{status:204,headers:headers(req)});if(!['GET','POST'].includes(m))throw new HttpError(405,'method_not_allowed');
 if(p==='/health')return json(req,{ok:true,service:'GAT Central Local',agent_version:VERSION,time:now(),mission_min_km:MIN_KM,test_mode:false});
 if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'cloudflare-workers-d1',mission_min_km:MIN_KM,test_mode:false});
 if(p==='/api/client/server-info')return json(req,{ok:true,online:true,server_name:'GAT CENTRAL CLOUD',session_id:'CLOUD',players:(await live(env)).length,max_players:999});if(p==='/api/client/players')return json(req,{ok:true,players:(await live(env)).map(x=>x.driver)});
 if(p==='/api/account/register'&&m==='POST'){const b=await body(req),user=clean(b.user),password=String(b.password||'');if(!/^[a-z0-9._-]{3,32}$/.test(user))throw new HttpError(400,'invalid_user');if(password.length<8||password.length>128)throw new HttpError(400,'weak_password');if(await env.DB.prepare('SELECT 1 FROM accounts WHERE user=?').bind(user).first())throw new HttpError(409,'user_exists');const salt=randomHex(16),t=now();await env.DB.prepare('INSERT INTO accounts(user,password_salt,password_hash,role,created_at,updated_at) VALUES(?,?,?,?,?,?)').bind(user,salt,await passHash(password,salt),'driver',t,t).run();await ensureProfile(env,user);return json(req,{ok:true,user,role:'driver',token:await makeSession(env,user)},201)}
 if(p==='/api/account/login'&&m==='POST'){const b=await body(req),user=clean(b.user),key=`${req.headers.get('CF-Connecting-IP')||'unknown'}:${user}`,cutoff=new Date(Date.now()-15*60000).toISOString(),attempts=await env.DB.prepare('SELECT COUNT(*) total FROM auth_attempts WHERE attempt_key=? AND succeeded=0 AND at>=?').bind(key,cutoff).first();if(Number(attempts?.total||0)>=10)throw new HttpError(429,'too_many_attempts');const a=await env.DB.prepare('SELECT * FROM accounts WHERE user=?').bind(user).first(),valid=!!a&&!a.disabled&&!!a.password_hash&&await verifyPassword(env,a,String(b.password||''));await env.DB.prepare('INSERT INTO auth_attempts(at,attempt_key,succeeded) VALUES(?,?,?)').bind(now(),key,valid?1:0).run();if(!valid)throw new HttpError(401,'invalid_credentials');return json(req,{ok:true,user:a.user,role:a.role,token:await makeSession(env,a.user)})}
 if((p==='/api/account/session'||p==='/api/site/session')&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}
 if(p==='/api/account/password'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b),password=String(b.password||'');if(password.length<8||password.length>128)throw new HttpError(400,'weak_password');const salt=randomHex(16);await env.DB.batch([env.DB.prepare('UPDATE accounts SET password_salt=?,password_hash=?,updated_at=? WHERE user=?').bind(salt,await passHash(password,salt),now(),s.user),env.DB.prepare('DELETE FROM sessions WHERE user=?').bind(s.user)]);return json(req,{ok:true})}
 if(p==='/api/client/login'&&m==='POST'){const b=await body(req),driver=clean(b.driver),device=String(b.device_id||'').trim();if(!driver||device.length<16)throw new HttpError(400,'driver_and_device_required');if(b.token){const c=await clientCredential(env,driver,device,String(b.token));if(c.row)return json(req,{ok:true,driver,account_user:c.row.account_user,token:b.token})}const done=await finishPair(env,driver,device);if(done)return json(req,{ok:true,driver,...done});const pair=await pairing(env,driver,device);return json(req,{ok:false,error:'link_required',pairing_code:pair.code_plain,expires_at:pair.expires_at,link_url:'https://gatlogets2.com.br/motorista.html?tab=account'},428)}
 if(p==='/api/site/client/link'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b),code=String(b.pairing_code||'').trim().toUpperCase();if(!/^[A-F0-9]{8}$/.test(code))throw new HttpError(400,'invalid_pairing_code');const pair=await env.DB.prepare('SELECT code_hash,driver,device_id,expires_at,claimed_user FROM client_pairings WHERE code_hash=?').bind(await sha(code)).first();if(!pair||pair.claimed_user||Date.parse(pair.expires_at)<=Date.now())throw new HttpError(404,'pairing_not_found');const old=await env.DB.prepare('SELECT account_user FROM client_tokens WHERE device_id=? AND revoked_at IS NULL LIMIT 1').bind(pair.device_id).first();if(old?.account_user&&old.account_user!==s.user)throw new HttpError(409,'device_already_linked');await env.DB.prepare('UPDATE client_pairings SET claimed_user=? WHERE code_hash=?').bind(s.user,pair.code_hash).run();await audit(env,s.user,'link_device',s.user,{driver:pair.driver,device_id_suffix:String(pair.device_id).slice(-8)});return json(req,{ok:true,user:s.user,driver:pair.driver})}
 if(p==='/api/client/heartbeat'&&m==='POST'){const b=await body(req),c=await clientCredential(env,clean(b.driver),String(b.device_id||''),String(b.token||''));if(!c.row)throw new HttpError(401,c.error);return json(req,{ok:true,driver:c.row.driver,account_user:c.row.account_user,time:now()})}
 if(p==='/api/client/telemetry'&&m==='POST'){
   const b=await body(req),driver=clean(b.driver),device=String(b.device_id||''),c=await clientCredential(env,driver,device,String(b.token||''));
   if(!c.row)throw new HttpError(401,c.error);
   const raw=b.telemetry&&typeof b.telemetry==='object'&&!Array.isArray(b.telemetry)?b.telemetry:{},receivedAt=now(),account=c.row.account_user;
   const collectedRaw=str(raw,'gat_collected_at','gatCollectedAt'),collectedMs=Date.parse(collectedRaw),receiveMs=Date.parse(receivedAt);
   const queuedTimeOK=Number.isFinite(collectedMs)&&collectedMs<=receiveMs+30000&&collectedMs>=receiveMs-6*60*60*1000;
   const t=queuedTimeOK?new Date(collectedMs).toISOString():receivedAt;
   const previous=await env.DB.prepare('SELECT account_user,updated_at,telemetry_json FROM telemetry_live WHERE driver=?').bind(driver).first();
   let prevRaw=null;try{if(previous?.account_user===account)prevRaw=JSON.parse(previous.telemetry_json)}catch{}
   const packetId=str(raw,'gat_packet_id','gatPacketId');
   if(packetId&&prevRaw&&packetId===str(prevRaw,'gat_packet_id','gatPacketId'))
     return json(req,{ok:true,driver,account_user:account,updated_at:previous.updated_at,duplicate_packet:true,next_upload_ms:15000});
   const previousCollected=str(prevRaw||{},'gat_collected_at','gatCollectedAt'),previousCollectedMs=Date.parse(previousCollected);
   const previousSampleAt=Number.isFinite(previousCollectedMs)?new Date(previousCollectedMs).toISOString():(prevRaw?previous.updated_at:null);
   const f=flat(driver,account,t,raw),loaded=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&f.mass_kg>0;
   const event=clean(str(raw,'gat_job_event','gatJobEvent'));
   const deliveryDetailsRaw=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},previousDeliveryDetailsRaw=pick(prevRaw||{},'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{};
   const deliveryDetailsPositive=num(deliveryDetailsRaw,'distanceKm','distance_km')>0||num(deliveryDetailsRaw,'revenue')>0||num(deliveryDetailsRaw,'earnedXp','earned_xp')>0;
   const deliveryDetailsChanged=!loaded&&prevRaw&&JSON.stringify(deliveryDetailsRaw)!==JSON.stringify(previousDeliveryDetailsRaw)&&deliveryDetailsPositive;
   const delivered=event==='delivered'||deliveryDetailsChanged||(!loaded&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
   const cancelled=!delivered&&(event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled')));
   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);
   const readiness=rankingReadiness(raw);
   const signature=x=>{const q=flat(driver,account,t,x);return JSON.stringify([rankingReadiness(x),q.job_latched,q.on_job,q.job_latch_key,q.cargo_id,q.cargo_name,q.source_city,q.destination_city,q.mass_kg,q.gat_map,num(x,'job.plannedDistanceKm','planned_distance_km'),x.cargo_damage_pct,x.truck_engine_damage_pct,x.truck_transmission_damage_pct,x.truck_cabin_damage_pct,x.truck_chassis_damage_pct,x.truck_wheels_damage_pct,x.trailer_damage_pct,deep(x,'game.paused')])};
   if(!queuedTimeOK&&!delivered&&!cancelled&&prevRaw&&Date.parse(receivedAt)-Date.parse(previous.updated_at)<15000&&signature(raw)===signature(prevRaw))
     return json(req,{ok:true,driver,account_user:account,updated_at:previous.updated_at,rank_status:readiness,telemetry_deferred:true,next_upload_ms:15000});
   const previousIdle=!!prevRaw&&prevRaw?.game?.connected===true&&!bool(prevRaw,'job_latched','jobLatched')&&!bool(prevRaw,'on_job','onJob','gameplay.onJob');
   const preflightTruckDamageReady=previousIdle&&['truck_engine_damage_pct','truck_transmission_damage_pct','truck_cabin_damage_pct','truck_chassis_damage_pct','truck_wheels_damage_pct'].every(key=>typeof prevRaw[key]==='number'&&Number.isFinite(prevRaw[key])&&prevRaw[key]>=0&&prevRaw[key]<=100);
   let missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);
   // Se um novo trip_id chegou enquanto a missao anterior ainda estava ativa,
   // o mesmo pacote encerra a antiga e ja abre a nova. Nao esperamos 15 s nem
   // dependemos de um segundo evento do cliente.
   if(loaded&&missionEvent&&['mission_cancelled','delivery_completed','delivery_completed_pending_classification','delivery_completed_xp_only'].includes(String(missionEvent.type))){
     const firstEvent=missionEvent,nextEvent=await processMission(env,account,raw,t,previousSampleAt);
     if(nextEvent)missionEvent={...firstEvent,next_mission_event:nextEvent};
   }
   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);invalidateRead('get:/api/public/work/catalog:'+account);if(String(missionEvent.type).startsWith('delivery_completed')){invalidateRead('get:/api/public/ranking:');invalidateRead('get:/api/public/safety-ranking:')}}
   await env.DB.prepare('INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json) VALUES(?,?,?,?,?) ON CONFLICT(driver) DO UPDATE SET account_user=excluded.account_user,device_id=excluded.device_id,updated_at=excluded.updated_at,telemetry_json=excluded.telemetry_json').bind(driver,account,device,receivedAt,JSON.stringify(raw)).run();
   return json(req,{ok:true,driver,account_user:account,updated_at:receivedAt,collected_at:t,replayed:queuedTimeOK,rank_status:readiness,mission_event:missionEvent,next_upload_ms:15000});
 }

 if(p==='/api/public/account-live'&&m==='GET')return json(req,{ok:true,telemetry:await live(env),updated_at:now()});
 if(p==='/api/public/ranking'&&m==='GET'){const season=(await env.DB.prepare("SELECT value FROM meta WHERE key='season'").first())?.value||month(),mode=(await env.DB.prepare("SELECT value FROM meta WHERE key='operation_mode'").first())?.value||'official',r=await env.DB.prepare(`SELECT p.user,p.monthly_completed,p.monthly_goal,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km,COALESCE(s.points,0) AS points FROM profiles p JOIN accounts a ON a.user=p.user LEFT JOIN (SELECT user,SUM(COALESCE(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER),MAX(0,100-penalty_xp))) AS points FROM deliveries WHERE substr(delivered_at,1,7)=? GROUP BY user) s ON s.user=p.user WHERE a.disabled=0 ORDER BY points DESC,p.monthly_completed DESC,p.perfect_trips DESC,p.penalty_xp ASC,p.speed_fines ASC,p.user ASC`).bind(season).all();return json(req,{ok:true,operation_mode:mode,season,scoring:{base_per_delivery:100,max_monthly:3000},ranking:r.results||[]})}
 if(p==='/api/public/safety-ranking'&&m==='GET'){const r=await env.DB.prepare('SELECT p.user,p.safety_score AS score,p.perfect_trips,p.speed_fines,p.penalty_xp FROM profiles p JOIN accounts a ON a.user=p.user WHERE a.disabled=0 ORDER BY p.safety_score DESC,p.perfect_trips DESC,p.speed_fines ASC,p.user ASC').all();return json(req,{ok:true,ranking:r.results||[]})}
 if(p==='/api/public/driver'&&m==='GET'){const d=await profile(env,clean(u.searchParams.get('user')));if(!d)throw new HttpError(404,'not_found');return json(req,{ok:true,profile:d})}
 if(p==='/api/site/profile'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);let pr=await profile(env,s.user);if(!pr){await ensureProfile(env,s.user);pr=await readProfile(env,s.user)}return json(req,{ok:true,profile:pr})}
 if(p==='/api/site/profile/avatar'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);const avatar=String(b.avatar_url||'').trim();if(avatar){if(avatar.length>220000)throw new HttpError(413,'avatar_too_large');if(!/^data:image\/(?:webp|png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(avatar))throw new HttpError(400,'invalid_avatar')}await env.DB.prepare('UPDATE profiles SET avatar_url=?,updated_at=? WHERE user=?').bind(avatar||null,now(),s.user).run();await audit(env,s.user,avatar?'profile_avatar_update':'profile_avatar_remove',s.user,{bytes:avatar.length});return json(req,{ok:true,avatar_url:avatar})}
 if(p==='/api/public/work/catalog'&&m==='GET'){const user=clean(u.searchParams.get('user')),c=await env.DB.prepare('SELECT id,position,title,category,icon,custom,compatible_cargos_json FROM work_catalog WHERE active=1 ORDER BY position').all(),d=await env.DB.prepare('SELECT work_id FROM work_completed WHERE user=? AND month_key=?').bind(user,month()).all(),done=new Set((d.results||[]).map(x=>x.work_id));return json(req,{ok:true,catalog:(c.results||[]).map(x=>{let compatible=[];try{compatible=JSON.parse(x.compatible_cargos_json||'[]')}catch{}const{compatible_cargos_json,...item}=x;return{...item,custom:!!x.custom,compatible_cargos:compatible,completed:done.has(x.id)}}),mission_min_km:MIN_KM})}
 if((p==='/api/site/work/select'||p==='/api/site/work/take')&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);const pr=await env.DB.prepare('SELECT current_mission_json,monthly_completed FROM profiles WHERE user=?').bind(s.user).first();if(pr?.current_mission_json)throw new HttpError(409,'mission_already_active');const item=p.endsWith('/take')&&!b.work_id?await env.DB.prepare('SELECT wc.* FROM work_catalog wc WHERE wc.active=1 AND NOT EXISTS(SELECT 1 FROM work_completed d WHERE d.user=? AND d.work_id=wc.id AND d.month_key=?) ORDER BY wc.position LIMIT 1').bind(s.user,month()).first():await env.DB.prepare('SELECT * FROM work_catalog WHERE id=? AND active=1').bind(String(b.work_id||'')).first();if(!item)throw new HttpError(404,'invalid_work');const repeatXpOnly=!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first());const custom=String(b.custom_cargo||'').trim();if(item.custom&&(custom.length<2||custom.length>100))throw new HttpError(400,'custom_cargo_required');const t=now(),mission={id:`${month()}-${s.user}-${item.id}-${randomHex(6)}`,catalog_id:item.id,sequence:item.position,title:item.title,category:item.category,custom_cargo:custom,xp_only:repeatXpOnly,state:'assigned',min_km:MIN_KM,created_at:t,assigned_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(mission),t,s.user).run();return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),xp_only:repeatXpOnly,rules_enabled:true,admin_test_mode:false,operation_mode:'official'})}
 if(p==='/api/public/notice'&&m==='GET'){const rows=await env.DB.prepare("SELECT key,value FROM meta WHERE key IN ('site_notice_enabled','site_notice_title','site_notice_message','site_notice_updated_at')").all(),v=Object.fromEntries((rows.results||[]).map(x=>[x.key,x.value]));return json(req,{ok:true,enabled:v.site_notice_enabled==='1',title:v.site_notice_title||'AVISO GAT',message:v.site_notice_message||'',updated_at:v.site_notice_updated_at||null})}
 if(p==='/api/site/admin/notice'&&m==='POST'){const b=await body(req),a=await requireAdmin(req,env,b);if(b.save){const title=String(b.title||'').trim().slice(0,80),message=String(b.message||'').trim().slice(0,600),enabled=b.enabled?1:0;if(enabled&&!message)throw new HttpError(400,'notice_message_required');const t=now();await env.DB.batch([env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_enabled',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(String(enabled)),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_title',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(title||'AVISO GAT'),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_message',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(message),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_updated_at',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(t)]);await audit(env,a.user,'site_notice',a.user,{enabled:!!enabled,title:title||'AVISO GAT'});return json(req,{ok:true,enabled:!!enabled,title:title||'AVISO GAT',message,updated_at:t})}const rows=await env.DB.prepare("SELECT key,value FROM meta WHERE key IN ('site_notice_enabled','site_notice_title','site_notice_message','site_notice_updated_at')").all(),v=Object.fromEntries((rows.results||[]).map(x=>[x.key,x.value]));return json(req,{ok:true,enabled:v.site_notice_enabled==='1',title:v.site_notice_title||'AVISO GAT',message:v.site_notice_message||'',updated_at:v.site_notice_updated_at||null})}
 if(p==='/api/site/admin/unclassified'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),q=await env.DB.prepare("SELECT q.id,q.delivery_id,q.user,q.cargo,q.source,q.destination,q.weight_kg,q.distance_km,q.delivered_at,q.suggested_work_id,q.suggested_confidence,d.xp,d.raw_json FROM cargo_classification_queue q JOIN deliveries d ON d.id=q.delivery_id WHERE q.status='pending' ORDER BY q.delivered_at DESC,q.id DESC LIMIT 200").all(),c=await env.DB.prepare('SELECT id,position,title,category,icon FROM work_catalog WHERE active=1 ORDER BY position').all();
  return json(req,{ok:true,viewer_role:s.role,pending:q.results||[],catalog:c.results||[]});
 }
 if(p==='/api/site/admin/classify'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),queueId=Math.trunc(Number(b.queue_id)||0),workId=String(b.work_id||'').trim();if(queueId<1||!workId)throw new HttpError(400,'invalid_classification');
  const q=await env.DB.prepare("SELECT q.*,d.raw_json FROM cargo_classification_queue q JOIN deliveries d ON d.id=q.delivery_id WHERE q.id=? AND q.status='pending'").bind(queueId).first();if(!q)throw new HttpError(404,'classification_not_found');
  const item=await env.DB.prepare('SELECT id,position,title,category FROM work_catalog WHERE id=? AND active=1').bind(workId).first();if(!item)throw new HttpError(404,'invalid_work');
  const at=now(),mk=month(q.delivered_at||at);await learnCargoAlias(env,q.cargo,item.id,1,'manual');
  let raw={};try{raw=JSON.parse(q.raw_json||'{}')}catch{}raw.classification={status:'classified',work_id:item.id,work_title:item.title,classified_by:s.user,classified_at:at};if(raw.audit)raw.audit={...raw.audit,classification_status:'classified',classification_work_id:item.id,classification_by:s.user};
  await env.DB.prepare('UPDATE deliveries SET sequence_no=?,raw_json=? WHERE id=?').bind(Number(item.position)||null,JSON.stringify(raw),q.delivery_id).run();
  await env.DB.prepare("UPDATE cargo_classification_queue SET status='classified',classified_work_id=?,classified_by=?,classified_at=? WHERE id=?").bind(item.id,s.user,at,queueId).run();
  const inserted=await env.DB.prepare('INSERT OR IGNORE INTO work_completed(user,work_id,month_key,completed_at) VALUES(?,?,?,?)').bind(q.user,item.id,mk,at).run(),counted=Number(inserted.meta?.changes||0)>0;
  if(counted)await env.DB.prepare('UPDATE profiles SET updated_at=? WHERE user=?').bind(at,q.user).run();
  await audit(env,s.user,'classify_cargo',q.user,{queue_id:queueId,delivery_id:q.delivery_id,cargo:q.cargo,work_id:item.id,work_title:item.title,counted});
  return json(req,{ok:true,counted,user:q.user,cargo:q.cargo,work:item});
 }
 if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}
 if(p==='/api/site/admin/drivers'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b),r=await env.DB.prepare('SELECT a.user,a.role,a.disabled,a.created_at,p.monthly_completed,p.monthly_goal,p.xp,p.total_km,p.current_mission_json,t.driver,t.updated_at last_telemetry_at,t.telemetry_json FROM accounts a LEFT JOIN profiles p ON p.user=a.user LEFT JOIN telemetry_live t ON t.account_user=a.user ORDER BY a.user').all(),drivers=(r.results||[]).map(x=>{let mission=null,raw={};try{mission=x.current_mission_json?JSON.parse(x.current_mission_json):null}catch{}try{raw=x.telemetry_json?JSON.parse(x.telemetry_json):{}}catch{}const f=flat(x.driver||x.user,x.user,x.last_telemetry_at||'',raw);return{user:x.user,role:x.role,disabled:!!x.disabled,created_at:x.created_at,monthly_completed:Number(x.monthly_completed||0),monthly_goal:Number(x.monthly_goal||30),xp:Number(x.xp||0),level:level(x.xp),total_km:Number(x.total_km||0),current_mission:mission,last_telemetry_at:x.last_telemetry_at,online:!!x.last_telemetry_at&&Date.now()-Date.parse(x.last_telemetry_at)<30000,truck:[f.truck_make,f.truck_model].filter(Boolean).join(' '),cargo:f.cargo_name}});return json(req,{ok:true,viewer_role:s.role,drivers})}
 if(p==='/api/site/admin/driver'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b),d=await adminDriver(env,clean(b.target));if(!d)throw new HttpError(404,'not_found');return json(req,{ok:true,viewer_role:s.role,...d})}
 if(p==='/api/site/admin/audit'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b,true),r=await env.DB.prepare('SELECT id,at,actor,action,target,details FROM audit ORDER BY id DESC LIMIT 200').all();return json(req,{ok:true,viewer_role:s.role,audit:r.results||[]})}
 if(p==='/api/site/admin/action'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return adminAction(req,env,b,s)}
 if(p==='/api/site/admin/health'&&m==='POST'){const b=await body(req);await requireAdmin(req,env,b);const[a,o,l,bk,last]=await Promise.all([env.DB.prepare('SELECT COUNT(*) total FROM accounts').first(),env.DB.prepare('SELECT COUNT(*) total FROM telemetry_live WHERE updated_at>=?').bind(new Date(Date.now()-45000).toISOString()).first(),env.DB.prepare('SELECT MAX(updated_at) at FROM telemetry_live').first(),env.DB.prepare('SELECT COUNT(*) total FROM admin_backups').first(),env.DB.prepare('SELECT name,created_at FROM admin_backups ORDER BY id DESC LIMIT 1').first()]);return json(req,{ok:true,agent_version:VERSION,online_drivers:Number(o?.total||0),accounts:Number(a?.total||0),data_bytes:0,last_telemetry_at:l?.at||null,backup_count:Number(bk?.total||0),backup_keep:7,last_backup:last?.name||null})}
 if(p==='/api/site/admin/backup'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b,true),[a,pr,d,w,r,meta]=await Promise.all([env.DB.prepare('SELECT user,role,disabled,created_at,updated_at FROM accounts').all(),env.DB.prepare('SELECT * FROM profiles').all(),env.DB.prepare('SELECT * FROM deliveries').all(),env.DB.prepare('SELECT * FROM work_completed').all(),env.DB.prepare('SELECT * FROM routes_completed').all(),env.DB.prepare('SELECT * FROM meta').all()]),t=now(),name=`gat-central-${t.replace(/[:.]/g,'-')}`;await env.DB.batch([env.DB.prepare('INSERT INTO admin_backups(name,created_at,actor,snapshot_json) VALUES(?,?,?,?)').bind(name,t,s.user,JSON.stringify({accounts:a.results,profiles:pr.results,deliveries:d.results,work_completed:w.results,routes_completed:r.results,meta:meta.results})),env.DB.prepare('DELETE FROM admin_backups WHERE id NOT IN (SELECT id FROM admin_backups ORDER BY id DESC LIMIT 7)')]);await audit(env,s.user,'backup','central',{name});return json(req,{ok:true,backup:name})}
 if(p==='/api/migration/import'&&m==='POST'){if(!env.MIGRATION_KEY)throw new HttpError(403,'migration_disabled');const b=await body(req);if(!await equal(String(b.key||''),String(env.MIGRATION_KEY)))throw new HttpError(403,'forbidden');const users=Array.isArray(b.users)?b.users.slice(0,500):[];for(const x of users){const user=clean(x.user);if(!user)continue;await env.DB.prepare('INSERT INTO accounts(user,role,disabled,created_at,updated_at) VALUES(?,?,?,?,?) ON CONFLICT(user) DO UPDATE SET role=excluded.role,disabled=excluded.disabled,updated_at=excluded.updated_at').bind(user,x.role||'driver',x.disabled?1:0,x.created_at||now(),now()).run();await ensureProfile(env,user)}return json(req,{ok:true,imported:users.length})}
 throw new HttpError(404,'not_found')}

export default{async scheduled(event,env,ctx){
 if(budgetState(env).paused)return;
 ctx.waitUntil(env.DB.batch([
   env.DB.prepare('DELETE FROM sessions WHERE token_hash IN (SELECT token_hash FROM sessions WHERE expires_at<? ORDER BY expires_at LIMIT 1000)').bind(now()),
   env.DB.prepare('DELETE FROM auth_attempts WHERE id IN (SELECT id FROM auth_attempts WHERE at<? ORDER BY at LIMIT 1000)').bind(new Date(Date.now()-86400000).toISOString())
 ]));
},async fetch(req,env,ctx){try{
 const p=new URL(req.url).pathname,budget=budgetState(env);
 if(req.method==='OPTIONS')return new Response(null,{status:204,headers:headers(req)});
 if(p==='/api/public/service-status')return json(req,{ok:true,...budget});
 if(budget.paused&&p!=='/health'&&p!=='/api/public/version'){
   if(p==='/api/public/notice')return json(req,{ok:true,enabled:true,title:'PAUSA DO PLANO GRATUITO',message:budget.message+(budget.resumes_at?' Renovação: '+new Date(budget.resumes_at).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo'})+' (Brasília).':''),...budget});
   if(p==='/api/public/account-live')return json(req,{ok:true,telemetry:[],service_paused:true,...budget});
   const response=json(req,{ok:false,error:'free_tier_protection',...budget},503);response.headers.set('Retry-After','300');return response;
 }
 return await economicalRoute(req,env)}catch(e){const status=e instanceof HttpError?e.status:500,code=e instanceof HttpError?e.code:'internal_error';console.error(JSON.stringify({message:'request_failed',status,code,path:new URL(req.url).pathname,error:e instanceof Error?e.message:String(e)}));return json(req,{ok:false,error:code},status)}}};

async function economicalRoute(req,env){
 const url=new URL(req.url),ttls={'/api/public/ranking':60,'/api/public/safety-ranking':60,'/api/public/work/catalog':60,'/api/public/notice':60};
 const ttl=req.method==='GET'?ttls[url.pathname]:0;
 if(!ttl)return route(req,env);
 const user=url.pathname==='/api/public/work/catalog'?clean(url.searchParams.get('user')):'';
 if(user&&!/^[a-z0-9._-]{3,32}$/.test(user))throw new HttpError(400,'invalid_user');
 const data=await cachedRead('get:'+url.pathname+':'+user,ttl,async()=>{
   const response=await route(req,env);
   if(!response.ok)throw new HttpError(response.status,'read_failed');
   return response.json();
 });
 return json(req,data);
}
