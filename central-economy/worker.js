import {telemetryCanonical} from './journal-canonical.js';
import {economyTrack,economyFinish,economyAction,ensureEconomy} from './economy-official.js';
import {createHash,createHmac,pbkdf2Sync} from 'node:crypto';
import {rankingPeriod} from './ranking-period.js';
const sha256=x=>createHash('sha256').update(x).digest();
const bytesToHex=x=>Buffer.from(x).toString('hex');
const pbkdf2=(_,password,salt,options)=>pbkdf2Sync(password,salt,options.c,options.dkLen,'sha256');
import {cachedRead,invalidateRead} from './read-cache.js';
import {budgetState} from './budget-guard.js';
import {rankingReadiness, rankingMessage, advanceRankGuard, restoreDeliveredTrailer} from './ranking-telemetry.js';
import {calculateGatScore, updateMissionFineTracker, GAT_SCORING_V2_START_LOCAL, GAT_TIMEZONE} from './scoring-rules.js';

export function recognizedTripDistance(plannedKm, measuredKm, fallbackKm) {
 const positive = value => Number.isFinite(Number(value)) && Number(value) > 0 ? Number(value) : 0;
 const planned = positive(plannedKm), measured = positive(measuredKm), fallback = positive(fallbackKm);
 const actual = measured || fallback;
 const cap = planned > 0 ? planned * 1.25 : 0;
 return {planned_distance_km:planned,actual_distance_km:actual,distance_cap_km:cap,
   distance_km:cap > 0 ? Math.min(actual,cap) : actual,
   distance_capped:cap > 0 && actual > cap,
   distance_source:measured > 0 ? 'telemetry_odometer' : 'delivery_details_or_planned',
   distance_rule:'actual_distance_capped_at_125_percent'};
}

export function resolveJobOutcome({event, cancelFlag, cancelWasActive=false, freshReceipt, legacyFallback, eventMatchesTrip=true}) {
 const newCancellation=eventMatchesTrip&&(event==='cancelled'||(cancelFlag&&!cancelWasActive));
 if(newCancellation&&freshReceipt)return {delivered:false,cancelled:false,conflict:true};
 if(newCancellation)return {delivered:false,cancelled:true};
 return {delivered:Boolean(freshReceipt||(!cancelFlag&&legacyFallback)),cancelled:false};
}

const VERSION='1.0.63.1-local';
const MIN_KM=0;
const SPEED_LIMIT_TOLERANCE_KMH=1;
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
const GAT_TZ_OFFSET_MS=3*60*60*1000;
const month=v=>{const ms=Date.parse(String(v||now()));return Number.isFinite(ms)?new Date(ms-GAT_TZ_OFFSET_MS).toISOString().slice(0,7):String(v||now()).slice(0,7)};
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const compact=v=>norm(v).replace(/[^a-z0-9]/g,'');
const deep=(o,p)=>String(p).split('.').reduce((v,k)=>v&&typeof v==='object'?v[k]:undefined,o);
const pick=(o,...ps)=>{for(const p of ps){const v=deep(o,p);if(v!==undefined&&v!==null)return v}};
const num=(o,...ps)=>{const v=Number(pick(o,...ps));return Number.isFinite(v)?v:0};
const str=(o,...ps)=>String(pick(o,...ps)||'').trim();
const bool=(o,...ps)=>{const v=pick(o,...ps);return v===true||v===1||String(v).toLowerCase()==='true'};
const truckIdentity=raw=>clean(str(raw,'truck_id','truckId','truck.id','truck.uid','vehicle_id','vehicleId','vehicle.id','profile.truckId','profile.truck.id'));
const level=x=>Math.max(1,Math.floor((Number(x)||0)/2000)+1);
const scoreTier=(v,a,b,c)=>{v=Math.max(0,Number(v)||0);return v<=0?0:v<=a?3:v<=b?5:v<=c?10:15};
const damageAliasPct=(raw,key)=>{const v=deep(raw,key),n=Number(v);return v!==undefined&&v!==null&&Number.isFinite(n)?Math.max(0,n):0};
const damageRawPct=(raw,...keys)=>{for(const key of keys){const v=deep(raw,key),n=Number(v);if(v!==undefined&&v!==null&&Number.isFinite(n))return Math.max(0,n<=1.0001?n*100:n)}return 0};
const truckDamageParts=raw=>({engine:Math.max(damageAliasPct(raw,'truck_engine_damage_pct'),damageRawPct(raw,'truck.wearEngine','truck.engineWear','truck.engineDamage')),transmission:Math.max(damageAliasPct(raw,'truck_transmission_damage_pct'),damageRawPct(raw,'truck.wearTransmission','truck.transmissionWear','truck.transmissionDamage')),cabin:Math.max(damageAliasPct(raw,'truck_cabin_damage_pct'),damageRawPct(raw,'truck.wearCabin','truck.cabinWear','truck.cabinDamage')),chassis:Math.max(damageAliasPct(raw,'truck_chassis_damage_pct'),damageRawPct(raw,'truck.wearChassis','truck.chassisWear','truck.chassisDamage')),wheels:Math.max(damageAliasPct(raw,'truck_wheels_damage_pct'),damageRawPct(raw,'truck.wearWheels','truck.wheelsWear','truck.wheelsDamage'))});
const truckDamageOf=raw=>{const p=truckDamageParts(raw);return Math.max(damageAliasPct(raw,'truck_damage_pct'),p.engine,p.transmission,p.cabin,p.chassis,p.wheels)};
const trailerDamageOf=raw=>Math.max(damageAliasPct(raw,'trailer_damage_pct'),damageRawPct(raw,'trailers.0.wearChassis'),damageRawPct(raw,'trailers.0.wearWheels'),damageRawPct(raw,'trailers.0.wearBody'));
const damageDelta=(max,start)=>Math.max(0,(Number(max)||0)-(Number(start)||0));
const monthPoints=list=>(list||[]).filter(x=>month(x?.delivered_at)===month()).reduce((sum,x)=>{const gp=Number(x?.gat_points);if(Number.isFinite(gp))return sum+Math.max(0,gp);return sum+Math.max(0,100-(Number(x?.penalty_xp)||0))},0);

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
async function farmEventHistory(env,user){const rows=await env.DB.prepare("WITH identified AS (SELECT id,cargo,distance_km,delivered_at,COALESCE(NULLIF(json_extract(CASE WHEN json_valid(raw_json) THEN raw_json ELSE '{}' END,'$.cargo_identity.cargo_id_raw'),''),NULLIF(json_extract(CASE WHEN json_valid(raw_json) THEN raw_json ELSE '{}' END,'$.cargo_identity.cargo_id'),''),NULLIF(json_extract(CASE WHEN json_valid(raw_json) THEN raw_json ELSE '{}' END,'$.mission.cargo_id'),''),NULLIF(json_extract(CASE WHEN json_valid(raw_json) THEN raw_json ELSE '{}' END,'$.cargo_id'),'')) AS cargo_id FROM deliveries WHERE user=? AND distance_km>=1000 AND julianday(delivered_at) IS NOT NULL), grouped AS (SELECT *,CASE WHEN cargo_id IN ('live_cattle') THEN 'Live Cattle' WHEN cargo_id IN ('live_pigs') THEN 'Live Pigs' WHEN cargo_id IN ('sheep_wool') THEN 'Sheep Wool' WHEN cargo_id IN ('cheese') THEN 'Cheese' WHEN cargo_id IN ('milk','milk_t') THEN 'Milk' WHEN cargo_id IN ('nonalco_beer') THEN 'Non-alcoholic Beer' WHEN cargo_id IN ('beef_meat') THEN 'Beef' WHEN cargo_id IN ('pork_meat') THEN 'Pork' WHEN cargo_id IN ('chicken_meat') THEN 'Chicken Meat' WHEN cargo_id IN ('pet_food','pet_food_c') THEN 'Pet Food' WHEN cargo_id IN ('straw_bales') THEN 'Straw Bales' WHEN cargo_id IN ('fertilizer') THEN 'Fertilizer' WHEN cargo_id IN ('big_bag_seed') THEN 'Big-Bags of Seeds' WHEN cargo_id IN ('drymilk') THEN 'Dry Milk' WHEN cargo_id IN ('young_seed') THEN 'Young Seedlings' ELSE NULL END AS category FROM identified), numbered AS (SELECT *,ROW_NUMBER() OVER(PARTITION BY category ORDER BY delivered_at,id) AS event_number FROM grouped WHERE category IS NOT NULL) SELECT id,cargo,cargo_id,distance_km,delivered_at FROM numbered WHERE event_number<=10 ORDER BY delivered_at,id").bind(user).all();return rows.results||[];}
async function readProfile(env,user){
 const p=await env.DB.prepare('SELECT * FROM profiles WHERE user=?').bind(user).first();if(!p)return null;
 const d=await env.DB.prepare('SELECT id,sequence_no AS sequence,source,destination,cargo,weight_kg,distance_km,xp,xp AS xp_awarded,perfect,perfect AS perfect_trip,penalty_xp,speed_fines,delivered_at,delivered_at AS completed_at,raw_json FROM deliveries WHERE user=? ORDER BY id DESC LIMIT 100').bind(user).all();
 const c=await env.DB.prepare('SELECT cargo,weight_kg FROM deliveries WHERE user=? GROUP BY cargo,weight_kg').bind(user).all();
 const currentMonth=month(),ma=await env.DB.prepare("SELECT COUNT(*) total,COALESCE(SUM(COALESCE(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER),MAX(0,100-penalty_xp))),0) points FROM deliveries WHERE user=? AND strftime('%Y-%m',delivered_at,'-3 hours')=?").bind(user,currentMonth).first();
 let mission=null;try{mission=p.current_mission_json?JSON.parse(p.current_mission_json):null}catch{}
 const deliveries=(d.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.raw_json||'{}')}catch{}return{...x,...raw.audit}}).reverse(),points=Number(ma?.points||0),monthlyCompleted=Number(ma?.total||0);
 const goldWallet=await env.DB.prepare('SELECT gold_units FROM eco_wallets WHERE user=?').bind(user).first();
 return{user,gold_balance:Number(goldWallet?.gold_units||0)/100,event_deliveries:await farmEventHistory(env,user),monthly_completed:monthlyCompleted,monthly_deliveries:monthlyCompleted,total_deliveries:p.total_deliveries,total_km:p.total_km,xp:p.xp,level:level(p.xp),points,perfect_trips:p.perfect_trips,penalty_xp:p.penalty_xp,speed_fines:p.speed_fines,safety_score:p.safety_score,avatar_url:p.avatar_url||'',current_mission:mission,cargo_history:c.results||[],deliveries}
}

