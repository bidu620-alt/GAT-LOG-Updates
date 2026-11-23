import {classifyVehicle,passengerBrandAccess} from './vehicle-classification.js';
import {observedPassengerService} from './passenger-service.js';
const obj=x=>x&&typeof x==='object'&&!Array.isArray(x)?x:{};
const num=x=>typeof x==='number'&&Number.isFinite(x)?x:null;
const text=x=>typeof x==='string'?x.trim().slice(0,120):'';
export function ensurePassengerSchema(db){db.sql.exec(`
 CREATE TABLE IF NOT EXISTS passenger_trips (
  user TEXT NOT NULL,id TEXT NOT NULL,device_id TEXT NOT NULL,profile_id TEXT NOT NULL,
  state TEXT NOT NULL,started_at TEXT NOT NULL,updated_at TEXT NOT NULL,
  delivery_id INTEGER UNIQUE,details_json TEXT NOT NULL,PRIMARY KEY(user,id));
 CREATE UNIQUE INDEX IF NOT EXISTS passenger_active_user ON passenger_trips(user) WHERE state='active';
 CREATE INDEX IF NOT EXISTS passenger_user_date ON passenger_trips(user,updated_at DESC);
`);}
export function passengerStatus(db,user,id){const row=db.sql.prepare('SELECT state,delivery_id,details_json FROM passenger_trips WHERE user=? AND id=?').get(user,id);if(!row)return null;const s=JSON.parse(row.details_json);return {id,state:row.state,delivery_id:row.delivery_id,distance_km:s.distance_km||0,reason:s.reason||s.pending_reason||null,stops:s.stops||0};}
function save(db,user,s,t){db.sql.prepare(`INSERT INTO passenger_trips(user,id,device_id,profile_id,state,started_at,updated_at,details_json)
 VALUES(?,?,?,?,?,?,?,?) ON CONFLICT(user,id) DO UPDATE SET state=excluded.state,updated_at=excluded.updated_at,details_json=excluded.details_json`).run(user,s.id,s.device_id,s.profile_id,s.state,s.started_at,t,JSON.stringify(s));}
