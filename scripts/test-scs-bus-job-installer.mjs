import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,copyFileSync,existsSync,mkdtempSync,rmSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=new URL('../',import.meta.url);
const installer=new URL('./install-scs-bus-job-central.mjs',import.meta.url);
const current=readFileSync(new URL('../central-economy/worker.js',import.meta.url),'utf8');
const compatSource=new URL('../central-economy/scs-job-compat.js',import.meta.url);
const installerSrc=readFileSync(installer,'utf8');
const editsLine=installerSrc.match(/const edits=(\[.*?\]);\r?\nlet patched=/s);
assert.ok(editsLine,'cannot extract installer patch data');
const edits=JSON.parse(editsLine[1]);
function fixture(){
 const root=mkdtempSync(join(tmpdir(),'bidulog-scs-bus-'));
 const central=join(root,'central');
 mkdirSync(central,{recursive:true});
 const source=join(root,'scs-job-compat.js');
 copyFileSync(compatSource,source);
 return {root,central,source};
}
function run(central,source){
 return spawnSync(process.execPath,[fileURLToPath(installer),central,source],{encoding:'utf8'});
}
test('installer safely patches older compatible Central, makes backup, and is idempotent',()=>{
 const f=fixture();
 try{
  let before=current;
  for(const [old,replacement] of [...edits].reverse()){
   assert.ok(before.includes(replacement),'current worker missing expected anchor');
   before=before.replace(replacement,old);
  }
  const path=join(f.central,'worker.js');
  writeFileSync(path,before,'utf8');
  const first=run(f.central,f.source);
  assert.equal(first.status,0,first.stderr||first.stdout);
  assert.equal(readFileSync(path,'utf8'),current);
  assert.equal(readFileSync(join(f.central,'scs-job-compat.js'),'utf8'),readFileSync(compatSource,'utf8'));
  const second=run(f.central,f.source);
  assert.equal(second.status,0,second.stderr||second.stdout);
  assert.equal(readFileSync(path,'utf8'),current);
  assert.ok(existsSync(join(f.central,'backups')));
 }finally{rmSync(f.root,{recursive:true,force:true})}
});
test('installer refuses unknown worker version without mutation',()=>{
 const f=fixture();
 try{
  const path=join(f.central,'worker.js');
  writeFileSync(path,"console.log('unknown-version');",'utf8');
  const r=run(f.central,f.source);
  assert.notEqual(r.status,0);
  assert.equal(readFileSync(path,'utf8'),"console.log('unknown-version');");
  assert.equal(existsSync(join(f.central,'scs-job-compat.js')),false);
 }finally{rmSync(f.root,{recursive:true,force:true})}
});
