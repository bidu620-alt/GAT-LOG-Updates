import test from 'node:test';import assert from 'node:assert/strict';
import {inspectSaveEvidence,receiveSaveEvidence} from '../save-evidence.js';
import {LocalDatabase} from '../database.mjs';
const time='2026-11-19T12:00:30Z';
const raw={gat_profile_id:'42494455',gat_save_evidence:{schema:1,source:'ets2_save_readonly',full_snapshot:true,profile_id:'42494455',save:{game_sha256:'a'.repeat(64),game_modified_at:'2026-11-19T12:00:00Z'},jobs:{current_bus_job:{type:'player_bus_job',id:'x',fields:{unknown_kind:'new'}}}}};
test('requires signed journal and exact active profile',()=>{
 assert.equal(inspectSaveEvidence(raw,false,time).reason,'journal_not_verified');
 assert.equal(inspectSaveEvidence({...raw,gat_profile_id:'ABCD'},true,time).reason,'profile_mismatch');
});
test('fresh bus save is evidence pending rules, never a reward',()=>{
 const r=inspectSaveEvidence(raw,true,time);assert.equal(r.status,'stored_pending_rules');assert.equal(r.job_kind,'bus_job');assert.equal(r.eligible_for_automatic_credit,false);
});
test('stale snapshots and courier jobs cannot imply completed passengers',()=>{
 assert.equal(inspectSaveEvidence(raw,true,'2026-11-19T13:00:00Z').status,'stored_stale');
 const x=structuredClone(raw);x.gat_save_evidence.jobs={current_car_job:{}};
 assert.equal(inspectSaveEvidence(x,true,time).reason,'car_job_is_not_necessarily_passengers');
});
test('persists snapshot idempotently and retains unknown fields without a deliveries table',async()=>{
 const db=new LocalDatabase(':memory:');try{
  await receiveSaveEvidence(db,'driver',raw,true,time);await receiveSaveEvidence(db,'driver',raw,true,time);
  const rows=db.sql.prepare('SELECT * FROM save_evidence_snapshots').all();assert.equal(rows.length,1);
  assert.equal(JSON.parse(rows[0].payload_json).jobs.current_bus_job.fields.unknown_kind,'new');
  assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM sqlite_master WHERE name='deliveries'").get().n,0);
 }finally{db.close();}
});
test('invalid or unsigned evidence creates no snapshot storage',async()=>{
 const db=new LocalDatabase(':memory:');try{
  await receiveSaveEvidence(db,'driver',raw,false,time);
  assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM sqlite_master WHERE name='save_evidence_snapshots'").get().n,0);
 }finally{db.close();}
});
