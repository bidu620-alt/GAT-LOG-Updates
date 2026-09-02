"""Aplica o fluxo automatico de classificacao de cargas somente na Central local."""
from pathlib import Path
import sys

path = Path(sys.argv[1])
worker = path.read_text(encoding='utf-8')

helper_anchor = next((x for x in ["async function processMission(env,user,raw,t,previousAt){","async function processMission(env,user,raw,t){"] if x in worker), None)
if not helper_anchor:
    raise RuntimeError('Nao encontrei processMission para instalar classificacao automatica.')

helpers = r'''
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
 {cargo:/\b(trator|tractor|escavadeira|excavator|bulldozer|dozer|locomotiva|locomotive|guindaste|crane|carregadeira|loader|colheitadeira|harvester)\b/,target:/(pesad|maquin|equip)/,score:.88},
 {cargo:/\b(tora|toras|madeira|timber|logs?|lumber)\b/,target:/(madeir|tora|florest|timber)/,score:.90},
 {cargo:/\b(tijolo|brick|cimento|cement|concreto|concrete|material de construcao|construction material)\b/,target:/(construc|material)/,score:.88},
 {cargo:/\b(container|conteiner|cont[eê]iner)\b/,target:/(container|conteiner)/,score:.92},
 {cargo:/\b(carro|carros|automovel|automoveis|cars?|motocicleta|motorcycle|veiculo|vehicle)\b/,target:/(veicul|automov|carro)/,score:.86},
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
 const alias=await env.DB.prepare('SELECT ca.work_id,ca.confidence,wc.id,wc.position,wc.title,wc.category,wc.icon,wc.compatible_cargos_json FROM cargo_aliases ca JOIN work_catalog wc ON wc.id=ca.work_id WHERE ca.cargo_key=? AND wc.active=1').bind(key).first();
 if(alias)return{work:alias,confidence:Math.max(.99,Number(alias.confidence)||0),suggested_work_id:alias.id,source:'learned'};
 const rows=await env.DB.prepare('SELECT id,position,title,category,icon,compatible_cargos_json FROM work_catalog WHERE active=1 ORDER BY position').all(),ranked=(rows.results||[]).map(item=>({item,score:catalogCargoScore(item,cargo)})).sort((a,b)=>b.score-a.score||Number(a.item.position)-Number(b.item.position));
 const first=ranked[0]||{item:null,score:0},second=ranked[1]||{score:0};
 const safe=first.item&&first.score>.50&&(first.score>=.85||first.score-second.score>=.12);
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
'''
worker = worker.replace(helper_anchor, helpers + '\n' + helper_anchor, 1)

opening_old = "const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();if(!row?.current_mission_json)return null;let m;try{m=JSON.parse(row.current_mission_json)}catch{return null}\n const adminTest=clean(user)==='biduzao';"
opening_new = """const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;if(row?.current_mission_json){try{m=JSON.parse(row.current_mission_json)}catch{m=null}}\n const adminTest=clean(user)==='biduzao';\n const observed=flat(user,user,t,raw);\n if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){\n  const classification=await autoClassifyCargo(env,observed.cargo_name),item=classification.work,already=item?!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,item.id,month(t)).first()):false;\n  m={id:`${month(t)}-${user}-${item?.id||'unclassified'}-${randomHex(6)}`,catalog_id:item?.id||'__unclassified__',sequence:item?.position||null,title:item?.title||'Carga a classificar',category:item?.category||'Trabalho aleatorio',state:'assigned',min_km:adminTest?0:MIN_KM,classification_mode:item?'automatic':'pending',classification_confidence:Number(classification.confidence||0),classification_suggested_work_id:classification.suggested_work_id||null,pending_classification:!item,xp_only:!!(item&&already&&!adminTest),created_at:t,assigned_at:t};\n  await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();\n }\n if(!m)return null;"""
if opening_old not in worker:
    raise RuntimeError('Nao encontrei abertura final de processMission com modo admin.')
worker = worker.replace(opening_old, opening_new, 1)

branch_anchor = " if(workAlreadyCompleted){"
if branch_anchor not in worker:
    raise RuntimeError('Nao encontrei branch de trabalho repetido para inserir fila pendente.')
pending_and_learning = r''' if(m.pending_classification){
  const pendingPoints=adminTest?100:gatPoints,pendingAudit={...auditData,gat_points:pendingPoints,classification_status:'pending',classification_confidence:Number(m.classification_confidence||0),classification_suggested_work_id:m.classification_suggested_work_id||null};
  await env.DB.batch([
   env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:pendingAudit,map_mode:rbr?'rbr':'base'})),
   env.DB.prepare('UPDATE profiles SET total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?').bind(distance,xp,pendingPoints,perfect,penalty,fines,penalty,t,user),
   env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)
  ]);
  const saved=await env.DB.prepare('SELECT id FROM deliveries WHERE user=? AND delivered_at=? ORDER BY id DESC LIMIT 1').bind(user,t).first();
  if(saved?.id)await env.DB.prepare("INSERT OR IGNORE INTO cargo_classification_queue(delivery_id,user,cargo,cargo_key,source,destination,weight_kg,distance_km,delivered_at,status,suggested_work_id,suggested_confidence) VALUES(?,?,?,?,?,?,?,?,?,'pending',?,?)").bind(saved.id,user,cargo,norm(cargo),source,destination,weight,distance,t,m.classification_suggested_work_id||null,Number(m.classification_confidence||0)).run();
  return{type:'delivery_completed_pending_classification',mission:m,distance_km:distance,xp_awarded:xp,gat_points:pendingPoints,classification_status:'pending'};
 }
 if(m.classification_mode==='automatic'&&workId)await learnCargoAlias(env,cargo,workId,m.classification_confidence,'automatic');
'''
worker = worker.replace(branch_anchor, pending_and_learning + branch_anchor, 1)

admin_anchor = " if(p==='/api/site/admin/session'&&m==='POST')"
if admin_anchor not in worker:
    raise RuntimeError('Nao encontrei rotas Admin para inserir classificacao manual.')
admin_routes = r''' if(p==='/api/site/admin/unclassified'&&m==='POST'){
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
  if(counted)await env.DB.prepare('UPDATE profiles SET monthly_completed=monthly_completed+1,updated_at=? WHERE user=?').bind(at,q.user).run();
  await audit(env,s.user,'classify_cargo',q.user,{queue_id:queueId,delivery_id:q.delivery_id,cargo:q.cargo,work_id:item.id,work_title:item.title,counted});
  return json(req,{ok:true,counted,user:q.user,cargo:q.cargo,work:item});
 }
'''
worker = worker.replace(admin_anchor, admin_routes + admin_anchor, 1)

for required in ["cargo_classification_queue","autoClassifyCargo","delivery_completed_pending_classification","/api/site/admin/classify","learnCargoAlias","classification_mode==='automatic'","pendingPoints=adminTest?100:gatPoints"]:
    if required not in worker:
        raise RuntimeError('Patch automatico incompleto: '+required)
path.write_text(worker,encoding='utf-8')
print('Auto cargo classification applied:', path)
