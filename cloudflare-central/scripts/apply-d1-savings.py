from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

def replace(old,new):
    global s
    if s.count(old)!=1: raise SystemExit('Expected one savings anchor: '+old[:100])
    s=s.replace(old,new,1)

s="import {cachedRead} from './read-cache.js';\nimport {budgetState} from './budget-guard.js';\n"+s
# No full sessions scan for every telemetry packet, OPTIONS or health request.
replace("ctx.waitUntil(env.DB.prepare('DELETE FROM sessions WHERE expires_at<?').bind(now()).run());", '')
replace('export default{async fetch', """export default{async scheduled(event,env,ctx){
 if(budgetState(env).paused)return;
 ctx.waitUntil(env.DB.batch([
   env.DB.prepare('DELETE FROM sessions WHERE token_hash IN (SELECT token_hash FROM sessions WHERE expires_at<? ORDER BY expires_at LIMIT 1000)').bind(now()),
   env.DB.prepare('DELETE FROM auth_attempts WHERE id IN (SELECT id FROM auth_attempts WHERE at<? ORDER BY at LIMIT 1000)').bind(new Date(Date.now()-86400000).toISOString())
 ]));
},async fetch""")
# Refresh token activity once a minute, while checking revocation on EVERY request.
replace('ct.device_id,ct.revoked_at,a.disabled', 'ct.device_id,ct.revoked_at,ct.last_seen_at,a.disabled')
replace("await env.DB.prepare('UPDATE client_tokens SET last_seen_at=? WHERE token_hash=?').bind(now(),hash).run();return{row:r}", "if(!r.last_seen_at||Date.now()-Date.parse(r.last_seen_at)>=60000)await env.DB.prepare('UPDATE client_tokens SET last_seen_at=? WHERE token_hash=?').bind(now(),hash).run();return{row:r}")
replace('async function live(env){', "async function live(env){return cachedRead('live',5,()=>readLive(env))}\nasync function readLive(env){")
replace('async function profile(env,user){', "async function profile(env,user){return cachedRead('profile:'+user,15,()=>readProfile(env,user))}\nasync function readProfile(env,user){")
# Authenticated profile polling need not perform INSERT OR IGNORE every time.
replace("const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);return json(req,{ok:true,profile:await profile(env,s.user)})", "const b=await body(req),s=await requireSession(req,env,b);let pr=await profile(env,s.user);if(!pr){await ensureProfile(env,s.user);pr=await readProfile(env,s.user)}return json(req,{ok:true,profile:pr})")
# Cache only public GET responses. Recreate headers for each request's origin.
replace('return await route(req,env)', 'return await economicalRoute(req,env)')
replace('try{return await economicalRoute(req,env)}', """try{
 const p=new URL(req.url).pathname,budget=budgetState(env);
 if(req.method==='OPTIONS')return new Response(null,{status:204,headers:headers(req)});
 if(p==='/api/public/service-status')return json(req,{ok:true,...budget});
 if(budget.paused&&p!=='/health'&&p!=='/api/public/version'){
   if(p==='/api/public/notice')return json(req,{ok:true,enabled:true,title:'PAUSA DO PLANO GRATUITO',message:budget.message+(budget.resumes_at?' Renovação: '+new Date(budget.resumes_at).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo'})+' (Brasília).':''),...budget});
   if(p==='/api/public/account-live')return json(req,{ok:true,telemetry:[],service_paused:true,...budget});
   const response=json(req,{ok:false,error:'free_tier_protection',...budget},503);response.headers.set('Retry-After','300');return response;
 }
 return await economicalRoute(req,env)}""")
s+="""
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
"""
# Persist unchanged telemetry at most every 15 seconds, but process damage changes,
# job changes and delivery/cancellation events immediately. Read the previous row
# by primary key; never trust an in-memory cache for rank continuity.
# Official clients may resend packets collected during a Central maintenance window.
# In that case mission/rank continuity uses gat_collected_at while online freshness
# continues to use the real server receive time.
start=s.index(" if(p==='/api/client/telemetry'&&m==='POST'){")
end=s.index("\n if(p==='/api/public/account-live'",start)
s=s[:start]+""" if(p==='/api/client/telemetry'&&m==='POST'){
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
   const delivered=event==='delivered'||(!loaded&&bool(raw,'gameplay.jobDelivered','jobDelivered'));
   const cancelled=event==='cancelled'||(!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled'));
   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);
   const readiness=rankingReadiness(raw);
   const signature=x=>{const q=flat(driver,account,t,x);return JSON.stringify([rankingReadiness(x),q.job_latched,q.on_job,q.job_latch_key,q.cargo_id,q.cargo_name,q.source_city,q.destination_city,q.mass_kg,q.gat_map,num(x,'job.plannedDistanceKm','planned_distance_km'),x.cargo_damage_pct,x.truck_engine_damage_pct,x.truck_transmission_damage_pct,x.truck_cabin_damage_pct,x.truck_chassis_damage_pct,x.truck_wheels_damage_pct,x.trailer_damage_pct,deep(x,'game.paused')])};
   if(!queuedTimeOK&&!delivered&&!cancelled&&prevRaw&&Date.parse(receivedAt)-Date.parse(previous.updated_at)<15000&&signature(raw)===signature(prevRaw))
     return json(req,{ok:true,driver,account_user:account,updated_at:previous.updated_at,rank_status:readiness,telemetry_deferred:true,next_upload_ms:15000});
   const missionEvent=await processMission(env,account,raw,t,previousSampleAt);
   await env.DB.prepare('INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json) VALUES(?,?,?,?,?) ON CONFLICT(driver) DO UPDATE SET account_user=excluded.account_user,device_id=excluded.device_id,updated_at=excluded.updated_at,telemetry_json=excluded.telemetry_json').bind(driver,account,device,receivedAt,JSON.stringify(raw)).run();
   return json(req,{ok:true,driver,account_user:account,updated_at:receivedAt,collected_at:t,replayed:queuedTimeOK,rank_status:readiness,mission_event:missionEvent,next_upload_ms:15000});
 }
"""+s[end:]
p.write_text(s,encoding='utf-8')
print('D1 savings: scheduled cleanup, indexed reads, cached read models, queue replay timestamps and fewer redundant writes.')
