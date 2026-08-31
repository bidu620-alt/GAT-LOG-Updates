const CORS={
  'Access-Control-Allow-Origin':'*',
  'Access-Control-Allow-Methods':'GET,POST,OPTIONS',
  'Access-Control-Allow-Headers':'Content-Type,Authorization',
  'Cache-Control':'no-store'
};

const MISSION_MIN_KM=5; // TEMPORARIO: teste da migracao Cloudflare

const enc=new TextEncoder();
const now=()=>new Date().toISOString();
const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
const json=(data,status=200)=>new Response(JSON.stringify(data),{status,headers:{...CORS,'Content-Type':'application/json; charset=utf-8'}});
const body=async req=>{try{return await req.json()}catch(_){try{return JSON.parse(await req.text())}catch(__){return {}}}};
const hex=buf=>[...new Uint8Array(buf)].map(x=>x.toString(16).padStart(2,'0')).join('');
const randHex=n=>{const a=new Uint8Array(n);crypto.getRandomValues(a);return [...a].map(x=>x.toString(16).padStart(2,'0')).join('')};
const sha=async s=>hex(await crypto.subtle.digest('SHA-256',enc.encode(String(s))));
const deep=(obj,path)=>{let v=obj;for(const part of String(path).split('.')){if(v==null||typeof v!=='object')return undefined;v=v[part]}return v};
const pick=(obj,...paths)=>{for(const path of paths){const v=deep(obj,path);if(v!==undefined&&v!==null)return v}return undefined};
const nval=(obj,...paths)=>{const n=Number(pick(obj,...paths));return Number.isFinite(n)?n:0};
const sval=(obj,...paths)=>String(pick(obj,...paths)||'').trim();
const bval=(obj,...paths)=>{const v=pick(obj,...paths);return v===true||v===1||String(v).toLowerCase()==='true'};

async function passHash(password,salt){
  const key=await crypto.subtle.importKey('raw',enc.encode(password),'PBKDF2',false,['deriveBits']);
  const bits=await crypto.subtle.deriveBits({name:'PBKDF2',hash:'SHA-256',salt:enc.encode(salt),iterations:140000},key,256);
  return hex(bits);
}
async function makeSession(env,user){const token=randHex(32),h=await sha(token),t=Date.now();await env.DB.prepare('INSERT INTO sessions(token_hash,user,created_at,expires_at) VALUES(?,?,?,?)').bind(h,user,new Date(t).toISOString(),new Date(t+30*86400000).toISOString()).run();return token}
async function sessionUser(env,token){if(!token)return null;const h=await sha(token),r=await env.DB.prepare('SELECT a.user,a.role,a.disabled,s.expires_at FROM sessions s JOIN accounts a ON a.user=s.user WHERE s.token_hash=?').bind(h).first();if(!r||r.disabled||Date.parse(r.expires_at)<Date.now())return null;return r}
async function clientAuth(env,driver,device,token){if(!token)return null;const h=await sha(token),r=await env.DB.prepare('SELECT driver,device_id FROM client_tokens WHERE token_hash=?').bind(h).first();if(!r||clean(r.driver)!==clean(driver))return null;if(r.device_id&&device&&r.device_id!==device)return null;await env.DB.prepare('UPDATE client_tokens SET last_seen_at=? WHERE token_hash=?').bind(now(),h).run();return r}
async function ensureProfile(env,user){const t=now();await env.DB.prepare("INSERT OR IGNORE INTO profiles(user,updated_at) VALUES(?,?)").bind(user,t).run()}
function levelFromXP(xp){return Math.max(1,Math.floor((Number(xp)||0)/2000)+1)}

