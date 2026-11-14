import assert from 'node:assert/strict';
import test from 'node:test';
import {truckBrandKey,vehicleEconomyKey} from '../vehicle-economy-key.js';

test('DAF XF 105 is recognized by make even when make includes model',()=>{
 for(const make of ['DAF','DAF XF105','DAF XF 105','DAF-XF105 WS Mods','DAF Trucks XF 105']){
  assert.equal(truckBrandKey(make),'daf',make);
  assert.equal(vehicleEconomyKey({truck:{make,model:'XF 105'},game:{connected:true}}),'daf',make);
 }
 assert.equal(vehicleEconomyKey({truck:{manufacturer:'DAF',modelName:'XF 105'}}),'daf');
 assert.equal(vehicleEconomyKey({truck:{brand:'DAF Trucks',model:'XF105'}}),'daf');
});
test('original brand whitelist remains by manufacturer for other trucks',()=>{
 for(const [make,brand] of [
  ['Scania R450','scania'],['SCANIA Vabis 111','scania'],
  ['Volvo FH16','volvo'],['MAN TGX','man'],
  ['Mercedes-Benz Actros','mercedes'],['MB Actros','mercedes'],
  ['Iveco Stralis','iveco'],['Renault Trucks T','renault']
 ]){
  assert.equal(vehicleEconomyKey({truck:{make}}),brand,make);
 }
});
test('do not misclassify ambiguous, unknown, or spoofed trucks',()=>{
 for(const make of ['Fake DAF','DAFx','DAF Volvo','Scania Mercedes','Kenworth W900']){
  assert.notEqual(vehicleEconomyKey({truck:{make}}),'daf',make);
 }
 assert.equal(vehicleEconomyKey({truck:{make:'DAF Volvo'}}),'daf volvo');
 assert.equal(vehicleEconomyKey({truck:{make:'',model:'DAF XF 105'}}),'');
});
test('manufacturer rule continues for official buses',()=>{
 assert.equal(vehicleEconomyKey({truck:{make:'Volvo',model:'9700 DD',type:'bus'}}),'volvo');
 assert.equal(vehicleEconomyKey({truck:{make:'Scania Touring',type:'coach'}}),'scania');
 assert.equal(vehicleEconomyKey({truck:{make:'Unknown',model:'Marcopolo Paradiso',type:'bus'}}),'onibus');
});

test('all garage brands accept any model suffix after real manufacturer prefix',()=>{
 const brands={
  volvo:['Volvo','VOLVO FH16 750','Volvo-Y','Volvo_XX','volvo Trucks Próximo Modelo','Volvo Future 2030'],
  scania:['Scania','SCANIA S 770','Scania-R450','Scania Vabis 111','Scania Futuro X'],
  daf:['DAF','DAF XF 105','DAF-XF 2027','DAF Trucks XG+','DAF Future Y'],
  man:['MAN','MAN TGX','MAN-Y','MAN Truck 2099'],
  iveco:['Iveco','IVECO S-Way','Iveco EuroStar','Iveco Future Y'],
  mercedes:['Mercedes','Mercedes-Benz Actros','MercedesBenz eActros','MB 1113','Mercedes Future Y'],
  renault:['Renault','Renault Trucks T','Renault-T High','Renault Future Y']
 };
 for(const [brand,models] of Object.entries(brands)){
  for(const make of models){
   assert.equal(truckBrandKey(make),brand,make);
   assert.equal(vehicleEconomyKey({truck:{make,model:'Qualquer modelo'}}),brand,make);
  }
 }
});

test('recognizes maker in alternate telemetry fields without guessing from model',()=>{
 for(const field of ['make','manufacturer','brand','makeName','manufacturerName']){
  assert.equal(vehicleEconomyKey({truck:{[field]:'VOLVO XX',model:'Qualquer modelo'}}),'volvo',field);
 }
 assert.equal(vehicleEconomyKey({truck:{make:' ',manufacturer:'DAF XF 105'}}),'daf');
 assert.equal(vehicleEconomyKey({truck:{make:' ',makeName:'Scania R'}}),'scania');
 assert.equal(vehicleEconomyKey({truck:{make:'',model:'Volvo FH',id:'volvo.fh'}}),'');
});

test('unknown makes and ambiguous cross-brand names never grant a brand',()=>{
 const invalid=['Novolvo X','VolvoX','MANager Truck','Scania Volvo','Mercedes Volvo','Volvo DAF','DAF Scania','Renault MAN','Iveco DAF','Fake Scania'];
 for(const name of invalid){
  assert.ok(!/^(volvo|scania|daf|man|iveco|mercedes|renault)$/.test(vehicleEconomyKey({truck:{make:name}})),name);
 }
});
