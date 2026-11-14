import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeSCSJob,scsJobKind} from '../scs-job-compat.js';

const sample={
 game:{connected:true,paused:false},
 bus_job:{active:true,sourceCity:'Aracaju',sourceCityId:'aracaju',destinationCity:'Maceió',destinationCityId:'maceio',plannedDistanceKm:210},
 truck:{make:'Volvo',model:'9700 DD'},
 gameplay:{onJob:false,jobDelivered:false},
 gat_client_version:'1.0.68.39'
};

test('bus_job is distinct from freight job with zero cargo weight',()=>{
 const before=JSON.stringify(sample);
 const job=normalizeSCSJob(sample);
 assert.equal(scsJobKind(sample),'bus_job');
 assert.equal(job.gat_scs_job_kind,'bus_job');
 assert.equal(job.gat_job_kind,'bus_job');
 assert.equal(job.on_job,true);
 assert.equal(job.cargo_name,'Passageiros');
 assert.equal(job.cargo_id,'');
 assert.equal(job.mass_kg,0);
 assert.equal(job.job.source_kind,'bus_job');
 assert.equal(job.job.cargo,'Passageiros');
 assert.equal(job.job.sourceCity,'Aracaju');
 assert.equal(job.job.destinationCity,'Maceió');
 assert.equal(job.job.plannedDistanceKm,210);
 assert.equal(JSON.stringify(sample),before,'Do not change the journal-signed original packet');
});

test('source_kind bus_job works with normalized plugin-style job and no bus_job property',()=>{
 const original={game:{connected:true},job:{source_kind:'bus_job',active:true,source_city:'Recife',destination_city:'Natal',planned_distance_km:275}};
 const t=normalizeSCSJob(original);
 assert.equal(t.on_job,true);
 assert.equal(t.job.sourceCity,'Recife');
 assert.equal(t.job.destinationCity,'Natal');
 assert.equal(t.job.plannedDistanceKm,275);
 assert.equal(t.mass_kg,0);
});

test('empty bus_job configuration does not clobber freight job',()=>{
 const t={game:{connected:true},job:{source_kind:'job',active:true,cargo:'Logs',cargoMass:24000,sourceCity:'A',destinationCity:'B'},bus_job:{}};
 assert.equal(scsJobKind(t),'job');
 assert.strictEqual(normalizeSCSJob(t),t);
});
test('bus_job with no route or disconnected ETS2 cannot claim active job',()=>{
 for(const t of [
  {game:{connected:true},bus_job:{active:true}},
  {game:{connected:false},gat_job_kind:'bus_job',bus_job:{active:true,sourceCity:'A',destinationCity:'B'}},
  {game:{connected:true},gat_job_kind:'bus_job',bus_job:{active:false,sourceCity:'A',destinationCity:'B'}}
 ]){
  const n=normalizeSCSJob(t);
  assert.notEqual(n.on_job,true);
 }
});
test('only explicit fresh bus delivery data can be mapped to a receipt',()=>{
 const t=normalizeSCSJob({...sample,gameplay:{...sample.gameplay,busJobDeliveredDetails:{distanceKm:190,revenue:5000,earnedXp:100}},gat_scs_event:'bus_job.delivered',bus_job:{...sample.bus_job,active:false}});
 assert.equal(t.gat_job_event,'delivered');
 assert.equal(t.on_job,false);
 assert.equal(t.gameplay.jobDeliveredDetails.distanceKm,190);
 assert.equal(t.gameplay.jobDelivered,true);
 const noReceipt=normalizeSCSJob({...sample,gat_scs_event:'bus_job.delivered',bus_job:{...sample.bus_job,active:false}});
 assert.equal(noReceipt.gameplay.jobDelivered,false);
 assert.equal(noReceipt.gameplay.jobDeliveredDetails,undefined);
});
test('unknown kinds and ordinary freight remain untouched',()=>{
 for(const t of [
  {job:{source_kind:'job',cargo:'Lumber',cargoMass:15000}},
  {game:{connected:true},job:{active:true,cargo:'Wood',cargoMass:1000}},
  {job:{source_kind:'car_job',active:true}}
 ]){
  assert.strictEqual(normalizeSCSJob(t),t);
 }
});
test('an empty freight configuration cannot mask active bus_job in the same packet',()=>{
 const raw={
  game:{connected:true},
  job:{source_kind:'job',active:false},
  bus_job:{active:true,sourceCity:'Recife',destinationCity:'Natal',plannedDistanceKm:300}
 };
 assert.equal(scsJobKind(raw),'bus_job');
 const normalized=normalizeSCSJob(raw);
 assert.equal(normalized.on_job,true);
 assert.equal(normalized.job.source_kind,'bus_job');
 assert.equal(normalized.cargo_name,'Passageiros');
});
