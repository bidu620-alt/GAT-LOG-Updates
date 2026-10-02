from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# GAT Server 1.0.63
# - Nao recalcula nem altera entregas, XP ou Pontos GAT ja gravados.
# - Novas entregas: 1 km valido = 1 XP, sem desconto de multas/danos no XP.
# - Pontos GAT mantem exatamente a formula atual por distancia/peso/penalidades.
# - A auditoria de multas e danos passa a ter marco zero no primeiro pacote em
#   que a carga esta efetivamente engatada/carregada.
# - Dano que o caminhao ja possuia antes de engatar a carga vira baseline e nao
#   e cobrado de novo em viagens seguintes.

worker=once(worker,"const VERSION='1.0.62-local';","const VERSION='1.0.63-local';",'versao 1.0.62')

damage_helper="const damageDelta=(max,start)=>Math.max(0,(Number(max)||0)-(Number(start)||0));\n"
fine_helper=r"""const fineCounterOf=raw=>{
 const keys=['speed_fines','speedFines','gat_speed_fines','gameplay.speedFines','gameplay.speed_fines','job.speedFines','job.speed_fines'];
 for(const key of keys){const v=deep(raw,key),n=Number(v);if(v!==undefined&&v!==null&&Number.isFinite(n))return Math.max(0,Math.trunc(n))}
 return null;
};
"""
worker=once(worker,damage_helper,damage_helper+fine_helper,'helper de delta de dano')

tracking_anchor=" const truckNow=truckDamageOf(raw),truckPartsNow=truckDamageParts(raw),trailerNow=trailerDamageOf(raw);\n if(hasLoadedJob&&m.state==='active'){"
tracking_new=r""" const truckNow=truckDamageOf(raw),truckPartsNow=truckDamageParts(raw),trailerNow=trailerDamageOf(raw);
 const scoringTripKey=String(str(raw,'gat_trip_id','gatTripId')||m.trip_id||m.job_latch_key||[f.cargo_id||f.cargo_name||'',f.source_city||'',f.destination_city||'',m.started_at||t].join('|'));
 const cargoDamageNow=Math.max(0,damageAliasPct(raw,'cargo_damage_pct'));
 const fineNow=fineCounterOf(raw);
 if(hasLoadedJob&&m.state==='active'&&(!m.scoring_started_at||String(m.scoring_trip_key||'')!==scoringTripKey)){
   m.scoring_started_at=t;m.scoring_trip_key=scoringTripKey;
   m.cargo_damage_start_pct=cargoDamageNow;
   m.speed_fines_start=fineNow===null?null:fineNow;
   m.truck_damage_start_pct=truckNow;m.truck_damage_max_pct=truckNow;
   m.truck_engine_damage_start_pct=truckPartsNow.engine;m.truck_engine_damage_max_pct=truckPartsNow.engine;
   m.truck_transmission_damage_start_pct=truckPartsNow.transmission;m.truck_transmission_damage_max_pct=truckPartsNow.transmission;
   m.truck_cabin_damage_start_pct=truckPartsNow.cabin;m.truck_cabin_damage_max_pct=truckPartsNow.cabin;
   m.truck_chassis_damage_start_pct=truckPartsNow.chassis;m.truck_chassis_damage_max_pct=truckPartsNow.chassis;
   m.truck_wheels_damage_start_pct=truckPartsNow.wheels;m.truck_wheels_damage_max_pct=truckPartsNow.wheels;
   m.trailer_damage_start_pct=trailerNow;m.trailer_damage_max_pct=trailerNow;
   await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
 if(hasLoadedJob&&m.state==='active'&&m.scoring_started_at){"""
worker=once(worker,tracking_anchor,tracking_new,'inicio da auditoria ao engatar a carga')

old_damage="damage=Math.max(damageEventPct,damageAliasPct(raw,'cargo_damage_pct')),"
new_damage="cargoFinalDamage=Math.max(damageEventPct,damageAliasPct(raw,'cargo_damage_pct')),cargoStartDamage=Math.max(0,Number(m.cargo_damage_start_pct)||0),damage=damageDelta(cargoFinalDamage,cargoStartDamage),"
worker=once(worker,old_damage,new_damage,'delta da carga a partir do engate')

old_fines="fines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),baseXP=Math.floor(distance/100)*20,"
new_fines="deliveredFines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),liveFineFinal=fineCounterOf(raw),fineStart=Math.max(0,Number(m.speed_fines_start)||0),fines=liveFineFinal===null?deliveredFines:Math.max(0,liveFineFinal-fineStart),baseXP=Math.max(0,Math.floor(distance)),"
worker=once(worker,old_fines,new_fines,'XP 1 por km e multas desde o engate')

old_xp="perfect=damage<=0.5&&truckDamage<=0.5&&fines===0?1:0,bonus=perfect?5:0,penalty=xpPenalty,xp=Math.max(0,baseXP-penalty+bonus),"
new_xp="perfect=damage<=0.5&&truckDamage<=0.5&&fines===0?1:0,bonus=0,penalty=xpPenalty,xp=baseXP,"
worker=once(worker,old_xp,new_xp,'XP independente das penalidades')

old_audit="auditData={base_xp:baseXP,"
new_audit="auditData={base_xp:baseXP,xp_rule:'1km_1xp',scoring_started_at:m.scoring_started_at||null,scoring_trip_key:m.scoring_trip_key||null,speed_fines_start:Number.isFinite(Number(m.speed_fines_start))?Number(m.speed_fines_start):null,cargo_damage_start_pct:cargoStartDamage,"
worker=once(worker,old_audit,new_audit,'auditoria da nova regra')

# A formula de Pontos GAT precisa permanecer exatamente como estava na 1.0.62.
required=[
 "const VERSION='1.0.63-local'",
 "gatBasePoints=Math.floor(distance/99)*10",
 "gatPoints=rankEligible?Math.max(0,gatBasePoints-pointPenalty):0",
 "gat_speed_penalty_points:gatSpeedPenalty",
 "gat_cargo_penalty_points:gatCargoPenalty",
 "gat_truck_penalty_points:gatTruckPenalty",
 "baseXP=Math.max(0,Math.floor(distance))",
 "xp=baseXP",
 "xp_rule:'1km_1xp'",
 "scoring_started_at=t",
 "m.truck_damage_start_pct=truckNow",
 "damage=damageDelta(cargoFinalDamage,cargoStartDamage)",
 "liveFineFinal===null?deliveredFines:Math.max(0,liveFineFinal-fineStart)",
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch 1.0.63 incompleto: '+marker)

for forbidden in [
 "baseXP=Math.floor(distance/100)*20",
 "xp=Math.max(0,baseXP-penalty+bonus)",
]:
    if forbidden in worker:
        raise SystemExit('1.0.63 ainda possui regra antiga de XP: '+forbidden)

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.63: 1 km = 1 XP; multas/danos iniciam no engate; Pontos GAT e historico preservados.')