function updateMissionSpeedingTracker(m,raw,t){
  const speed=Math.abs(num(
    raw,
    'speed_kmh',
    'truck.speedKmh',
    'truck.speed_kmh',
    'truck.speed'
  ));

  const limit=Math.abs(num(
    raw,
    'navigation.speedLimit',
    'speed_limit_kmh',
    'speedLimit',
    'navigation.speed_limit'
  ));

  const at=Date.parse(t);

  if(!Number.isFinite(at)){
    return false;
  }

  const valid=
    Number.isFinite(speed)&&
    Number.isFinite(limit)&&
    limit>0;

  const moving=valid&&speed>=5;

  const state=
    !valid
      ?'invalid'
      :(!moving
        ?'stopped'
        :(speed>limit+SPEED_LIMIT_TOLERANCE_KMH?'over':'normal'));

  const prevAt=Date.parse(String(m.gat_speed_last_at||''));
  let prevState=String(m.gat_speed_last_state||'invalid');
  const prevSpeed=Number(m.gat_speed_last_speed_kmh);
  const prevLimit=Number(m.gat_speed_last_limit_kmh);
  if(prevState==='over'&&Number.isFinite(prevSpeed)&&Number.isFinite(prevLimit)&&prevSpeed<=prevLimit+SPEED_LIMIT_TOLERANCE_KMH){
    prevState='normal';
  }

  let changed=false;


  // ========================================================
  // CALCULA TEMPO ENTRE UM PACOTE E OUTRO
  // ========================================================

  if(Number.isFinite(prevAt)&&at>prevAt){

    // Protecao para nao contar buracos muito grandes
    // caso a telemetria pare temporariamente.
    const dt=Math.min(
      15,
      Math.max(0,(at-prevAt)/1000)
    );


    // ======================================================
    // ESTAVA ACIMA DO LIMITE
    // ======================================================

    if(dt>0&&prevState==='over'){

      m.gat_speed_observed_seconds=
        Math.max(
          0,
          Number(m.gat_speed_observed_seconds)||0
        )+dt;

      m.gat_speeding_event_seconds=
        Math.max(
          0,
          Number(m.gat_speeding_event_seconds)||0
        )+dt;


      const eventSeconds=
        Math.max(
          0,
          Number(m.gat_speeding_event_seconds)||0
        );

      const hasGrace=
        m.gat_speed_event_has_grace===true;

      const committed=
        m.gat_speed_event_committed===true;


      // ----------------------------------------------------
      // EVENTO COM TOLERANCIA
      //
      // Ate 8 segundos:
      // nao conta nada.
      //
      // Passou de 8 segundos:
      // conta TODO O PERIODO desde o primeiro segundo.
      // ----------------------------------------------------

      if(hasGrace){

        if(!committed&&eventSeconds>8){

          m.gat_speeding_seconds=
            Math.max(
              0,
              Number(m.gat_speeding_seconds)||0
            )+eventSeconds;

          m.gat_speed_event_committed=true;

        }else if(committed){

          m.gat_speeding_seconds=
            Math.max(
              0,
              Number(m.gat_speeding_seconds)||0
            )+dt;
        }

      }else{

        // Nao recuperou ainda os 60 segundos.
        // Portanto nao existe nova tolerancia.
        // Conta desde o inicio.

        m.gat_speeding_seconds=
          Math.max(
            0,
            Number(m.gat_speeding_seconds)||0
          )+dt;

        m.gat_speed_event_committed=true;
      }


      m.gat_speeding_longest_seconds=
        Math.max(
          Math.max(
            0,
            Number(m.gat_speeding_longest_seconds)||0
          ),
          eventSeconds
        );

      changed=true;
    }


    // ======================================================
    // ESTAVA RODANDO DENTRO DO LIMITE
    // ======================================================

    if(dt>0&&prevState==='normal'){

      m.gat_speed_observed_seconds=
        Math.max(
          0,
          Number(m.gat_speed_observed_seconds)||0
        )+dt;

      m.gat_speed_compliant_seconds=
        Math.max(
          0,
          Number(m.gat_speed_compliant_seconds)||0
        )+dt;

      m.gat_speed_normal_streak_s=
        Math.max(
          0,
          Number(m.gat_speed_normal_streak_s)||0
        )+dt;


      // Depois de 60 segundos continuos
      // dentro do limite, recupera os 8 segundos.
      if(Number(m.gat_speed_normal_streak_s)>=60){
        m.gat_speed_grace_available=true;
      }

      changed=true;
    }
  }


  // ========================================================
  // ACABOU DE ENTRAR ACIMA DO LIMITE
  // ========================================================

  if(state==='over'&&prevState!=='over'){

    m.gat_speed_events=
      Math.max(
        0,
        Number(m.gat_speed_events)||0
      )+1;

    m.gat_speeding_event_seconds=0;
    m.gat_speed_normal_streak_s=0;

    const canUseGrace=
      m.gat_speed_grace_available!==false;

    m.gat_speed_event_has_grace=canUseGrace;
    m.gat_speed_event_committed=!canUseGrace;

    if(canUseGrace){
      // Consumiu a tolerancia.
      // Outra somente depois de 60 segundos normais.
      m.gat_speed_grace_available=false;
    }

    changed=true;
  }


  // ========================================================
  // VOLTOU PARA DENTRO DO LIMITE
  // ========================================================

  if(state==='normal'&&prevState!=='normal'){

    m.gat_speed_normal_streak_s=0;
    m.gat_speeding_event_seconds=0;
    m.gat_speed_event_committed=false;
    m.gat_speed_event_has_grace=false;

    changed=true;
  }


  // ========================================================
  // PARADO OU SEM LIMITE VALIDO
  //
  // Parado nao recupera os 60 segundos.
  // ========================================================

  if(state==='stopped'||state==='invalid'){
    m.gat_speed_normal_streak_s=0;
  }


  // ========================================================
  // GUARDA MAIOR EXCESSO DE VELOCIDADE
  // ========================================================

  if(valid){

    const over=Math.max(0,speed-limit-SPEED_LIMIT_TOLERANCE_KMH);

    m.gat_speed_max_over_kmh=
      Math.max(
        Math.max(
          0,
          Number(m.gat_speed_max_over_kmh)||0
        ),
        over
      );

    m.gat_speed_last_speed_kmh=speed;
    m.gat_speed_last_limit_kmh=limit;
  }


  m.gat_speed_last_at=t;
  m.gat_speed_last_state=state;

  return changed;
}
async function resetAssigned(env,user,mission,reason,extra={}){const m={...mission,state:'assigned',min_km:MIN_KM,last_rejected_at:now(),last_rejected_reason:reason,...extra};for(const k of['cargo','cargo_id','source','source_city_id','destination','destination_city_id','weight_kg','planned_distance_km','rbr_start_remaining_km','map_mode','distance_source','started_at','suspended_at','resumed_at','job_latch_key','start_remaining_km','start_odometer_km','start_truck_identity','trip_progress_confirmed','delivery_progress_confirmed','rank_guard','delivery_details_start','cancel_flag_start','cancel_flag_last','pending_review','pending_delivery_details','pending_delivery_at'])delete m[k];for(const k of Object.keys(m))if(/^(truck_|trailer_).*damage_(start|max)_pct$/.test(k))delete m[k];await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),now(),user).run();return m}
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

function journalPacketId(raw){return String(raw?.gat_packet_id||'').trim()}
function journalTripId(raw){return String(raw?.gat_trip_id||raw?.job_latch_key||'').trim()}
function journalPayloadHash(raw){
 const copy={...raw};for(const key of ['gat_journal_seq','gat_journal_prev','gat_journal_chain','gat_journal_payload_sha256','gat_journal_version','gat_journal_verified','gat_journal_invalid'])delete copy[key];
 if(String(raw?.gat_journal_version)==='3'){
   delete copy.gat_journal_payload;
   if(typeof raw.gat_journal_payload!=='string'||raw.gat_journal_payload.length>MAX_BODY)return '';
   let original;try{original=JSON.parse(raw.gat_journal_payload)}catch{return ''}
   if(!original||Array.isArray(original)||typeof original!=='object')return '';
   // The signed original MUST be semantically identical to every field in use.
   if(telemetryCanonical(original)!==telemetryCanonical(copy))return '';
   return createHash('sha256').update(raw.gat_journal_payload).digest('hex');
 }
 return createHash('sha256').update(String(raw?.gat_journal_version)==='2'?telemetryCanonical(copy):JSON.stringify(copy)).digest('hex');
}
function fixedHexEqual(a,b){
 a=String(a||'').toLowerCase();b=String(b||'').toLowerCase();if(a.length!==b.length||!a.length)return false;let diff=0;for(let i=0;i<a.length;i++)diff|=a.charCodeAt(i)^b.charCodeAt(i);return diff===0;
}
function journalMacKey(token,device){return createHash('sha256').update(`GAT-JOURNAL-V1|${String(token||'')}|${String(device||'')}`).digest()}
async function inspectClientPacket(env,user,device,token,raw,t){
 const packetId=journalPacketId(raw);if(!packetId)return{packet_id:'',duplicate:false,journal_present:false,journal_verified:false,journal_reason:'legacy_no_packet_id'};
 const prior=await env.DB.prepare('SELECT mission_event_json,journal_verified,journal_reason FROM telemetry_packet_receipts WHERE user=? AND packet_id=?').bind(user,packetId).first();
 if(prior)return{packet_id:packetId,duplicate:true,journal_present:true,journal_verified:!!prior.journal_verified,journal_reason:prior.journal_reason||null,mission_event_json:prior.mission_event_json||null};
 const seq=Math.trunc(Number(raw?.gat_journal_seq)||0),prev=String(raw?.gat_journal_prev||''),chain=String(raw?.gat_journal_chain||'').toLowerCase(),claimedPayload=String(raw?.gat_journal_payload_sha256||'').toLowerCase(),collected=String(raw?.gat_collected_at||''),tripId=journalTripId(raw);
 const present=seq>0&&chain&&claimedPayload&&['1','2','3'].includes(String(raw?.gat_journal_version||''));
 if(!present)return{packet_id:packetId,duplicate:false,journal_present:false,journal_verified:false,journal_reason:'legacy_unsigned_packet',seq:null,chain:null,trip_id:tripId,collected_at:collected};
 const actualPayload=journalPayloadHash(raw);
 if(!fixedHexEqual(actualPayload,claimedPayload))return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:false,journal_reason:'journal_payload_changed',seq,chain,trip_id:tripId,collected_at:collected};
 const canonical=`${packetId}|${collected}|${tripId}|${seq}|${prev}|${claimedPayload}`;
 const expected=createHmac('sha256',journalMacKey(token,device)).update(canonical).digest('hex');
 if(!fixedHexEqual(expected,chain))return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:false,journal_reason:'journal_signature_invalid',seq,chain,trip_id:tripId,collected_at:collected};
 const state=await env.DB.prepare('SELECT last_seq,last_chain,last_packet_id FROM telemetry_journal_state WHERE user=? AND device_id=?').bind(user,device).first();
 const continuous=!state||(seq===Number(state.last_seq)+1&&prev===String(state.last_chain||''));
 return{packet_id:packetId,duplicate:false,journal_present:true,journal_verified:continuous,journal_reason:continuous?null:'journal_chain_gap',seq,chain,prev,trip_id:tripId,collected_at:collected};
}
async function persistClientPacket(env,user,device,raw,t,packetState,missionEvent,openJourneyState,phase){
 const packetId=String(packetState?.packet_id||journalPacketId(raw));if(!packetId)return;
 const tripId=String(openJourneyState?.canonical||packetState?.trip_id||journalTripId(raw)||'').trim();
 if(packetState?.journal_verified){
   await env.DB.prepare("INSERT INTO telemetry_journal_state(user,device_id,last_seq,last_chain,last_packet_id,updated_at) VALUES(?,?,?,?,?,?) ON CONFLICT(user,device_id) DO UPDATE SET last_seq=excluded.last_seq,last_chain=excluded.last_chain,last_packet_id=excluded.last_packet_id,updated_at=excluded.updated_at").bind(user,device,Number(packetState.seq)||0,String(packetState.chain||''),packetId,t).run();
 }
 await env.DB.prepare('INSERT INTO telemetry_packet_receipts(user,packet_id,device_id,trip_id,collected_at,received_at,journal_seq,journal_chain,journal_verified,journal_reason,mission_event_json) VALUES(?,?,?,?,?,?,?,?,?,?,?)').bind(user,packetId,device,tripId,String(packetState?.collected_at||raw?.gat_collected_at||''),t,packetState?.seq??null,packetState?.chain||null,packetState?.journal_verified?1:0,packetState?.journal_reason||null,missionEvent?JSON.stringify(missionEvent):null).run();
 if(!tripId)return;
 const terminal=phase==='delivered'||phase==='cancelled',last=await env.DB.prepare('SELECT received_at FROM trip_checkpoints WHERE user=? AND trip_id=? ORDER BY id DESC LIMIT 1').bind(user,tripId).first(),age=last?Date.parse(t)-Date.parse(last.received_at):Infinity;
 if(!terminal&&Number.isFinite(age)&&age<120000)return;
 const f=flat(user,user,t,raw),summary={phase,cargo:f.cargo_name,source:f.source_city,destination:f.destination_city,weight_kg:f.mass_kg,remaining_km:f.remaining_km,speed_kmh:f.speed_kmh,game_name:str(raw,'gat_game','game.gameName','gameName'),map_mode:f.gat_map,cargo_damage_pct:num(raw,'cargo_damage_pct'),truck_engine_damage_pct:num(raw,'truck_engine_damage_pct'),truck_transmission_damage_pct:num(raw,'truck_transmission_damage_pct'),truck_cabin_damage_pct:num(raw,'truck_cabin_damage_pct'),truck_chassis_damage_pct:num(raw,'truck_chassis_damage_pct'),truck_wheels_damage_pct:num(raw,'truck_wheels_damage_pct'),trailer_damage_pct:num(raw,'trailer_damage_pct'),journal_verified:!!packetState?.journal_verified,journal_reason:packetState?.journal_reason||null};
 await env.DB.prepare('INSERT OR IGNORE INTO trip_checkpoints(user,trip_id,packet_id,phase,collected_at,received_at,journal_verified,summary_json,raw_json) VALUES(?,?,?,?,?,?,?,?,?)').bind(user,tripId,packetId,phase,String(packetState?.collected_at||raw?.gat_collected_at||''),t,packetState?.journal_verified?1:0,JSON.stringify(summary),JSON.stringify(raw)).run();
}

