import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');
const host=fs.readFileSync('server-local/runtime/host.mjs','utf8');

test('1.0.49 stores completed trips independently from ranking eligibility',()=>{
  assert.match(worker,/const VERSION='1\.0\.49-local'/);
  assert.match(worker,/rankEligible=!rankReason/);
  assert.match(worker,/gatPoints=rankEligible\?Math\.max\(0,100-pointPenalty\):0/);
  assert.match(worker,/xp=baseXP,cargo=/);
  assert.match(worker,/history_recorded:true/);
  assert.match(worker,/ranking_reason:rankReason/);
  assert.doesNotMatch(worker,/reason:'route_already_used'/);
  assert.doesNotMatch(worker,/reason:'no_trip_progress'/);
});

test('1.0.49 keeps delivery persistence idempotent and unknown cargo recoverable',()=>{
  assert.match(worker,/INSERT OR IGNORE INTO mission_completions/);
  assert.match(worker,/delivery_completed_pending_classification/);
  assert.match(worker,/classification_status:'pending'/);
  assert.match(worker,/history_recorded:true/);
});

test('1.0.49 removes monthly target semantics but keeps monthly count',()=>{
  assert.match(worker,/monthly_deliveries:p\.monthly_completed/);
  assert.match(worker,/monthly_completed=monthly_completed\+1/);
  assert.doesNotMatch(worker,/monthly_goal:p\.monthly_goal/);
  assert.doesNotMatch(worker,/monthly_completed=MIN\(monthly_goal/);
  assert.match(host,/reconcileMonthlyTripCount/);
  assert.doesNotMatch(host,/reconcileMonthlyTripGoal/);
  assert.doesNotMatch(host,/x\/30/);
});

test('1.0.49 exposes full career cargo history for collection UI',()=>{
  assert.match(worker,/cargo_history:c\.results\|\|\[\]/);
  assert.match(worker,/SELECT cargo,weight_kg FROM deliveries WHERE user=\? GROUP BY cargo,weight_kg/);
});
