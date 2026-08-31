from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

# Helpers da nova pontuacao mensal GAT.
marker="const level=x=>Math.max(1,Math.floor((Number(x)||0)/2000)+1);\n"
helpers="""const level=x=>Math.max(1,Math.floor((Number(x)||0)/2000)+1);\nconst scoreTier=(v,a,b,c)=>{v=Math.max(0,Number(v)||0);return v<=0?0:v<=a?3:v<=b?5:v<=c?10:15};\nconst monthPoints=list=>(list||[]).filter(x=>String(x?.delivered_at||'').slice(0,7)===month()).reduce((sum,x)=>{const gp=Number(x?.gat_points);if(Number.isFinite(gp))return sum+Math.max(0,gp);return sum+Math.max(0,100-(Number(x?.penalty_xp)||0))},0);\n"""
if 'const scoreTier=' not in s:
    if marker not in s: raise SystemExit('helper level nao encontrado')
    s=s.replace(marker,helpers,1)

# Perfil: points passa a significar Pontos GAT do mes atual, sem depender de reset manual.
old="const deliveries=(d.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.raw_json||'{}')}catch{}return{...x,...raw.audit}}).reverse();return{user,monthly_completed:p.monthly_completed,monthly_goal:p.monthly_goal,total_deliveries:p.total_deliveries,total_km:p.total_km,xp:p.xp,level:level(p.xp),points:p.points,perfect_trips:p.perfect_trips,penalty_xp:p.penalty_xp,speed_fines:p.speed_fines,safety_score:p.safety_score,current_mission:mission,deliveries}"
new="const deliveries=(d.results||[]).map(x=>{let raw={};try{raw=JSON.parse(x.raw_json||'{}')}catch{}return{...x,...raw.audit}}).reverse(),points=monthPoints(deliveries);return{user,monthly_completed:p.monthly_completed,monthly_goal:p.monthly_goal,total_deliveries:p.total_deliveries,total_km:p.total_km,xp:p.xp,level:level(p.xp),points,perfect_trips:p.perfect_trips,penalty_xp:p.penalty_xp,speed_fines:p.speed_fines,safety_score:p.safety_score,current_mission:mission,deliveries}"
if old not in s: raise SystemExit('retorno profile esperado nao encontrado')
s=s.replace(old,new,1)

# Entrega: 100 pontos fixos no ranking; as mesmas penalidades tambem reduzem o XP da viagem.
old="const damageRaw=Math.max(0,Number(details.cargoDamage)||0),damage=damageRaw<=1?damageRaw*100:damageRaw,fines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),baseXP=Math.floor(distance/100)*20,speedPenalty=fines*3,cargoPenalty=Math.round(damage*2),perfect=damage<=0.1&&fines===0?1:0,bonus=perfect?25:0,penalty=speedPenalty+cargoPenalty,xp=Math.max(0,baseXP-penalty+bonus),cargo=m.cargo||f.cargo_name||m.custom_cargo||m.title||'Carga',weight=Number(m.weight_kg)||f.mass_kg||0,auditData={base_xp:baseXP,speed_penalty_xp:speedPenalty,cargo_penalty_xp:cargoPenalty,truck_penalty_xp:0,perfect_bonus_xp:bonus,cargo_damage_pct:damage,truck_damage_delta_pct:0,perfect_trip:!!perfect,xp_awarded:xp};"
new="const damageRaw=Math.max(0,Number(details.cargoDamage)||0),damage=damageRaw<=1?damageRaw*100:damageRaw,truckRaw=Math.max(0,num(details,'truckDamageDeltaPct','truck_damage_delta_pct','truckDamage')||num(raw,'truck_damage_delta_pct','truckDamageDeltaPct')),truckDamage=truckRaw<=1?truckRaw*100:truckRaw,fines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),baseXP=Math.floor(distance/100)*20,gatSpeedPenalty=fines*3,gatCargoPenalty=scoreTier(damage,3,7,15),gatTruckPenalty=scoreTier(truckDamage,5,10,20),xpPenalty=gatSpeedPenalty+gatCargoPenalty+gatTruckPenalty,pointPenalty=Math.min(100,xpPenalty),gatPoints=Math.max(0,100-pointPenalty),perfect=damage<=0.5&&truckDamage<=0.5&&fines===0?1:0,bonus=perfect?5:0,penalty=xpPenalty,xp=Math.max(0,baseXP-penalty+bonus),cargo=m.cargo||f.cargo_name||m.custom_cargo||m.title||'Carga',weight=Number(m.weight_kg)||f.mass_kg||0,auditData={base_xp:baseXP,speed_penalty_xp:gatSpeedPenalty,cargo_penalty_xp:gatCargoPenalty,truck_penalty_xp:gatTruckPenalty,perfect_bonus_xp:bonus,cargo_damage_pct:damage,truck_damage_delta_pct:truckDamage,perfect_trip:!!perfect,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:gatSpeedPenalty,gat_cargo_penalty_points:gatCargoPenalty,gat_truck_penalty_points:gatTruckPenalty,gat_penalty_points:pointPenalty,gat_points:gatPoints};"
if old not in s: raise SystemExit('calculo de entrega esperado nao encontrado')
s=s.replace(old,new,1)

