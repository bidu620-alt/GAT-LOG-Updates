from pathlib import Path
import re

p = Path('worker.js')
s = p.read_text(encoding='utf-8')

# GAT Telemetria 1.0.28 envia danos em porcentagem nos aliases *_damage_pct.
# Valores brutos do TruckSim (wear*) continuam em escala 0..1 e sao convertidos aqui.
score_anchor = "const scoreTier=(v,a,b,c)=>{v=Math.max(0,Number(v)||0);return v<=0?0:v<=a?3:v<=b?5:v<=c?10:15};\n"
helpers = r"""const damageAliasPct=(raw,key)=>{const v=deep(raw,key),n=Number(v);return v!==undefined&&v!==null&&Number.isFinite(n)?Math.max(0,n):0};
const damageRawPct=(raw,...keys)=>{for(const key of keys){const v=deep(raw,key),n=Number(v);if(v!==undefined&&v!==null&&Number.isFinite(n))return Math.max(0,n<=1.0001?n*100:n)}return 0};
const truckDamageParts=raw=>({engine:Math.max(damageAliasPct(raw,'truck_engine_damage_pct'),damageRawPct(raw,'truck.wearEngine','truck.engineWear','truck.engineDamage')),transmission:Math.max(damageAliasPct(raw,'truck_transmission_damage_pct'),damageRawPct(raw,'truck.wearTransmission','truck.transmissionWear','truck.transmissionDamage')),cabin:Math.max(damageAliasPct(raw,'truck_cabin_damage_pct'),damageRawPct(raw,'truck.wearCabin','truck.cabinWear','truck.cabinDamage')),chassis:Math.max(damageAliasPct(raw,'truck_chassis_damage_pct'),damageRawPct(raw,'truck.wearChassis','truck.chassisWear','truck.chassisDamage')),wheels:Math.max(damageAliasPct(raw,'truck_wheels_damage_pct'),damageRawPct(raw,'truck.wearWheels','truck.wheelsWear','truck.wheelsDamage'))});
const truckDamageOf=raw=>{const p=truckDamageParts(raw);return Math.max(damageAliasPct(raw,'truck_damage_pct'),p.engine,p.transmission,p.cabin,p.chassis,p.wheels)};
const trailerDamageOf=raw=>Math.max(damageAliasPct(raw,'trailer_damage_pct'),damageRawPct(raw,'trailers.0.wearChassis'),damageRawPct(raw,'trailers.0.wearWheels'),damageRawPct(raw,'trailers.0.wearBody'));
const damageDelta=(max,start)=>Math.max(0,(Number(max)||0)-(Number(start)||0));
"""
if 'const truckDamageParts=' not in s:
    if score_anchor not in s:
        raise SystemExit('helper scoreTier nao encontrado; confirme a ordem dos patches')
    s = s.replace(score_anchor, score_anchor + helpers, 1)

