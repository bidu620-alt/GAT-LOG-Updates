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
