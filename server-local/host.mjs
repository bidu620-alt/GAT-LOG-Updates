import http from 'node:http';
import {join,dirname,basename} from 'node:path';
import {fileURLToPath} from 'node:url';
import {existsSync,writeFileSync,mkdirSync,readFileSync} from 'node:fs';
import worker from './worker.js';
import {LocalDatabase,validateDatabase,importDatabase,saveBackup} from './database.mjs';

const here=dirname(fileURLToPath(import.meta.url));
function ensureAutomaticCargoCatalog(db){
  // Reexecutar o schema e seguro: todas as tabelas/indices usam IF NOT EXISTS.
  // Isso cria a fila de classificacao tambem em bancos que ja existiam antes da 1.0.40.
  db.sql.exec(readFileSync(join(here,'schema.sql'),'utf8'));
  const initialized=db.sql.prepare("SELECT value FROM meta WHERE key='auto_cargo_catalog_v1'").get();
  if(initialized)return;
  // O proprietario pediu para recomecar as sugestoes de nomes do zero. Mantemos os
  // 30 trabalhos, historico, contas e progresso; limpamos somente os nomes sugeridos.
  db.sql.prepare("UPDATE work_catalog SET compatible_cargos_json='[]' WHERE active=1").run();
  const t=new Date().toISOString();
  db.sql.prepare("INSERT OR REPLACE INTO meta(key,value) VALUES('auto_cargo_catalog_v1',?)").run(t);
}

async function ensureGoLiveBaseline(db,dataDir){
  const key='go_live_baseline_2026_09_02';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(key))return null;

  // Backup de arquivo completo antes de qualquer limpeza. Contas, senhas, tokens,
  // dispositivos, papeis, configuracao e catalogo nao sao apagados pelo reset.
  const backupPath=await saveBackup(db,dataDir);
  const t=new Date().toISOString();
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    // Remove somente dados competitivos/de teste. A fila depende de deliveries e
    // por isso e limpa primeiro. O catalogo e os aliases aprendidos permanecem.
    db.sql.prepare('DELETE FROM cargo_classification_queue').run();
    db.sql.prepare('DELETE FROM work_completed').run();
    db.sql.prepare('DELETE FROM routes_completed').run();
    db.sql.prepare('DELETE FROM mission_completions').run();
    db.sql.prepare('DELETE FROM deliveries').run();
    db.sql.prepare(`UPDATE profiles SET
      monthly_completed=0,
      total_deliveries=0,
      total_km=0,
      xp=0,
      points=0,
      perfect_trips=0,
      penalty_xp=0,
      speed_fines=0,
      safety_score=100,
      current_mission_json=NULL,
      updated_at=?`).run(t);
    db.sql.prepare('INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)').run(key,t);
    db.sql.prepare('INSERT INTO audit(at,actor,action,target,details) VALUES(?,?,?,?,?)').run(
      t,'system','go_live_reset','all_drivers',JSON.stringify({started_at:t,backup:basename(backupPath),preserved:['accounts','sessions','client_tokens','client_pairings','work_catalog','cargo_aliases','telemetry_live']})
    );
    db.sql.exec('COMMIT');
    db.sql.exec('PRAGMA wal_checkpoint(TRUNCATE)');
    return backupPath;
  }catch(e){
    try{db.sql.exec('ROLLBACK')}catch{}
    throw e;
  }
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
  const db=new LocalDatabase(path);validateDatabase(db.sql);ensureAutomaticCargoCatalog(db);
  if(process.argv[2]==='backup'){console.log(await saveBackup(db,dataDir));db.close();return;}
  const resetBackup=await ensureGoLiveBaseline(db,dataDir);
  if(resetBackup)console.log('GAT go-live: progresso de testes zerado. Backup: '+resetBackup);
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
