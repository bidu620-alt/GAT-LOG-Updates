import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const worker=readFileSync('server-local/runtime/worker.js','utf8');

const gat=(distance,penalty=0)=>Math.max(0,Math.floor(distance/99)*10-penalty);

test('1.0.58 keeps XP at 20 per full 100 km',()=>{
  assert.match(worker,/const VERSION='1\.0\.58-local'/);
  assert.match(worker,/baseXP=Math\.floor\(distance\/100\)\*20/);
  assert.match(worker,/xp=baseXP/);
});

test('GAT points are progressive every full 99 km',()=>{
  assert.match(worker,/gatBasePoints=Math\.floor\(distance\/99\)\*10/);
  assert.match(worker,/gatPoints=rankEligible\?Math\.max\(0,gatBasePoints-pointPenalty\):0/);
  assert.match(worker,/gat_base_points:gatBasePoints/);
  assert.equal(gat(0),0);
  assert.equal(gat(98.99),0);
  assert.equal(gat(99),10);
  assert.equal(gat(197.99),10);
  assert.equal(gat(198),20);
  assert.equal(gat(495),50);
  assert.equal(gat(500),50);
  assert.equal(gat(990),100);
  assert.equal(gat(1980),200);
});

test('existing penalties are subtracted from the distance base',()=>{
  assert.match(worker,/gatSpeedPenalty=adminTest\?0:fines\*3/);
  assert.match(worker,/gatCargoPenalty=adminTest\?0:scoreTier\(damage,3,7,15\)/);
  assert.match(worker,/gatTruckPenalty=adminTest\?0:scoreTier\(truckDamage,5,10,20\)/);
  assert.equal(gat(500,7),43);
  assert.equal(gat(99,20),0);
  assert.equal(gat(1980,25),175);
});

test('500 km minimum and fixed 100 point base are removed',()=>{
  assert.match(worker,/rankReason=null,rankEligible=true/);
  assert.doesNotMatch(worker,/gat_distance_below_500/);
  assert.doesNotMatch(worker,/gatPoints=rankEligible\?Math\.max\(0,100-pointPenalty\):0/);
  assert.doesNotMatch(worker,/gat_base_points:100/);
  assert.match(worker,/scoring:\{base_per_delivery:0,distance_block_km:99,points_per_block:10\}/);
  assert.match(worker,/Pontos GAT calculados por distancia: 10 pontos a cada 99 km completos, menos as penalidades da viagem\./);
});