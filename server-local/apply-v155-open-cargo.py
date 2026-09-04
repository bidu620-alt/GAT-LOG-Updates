from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')


def replace_once(text, old, new, label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)


def remove_js_function(text, signature):
    start=text.find(signature)
    if start<0:
        raise SystemExit('Nao encontrei helper antigo: '+signature)
    brace=text.find('{',start)
    if brace<0:
        raise SystemExit('Helper sem corpo: '+signature)
    depth=0
    i=brace
    quote=None
    escaped=False
    while i<len(text):
        ch=text[i]
        if quote:
            if escaped:
                escaped=False
            elif ch=='\\':
                escaped=True
            elif ch==quote:
                quote=None
        else:
            if ch in ("'",'"','`'):
                quote=ch
            elif ch=='{':
                depth+=1
            elif ch=='}':
                depth-=1
                if depth==0:
                    end=i+1
                    while end<len(text) and text[end] in '\r\n':
                        end+=1
                    return text[:start]+text[end:]
        i+=1
    raise SystemExit('Nao consegui fechar helper: '+signature)

# GAT Server 1.0.55 - carga aberta.
# A carga observada pela telemetria e dado da viagem, nunca uma regra de aceitacao.
worker=replace_once(worker,"const VERSION='1.0.54-local';","const VERSION='1.0.55-local';",'versao 1.0.54')

# A carga nunca e rejeitada por compatibilidade com catalogo/categoria.
cargo_guard="if(!await cargoOK(env,m,m.cargo||f.cargo_name)){await resetAssigned(env,user,m,'cargo_not_compatible',{last_cargo:m.cargo||f.cargo_name});return{type:'delivery_rejected',reason:'cargo_not_compatible'}}"
if cargo_guard in worker:
    worker=worker.replace(cargo_guard,'',1)

# Qualquer carga detectada abre uma viagem automaticamente, inclusive nomes de mods.
start=worker.find(" const observed=flat(user,user,t,raw);\n if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){")
end=worker.find("\n if(!m)return null;",start)
if start<0 or end<0:
    raise SystemExit('Nao encontrei abertura automatica antiga da classificacao')
end += len("\n if(!m)return null;")
open_start=""" const observed=flat(user,user,t,raw);
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
 if(!m)return null;"""
worker=worker[:start]+open_start+worker[end:]

# Uma carga aberta conclui como entrega normal e entra no historico/ranking normal.
pending_start=worker.find(" if(m.pending_classification){")
learn_start=worker.find(" if(m.classification_mode==='automatic'&&workId)await learnCargoAlias",pending_start)
if pending_start<0 or learn_start<0:
    raise SystemExit('Nao encontrei ramo pendente/learning antigo')
learn_end=worker.find('\n',learn_start)
if learn_end<0: learn_end=len(worker)
open_finish=r''' if(m.open_cargo){
  const openAudit={...auditData,gat_points:gatPoints,cargo_mode:'open',cargo_rule:'none'};
  await env.DB.batch([
   env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,null,source,destination,cargo,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:openAudit,map_mode:rbr?'rbr':'base'})),
   env.DB.prepare('UPDATE profiles SET monthly_completed=monthly_completed+1,total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?').bind(distance,xp,gatPoints,perfect,penalty,fines,penalty,t,user),
   env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)
  ]);
  return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,xp_awarded:xp,perfect:!!perfect,gat_points:gatPoints,rank_eligible:rankEligible,ranking_reason:rankReason,history_recorded:true,open_cargo:true,min_km:MIN_KM};
 }
'''
worker=worker[:pending_start]+open_finish+worker[learn_end+1:]

# Remove as rotas visuais/administrativas da antiga fila de classificacao.
admin_start=worker.find(" if(p==='/api/site/admin/unclassified'&&m==='POST'){")
admin_end=worker.find(" if(p==='/api/site/admin/session'&&m==='POST')",admin_start)
if admin_start<0 or admin_end<0:
    raise SystemExit('Nao encontrei endpoints antigos de classificacao')
worker=worker[:admin_start]+worker[admin_end:]

# Remove cirurgicamente SOMENTE os helpers do classificador. Nao usa intervalo entre
# funcoes para nao atingir processMission, retomada de viagem ou journal assinado.
rules_start=worker.find('const AUTO_CARGO_RULES=[')
rules_end=worker.find('function catalogCargoScore',rules_start)
if rules_start<0 or rules_end<0:
    raise SystemExit('Nao encontrei regras antigas de classificacao')
worker=worker[:rules_start]+worker[rules_end:]
for signature in [
    'function cargoWordSet(v){',
    'function cargoTextScore(a,b){',
    'function catalogCargoScore(item,cargo){',
    'async function autoClassifyCargo(env,cargo){',
    'async function learnCargoAlias(env,cargo,workId,confidence=1,source=',
]:
    worker=remove_js_function(worker,signature)

# Patches antigos podem conhecer o nome do evento pendente; a 1.0.55 nunca o produz.
worker=worker.replace(",'delivery_completed_pending_classification'",'')
worker=worker.replace("'delivery_completed_pending_classification',",'')
worker=worker.replace(',"delivery_completed_pending_classification"','')
worker=worker.replace('"delivery_completed_pending_classification",','')

required=[
    "const VERSION='1.0.55-local'",
    "catalog_id:'__open_cargo__'",
    "open_cargo:true",
    "cargo_mode:'open'",
    "cargo_rule:'none'",
    "cargo_history:c.results||[]",
    "history_recorded:true",
    "async function processMission(",
    "async function inspectClientPacket(",
    "async function persistClientPacket(",
    "createHmac",
    "journal_signature_invalid",
    "journal_chain_gap",
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch 1.0.55 removeu funcionalidade necessaria: '+marker)
for forbidden in [
    "reason:'cargo_not_compatible'",
    "delivery_completed_pending_classification",
    "classification_status:'pending'",
    "/api/site/admin/classify",
    "/api/site/admin/unclassified",
    "autoClassifyCargo",
    "learnCargoAlias",
    "cargo_classification_queue",
    "AUTO_CARGO_RULES",
    "cargoTextScore",
]:
    if forbidden in worker:
        raise SystemExit('Classificacao antiga ainda ativa no runtime: '+forbidden)

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.55: carga aberta ativada; somente o classificador foi removido.')
