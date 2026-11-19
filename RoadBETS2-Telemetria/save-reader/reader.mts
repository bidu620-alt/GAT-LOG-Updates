import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {SIIDecryptor} from './vendor/decryptor.ts';

// SII decoder upstream: https://github.com/Trucky/sii-decrypt-ts (MIT).
// Type-only imports adapted for Node 24; source otherwise retained in vendor.
const hash=(v)=>createHash('sha256').update(v).digest('hex');
const nullRef=v=>!v||v==='null'||v==='nil';
export function parseUnits(text){
 const units=new Map();
 for(const m of text.matchAll(/^(\w+)\s*:\s*(\S+)\s*\{\r?\n([\s\S]*?)^\}/gm)){
  const fields=Object.create(null);
  for(const line of m[3].split(/\r?\n/)){
   const f=line.match(/^\s+([\w.]+)(?:\[(\d+)\])?:\s*(.*)$/);
   if(!f)continue;
   const key=f[1],value=f[3];
   if(f[2]!==undefined){if(!Array.isArray(fields[key]))fields[key]=[];fields[key][Number(f[2])]=value;}
   else fields[key]=value;
  }
  units.set(m[2],{type:m[1],id:m[2],fields});
 }
 return units;
}
function compact(unit){
 if(!unit)return null;
 const fields=Object.create(null);
 for(const [k,v] of Object.entries(unit.fields))fields[k]=Array.isArray(v)?v.slice(-200).map(x=>String(x).slice(0,1000)):String(v).slice(0,1000);
 return {type:unit.type,id:unit.id,fields};
}
export function summarize(text,profileId){
 const units=parseUnits(text),player=[...units.values()].find(u=>u.type==='player'),economy=[...units.values()].find(u=>u.type==='economy');
 if(!player)throw Error('player_unit_missing');
 const jobs=Object.create(null),related=new Map();
 const collect=(ref,depth)=>{
  if(nullRef(ref)||related.has(ref)||depth<0||related.size>=100)return;
  const u=units.get(ref);if(!u||['bank','economy','player','company','driver'].includes(u.type))return;
  related.set(ref,compact(u));
  // Follow only job/vehicle accessories; never player, bank, economy or world.
  for(const [key,value] of Object.entries(u.fields)){
   if(/^(player|economy|bank|companies|garages|drivers|bus_stops)$/.test(key))continue;
   for(const next of (Array.isArray(value)?value.slice(-80):[value]))if(String(next).startsWith('_nameless.'))collect(next,depth-1);
  }
 };
 for(const kind of ['current_job','current_bus_job','current_car_job']){
  const ref=player.fields[kind];jobs[kind]=nullRef(ref)?null:compact(units.get(ref));collect(ref,2);
 }
 for(const key of ['assigned_vehicles','assigned_truck','my_truck'])collect(player.fields[key],2);
 const history=Object.create(null);
 for(const key of ['delivery_log','bus_job_log','car_job_log']){
  const ref=economy?.fields[key]||player.fields[key],u=units.get(ref);
  if(!u){history[key]=null;continue;}
  const entries=Array.isArray(u.fields.entries)?u.fields.entries:[];
  // Preserve opaque records for Central parsing. Do not claim all entries are completions.
  history[key]={type:u.type,id:ref,version:u.fields.version||null,total_entries:entries.length,entries:entries.slice(-30).map(v=>units.has(v)?compact(units.get(v)):{raw:v}),truncated:entries.length>30};
 }
 const paths=[...related.values()].filter(u=>u.type.startsWith('vehicle_accessory')).map(u=>u.fields.data_path).filter(Boolean);
 const mode=player.fields.assigned_vehicles_mode;
 const activeJob=mode==='bus'?jobs.current_bus_job:mode==='car'?jobs.current_car_job:jobs.current_job;
 const group=units.get(player.fields.assigned_vehicles);
 const vehicleRef=['company_truck','bus','car','vehicle'].map(k=>activeJob?.fields[k]).find(v=>!nullRef(v))||group?.fields.vehicle||player.fields.assigned_truck;
 const vehicleUnit=units.get(vehicleRef);
 const accessoryRefs=Array.isArray(vehicleUnit?.fields.accessories)?vehicleUnit.fields.accessories:[];
 const definitions=accessoryRefs.map(ref=>units.get(ref)?.fields.data_path).filter(Boolean);
 const dataPath=definitions.find(p=>/\/data\.sii"?$/.test(p))||null;
 const modelId=dataPath?.match(/\/def\/vehicle\/(?:truck|bus|car)\/([^/]+)\/data\.sii/)?.[1]||null;
 const maker=modelId?.split(/[._]/)[0];
 const brand=['scania','volvo','man','daf','iveco','renault','mercedes'].includes(maker)?maker:null;
 const vehicle={type:mode||null,reference:vehicleRef||null,definition_id:modelId,brand_id:brand,model_id:modelId,data_path:dataPath,
  damage:vehicleUnit?Object.fromEntries(Object.entries(vehicleUnit.fields).filter(([k])=>/wear|damage/.test(k))):{}};

 return {profile_id:profileId,profile_name:Buffer.from(profileId,'hex').toString('utf8'),game_time:economy?.fields.game_time||null,
  vehicle_mode:player.fields.assigned_vehicles_mode||null,my_vehicles_mode:player.fields.my_vehicles_mode||null,
  job_kind:player.fields.assigned_vehicles_mode==='bus'?(jobs.current_bus_job?'bus_job':null):
   player.fields.assigned_vehicles_mode==='car'?(jobs.current_car_job?'car_job':null):
   player.fields.assigned_vehicles_mode==='truck'?(jobs.current_job?'job':null):
   Object.values(jobs).filter(Boolean).length===1?(jobs.current_bus_job?'bus_job':jobs.current_car_job?'car_job':'job'):null,
  vehicle,jobs,related_units:[...related.values()],vehicle_definitions:paths,history};
}
function decode(file){
 const st=fs.statSync(file);if(st.size>64*1024*1024)throw Error('save_too_large');
 const before=fs.readFileSync(file),sha=hash(before);
 const oldLog=console.log,oldWarn=console.warn;console.log=()=>{};console.warn=()=>{};
 let result;try{result=SIIDecryptor.decrypt(file);}finally{console.log=oldLog;console.warn=oldWarn;}
 const after=fs.statSync(file);
 if(after.size!==st.size||after.mtimeMs!==st.mtimeMs||hash(fs.readFileSync(file))!==sha)throw Error('save_changed_during_read');
 if(!result.success||(result.type==='binary'&&result.binaryFormatInfo?.success!==true))throw Error('save_decode_failed');
 return {text:result.data.toString('utf8'),sha256:sha,modified_at:st.mtime.toISOString(),size_bytes:st.size};
}
export function collectSave(folder,profileId){
 if(!/^(?:[a-fA-F0-9]{2}){1,128}$/.test(profileId))throw Error('invalid_profile_id');
 const game=decode(path.join(folder,'game.sii'));let info=null;
 try{info=decode(path.join(folder,'info.sii'));}catch{}
 const data=summarize(game.text,profileId);
 const infoUnits=info?parseUnits(info.text):null;
 const container=infoUnits?[...infoUnits.values()].find(x=>x.type==='save_container'):null;
 const age=(Date.now()-Date.parse(game.modified_at))/1000;
 return {schema:1,source:'ets2_save_readonly',status:age>=-30&&age<=120?'snapshot_recent':'snapshot_stale',
  captured_at:new Date().toISOString(),snapshot_age_seconds:Math.max(0,Math.round(age)),
  save:{game_sha256:game.sha256,game_modified_at:game.modified_at,game_size_bytes:game.size_bytes,
   info_sha256:info?.sha256||null,info_modified_at:info?.modified_at||null,
   pair_consistent:!!info&&Math.abs(Date.parse(info.modified_at)-Date.parse(game.modified_at))<=2000},
  ...data,dependencies:container?.fields.dependencies||[],history_is_completion_proof:false};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{
  const result=collectSave(process.argv[2],process.argv[3]);
  let json=JSON.stringify(result);
  if(Buffer.byteLength(json)>60000){
   result.transport_truncated=true;
   for(const h of Object.values(result.history||{}))if(h&&h.entries){h.entries=h.entries.slice(-5);h.truncated=true;}
   result.related_units=result.related_units.slice(0,40);
   json=JSON.stringify(result);
  }
  if(Buffer.byteLength(json)>60000)throw Error('evidence_too_large');
  process.stdout.write(json);
 }catch(error){process.stdout.write(JSON.stringify({schema:1,source:'ets2_save_readonly',status:'unavailable',reason:String(error.message).slice(0,120)}));process.exitCode=1;}
}
