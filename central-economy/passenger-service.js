const text=v=>{if(typeof v!=='string')return '';try{if(v.startsWith('"'))v=JSON.parse(v)}catch{return ''}return v.trim().slice(0,120)};
export function observedPassengerService(raw,at){
 const native=raw?.bus_job;
 const b=native&&(native.active===true||native.onJob===true)?native:(raw?.job?.source_kind||raw?.job?.sourceKind)==='bus_job'?raw.job:null;
 if(b&&(b.active===true||b.onJob===true)){
  const origin=text(b.sourceCity||b.source_city),destination=text(b.destinationCity||b.destination_city);
  if(origin&&destination)return {source:'sdk',origin,destination,service_id:text(b.id||b.jobId)};
 }
 const e=raw?.gat_save_evidence,job=e?.jobs?.current_bus_job;
 if(e?.status!=='snapshot_recent'||e?.profile_id!==raw.gat_profile_id||e?.job_kind!=='bus_job'||!job||e.vehicle_mode!=='bus'||!e.save?.game_sha256||Number(e.snapshot_age_seconds)>120)return null;
 const age=(Date.parse(at)-Date.parse(e.save.game_modified_at))/1000;
 if(!Number.isFinite(age)||age< -30||age>120||e.transport_truncated===true)return null;
 const fields=job.fields||{},refs=Array.isArray(fields.stops)?fields.stops:[],units=new Map((e.related_units||[]).map(u=>[u.id,u]));
 const cities=refs.map(ref=>text(units.get(ref)?.fields?.city)).filter(Boolean);
 const origin=text(fields.source_city)||cities[0],destination=text(fields.target_city||fields.destination_city)||cities.at(-1);
 if(!origin||!destination||origin===destination)return null;
 return {source:'save',origin,destination,service_id:text(job.id)};
}
