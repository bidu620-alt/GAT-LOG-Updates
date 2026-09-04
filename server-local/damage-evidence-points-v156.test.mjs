import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');

test('v1.56 recovers only damage-only ranking failures after prior verification',()=>{
  assert.match(worker,/const VERSION='1\.0\.56-local'/);
  assert.match(worker,/function damageRankCanAutoScore/);
  assert.match(worker,/if\(!g\.verified_at\)return false/);
  assert.match(worker,/reason==='damage_data_incomplete'/);
  assert.match(worker,/reason!=='telemetry_resume_pending'/);
  assert.match(worker,/trigger==='damage_data_incomplete'/);
  assert.match(worker,/trigger==='migrated_damage_data_incomplete'/);
  assert.match(worker,/rankDamageAutoRecovered=damageRankCanAutoScore\(m,rankGuardReason\)/);
  assert.match(worker,/rankEffectiveGuardReason=rankDamageAutoRecovered\?null:rankGuardReason/);
});

test('v1.56 does not auto-approve real telemetry or integrity suspicions',()=>{
  const start=worker.indexOf('function damageRankCanAutoScore');
  const end=worker.indexOf('async function processMission',start);
  assert.ok(start>=0&&end>start);
  const helper=worker.slice(start,end);
  for(const reason of ['telemetry_gap','telemetry_disconnected','client_update_required','local_journal_invalid','trip_progress_unverified']){
    assert.doesNotMatch(helper,new RegExp(reason));
  }
});

test('automatic damage recovery is explicit in audit and manual review remains available',()=>{
  assert.match(worker,/automatic_ranking_reason:rankDamageAutoRecovered\?rankGuardReason:null/);
  assert.match(worker,/ranking_recovered_from_damage_evidence:rankDamageAutoRecovered/);
  assert.match(worker,/Pontos GAT calculados automaticamente com os danos ja validados durante a viagem/);
  assert.match(worker,/function gatReviewSuggestion/);
  assert.match(worker,/gat_review_suggested_points/);
  assert.match(worker,/review_gat_points/);
  assert.match(worker,/gat_manual_review/);
});