function telemetryFlat(driver,accountUser,updated,raw){
  return {
    driver,account_user:accountUser||driver,updated_at:updated,telemetry:raw,
    on_job:bval(raw,'on_job','onJob','gameplay.onJob','job.onJob','job.active'),
    cargo_name:sval(raw,'cargo_name','cargo','job.cargo','job.cargoName','job.cargo.name'),
    source_city:sval(raw,'source_city','source','job.sourceCity','job.source.cityName'),
    destination_city:sval(raw,'destination_city','destination','job.destinationCity','job.destination.cityName'),
    mass_kg:nval(raw,'mass_kg','cargo_mass','cargoMass','job.cargoMass','job.mass_kg'),
    remaining_km:nval(raw,'remaining_km')||nval(raw,'distance_m','navigation.estimatedDistance')/1000,
    speed_kmh:Math.abs(nval(raw,'speed_kmh','truck.speedKmh','truck.speed_kmh','truck.speed')),
    truck_make:sval(raw,'truck_make','truck.make'),truck_model:sval(raw,'truck_model','truck.model'),
    map_x:nval(raw,'map_x','truck.placement.x'),map_z:nval(raw,'map_z','truck.placement.z'),map_heading:nval(raw,'map_heading','truck.placement.heading'),
    gat_map:sval(raw,'gat_map','map_mode','gatMap')||'base'
  };
}
async function profileData(env,user){
  const p=await env.DB.prepare('SELECT * FROM profiles WHERE user=?').bind(user).first();
  if(!p)return null;
  const d=await env.DB.prepare('SELECT sequence_no AS sequence,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at FROM deliveries WHERE user=? ORDER BY id DESC LIMIT 100').bind(user).all();
  let mission=null;try{mission=p.current_mission_json?JSON.parse(p.current_mission_json):null}catch(_){}
  return {user,monthly_completed:p.monthly_completed,monthly_goal:p.monthly_goal,total_deliveries:p.total_deliveries,total_km:p.total_km,xp:p.xp,level:levelFromXP(p.xp),points:p.points,perfect_trips:p.perfect_trips,penalty_xp:p.penalty_xp,speed_fines:p.speed_fines,current_mission:mission,deliveries:(d.results||[]).reverse()};
}
async function ranking(env){const r=await env.DB.prepare('SELECT p.user,p.monthly_completed,p.monthly_goal,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km FROM profiles p JOIN accounts a ON a.user=p.user WHERE a.disabled=0 ORDER BY p.monthly_completed DESC,p.perfect_trips DESC,p.penalty_xp ASC,p.speed_fines ASC,p.user ASC').all();return r.results||[]}
async function liveData(env){const cutoff=new Date(Date.now()-45000).toISOString(),r=await env.DB.prepare('SELECT driver,account_user,updated_at,telemetry_json FROM telemetry_live WHERE updated_at>=? ORDER BY updated_at DESC').bind(cutoff).all();return (r.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.telemetry_json||'{}')}catch(_){}return telemetryFlat(x.driver,x.account_user,x.updated_at,raw)})}

async function processMissionTelemetry(env,user,raw,t){
  if(!user)return null;
  await ensureProfile(env,user);
  const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();
  if(!row?.current_mission_json)return null;
  let mission;try{mission=JSON.parse(row.current_mission_json)}catch(_){return null}
  if(!mission||typeof mission!=='object')return null;

  const flat=telemetryFlat(user,user,t,raw);
  const cargo=flat.cargo_name,source=flat.source_city,destination=flat.destination_city,weight=flat.mass_kg;
  const plannedKm=nval(raw,'job.plannedDistanceKm','planned_distance_km');

  if(flat.on_job&&cargo){
    const changed=mission.state!=='in_progress'||mission.cargo!==cargo||mission.source!==source||mission.destination!==destination;
    if(changed){
      mission={...mission,state:'in_progress',min_km:MISSION_MIN_KM,cargo,source,destination,weight_kg:weight,planned_distance_km:plannedKm,started_at:mission.started_at||t};
      await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(mission),t,user).run();
    }
  }

  const delivered=bval(raw,'gameplay.jobDelivered','jobDelivered');
  if(!delivered)return flat.on_job?{type:'mission_in_progress',mission}:null;

  const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{};
  const distanceKm=Number(details.distanceKm)||Number(mission.planned_distance_km)||plannedKm||0;
  const minKm=MISSION_MIN_KM;

  if(distanceKm<minKm){
    mission={...mission,state:'assigned',min_km:minKm,last_rejected_at:t,last_rejected_reason:'distance_below_minimum',last_distance_km:distanceKm};
    delete mission.cargo;delete mission.source;delete mission.destination;delete mission.weight_kg;delete mission.planned_distance_km;delete mission.started_at;
    await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(mission),t,user).run();
    return {type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distanceKm,min_km:minKm};
  }

  const earnedXp=Math.max(0,Number(details.earnedXp)||0);
  const cargoDamage=Math.max(0,Number(details.cargoDamage)||0);
  const perfect=cargoDamage<=0.001?1:0;
  const finalCargo=mission.cargo||cargo||mission.custom_cargo||mission.title||'Carga';
  const finalSource=mission.source||source||'';
  const finalDestination=mission.destination||destination||'';
  const finalWeight=Number(mission.weight_kg)||weight||0;
  const rawJson=JSON.stringify({mission,delivery_details:details,test_min_km:minKm});

  await env.DB.batch([
    env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,Number(mission.sequence)||null,finalSource,finalDestination,finalCargo,finalWeight,distanceKm,earnedXp,perfect,0,0,t,rawJson),
    env.DB.prepare('UPDATE profiles SET monthly_completed=monthly_completed+1,total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,perfect_trips=perfect_trips+?,current_mission_json=NULL,updated_at=? WHERE user=?').bind(distanceKm,earnedXp,perfect,t,user),
    env.DB.prepare('INSERT OR IGNORE INTO work_completed(user,work_id,month_key,completed_at) VALUES(?,?,?,?)').bind(user,String(mission.catalog_id||''),t.slice(0,7),t)
  ]);

  return {type:'delivery_completed',user,cargo:finalCargo,source:finalSource,destination:finalDestination,distance_km:distanceKm,xp:earnedXp,perfect:!!perfect,min_km:minKm};
}

