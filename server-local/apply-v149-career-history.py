from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
host_path=root/'host.mjs'
worker=worker_path.read_text(encoding='utf-8')
host=host_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# v1.49: a entrega e historico primeiro. Ranking apenas decide Pontos GAT.
worker=once(worker,"const VERSION='1.0.48-local';","const VERSION='1.0.49-local';",'versao local 1.0.48')

# Perfil publico deixa de expor a antiga meta mensal. Mantemos somente a contagem real do mes.
worker=once(worker,
"return{user,monthly_completed:p.monthly_completed,monthly_goal:p.monthly_goal,total_deliveries:p.total_deliveries",
"return{user,monthly_completed:p.monthly_completed,monthly_deliveries:p.monthly_completed,total_deliveries:p.total_deliveries",
'perfil com monthly_goal')

# Repeticao de carga/trabalho nunca muda a natureza da viagem. Cada conclusao e um novo registro.
worker=worker.replace("xp_only:!!(item&&already&&!adminTest)","xp_only:false")
worker=worker.replace("xp_only:repeatXpOnly,state:'assigned'","xp_only:false,state:'assigned'")
worker=worker.replace("xp_only:repeatXpOnly,rules_enabled:true","xp_only:false,rules_enabled:true")

# Falta de confirmacao de progresso e uma razao para nao pontuar, nao para apagar a entrega.
worker=once(worker,
" if(!adminTest&&delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};\n",
"",
'antigo descarte no_trip_progress')

old_rank=""" if(!adminTest&&(m.rank_guard?.reason||!m.rank_guard)){
   const reason=m.rank_guard?.reason||'telemetry_not_verified_from_start';
   await resetAssigned(env,user,m,reason);
   return{type:'delivery_rejected',reason,rank_eligible:false,gat_points:0,xp:0,message:rankingMessage(reason)};
 }
 if(!adminTest&&m.state!=='active')return{type:'delivery_rejected',reason:'mission_not_active'};const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr,distance=rbr?(Number(m.rbr_start_remaining_km)||Number(m.planned_distance_km)||teleKm||0):(Number(details.distanceKm)||Number(m.planned_distance_km)||baseKm||0);
 if(!adminTest&&distance<minKm){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:minKm}}
"""
new_rank=""" const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr,distance=Math.max(0,rbr?(Number(m.rbr_start_remaining_km)||Number(m.planned_distance_km)||teleKm||0):(Number(details.distanceKm)||Number(m.planned_distance_km)||baseKm||0)),rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankReason=adminTest?null:(rankGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;
"""
worker=once(worker,old_rank,new_rank,'bloqueio de ranking antes do registro')

# Rota repetida e permitida. A tabela routes_completed continua apenas como indice historico/legado.
old_route="const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||''),workAlreadyCompleted=adminTest?false:(Boolean(m.xp_only)||!!(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first()));if(!adminTest&&!workAlreadyCompleted&&await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first()){await resetAssigned(env,user,m,'route_already_used');return{type:'delivery_rejected',reason:'route_already_used'}}"
new_route="const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||'')"
worker=once(worker,old_route,new_route,'bloqueio de rota repetida')

# XP e experiencia de estrada: sempre baseado na quilometragem registrada. Danos/multas afetam Pontos GAT e estatisticas, nao apagam nem reduzem o XP da viagem.
worker=once(worker,
"pointPenalty=Math.min(100,xpPenalty),gatPoints=Math.max(0,100-pointPenalty),perfect=adminTest?1:(damage<=0.5&&truckDamage<=0.5&&fines===0?1:0),bonus=perfect?5:0,penalty=xpPenalty,xp=Math.max(0,baseXP-penalty+bonus),cargo=",
"pointPenalty=Math.min(100,xpPenalty),gatPoints=rankEligible?Math.max(0,100-pointPenalty):0,perfect=adminTest?1:(rankEligible&&damage<=0.5&&truckDamage<=0.5&&fines===0?1:0),bonus=0,penalty=xpPenalty,xp=baseXP,cargo=",
'formula de XP/Pontos GAT')

worker=once(worker,
"rank_verified:!adminTest,admin_test_mode:adminTest,rank_client_version:raw.gat_client_version",
"rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,ranking_message:rankReason?rankingMessage(rankReason):'',history_recorded:true,admin_test_mode:adminTest,rank_client_version:raw.gat_client_version",
'auditoria de ranking')

# Carga ainda nao classificada tambem entra no historico e recebe XP. A classificacao fica desacoplada.
worker=worker.replace("const pendingPoints=adminTest?100:gatPoints","const pendingPoints=gatPoints")
worker=worker.replace("monthly_completed=MIN(monthly_goal,monthly_completed+1)","monthly_completed=monthly_completed+1")
worker=once(worker,
"return{type:'delivery_completed_pending_classification',mission:m,distance_km:distance,xp_awarded:xp,gat_points:pendingPoints,classification_status:'pending',monthly_increment:1};",
"return{type:'delivery_completed_pending_classification',mission:m,distance_km:distance,xp_awarded:xp,gat_points:pendingPoints,rank_eligible:rankEligible,ranking_reason:rankReason,history_recorded:true,classification_status:'pending',monthly_increment:1};",
'retorno de carga pendente')

