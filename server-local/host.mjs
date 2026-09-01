import http from 'node:http';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {existsSync,writeFileSync,mkdirSync} from 'node:fs';
import worker from './worker.js';
import {LocalDatabase,validateDatabase,importDatabase,saveBackup} from './database.mjs';

const here=dirname(fileURLToPath(import.meta.url));
function ensureLocalCatalog(db){
  const row=db.sql.prepare("SELECT compatible_cargos_json FROM work_catalog WHERE id='fuel'").get();
  if(!row)return;
  let cargos=[];try{cargos=JSON.parse(row.compatible_cargos_json||'[]')}catch{}
  for(const name of ['Ethanol','Etanol'])if(!cargos.some(x=>String(x).toLowerCase()===name.toLowerCase()))cargos.push(name);
  db.sql.prepare("UPDATE work_catalog SET compatible_cargos_json=? WHERE id='fuel'").run(JSON.stringify(cargos));
}
export function createCentral(db,{onError=()=>{}}={}){
  let queue=Promise.resolve(),queued=0;
  function exclusive(fn){const work=queue.then(fn);queue=work.catch(()=>{});return work;}
  const server=http.createServer(async(req,res)=>{
    if(queued>=100){res.writeHead(503,{'Retry-After':'5'});res.end();return;}
    queued++;
    try{
      const parts=[];let size=0;
      for await(const chunk of req){size+=chunk.length;if(size>262144){res.writeHead(413);res.end();return;}parts.push(chunk);}
      const headers=new Headers();for(const[k,v]of Object.entries(req.headers))if(v!==undefined)headers.set(k,Array.isArray(v)?v.join(','):v);
      const request=new Request('https://api.gatlogets2.com.br'+req.url,{method:req.method,headers,...(!['GET','HEAD'].includes(req.method)?{body:Buffer.concat(parts)}:{})});
      const response=await exclusive(async()=>{
        const tasks=[];db.sql.exec('BEGIN IMMEDIATE');
        try{
          const result=await worker.fetch(request,{DB:db},{waitUntil:p=>tasks.push(p)});
          await Promise.all(tasks);
          db.sql.exec(result.status>=500?'ROLLBACK':'COMMIT');return result;
        }catch(e){db.sql.exec('ROLLBACK');throw e;}
      });
      res.writeHead(response.status,Object.fromEntries(response.headers));res.end(Buffer.from(await response.arrayBuffer()));
    }catch(e){onError(e);if(!res.headersSent)res.writeHead(500);res.end('{"ok":false,"error":"local_server_error"}');}
    finally{queued--;}
  });
  server.requestTimeout=15000;server.headersTimeout=10000;server.maxRequestsPerSocket=1000;
  return {server,exclusive};
}

async function main(){
  const dataDir=join(process.env.LOCALAPPDATA||process.cwd(),'GAT-LOG','Central');
  if(process.argv[2]==='import'){
    const counts=await importDatabase(process.argv[3],dataDir,join(here,'schema.sql'),join(here,'migrations/0004_read_efficiency.sql'));
    console.log(JSON.stringify({ok:true,...counts}));return;
  }
  const path=join(dataDir,'central.sqlite');
  if(!existsSync(path))throw Error('Importe a exportacao completa do banco antes de iniciar a central.');
  const db=new LocalDatabase(path);validateDatabase(db.sql);ensureLocalCatalog(db);
  if(process.argv[2]==='backup'){console.log(await saveBackup(db,dataDir));db.close();return;}
  let lastError='';
  const {server,exclusive}=createCentral(db,{onError:e=>{lastError=String(e.message);console.error(lastError);}});
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(5056,'127.0.0.1',resolve);});
  mkdirSync(dataDir,{recursive:true});
  const status=()=>writeFileSync(join(dataDir,'status.json'),JSON.stringify({pid:process.pid,updated_at:new Date().toISOString(),backend:'local-sqlite',port:5056,last_error:lastError}));
  status();const heartbeat=setInterval(status,5000);
  const backupNow=()=>exclusive(()=>saveBackup(db,dataDir)).catch(e=>{lastError='Backup: '+e.message;});
  await backupNow();const backups=setInterval(backupNow,6*3600000);
  const cleanup=setInterval(()=>exclusive(async()=>{const tasks=[];await worker.scheduled({}, {DB:db},{waitUntil:p=>tasks.push(p)});await Promise.all(tasks);}).catch(e=>{lastError=e.message;}),3600000);
  async function stop(){clearInterval(heartbeat);clearInterval(backups);clearInterval(cleanup);server.close();await exclusive(async()=>{db.sql.exec('PRAGMA wal_checkpoint(TRUNCATE)');db.close();});process.exit(0);}
  process.on('SIGTERM',stop);process.on('SIGINT',stop);
  console.log('GAT Central local ativa em 127.0.0.1:5056.');
}
if(process.argv[1]&&fileURLToPath(import.meta.url)===process.argv[1])main().catch(e=>{console.error(e.message);process.exitCode=1;});
