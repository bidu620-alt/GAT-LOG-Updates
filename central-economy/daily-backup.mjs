import {readFileSync,writeFileSync,mkdirSync,renameSync} from 'node:fs';
import {join} from 'node:path';
export const BACKUP_FOLDER='D:\\BiduLog\\Backups';
export function brasiliaTime(now){
 const parts=Object.fromEntries(new Intl.DateTimeFormat('en-CA',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(now).map(p=>[p.type,p.value]));
 return {day:`${parts.year}-${parts.month}-${parts.day}`,midnight:parts.hour==='00'&&parts.minute==='00'};
}
export function createDailyBackup({run,onError,folder=BACKUP_FOLDER,clock=()=>new Date()}){
 let busy=false,lastAttempt='';
 const marker=join(folder,'ultimo-backup-diario.json');
 return async function tick(){
  const {day,midnight}=brasiliaTime(clock());
  if(!midnight||busy||lastAttempt===day)return;
  busy=true;lastAttempt=day;
  try{
   let previous=null;try{previous=JSON.parse(readFileSync(marker,'utf8'));}catch(e){if(e.code!=='ENOENT')throw e;}
   if(previous?.day===day)return;
   // Do not silently fall back to C: if the requested disk is unavailable.
   mkdirSync(folder,{recursive:true});
   const path=await run();
   writeFileSync(marker+'.tmp',JSON.stringify({day,path,completed_at:clock().toISOString(),timezone:'America/Sao_Paulo'}));
   renameSync(marker+'.tmp',marker);
  }catch(e){onError(e);}finally{busy=false;}
 };
}
