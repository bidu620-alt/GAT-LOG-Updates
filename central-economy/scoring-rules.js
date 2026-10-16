export const GAT_SCORING_V2_START_ISO = '2026-10-01T03:00:00.000Z';
export const GAT_SCORING_V2_START_LOCAL = '2026-10-01T00:00:00-03:00';
export const GAT_TIMEZONE = 'America/Sao_Paulo';

const deep=(o,p)=>String(p).split('.').reduce((v,k)=>v&&typeof v==='object'?v[k]:undefined,o);
const pick=(o,...ps)=>{for(const p of ps){const v=deep(o,p);if(v!==undefined&&v!==null)return v}};
const bool=(o,...ps)=>{const v=pick(o,...ps);return v===true||v===1||String(v).toLowerCase()==='true'};
const clean=v=>String(v??'').trim();
const norm=v=>clean(v).normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,'_').replace(/^_+|_+$/g,'');
const scoreTier=(v,a,b,c)=>{v=Math.max(0,Number(v)||0);return v<=0?0:v<=a?3:v<=b?5:v<=c?10:15};

export function isScoringV2(at){
  const ms=Date.parse(String(at||''));
  return Number.isFinite(ms)&&ms>=Date.parse(GAT_SCORING_V2_START_ISO);
}

export function gatWeightBonus(weightKg,cargoName){
  const weight=Math.max(0,Number(weightKg)||0),cargo=clean(cargoName);
  if(!cargo||weight<=0)return 0;
  if(weight<=4999)return 1;
  if(weight<=9999)return 2;
  if(weight<=19999)return 3;
  if(weight<=29999)return 4;
  if(weight<=39999)return 6;
  return 8;
}

export function isSpeedFineOffence(offence){
  const key=norm(offence);
  return /speed|veloc/.test(key);
}

function fineDetails(raw){
  const d=pick(raw,'finedDetails','gameplay.finedDetails','game.finedDetails','events.finedDetails');
  return d&&typeof d==='object'&&!Array.isArray(d)?d:{};
}

export function updateMissionFineTracker(mission,raw,at){
  if(!mission||typeof mission!=='object')return false;
  const active=bool(raw,'fined','gameplay.fined','game.fined','events.fined');
  const details=fineDetails(raw);
  const offence=clean(details.offence??details.offense??details.type??details.reason);
  const amountRaw=Number(details.amount??details.value??details.fineAmount);
  const amount=Number.isFinite(amountRaw)?amountRaw:0;
  const signature=`${norm(offence)||'unknown'}|${amount}`;
  const latched=mission.gat_fine_latched===true;
  let changed=false;

  if(active){
    if(!latched){
      mission.gat_fine_count=Math.max(0,Math.trunc(Number(mission.gat_fine_count)||0))+1;
      if(isSpeedFineOffence(offence))mission.gat_speed_fine_count=Math.max(0,Math.trunc(Number(mission.gat_speed_fine_count)||0))+1;
      else mission.gat_other_fine_count=Math.max(0,Math.trunc(Number(mission.gat_other_fine_count)||0))+1;
      const events=Array.isArray(mission.gat_fine_events)?mission.gat_fine_events.slice(-19):[];
      events.push({at:String(at||''),offence:offence||'unknown',amount});
      mission.gat_fine_events=events;
      mission.gat_last_fine_signature=signature;
      mission.gat_last_fine_at=String(at||'');
      changed=true;
    }
    if(!latched){mission.gat_fine_latched=true;changed=true;}
  }else if(latched){
    mission.gat_fine_latched=false;
    changed=true;
  }
  return changed;
}

export function fineCountsForDelivery(mission={},details={},useV2=false){
  const receiptSpeed=Math.max(0,Math.trunc(Number(details?.speedFines??details?.speed_fines??details?.fines)||0));
  if(!useV2)return{total:receiptSpeed,speed:receiptSpeed,other:0,receipt_speed:receiptSpeed,observed_total:0};
  const observedSpeed=Math.max(0,Math.trunc(Number(mission?.gat_speed_fine_count)||0));
  const observedOther=Math.max(0,Math.trunc(Number(mission?.gat_other_fine_count)||0));
  const observedTotal=Math.max(observedSpeed+observedOther,Math.max(0,Math.trunc(Number(mission?.gat_fine_count)||0)));
  const speed=Math.max(observedSpeed,receiptSpeed);
  const other=Math.max(observedOther,observedTotal-observedSpeed);
  return{total:speed+other,speed,other,receipt_speed:receiptSpeed,observed_total:observedTotal};
}

export function calculateGatScore({at,distanceKm,weightKg,cargoName,cargoDamagePct,truckDamagePct,trailerDamagePct,deliveryDetails,mission,adminTest=false}){
  const useV2=isScoringV2(at),distance=Math.max(0,Number(distanceKm)||0),weight=Math.max(0,Number(weightKg)||0);
  const cargoDamage=Math.max(0,Number(cargoDamagePct)||0),truckDamage=Math.max(0,Number(truckDamagePct)||0),trailerDamage=Math.max(0,Number(trailerDamagePct)||0);
  const fines=fineCountsForDelivery(mission,deliveryDetails,useV2);
  const basePoints=Math.floor(distance/99)*10;
  const weightBonus=useV2?gatWeightBonus(weight,cargoName):0;
  const finePenalty=adminTest?0:fines.total*3;
  const cargoPenalty=adminTest?0:scoreTier(cargoDamage,3,7,15);
  const vehicleDamage=useV2?Math.max(truckDamage,trailerDamage):truckDamage;
  const vehiclePenalty=adminTest?0:scoreTier(vehicleDamage,5,10,20);
  const penaltyTotal=finePenalty+cargoPenalty+vehiclePenalty;
  const pointPenalty=useV2?penaltyTotal:Math.min(100,penaltyTotal);
  const points=Math.max(0,basePoints+weightBonus-pointPenalty);
  return{
    use_v2:useV2,
    scoring_version:useV2?'2026-10-01-v2':'legacy-v1',
    base_points:basePoints,
    weight_bonus_points:weightBonus,
    fine_count:fines.total,
    speed_fine_count:fines.speed,
    other_fine_count:fines.other,
    receipt_speed_fines:fines.receipt_speed,
    observed_fines:fines.observed_total,
    fine_penalty_points:finePenalty,
    cargo_penalty_points:cargoPenalty,
    vehicle_penalty_points:vehiclePenalty,
    vehicle_damage_pct:vehicleDamage,
    point_penalty_points:pointPenalty,
    points
  };
}
