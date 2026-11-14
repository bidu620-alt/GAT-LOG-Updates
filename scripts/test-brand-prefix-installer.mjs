import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtempSync,readFileSync,writeFileSync,cpSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const root=resolve(fileURLToPath(new URL('../',import.meta.url)));
const installer=join(root,'scripts/install-brand-prefix-central.mjs');
const moduleFile=join(root,'central-economy/vehicle-economy-key.js');
const old=String.raw`const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
export function vehicleEconomyKey(raw){
 const t=raw?.truck||{};
 const make=norm(t.make||t.manufacturer||t.brand);
 const makeKey=({'mercedes benz':'mercedes','renault trucks':'renault'}[make]||make);
 return makeKey;
}`;
function run(dir){
 const x=spawnSync(process.execPath,[installer,dir],{encoding:'utf8',timeout:15000});
 assert.equal(x.status,0,String(x.stderr||x.stdout));
}
test('installer upgrades old central and is idempotent',async()=>{
 const dir=mkdtempSync(join(tmpdir(),'bidulog-brands-'));
 try{
  const dest=join(dir,'vehicle-economy-key.js');writeFileSync(dest,old,'utf8');run(dir);
  let mod=await import(pathToFileURL(dest).href+'?a=1');
  for(const [make,brand] of [['Volvo XX','volvo'],['Scania R450','scania'],['DAF XF 105','daf'],['MAN TGX','man'],['Iveco Novo','iveco'],['MercedesBenz Actros','mercedes'],['Renault Trucks T','renault']])
   assert.equal(mod.vehicleEconomyKey({truck:{make}}),brand,make);
  assert.equal(mod.vehicleEconomyKey({truck:{makeName:'Volvo Novo'}}),'volvo');
  const after=readFileSync(dest,'utf8');run(dir);assert.equal(readFileSync(dest,'utf8'),after);
 }finally{rmSync(dir,{recursive:true,force:true})}
});
test('installer detects new source without unnecessary replacement',()=>{
 const dir=mkdtempSync(join(tmpdir(),'bidulog-brands-'));
 try{
  const dest=join(dir,'vehicle-economy-key.js');cpSync(moduleFile,dest);const before=readFileSync(dest,'utf8');
  run(dir);assert.equal(readFileSync(dest,'utf8'),before);
 }finally{rmSync(dir,{recursive:true,force:true})}
});