function journeyGame(raw){return clean(str(raw,'gat_game','game.gameName','gameName'))||'unknown'}
function journeyMap(raw){return clean(str(raw,'gat_map','map_mode','gatMap'))||'base'}
function journeyContext(raw){const explicit=clean(str(raw,'gat_context_id','gatContextId','profile_context_id','profileContextId'));return explicit||`${journeyGame(raw)}|${journeyMap(raw)}`}
function journeyFingerprintRaw(raw){
 const cargo=clean(str(raw,'cargo_id','job.cargoId','Job.CargoId'))||norm(str(raw,'cargo_name','job.cargoName','job.cargo'));
 const source=clean(str(raw,'source_city_id','job.sourceCityId','Job.SourceCityId'))||norm(str(raw,'source_city','job.sourceCity','Job.SourceCity'));
 const destination=clean(str(raw,'destination_city_id','job.destinationCityId','Job.DestinationCityId'))||norm(str(raw,'destination_city','job.destinationCity','Job.DestinationCity'));
 const planned=Math.max(0,Math.round(num(raw,'planned_distance_km','job.plannedDistanceKm','Job.PlannedDistanceKm'))),mass=Math.max(0,Math.round(num(raw,'mass_kg','cargoMass','cargo_mass','job.cargoMass','Job.CargoMass')));
 return `${journeyContext(raw)}|${cargo}|${source}|${destination}|${planned}|${mass}`;
}
function journeyFingerprintMission(m,raw={}){
 const context=String(m?.context_key||'').trim()||`${clean(m?.game_name)||journeyGame(raw)}|${clean(m?.map_mode)||journeyMap(raw)}`;
 const cargo=clean(m?.cargo_id)||norm(m?.cargo||'');
 const source=clean(m?.source_city_id)||norm(m?.source||'');
 const destination=clean(m?.destination_city_id)||norm(m?.destination||'');
 const planned=Math.max(0,Math.round(Number(m?.planned_distance_km)||0)),mass=Math.max(0,Math.round(Number(m?.weight_kg)||0));
 return `${context}|${cargo}|${source}|${destination}|${planned}|${mass}`;
}
async function openJourneyAlias(env,user,observed,canonical,t){
 observed=String(observed||'').trim();canonical=String(canonical||'').trim();if(!observed||!canonical)return;
 await env.DB.prepare("INSERT INTO open_trip_aliases(observed_trip_id,trip_id,user,first_seen_at,last_seen_at) VALUES(?,?,?,?,?) ON CONFLICT(observed_trip_id) DO UPDATE SET trip_id=excluded.trip_id,user=excluded.user,last_seen_at=excluded.last_seen_at").bind(observed,canonical,user,t,t).run();
}
async function saveOpenJourney(env,user,m,raw,t,state='suspended'){
 if(!m)return null;const canonical=String(m.canonical_trip_id||m.trip_id||m.job_latch_key||m.id||'').trim();if(!canonical)return null;
 const game=clean(m.game_name)||journeyGame(raw),context=String(m.context_key||'').trim()||journeyContext(raw),fingerprint=String(m.journey_fingerprint||'').trim()||journeyFingerprintMission({...m,game_name:game,context_key:context},raw);
 m.canonical_trip_id=canonical;m.game_name=game;m.context_key=context;m.journey_fingerprint=fingerprint;
 await env.DB.prepare("INSERT INTO open_trips(trip_id,user,game_name,context_key,fingerprint,state,mission_json,created_at,updated_at,last_seen_at) VALUES(?,?,?,?,?,?,?,?,?,?) ON CONFLICT(trip_id) DO UPDATE SET game_name=excluded.game_name,context_key=excluded.context_key,fingerprint=excluded.fingerprint,state=excluded.state,mission_json=excluded.mission_json,updated_at=excluded.updated_at,last_seen_at=excluded.last_seen_at").bind(canonical,user,game,context,fingerprint,state,JSON.stringify(m),String(m.started_at||m.created_at||t),t,t).run();
 return canonical;
}
async function prepareOpenJourney(env,user,raw,t,loaded,terminal){
 const observed=String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||'').trim(),rawFp=loaded?journeyFingerprintRaw(raw):'',rawContext=loaded?journeyContext(raw):'';
 const pr=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let current=null;try{current=pr?.current_mission_json?JSON.parse(pr.current_mission_json):null}catch{}
 let canonical=current?String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim():'';
 if(current){
   if(!current.game_name)current.game_name=journeyGame(raw);if(!current.context_key)current.context_key=journeyContext(raw);if(!current.journey_fingerprint)current.journey_fingerprint=journeyFingerprintMission(current,raw);
   const same=loaded&&current.journey_fingerprint===rawFp;
   if(!terminal&&(!loaded||!same)){
     canonical=await saveOpenJourney(env,user,current,raw,t,'suspended')||canonical;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(t,user).run();current=null;canonical='';
   }else if(same){
     canonical=String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim();current.canonical_trip_id=canonical;
     if(observed){current.trip_id=observed;current.job_latch_key=observed}
     current.game_name=journeyGame(raw);current.context_key=rawContext;current.journey_fingerprint=rawFp;current.state='active';current.resumed_at=current.resumed_at||t;
     // FK-safe: primeiro persiste a viagem canonica; somente depois cria/atualiza o alias observado.
     canonical=await saveOpenJourney(env,user,current,raw,t,'active')||canonical;
     if(observed&&canonical)await openJourneyAlias(env,user,observed,canonical,t);
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(current),t,user).run();
     return{canonical,observed,fingerprint:rawFp,resumed:true};
   }
 }
 if(loaded&&!current){
   let saved=null;
   if(observed){const a=await env.DB.prepare('SELECT trip_id FROM open_trip_aliases WHERE observed_trip_id=? AND user=?').bind(observed,user).first();if(a?.trip_id)saved=await env.DB.prepare("SELECT * FROM open_trips WHERE trip_id=? AND user=? AND state IN ('active','suspended')").bind(a.trip_id,user).first()}
   if(!saved)saved=await env.DB.prepare("SELECT * FROM open_trips WHERE user=? AND context_key=? AND fingerprint=? AND state IN ('active','suspended') ORDER BY updated_at DESC LIMIT 1").bind(user,rawContext,rawFp).first();
   if(saved){let m=null;try{m=JSON.parse(saved.mission_json||'{}')}catch{}if(m){canonical=String(saved.trip_id);m.canonical_trip_id=canonical;m.game_name=journeyGame(raw);m.context_key=rawContext;m.journey_fingerprint=rawFp;m.state='active';m.resumed_at=t;if(observed){m.trip_id=observed;m.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();await saveOpenJourney(env,user,m,raw,t,'active');return{canonical,observed,fingerprint:rawFp,resumed:true}}}
 }
 return{canonical,observed,fingerprint:rawFp,resumed:false};
}
async function finishOpenJourney(env,user,raw,t,state,event){
 const completed=event&&['delivery_completed','delivery_completed_xp_only'].includes(String(event.type)),cancelled=event&&String(event.type)==='mission_cancelled';
 let canonical=String(state?.canonical||'').trim();if(!canonical&&state?.observed){const a=await env.DB.prepare('SELECT trip_id FROM open_trip_aliases WHERE observed_trip_id=? AND user=?').bind(state.observed,user).first();canonical=String(a?.trip_id||'').trim()}
 if(completed||cancelled){if(canonical)await env.DB.prepare('DELETE FROM open_trips WHERE trip_id=? AND user=?').bind(canonical,user).run();return}
 const pr=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;try{m=pr?.current_mission_json?JSON.parse(pr.current_mission_json):null}catch{}if(!m)return;
 if(!m.game_name)m.game_name=journeyGame(raw);if(!m.context_key)m.context_key=journeyContext(raw);if(!m.journey_fingerprint)m.journey_fingerprint=journeyFingerprintMission(m,raw);
 canonical=await saveOpenJourney(env,user,m,raw,t,m.state==='active'?'active':'suspended')||canonical;if(state?.observed&&canonical)await openJourneyAlias(env,user,state.observed,canonical,t);
 await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
}

function damageRankCanAutoScore(m,reason){
 const g=m?.rank_guard||{};
 if(!g.verified_at)return false;
 if(reason==='damage_data_incomplete')return true;
 if(reason!=='telemetry_resume_pending')return false;
 const trigger=String(g.resume_trigger||g.transient_invalid_reason||'');
 return trigger==='damage_data_incomplete'||trigger==='migrated_damage_data_incomplete';
}
function startupRankCanAutoScore(m,reason){
 const g=m?.rank_guard||{};
 if(reason!=='telemetry_not_verified_from_start'||g.verified_at||m?.trip_progress_confirmed!==true)return false;
 const lastInvalid=String(g.last_invalid_reason||'');
 if(lastInvalid&&lastInvalid!=='damage_data_incomplete')return false;
 const preflight=g.preflight_truck_damage_ready===true;
 const startupSample=Math.max(0,Number(g.valid_samples)||0)>=1;
 if(!preflight&&!startupSample)return false;
 if(lastInvalid==='damage_data_incomplete'&&!preflight)return false;
 const planned=Math.max(0,Number(m?.planned_distance_km)||0);
 const start=Math.max(0,Number(m?.start_remaining_km)||0,Number(m?.rbr_start_remaining_km)||0);
 if(planned<=0||start<=0)return false;
 const ratio=start/planned;
 return ratio>=0.80&&ratio<=1.25;
}

