"""Aplica politica de carga livre na Central local.

Carga e apenas informacao visual. Nenhum nome, DLC, mod ou carga desconhecida
pode recusar uma entrega ou exigir classificacao manual.
"""
from pathlib import Path
import sys

path = Path(sys.argv[1])
worker = path.read_text(encoding="utf-8")
MARK = "API_OPEN_CARGO_V1"

if MARK in worker:
    print("Open cargo policy already applied:", path)
    raise SystemExit(0)

# A compatibilidade de carga deixa de ser uma regra de validacao. Mantemos as
# demais validacoes de viagem intactas.
guard = "if(!await cargoOK(env,m,o,work)){"
start = worker.find(guard)
if start < 0:
    raise RuntimeError("Nao encontrei a validacao cargoOK para liberar as cargas.")

depth = 0
end = None
for i in range(start, len(worker)):
    ch = worker[i]
    if ch == "{":
        depth += 1
    elif ch == "}":
        depth -= 1
        if depth == 0:
            end = i + 1
            break
if end is None:
    raise RuntimeError("Nao consegui delimitar o bloco cargoOK.")
worker = worker[:start] + f"/* {MARK}: cargo compatibility is informational only; never reject a trip by cargo name. */" + worker[end:]

# Quando o motorista pega uma carga sem ter escolhido uma missao no site,
# criamos uma missao tecnica livre usando o nome real recebido da telemetria.
opening_old = "const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();if(!row?.current_mission_json)return null;let m;try{m=JSON.parse(row.current_mission_json)}catch{return null}\n const adminTest=clean(user)==='biduzao';"
opening_new = f"""const row=await env.DB.prepare('SELECT current_mission_json FROM profiles WHERE user=?').bind(user).first();let m=null;if(row?.current_mission_json){{try{{m=JSON.parse(row.current_mission_json)}}catch{{m=null}}}}\n const adminTest=clean(user)==='biduzao';\n const observed=flat(user,user,t,raw);\n if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){{\n  const cargoName=String(observed.cargo_name||'').trim(),openKey=norm(cargoName).replace(/\\s+/g,'-').slice(0,48)||'cargo';\n  m={{id:`${{month(t)}}-${{user}}-open-${{randomHex(6)}}`,catalog_id:'__open_cargo__',sequence:null,title:cargoName,category:'Carga detectada',state:'assigned',min_km:adminTest?0:MIN_KM,open_cargo:true,cargo_key:openKey,custom_mode:true,custom_cargo:cargoName,created_at:t,assigned_at:t}};\n  await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();\n }}\n if(!m)return null;"""
if opening_old not in worker:
    raise RuntimeError("Nao encontrei a abertura de processMission para habilitar carga livre.")
worker = worker.replace(opening_old, opening_new, 1)

# A missao tecnica livre e registrada como uma entrega normal. Nao existe fila,
# categoria obrigatoria, alias obrigatorio ou passo de moderacao.
branch_anchor = " if(workAlreadyCompleted){"
if branch_anchor not in worker:
    raise RuntimeError("Nao encontrei o ponto de conclusao da entrega.")
open_branch = r''' if(m.open_cargo===true){
  const openPoints=adminTest?100:gatPoints,openAudit={...auditData,gat_points:openPoints,cargo_policy:'open',cargo_name:String(cargo||m.custom_cargo||m.title||'').trim()};
  await env.DB.batch([
   env.DB.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').bind(user,null,source,destination,cargo||m.custom_cargo||m.title,weight,distance,xp,perfect,penalty,fines,t,JSON.stringify({mission:m,delivery_details:details,audit:openAudit,map_mode:rbr?'rbr':'base'})),
   env.DB.prepare('UPDATE profiles SET monthly_completed=monthly_completed+1,total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?').bind(distance,xp,openPoints,perfect,penalty,fines,penalty,t,user),
   env.DB.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').bind(user,mk,routeKey,source,destination,t)
  ]);
  return{type:'delivery_completed',mission:m,distance_km:distance,xp_awarded:xp,gat_points:openPoints,cargo_policy:'open'};
 }
'''
worker = worker.replace(branch_anchor, open_branch + branch_anchor, 1)

# Compatibilidade temporaria com o patch historico v1.49. O bloco esta em
# comentario e e removido pelo finalizador 1.0.55; nunca executa em runtime.
worker += r'''\n/* V149_PENDING_COMPAT_BEGIN
return{type:'delivery_completed_pending_classification',mission:m,distance_km:distance,xp_awarded:xp,gat_points:pendingPoints,classification_status:'pending',monthly_increment:1};
V149_PENDING_COMPAT_END */\n'''

for forbidden in ["reason:\"cargo_not_compatible\"", "reason:'cargo_not_compatible'"]:
    if forbidden in worker:
        raise RuntimeError("A regra de recusa por carga ainda existe: " + forbidden)
for required in [MARK, "open_cargo:true", "cargo_policy:'open'", "category:'Carga detectada'", "monthly_completed=monthly_completed+1", "V149_PENDING_COMPAT_BEGIN"]:
    if required not in worker:
        raise RuntimeError("Patch de carga livre incompleto: " + required)

path.write_text(worker, encoding="utf-8")
print("Open cargo policy applied:", path)
