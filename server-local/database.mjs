import {DatabaseSync,backup} from 'node:sqlite';
import {existsSync,mkdirSync,readFileSync,renameSync,rmSync,readdirSync} from 'node:fs';
import {join} from 'node:path';

export class LocalDatabase {
  constructor(path){
    this.sql=new DatabaseSync(path,{enableForeignKeyConstraints:true,allowExtension:false});
    this.sql.exec('PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL;');
  }
  prepare(query){
    const db=this;let args=[];
    return {
      bind(...values){args=values;return this;},
      async first(column){const row=db.sql.prepare(query).get(...args);return column?(row?.[column]??null):(row??null);},
      async all(){return {success:true,results:db.sql.prepare(query).all(...args)};},
      async run(){const r=db.sql.prepare(query).run(...args);return {success:true,meta:{changes:Number(r.changes),last_row_id:Number(r.lastInsertRowid)}};}
    };
  }
  async batch(statements){
    this.sql.exec('SAVEPOINT gat_batch');
    try{const result=[];for(const s of statements)result.push(await s.run());this.sql.exec('RELEASE gat_batch');return result;}
    catch(e){this.sql.exec('ROLLBACK TO gat_batch; RELEASE gat_batch');throw e;}
  }
  close(){this.sql.close();}
}

const required=['accounts','profiles','sessions','client_tokens','work_catalog','deliveries','work_completed','routes_completed','mission_completions'];
export function validateDatabase(sql){
  const integrity=sql.prepare('PRAGMA integrity_check').all();
  if(integrity.some(x=>x.integrity_check!=='ok'))throw Error('Banco com erro de integridade.');
  if(sql.prepare('PRAGMA foreign_key_check').all().length)throw Error('Banco com referencias incompletas.');
  for(const table of required)if(!sql.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?").get(table))throw Error('Exportacao incompleta: '+table);
  const users=sql.prepare('SELECT COUNT(*) n FROM accounts').get().n;
  const owner=sql.prepare("SELECT 1 FROM accounts WHERE role='owner' AND disabled=0 AND length(password_hash)>0 AND length(password_salt)>0").get();
  if(!users||!owner)throw Error('A exportacao precisa conter as contas e a senha do proprietario.');
  return {accounts:users,deliveries:sql.prepare('SELECT COUNT(*) n FROM deliveries').get().n};
}

// Split a SQL dump without treating semicolons inside string values as syntax.
// Only schema/data statements emitted by D1/SQLite dumps are accepted. Import
// cannot ATTACH another database, load extensions, create triggers or run VACUUM.
export function dumpStatements(source){
  const statements=[];let current='',quote=null,line=false,block=false;
  for(let i=0;i<source.length;i++){
    const c=source[i],n=source[i+1];
    if(line){if(c==='\n'){line=false;current+=' ';}continue;}
    if(block){if(c==='*'&&n==='/'){block=false;i++;current+=' ';}continue;}
    if(quote){current+=c;if(c===quote){if(n===quote){current+=n;i++;}else quote=null;}continue;}
    if(c==='-'&&n==='-'){line=true;i++;continue;}
    if(c==='/'&&n==='*'){block=true;i++;continue;}
    if(c==="'"||c==='"'||c==='`'||c==='['){quote=c==='['?']':c;current+=c;continue;}
    if(c===';'){if(current.trim())statements.push(current.trim());current='';}else current+=c;
  }
  if(quote||block)throw Error('Arquivo SQL incompleto.');
  if(current.trim())statements.push(current.trim());
  return statements;
}

export async function importDatabase(sourcePath,dataDir,schemaPath,indexPath){
  mkdirSync(dataDir,{recursive:true});
  const target=join(dataDir,'central.sqlite'),temp=join(dataDir,'importing.sqlite');
  // Initial setup only. Never replace a running or previously imported database.
  if(existsSync(target))throw Error('Ja existe um banco local. A importacao nao substitui dados existentes.');
  for(const suffix of ['','-wal','-shm'])rmSync(temp+suffix,{force:true});
  const db=new LocalDatabase(temp);
  try{
    db.sql.exec('BEGIN IMMEDIATE; PRAGMA defer_foreign_keys=ON');
    for(const statement of dumpStatements(readFileSync(sourcePath,'utf8').replace(/^\uFEFF/,''))){
      if(/^(BEGIN(?: TRANSACTION)?|COMMIT|END(?: TRANSACTION)?)$/i.test(statement))continue;
      if(/^PRAGMA\s+(?:defer_foreign_keys|foreign_keys)\s*=\s*(?:ON|OFF|TRUE|FALSE|0|1)$/i.test(statement))continue;
      if(!/^(CREATE\s+(?:(?:UNIQUE\s+)?INDEX|TABLE)\s|INSERT\s+(?:OR\s+(?:IGNORE|REPLACE)\s+)?INTO\s|DELETE\s+FROM\s+["`]?sqlite_sequence["`]?\s*$)/i.test(statement))throw Error('Comando nao permitido na exportacao SQL.');
      db.sql.exec(statement);
    }
    // Verify the original dump BEFORE adding optional tables. Otherwise a
    // partial accounts-only export could silently appear to be a full backup.
    validateDatabase(db.sql);
    // Add current indexes and optional tables, without modifying imported rows.
    db.sql.exec(readFileSync(schemaPath,'utf8'));
    db.sql.exec(readFileSync(indexPath,'utf8'));
    const counts=validateDatabase(db.sql);
    db.sql.exec('COMMIT; PRAGMA wal_checkpoint(TRUNCATE)');db.close();
    renameSync(temp,target);
    return counts;
  }catch(e){try{db.sql.exec('ROLLBACK');}catch{}try{db.close();}catch{}for(const suffix of ['','-wal','-shm'])rmSync(temp+suffix,{force:true});throw e;}
}

export async function saveBackup(db,dataDir){
  const folder=join(dataDir,'backups');mkdirSync(folder,{recursive:true});
  const stamp=new Date().toISOString().replace(/[:.]/g,'-');
  const target=join(folder,'central-'+stamp+'.sqlite');
  await backup(db.sql,target);
  const files=readdirSync(folder).filter(x=>/^central-.*\.sqlite$/.test(x)).sort();
  for(const old of files.slice(0,-14))rmSync(join(folder,old));
  return target;
}