export async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){
 let economyOK=true;
 try{await economyTrack(env.DB,user,raw,t)}catch(e){economyOK=false;console.error('Economy evidence failed:',e.message)}
 const event=await processMissionCore(env,user,raw,t,previousAt,preflightTruckDamageReady);
 try{await economyTrack(env.DB,user,raw,t);if(economyOK)await economyFinish(env.DB,user,t,event)}catch(e){console.error('Economy result failed:',e.message)}
 return event;
}
async function processMissionCore(env,user,raw,t,previousAt,preflightTruckDamageReady=false){
 const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;if(row?.current_mission_json){try{m=JSON.parse(row.current_mission_json)}catch{m=null}}
 const adminTest=false;
 const observed=flat(user,user,t,raw);
 if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){
  const cargoName=String(observed.cargo_name||'').trim();
  m={id:`${month(t)}-${user}-open-${randomHex(6)}`,catalog_id:'__open_cargo__',sequence:null,title:cargoName,category:'Carga detectada',state:'assigned',min_km:adminTest?0:MIN_KM,open_cargo:true,custom_cargo:cargoName,xp_only:false,created_at:t,assigned_at:t};
  await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 // Compatibilidade de upgrade: uma carga que estava pendente na 1.0.54 vira carga aberta.
 if(m&&(m.pending_classification===true||m.catalog_id==='__unclassified__')){
  m={...m,open_cargo:true,catalog_id:'__open_cargo__',sequence:null,title:String(m.cargo||m.title||observed.cargo_name||'Carga detectada'),category:'Carga detectada',custom_cargo:String(m.cargo||observed.cargo_name||m.title||'Carga')};
  delete m.pending_classification;delete m.classification_mode;delete m.classification_confidence;delete m.classification_suggested_work_id;
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
     m={...m,state:'active',min_km:minKm,cargo:f.cargo_name||m.cargo||'Carga',cargo_id:f.cargo_id||m.cargo_id||'',source:f.source_city||m.source||'',source_city_id:f.source_city_id||m.source_city_id||'',destination:f.destination_city||m.destination||'',destination_city_id:f.destination_city_id||m.destination_city_id||'',weight_kg:f.mass_kg||m.weight_kg||0,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,job_latch_key:f.job_latch_key||'',start_remaining_km:teleKm>0?teleKm:0,start_odometer_km:odoNow>0?odoNow:0,start_truck_identity:truckIdentity(raw),trip_progress_confirmed:false,delivery_progress_confirmed:false,rank_guard:adminTest?{reason:null,verified_at:t,last_sample_at:t,valid_samples:2,preflight_truck_damage_ready:true}:{reason:'telemetry_not_verified_from_start',valid_samples:rankingReadiness(raw).eligible?1:0,startup_started_at:t,last_sample_at:t,last_invalid_reason:rankingReadiness(raw).reason||null,preflight_truck_damage_ready:!!preflightTruckDamageReady},last_rejected_reason:undefined,last_rejected_at:undefined,cancel_flag_start:bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled'),cancel_flag_last:bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled'),delivery_details_start:JSON.stringify(pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{}),trip_id:String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||''),started_at:t};
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
 const scoringTripKey=String(str(raw,'gat_trip_id','gatTripId')||str(raw,'job_latch_key','jobLatchKey')||m.trip_id||m.job_latch_key||m.id||'');
 if(hasLoadedJob&&m.state==='active'){
   let damageChanged=false;

   // NOVA VIAGEM: o estado atual vira o ponto zero.
   // Dano antigo do caminhao nao pertence a esta viagem.
   if(!m.scoring_started_at||String(m.scoring_trip_key||'')!==scoringTripKey){
     m.scoring_started_at=t;
     m.scoring_trip_key=scoringTripKey;
     m.gat_speeding_seconds=0;
     m.gat_speed_observed_seconds=0;
     m.gat_speed_compliant_seconds=0;
     m.gat_speed_events=0;
     m.gat_speeding_event_seconds=0;
     m.gat_speeding_longest_seconds=0;
     m.gat_speed_max_over_kmh=0;
     m.gat_speed_normal_streak_s=0;
     m.gat_speed_grace_available=true;
     m.gat_speed_grace_remaining_s=0;
     m.gat_speed_last_at=t;
     m.gat_speed_last_state='invalid';

     m.cargo_damage_start_pct=Math.max(0,damageAliasPct(raw,'cargo_damage_pct'));

     m.truck_damage_start_pct=truckNow;
     m.truck_damage_max_pct=truckNow;

     m.truck_engine_damage_start_pct=truckPartsNow.engine;
     m.truck_engine_damage_max_pct=truckPartsNow.engine;

     m.truck_transmission_damage_start_pct=truckPartsNow.transmission;
     m.truck_transmission_damage_max_pct=truckPartsNow.transmission;

     m.truck_cabin_damage_start_pct=truckPartsNow.cabin;
     m.truck_cabin_damage_max_pct=truckPartsNow.cabin;

     m.truck_chassis_damage_start_pct=truckPartsNow.chassis;
     m.truck_chassis_damage_max_pct=truckPartsNow.chassis;

     m.truck_wheels_damage_start_pct=truckPartsNow.wheels;
     m.truck_wheels_damage_max_pct=truckPartsNow.wheels;

     m.trailer_damage_start_pct=trailerNow;
     m.trailer_damage_max_pct=trailerNow;

     damageChanged=true;
   }

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
 const packetTripMismatch=!!(packetTripId&&missionTripId&&packetTripId!==missionTripId);
 const valueChanged=(a,b,normalize=clean)=>!!(a&&b&&normalize(a)!==normalize(b));
 const cargoChanged=f.cargo_id&&m.cargo_id?valueChanged(f.cargo_id,m.cargo_id):valueChanged(f.cargo_name,m.cargo,norm);
 const sourceChanged=f.source_city_id&&m.source_city_id?valueChanged(f.source_city_id,m.source_city_id):valueChanged(f.source_city,m.source,norm);
 const destinationChanged=f.destination_city_id&&m.destination_city_id?valueChanged(f.destination_city_id,m.destination_city_id):valueChanged(f.destination_city,m.destination,norm);
 const weightChanged=Number(f.mass_kg)>0&&Number(m.weight_kg)>0&&Math.round(Number(f.mass_kg))!==Math.round(Number(m.weight_kg));
 const jobIdentityChanged=!!(hasLoadedJob&&(cargoChanged||sourceChanged||destinationChanged||weightChanged));
 const truckIdNow=truckIdentity(raw),truckIdStart=String(m.start_truck_identity||''),truckIdentityMismatch=!!(truckIdStart&&truckIdNow&&truckIdStart!==truckIdNow);
 const progressStartRemaining=Number(m.start_remaining_km)||0,progressStartOdometer=Number(m.start_odometer_km)||0;
 const deliveryProgressByRemaining=progressStartRemaining>0&&teleKm>=0&&(progressStartRemaining-teleKm)>=1;
 const deliveryProgressByOdometer=progressStartOdometer>0&&odoNow>0&&(odoNow-progressStartOdometer)>=1;
 if(m.state==='active'&&m.delivery_progress_confirmed!==true&&!truckIdentityMismatch&&(deliveryProgressByRemaining||deliveryProgressByOdometer)){
   m.delivery_progress_confirmed=true;
   await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 const deliveryProgressConfirmed=!truckIdentityMismatch&&(m.delivery_progress_confirmed===true||deliveryProgressByRemaining||deliveryProgressByOdometer);
 const deliveryDetails=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},deliveryDetailsNow=JSON.stringify(deliveryDetails),deliveryDetailsStart=String(m.delivery_details_start||'');
 const deliveryDetailsPositive=num(deliveryDetails,'distanceKm','distance_km')>0||num(deliveryDetails,'revenue')>0||num(deliveryDetails,'earnedXp','earned_xp')>0;
 const deliveryDetailsChanged=(!hasLoadedJob||tripReplaced||jobIdentityChanged)&&deliveryDetailsStart&&deliveryDetailsNow!==deliveryDetailsStart&&deliveryDetailsPositive;
 const legacyDeliveryFallback=!hasLoadedJob&&!deliveryDetailsStart&&deliveryProgressConfirmed&&teleKm>=0&&teleKm<=2&&deliveryDetailsPositive;
 const outcome=resolveJobOutcome({event:gatJobEvent,cancelFlag:!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled'),cancelWasActive:m.cancel_flag_last===true||m.cancel_flag_last===undefined&&m.cancel_flag_start===true,eventMatchesTrip:!packetTripMismatch,freshReceipt:!!deliveryDetailsChanged,legacyFallback:legacyDeliveryFallback});
 const deliveredSignal=outcome.delivered;
 const deliveryMatchesMission=(!packetTripMismatch&&!jobIdentityChanged)||!!deliveryDetailsChanged;
 const delivered=deliveredSignal&&deliveryProgressConfirmed&&deliveryMatchesMission;
 const cancelled=outcome.cancelled;
 if(m.state==='active'){
   let guardChanged=hasLoadedJob?updateMissionFineTracker(m,raw,t):false;
   if(hasLoadedJob){const currentCancel=bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled');if(m.cancel_flag_last!==currentCancel){m.cancel_flag_last=currentCancel;guardChanged=true;}}
    if(hasLoadedJob&&updateMissionSpeedingTracker(m,raw,t))guardChanged=true;
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
 if(outcome.conflict&&!hasLoadedJob){m={...m,state:'suspended',pending_review:true,last_rejected_reason:'conflicting_new_cancel_and_delivery_receipt',last_rejected_at:t,pending_delivery_details:deliveryDetails,pending_delivery_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();return{type:'mission_pending_review',reason:m.last_rejected_reason,trip_id:missionTripId,mission:m}}
 if(!delivered&&cancelled&&(!hasLoadedJob||tripReplaced)&&(m.state==='active'||m.state==='suspended')){if(m.classification_mode==='automatic'||m.classification_mode==='pending'){await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(t,user).run();return{type:'mission_cancelled',reason:tripReplaced?'trip_replaced':'observed_job_end',trip_id:missionTripId||null,mission:null}}m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}
 if(!delivered&&!hasLoadedJob&&m.state==='active'){m={...m,state:'suspended',suspended_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();return{type:'mission_suspended',mission:m}}
 if(!delivered&&!hasLoadedJob&&m.state==='suspended')return{type:'mission_suspended',mission:m};
 if(!delivered)return hasLoadedJob?{type:'mission_in_progress',mission:m,distance_km:planned}:null;
 const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr;
 const plannedDistance=Number(m.planned_distance_km)||Number(m.rbr_start_remaining_km)||baseKm||0;
 const startOdometer=Number(m.start_odometer_km)||0;
 const actualOdometerDistance=!truckIdentityMismatch&&startOdometer>0&&odoNow>=startOdometer?odoNow-startOdometer:0;
 const fallbackDistance=Number(details.distanceKm)||Number(details.distance_km)||plannedDistance||teleKm||0;
 const distanceAudit=recognizedTripDistance(plannedDistance,actualOdometerDistance,fallbackDistance);
 const distance=distanceAudit.distance_km,rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankReason=null,rankEligible=true;
 const source=String(m.source||f.source_city||'Origem nao informada').trim()||'Origem nao informada',destination=String(m.destination||f.destination_city||'Destino nao informado').trim()||'Destino nao informado';
 const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||'')
 const missionId=String(m.id||`${mk}-${user}-${workId}-${m.created_at||m.assigned_at||''}`),guard=await env.DB.prepare('INSERT OR IGNORE INTO mission_completions(mission_id,user,completed_at) VALUES(?,?,?)').bind(missionId,user,t).run();if(!guard.meta?.changes)return{type:'delivery_ignored',reason:'duplicate_event'};
 const damageEventRaw=Math.max(0,Number(details.cargoDamage)||0),damageEventPct=damageEventRaw<=1.0001?damageEventRaw*100:damageEventRaw,cargoFinalDamage=Math.max(damageEventPct,damageAliasPct(raw,'cargo_damage_pct')),cargoStartDamage=Math.max(0,Number(m.cargo_damage_start_pct)||0),damage=damageDelta(cargoFinalDamage,cargoStartDamage),
 truckStart=Math.max(0,Number(m.truck_damage_start_pct)||0),truckMax=Math.max(truckStart,Number(m.truck_damage_max_pct)||0,truckNow),
 engineStart=Math.max(0,Number(m.truck_engine_damage_start_pct)||0),engineMax=Math.max(engineStart,Number(m.truck_engine_damage_max_pct)||0,truckPartsNow.engine),engineDelta=damageDelta(engineMax,engineStart),
 transmissionStart=Math.max(0,Number(m.truck_transmission_damage_start_pct)||0),transmissionMax=Math.max(transmissionStart,Number(m.truck_transmission_damage_max_pct)||0,truckPartsNow.transmission),transmissionDelta=damageDelta(transmissionMax,transmissionStart),
 cabinStart=Math.max(0,Number(m.truck_cabin_damage_start_pct)||0),cabinMax=Math.max(cabinStart,Number(m.truck_cabin_damage_max_pct)||0,truckPartsNow.cabin),cabinDelta=damageDelta(cabinMax,cabinStart),
 chassisStart=Math.max(0,Number(m.truck_chassis_damage_start_pct)||0),chassisMax=Math.max(chassisStart,Number(m.truck_chassis_damage_max_pct)||0,truckPartsNow.chassis),chassisDelta=damageDelta(chassisMax,chassisStart),
 wheelsStart=Math.max(0,Number(m.truck_wheels_damage_start_pct)||0),wheelsMax=Math.max(wheelsStart,Number(m.truck_wheels_damage_max_pct)||0,truckPartsNow.wheels),wheelsDelta=damageDelta(wheelsMax,wheelsStart),
 aggregateTruckDelta=damageDelta(truckMax,truckStart),truckDamage=Math.max(aggregateTruckDelta,engineDelta,transmissionDelta,cabinDelta,chassisDelta,wheelsDelta),
 trailerStart=Math.max(0,Number(m.trailer_damage_start_pct)||0),trailerMax=Math.max(trailerStart,Number(m.trailer_damage_max_pct)||0,trailerNow),trailerDelta=damageDelta(trailerMax,trailerStart),
 cargo=m.cargo||f.cargo_name||m.custom_cargo||m.title||'Carga',weight=Number(m.weight_kg)||f.mass_kg||0,
 score=calculateGatScore({at:t,distanceKm:distance,weightKg:weight,cargoName:cargo,cargoDamagePct:damage,truckDamagePct:truckDamage,trailerDamagePct:trailerDelta,deliveryDetails:details,mission:m,adminTest}),
 fines=score.fine_count,baseXP=Math.max(0,Math.floor(distance)),gatSpeedPenalty=score.fine_penalty_points,gatCargoPenalty=score.cargo_penalty_points,gatTruckPenalty=score.vehicle_penalty_points,xpPenalty=gatSpeedPenalty+gatCargoPenalty+gatTruckPenalty,pointPenalty=score.point_penalty_points,gatBasePoints=Math.max(0,Math.floor(distance)),gatSpeedingSeconds=Math.max(0,Number(m.gat_speeding_seconds)||0),gatSpeedCompliantSeconds=Math.max(0,Number(m.gat_speed_compliant_seconds)||0),gatSpeedObservedSeconds=Math.max(0,Number(m.gat_speed_observed_seconds)||0),gatSpeedTrackedSeconds=gatSpeedObservedSeconds,gatSpeedingRatio=gatSpeedTrackedSeconds>0?Math.min(1,gatSpeedingSeconds/gatSpeedTrackedSeconds):0,gatSpeedingPenalty=Math.min(gatBasePoints,Math.round(gatBasePoints*gatSpeedingRatio)),gatWeightBonus=score.weight_bonus_points,gatPoints=rankEligible?Math.max(0,gatBasePoints-gatSpeedingPenalty+gatWeightBonus-score.point_penalty_points):0,perfect=adminTest?1:(rankEligible&&damage<=0.5&&Math.max(truckDamage,trailerDelta)<=0.5&&fines===0?1:0),bonus=0,penalty=xpPenalty,xpBase=gatPoints,
xpHasPenalty=gatSpeedingPenalty>0||score.point_penalty_points>0,
xpModifier=xpHasPenalty?0.90:1.10,
xp=Math.max(0,Math.round(xpBase*xpModifier)),
 auditData={base_xp:baseXP,speed_penalty_xp:gatSpeedPenalty,cargo_penalty_xp:gatCargoPenalty,truck_penalty_xp:gatTruckPenalty,perfect_bonus_xp:bonus,cargo_damage_pct:damage,truck_damage_start_pct:truckStart,truck_damage_max_pct:truckMax,truck_damage_delta_pct:truckDamage,truck_overall_delta_pct:aggregateTruckDelta,truck_engine_damage_start_pct:engineStart,truck_engine_damage_max_pct:engineMax,truck_engine_damage_delta_pct:engineDelta,truck_transmission_damage_start_pct:transmissionStart,truck_transmission_damage_max_pct:transmissionMax,truck_transmission_damage_delta_pct:transmissionDelta,truck_cabin_damage_start_pct:cabinStart,truck_cabin_damage_max_pct:cabinMax,truck_cabin_damage_delta_pct:cabinDelta,truck_chassis_damage_start_pct:chassisStart,truck_chassis_damage_max_pct:chassisMax,truck_chassis_damage_delta_pct:chassisDelta,truck_wheels_damage_start_pct:wheelsStart,truck_wheels_damage_max_pct:wheelsMax,truck_wheels_damage_delta_pct:wheelsDelta,trailer_damage_start_pct:trailerStart,trailer_damage_max_pct:trailerMax,trailer_damage_delta_pct:trailerDelta,vehicle_damage_delta_pct:score.vehicle_damage_pct,rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,automatic_ranking_reason:null,ranking_integrity_reason:rankGuardReason||null,ranking_recovered_from_damage_evidence:false,ranking_recovered_from_start_evidence:false,ranking_message:score.use_v2?'Pontos GAT: 1 ponto por km, com reducao proporcional por excesso de velocidade + bonus de peso - multas e danos.':'Pontos GAT calculados por distancia: 1 ponto por km, menos as penalidades da viagem.',history_recorded:true,admin_test_mode:adminTest,rank_client_version:raw.gat_client_version,perfect_trip:!!perfect,xp_awarded:xp,gat_scoring_version:score.scoring_version,gat_scoring_v2_active:score.use_v2,gat_scoring_v2_starts_at:GAT_SCORING_V2_START_LOCAL,gat_base_points:gatBasePoints,gat_speeding_seconds:gatSpeedingSeconds,gat_speed_compliant_seconds:gatSpeedCompliantSeconds,gat_speed_observed_seconds:gatSpeedObservedSeconds,gat_speeding_ratio_pct:Math.round(gatSpeedingRatio*10000)/100,gat_speeding_penalty_points:gatSpeedingPenalty,speed_limit_tolerance_kmh:SPEED_LIMIT_TOLERANCE_KMH,speed_limit_rule:'posted_limit_plus_1_kmh', gat_speeding_events:Number(m.gat_speed_events)||0,gat_speeding_longest_seconds:Number(m.gat_speeding_longest_seconds)||0,gat_speeding_max_over_kmh:Number(m.gat_speed_max_over_kmh)||0,gat_weight_bonus_points:gatWeightBonus,gat_speed_penalty_points:gatSpeedPenalty,gat_fine_penalty_points:gatSpeedPenalty,gat_fine_count:score.fine_count,gat_speed_fine_count:score.speed_fine_count,gat_other_fine_count:score.other_fine_count,gat_receipt_speed_fines:score.receipt_speed_fines,gat_observed_fines:score.observed_fines,gat_fine_events:Array.isArray(m.gat_fine_events)?m.gat_fine_events:[],gat_cargo_penalty_points:gatCargoPenalty,gat_truck_penalty_points:gatTruckPenalty,gat_vehicle_penalty_points:gatTruckPenalty,gat_penalty_points:pointPenalty,gat_points:gatPoints};
 Object.assign(auditData,distanceAudit);
 if(m.open_cargo){
  const openAudit={...auditData,gat_points:gatPoints,
xp_base_points:xpBase,
xp_quality_modifier_pct:xpHasPenalty?-10:10,
xp_has_penalty:xpHasPenalty,
xp_awarded:xp,cargo_mode:'open',cargo_rule:'none'};
  await env.DB.batch([
   env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:openAudit,map_mode:rbr?'rbr':'base'})),
   env.DB.prepare("UPDATE profiles SET monthly_completed=(SELECT COUNT(*) FROM deliveries d WHERE d.user=profiles.user AND strftime('%Y-%m',d.delivered_at,'-3 hours')=?),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?").bind(mk,distance,xp,gatPoints,perfect,penalty,fines,penalty,t,user),
   env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)
  ]);
  const achievementReward=await awardGatAchievements(env,user,t);
return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,xp_awarded:xp,achievement_xp:achievementReward.xp,achievements_unlocked:achievementReward.unlocked,perfect:!!perfect,gat_points:gatPoints,
xp_base_points:xpBase,
xp_quality_modifier_pct:xpHasPenalty?-10:10,
xp_has_penalty:xpHasPenalty,
xp_awarded:xp,rank_eligible:rankEligible,ranking_reason:rankReason,history_recorded:true,open_cargo:true,min_km:MIN_KM};
 }
 await env.DB.batch([env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,Number(m.sequence)||null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:auditData,map_mode:rbr?'rbr':'base'})),env.DB.prepare("UPDATE profiles SET monthly_completed=(SELECT COUNT(*) FROM deliveries d WHERE d.user=profiles.user AND strftime('%Y-%m',d.delivered_at,'-3 hours')=?),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?").bind(mk,distance,xp,perfect,penalty,fines,penalty,t,user),env.DB.prepare('INSERT OR IGNORE INTO work_completed(user,work_id,month_key,completed_at) VALUES(?,?,?,?)').bind(user,workId,mk,t),env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)]);
 const achievementReward=await awardGatAchievements(env,user,t);
