// The published season is immutable: later layout releases must not reset its dates.
const fail=(status,code)=>{throw Object.assign(new Error(code),{status})};
export const MONTHLY_PASS=Object.freeze({id:'2026-11',product_id:'pass_2026_11',title:'Passe BiduLog • Novembro 2026',starts_at:'2026-11-01T00:00:00-03:00',ends_at:'2026-11-30T23:50:00-03:00',timezone:'America/Sao_Paulo',price_gold:2000,price_brl:5,rewards_defined:false});
// Explicit game cargo IDs from the installed ETS2 cargo catalog. Never classify by
// fuzzy/localized display names, trailer appearance, or the last 100 profile trips.
import {getCargoIds, classifyCargoId} from './cargo-library/index.js';
export const PASS_CARGO_IDS=Object.freeze(Object.fromEntries(
 ['cegonheiro','madeireiro','frigorifico','carga_seca','maquinario','agronegocio','granel','conteineres']
  .map(category=>[category,getCargoIds(category)])
));
const GOALS=Object.freeze([
 {id:'rodagem_bruta',title:'Rodagem Bruta',target:10000,unit:'km'},
 {id:'peso_pesado',title:'Peso Pesado',target:3,unit:'cargas',weight_over_kg:40000},
 {id:'cegonheiro',title:'Cegonheiro',target:3,unit:'cargas'},
 {id:'madeireiro',title:'Madeireiro',target:3,unit:'cargas'},
 {id:'frigorifico',title:'Frigorífico',target:3,unit:'cargas'},
 {id:'carga_seca',title:'Carga Seca / Geral',target:3,unit:'cargas'},
 {id:'maquinario',title:'Maquinário & Construção',target:3,unit:'cargas'},
 {id:'agronegocio',title:'Agronegócio',target:3,unit:'cargas'},
 {id:'granel',title:'Granel & Mineração',target:3,unit:'cargas'},
 {id:'conteineres',title:'Contêineres',target:3,unit:'cargas'}
]);
const START=Date.parse(MONTHLY_PASS.starts_at),END=Date.parse(MONTHLY_PASS.ends_at);
export function monthlyPassConfig(at=Date.now()){
 const phase=at<START?'presale':at<END?'active':'closed';
 return {...MONTHLY_PASS,phase,purchase_available:at<END,goals:GOALS.map(g=>({...g})),server_time:new Date(at).toISOString()};
}
export function monthlyPassProduct(){return {id:MONTHLY_PASS.product_id,kind:'pass',title:MONTHLY_PASS.title,description:'Passe mensal com 10 metas de transporte. Válido de 01/11/2026 às 00h00 a 30/11/2026 às 23h50, horário de Brasília. Pré-venda; sem renovação automática. Recompensas ainda não definidas.',gold_units:200000,cash_centavos:500,bcoin_units:0,starts_at:MONTHLY_PASS.starts_at,ends_at:MONTHLY_PASS.ends_at};}
export function assertMonthlyPassPurchase(product,at=Date.now()){
 if(product?.id===MONTHLY_PASS.product_id&&at>=END)fail(409,'pass_closed');
}
export function monthlyPassPaymentReason(product,approvedAt){
 if(product?.id!==MONTHLY_PASS.product_id)return null;
 const at=Date.parse(approvedAt||'');return !Number.isFinite(at)?'pass_approval_time_missing':at>=END?'pass_payment_after_close':null;
}
function parse(raw){try{return JSON.parse(raw||'{}')||{}}catch{return {}}}
function cargoId(row,raw){return String(raw?.cargo_identity?.cargo_id_raw||raw?.cargo_identity?.cargo_id||raw?.mission?.cargo_id||raw?.cargo_id||'').trim().toLowerCase().replace(/^cargo\./,'');}
function complete(raw){return ![raw?.state,raw?.status,raw?.mission?.state].some(v=>['cancelled','canceled','rejected','failed','assigned','pending'].includes(String(v||'').trim().toLowerCase()))&&raw?.completed!==false&&raw?.delivered!==false;}
export function monthlyPassState(db,s,month=MONTHLY_PASS.id,at=Date.now()){
 if(month!==MONTHLY_PASS.id)fail(404,'pass_not_found');
 const pass=monthlyPassConfig(at),entitlement=db.sql.prepare('SELECT acquired_at FROM eco_entitlements WHERE user=? AND product_id=?').get(s.user,MONTHLY_PASS.product_id);
 const values=Object.fromEntries(GOALS.map(g=>[g.id,0]));let totalKm=0,totalDeliveries=0;
 if(entitlement&&at>=START){
  // Restrict the COMPLETE time window on the server before reading or aggregating.
  // julianday handles stored UTC and explicit-offset dates equally. No recent cap.
  const rows=db.sql.prepare(`SELECT id,weight_kg,distance_km,raw_json FROM deliveries WHERE user=?
    AND julianday(delivered_at)>=julianday(?) AND julianday(delivered_at)<julianday(?)
    AND julianday(delivered_at)<=julianday(?) ORDER BY delivered_at,id`).all(s.user,MONTHLY_PASS.starts_at,MONTHLY_PASS.ends_at,new Date(at).toISOString());
  for(const row of rows){
   const raw=parse(row.raw_json);if(!complete(raw))continue;
   const distance=Number(row.distance_km);if(!Number.isFinite(distance)||distance<=0)continue;
   totalDeliveries++;totalKm+=distance;
   const weight=Number(row.weight_kg);if(Number.isFinite(weight)&&weight>40000)values.peso_pesado++;
   const id=cargoId(row,raw);if(!id)continue;
   // Category lists are disjoint. A delivery may add distance, heavy weight, and
   // ONE cargo category; unknown IDs still add legitimate monthly kilometers.
   const category=classifyCargoId(id);if(category&&Object.hasOwn(PASS_CARGO_IDS,category))values[category]++;
  }
 }
 values.rodagem_bruta=Math.round(totalKm*100)/100;
 const goals=GOALS.map(g=>({...g,current:Math.min(g.target,values[g.id]),completed:values[g.id]>=g.target}));
 return {ok:true,pass,owned:!!entitlement,acquired_at:entitlement?.acquired_at||null,rewards_defined:false,progress:{goals,completed_goals:goals.filter(g=>g.completed).length,total_goals:10,total_km:Math.round(totalKm*100)/100,total_deliveries:totalDeliveries,computed_at:new Date(at).toISOString()}};
}
