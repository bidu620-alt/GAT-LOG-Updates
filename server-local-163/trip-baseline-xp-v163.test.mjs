import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const worker=readFileSync('server-local/runtime/worker.js','utf8');

test('v163 uses 1 km = 1 XP without changing GAT points formula',()=>{
  assert.match(worker,/const VERSION='1\.0\.63-local'/);
  assert.ok(worker.includes('baseXP=Math.max(0,Math.floor(distance))'));
  assert.ok(worker.includes('xp=baseXP'));
  assert.ok(worker.includes("xp_rule:'1km_1xp'"));
  assert.ok(worker.includes('gatBasePoints=Math.floor(distance/99)*10'));
  assert.ok(worker.includes('gatPoints=rankEligible?Math.max(0,gatBasePoints-pointPenalty):0'));
  assert.ok(!worker.includes('baseXP=Math.floor(distance/100)*20'));
});

test('v163 starts penalties at cargo attachment and uses per-trip deltas',()=>{
  assert.ok(worker.includes("if(hasLoadedJob&&m.state==='active'&&(!m.scoring_started_at"));
  assert.ok(worker.includes('m.truck_damage_start_pct=truckNow'));
  assert.ok(worker.includes('m.cargo_damage_start_pct=cargoDamageNow'));
  assert.ok(worker.includes('m.speed_fines_start=fineNow===null?null:fineNow'));
  assert.ok(worker.includes('damage=damageDelta(cargoFinalDamage,cargoStartDamage)'));
  assert.ok(worker.includes('liveFineFinal===null?deliveredFines:Math.max(0,liveFineFinal-fineStart)'));
});