return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,xp_awarded:xp,achievement_xp:achievementReward.xp,achievements_unlocked:achievementReward.unlocked,perfect:!!perfect,gat_points:gatPoints,
xp_base_points:xpBase,
xp_quality_modifier_pct:xpHasPenalty?-10:10,
xp_has_penalty:xpHasPenalty,
xp_awarded:xp,rank_eligible:rankEligible,ranking_reason:rankReason,history_recorded:true,min_km:MIN_KM}
}


// GAT_ACHIEVEMENTS_V1
const GAT_ACHIEVEMENTS=[
 {id:'peso_pesado',title:'Peso Pesado',difficulty:'Fácil',xp:250},
 {id:'entrega_perfeita',title:'Entrega Perfeita',difficulty:'Média',xp:500},
 {id:'explorador',title:'Explorador',difficulty:'Média',xp:500},
 {id:'gigante_estrada',title:'Gigante da Estrada',difficulty:'Média',xp:500},
 {id:'carga_extrema',title:'Carga Extrema',difficulty:'Difícil',xp:1000},
 {id:'longa_jornada',title:'Longa Jornada',difficulty:'Difícil',xp:1000},
 {id:'viagem_impecavel',title:'Viagem Impecável',difficulty:'Difícil',xp:1000},
 {id:'veterano',title:'Veterano',difficulty:'Difícil',xp:1000},
 {id:'horizonte_distante',title:'Horizonte Distante',difficulty:'Muito difícil',xp:2000},
 {id:'lenda_estrada',title:'Lenda da Estrada',difficulty:'Muito difícil',xp:2000},

 {id:'primeiros_passos',title:'Primeiros Passos',difficulty:'Fácil',xp:250},
 {id:'estradeiro',title:'Estradeiro',difficulty:'Média',xp:500},
 {id:'rodagem_inicial',title:'Rodagem Inicial',difficulty:'Fácil',xp:250},
 {id:'pe_na_estrada',title:'Pé na Estrada',difficulty:'Média',xp:500},
 {id:'rodador_nato',title:'Rodador Nato',difficulty:'Difícil',xp:1000},
 {id:'carga_limpa',title:'Carga Limpa',difficulty:'Média',xp:500},
 {id:'direcao_segura',title:'Direção Segura',difficulty:'Média',xp:500},
 {id:'motorista_exemplar',title:'Motorista Exemplar',difficulty:'Difícil',xp:1000},
 {id:'rei_do_peso',title:'Rei do Peso',difficulty:'Difícil',xp:1000},
 {id:'maratonista',title:'Maratonista',difficulty:'Muito difícil',xp:2000}
];

async function gatAchievementStats(env,user){
 const s=await env.DB.prepare(`
  SELECT
   COUNT(*) deliveries,
   COALESCE(SUM(distance_km),0) total_km,
   COALESCE(MAX(distance_km),0) max_distance,
   COALESCE(MAX(weight_kg),0) max_weight,

   COALESCE(SUM(
    CASE
     WHEN distance_km>=500
      AND CAST(json_extract(raw_json,'$.audit.cargo_damage_pct') AS REAL)=0
     THEN 1 ELSE 0
    END
   ),0) perfect_cargo_500,

   COALESCE(SUM(
    CASE
     WHEN CAST(json_extract(raw_json,'$.audit.cargo_damage_pct') AS REAL)=0
     THEN 1 ELSE 0
    END
   ),0) clean_cargo,

   COALESCE(SUM(
    CASE
     WHEN json_type(raw_json,'$.audit.gat_speeding_penalty_points') IS NOT NULL
      AND CAST(json_extract(raw_json,'$.audit.gat_speeding_penalty_points') AS INTEGER)=0
     THEN 1 ELSE 0
    END
   ),0) safe_speed,

   COALESCE(SUM(
    CASE
     WHEN json_type(raw_json,'$.audit.gat_speeding_penalty_points') IS NOT NULL
      AND CAST(json_extract(raw_json,'$.audit.gat_speeding_penalty_points') AS INTEGER)=0
      AND CAST(json_extract(raw_json,'$.audit.cargo_damage_pct') AS REAL)=0
      AND CAST(json_extract(raw_json,'$.audit.gat_truck_penalty_points') AS INTEGER)=0
     THEN 1 ELSE 0
    END
   ),0) exemplar,

   COALESCE(SUM(
    CASE WHEN weight_kg>=40000 THEN 1 ELSE 0 END
   ),0) heavy40,

   COALESCE(SUM(
    CASE WHEN distance_km>=2000 THEN 1 ELSE 0 END
   ),0) marathon,

   COALESCE(SUM(
    CASE
     WHEN distance_km>=1000
      AND json_type(raw_json,'$.audit.gat_speeding_penalty_points') IS NOT NULL
      AND CAST(json_extract(raw_json,'$.audit.gat_speeding_penalty_points') AS INTEGER)=0
      AND CAST(json_extract(raw_json,'$.audit.cargo_damage_pct') AS REAL)=0
      AND CAST(json_extract(raw_json,'$.audit.gat_truck_penalty_points') AS INTEGER)=0
     THEN 1 ELSE 0
    END
   ),0) impeccable1000

  FROM deliveries
  WHERE user=?
 `).bind(user).first();

 const cities=await env.DB.prepare(
  'SELECT source,destination FROM deliveries WHERE user=?'
 ).bind(user).all();

 const citySet=new Set();

 for(const r of cities.results||[]){
  const a=String(r.source||'').trim().toLowerCase();
  const b=String(r.destination||'').trim().toLowerCase();

  if(a)citySet.add(a);
  if(b)citySet.add(b);
 }

 return{
  deliveries:Number(s?.deliveries||0),
  totalKm:Number(s?.total_km||0),
  maxDistance:Number(s?.max_distance||0),
  maxWeight:Number(s?.max_weight||0),
  cities:citySet.size,

  perfectCargo500:Number(s?.perfect_cargo_500||0),
  cleanCargo:Number(s?.clean_cargo||0),
  safeSpeed:Number(s?.safe_speed||0),
  exemplar:Number(s?.exemplar||0),
  heavy40:Number(s?.heavy40||0),
  marathon:Number(s?.marathon||0),
  impeccable1000:Number(s?.impeccable1000||0)
 };
}

function gatAchievementUnlocked(id,s){
 switch(id){

  case 'peso_pesado':
   return s.maxWeight>=30000;

  case 'entrega_perfeita':
   return s.perfectCargo500>=1;

  case 'explorador':
   return s.cities>=25;

  case 'gigante_estrada':
   return s.maxWeight>=50000;

  case 'carga_extrema':
   return s.maxWeight>=60000;

  case 'longa_jornada':
   return s.maxDistance>=3000;

  case 'viagem_impecavel':
   return s.impeccable1000>=1;

  case 'veterano':
   return s.deliveries>=500;

  case 'horizonte_distante':
   return s.maxDistance>=5000;

  case 'lenda_estrada':
   return s.deliveries>=1500;

  case 'primeiros_passos':
   return s.deliveries>=10;

  case 'estradeiro':
   return s.deliveries>=50;

  case 'rodagem_inicial':
   return s.totalKm>=5000;

  case 'pe_na_estrada':
   return s.totalKm>=25000;

  case 'rodador_nato':
   return s.totalKm>=100000;

  case 'carga_limpa':
   return s.cleanCargo>=10;

  case 'direcao_segura':
   return s.safeSpeed>=10;

  case 'motorista_exemplar':
   return s.exemplar>=10;

  case 'rei_do_peso':
   return s.heavy40>=10;

  case 'maratonista':
   return s.marathon>=5;
 }

 return false;
}

async function awardGatAchievements(env,user,t){

 const stats=await gatAchievementStats(env,user);

 let xpReward=0;
 const unlocked=[];

 for(const a of GAT_ACHIEVEMENTS){

  if(!gatAchievementUnlocked(a.id,stats))
   continue;

  const key=`achievement:${user}:${a.id}`;

  const value=JSON.stringify({
   id:a.id,
   title:a.title,
   difficulty:a.difficulty,
   xp:a.xp,
   unlocked_at:t
  });

  const result=await env.DB.prepare(
   'INSERT OR IGNORE INTO meta(key,value) VALUES(?,?)'
  ).bind(key,value).run();

  if(result.meta?.changes){
   xpReward+=a.xp;

   unlocked.push({
    id:a.id,
    title:a.title,
    difficulty:a.difficulty,
    xp:a.xp
   });
  }
 }

 if(xpReward>0){
  await env.DB.prepare(
   'UPDATE profiles SET xp=xp+?,updated_at=? WHERE user=?'
  ).bind(xpReward,t,user).run();
 }

 return{
  xp:xpReward,
  unlocked
 };
}

