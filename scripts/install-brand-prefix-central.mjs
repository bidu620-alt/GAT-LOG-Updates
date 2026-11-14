// BiduLog — atualizacao segura do reconhecimento de marcas (sem editar SQLite).
// Executar com o Node incluido na Central; aplica somente a vehicle-economy-key.js.
import {existsSync,readFileSync,writeFileSync,copyFileSync,unlinkSync} from 'node:fs';
import {join} from 'node:path';
import {spawnSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';

const root=process.argv[2]||'C:\\ProgramData\\GAT-LOG Server\\central';
const target=join(root,'vehicle-economy-key.js');
if(!existsSync(target))throw Error('Arquivo da Central nao encontrado: '+target+'; nenhuma alteracao feita.');
const before=readFileSync(target,'utf8');
const marker='export function vehicleEconomyKey(';
if(!before.includes(marker)||!before.includes('const norm=')){
 throw Error('Versao da Central nao compativel; nenhum arquivo modificado.');
}
let code=before;
const oldMake='const make=norm(t.make||t.manufacturer||t.brand);';
const newMake="const make=norm([t.make,t.manufacturer,t.brand,t.makeName,t.manufacturerName].find(v=>typeof v==='string'&&v.trim())||'');";
if(code.includes(oldMake))code=code.replace(oldMake,newMake);
const oldKeys=[
 "const makeKey=({'mercedes benz':'mercedes','renault trucks':'renault'}[make]||make);",
 'const makeKey=resolveManufacturerFromMake(make);'
];
const helperName='resolveGarageBrand';
const helper=[
 'function '+helperName+'(make){',
 ' const n=String(make||"").trim();',
 ' if(!n)return "";',
 ' let brand="";',
 ' if(/^scania(?: |$)/.test(n))brand="scania";',
 ' else if(/^volvo(?: |$)/.test(n))brand="volvo";',
 ' else if(/^daf(?: |$)/.test(n))brand="daf";',
 ' else if(/^iveco(?: |$)/.test(n))brand="iveco";',
 ' else if(/^man(?: |$)/.test(n))brand="man";',
 ' else if(/^(?:mercedes(?: benz)?|mercedesbenz|mb)(?: |$)/.test(n))brand="mercedes";',
 ' else if(/^renault(?: |$)/.test(n))brand="renault";',
 ' if(!brand)return n;',
 ' const others={scania:/(?:^| )scania(?: |$)/,volvo:/(?:^| )volvo(?: |$)/,daf:/(?:^| )daf(?: |$)/,iveco:/(?:^| )iveco(?: |$)/,man:/(?:^| )man(?: |$)/,mercedes:/(?:^| )(?:mercedes|mercedesbenz|mb)(?: |$)/,renault:/(?:^| )renault(?: |$)/};',
 ' for(const [name,rx] of Object.entries(others))if(name!==brand&&rx.test(n))return n;',
 ' return brand;',
 '}'
].join('\n');
let replaced=false;
for(const old of oldKeys){
 if(code.includes(old)){
  if(!code.includes('function '+helperName+'(')){
   const at=code.indexOf(marker);
   code=code.slice(0,at)+helper+'\n'+code.slice(at);
  }
  code=code.replace(old,'const makeKey='+helperName+'(make);');
  replaced=true;
  break;
 }
}
// Newer source already exports truckBrandKey(). Accept only the known cases.
if(!replaced&&!code.includes('const makeKey='+helperName+'(make);')&&
 !(code.includes('const maker=truckBrandKey(make);')&&code.includes('export function truckBrandKey('))){
 throw Error('Normalizador atual nao reconhecido. Nada alterado para preservar a Central.');
}
if(code.includes('export function truckBrandKey(')){
 const oldMercedes="else if(/^(?:mercedes(?: benz)?|mb)(?: |$)/.test(n))brand='mercedes';";
 if(code.includes(oldMercedes))code=code.replace(oldMercedes,"else if(/^(?:mercedes(?: benz)?|mercedesbenz|mb)(?: |$)/.test(n))brand='mercedes';");
 const oldOther="mercedes:/(?:^| )(?:mercedes|mb)(?: |$)/,";
 if(code.includes(oldOther))code=code.replace(oldOther,"mercedes:/(?:^| )(?:mercedes|mercedesbenz|mb)(?: |$)/,");
}
if(code===before){console.log('O reconhecimento por marca ja esta atualizado; nenhuma alteracao necessaria.');process.exit(0)}
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const stage=join(root,'vehicle-economy-key.stage-'+stamp+'.mjs');
const backup=join(root,'vehicle-economy-key.js.backup-'+stamp);
writeFileSync(stage,code,'utf8');
try{
 const check=spawnSync(process.execPath,['--check',stage],{encoding:'utf8'});
 if(check.status!==0)throw Error('Erro de sintaxe; nada modificado: '+String(check.stderr||''));
 const mod=await import(pathToFileURL(stage).href);
 const expected={
  volvo:['Volvo','Volvo XX','VOLVO-FH16','Volvo_Y'],
  scania:['Scania','Scania R','SCANIA-S770'],
  daf:['DAF','DAF XF 105','DAF-XF105'],
  man:['MAN','MAN TGX'],
  iveco:['Iveco','Iveco S-Way'],
  mercedes:['Mercedes','Mercedes-Benz Actros','MercedesBenz Actros','MB 1113'],
  renault:['Renault','Renault Trucks T','Renault Premium']
 };
 for(const [brand,variants] of Object.entries(expected)){
  for(const make of variants){
   if(mod.vehicleEconomyKey({truck:{make,model:'Qualquer modelo'}})!==brand)throw Error('Teste falhou: '+make);
  }
 }
 if(mod.vehicleEconomyKey({truck:{makeName:'Volvo XX'}})!=='volvo')throw Error('Falha no campo makeName');
 for(const name of ['Fake Volvo','Volvox','Volvo Scania','MANager']){
  const result=mod.vehicleEconomyKey({truck:{make:name}});
  if(Object.keys(expected).includes(result))throw Error('Falso positivo: '+name);
 }
 copyFileSync(target,backup);
 try{copyFileSync(stage,target)}catch(error){copyFileSync(backup,target);throw error}
 console.log('SUCESSO: as 7 marcas identificam automaticamente todos os modelos com nome de marca valido.');
 console.log('Backup do modulo: '+backup);
 console.log('O banco SQLite, as entregas, XP, Gold e contas nao foram alterados.');
 console.log('Reinicie a Central para que o Node carregue o modulo atualizado.');
}finally{try{unlinkSync(stage)}catch{}}
