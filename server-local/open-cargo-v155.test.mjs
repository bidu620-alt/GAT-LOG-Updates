import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const worker=readFileSync(new URL('./runtime/worker.js',import.meta.url),'utf8');

test('1.0.55 aceita qualquer carga e remove classificacao obrigatoria',()=>{
  assert.match(worker,/const VERSION='1\.0\.55-local'/);
  assert.match(worker,/catalog_id:'__open_cargo__'/);
  assert.match(worker,/open_cargo:true/);
  assert.match(worker,/cargo_mode:'open'/);
  assert.match(worker,/cargo_rule:'none'/);
  assert.match(worker,/cargo_history:c\.results\|\|\[\]/);
  assert.doesNotMatch(worker,/reason:'cargo_not_compatible'/);
  assert.doesNotMatch(worker,/delivery_completed_pending_classification/);
  assert.doesNotMatch(worker,/classification_status:'pending'/);
  assert.doesNotMatch(worker,/\/api\/site\/admin\/classify/);
  assert.doesNotMatch(worker,/\/api\/site\/admin\/unclassified/);
  assert.doesNotMatch(worker,/autoClassifyCargo/);
  assert.doesNotMatch(worker,/learnCargoAlias/);
  assert.doesNotMatch(worker,/cargo_classification_queue/);
});

test('1.0.55 preserva processamento da viagem e caixa-preta 1.0.54',()=>{
  assert.match(worker,/async function processMission\(/);
  assert.match(worker,/inspectClientPacket/);
  assert.match(worker,/persistClientPacket/);
  assert.match(worker,/createHmac/);
  assert.match(worker,/journal_signature_invalid/);
  assert.match(worker,/journal_chain_gap/);
});
