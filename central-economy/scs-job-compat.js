// Canonical, additive support for SCS configuration id "bus_job" (Coaches).
// Runs on a view of authenticated telemetry; NEVER modify the signed packet
// before the journal's SHA/HMAC verification.
const obj=x=>x&&typeof x==='object'&&!Array.isArray(x)?x:{};
const text=x=>typeof x==='string'?x.trim():'';
const first=(...a)=>a.map(text).find(Boolean)||'';
const field=(o,...names)=>{for(const k of names){const v=o[k];if(v!==null&&v!==undefined&&v!=='')return v}return undefined};
const distance=(o,...keys)=>{const n=Number(field(o,...keys));return Number.isFinite(n)&&n>=0?n:undefined};
const location=(o,name)=>first(field(o,name+'City',name+'_city',name+'CityName',name+'_city_name'),o[name]?.city);
const knownKind=x=>{
 const s=text(x).toLowerCase();
 return s==='bus_job'||s==='job'||s==='car_job'?s:'';
};

// Keep manufacturer entitlement separate from the kind of transport. Names of
// models alone are never evidence that passengers were carried.
export function passengerEvidence(raw){
 const r=obj(raw),truck=obj(r.truck),kind=scsJobKind(r);
 return {
  schema:1,job_kind:kind,
  vehicle:{make:first(truck.make,truck.manufacturer,truck.brand),model:first(truck.model,truck.modelName,truck.model_name),id:first(truck.id,truck.unitId,truck.vehicleId)},
  classification:kind==='bus_job'?'passenger_official':'unconfirmed',
  service_id:null, // Quick/Contract/Tourist IDs require real provider evidence.
  configuration:kind==='bus_job'?obj(r.bus_job??r.job):{},
  sdk_event:first(r.gat_scs_event,r.bus_job_event,obj(r.gameplay).busJobEvent)
 };
}

export function scsJobKind(raw){
 const t=obj(raw);
 const job=obj(t.job);
 const kind=knownKind(first(
  t.gat_scs_job_kind,t.gat_job_kind,t.job_source_kind,
  job.source_kind,job.sourceKind,t.scs_configuration_id,t.scs_job_configuration
 ));
 const sdkEvent=first(t.gat_scs_event,t.bus_job_event,obj(t.gameplay).busJobEvent).toLowerCase();
 if(sdkEvent==='bus_job.delivered'||sdkEvent==='bus_job.cancelled')return 'bus_job';
 const bus=obj(t.bus_job);
 const hasBusProof=(bus.active===true||bus.onJob===true)&&
  (location(bus,'source')||location(bus,'destination'));
 const truck=job.active===true||job.onJob===true||location(job,'source')&&location(job,'destination');
 // Some SDK adapters emit an empty "job" alongside a real "bus_job".
 // A stale freight source_kind must not conceal the active passenger job.
 if(kind==='job'&&hasBusProof&&!truck)return 'bus_job';
 if(kind)return kind;
 return hasBusProof&&!truck?'bus_job':'job';
}

export function normalizeSCSJob(raw){
 if(!raw||typeof raw!=='object'||Array.isArray(raw))return raw;
 if(scsJobKind(raw)!=='bus_job')return raw;

 const b=obj(raw.bus_job),j=obj(raw.job),gameplay=obj(raw.gameplay);
 // Keep a job already carrying the bus_job source_kind, otherwise use the
 // SCS bus_job configuration, not an empty legacy "job" configuration.
 const job=knownKind(first(j.source_kind,j.sourceKind))==='bus_job'?j:
   (Object.keys(b).length?b:j);
 const active=typeof job.active==='boolean'?job.active:
  typeof job.onJob==='boolean'?job.onJob:
  typeof gameplay.busJobActive==='boolean'?gameplay.busJobActive:
  typeof raw.bus_on_job==='boolean'?raw.bus_on_job:
  typeof gameplay.onJob==='boolean'?gameplay.onJob:
  raw.on_job===true;
 const source=location(job,'source')||location(b,'source');
 const destination=location(job,'destination')||location(b,'destination');
 const sourceId=first(field(job,'sourceCityId','source_city_id'),field(b,'sourceCityId','source_city_id'));
 const destinationId=first(field(job,'destinationCityId','destination_city_id'),field(b,'destinationCityId','destination_city_id'));
 const planned=distance(job,'plannedDistanceKm','planned_distance_km')??distance(b,'plannedDistanceKm','planned_distance_km');
 const delivery=obj(field(gameplay,'busJobDeliveredDetails','bus_job_delivered_details')??field(raw,'bus_job_delivered_details')??field(b,'deliveredDetails','delivery_details'));
 const receiptDistance=distance(delivery,'distanceKm','distance_km');
 const receipt=Object.keys(delivery).length&&receiptDistance!==undefined&&receiptDistance>0?{
  revenue:field(delivery,'revenue','income')??0,
  earnedXp:field(delivery,'earnedXp','earned_xp')??0,
  distanceKm:receiptDistance,
  deliveryTime:field(delivery,'deliveryTime','delivery_time')??0
 }:null;
 const event=first(raw.gat_scs_event,raw.bus_job_event,gameplay.busJobEvent).toLowerCase();
 const terminal=event==='bus_job.delivered'||event==='bus_job.cancelled';
 const normalizedEvent=event==='bus_job.delivered'?'delivered':
  event==='bus_job.cancelled'?'cancelled':undefined;
 const hasStartEvidence=!!(source&&destination);
 const isInJob=active===true&&hasStartEvidence&&obj(raw.game).connected===true;
 const n={
  ...raw,
  gat_scs_job_kind:'bus_job',
  gat_job_kind:'bus_job',
  gat_transport_evidence:passengerEvidence(raw),
  on_job:isInJob,
  // There is no freight cargo id or weight for passenger transport.
  cargo_name:isInJob?'Passageiros':'',
  cargo_id:'',
  mass_kg:0,
  job:{
   ...j,...job,
   source_kind:'bus_job',
   active:isInJob,
   cargo:isInJob?'Passageiros':'',
   cargoId:'',
   cargoMass:0,
   sourceCity:source,
   destinationCity:destination,
   sourceCityId:sourceId,
   destinationCityId:destinationId,
   ...(planned!==undefined?{plannedDistanceKm:planned}:{})
  },
  ...(isInJob?{
   source_city:source,destination_city:destination,
   source_city_id:sourceId,destination_city_id:destinationId,
   ...(planned!==undefined?{planned_distance_km:planned}:{})
  }:{}),
  gameplay:{
   ...gameplay,
   onJob:isInJob,
   // Legacy freight flags/receipts can linger after changing game mode.
   jobDelivered:false,jobFinished:false,jobCancelled:false,
   jobDeliveredDetails:receipt||{},
  },
  ...(normalizedEvent?{gat_job_event:normalizedEvent}:{})
 };
 // Preserve the real SDK event even if the game gives no receipt: we must
 // NEVER invent jobDeliveredDetails or count a trip on an event flag alone.
 if(terminal&&normalizedEvent==='delivered'){
  n.gameplay.jobDelivered=Boolean(receipt);
 }
 if(terminal&&normalizedEvent==='cancelled'){
  n.gameplay.jobCancelled=true;
 }
 return n;
}
