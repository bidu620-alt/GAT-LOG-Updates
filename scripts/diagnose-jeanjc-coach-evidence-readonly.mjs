// Diagnóstico da marca registrada nas viagens de @jeanjc.
// SQLite exclusivamente em modo somente leitura; nenhuma mudança de banco.
import {DatabaseSync} from 'node:sqlite';
import path from 'node:path';
const target=path.join(process.env.LOCALAPPDATA||'', 'GAT-LOG','Central','central.sqlite');
const db=new DatabaseSync(target,{readOnly:true});
db.exec('PRAGMA query_only=ON');
const user='jeanjc';
const parse=v=>{try{return JSON.parse(v||'{}')||{}}catch{return{}}};
const text=v=>String(v??'').slice(0,120);
console.log('DIAGNOSTICO MARCA POR VIAGEM (SOMENTE LEITURA): '+user);
try{
 const deliveries=db.prepare("SELECT id,delivered_at,cargo,raw_json FROM deliveries WHERE user=? ORDER BY delivered_at DESC,id DESC LIMIT 15").all(user);
 const journey=db.prepare("SELECT evidence_json FROM eco_journeys WHERE user=? AND mission_id=?");
 const reward=db.prepare("SELECT gold_units,gat_points,reason,mission_id FROM eco_rewards WHERE user=? AND delivery_id=?");
 for(const d of deliveries){
  const raw=parse(d.raw_json),mission=raw.mission||{},e=parse(journey.get(user,String(mission.id||''))?.evidence_json),r=reward.get(user,d.id);
  console.log(JSON.stringify({id:d.id,at:d.delivered_at,cargo:text(d.cargo),
   mission_id:text(mission.id),mission_keys:Object.keys(mission),
   economy_brand:e.brand||null,economy_reason:e.reason||null,economy_samples:e.samples||null,
   gold_units:r?.gold_units??null,gold_reason:r?.reason||null,
   brand_mismatch_note:"O modelo do veiculo de cada viagem nao esta confirmado por estes dados."
  }));
 }
 const current=db.prepare("SELECT updated_at,telemetry_json FROM telemetry_live WHERE account_user=? ORDER BY updated_at DESC LIMIT 1").get(user);
 if(current){const t=parse(current.telemetry_json);console.log('TELEMETRIA ATUAL: '+JSON.stringify({at:current.updated_at,make:t?.truck?.make||null,model:t?.truck?.model||null,id:t?.truck?.id||null,vehicle_type:t?.truck?.vehicle_type||t?.truck?.type||null,cargo_id:t?.job?.cargoId||null,cargo:t?.job?.cargo||null,job_market:t?.job?.jobMarket||null,on_job:t?.gameplay?.onJob??null}));}
}catch(err){console.error('ERRO:',err.message);process.exitCode=1}finally{db.close()}
