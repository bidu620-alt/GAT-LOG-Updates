import {readFileSync,writeFileSync,renameSync} from 'node:fs';

export const EVENT_ID='iceland_2026';
export const PRODUCT_ID='event_iceland_2026';
export const CITIES=[
 ['akureyri','Akureyri',[]],['blonduos','Blönduós',[]],
 ['borgarnes','Borgarnes',[]],['egilsstadir','Egilsstaðir',[]],
 ['hofn','Höfn í Hornafirði',['Höfn','Hofn i Hornafirdi']],
 ['holmavik','Hólmavík',[]],['husavik','Húsavík',[]],
 ['isafjordur','Ísafjörður',['Isafjordur']],['keflavik','Keflavík',[]],
 ['olafsvik','Ólafsvík',[]],['patreksfjordur','Patreksfjörður',[]],
 ['reykjavik','Reiquiavique',['Reykjavík','Reykjavik']],
 ['selfoss','Selfoss',[]],['thorshofn','Thórshöfn',['Þórshöfn','Thorshofn']]
].map(([id,label,aliases])=>({id,label,aliases:[id,label,...aliases]}));
const norm=v=>String(v||'').toLowerCase().replace(/ð/g,'d').replace(/þ/g,'th').replace(/æ/g,'ae').replace(/ø/g,'o').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/[^a-z0-9]/g,'');
const cityIndex=new Map(CITIES.flatMap(c=>c.aliases.map(a=>[norm(a),c.id])));
const parse=v=>{try{return typeof v==='object'&&v?v:JSON.parse(v||'{}')}catch{return {}}};
export function readIcelandConfig(){
 try{
  const file=new URL('./iceland-event.json',import.meta.url),c=JSON.parse(readFileSync(file,'utf8'));
  // Freeze the launch on the first live Central access, never at draft creation.
  if(c.active===true&&c.starts_at===null){
   c.starts_at=new Date().toISOString();
   const temp=new URL('./iceland-event.json.tmp',import.meta.url);
   writeFileSync(temp,JSON.stringify(c,null,2)+'\n','utf8');renameSync(temp,file);
  }
  return c;
 }catch{return {active:false,starts_at:null}}
}
export function validConfig(c){return c?.active===true&&typeof c.starts_at==='string'&&/(Z|[+-]\d\d:\d\d)$/.test(c.starts_at)&&Number.isFinite(Date.parse(c.starts_at))}
export function icelandProducts(config=readIcelandConfig()){
 if(!validConfig(config))return [];
 return [{id:PRODUCT_ID,kind:'event',title:'Islândia — Terra de Fogo e Gelo',
 description:'Compra única. Complete entregas válidas de pelo menos 200 km envolvendo as 14 cidades da Islândia após a compra. Conquista permanente no perfil, sem XP/Gold extra. Requer o mapa da Islândia no ETS2. Não concede recompensas do World of Trucks.',
 gold_units:500000,cash_centavos:300,bcoin_units:0,starts_at:config.starts_at}];
}
export function eligibleCities(row,{starts_at,acquired_at,at=new Date().toISOString()}={}){
 const t=Date.parse(row?.delivered_at||''),start=Date.parse(starts_at||''),acquired=Date.parse(acquired_at||''),upper=Date.parse(at);
 if(![t,start,acquired,upper].every(Number.isFinite)||t<Math.max(start,acquired)||t>upper||Number(row?.distance_km)<200||!Number.isFinite(Number(row?.distance_km)))return [];
 const raw=parse(row.raw_json),mission=raw.mission||{};
 if(raw.audit?.event_eligible===false)return [];
 if([row.completed,row.delivered,raw.completed,raw.delivered].some(v=>v===false||v===0))return [];
 if([row.state,row.status,raw.state,raw.status,mission.state].some(v=>['cancelled','canceled','rejected','failed','pending','assigned'].includes(String(v||'').toLowerCase())))return [];
 // A delivered DB row retains the mission's pre-completion active/suspended snapshot.
 if([row.state,row.status].some(v=>['active','suspended'].includes(String(v||'').toLowerCase())))return [];
 if(String(raw.map_mode||mission.map_mode||'base').toLowerCase()!=='base')return [];
 const game=norm(mission.game_name||raw.game_name||raw.gat_game);
 if(game&&!['ets2','eurotrucksimulator2','eurotrucksimulator'].includes(game))return [];
 const src=cityIndex.get(norm(row.source)),dst=cityIndex.get(norm(row.destination));
 // Prefer explicit localized city names; SDK city IDs are a fallback, never substring matches.
 const a=src||cityIndex.get(norm(mission.source_city_id)),b=dst||cityIndex.get(norm(mission.destination_city_id));
 return [...new Set([a,b].filter(Boolean))];
}
function ensure(db){db.sql.exec(`CREATE TABLE IF NOT EXISTS bidulog_event_cities(
 event_id TEXT NOT NULL,user TEXT NOT NULL,city_id TEXT NOT NULL,delivery_id INTEGER NOT NULL,completed_at TEXT NOT NULL,
 PRIMARY KEY(event_id,user,city_id));`)}
