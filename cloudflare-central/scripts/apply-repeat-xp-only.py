from pathlib import Path
import re

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

# 1) Selecionar novamente um trabalho ja concluido passa a criar uma missao XP-only.
old="if(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first())throw new HttpError(409,'work_already_completed');const custom="
new="const repeatXpOnly=!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(s.user,item.id,month()).first());const custom="
if old not in s:
    raise SystemExit('bloqueio de trabalho concluido nao encontrado')
s=s.replace(old,new,1)

old="custom_cargo:custom,state:'assigned',min_km:MIN_KM,created_at:t,assigned_at:t};"
new="custom_cargo:custom,xp_only:repeatXpOnly,state:'assigned',min_km:MIN_KM,created_at:t,assigned_at:t};"
if old not in s:
    raise SystemExit('objeto de missao selecionada nao encontrado')
s=s.replace(old,new,1)

old="return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),rules_enabled:true,operation_mode:'official'})"
new="return json(req,{ok:true,mission,completed:Number(pr?.monthly_completed||0),xp_only:repeatXpOnly,rules_enabled:true,operation_mode:'official'})"
if old not in s:
    raise SystemExit('retorno de selecao de trabalho nao encontrado')
s=s.replace(old,new,1)

# 2) Se o trabalho ja conta como concluido no mes, rota repetida deixa de bloquear.
# Fazemos a troca por limites estaveis porque patches anteriores alteram a formatacao interna.
route_start=s.find(" const routeKey=`${norm(source)}>${norm(destination)}`")
mission_start=s.find(" const missionId=",route_start)
if route_start < 0 or mission_start < 0:
    raise SystemExit('bloco de rota/trabalho concluido nao encontrado')
new_route=""" const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||''),workAlreadyCompleted=Boolean(m.xp_only)||!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first());if(!workAlreadyCompleted&&await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first()){await resetAssigned(env,user,m,'route_already_used');return{type:'delivery_rejected',reason:'route_already_used'}}
"""
s=s[:route_start]+new_route+s[mission_start:]

# 3) Depois de calcular o XP normalmente, repeticoes alteram SOMENTE profiles.xp.
# Nao somam x/30, Pontos GAT, entregas, km, perfeitas, multas ou penalidades do ranking.
marker=",gat_points:gatPoints};\n await env.DB.batch(["
insert=""",gat_points:gatPoints};
 if(workAlreadyCompleted){
   await env.DB.prepare('UPDATE profiles SET xp=xp+?,current_mission_json=NULL,updated_at=? WHERE user=?').bind(xp,t,user).run();
   return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,perfect:!!perfect,gat_points:0,monthly_increment:0,rank_eligible:false,xp_only:true,min_km:MIN_KM}
 }
 await env.DB.batch(["""
if marker not in s:
    raise SystemExit('ponto de calculo GAT para XP-only nao encontrado')
s=s.replace(marker,insert,1)

# Define a versao final sem depender do numero temporario deixado pelos patches anteriores.
s,n=re.subn(r"const VERSION='[0-9.]+-cloudflare';","const VERSION='1.0.49-cloudflare';",s,count=1)
if n!=1:
    raise SystemExit('constante VERSION do Worker nao encontrada')

required=[
    'xp_only:repeatXpOnly',
    'workAlreadyCompleted=Boolean(m.xp_only)',
    "UPDATE profiles SET xp=xp+?,current_mission_json=NULL",
    "rank_eligible:false,xp_only:true",
    "monthly_increment:0",
    "const VERSION='1.0.49-cloudflare'",
]
for x in required:
    if x not in s:
        raise SystemExit('patch XP-only incompleto: '+x)

# Garante que o bloqueio antigo nao sobreviveu no fluxo de conclusao.
segment=s[s.find('async function processMission'):s.find('async function clientCredential')]
if "reason:'work_already_completed'" in segment:
    raise SystemExit('bloqueio work_already_completed ainda presente em processMission')

p.write_text(s,encoding='utf-8')
print('Repeticao XP-only aplicada: trabalho concluido pode repetir sem alterar ranking ou 30/30.')
