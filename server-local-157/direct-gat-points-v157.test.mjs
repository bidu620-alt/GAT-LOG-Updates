import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const worker=readFileSync('server-local/runtime/worker.js','utf8');
const host=readFileSync('server-local/runtime/host.mjs','utf8');

test('1.0.57 keeps XP independent at 20 per full 100 km',()=>{
  assert.match(worker,/const VERSION='1\.0\.57-local'/);
  assert.match(worker,/baseXP=Math\.floor\(distance\/100\)\*20/);
  assert.match(worker,/xp=baseXP/);
});

test('GAT points use only accepted delivery plus 500 km minimum',()=>{
  assert.match(worker,/rankReason=distance>=500\?null:'gat_distance_below_500'/);
  assert.match(worker,/gatPoints=rankEligible\?Math\.max\(0,100-pointPenalty\):0/);
  assert.match(worker,/gatSpeedPenalty=adminTest\?0:fines\*3/);
  assert.match(worker,/gatCargoPenalty=adminTest\?0:scoreTier\(damage,3,7,15\)/);
  assert.match(worker,/gatTruckPenalty=adminTest\?0:scoreTier\(truckDamage,5,10,20\)/);
  assert.doesNotMatch(worker,/rankEffectiveGuardReason=\(rankDamageAutoRecovered\|\|rankStartupAutoRecovered\)\?null:rankGuardReason/);
  assert.match(worker,/ranking_integrity_reason:rankGuardReason\|\|null/);
});

test('trips below 500 km still record XP and history but zero GAT points',()=>{
  assert.match(worker,/gat_distance_below_500/);
  assert.match(worker,/Viagem registrada e XP calculado normalmente, mas Pontos GAT exigem no minimo 500 km/);
  assert.match(worker,/history_recorded:true/);
  assert.match(worker,/xp=xp\+\?,points=points\+\?/);
});

test('journal validation happens before server mutates delivered trailer fields',()=>{
  const inspect=worker.indexOf("const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);");
  const restore=worker.indexOf('restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);');
  assert.ok(inspect>=0,'inspectClientPacket call missing');
  assert.ok(restore>=0,'restoreDeliveredTrailer call missing');
  assert.ok(inspect<restore,'journal must be checked before restoreDeliveredTrailer mutates raw');
  assert.equal(worker.match(/const packetState=await inspectClientPacket\(env,account,device,String\(b\.token\|\|''\),raw,t\);/g)?.length,1);
});

test('signed journal packets are not deferred before persistence and broken state resets once',()=>{
  assert.match(worker,/if\(!packetState\.journal_present&&!queuedTimeOK/);
  assert.match(host,/function resetJournalStateV157/);
  assert.match(host,/journal_state_reset_v157_direct_points/);
  assert.match(host,/DELETE FROM telemetry_journal_state/);
  assert.match(host,/resetJournalStateV157\(db\);/);
});
