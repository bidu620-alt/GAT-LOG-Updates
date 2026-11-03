import {monthlyPassConfig,monthlyPassState} from './monthly-pass.js';
import {classifyCargoId} from './cargo-library/index.js';

const fail=(status,code)=>{throw Object.assign(new Error(code),{status})};
const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
const parse=v=>{try{return JSON.parse(v||'{}')||{}}catch{return {}}};
const scalar=v=>['string','number','boolean'].includes(typeof v)?v:null;
function auditView(raw){const a=raw.audit||{};return Object.fromEntries(Object.entries(a).filter(([k,v])=>/^(gat_|base_xp$|speed_|cargo_damage|truck_damage|trailer_damage|cargo_penalty|truck_penalty|perfect_|penalty_xp$|xp_awarded$|ranking_|rank_)/.test(k)&&scalar(v)!==null));}
function delivery(row,details=false){
 const raw=parse(row.raw_json),id=String(raw?.cargo_identity?.cargo_id_raw||raw?.cargo_identity?.cargo_id||raw?.mission?.cargo_id||raw?.cargo_id||'').toLowerCase().replace(/^cargo\./,'');
 const state=String(raw.state||raw.status||raw.mission?.state||'completed').toLowerCase();
 const valid=raw.completed!==false&&raw.delivered!==false&&![raw.state,raw.status,raw.mission?.state].some(s=>['cancelled','canceled','rejected','failed','assigned','pending'].includes(String(s||'').toLowerCase()));
 const {raw_json,...safe}=row;
 return {...safe,completed_at:row.delivered_at,completed:valid,delivered:valid,state,cargo_id:id,category:classifyCargoId(id),...(details?{audit:auditView(raw),receipt:Object.fromEntries(Object.entries(raw.delivery_details||{}).filter(([k,v])=>['revenue','earnedXp','cargoDamage','distanceKm','deliveryTime','autoParked','autoLoaded'].includes(k)&&scalar(v)!==null))}:{})};
}
function filters(b){
 const where=[],args=[];
 const user=clean(b.target);if(user){where.push('d.user=?');args.push(user);}
 const q=String(b.search||'').trim().slice(0,100);if(q){where.push('(d.user LIKE ? OR d.cargo LIKE ? OR d.source LIKE ? OR d.destination LIKE ? OR CAST(d.id AS TEXT)=?)');const like='%'+q+'%';args.push(like,like,like,like,q);}
 for(const [key,operator] of [['from','>='],['to','<']])if(b[key]){if(!/^\d{4}-\d{2}-\d{2}$/.test(b[key])||!Number.isFinite(Date.parse(b[key])))fail(400,'invalid_date');const date=key==='to'?new Date(Date.parse(b[key]+'T00:00:00-03:00')+86400000).toISOString():b[key]+'T00:00:00-03:00';where.push(`julianday(d.delivered_at)${operator}julianday(?)`);args.push(date);}
 return {where:where.length?' WHERE '+where.join(' AND '):'',args};
}
// Called only AFTER requireAdmin in worker.js. These are read-only queries:
// no schema migrations, account changes, reward claims or progress recalculation.
export function adminWorkspace(db,actor,resource,b={}){
 if(!actor||!['owner','admin','moderator'].includes(actor.role))fail(403,'forbidden');
 if(resource==='passes'){
  const config=monthlyPassConfig(),target=clean(b.target);
  const users=db.sql.prepare('SELECT user,disabled FROM accounts'+(target?' WHERE user=?':'')+' ORDER BY user').all(...(target?[target]:[]));
  return {ok:true,pass:config,participants:users.map(u=>({user:u.user,disabled:!!u.disabled,...monthlyPassState(db,{user:u.user})})),rewards_status:config.rewards_defined?'unavailable':'not_defined'};
 }
 if(resource==='deliveries'){
  const page=Math.max(1,Math.trunc(Number(b.page)||1)),size=25,{where,args}=filters(b);
  const total=db.sql.prepare('SELECT COUNT(*) total FROM deliveries d'+where).get(...args).total;
  const rows=db.sql.prepare('SELECT d.id,d.user,d.sequence_no AS sequence,d.source,d.destination,d.cargo,d.weight_kg,d.distance_km,d.xp,d.perfect,d.penalty_xp,d.speed_fines,d.delivered_at,d.raw_json FROM deliveries d'+where+' ORDER BY d.id DESC LIMIT ? OFFSET ?').all(...args,size,(page-1)*size);
  return {ok:true,page,page_size:size,total,deliveries:rows.map(r=>delivery(r))};
 }
 if(resource==='delivery'){
  const id=Number(b.id);if(!Number.isSafeInteger(id)||id<=0)fail(400,'invalid_delivery_id');
  const row=db.sql.prepare('SELECT id,user,sequence_no AS sequence,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json FROM deliveries WHERE id=?').get(id);if(!row)fail(404,'delivery_not_found');
  const d=delivery(row,true),state=monthlyPassState(db,{user:row.user}),config=state.pass,t=Date.parse(row.delivered_at),inWindow=t>=Date.parse(config.starts_at)&&t<Date.parse(config.ends_at)&&t<=Date.now();
  const eligible=state.owned&&inWindow&&d.completed&&Number(d.distance_km)>0;
  const reason=!state.owned?'Motorista sem este passe':!inWindow?'Entrega fora do período da temporada':!d.completed?'Entrega não concluída ou cancelada':!(Number(d.distance_km)>0)?'Distância inválida':!d.category?'Conta para rodagem e, quando aplicável, peso pesado; ID sem categoria no passe':'Conta para rodagem e para a categoria até o limite da meta';
  return {ok:true,delivery:d,pass_validation:{eligible,reason,category:d.category,heavy:eligible&&Number(d.weight_kg)>40000,pass:config.title}};
 }
 if(resource==='event-history'){
  const target=clean(b.target),where=[],args=[];
  if(target){where.push('user=?');args.push(target);}
  // Filter temporal window BEFORE reading the complete history. No recent-trip cap.
  if(b.start){if(!Number.isFinite(Date.parse(b.start)))fail(400,'invalid_start');where.push('julianday(delivered_at)>=julianday(?)');args.push(b.start);}
  if(b.end){if(!Number.isFinite(Date.parse(b.end)))fail(400,'invalid_end');where.push('julianday(delivered_at)<julianday(?)');args.push(b.end);}
  const rows=db.sql.prepare('SELECT id,user,sequence_no AS sequence,source,destination,cargo,weight_kg,distance_km,delivered_at,raw_json FROM deliveries'+(where.length?' WHERE '+where.join(' AND '):'')+' ORDER BY delivered_at,id').all(...args);
  return {ok:true,deliveries:rows.map(r=>delivery(r)),complete_history:true};
 }
 if(resource==='companies'){
  const companies=db.sql.prepare('SELECT c.id,c.name,c.tag,c.description,c.owner_user,c.created_at,COUNT(m.user) member_count FROM eco_companies c LEFT JOIN eco_company_members m ON m.company_id=c.id GROUP BY c.id ORDER BY c.name').all();
  const members=db.sql.prepare('SELECT user,company_id,role,joined_at FROM eco_company_members ORDER BY user').all();
  return {ok:true,companies,members};
 }
 fail(404,'not_found');
}

