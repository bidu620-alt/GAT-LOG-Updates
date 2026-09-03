import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');

test('1.0.50 public ranking exposes career delivery and km totals',()=>{
  assert.match(worker,/const VERSION='1\.0\.50-local'/);
  const start=worker.indexOf("if(p==='/api/public/ranking'&&m==='GET')");
  const end=worker.indexOf("if(p==='/api/public/safety-ranking'",start);
  const route=worker.slice(start,end);
  assert.match(route,/p\.total_deliveries/);
  assert.match(route,/p\.total_km/);
  assert.doesNotMatch(route,/monthly_goal/);
  assert.doesNotMatch(route,/max_monthly/);
  assert.match(route,/scoring:\{base_per_delivery:100\}/);
});

test('1.0.50 admin driver list also uses career totals without monthly goal',()=>{
  const start=worker.indexOf("if(p==='/api/site/admin/drivers'&&m==='POST')");
  const end=worker.indexOf("if(p==='/api/site/admin/driver'&&m==='POST')",start);
  const route=worker.slice(start,end);
  assert.match(route,/p\.total_deliveries/);
  assert.match(route,/total_deliveries:Number\(x\.total_deliveries\|\|0\)/);
  assert.match(route,/total_km:Number\(x\.total_km\|\|0\)/);
  assert.doesNotMatch(route,/monthly_goal/);
});