# Remove definitivamente o antigo ramo XP-only de repeticao.
old_repeat=""" if(false&&workAlreadyCompleted){
   await env.DB.prepare('UPDATE profiles SET xp=xp+?,current_mission_json=NULL,updated_at=? WHERE user=?').bind(xp,t,user).run();
   return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,perfect:!!perfect,gat_points:0,monthly_increment:0,rank_eligible:false,xp_only:true,min_km:MIN_KM}
 }
"""
worker=once(worker,old_repeat,"",'ramo XP-only de repeticao')
worker=once(worker,
"return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,perfect:!!perfect,min_km:MIN_KM}",
"return{type:'delivery_completed',user,cargo,source,destination,distance_km:distance,xp,xp_awarded:xp,perfect:!!perfect,gat_points:gatPoints,rank_eligible:rankEligible,ranking_reason:rankReason,history_recorded:true,min_km:MIN_KM}",
'retorno de entrega classificada')

# O catalogo pode continuar marcando primeira conclusao, mas nao reinicia visualmente por mes.
worker=once(worker,
"d=await env.DB.prepare('SELECT work_id FROM work_completed WHERE user=? AND month_key=?').bind(user,month()).all()",
"d=await env.DB.prepare('SELECT DISTINCT work_id FROM work_completed WHERE user=?').bind(user).all()",
'catalogo mensal concluido')

# Reparos antigos continuam idempotentes; apenas deixam de respeitar teto mensal.
host=host.replace("monthly_completed=MIN(monthly_goal,monthly_completed+1)","monthly_completed=monthly_completed+1")
old_reconcile="""function reconcileMonthlyTripGoal(db){
  // A meta mensal e por VIAGENS VALIDAS, nao por classificacoes. Como toda entrega
  // aceita pelo ranking gera uma linha em deliveries, ela e a fonte definitiva do x/30.
  // Isso tambem corrige automaticamente a virada do mes e recupera entregas que foram
  // salvas como pendentes de classificacao antes da 1.0.46.
  const mk=new Date().toISOString().slice(0,7);
  db.sql.prepare(`UPDATE profiles SET monthly_completed=MIN(monthly_goal,(
    SELECT COUNT(*) FROM deliveries d
    WHERE d.user=profiles.user AND substr(d.delivered_at,1,7)=?
  ))`).run(mk);
}
"""
new_reconcile="""function reconcileMonthlyTripCount(db){
  // Contagem mensal informativa, sem meta e sem teto. A fonte e o historico real de entregas.
  const mk=new Date().toISOString().slice(0,7);
  db.sql.prepare(`UPDATE profiles SET monthly_completed=(
    SELECT COUNT(*) FROM deliveries d
    WHERE d.user=profiles.user AND substr(d.delivered_at,1,7)=?
  )`).run(mk);
}
"""
host=once(host,old_reconcile,new_reconcile,'reconciliacao antiga x/30')
host=host.replace('reconcileMonthlyTripGoal','reconcileMonthlyTripCount')
host=host.replace('os\n  // 30 trabalhos, historico, contas e progresso','o\n  // catalogo, historico, contas e progresso')

# Contratos finais da filosofia GAT LOG 1.0.49.
segment=worker[worker.find('async function processMission'):worker.find('async function clientCredential')]
required=[
    "const VERSION='1.0.49-local'",
    'rankEligible=!rankReason',
    'gatPoints=rankEligible?Math.max(0,100-pointPenalty):0',
    'xp=baseXP,cargo=',
    'history_recorded:true',
    'ranking_reason:rankReason',
    'monthly_completed=monthly_completed+1',
    "INSERT OR IGNORE INTO mission_completions",
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch v1.49 incompleto: '+marker)
for forbidden in ["reason:'route_already_used'","reason:'no_trip_progress'","if(false&&workAlreadyCompleted)","monthly_completed=MIN(monthly_goal"]:
    if forbidden in segment:
        raise SystemExit('Regra antiga ainda presente no fluxo de entrega: '+forbidden)
if 'monthly_goal:p.monthly_goal' in worker:
    raise SystemExit('Meta mensal ainda exposta no perfil')
if 'reconcileMonthlyTripGoal' in host or 'MIN(monthly_goal' in host or 'x/30' in host:
    raise SystemExit('Meta mensal antiga ainda presente no host local')

worker_path.write_text(worker,encoding='utf-8')
host_path.write_text(host,encoding='utf-8')
print('GAT Server 1.0.49: historico, XP, Pontos GAT e catalogo agora sao responsabilidades separadas.')
