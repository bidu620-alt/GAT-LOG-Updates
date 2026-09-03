import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');

test('v1.52 exposes safe manual GAT review',()=>{
  assert.match(worker,/const VERSION='1\.0\.52-local'/);
  assert.match(worker,/function gatReviewSuggestion/);
  assert.match(worker,/gat_review_suggested_points/);
  assert.match(worker,/review_gat_points/);
  assert.match(worker,/gat_review_already_done/);
  assert.match(worker,/gat_review_no_saved_breakdown/);
  assert.match(worker,/gat_manual_review/);
  assert.match(worker,/automatic_ranking_reason/);
});

test('moderator can review GAT points but does not gain general admin power',()=>{
  assert.match(worker,/actor\.role==='moderator'&&!\['reset_mission','review_gat_points'\]\.includes\(action\)/);
  assert.match(worker,/if\(!POWER\.has\(actor\.role\)\)throw new HttpError\(403,'forbidden'\)/);
});

test('approval uses saved penalty breakdown, is idempotent and audited',()=>{
  assert.match(worker,/gat_base_points/);
  assert.match(worker,/gat_speed_penalty_points/);
  assert.match(worker,/gat_cargo_penalty_points/);
  assert.match(worker,/gat_truck_penalty_points/);
  assert.match(worker,/points=MAX\(0,points\+\?\)/);
  assert.match(worker,/await audit\(env,actor\.user,'review_gat_points'/);
  assert.match(worker,/invalidateRead\('profile:'\+target\)/);
});

test('manual approval never claims telemetry was automatically verified',()=>{
  assert.doesNotMatch(worker,/aData\.rank_verified\s*=\s*true/);
  assert.match(worker,/aData\.ranking_eligible=true/);
  assert.match(worker,/aData\.rank_eligible=true/);
});
