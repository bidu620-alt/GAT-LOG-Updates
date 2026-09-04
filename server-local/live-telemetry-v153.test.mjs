import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

const runtime=path.resolve('server-local/runtime');
const worker=fs.readFileSync(path.join(runtime,'worker.js'),'utf8');
const schema=fs.readFileSync(path.join(runtime,'schema.sql'),'utf8');
const host=fs.readFileSync(path.join(runtime,'host.mjs'),'utf8');

test('1.0.53 grava open_trips antes do alias com foreign key',()=>{
  assert.match(worker,/const VERSION='1\.0\.53-local'/);
  const safe="canonical=await saveOpenJourney(env,user,current,raw,t,'active')||canonical;\n     if(observed&&canonical)await openJourneyAlias(env,user,observed,canonical,t);";
  assert.ok(worker.includes(safe),'viagem canonica deve existir antes do alias observado');
  assert.ok(!worker.includes("if(observed){current.trip_id=observed;current.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}"),'ordem antiga FK-invalida nao pode voltar');
});

test('schema e startup garantem tabelas de viagens em banco existente',()=>{
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trips/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS open_trip_aliases/);
  assert.match(schema,/FOREIGN KEY\(trip_id\) REFERENCES open_trips\(trip_id\) ON DELETE CASCADE/);
  assert.match(host,/db\.sql\.exec\(readFileSync\(join\(here,'schema\.sql'\),'utf8'\)\)/);
});

test('telemetry_live continua sendo persistida antes do processamento da missao',()=>{
  const live=worker.indexOf("INSERT INTO telemetry_live(driver,account_user,device_id,updated_at,telemetry_json)");
  const journey=worker.indexOf('prepareOpenJourney(env,account,raw,t,loaded,delivered||cancelled)');
  assert.ok(live>=0,'persistencia de telemetry_live ausente');
  assert.ok(journey>live,'processamento de viagem deve ocorrer depois do registro live');
});
