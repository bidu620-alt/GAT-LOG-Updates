// Private test patch. Run only against a stopped, backed-up compatible Central.
import {readFileSync,writeFileSync,copyFileSync,existsSync,mkdirSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=resolve(process.argv[2]||'');
if(!process.argv[2])throw Error('Informe o diretorio da Central de teste.');
const file=join(root,'worker.js'),moduleFile=join(root,'save-evidence.js');
const source=fileURLToPath(new URL('./save-evidence.js',import.meta.url));
let original=readFileSync(file,'utf8'),patched=original;
const edits=[
 ["import {normalizeSCSJob,scsJobKind} from './scs-job-compat.js';","import {normalizeSCSJob,scsJobKind} from './scs-job-compat.js';\nimport {receiveSaveEvidence} from './save-evidence.js';"],
 ["const packetState=await inspectClientPacket(env,account,device,String(b.token||''),signedRaw,t);","const packetState=await inspectClientPacket(env,account,device,String(b.token||''),signedRaw,t);\n   let saveCapture={status:'not_sent'};\n   try{saveCapture=await receiveSaveEvidence(env.DB,account,signedRaw,packetState.journal_verified===true,t)}catch(e){saveCapture={status:'storage_error',reason:'save_evidence_storage_failed'};console.error('Save evidence storage failed:',e.message)}"],
 ['replayed:queuedTimeOK,rank_status:readiness,mission_event:missionEvent,next_upload_ms:5000','replayed:queuedTimeOK,save_capture:saveCapture,rank_status:readiness,mission_event:missionEvent,next_upload_ms:5000']
];
for(const [from,to] of edits){if(patched.includes(to))continue;if(patched.split(from).length!==2)throw Error('Central incompatível; nenhum arquivo alterado: '+from.slice(0,70));patched=patched.replace(from,to);}
const stamp=new Date().toISOString().replace(/[:.]/g,'-'),backup=join(root,'backups','save-reader-'+stamp);
const temp=join(root,'.save-reader-'+stamp+'.mjs');
writeFileSync(temp,patched);
try{
 for(const f of [temp,source]){const r=spawnSync(process.execPath,['--check',f],{encoding:'utf8'});if(r.status!==0)throw Error(r.stderr||'Syntax failed');}
 mkdirSync(backup,{recursive:true});copyFileSync(file,join(backup,'worker.js'));
 const hadModule=existsSync(moduleFile);if(hadModule)copyFileSync(moduleFile,join(backup,'save-evidence.js'));
 try{copyFileSync(source,moduleFile);writeFileSync(file,patched);}
 catch(error){writeFileSync(file,original);if(hadModule)copyFileSync(join(backup,'save-evidence.js'),moduleFile);else rmSync(moduleFile,{force:true});throw error;}
 console.log('Preparado. Backup: '+backup+'\nReinicie a Central de teste. Sem alteracoes de pontuacao ou Gold.');
}finally{rmSync(temp,{force:true});}