export function icelandState(db,user,config=readIcelandConfig(),at=new Date().toISOString()){
 const event={id:EVENT_ID,title:'Islândia — Terra de Fogo e Gelo',goal:14,min_km:200,starts_at:config.starts_at||null,
 active:validConfig(config),price_gold:5000,price_brl:3,permanent:true,reward:'Conquista permanente no perfil',market:'Entregas válidas registradas pela Central',cities:CITIES.map(({id,label})=>({id,label}))};
 if(!event.active)return {ok:true,event,owned:false,progress:{count:0,goal:14,completed:false,cities:[]}};
 ensure(db);
 const hasEconomy=db.sql.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name='eco_entitlements'").get();
 const entitlement=hasEconomy?db.sql.prepare("SELECT acquired_at FROM eco_entitlements e WHERE user=? AND product_id=? AND (source<>'streamer-benefit' OR EXISTS(SELECT 1 FROM accounts a WHERE a.user=e.user AND a.role='streamer' AND a.disabled=0))").get(user,PRODUCT_ID):null;
 if(entitlement&&Date.parse(at)>=Date.parse(config.starts_at)){
  // Apply time boundaries before scanning. Never use the profile's latest 100 rows.
  const rows=db.sql.prepare(`SELECT id,source,destination,distance_km,delivered_at,raw_json FROM deliveries
    WHERE user=? AND julianday(delivered_at)>=julianday(?) AND julianday(delivered_at)>=julianday(?)
    AND julianday(delivered_at)<=julianday(?) AND distance_km>=200 ORDER BY julianday(delivered_at),id`)
   .all(user,config.starts_at,entitlement.acquired_at,at);
  db.sql.exec('SAVEPOINT iceland_progress');
  try{
   const insert=db.sql.prepare('INSERT OR IGNORE INTO bidulog_event_cities VALUES(?,?,?,?,?)');
   for(const row of rows)for(const city of eligibleCities(row,{starts_at:config.starts_at,acquired_at:entitlement.acquired_at,at}))
    insert.run(EVENT_ID,user,city,row.id,row.delivered_at);
   const saved=db.sql.prepare('SELECT city_id,completed_at FROM bidulog_event_cities WHERE event_id=? AND user=?').all(EVENT_ID,user);
   if(CITIES.every(c=>saved.some(x=>x.city_id===c.id))){
    const completedAt=saved.reduce((latest,c)=>Date.parse(c.completed_at)>Date.parse(latest)?c.completed_at:latest,saved[0].completed_at);
    db.sql.prepare('INSERT OR IGNORE INTO meta(key,value) VALUES(?,?)').run(`achievement:${user}:${EVENT_ID}`,JSON.stringify({id:EVENT_ID,title:'Desbravador da Islândia',description:'Concluiu as 14 cidades da Islândia no evento BiduLog.',kind:'event',eventId:EVENT_ID,medal:'🇮🇸',xp:0,progress:14,goal:14,completedAt:Date.parse(completedAt),unlocked:true,unlocked_at:completedAt}));
   }
   db.sql.exec('RELEASE iceland_progress');
  }catch(e){db.sql.exec('ROLLBACK TO iceland_progress; RELEASE iceland_progress');throw e;}
 }
 const saved=db.sql.prepare('SELECT city_id,delivery_id,completed_at FROM bidulog_event_cities WHERE event_id=? AND user=?').all(EVENT_ID,user);
 const cities=CITIES.map(c=>{const s=saved.find(x=>x.city_id===c.id);return {id:c.id,label:c.label,completed:!!s,delivery_id:s?.delivery_id||null,completed_at:s?.completed_at||null}});
 const count=cities.filter(c=>c.completed).length;
 return {ok:true,event,owned:!!entitlement,acquired_at:entitlement?.acquired_at||null,progress:{count,goal:14,completed:count===14,cities}};
}
