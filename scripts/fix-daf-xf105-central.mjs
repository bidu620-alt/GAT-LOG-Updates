// BiduLog Central - targeted patch for DAF XF 105 and other official models.
// Does not read, write, or replace the SQLite database.
import {existsSync,readFileSync,copyFileSync,writeFileSync,unlinkSync} from 'node:fs';
import {join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';

function resolveManufacturerFromMake(make){
 const n=String(make||'').trim();
 let brand='';
 if(/^scania(?: |$)/.test(n))brand='scania';
 else if(/^volvo(?: |$)/.test(n))brand='volvo';
 else if(/^daf(?: |$)/.test(n))brand='daf';
 else if(/^iveco(?: |$)/.test(n))brand='iveco';
 else if(/^man(?: |$)/.test(n))brand='man';
 else if(/^(?:mercedes(?: benz)?|mb)(?: |$)/.test(n))brand='mercedes';
 else if(/^renault(?: |$)/.test(n))brand='renault';
 if(!brand)return n;
 const check={
  scania:/(?:^| )scania(?: |$)/,
  volvo:/(?:^| )volvo(?: |$)/,
  daf:/(?:^| )daf(?: |$)/,
  iveco:/(?:^| )iveco(?: |$)/,
  man:/(?:^| )man(?: |$)/,
  mercedes:/(?:^| )(?:mercedes|mb)(?: |$)/,
  renault:/(?:^| )renault(?: |$)/
 };
 for(const [other,match] of Object.entries(check))
  if(other!==brand&&match.test(n))return n;
 return brand;
}
const root=process.argv[2]||'C:\\ProgramData\\GAT-LOG Server\\central';
const target=join(root,'vehicle-economy-key.js');
if(!existsSync(target))throw Error('Arquivo vehicle-economy-key.js ausente. Nenhuma alteração aplicada.');
const original=readFileSync(target,'utf8');
const old="const makeKey=({'mercedes benz':'mercedes','renault trucks':'renault'}[make]||make);";
if(!original.includes(old)){
 if(original.includes('const makeKey=resolveManufacturerFromMake(make);')){
  console.log('Marca já normalizada; nenhuma alteração necessária.');process.exit(0);
 }
 throw Error('Central com versão diferente. Nada modificado; verifique antes de atualizar.');
}
const marker='export function vehicleEconomyKey(';
const at=original.indexOf(marker);
if(at<0)throw Error('Estrutura desconhecida. Nenhuma alteração aplicada.');
const source=original.slice(0,at)+resolveManufacturerFromMake.toString()+'\n'+
 original.slice(at).replace(old,'const makeKey=resolveManufacturerFromMake(make);');
if(source===original||source.includes(old))throw Error('Não foi possível preparar a correção.');
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const staged=join(root,'vehicle-economy-key.patch-'+stamp+'.mjs');
const backup=join(root,'vehicle-economy-key.js.bak-'+stamp);
writeFileSync(staged,source,'utf8');
try{
 const result=spawnSync(process.execPath,['--check',staged],{encoding:'utf8'});
 if(result.status!==0)throw Error('Falha de sintaxe: '+String(result.stderr||''));
 const module=await import(pathToFileURL(staged).href);
 for(const [make,expected] of [
  ['DAF','daf'],['DAF XF 105','daf'],['DAF-XF105 WS Mods','daf'],
  ['DAF Trucks XF 105','daf'],['Volvo FH','volvo'],['Scania R','scania'],
  ['Fake DAF','fake daf'],['DAF Volvo','daf volvo']
 ]){
  const found=module.vehicleEconomyKey({truck:{make,model:'XF 105'}});
  if(found!==expected)throw Error('Falha no teste '+make+': '+found);
 }
 copyFileSync(target,backup);
 try{copyFileSync(staged,target)}catch(error){copyFileSync(backup,target);throw error}
 console.log('SUCESSO: identificador DAF corrigido. Reinicie a Central para carregar esta versão.');
 console.log('Backup da versão antiga: '+backup);
 console.log('O banco SQLite, histórico, contas, Gold e XP não foram alterados.');
}finally{try{unlinkSync(staged)}catch{}}
