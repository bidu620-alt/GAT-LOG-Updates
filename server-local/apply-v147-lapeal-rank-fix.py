from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
rank=root/'ranking-telemetry.js'
host=root/'host.mjs'

# 1) O primeiro pacote de uma nova carga do TruckSim pode chegar com a carga nova,
# mas ainda com aliases de dano/navegacao da transicao anterior. 30 s foi curto no
# caso real do Lapeal. Mantemos a exigencia de duas leituras completas e continuas,
# apenas ampliando a janela de aquecimento para 120 s. Depois de verificada, gaps e
# falta de dano continuam sticky como antes.
s=rank.read_text(encoding='utf-8')
old="export const RANK_STARTUP_GRACE_MS = 30000;"
new="export const RANK_STARTUP_GRACE_MS = 120000;"
if old not in s:
    raise SystemExit('Nao encontrei a janela inicial de 30 s do ranking.')
s=s.replace(old,new,1)

# Uma instalacao antiga do TruckSim costuma enviar cargo/reboque, mas nao envia nenhum
# dos cinco componentes de dano do caminhao. Isso nao e pacote transitorio: a viagem
# deve continuar bloqueada mesmo se o motorista atualizar o plugin no meio da rota.
# Ja um pacote de troca de carga que perde apenas um ou alguns aliases pode se recuperar
# dentro dos 120 s e ainda precisa de duas amostras completas consecutivas.
startup_anchor="""  if (!next.verified_at) {
    const startupTime = Date.parse(next.startup_started_at || at);
"""
startup_new="""  if (!next.verified_at) {
    const missingNow = Array.isArray(readiness?.missing_damage) ? readiness.missing_damage : [];
    const truckMissing = ['engine','transmission','cabin','chassis','wheels'];
    const oldTruckSimDamagePlugin = readiness?.reason === 'damage_data_incomplete' &&
      truckMissing.every(name => missingNow.includes(name)) &&
      !missingNow.includes('cargo') && !missingNow.includes('trailer');
    if (oldTruckSimDamagePlugin) {
      next.incompatible_damage_plugin = true;
      next.last_invalid_reason = 'damage_data_incomplete';
      if (!next.startup_failed_at) next.startup_failed_at = at;
    }
    if (next.incompatible_damage_plugin) {
      next.reason = 'damage_data_incomplete';
      next.valid_samples = 0;
      next.last_sample_at = at;
      return next;
    }
    const startupTime = Date.parse(next.startup_started_at || at);
"""
if startup_anchor not in s:
    raise SystemExit('Nao encontrei o bloco inicial do rank guard.')
s=s.replace(startup_anchor,startup_new,1)

# Preserve o primeiro instante da falha para diagnostico; nao reescreva a cada pacote.
s=s.replace("next.startup_failed_at = at;\n      next.last_sample_at = at;",
            "if (!next.startup_failed_at) next.startup_failed_at = at;\n      next.last_sample_at = at;",1)
rank.write_text(s,encoding='utf-8')

# 2) Reparo idempotente da entrega confirmada do Lapeal. Evidencias recuperadas da
# propria telemetria ao iniciar a viagem seguinte:
#   receipt: distanceKm=1135, cargoDamage=0, revenue=57672, earnedXp=1637,
#            deliveryTime=63, autoParked=false, autoLoaded=true.
# A tela ao vivo anterior confirmou carga/rota/peso: Mower Conditioner Krone BiG M 450,
# Malaga -> A Coruna, 15,5 t. Nao concedemos bonus de viagem perfeita nem penalidade de
# dano de caminhao porque o delta inicial/final da viagem perdida nao ficou persistido.
h=host.read_text(encoding='utf-8')
anchor="""function reconcileMonthlyTripGoal(db){
  // A meta mensal e por VIAGENS VALIDAS, nao por classificacoes. Como toda entrega
"""
if anchor not in h:
    raise SystemExit('Nao encontrei reconcileMonthlyTripGoal no host local.')
