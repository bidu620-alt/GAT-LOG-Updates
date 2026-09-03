import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';

const runtime=path.resolve('server-local/runtime');
const worker=fs.readFileSync(path.join(runtime,'worker.js'),'utf8');
const schema=fs.readFileSync(path.join(runtime,'schema.sql'),'utf8');
const host=fs.readFileSync(path.join(runtime,'host.mjs'),'utf8');
const rankText=fs.readFileSync(path.join(runtime,'ranking-telemetry.js'),'utf8');
const rank=await import(pathToFileURL(path.join(runtime,'ranking-telemetry.js')).href+'?v151='+Date.now());

const good={eligible:true,reason:null,missing_damage:[],client_version:'1.0.31',minimum_version:'1.0.28'};
const missing={eligible:false,reason:'damage_data_incomplete',missing_damage:['trailer'],client_version:'1.0.31',minimum_version:'1.0.28'};

test('1.0.51 instala armazenamento de viagens abertas e aliases',()=>{
  assert.match(worker,/const VERSION='1\.0\.51-local'/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trips/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trip_aliases/);
  assert.match(worker,/prepareOpenJourney/);
  assert.match(worker,/journeyFingerprintRaw/);
  assert.match(worker,/canonical_trip_id/);
});

test('idle, troca de jogo ou novo trip id nao sao cancelamento automatico',()=>{
  assert.doesNotMatch(worker,/observedIdle\|\|tripReplaced\|\|gatJobEvent==='cancelled'/);
  assert.match(worker,/raw\?\.game\?\.connected===true&&\(gatJobEvent==='cancelled'/);
  assert.match(worker,/saveOpenJourney\(env,user,current,raw,t,'suspended'\)/);
});

test('pacote de dano transitorio depois de rank verificado nao zera a viagem',()=>{
  const t0='2026-09-03T20:00:00.000Z';
  let g={reason:null,verified_at:t0,last_sample_at:t0,valid_samples:2};
  g=rank.advanceRankGuard(g,missing,t0,'2026-09-03T20:00:02.000Z',true);
  assert.equal(g.reason,null);
  assert.equal(g.transient_invalid_reason,'damage_data_incomplete');
  g=rank.advanceRankGuard(g,good,'2026-09-03T20:00:02.000Z','2026-09-03T20:00:04.000Z',true);
  assert.equal(g.reason,null);
  assert.equal(g.transient_invalid_reason,undefined);
});

test('gap longo entra em retomada e recupera com duas amostras validas',()=>{
  const t0='2026-09-03T20:00:00.000Z';
  let g={reason:null,verified_at:t0,last_sample_at:t0,valid_samples:2};
  g=rank.advanceRankGuard(g,good,t0,'2026-09-03T20:03:00.000Z',true);
  assert.equal(g.reason,'telemetry_resume_pending');
  assert.equal(g.resume_valid_samples,1);
  g=rank.advanceRankGuard(g,good,'2026-09-03T20:03:00.000Z','2026-09-03T20:03:02.000Z',true);
  assert.equal(g.reason,null);
  assert.ok(g.resumed_verified_at);
});

test('regra inicial continua exigindo telemetria valida e plugin de danos',()=>{
  assert.match(rankText,/MIN_RANK_CLIENT = '1\.0\.28'/);
  assert.match(rankText,/incompatible_damage_plugin/);
  assert.match(rankText,/preflight_truck_damage_ready/);
  assert.match(rankText,/RESUME_VALID_SAMPLES = 2/);
});

test('reparo do Eduardo usa evidencias persistidas e devolve 95 GAT',()=>{
  assert.match(host,/repair_eduardovidal_loader_2026_09_03_v151/);
  assert.match(host,/transient_final_damage_packet/);
  assert.match(host,/100-Math\.max\(0,Number\(a\.gat_speed_penalty_points\)/);
  assert.match(host,/gat_points:points/);
});