# Ranking oficial agora usa Pontos GAT como criterio principal. 100 por entrega, maximo teorico 3000.
old="if(p==='/api/public/ranking'&&m==='GET'){const r=await env.DB.prepare('SELECT p.user,p.monthly_completed,p.monthly_goal,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km FROM profiles p JOIN accounts a ON a.user=p.user WHERE a.disabled=0 ORDER BY p.monthly_completed DESC,p.perfect_trips DESC,p.penalty_xp ASC,p.speed_fines ASC,p.user ASC').all(),season=(await env.DB.prepare(\"SELECT value FROM meta WHERE key='season'\").first())?.value||month(),mode=(await env.DB.prepare(\"SELECT value FROM meta WHERE key='operation_mode'\").first())?.value||'official';return json(req,{ok:true,operation_mode:mode,season,ranking:r.results||[]})}"
new="if(p==='/api/public/ranking'&&m==='GET'){const season=(await env.DB.prepare(\"SELECT value FROM meta WHERE key='season'\").first())?.value||month(),mode=(await env.DB.prepare(\"SELECT value FROM meta WHERE key='operation_mode'\").first())?.value||'official',r=await env.DB.prepare(`SELECT p.user,p.monthly_completed,p.monthly_goal,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km,COALESCE(s.points,0) AS points FROM profiles p JOIN accounts a ON a.user=p.user LEFT JOIN (SELECT user,SUM(COALESCE(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER),MAX(0,100-penalty_xp))) AS points FROM deliveries WHERE substr(delivered_at,1,7)=? GROUP BY user) s ON s.user=p.user WHERE a.disabled=0 ORDER BY points DESC,p.monthly_completed DESC,p.perfect_trips DESC,p.penalty_xp ASC,p.speed_fines ASC,p.user ASC`).bind(season).all();return json(req,{ok:true,operation_mode:mode,season,scoring:{base_per_delivery:100,max_monthly:3000},ranking:r.results||[]})}"
if old not in s: raise SystemExit('rota de ranking esperada nao encontrada')
s=s.replace(old,new,1)

# Sobe versao final do Worker depois dos patches anteriores.
s=s.replace("const VERSION='1.0.46-cloudflare';","const VERSION='1.0.47-cloudflare';",1)

required=['gat_points:gatPoints','gat_base_points:100','ORDER BY points DESC','scoring:{base_per_delivery:100,max_monthly:3000}','const scoreTier=', 'xp=Math.max(0,baseXP-penalty+bonus)', 'speed_penalty_xp:gatSpeedPenalty']
for x in required:
    if x not in s: raise SystemExit('patch de pontuacao incompleto: '+x)

p.write_text(s,encoding='utf-8')
print('Pontuacao GAT aplicada: 100 por entrega; penalidades reduzem pontos e XP.')
