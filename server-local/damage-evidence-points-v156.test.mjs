import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');

test('v1.56 recovers damage-only ranking failures after prior verification',()=>{
  assert.match(worker,/const VERSION='1\.0\.56-local'/);
  assert.match(worker,/function damageRankCanAutoScore/);
  assert.match(worker,/if\(!g\.verified_at\)return false/);
  assert.match(worker,/reason==='damage_data_incomplete'/);
  assert.match(worker,/reason!=='telemetry_resume_pending'/);
  assert.match(worker,/trigger==='damage_data_incomplete'/);
  assert.match(worker,/trigger==='migrated_damage_data_incomplete'/);
  assert.match(worker,/rankDamageAutoRecovered=damageRankCanAutoScore\(m,rankGuardReason\)/);
});

test('v1.56 hotfix recovers false startup guard only with strong start evidence',()=>{
  assert.match(worker,/function startupRankCanAutoScore/);
  assert.match(worker,/reason!=='telemetry_not_verified_from_start'/);
  assert.match(worker,/m\?\.trip_progress_confirmed!==true/);
  assert.match(worker,/preflight_truck_damage_ready===true/);
  assert.match(worker,/valid_samples/);
  assert.match(worker,/lastInvalid&&lastInvalid!=='damage_data_incomplete'/);
  assert.match(worker,/lastInvalid==='damage_data_incomplete'&&!preflight/);
  assert.match(worker,/planned_distance_km/);
  assert.match(worker,/start_remaining_km/);
  assert.match(worker,/rbr_start_remaining_km/);
  assert.match(worker,/ratio>=0\.80&&ratio<=1\.25/);
  assert.match(worker,/rankStartupAutoRecovered=startupRankCanAutoScore\(m,rankGuardReason\)/);
  assert.match(worker,/rankEffectiveGuardReason=\(rankDamageAutoRecovered\|\|rankStartupAutoRecovered\)\?null:rankGuardReason/);
});

test('startup evidence keeps obviously late telemetry in manual review',()=>{
  const helperStart=worker.indexOf('function startupRankCanAutoScore');
  const helperEnd=worker.indexOf('async function processMission',helperStart);
  assert.ok(helperStart>=0&&helperEnd>helperStart);
  const helper=worker.slice(helperStart,helperEnd);
  for(const reason of ['telemetry_gap','telemetry_disconnected','client_update_required','local_journal_invalid','trip_progress_unverified']){
    assert.doesNotMatch(helper,new RegExp(reason));
  }
  const emulate=(m,reason)=>{
    const g=m?.rank_guard||{};
    if(reason!=='telemetry_not_verified_from_start'||g.verified_at||m?.trip_progress_confirmed!==true)return false;
    const lastInvalid=String(g.last_invalid_reason||'');
    if(lastInvalid&&lastInvalid!=='damage_data_incomplete')return false;
    const preflight=g.preflight_truck_damage_ready===true;
    const startupSample=Math.max(0,Number(g.valid_samples)||0)>=1;
    if(!preflight&&!startupSample)return false;
    if(lastInvalid==='damage_data_incomplete'&&!preflight)return false;
    const planned=Math.max(0,Number(m?.planned_distance_km)||0);
    const start=Math.max(0,Number(m?.start_remaining_km)||0,Number(m?.rbr_start_remaining_km)||0);
    if(planned<=0||start<=0)return false;
    const ratio=start/planned;
    return ratio>=0.80&&ratio<=1.25;
  };
  assert.equal(emulate({planned_distance_km:1465,start_remaining_km:1525.649,trip_progress_confirmed:true,rank_guard:{valid_samples:1}},'telemetry_not_verified_from_start'),true);
  assert.equal(emulate({planned_distance_km:4459,start_remaining_km:5.463,trip_progress_confirmed:true,rank_guard:{valid_samples:1}},'telemetry_not_verified_from_start'),false);
  assert.equal(emulate({planned_distance_km:1000,start_remaining_km:1000,trip_progress_confirmed:true,rank_guard:{valid_samples:0,last_invalid_reason:'client_update_required'}},'telemetry_not_verified_from_start'),false);
  assert.equal(emulate({planned_distance_km:1000,start_remaining_km:1000,trip_progress_confirmed:true,rank_guard:{valid_samples:0,last_invalid_reason:'damage_data_incomplete'}},'telemetry_not_verified_from_start'),false);
  assert.equal(emulate({planned_distance_km:1000,start_remaining_km:1000,trip_progress_confirmed:true,rank_guard:{valid_samples:0,last_invalid_reason:'damage_data_incomplete',preflight_truck_damage_ready:true}},'telemetry_not_verified_from_start'),true);
});

test('automatic recovery is explicit in audit and manual review remains available',()=>{
  assert.match(worker,/automatic_ranking_reason:\(rankDamageAutoRecovered\|\|rankStartupAutoRecovered\)\?rankGuardReason:null/);
  assert.match(worker,/ranking_recovered_from_damage_evidence:rankDamageAutoRecovered/);
  assert.match(worker,/ranking_recovered_from_start_evidence:rankStartupAutoRecovered/);
  assert.match(worker,/telemetria foi comprovada perto do inicio e o progresso real da viagem foi confirmado/);
  assert.match(worker,/function gatReviewSuggestion/);
  assert.match(worker,/gat_review_suggested_points/);
  assert.match(worker,/review_gat_points/);
  assert.match(worker,/gat_manual_review/);
});
