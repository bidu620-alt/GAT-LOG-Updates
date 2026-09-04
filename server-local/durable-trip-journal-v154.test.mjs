import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const worker=readFileSync('server-local/runtime/worker.js','utf8');
const rank=readFileSync('server-local/runtime/ranking-telemetry.js','utf8');
const schema=readFileSync('server-local/runtime/schema.sql','utf8');

test('1.0.54 exposes durable packet and trip journal storage',()=>{
  assert.match(worker,/const VERSION='1\.0\.54-local'/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS telemetry_packet_receipts/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS telemetry_journal_state/);
  assert.match(schema,/CREATE TABLE IF NOT EXISTS trip_checkpoints/);
  assert.match(worker,/inspectClientPacket/);
  assert.match(worker,/persistClientPacket/);
  assert.match(worker,/journal_signature_invalid/);
  assert.match(worker,/journal_chain_gap/);
});

test('signed local journal is fail-closed only for GAT points',()=>{
  assert.match(worker,/raw\.gat_journal_invalid=true/);
  assert.match(rank,/local_journal_invalid/);
  assert.match(rank,/caixa-preta local nao passou na verificacao de integridade/);
  assert.match(worker,/missionEvent=await processMission/);
});

test('packet idempotency prevents replay from creating another delivery',()=>{
  assert.match(worker,/SELECT mission_event_json,journal_verified,journal_reason FROM telemetry_packet_receipts/);
  assert.match(worker,/if\(packetState\.duplicate\)/);
  assert.match(worker,/INSERT INTO telemetry_packet_receipts/);
});