# Registra o dano que o caminhao ja tinha quando a tentativa comeca e o maior
# valor observado durante a viagem. Os componentes sao acompanhados separadamente
# para detectar dano novo mesmo quando outro componente ja era o maior no inicio.
delivered_marker = " const delivered=bool(raw,'gameplay.jobDelivered','jobDelivered');"
tracking = r""" const truckNow=truckDamageOf(raw),truckPartsNow=truckDamageParts(raw),trailerNow=trailerDamageOf(raw);
 if(f.on_job&&m.state==='active'){
   let damageChanged=false;
   const initDamage=(key,value)=>{if(!Number.isFinite(Number(m[key]))){m[key]=Math.max(0,Number(value)||0);damageChanged=true}};
   const maxDamage=(key,value)=>{const v=Math.max(0,Number(value)||0),old=Number(m[key]);if(!Number.isFinite(old)||v>old+0.0001){m[key]=Math.max(v,Number.isFinite(old)?old:0);damageChanged=true}};
   initDamage('truck_damage_start_pct',truckNow);
   initDamage('truck_engine_damage_start_pct',truckPartsNow.engine);
   initDamage('truck_transmission_damage_start_pct',truckPartsNow.transmission);
   initDamage('truck_cabin_damage_start_pct',truckPartsNow.cabin);
   initDamage('truck_chassis_damage_start_pct',truckPartsNow.chassis);
   initDamage('truck_wheels_damage_start_pct',truckPartsNow.wheels);
   initDamage('trailer_damage_start_pct',trailerNow);
   maxDamage('truck_damage_max_pct',truckNow);
   maxDamage('truck_engine_damage_max_pct',truckPartsNow.engine);
   maxDamage('truck_transmission_damage_max_pct',truckPartsNow.transmission);
   maxDamage('truck_cabin_damage_max_pct',truckPartsNow.cabin);
   maxDamage('truck_chassis_damage_max_pct',truckPartsNow.chassis);
   maxDamage('truck_wheels_damage_max_pct',truckPartsNow.wheels);
   maxDamage('trailer_damage_max_pct',trailerNow);
   if(damageChanged)await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
 }
"""
if 'const truckNow=truckDamageOf(raw)' not in s:
    if delivered_marker not in s:
        raise SystemExit('ponto de rastreio de dano nao encontrado')
    s = s.replace(delivered_marker, tracking + delivered_marker, 1)