repair=r'''export function repairLapealMowerDelivery(db){
  const marker='repair_lapeal_mower_2026_09_03_v1';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(marker))return false;
  const user='lapeal67',cargo='Mower Conditioner Krone BiG M 450',source='Málaga',destination='A Coruña';
  if(!db.sql.prepare('SELECT 1 FROM profiles WHERE user=?').get(user))return false;
  const existing=db.sql.prepare(`SELECT id FROM deliveries WHERE user=? AND lower(cargo)=lower(?)
    AND source=? AND destination=? AND substr(delivered_at,1,7)='2026-09' LIMIT 1`).get(user,cargo,source,destination);
  const recordedAt='2026-09-03T01:14:44.000Z',repairAt=new Date().toISOString();
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    if(!existing){
      const distance=1135,weight=15500,xp=220,gatPoints=100;
      const receipt={revenue:57672,earnedXp:1637,cargoDamage:0,distanceKm:1135,deliveryTime:63,autoParked:false,autoLoaded:true};
      const mission={id:'repair-2026-09-lapeal67-mower-big-m-450',catalog_id:'__official_cargo__',title:cargo,category:'Trator e máquinas agrícolas',state:'repaired',cargo,source,destination,weight_kg:weight,planned_distance_km:distance,map_mode:'base',classification_mode:'official_cargo_repair'};
      const audit={base_xp:220,speed_penalty_xp:0,cargo_penalty_xp:0,truck_penalty_xp:0,perfect_bonus_xp:0,cargo_damage_pct:0,truck_damage_delta_pct:null,perfect_trip:false,xp_awarded:xp,gat_base_points:100,gat_speed_penalty_points:0,gat_cargo_penalty_points:0,gat_truck_penalty_points:0,gat_penalty_points:0,gat_points:gatPoints,rank_verified:true,repair_verified_receipt:true};
      const raw=JSON.stringify({mission,delivery_details:receipt,audit,repair:{reason:'missed_delivery_recovered_from_next_job_receipt',evidence:'gat_telemetry_delivery_details_start',repaired_at:repairAt,recorded_delivery_at:recordedAt}});
      db.sql.prepare('INSERT INTO deliveries(user,sequence_no,source,destination,cargo,weight_kg,distance_km,xp,perfect,penalty_xp,speed_fines,delivered_at,raw_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)').run(user,null,source,destination,cargo,weight,distance,xp,0,0,0,recordedAt,raw);
      db.sql.prepare(`UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,updated_at=? WHERE user=?`).run(distance,xp,gatPoints,repairAt,user);
      db.sql.prepare('INSERT OR IGNORE INTO routes_completed(user,month_key,route_key,source,destination,completed_at) VALUES(?,?,?,?,?,?)').run(user,'2026-09','malaga>a coruna',source,destination,recordedAt);
      db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(repairAt,'system','repair_missed_delivery',user,JSON.stringify({cargo,source,destination,distance_km:distance,weight_kg:weight,gat_points:gatPoints,xp,receipt}));
    }
    db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(marker,repairAt);
    db.sql.exec('COMMIT');
    return !existing;
  }catch(e){
    try{db.sql.exec('ROLLBACK')}catch{}
    throw e;
  }
}

'''
h=h.replace(anchor,repair+anchor,1)
call="""  await backupNow();const backups=setInterval(backupNow,6*3600000);
"""
replace="""  await backupNow();
  const repaired=await exclusive(async()=>{const changed=repairLapealMowerDelivery(db);if(changed)reconcileMonthlyTripGoal(db);return changed;});
  if(repaired)console.log('GAT 1.0.47: entrega Mower Conditioner do lapeal67 recuperada no ranking.');
  const backups=setInterval(backupNow,6*3600000);
"""
if call not in h:
    raise SystemExit('Nao encontrei o primeiro backup do host para aplicar o reparo com seguranca.')
h=h.replace(call,replace,1)
host.write_text(h,encoding='utf-8')

for marker in ['RANK_STARTUP_GRACE_MS = 120000','incompatible_damage_plugin','repairLapealMowerDelivery','repair_lapeal_mower_2026_09_03_v1','Mower Conditioner Krone BiG M 450','distance=1135','gatPoints=100']:
    body=(rank.read_text(encoding='utf-8')+'\n'+host.read_text(encoding='utf-8'))
    if marker not in body: raise SystemExit('Patch 1.0.47 incompleto: '+marker)
print('GAT 1.0.47 preparado: startup de 120 s sem liberar TruckSim antigo + reparo do Lapeal.')