function position(r){const p=obj(r.truck?.placement),x=num(p.x),z=num(p.z);return x===null||z===null?null:{x,z};}
function fingerprint(v){return [v.vehicle_brand,v.vehicle_model,v.vehicle_id,v.license_plate].join('|').toLowerCase();}
export function advancePassenger(db,user,device,signed,verified,t,{minimumKm=200}={}){
 const c=obj(signed.gat_passenger_session);if(!Object.keys(c).length)return {raw:signed};
 ensurePassengerSchema(db);
 const reject=reason=>({raw:null,error:reason,status:{id:text(c.id),state:'rejected',reason,recorded:!!db.sql.prepare('SELECT 1 FROM passenger_trips WHERE user=? AND id=?').get(user,text(c.id))}});
 if(!verified)return reject('passenger_journal_not_verified');
 if(!/^[a-f0-9]{32}$/.test(text(c.id)))return reject('invalid_passenger_id');
 const v=classifyVehicle(signed),profile=typeof signed.gat_profile_id==='string'?signed.gat_profile_id.trim():'',odo=num(signed.truck?.odometer),pos=position(signed),ms=Date.parse(t),speedValue=num(signed.truck?.speed)??num(signed.speed_kmh),speed=Math.abs(speedValue??0);
 if(!/^(?:[a-fA-F0-9]{2}){1,128}$/.test(profile)||!Number.isFinite(ms)||odo===null||odo<0||!pos||speedValue===null)return reject('passenger_vehicle_sample_incomplete');
 if(v.vehicle_type!=='bus')return reject('passenger_bus_not_identified');
 const row=db.sql.prepare('SELECT state,details_json FROM passenger_trips WHERE user=? AND id=?').get(user,c.id);
 let s=row?JSON.parse(row.details_json):null;
 if(s&&row.state!=='active')return {raw:null,status:passengerStatus(db,user,c.id),duplicate:true};
 if(s&&(s.device_id!==device||s.profile_id!==profile||s.vehicle_fingerprint!==fingerprint(v)))return reject('passenger_context_changed');
 if(c.action==='cancel'){
  s=s||{id:c.id,device_id:device,profile_id:profile,started_at:t,vehicle_fingerprint:fingerprint(v),distance_km:0};s.state='cancelled';save(db,user,s,t);
  db.sql.prepare("UPDATE profiles SET current_mission_json=NULL,updated_at=? WHERE user=? AND json_extract(current_mission_json,'$.trip_id')=?").run(t,user,c.id);
  db.sql.prepare("UPDATE open_trips SET state='cancelled',updated_at=? WHERE user=? AND trip_id=?").run(t,user,c.id);
  return {raw:null,cancelled:true,status:passengerStatus(db,user,c.id)};
 }
 const access=passengerBrandAccess(db.sql,user,v,s?.started_at||t);if(!access.ok)return reject(access.reason);
 const connected=signed.game?.connected===true,paused=signed.game?.paused===true;
 if(!s){
  if(c.confirmed!==true||!['manual','sdk','save'].includes(c.source))return reject('passenger_confirmation_required');
  if(c.source!=='manual'){const proof=observedPassengerService(signed,t);if(!proof||proof.source!==c.source||proof.origin!==text(c.origin)||proof.destination!==text(c.destination)||proof.service_id!==text(c.service_id))return reject('passenger_official_service_not_observed');}
  if(!connected||paused||(c.source==='manual'&&speed>1))return reject('passenger_start_requires_stopped_vehicle');
  if(Number(signed.job?.cargoMass)>0&&(signed.gameplay?.onJob===true||signed.on_job===true))return reject('another_trip_active');
  if(!text(c.origin)||!text(c.destination))return reject('passenger_route_required');
  if(db.sql.prepare("SELECT 1 FROM passenger_trips WHERE user=? AND state='active'").get(user))return reject('passenger_trip_already_active');
  const completed=db.sql.prepare("SELECT details_json,updated_at FROM passenger_trips WHERE user=? AND state='completed' ORDER BY updated_at DESC LIMIT 1").get(user);
  if(completed){const prior=JSON.parse(completed.details_json);if(ms<=Date.parse(completed.updated_at))return reject('passenger_overlapping_trip');if(prior.profile_id===profile&&prior.vehicle_fingerprint===fingerprint(v)&&odo<prior.last_odometer-.01)return reject('passenger_odometer_before_previous_trip');}
  const mission=db.sql.prepare('SELECT current_mission_json FROM profiles WHERE user=?').get(user);let current=null;try{current=JSON.parse(mission?.current_mission_json||'null')}catch{}
  if(current&&['active','suspended'].includes(current.state))return reject('another_trip_active');
  s={id:c.id,state:'active',device_id:device,profile_id:profile,vehicle:v,vehicle_fingerprint:fingerprint(v),entitlement_key:access.entitlement_key,source:c.source,service_id:text(c.service_id),origin:text(c.origin),destination:text(c.destination),started_at:t,last_at:t,start_odometer:odo,start_position:pos,last_odometer:odo,last_position:pos,distance_km:0,observed_seconds:0,samples:0,stops:0,stop_since:t,reason:null};
 }
 const dt=(ms-Date.parse(s.last_at))/1000;
 if(dt<0)return reject('passenger_sample_out_of_order');
 if(dt===0&&(Math.abs(odo-s.last_odometer)>.005||Math.hypot(pos.x-s.last_position.x,pos.z-s.last_position.z)>.5))return reject('passenger_sample_out_of_order');
 if(dt>0){
  const delta=odo-s.last_odometer,meters=Math.hypot(pos.x-s.last_position.x,pos.z-s.last_position.z),reportedScale=num(signed.game?.timeScale),scale=Math.max(20,reportedScale>=1&&reportedScale<=60?reportedScale:20);
  if(connected&&!paused&&dt<=30){
   if(delta<-.005)s.reason||='passenger_odometer_reversed';
   else if(delta>250*scale*dt/3600+.05||meters>85*dt+30||delta>meters*scale*1.25/1000+.1)s.reason||='passenger_impossible_movement';
   else if(delta>0&&meters>0.2){s.distance_km+=delta;s.observed_seconds+=dt;s.samples++;}
  }else if(dt>30){s.gap_seconds=(s.gap_seconds||0)+dt;}
  if(connected&&!paused&&speed<1){if(!s.stop_since)s.stop_since=t;if(ms-Date.parse(s.stop_since)>=10000&&!s.stop_counted){s.stops++;s.stop_counted=true;}}
  else{s.stop_since=null;s.stop_counted=false;}
  s.last_at=t;s.last_odometer=odo;s.last_position=pos;
 }
 if(c.action==='cancel'){s.state='cancelled';save(db,user,s,t);return {raw:null,cancelled:true,status:passengerStatus(db,user,c.id)};}
 let finish=c.action==='finish';
 if(finish){
  const reason=s.reason||(!connected||paused?'passenger_not_connected':speed>=1||!s.stop_since||ms-Date.parse(s.stop_since)<10000?'passenger_finish_requires_stop':s.distance_km<minimumKm?'passenger_distance_below_minimum':s.samples<2||s.observed_seconds<30?'passenger_insufficient_movement':null);
  if(reason){if(['passenger_finish_requires_stop','passenger_distance_below_minimum','passenger_insufficient_movement','passenger_not_connected'].includes(reason)){finish=false;s.pending_reason=reason;}else{save(db,user,s,t);return reject(reason);}}
  else{delete s.pending_reason;s.finished_at=t;s.end_odometer=odo;s.finish_position=pos;}
 }
 save(db,user,s,t);
 const raw={...signed,vehicle_brand:v.vehicle_brand,vehicle_model:v.vehicle_model,vehicle_type:v.vehicle_type,gat_trip_id:c.id,job_latch_key:c.id,job_latched:!finish,gat_passenger_validated:{...s,validation:'server_monitored',distance_km:s.distance_km},
  gat_context_id:'passengers|'+profile+'|'+c.id,gat_scs_job_kind:'bus_job',gat_job_kind:'bus_job',cargo_name:'Passageiros',cargo_id:'',mass_kg:0,source_city:s.origin,destination_city:s.destination,planned_distance_km:0,on_job:!finish,
  gat_transport_evidence:{classification:s.source==='manual'?'passenger_monitored':'passenger_official_monitored',service_id:s.service_id,vehicle:v,confirmed:true},
  job:{source_kind:'bus_job',active:!finish,cargo:'Passageiros',cargoId:'',cargoMass:0,sourceCity:s.origin,destinationCity:s.destination,plannedDistanceKm:0},
  gameplay:{...obj(signed.gameplay),onJob:!finish,jobDelivered:finish,jobCancelled:false,jobFinished:false,jobDeliveredDetails:finish?{distanceKm:s.distance_km,revenue:0,earnedXp:0,receiptSource:'bidulog_server_monitored'}:{}},gat_job_event:finish?'delivered':'',
 };
 return {raw,status:{id:s.id,state:s.state,distance_km:s.distance_km,reason:s.reason||s.pending_reason||null,stops:s.stops},finish};
}
export function completePassenger(db,user,id,t,event){
 if(!id||!String(event?.type||'').startsWith('delivery_completed'))return;
 const d=db.sql.prepare('SELECT id,raw_json FROM deliveries WHERE user=? AND delivered_at=? ORDER BY id DESC LIMIT 1').get(user,t);if(!d)return;
 const raw=JSON.parse(d.raw_json);if(raw.mission?.trip_id!==id)throw Error('passenger_delivery_identity_mismatch');
 const row=db.sql.prepare('SELECT details_json FROM passenger_trips WHERE user=? AND id=?').get(user,id);const s=JSON.parse(row.details_json);s.state='completed';s.finished_at=t;s.validation='validated';s.xp=event.xp;const reward=db.sql.prepare('SELECT gold_units FROM eco_rewards WHERE user=? AND delivery_id=?').get(user,d.id);if(!reward)throw Error('passenger_reward_not_observed');s.gold_units=reward.gold_units;
 db.sql.prepare("UPDATE passenger_trips SET state='completed',delivery_id=?,updated_at=?,details_json=? WHERE user=? AND id=? AND state='active'").run(d.id,t,JSON.stringify(s),user,id);
}
