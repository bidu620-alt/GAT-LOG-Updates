import {MONTHLY_PASS,monthlyPassConfig,PASS_CARGO_IDS} from './monthly-pass.js';

const SCHEMA=`CREATE TABLE IF NOT EXISTS pass_tier_seasons (
 id TEXT PRIMARY KEY, product_id TEXT NOT NULL UNIQUE, rules_json TEXT NOT NULL, registered_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS pass_tier_completions (
 user TEXT NOT NULL REFERENCES accounts(user) ON DELETE CASCADE,
 season_id TEXT NOT NULL REFERENCES pass_tier_seasons(id),
 completed_at TEXT NOT NULL, recorded_at TEXT NOT NULL, evidence_json TEXT NOT NULL,
 PRIMARY KEY(user,season_id)
);`;
const parse=value=>{try{return JSON.parse(value||'{}')||{}}catch{return {}}};
const cargoId=raw=>String(raw?.cargo_identity?.cargo_id_raw||raw?.cargo_identity?.cargo_id||raw?.mission?.cargo_id||raw?.cargo_id||'').trim().toLowerCase().replace(/^cargo\./,'');
const complete=raw=>![raw?.state,raw?.status,raw?.mission?.state].some(v=>['cancelled','canceled','rejected','failed','assigned','pending'].includes(String(v||'').trim().toLowerCase()))&&raw?.completed!==false&&raw?.delivered!==false;

export function currentTierSeason(){
 const pass=monthlyPassConfig();
 return {id:MONTHLY_PASS.id,product_id:MONTHLY_PASS.product_id,title:pass.title,starts_at:pass.starts_at,ends_at:pass.ends_at,
  goals:pass.goals.map(g=>({...g})),cargo_ids:Object.fromEntries(Object.entries(PASS_CARGO_IDS).map(([id,ids])=>[id,[...ids]]))};
}

export function registerTierSeason(db,season,at=Date.now()){
 if(!season?.id||!season.product_id||!Number.isFinite(Date.parse(season.starts_at))||!Number.isFinite(Date.parse(season.ends_at))||Date.parse(season.ends_at)<=Date.parse(season.starts_at)||!Array.isArray(season.goals)||!season.goals.length)throw Error('Invalid pass season');
 if(new Set(season.goals.map(g=>g.id)).size!==season.goals.length||season.goals.some(g=>!g.id||!Number.isFinite(g.target)||g.target<=0))throw Error('Invalid pass goals');
 db.sql.exec(SCHEMA);
 // Keep the published time window, goals and catalog selections unchanged forever.
 db.sql.prepare('INSERT OR IGNORE INTO pass_tier_seasons(id,product_id,rules_json,registered_at) VALUES(?,?,?,?)').run(season.id,season.product_id,JSON.stringify(season),new Date(at).toISOString());
}

export function findPassCompletion(db,user,season,at=Date.now()){
 if(!Number.isFinite(at)||at<Date.parse(season.starts_at))return null;
 if(!db.sql.prepare('SELECT 1 FROM eco_entitlements WHERE user=? AND product_id=?').get(user,season.product_id))return null;
 const values=Object.fromEntries(season.goals.map(g=>[g.id,0])),categories=new Map();
 for(const [category,ids] of Object.entries(season.cargo_ids||{}))for(const id of ids){if(categories.has(id))throw Error('Ambiguous saved cargo category');categories.set(id,category);}
 const rows=db.sql.prepare(`SELECT id,weight_kg,distance_km,delivered_at,raw_json FROM deliveries WHERE user=?
  AND julianday(delivered_at)>=julianday(?) AND julianday(delivered_at)<julianday(?)
  AND julianday(delivered_at)<=julianday(?) ORDER BY julianday(delivered_at),id`).all(user,season.starts_at,season.ends_at,new Date(at).toISOString());
 let totalKm=0,count=0;
 for(const row of rows){
  const raw=parse(row.raw_json),distance=Number(row.distance_km);
  if(raw.audit?.event_eligible===false&&!season.transport_types?.includes("passengers"))continue;
  if(!complete(raw)||!Number.isFinite(distance)||distance<=0)continue;
  count++;totalKm+=distance;
  const category=categories.get(cargoId(raw));
  for(const goal of season.goals){
   if(goal.unit==='km')values[goal.id]=Math.round(totalKm*100)/100;
   else if(goal.weight_over_kg!==undefined){if(Number(row.weight_kg)>goal.weight_over_kg)values[goal.id]++;}
   else if(goal.id===category)values[goal.id]++;
  }
  if(season.goals.every(g=>values[g.id]>=g.target))return {
   completed_at:new Date(row.delivered_at).toISOString(),delivery_id:row.id,total_deliveries:count,total_km:Math.round(totalKm*100)/100,
   goals:season.goals.map(g=>({id:g.id,target:g.target,current:Math.min(g.target,values[g.id])}))
  };
 }
 return null;
}

export function readPassTier(db,user){
 const exists=db.sql.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name='pass_tier_completions'").get();
 const seasons=exists?db.sql.prepare('SELECT season_id,completed_at FROM pass_tier_completions WHERE user=? ORDER BY completed_at,season_id').all(user):[];
 return {tier:seasons.length,completed_passes:seasons.length,label:seasons.length?'Tier '+seasons.length:null,
  latest_completed_at:seasons.at(-1)?.completed_at||null,seasons};
}

export function syncPassTiers(db,user,at=Date.now()){
 registerTierSeason(db,currentTierSeason(),at);
 const seasons=db.sql.prepare('SELECT rules_json FROM pass_tier_seasons ORDER BY id').all();
 for(const row of seasons){
  const season=JSON.parse(row.rules_json);
  if(db.sql.prepare('SELECT 1 FROM pass_tier_completions WHERE user=? AND season_id=?').get(user,season.id))continue;
  const evidence=findPassCompletion(db,user,season,at);if(!evidence)continue;
  // Exactly one permanent record per driver and distinct pass. No XP or Gold mutations.
  db.sql.prepare('INSERT OR IGNORE INTO pass_tier_completions(user,season_id,completed_at,recorded_at,evidence_json) VALUES(?,?,?,?,?)')
   .run(user,season.id,evidence.completed_at,new Date(at).toISOString(),JSON.stringify(evidence));
 }
 return readPassTier(db,user);
}