async function handle(req,env){
  const u=new URL(req.url),p=u.pathname,m=req.method;
  if(m==='OPTIONS')return new Response(null,{status:204,headers:CORS});
  if(p==='/health')return json({ok:true,service:'GAT Central Cloud',time:now(),mission_min_km:MISSION_MIN_KM,test_mode:true});
  if(p==='/api/public/version')return json({ok:true,agent_version:'cloud-1.0.1-test',platform:'cloudflare-workers-d1',mission_min_km:MISSION_MIN_KM});
  if(p==='/api/client/server-info')return json({ok:true,online:true,server_name:'GAT CENTRAL CLOUD',session_id:'CLOUD',players:(await liveData(env)).length,max_players:999});
  if(p==='/api/client/players')return json({ok:true,players:(await liveData(env)).map(x=>x.driver)});

  if(p==='/api/account/register'&&m==='POST'){
    const b=await body(req),user=clean(b.user),password=String(b.password||'');
    if(!/^[a-z0-9._-]{3,32}$/.test(user))return json({ok:false,error:'invalid_user'},400);
    if(password.length<6)return json({ok:false,error:'weak_password'},400);
    if(await env.DB.prepare('SELECT user FROM accounts WHERE user=?').bind(user).first())return json({ok:false,error:'user_exists'},409);
    const salt=randHex(16),ph=await passHash(password,salt),t=now();
    await env.DB.prepare('INSERT INTO accounts(user,password_salt,password_hash,role,created_at,updated_at) VALUES(?,?,?,?,?,?)').bind(user,salt,ph,'driver',t,t).run();await ensureProfile(env,user);
    return json({ok:true,user,token:await makeSession(env,user)});
  }
  if(p==='/api/account/login'&&m==='POST'){
    const b=await body(req),user=clean(b.user),password=String(b.password||''),a=await env.DB.prepare('SELECT * FROM accounts WHERE user=?').bind(user).first();
    if(!a||a.disabled||!a.password_hash||await passHash(password,a.password_salt)!==a.password_hash)return json({ok:false,error:'invalid_credentials'},401);
    return json({ok:true,user:a.user,token:await makeSession(env,a.user),role:a.role});
  }
  if((p==='/api/account/session'||p==='/api/site/session')&&m==='POST'){
    const b=await body(req),s=await sessionUser(env,b.token);if(!s)return json({ok:false,error:'invalid_session'},401);return json({ok:true,user:s.user,role:s.role});
  }

  if(p==='/api/client/login'&&m==='POST'){
    const b=await body(req),driver=clean(b.driver),device=String(b.device_id||'');if(!driver)return json({ok:false,error:'driver_required'},400);
    if(b.token&&await clientAuth(env,driver,device,b.token))return json({ok:true,driver,token:b.token});
    const token=randHex(32),h=await sha(token);await env.DB.prepare('INSERT INTO client_tokens(token_hash,driver,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?)').bind(h,driver,device,now(),now()).run();
    return json({ok:true,driver,token});
  }
  if(p==='/api/client/heartbeat'&&m==='POST'){
    const b=await body(req),driver=clean(b.driver);if(!await clientAuth(env,driver,String(b.device_id||''),b.token))return json({ok:false,error:'token_required'},401);return json({ok:true,driver,time:now()});
  }
  if(p==='/api/client/telemetry'&&m==='POST'){
    const b=await body(req),driver=clean(b.driver);if(!await clientAuth(env,driver,String(b.device_id||''),b.token))return json({ok:false,error:'token_required'},401);
    const raw=b.telemetry&&typeof b.telemetry==='object'?b.telemetry:{},account=clean(raw.account_user||b.account_user||driver),t=now();
    await env.DB.prepare('INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json) VALUES(?,?,?,?,?) ON CONFLICT(driver) DO UPDATE SET account_user=excluded.account_user,device_id=excluded.device_id,updated_at=excluded.updated_at,telemetry_json=excluded.telemetry_json').bind(driver,account,String(b.device_id||''),t,JSON.stringify(raw)).run();
    const missionEvent=await processMissionTelemetry(env,account,raw,t);
    return json({ok:true,driver,updated_at:t,mission_event:missionEvent});
  }

  if(p==='/api/public/account-live'&&m==='GET')return json({ok:true,telemetry:await liveData(env),updated_at:now()});
  if(p==='/api/public/ranking'&&m==='GET'){
    const season=(await env.DB.prepare("SELECT value FROM meta WHERE key='season'").first())?.value||now().slice(0,7);
    const mode=(await env.DB.prepare("SELECT value FROM meta WHERE key='operation_mode'").first())?.value||'official';
    return json({ok:true,operation_mode:mode,season,ranking:await ranking(env)});
  }
  if(p==='/api/public/safety-ranking'&&m==='GET'){
    const r=await env.DB.prepare('SELECT p.user,p.safety_score AS score,p.perfect_trips,p.speed_fines,p.penalty_xp FROM profiles p JOIN accounts a ON a.user=p.user WHERE a.disabled=0 ORDER BY p.safety_score DESC,p.perfect_trips DESC,p.speed_fines ASC,p.user ASC').all();return json({ok:true,ranking:r.results||[]});
  }
  if(p==='/api/public/driver'&&m==='GET'){
    const user=clean(u.searchParams.get('user')),prof=await profileData(env,user);if(!prof)return json({ok:false,error:'not_found'},404);return json({ok:true,profile:prof});
  }
  if(p==='/api/site/profile'&&m==='POST'){
    const b=await body(req),s=await sessionUser(env,b.token);if(!s)return json({ok:false,error:'invalid_session'},401);await ensureProfile(env,s.user);return json({ok:true,profile:await profileData(env,s.user)});
  }

  if(p==='/api/public/work/catalog'&&m==='GET'){
    const user=clean(u.searchParams.get('user')),month=now().slice(0,7);const c=await env.DB.prepare('SELECT id,position,title,category,icon,custom FROM work_catalog WHERE active=1 ORDER BY position').all();const done=await env.DB.prepare('SELECT work_id FROM work_completed WHERE user=? AND month_key=?').bind(user,month).all(),set=new Set((done.results||[]).map(x=>x.work_id));return json({ok:true,catalog:(c.results||[]).map(x=>({...x,custom:!!x.custom,completed:set.has(x.id)})),mission_min_km:MISSION_MIN_KM});
  }
  if(p==='/api/site/work/select'&&m==='POST'){
    const b=await body(req),s=await sessionUser(env,b.token);if(!s)return json({ok:false,error:'invalid_session'},401);await ensureProfile(env,s.user);const pr=await env.DB.prepare('SELECT current_mission_json,monthly_completed FROM profiles WHERE user=?').bind(s.user).first();if(pr?.current_mission_json)return json({ok:false,error:'mission_already_active'},409);const item=await env.DB.prepare('SELECT * FROM work_catalog WHERE id=? AND active=1').bind(String(b.work_id||'')).first();if(!item)return json({ok:false,error:'invalid_work'},404);const mission={catalog_id:item.id,sequence:item.position,title:item.title,category:item.category,custom_cargo:String(b.custom_cargo||''),state:'assigned',min_km:MISSION_MIN_KM,created_at:now()};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(mission),now(),s.user).run();return json({ok:true,mission,completed:pr?.monthly_completed||0});
  }

  if(p==='/api/site/admin/session'&&m==='POST'){
    const b=await body(req),s=await sessionUser(env,b.token);if(!s)return json({ok:false,error:'invalid_session'},401);if(!['owner','admin','moderator'].includes(s.role))return json({ok:false,error:'forbidden'},403);return json({ok:true,user:s.user,role:s.role});
  }
  if(p==='/api/site/admin/drivers'&&m==='POST'){
    const b=await body(req),s=await sessionUser(env,b.token);if(!s||!['owner','admin','moderator'].includes(s.role))return json({ok:false,error:'forbidden'},403);const r=await env.DB.prepare("SELECT a.user,a.role,a.disabled,a.created_at,p.monthly_completed,p.monthly_goal,p.xp,p.total_km,p.current_mission_json,t.updated_at AS last_telemetry_at,t.telemetry_json FROM accounts a LEFT JOIN profiles p ON p.user=a.user LEFT JOIN telemetry_live t ON t.account_user=a.user ORDER BY a.user").all();const drivers=(r.results||[]).map(x=>{let mission=null,tel={};try{mission=x.current_mission_json?JSON.parse(x.current_mission_json):null}catch(_){}try{tel=x.telemetry_json?JSON.parse(x.telemetry_json):{}}catch(_){}const f=telemetryFlat(x.user,x.user,x.last_telemetry_at||'',tel);return {user:x.user,role:x.role,disabled:!!x.disabled,created_at:x.created_at,monthly_completed:x.monthly_completed||0,monthly_goal:x.monthly_goal||30,xp:x.xp||0,total_km:x.total_km||0,current_mission:mission,last_telemetry_at:x.last_telemetry_at,online:x.last_telemetry_at?Date.now()-Date.parse(x.last_telemetry_at)<30000:false,truck:[f.truck_make,f.truck_model].filter(Boolean).join(' '),cargo:f.cargo_name}});return json({ok:true,viewer_role:s.role,drivers});
  }

  if(p==='/api/migration/import'&&m==='POST'){
    if(!env.MIGRATION_KEY)return json({ok:false,error:'migration_disabled'},403);const b=await body(req);if(String(b.key||'')!==String(env.MIGRATION_KEY))return json({ok:false,error:'forbidden'},403);const users=Array.isArray(b.users)?b.users:[];for(const x of users){const user=clean(x.user);if(!user)continue;const t=x.created_at||now();await env.DB.prepare("INSERT INTO accounts(user,role,disabled,created_at,updated_at) VALUES(?,?,?,?,?) ON CONFLICT(user) DO UPDATE SET role=excluded.role,disabled=excluded.disabled,updated_at=excluded.updated_at").bind(user,x.role||'driver',x.disabled?1:0,t,now()).run();await ensureProfile(env,user);await env.DB.prepare('UPDATE profiles SET monthly_completed=?,monthly_goal=?,total_deliveries=?,total_km=?,xp=?,points=?,perfect_trips=?,penalty_xp=?,speed_fines=?,safety_score=?,current_mission_json=?,updated_at=? WHERE user=?').bind(Number(x.monthly_completed)||0,Number(x.monthly_goal)||30,Number(x.total_deliveries)||0,Number(x.total_km)||0,Number(x.xp)||0,Number(x.points)||0,Number(x.perfect_trips)||0,Number(x.penalty_xp)||0,Number(x.speed_fines)||0,Number(x.safety_score)||100,x.current_mission?JSON.stringify(x.current_mission):null,now(),user).run()}
    return json({ok:true,imported:users.length});
  }
  return json({ok:false,error:'not_found',path:p},404);
}

export default {async fetch(req,env){try{return await handle(req,env)}catch(e){return json({ok:false,error:'internal_error',message:String(e?.message||e)},500)}}};