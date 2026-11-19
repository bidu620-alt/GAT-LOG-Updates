import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,writeFileSync,rmSync,readFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {summarize,collectSave} from './reader.mts';
const save=`SiiNunit
{
economy : _nameless.e {
 game_time: 100
 bus_job_log: _nameless.log
}
player : _nameless.p {
 assigned_vehicles_mode: bus
 current_job: null
 current_bus_job: _nameless.bus
 current_car_job: null
 assigned_vehicles: _nameless.group
}
player_bus_job : _nameless.bus {
 source_company: company.volatile.terminal.recife
 target_company: company.volatile.terminal.natal
 service_kind: unknown_official_kind
 stops: 2
 stops[0]: _nameless.stop
 stops[1]: _nameless.stop2
}
bus_stop : _nameless.stop {
 city: recife
}
bus_stop : _nameless.stop2 {
 city: natal
}
player_vehicles : _nameless.group {
 vehicle: _nameless.vehicle
}
vehicle : _nameless.vehicle {
 accessories: 1
 accessories[0]: _nameless.acc
}
vehicle_accessory : _nameless.acc {
 data_path: "/def/vehicle/truck/volvo.bus/data.sii"
}
bus_job_log : _nameless.log {
 version: 1
 entries: 1
 entries[0]: _nameless.record
}
bus_job_log_entry : _nameless.record {
 params: 1
 params[0]: "opaque completed data"
}
bank : _nameless.bank {
 money_account: 99999999
}
}
`;
test('reads official bus pointer, opaque attributes, stops, vehicle and bus history',()=>{
 const r=summarize(save,'424944555A414F');
 assert.equal(r.profile_name,'BIDUZAO');assert.equal(r.job_kind,'bus_job');
 assert.equal(r.jobs.current_bus_job.fields.service_kind,'unknown_official_kind');
 assert.equal(r.related_units.find(u=>u.id==='_nameless.stop2').fields.city,'natal');
 assert.equal(r.history.bus_job_log.entries.length,1);
 assert.deepEqual(r.vehicle_definitions,['"/def/vehicle/truck/volvo.bus/data.sii"']);
 assert.ok(!JSON.stringify(r).includes('money_account'));
 assert.ok(!JSON.stringify(r).includes('99999999'));
});
test('car work does not become passenger work and null bus stays null',()=>{
 const s=save.replace('assigned_vehicles_mode: bus','assigned_vehicles_mode: car').replace('current_bus_job: _nameless.bus','current_bus_job: null').replace('current_car_job: null','current_car_job: _nameless.bus');
 const r=summarize(s,'424944555A414F');assert.equal(r.job_kind,'car_job');assert.equal(r.jobs.current_bus_job,null);
});
test('plaintext snapshot is read without modification and never treated as proof of completion',()=>{
 const dir=mkdtempSync(join(tmpdir(),'bidu-save-test-'));
 try{
  writeFileSync(join(dir,'game.sii'),save);writeFileSync(join(dir,'info.sii'),'SiiNunit\n{\nsave_container : _nameless.info {\n version: 105\n dependencies: 0\n}\n}\n');
  const r=collectSave(dir,'424944555A414F');assert.equal(r.status,'snapshot_recent');
  assert.equal(r.history_is_completion_proof,false);assert.match(r.save.game_sha256,/^[a-f0-9]{64}$/);
  assert.equal(readFileSync(join(dir,'game.sii'),'utf8'),save);
  assert.throws(()=>collectSave(dir,'../other'),/invalid_profile_id/);
 }finally{rmSync(dir,{recursive:true,force:true});}
});