async function gatAchievementRewards(env,user){

 const rows=await env.DB.prepare(
  'SELECT value FROM meta WHERE key LIKE ? ORDER BY key'
 ).bind(`achievement:${user}:%`).all();

 const out=[];

 for(const row of rows.results||[]){
  try{
   const a=JSON.parse(row.value||'{}');
   if(a&&a.id)out.push(a);
  }catch{}
 }

 return out;
}

async function clientCredential(env,driver,device,token){if(!token)return{error:'token_required'};const hash=await sha(token),r=await env.DB.prepare('SELECT ct.token_hash,ct.driver,ct.account_user,ct.device_id,ct.revoked_at,ct.last_seen_at,a.disabled FROM client_tokens ct LEFT JOIN accounts a ON a.user=ct.account_user WHERE ct.token_hash=?').bind(hash).first();if(!r||r.revoked_at||!r.account_user||r.disabled)return{error:'link_required'};if(clean(r.driver)!==clean(driver))return{error:'driver_mismatch'};if(r.device_id&&device&&r.device_id!==device)return{error:'device_mismatch'};if(!r.last_seen_at||Date.now()-Date.parse(r.last_seen_at)>=60000)await env.DB.prepare('UPDATE client_tokens SET last_seen_at=? WHERE token_hash=?').bind(now(),hash).run();return{row:r}}
async function pairing(env,driver,device){const old=await env.DB.prepare('SELECT code_plain,expires_at FROM client_pairings WHERE driver=? AND device_id=? AND claimed_user IS NULL').bind(driver,device).first();if(old&&Date.parse(old.expires_at)>Date.now())return old;const code=randomHex(4).toUpperCase(),expires=new Date(Date.now()+15*60000).toISOString();await env.DB.prepare('INSERT INTO client_pairings(code_hash,code_plain,driver,device_id,created_at,expires_at,claimed_user) VALUES(?,?,?,?,?,?,NULL) ON CONFLICT(driver,device_id) DO UPDATE SET code_hash=excluded.code_hash,code_plain=excluded.code_plain,created_at=excluded.created_at,expires_at=excluded.expires_at,claimed_user=NULL').bind(await sha(code),code,driver,device,now(),expires).run();return{code_plain:code,expires_at:expires}}
async function finishPair(env,driver,device){const p=await env.DB.prepare('SELECT code_hash,claimed_user,expires_at FROM client_pairings WHERE driver=? AND device_id=?').bind(driver,device).first();if(!p?.claimed_user||Date.parse(p.expires_at)<=Date.now())return null;const token=randomHex(32);await env.DB.batch([env.DB.prepare('UPDATE client_tokens SET revoked_at=? WHERE device_id=? AND revoked_at IS NULL').bind(now(),device),env.DB.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at,revoked_at) VALUES(?,?,?,?,?,?,NULL)').bind(await sha(token),driver,p.claimed_user,device,now(),now()),env.DB.prepare('DELETE FROM client_pairings WHERE code_hash=?').bind(p.code_hash)]);return{token,account_user:p.claimed_user}}


function gatReviewSuggestion(a={}){
  const own=k=>Object.prototype.hasOwnProperty.call(a||{},k)&&Number.isFinite(Number(a?.[k]));
  const available=own('gat_base_points')&&own('gat_speed_penalty_points')&&own('gat_cargo_penalty_points')&&own('gat_truck_penalty_points');
  if(!available)return{available:false,points:null,base:null,weight:null,speed:null,cargo:null,truck:null};
  const v2=String(a.gat_scoring_version||'').includes('2026-10-01-v2')||a.gat_scoring_v2_active===true;
  const rawBase=Math.max(0,Math.round(Number(a.gat_base_points))),base=v2?rawBase:Math.min(100,rawBase),weight=v2?Math.max(0,Math.round(Number(a.gat_weight_bonus_points)||0)):0,speed=Math.max(0,Math.round(Number(a.gat_speed_penalty_points))),cargo=Math.max(0,Math.round(Number(a.gat_cargo_penalty_points))),truck=Math.max(0,Math.round(Number(a.gat_truck_penalty_points)));
  const points=Math.max(0,base+weight-speed-cargo-truck);
  return{available:true,points:v2?points:Math.min(100,points),base,weight,speed,cargo,truck};
}
function gatReviewView(a={}){
  const suggestion=gatReviewSuggestion(a),manual=a?.gat_manual_review&&typeof a.gat_manual_review==='object'?a.gat_manual_review:null,current=Number.isFinite(Number(a?.gat_points))?Number(a.gat_points):0,reason=String(a?.ranking_reason||a?.automatic_ranking_reason||'');
  return{gat_review_suggested_points:suggestion.points,gat_review_suggestion_available:suggestion.available,gat_reviewable:current===0&&!!reason&&suggestion.available&&!manual,gat_review_status:manual?.status||null,gat_review_actor:manual?.actor||null,gat_review_at:manual?.at||null,gat_review_previous_points:manual?.previous_points??null,gat_review_approved_points:manual?.approved_points??null};
}

async function adminDriver(env,target){const a=await env.DB.prepare('SELECT user,role,disabled,created_at,updated_at FROM accounts WHERE user=?').bind(target).first();if(!a)return null;const p=await profile(env,target),sessions=await env.DB.prepare('SELECT COUNT(*) total FROM sessions WHERE user=? AND expires_at>?').bind(target,now()).first(),tel=await env.DB.prepare('SELECT driver,account_user,updated_at,telemetry_json FROM telemetry_live WHERE account_user=? ORDER BY updated_at DESC LIMIT 1').bind(target).first();let liveData={online:false};if(tel){let raw={};try{raw=JSON.parse(tel.telemetry_json||'{}')}catch{}liveData={...flat(tel.driver,tel.account_user,tel.updated_at,raw),online:Date.now()-Date.parse(tel.updated_at)<30000}}p.deliveries=(Array.isArray(p?.deliveries)?p.deliveries:[]).map(x=>({...x,...gatReviewView(x)}));return{account:{...a,disabled:!!a.disabled,active_sessions:Number(sessions?.total||0)},profile:p,live:liveData}}
async function recalc(env,user){
 const s=await env.DB.prepare(
  'SELECT COUNT(*) total,COALESCE(SUM(distance_km),0) km,COALESCE(SUM(xp),0) xp,COALESCE(SUM(perfect),0) perfect,COALESCE(SUM(penalty_xp),0) penalties,COALESCE(SUM(speed_fines),0) fines FROM deliveries WHERE user=?'
 ).bind(user).first();

 const m=await env.DB.prepare(
  "SELECT COUNT(*) total FROM deliveries WHERE user=? AND strftime('%Y-%m',delivered_at,'-3 hours')=?"
 ).bind(user,month()).first();

 const achievementXp=await env.DB.prepare(
  "SELECT COALESCE(SUM(CAST(json_extract(value,'$.xp') AS INTEGER)),0) xp FROM meta WHERE key LIKE ?"
 ).bind(`achievement:${user}:%`).first();

 const totalXp=
  Number(s?.xp||0)+
  Number(achievementXp?.xp||0);

 await env.DB.prepare(
  'UPDATE profiles SET monthly_completed=?,total_deliveries=?,total_km=?,xp=?,perfect_trips=?,penalty_xp=?,speed_fines=?,safety_score=MAX(0,100-(?*0.1)),updated_at=? WHERE user=?'
 ).bind(
  Number(m?.total||0),
  Number(s?.total||0),
  Number(s?.km||0),
  totalXp,
  Number(s?.perfect||0),
  Number(s?.penalties||0),
  Number(s?.fines||0),
  Number(s?.penalties||0),
  now(),
  user
 ).run()
}

async function adminAction(req,env,b,actor){const action=String(b.action||''),target=clean(b.target);if(!target)throw new HttpError(400,'target_required');const a=await env.DB.prepare('SELECT user,role,disabled FROM accounts WHERE user=?').bind(target).first();if(!a)throw new HttpError(404,'not_found');if(a.role==='owner'&&actor.role!=='owner')throw new HttpError(403,'owner_protected');if(actor.role==='moderator'&&!['reset_mission','review_gat_points'].includes(action))throw new HttpError(403,'forbidden');
 if(action==='review_gat_points'){
   const id=Math.trunc(Number(b.delivery_id)),decision=String(b.review_decision||'');
   if(!Number.isFinite(id)||id<=0)throw new HttpError(400,'invalid_delivery_id');
   if(!['approve','keep_zero'].includes(decision))throw new HttpError(400,'invalid_review_decision');
   const d=await env.DB.prepare('SELECT id,raw_json FROM deliveries WHERE id=? AND user=?').bind(id,target).first();if(!d)throw new HttpError(404,'delivery_not_found');
   let raw={};try{raw=JSON.parse(d.raw_json||'{}')}catch{}if(!raw||typeof raw!=='object')raw={};
   const aData=raw.audit&&typeof raw.audit==='object'?{...raw.audit}:{},existing=aData.gat_manual_review&&typeof aData.gat_manual_review==='object'?aData.gat_manual_review:null;
   if(existing)throw new HttpError(409,'gat_review_already_done');
   const current=Number.isFinite(Number(aData.gat_points))?Math.max(0,Math.round(Number(aData.gat_points))):0,automaticReason=String(aData.ranking_reason||aData.automatic_ranking_reason||'');
   if(current!==0)throw new HttpError(409,'gat_review_requires_zero_points');
   if(!automaticReason)throw new HttpError(409,'gat_review_requires_validation_error');
   const suggestion=gatReviewSuggestion(aData);if(!suggestion.available)throw new HttpError(409,'gat_review_no_saved_breakdown');
   const approved=decision==='approve'?suggestion.points:0,reviewAt=now(),manual={status:decision==='approve'?'approved':'kept_zero',actor:actor.user,at:reviewAt,previous_points:current,suggested_points:suggestion.points,approved_points:approved,automatic_reason:automaticReason};
   aData.automatic_ranking_reason=aData.automatic_ranking_reason||automaticReason;
   aData.automatic_ranking_message=aData.automatic_ranking_message||String(aData.ranking_message||'');
   aData.gat_manual_review=manual;aData.gat_points_manual=decision==='approve';aData.gat_points=approved;
   if(decision==='approve'){
     aData.ranking_eligible=true;aData.rank_eligible=true;aData.ranking_reason=null;aData.ranking_message='Pontuacao GAT validada manualmente por @'+actor.user+'.';
   }
   raw.audit=aData;
   const delta=approved-current;
   await env.DB.batch([env.DB.prepare('UPDATE deliveries SET raw_json=? WHERE id=? AND user=?').bind(JSON.stringify(raw),id,target),env.DB.prepare('UPDATE profiles SET points=MAX(0,points+?),updated_at=? WHERE user=?').bind(delta,reviewAt,target)]);
   await audit(env,actor.user,'review_gat_points',target,{delivery_id:id,decision,previous_points:current,suggested_points:suggestion.points,approved_points:approved,points_delta:delta,automatic_reason:automaticReason});
   invalidateRead('profile:'+target);invalidateRead('get:/api/public/ranking:current');
   return json(req,{ok:true,action,target,delivery_id:id,decision,previous_points:current,suggested_points:suggestion.points,approved_points:approved,points_delta:delta});
 }
 if(action==='reset_mission')await env.DB.prepare('UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=?').bind(now(),target).run();
 else if(action==='block'||action==='unblock'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const disabled=action==='block'?1:0;await env.DB.batch([env.DB.prepare('UPDATE accounts SET disabled=?,updated_at=? WHERE user=?').bind(disabled,now(),target),...(disabled?[env.DB.prepare('DELETE FROM sessions WHERE user=?').bind(target),env.DB.prepare('UPDATE client_tokens SET revoked_at=? WHERE account_user=? AND revoked_at IS NULL').bind(now(),target)]:[])])}
 else if(action==='reset_password'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const password=String(b.password||'');if(password.length<8||password.length>128)throw new HttpError(400,'weak_password');const salt=randomHex(16);await env.DB.batch([env.DB.prepare('UPDATE accounts SET password_salt=?,password_hash=?,updated_at=? WHERE user=?').bind(salt,await passHash(password,salt),now(),target),env.DB.prepare('DELETE FROM sessions WHERE user=?').bind(target)])}
 else if(action==='role'){if(actor.role!=='owner'||a.role==='owner')throw new HttpError(403,'forbidden');const role=String(b.role||'');if(!['driver','moderator','admin'].includes(role))throw new HttpError(400,'invalid_role');await env.DB.prepare('UPDATE accounts SET role=?,updated_at=? WHERE user=?').bind(role,now(),target).run()}
 else if(action==='delete'){if(actor.role!=='owner'||a.role==='owner'||actor.user===target)throw new HttpError(403,'forbidden');await env.DB.prepare('DELETE FROM accounts WHERE user=?').bind(target).run()}
 else if(action==='set_progress'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const monthly=Math.trunc(Number(b.monthly_completed)),total=Math.trunc(Number(b.total_deliveries)),km=Number(b.total_km);if(!Number.isFinite(monthly)||!Number.isFinite(total)||!Number.isFinite(km)||monthly<0||monthly>40||total<monthly||km<0)throw new HttpError(400,'invalid_progress');await env.DB.prepare('UPDATE profiles SET monthly_completed=?,total_deliveries=?,total_km=?,updated_at=? WHERE user=?').bind(monthly,total,km,now(),target).run()}
 else if(action==='delete_delivery'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const id=Math.trunc(Number(b.delivery_id)),d=await env.DB.prepare('SELECT id,raw_json,delivered_at FROM deliveries WHERE id=? AND user=?').bind(id,target).first();if(!d)throw new HttpError(404,'delivery_not_found');let raw={};try{raw=JSON.parse(d.raw_json||'{}')}catch{}const work=String(raw?.mission?.catalog_id||'');await env.DB.batch([env.DB.prepare('DELETE FROM deliveries WHERE id=? AND user=?').bind(id,target),...(work?[env.DB.prepare('DELETE FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(target,work,month(d.delivered_at))]:[])]);await recalc(env,target)}
 else if(action==='set_delivery_xp'){if(!POWER.has(actor.role))throw new HttpError(403,'forbidden');const id=Math.trunc(Number(b.delivery_id)),xp=Math.trunc(Number(b.delivery_xp));if(!Number.isFinite(xp)||xp<0||xp>100000)throw new HttpError(400,'invalid_xp');const r=await env.DB.prepare('UPDATE deliveries SET xp=? WHERE id=? AND user=?').bind(xp,id,target).run();if(!r.meta?.changes)throw new HttpError(404,'delivery_not_found');await recalc(env,target)}else throw new HttpError(400,'invalid_action');await audit(env,actor.user,action,target,{role:b.role,delivery_id:b.delivery_id});return json(req,{ok:true,action,target})}


// GAT_RADIO_V159 - radio controlada pelo site e reproduzida somente no GAT Telemetria.
// GAT_RADIO_STREAM_V162 - YouTube (video/playlist) + radio online por URL direta MP3/AAC.
function gatRadioValidPlaylistId(id){return /^[A-Za-z0-9_-]{10,120}$/.test(String(id||''))}
function gatRadioValidVideoId(id){return /^[A-Za-z0-9_-]{11}$/.test(String(id||''))}
function gatRadioLooksPlaylistId(id){const s=String(id||'').toUpperCase();return /^(PL|UU|LL|FL|OL|RD)/.test(s)}
function gatRadioParse(value){
 const raw=String(value||'').trim();
 if(!raw)return{type:'',id:'',url:''};
 if(gatRadioValidVideoId(raw))return{type:'video',id:raw,url:`https://www.youtube.com/watch?v=${raw}`};
 if(gatRadioValidPlaylistId(raw)&&gatRadioLooksPlaylistId(raw))return{type:'playlist',id:raw,url:`https://www.youtube.com/playlist?list=${raw}`};
 let candidate=raw;
 if(/^(www\.|youtube\.com|youtu\.be)/i.test(candidate))candidate='https://'+candidate;
 try{
  const x=new URL(candidate);
  if(!['http:','https:'].includes(x.protocol))return{type:'',id:'',url:''};
  if(x.username||x.password)return{type:'',id:'',url:''};
  const host=x.hostname.toLowerCase().replace(/^www\./,'');
  const isYoutube=['youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(host);
  if(isYoutube){
   const path=x.pathname.replace(/^\/+|\/+$/g,''),list=String(x.searchParams.get('list')||'').trim(),v=String(x.searchParams.get('v')||'').trim();
   // Se um link watch tiver tambem list=, prioriza a playlist completa.
   if(gatRadioValidPlaylistId(list)&&gatRadioLooksPlaylistId(list))return{type:'playlist',id:list,url:`https://www.youtube.com/playlist?list=${list}`};
   if(host==='youtu.be'){
    const id=path.split('/')[0];
    return gatRadioValidVideoId(id)?{type:'video',id,url:`https://www.youtube.com/watch?v=${id}`}:{type:'',id:'',url:''};
   }
   if(path==='watch'&&gatRadioValidVideoId(v))return{type:'video',id:v,url:`https://www.youtube.com/watch?v=${v}`};
   const parts=path.split('/');
   if(['shorts','live','embed'].includes(parts[0])&&gatRadioValidVideoId(parts[1]))return{type:'video',id:parts[1],url:`https://www.youtube.com/watch?v=${parts[1]}`};
   return{type:'',id:'',url:''};
  }
  // A Central nao baixa nem retransmite o audio; apenas distribui a URL aos clientes.
  const url=x.href;
  return{type:'stream',id:url,url};
 }catch{}
 return{type:'',id:'',url:''};
}
function gatRadioPublic(row){
 const parsed=gatRadioParse(row?.playlist_url||'');
 const legacyId=String(row?.playlist_id||'');
 const type=parsed.type||(legacyId?'playlist':'');
 const id=parsed.id||legacyId;
 const url=parsed.url||String(row?.playlist_url||'');
 const provider=type==='stream'?'radio_online':'youtube';
 return{enabled:!!Number(row?.enabled||0),source:provider,source_type:type,source_url:url,stream_url:type==='stream'?url:'',video_id:type==='video'?id:'',playlist_url:type==='playlist'?url:'',playlist_id:type==='playlist'?id:'',label:String(row?.label||'Radio GAT'),revision:Number(row?.revision||0),updated_by:String(row?.updated_by||''),updated_at:String(row?.updated_at||'')};
}
async function gatRadioState(env){
 const row=await env.DB.prepare('SELECT enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at FROM radio_state WHERE id=1').first();
 return gatRadioPublic(row);
}

async function route(req,env){const u=new URL(req.url),p=u.pathname,m=req.method;if(m==='OPTIONS')return new Response(null,{status:204,headers:headers(req)});if(!['GET','POST'].includes(m))throw new HttpError(405,'method_not_allowed');
 if(p==='/health')return json(req,{ok:true,economy_mode:'official',economy_version:'1.0.0',service:'GAT Central Local',agent_version:VERSION,time:now(),mission_min_km:MIN_KM,test_mode:false});
 if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'cloudflare-workers-d1',mission_min_km:MIN_KM,test_mode:false});
 if(p==='/api/public/radio'&&m==='GET')return json(req,{ok:true,radio:await gatRadioState(env)});
 if(p==='/api/client/server-info')return json(req,{ok:true,online:true,server_name:'GAT CENTRAL CLOUD',session_id:'CLOUD',players:(await live(env)).length,max_players:999});if(p==='/api/client/players')return json(req,{ok:true,players:(await live(env)).map(x=>x.driver)});
 if(p.startsWith('/api/economy/')&&m==='POST'){
   const b=await body(req),session=await requireSession(req,env,b);
   try{return json(req,economyAction(env.DB,session,p.slice('/api/economy/'.length),b))}
   catch(e){if(Number.isInteger(e.status))return json(req,{ok:false,error:e.message},e.status);throw e}
 }
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
     return json(req,{ok:true,driver,account_user:account,updated_at:previous.updated_at,duplicate_packet:true,next_upload_ms:5000});
   const previousCollected=str(prevRaw||{},'gat_collected_at','gatCollectedAt'),previousCollectedMs=Date.parse(previousCollected);
   const previousSampleAt=Number.isFinite(previousCollectedMs)?new Date(previousCollectedMs).toISOString():(prevRaw?previous.updated_at:null);
   const f=flat(driver,account,t,raw),loaded=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&f.mass_kg>0;
   const event=clean(str(raw,'gat_job_event','gatJobEvent'));
   const deliveryDetailsRaw=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},previousDeliveryDetailsRaw=pick(prevRaw||{},'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{};
   const deliveryDetailsPositive=num(deliveryDetailsRaw,'distanceKm','distance_km')>0||num(deliveryDetailsRaw,'revenue')>0||num(deliveryDetailsRaw,'earnedXp','earned_xp')>0;
   const deliveryDetailsChanged=!loaded&&prevRaw&&JSON.stringify(deliveryDetailsRaw)!==JSON.stringify(previousDeliveryDetailsRaw)&&deliveryDetailsPositive;
   const outcome=resolveJobOutcome({event,cancelFlag:!loaded&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled'),cancelWasActive:bool(prevRaw||{},'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled'),eventMatchesTrip:!str(prevRaw||{},'gat_trip_id','gatTripId')||!str(raw,'gat_trip_id','gatTripId')||str(prevRaw,'gat_trip_id','gatTripId')===str(raw,'gat_trip_id','gatTripId'),freshReceipt:!!deliveryDetailsChanged,legacyFallback:false});
   const delivered=outcome.delivered;
   const cancelled=outcome.cancelled;
   const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);
   raw.gat_journal_verified=packetState.journal_verified===true;
   if(packetState.journal_present&&!packetState.journal_verified)raw.gat_journal_invalid=true;
   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);
   const readiness=rankingReadiness(raw);
   const signature=x=>{const q=flat(driver,account,t,x);return JSON.stringify([q.truck_make,q.truck_model,rankingReadiness(x),q.job_latched,q.on_job,q.job_latch_key,q.cargo_id,q.cargo_name,q.source_city,q.destination_city,q.mass_kg,q.gat_map,num(x,'job.plannedDistanceKm','planned_distance_km'),x.cargo_damage_pct,x.truck_engine_damage_pct,x.truck_transmission_damage_pct,x.truck_cabin_damage_pct,x.truck_chassis_damage_pct,x.truck_wheels_damage_pct,x.trailer_damage_pct,deep(x,'game.paused')])};
   if(!packetState.journal_present&&!queuedTimeOK&&!delivered&&!cancelled&&prevRaw&&Date.parse(receivedAt)-Date.parse(previous.updated_at)<15000&&signature(raw)===signature(prevRaw))
     return json(req,{ok:true,driver,account_user:account,updated_at:previous.updated_at,rank_status:readiness,telemetry_deferred:true,next_upload_ms:5000});
   const previousIdle=!!prevRaw&&prevRaw?.game?.connected===true&&!bool(prevRaw,'job_latched','jobLatched')&&!bool(prevRaw,'on_job','onJob','gameplay.onJob');
   const preflightTruckDamageReady=previousIdle&&['truck_engine_damage_pct','truck_transmission_damage_pct','truck_cabin_damage_pct','truck_chassis_damage_pct','truck_wheels_damage_pct'].every(key=>typeof prevRaw[key]==='number'&&Number.isFinite(prevRaw[key])&&prevRaw[key]>=0&&prevRaw[key]<=100);
   let openJourneyState=null,missionEvent=null;
   if(packetState.duplicate){try{missionEvent=packetState.mission_event_json?JSON.parse(packetState.mission_event_json):null}catch{missionEvent=null}}
   else{
     openJourneyState=await prepareOpenJourney(env,account,raw,t,loaded,delivered||cancelled);
     missionEvent=await processMission(env,account,raw,t,previousSampleAt,preflightTruckDamageReady);
   // Se um novo trip_id chegou enquanto a missao anterior ainda estava ativa,
   // o mesmo pacote encerra a antiga e ja abre a nova. Nao esperamos 15 s nem
   // dependemos de um segundo evento do cliente.
   if(loaded&&missionEvent&&['mission_cancelled','delivery_completed','delivery_completed_xp_only'].includes(String(missionEvent.type))){
     const firstEvent=missionEvent,nextEvent=await processMission(env,account,raw,t,previousSampleAt);
     if(nextEvent)missionEvent={...firstEvent,next_mission_event:nextEvent};
   }
     await finishOpenJourney(env,account,raw,t,openJourneyState,missionEvent);
     const checkpointPhase=delivered?'delivered':cancelled?'cancelled':loaded?'progress':'idle';
     await persistClientPacket(env,account,device,raw,t,packetState,missionEvent,openJourneyState,checkpointPhase);
   }
   if(missionEvent&&missionEvent.type&&!['mission_in_progress','mission_waiting'].includes(String(missionEvent.type))){invalidateRead('profile:'+account);invalidateRead('get:/api/public/work/catalog:'+account);if(String(missionEvent.type).startsWith('delivery_completed')){invalidateRead('get:/api/public/ranking:current');invalidateRead('get:/api/public/safety-ranking:')}}
   await env.DB.prepare('INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json) VALUES(?,?,?,?,?) ON CONFLICT(driver) DO UPDATE SET account_user=excluded.account_user,device_id=excluded.device_id,updated_at=excluded.updated_at,telemetry_json=excluded.telemetry_json').bind(driver,account,device,receivedAt,JSON.stringify(raw)).run();
   return json(req,{ok:true,driver,account_user:account,updated_at:receivedAt,collected_at:t,replayed:queuedTimeOK,rank_status:readiness,mission_event:missionEvent,next_upload_ms:5000});
 }

 if(p==='/api/public/account-live'&&m==='GET')return json(req,{ok:true,telemetry:await live(env),updated_at:now()});
 if(p==='/api/public/ranking-period'&&m==='GET'){
   const data=await rankingPeriod(env.DB,u.searchParams,month());
   return json(req,data,data.ok?200:400);
 }
 if(p==='/api/public/ranking'&&m==='GET'){
  const currentMonth=month(),requested=String(u.searchParams.get('month')||'').trim(),season=requested||currentMonth;
  if(!/^\d{4}-(0[1-9]|1[0-2])$/.test(season))throw new HttpError(400,'invalid_month');
  const mode=(await env.DB.prepare("SELECT value FROM meta WHERE key='operation_mode'").first())?.value||'official';
  const r=await env.DB.prepare(`SELECT
    p.user,
    COALESCE(s.monthly_completed,0) AS monthly_completed,
    p.total_deliveries,
    p.xp,
    COALESCE(s.monthly_perfect_trips,0) AS perfect_trips,
    COALESCE(s.monthly_penalty_points,0) AS penalty_xp,
    COALESCE(s.monthly_fines,0) AS speed_fines,
    p.total_km,
    COALESCE(s.monthly_km,0) AS monthly_km,
    COALESCE(s.monthly_fines,0) AS monthly_fines,
    COALESCE(s.monthly_penalty_points,0) AS monthly_penalty_points,
    COALESCE(s.points,0) AS points
   FROM profiles p
   JOIN accounts a ON a.user=p.user
   LEFT JOIN (
     SELECT user,
       COUNT(*) AS monthly_completed,
       COALESCE(SUM(distance_km),0) AS monthly_km,
       COALESCE(SUM(perfect),0) AS monthly_perfect_trips,
       COALESCE(SUM(penalty_xp),0) AS monthly_penalty_points,
       COALESCE(SUM(speed_fines),0) AS monthly_fines,
       COALESCE(SUM(COALESCE(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER),MAX(0,100-penalty_xp))),0) AS points
     FROM deliveries
     WHERE strftime('%Y-%m',delivered_at,'-3 hours')=?
     GROUP BY user
   ) s ON s.user=p.user
   WHERE a.disabled=0
   ORDER BY points DESC,monthly_completed DESC,monthly_km DESC,perfect_trips DESC,penalty_xp ASC,speed_fines ASC,p.user ASC`).bind(season).all();
  const monthsRows=await env.DB.prepare("SELECT DISTINCT strftime('%Y-%m',delivered_at,'-3 hours') AS month_key FROM deliveries WHERE delivered_at IS NOT NULL AND length(delivered_at)>=7 ORDER BY month_key DESC").all();
  const availableMonths=[...new Set([currentMonth,...(monthsRows.results||[]).map(x=>String(x.month_key||'')).filter(x=>/^\d{4}-(0[1-9]|1[0-2])$/.test(x))])];
  return json(req,{ok:true,operation_mode:mode,season,current_month:currentMonth,is_current_month:season===currentMonth,timezone:GAT_TIMEZONE,available_months:availableMonths,scoring:{rules_version:'2026-10-01-v2',starts_at:GAT_SCORING_V2_START_LOCAL,monthly:true,distance_block_km:99,points_per_block:10,weight_bonus:[{max_kg:4999,points:1},{min_kg:5000,max_kg:9999,points:2},{min_kg:10000,max_kg:19999,points:3},{min_kg:20000,max_kg:29999,points:4},{min_kg:30000,max_kg:39999,points:6},{min_kg:40000,points:8}],fine_penalty_points:3,speed_limit_tolerance_kmh:SPEED_LIMIT_TOLERANCE_KMH,speed_rule_source:'posted_road_limit',cargo_damage_penalties:[{max_pct:3,points:3},{max_pct:7,points:5},{max_pct:15,points:10},{over_pct:15,points:15}],vehicle_damage_penalties:[{max_pct:5,points:3},{max_pct:10,points:5},{max_pct:20,points:10},{over_pct:20,points:15}]},ranking:r.results||[]})
 }
 if(p==='/api/public/safety-ranking'&&m==='GET'){const r=await env.DB.prepare('SELECT p.user,p.safety_score AS score,p.perfect_trips,p.speed_fines,p.penalty_xp FROM profiles p JOIN accounts a ON a.user=p.user WHERE a.disabled=0 ORDER BY p.safety_score DESC,p.perfect_trips DESC,p.speed_fines ASC,p.user ASC').all();return json(req,{ok:true,ranking:r.results||[]})}
 if(p==='/api/public/driver'&&m==='GET'){const d=await profile(env,clean(u.searchParams.get('user')));if(!d)throw new HttpError(404,'not_found');return json(req,{ok:true,profile:d})}
 if(p==='/api/site/profile'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);let pr=await profile(env,s.user);if(!pr){await ensureProfile(env,s.user);pr=await readProfile(env,s.user)}return json(req,{ok:true,profile:pr})}
 if(p==='/api/site/profile/avatar'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);const avatar=String(b.avatar_url||'').trim();if(avatar){if(avatar.length>220000)throw new HttpError(413,'avatar_too_large');if(!/^data:image\/(?:webp|png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(avatar))throw new HttpError(400,'invalid_avatar')}await env.DB.prepare('UPDATE profiles SET avatar_url=?,updated_at=? WHERE user=?').bind(avatar||null,now(),s.user).run();await audit(env,s.user,avatar?'profile_avatar_update':'profile_avatar_remove',s.user,{bytes:avatar.length});return json(req,{ok:true,avatar_url:avatar})}
 if(p==='/api/public/work/catalog'&&m==='GET'){const user=clean(u.searchParams.get('user')),c=await env.DB.prepare('SELECT id,position,title,category,icon,custom,compatible_cargos_json FROM work_catalog WHERE active=1 ORDER BY position').all(),d=await env.DB.prepare('SELECT DISTINCT work_id FROM work_completed WHERE user=?').bind(user).all(),done=new Set((d.results||[]).map(x=>x.work_id));return json(req,{ok:true,catalog:(c.results||[]).map(x=>{let compatible=[];try{compatible=JSON.parse(x.compatible_cargos_json||'[]')}catch{}const{compatible_cargos_json,...item}=x;return{...item,custom:!!x.custom,compatible_cargos:compatible,completed:done.has(x.id)}}),mission_min_km:MIN_KM})}
 if((p==='/api/site/work/select'||p==='/api/site/work/take')&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);const pr=await env.DB.prepare('SELECT current_mission_json,monthly_completed FROM profiles WHERE user=?').bind(s.user).first();if(pr?.current_mission_json)throw new HttpError(409,'mission_already_active');const item=p.endsWith('/take')&&!b.work_id?await env.DB.prepare('SELECT wc.* FROM work_catalog wc WHERE wc.active=1 AND NOT EXISTS(SELECT 1 FROM work_completed d WHERE d.user=? AND d.work_id=wc.id AND d.month_key=?) ORDER BY wc.position LIMIT 1').bind(s.user,month()).first():await env.DB.prepare('SELECT * FROM work_catalog WHERE id=? AND active=1').bind(String(b.work_id||'')).first();if(!item)throw new HttpError(404,'invalid_work');const repeatXpOnly=!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first());const custom=String(b.custom_cargo||'').trim();if(item.custom&&(custom.length<2||custom.length>100))throw new HttpError(400,'custom_cargo_required');const t=now(),mission={id:`${month()}-${s.user}-${item.id}-${randomHex(6)}`,catalog_id:item.id,sequence:item.position,title:item.title,category:item.category,custom_cargo:custom,xp_only:false,state:'assigned',min_km:MIN_KM,created_at:t,assigned_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(mission),t,s.user).run();return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),xp_only:false,rules_enabled:true,admin_test_mode:false,operation_mode:'official'})}
 if(p==='/api/public/notice'&&m==='GET'){const rows=await env.DB.prepare("SELECT key,value FROM meta WHERE key IN ('site_notice_enabled','site_notice_title','site_notice_message','site_notice_updated_at')").all(),v=Object.fromEntries((rows.results||[]).map(x=>[x.key,x.value]));return json(req,{ok:true,enabled:v.site_notice_enabled==='1',title:v.site_notice_title||'AVISO GAT',message:v.site_notice_message||'',updated_at:v.site_notice_updated_at||null})}
 if(p==='/api/site/admin/notice'&&m==='POST'){const b=await body(req),a=await requireAdmin(req,env,b);if(b.save){const title=String(b.title||'').trim().slice(0,80),message=String(b.message||'').trim().slice(0,600),enabled=b.enabled?1:0;if(enabled&&!message)throw new HttpError(400,'notice_message_required');const t=now();await env.DB.batch([env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_enabled',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(String(enabled)),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_title',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(title||'AVISO GAT'),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_message',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(message),env.DB.prepare("INSERT INTO meta(key,value) VALUES('site_notice_updated_at',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value").bind(t)]);await audit(env,a.user,'site_notice',a.user,{enabled:!!enabled,title:title||'AVISO GAT'});return json(req,{ok:true,enabled:!!enabled,title:title||'AVISO GAT',message,updated_at:t})}const rows=await env.DB.prepare("SELECT key,value FROM meta WHERE key IN ('site_notice_enabled','site_notice_title','site_notice_message','site_notice_updated_at')").all(),v=Object.fromEntries((rows.results||[]).map(x=>[x.key,x.value]));return json(req,{ok:true,enabled:v.site_notice_enabled==='1',title:v.site_notice_title||'AVISO GAT',message:v.site_notice_message||'',updated_at:v.site_notice_updated_at||null})}
 if(p==='/api/site/admin/radio'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),current=await gatRadioState(env);
  const rawUrl=String(b.source_url!==undefined?b.source_url:(b.playlist_url!==undefined?b.playlist_url:(current.source_url||current.playlist_url||''))).trim();
  const parsed=gatRadioParse(rawUrl);
  const enabled=b.enabled!==undefined?(b.enabled===true||b.enabled===1||String(b.enabled).toLowerCase()==='true'):current.enabled;
  if(rawUrl&&!parsed.id)throw new HttpError(400,'invalid_media_url');
  if(enabled&&!parsed.id)throw new HttpError(400,'source_required');
  const sourceUrl=parsed.id?parsed.url:'',sourceId=parsed.id||'',label=String(b.label||current.label||'Radio GAT').trim().slice(0,80)||'Radio GAT',t=now();
  await env.DB.prepare('INSERT INTO radio_state(id,enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at) VALUES(1,?,?,?,?,1,?,?) ON CONFLICT(id) DO UPDATE SET enabled=excluded.enabled,playlist_url=excluded.playlist_url,playlist_id=excluded.playlist_id,label=excluded.label,revision=radio_state.revision+1,updated_by=excluded.updated_by,updated_at=excluded.updated_at').bind(enabled?1:0,sourceUrl,sourceId,label,s.user,t).run();
  await audit(env,s.user,'radio_update','radio',{enabled:!!enabled,source_type:parsed.type,source_id:sourceId,label});
  return json(req,{ok:true,radio:await gatRadioState(env)});
 }
 if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}
 if(p==='/api/site/admin/drivers'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b),r=await env.DB.prepare('SELECT a.user,a.role,a.disabled,a.created_at,p.monthly_completed,p.total_deliveries,p.xp,p.total_km,p.current_mission_json,t.driver,t.updated_at last_telemetry_at,t.telemetry_json FROM accounts a LEFT JOIN profiles p ON p.user=a.user LEFT JOIN telemetry_live t ON t.account_user=a.user ORDER BY a.user').all(),drivers=(r.results||[]).map(x=>{let mission=null,raw={};try{mission=x.current_mission_json?JSON.parse(x.current_mission_json):null}catch{}try{raw=x.telemetry_json?JSON.parse(x.telemetry_json):{}}catch{}const f=flat(x.driver||x.user,x.user,x.last_telemetry_at||'',raw);return{user:x.user,role:x.role,disabled:!!x.disabled,created_at:x.created_at,monthly_completed:Number(x.monthly_completed||0),total_deliveries:Number(x.total_deliveries||0),xp:Number(x.xp||0),level:level(x.xp),total_km:Number(x.total_km||0),current_mission:mission,last_telemetry_at:x.last_telemetry_at,online:!!x.last_telemetry_at&&Date.now()-Date.parse(x.last_telemetry_at)<30000,truck:[f.truck_make,f.truck_model].filter(Boolean).join(' '),cargo:f.cargo_name}});return json(req,{ok:true,viewer_role:s.role,drivers})}
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
 const scope=url.pathname==='/api/public/work/catalog'?user:(url.pathname==='/api/public/ranking'?(String(url.searchParams.get('month')||'current')):'');
 const data=await cachedRead('get:'+url.pathname+':'+scope,ttl,async()=>{
   const response=await route(req,env);
   if(!response.ok)throw new HttpError(response.status,'read_failed');
   return response.json();
 });
 return json(req,data);
}










