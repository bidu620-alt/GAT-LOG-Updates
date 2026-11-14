// BiduLog Central: update existing worker in-place for SCS bus_job.
// Only string-exact, fail-closed changes. Preserves SQLite and custom settings.
import {existsSync,readFileSync,writeFileSync,copyFileSync,mkdirSync,rmSync,renameSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const root=resolve(process.argv[2]||'C:\\ProgramData\\GAT-LOG Server\\central');
const sourceModule=process.argv[3];
if(!sourceModule||!existsSync(sourceModule))throw Error('Informe o caminho do arquivo scs-job-compat.js baixado.');
const workerPath=join(root,'worker.js'),modulePath=join(root,'scs-job-compat.js');
if(!existsSync(workerPath))throw Error('worker.js ausente: '+workerPath+'. Nada foi alterado.');
const original=readFileSync(workerPath,'utf8');
const compat=readFileSync(sourceModule,'utf8');
if(!original.includes('async function processMissionCore(')||!original.includes("'/api/client/telemetry'"))throw Error('Versao da Central desconhecida. Nenhum arquivo substituido.');
if(!compat.includes('export function normalizeSCSJob(')||!compat.includes('export function scsJobKind('))throw Error('Modulo de onibus invalido; nada foi alterado.');
const edits=[["import {telemetryCanonical} from './journal-canonical.js';","import {telemetryCanonical} from './journal-canonical.js';\nimport {normalizeSCSJob,scsJobKind} from './scs-job-compat.js';"],["function flat(driver,account,updated,raw){return{driver,account_user:account||'',updated_at:updated,rank_status:rankingReadiness(raw),telemetry:raw,","function busJobLoaded(f,raw){return f.job_kind==='bus_job'&&f.on_job&&raw?.game?.connected===true&&!!f.source_city&&!!f.destination_city}\nfunction flat(driver,account,updated,raw){return{driver,account_user:account||'',updated_at:updated,rank_status:rankingReadiness(raw),job_kind:scsJobKind(raw),telemetry:raw,"],["if(!m&&observed.cargo_name&&Number(observed.mass_kg)>0){","if(!m&&(observed.cargo_name&&Number(observed.mass_kg)>0||busJobLoaded(observed,raw))){"],["category:'Carga detectada',state:'assigned',min_km:adminTest?0:MIN_KM","category:observed.job_kind==='bus_job'?'Transporte de passageiros':'Carga detectada',job_kind:observed.job_kind,state:'assigned',min_km:adminTest?0:MIN_KM"],["const hasLoadedJob=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&Number(f.mass_kg)>0;","const hasLoadedJob=busJobLoaded(f,raw)||Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&Number(f.mass_kg)>0;"],["m={...m,state:'active',min_km:minKm,cargo:f.cargo_name||m.cargo||'Carga',","m={...m,state:'active',min_km:minKm,job_kind:f.job_kind,cargo:f.cargo_name||m.cargo||'Carga',"],["const jobIdentityChanged=!!(hasLoadedJob&&(cargoChanged||sourceChanged||destinationChanged||weightChanged));","const jobIdentityChanged=!!(hasLoadedJob&&(cargoChanged||sourceChanged||destinationChanged||weightChanged||(m.job_kind&&f.job_kind&&m.job_kind!==f.job_kind)));"],["const raw=b.telemetry&&typeof b.telemetry==='object'&&!Array.isArray(b.telemetry)?b.telemetry:{},receivedAt=now(),account=c.row.account_user;","const signedRaw=b.telemetry&&typeof b.telemetry==='object'&&!Array.isArray(b.telemetry)?b.telemetry:{},raw=normalizeSCSJob(signedRaw),receivedAt=now(),account=c.row.account_user;"],["const f=flat(driver,account,t,raw),loaded=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&f.mass_kg>0;","const f=flat(driver,account,t,raw),loaded=busJobLoaded(f,raw)||Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&f.mass_kg>0;"],["const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);","const packetState=await inspectClientPacket(env,account,device,String(b.token||''),signedRaw,t);"],["const signature=x=>{const q=flat(driver,account,t,x);return JSON.stringify([q.truck_make,q.truck_model,","const signature=x=>{const q=flat(driver,account,t,x);return JSON.stringify([q.job_kind,q.truck_make,q.truck_model,"],["const openAudit={...auditData,gat_points:gatPoints,","const openAudit={...auditData,job_kind:m.job_kind||'job',gat_points:gatPoints,"]];
let patched=original,applied=0;
for(const [from,to] of edits){
 if(patched.includes(to))continue;
 if(patched.split(from).length!==2)throw Error('Central local com trecho diferente ou duplicado: '+from.slice(0,100)+'; nenhum arquivo alterado.');
 patched=patched.replace(from,to);applied++;
}
if(!patched.includes("import {normalizeSCSJob,scsJobKind} from './scs-job-compat.js';")||!patched.includes('inspectClientPacket(env,account,device,String(b.token||\'\'),signedRaw,t)'))throw Error('Patch incompleto.');
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const staging=join(root,'.scs-bus-'+stamp),backups=join(root,'backups','scs-bus-'+stamp);
mkdirSync(staging,{recursive:true});
const tmpWorker=join(staging,'worker.mjs'),tmpModule=join(staging,'scs-job-compat.mjs');
writeFileSync(tmpWorker,patched,'utf8');writeFileSync(tmpModule,compat,'utf8');
try{
 for(const file of [tmpWorker,tmpModule]){
  const result=spawnSync(process.execPath,['--check',file],{encoding:'utf8'});
  if(result.status!==0)throw Error('Sintaxe invalida: '+String(result.stderr||result.stdout||'').slice(0,500));
 }
 mkdirSync(backups,{recursive:true});
 copyFileSync(workerPath,join(backups,'worker.js'));
 if(existsSync(modulePath))copyFileSync(modulePath,join(backups,'scs-job-compat.js'));
 try{
  copyFileSync(tmpModule,modulePath);
  copyFileSync(tmpWorker,workerPath);
 }catch(err){
  // Always restore even if a failed copy partially truncated its destination.
  copyFileSync(join(backups,'worker.js'),workerPath);
  const old=join(backups,'scs-job-compat.js');
  if(existsSync(old))copyFileSync(old,modulePath);
  else rmSync(modulePath,{force:true});
  throw err;
 }
 console.log('OK: Central preparada para ler job e bus_job separadamente. Alteracoes: '+applied);
 console.log('Backup seguro dos arquivos anteriores: '+backups);
 console.log('Banco SQLite, entregas, XP e Gold nao foram modificados.');
 console.log('Reinicie a Central para aplicar as novas regras.');
}finally{rmSync(staging,{recursive:true,force:true})}
