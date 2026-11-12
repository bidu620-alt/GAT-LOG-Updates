// Diagnostico somente leitura: viagens oficiais de onibus e creditos do @jeanjc.
// Nunca alterar o banco em producao neste script.
import {DatabaseSync} from 'node:sqlite';
import path from 'node:path';
const user='jeanjc';
const dbPath=path.join(process.env.LOCALAPPDATA||'', 'GAT-LOG','Central','central.sqlite');
const db=new DatabaseSync(dbPath,{readOnly:true});
db.exec('PRAGMA query_only=ON');
const parse=x=>{try{return JSON.parse(x||'null')||{}}catch{return{}}};
const clean=x=>{const s=String(x??'').trim();return s.length>160?s.slice(0,160)+'…':s};
const get=(obj,p)=>p.split('.').reduce((v,k)=>v&&typeof v==='object'?v[k]:undefined,obj);
const pick=(obj,fields)=>Object.fromEntries(fields.map(f=>[f,clean(get(obj,f))]).filter(([,v])=>v));
const telemetryFields=['truck.make','truck.manufacturer','truck.brand','truck.model','truck.modelName','truck.model_name','truck.id','truck.unitId','truck.unit_id','truck.vehicleId','truck.vehicle_type','truck.vehicleType','truck.type','truck.economy_brand_key','vehicle_type','vehicle.type','cargo_id','cargo_name','job.cargoId','job.cargoName','job.cargo','job.market','job.marketType','game.connected','gameplay.onJob'];
console.log('CONSULTA SOMENTE LEITURA — BIDULOG — @'+user);
console.log('Banco:',dbPath);
console.log('Data da consulta:',new Date().toISOString());
try {
 const rows=db.prepare("SELECT id,sequence_no,cargo,source,destination,distance_km,delivered_at,raw_json FROM deliveries WHERE user=? ORDER BY delivered_at DESC,id DESC LIMIT 35").all(user);
 const rewardStmt=db.prepare("SELECT gat_points,gold_units,reason FROM eco_rewards WHERE user=? AND delivery_id=?");
 for(const d of rows){
  const raw=parse(d.raw_json),m=raw.mission||{},details=raw.delivery_details||{},audit=raw.audit||{};
  let reward=null;try{reward=rewardStmt.get(user,d.id)}catch{}
  console.log(JSON.stringify({
   delivery_id:d.id,date:d.delivered_at,cargo:d.cargo,route:[d.source,d.destination],distance_km:d.distance_km,
   mission:pick(m,['cargo','cargo_name','cargo_id','vehicle_type','job_market','work_id','id','state']),
   receipt:pick(details,['cargo','cargoName','cargoId','truckMake','truckModel','truckId','vehicleType','type','jobType']),
   raw_vehicle:pick(raw,telemetryFields),
   audit:pick(audit,['cargo_mode','gat_points','rank_eligible','ranking_reason']),
   gold:reward?{gat_points:reward.gat_points,gold_units:reward.gold_units,reason:reward.reason}:null
  }));
 }
 const tel=db.prepare("SELECT updated_at,telemetry_json FROM telemetry_live WHERE account_user=? ORDER BY updated_at DESC LIMIT 1").get(user);
 if(tel){
  const raw=parse(tel.telemetry_json);
  console.log('ULTIMA TELEMETRIA: '+JSON.stringify({at:tel.updated_at,fields:pick(raw,telemetryFields),truckKeys:Object.keys(raw.truck||{}),jobKeys:Object.keys(raw.job||{})}));
 }else console.log('Nenhuma telemetria ativa para o motorista.');
}catch(e){console.error('FALHA NA CONSULTA:',e.message);process.exitCode=1;}
finally{db.close();}