# Substitui o calculo final de dano gerado por apply-gat-points.py.
# Regra oficial:
# - carga: faixa do dano final da carga;
# - caminhao: MAIOR aumento entre motor/cambio/cabine/chassi/rodas (nao soma);
# - reboque: registrado para auditoria, sem penalidade enquanto nao houver regra.
calc_pattern = re.compile(r" const damageRaw=.*?auditData=\{.*?gat_points:gatPoints\};\n")
calc_new = r""" const damageEventRaw=Math.max(0,Number(details.cargoDamage)||0),damageEventPct=damageEventRaw<=1.0001?damageEventRaw*100:damageEventRaw,damage=Math.max(damageEventPct,damageAliasPct(raw,'cargo_damage_pct')),
 truckStart=Math.max(0,Number(m.truck_damage_start_pct)||0),truckMax=Math.max(truckStart,Number(m.truck_damage_max_pct)||0,truckNow),
 engineStart=Math.max(0,Number(m.truck_engine_damage_start_pct)||0),engineMax=Math.max(engineStart,Number(m.truck_engine_damage_max_pct)||0,truckPartsNow.engine),engineDelta=damageDelta(engineMax,engineStart),
 transmissionStart=Math.max(0,Number(m.truck_transmission_damage_start_pct)||0),transmissionMax=Math.max(transmissionStart,Number(m.truck_transmission_damage_max_pct)||0,truckPartsNow.transmission),transmissionDelta=damageDelta(transmissionMax,transmissionStart),
 cabinStart=Math.max(0,Number(m.truck_cabin_damage_start_pct)||0),cabinMax=Math.max(cabinStart,Number(m.truck_cabin_damage_max_pct)||0,truckPartsNow.cabin),cabinDelta=damageDelta(cabinMax,cabinStart),
 chassisStart=Math.max(0,Number(m.truck_chassis_damage_start_pct)||0),chassisMax=Math.max(chassisStart,Number(m.truck_chassis_damage_max_pct)||0,truckPartsNow.chassis),chassisDelta=damageDelta(chassisMax,chassisStart),
 wheelsStart=Math.max(0,Number(m.truck_wheels_damage_start_pct)||0),wheelsMax=Math.max(wheelsStart,Number(m.truck_wheels_damage_max_pct)||0,truckPartsNow.wheels),wheelsDelta=damageDelta(wheelsMax,wheelsStart),
 aggregateTruckDelta=damageDelta(truckMax,truckStart),truckDamage=Math.max(aggregateTruckDelta,engineDelta,transmissionDelta,cabinDelta,chassisDelta,wheelsDelta),
 trailerStart=Math.max(0,Number(m.trailer_damage_start_pct)||0),trailerMax=Math.max(trailerStart,Number(m.trailer_damage_max_pct)||0,trailerNow),trailerDelta=damageDelta(trailerMax,trailerStart),
 fines=Math.max(0,Math.trunc(num(details,'speedFines','speed_fines','fines'))),baseXP=Math.floor(distance/100)*20,gatSpeedPenalty=fines*3,gatCargoPenalty=scoreTier(damage,3,7,15),gatTruckPenalty=scoreTier(truckDamage,5,10,20),xpPenalty=gatSpeedPenalty+gatCargoPenalty+gatTruckPenalty,pointPenalty=Math.min(100,xpPenalty),gatPoints=Math.max(0,100-pointPenalty),perfect=damage<=0.5&&truckDamage<=0.5&&fines===0?1:0,bonus=perfect?5:0,penalty=xpPenalty,xp=Math.max(0,baseXP-penalty+bonus),cargo=m.cargo||f.cargo_name||m.custom_cargo||m.title||'Carga',weight=Number(m.weight_kg)||f.mass_kg||0,auditData={base_xp:baseXP,speed_penalty_xp:gatSpeedPenalty,cargo_penalty_xp:gatCargoPenalty,truck_penalty_xp:gatTruckPenalty,perfect_bonus_xp:bonus,cargo_damage_pct:damage,truck_damage_start_pct:truckStart,truck_damage_max_pct:truckMax,truck_damage_delta_pct:truckDamage,truck_overall_delta_pct:aggregateTruckDelta,truck_engine_damage_start_pct:engineStart,truck_engine_damage_max_pct:engineMax,truck_engine_damage_delta_pct:engineDelta,truck_transmission_damage_start_pct:transmissionStart,truck_transmission_damage_max_pct:transmissionMax,truck_transmission_damage_delta_pct:transmissionDelta,truck_cabin_damage_start_pct:cabinStart,truck_cabin_damage_max_pct:cabinMax,truck_cabin_damage_delta_pct:cabinDelta,truck_chassis_damage_start_pct:chassisStart,truck_chassis_damage_max_pct:chassisMax,truck_chassis_damage_delta_pct:chassisDelta,truck_wheels_damage_start_pct:wheelsStart,truck_wheels_damage_max_pct:wheelsMax,truck_wheels_damage_delta_pct:wheelsDelta,trailer_damage_start_pct:trailerStart,trailer_damage_max_pct:trailerMax,trailer_damage_delta_pct:trailerDelta,perfect_trip:!!perfect,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:gatSpeedPenalty,gat_cargo_penalty_points:gatCargoPenalty,gat_truck_penalty_points:gatTruckPenalty,gat_penalty_points:pointPenalty,gat_points:gatPoints};
"""
if 'truck_engine_damage_delta_pct:engineDelta' not in s:
    s, n = calc_pattern.subn(calc_new, s, count=1)
    if n != 1:
        raise SystemExit('calculo final de penalidade nao encontrado')

s, n = re.subn(r"const VERSION='[0-9.]+-cloudflare';", "const VERSION='1.0.50-cloudflare';", s, count=1)
if n != 1:
    raise SystemExit('constante VERSION nao encontrada')

required = [
    "const VERSION='1.0.50-cloudflare'",
    "truck_damage_start_pct",
    "truck_damage_max_pct",
    "truck_damage_delta_pct:truckDamage",
    "truck_engine_damage_delta_pct:engineDelta",
    "truck_transmission_damage_delta_pct:transmissionDelta",
    "truck_cabin_damage_delta_pct:cabinDelta",
    "truck_chassis_damage_delta_pct:chassisDelta",
    "truck_wheels_damage_delta_pct:wheelsDelta",
    "trailer_damage_delta_pct:trailerDelta",
    "gatTruckPenalty=scoreTier(truckDamage,5,10,20)",
    "gatCargoPenalty=scoreTier(damage,3,7,15)",
]
for x in required:
    if x not in s:
        raise SystemExit('patch de dano incompleto: ' + x)

p.write_text(s, encoding='utf-8')
print('Danos 1.0.50 aplicados: inicio/maximo por componente, dano novo do caminhao e penalidades oficiais.')
