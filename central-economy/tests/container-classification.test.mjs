import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {classifyCargoId,getCargoIds} from '../cargo-library/index.js';
import {correctNovemberSnapshot,CORRECTED_CONTAINER_IDS} from '../scripts/migrate-container-tiers.mjs';
for(const id of CORRECTED_CONTAINER_IDS)assert.equal(classifyCargoId(id),'conteineres',id);
for(const [id,expected]of Object.entries({tractor_c:'maquinario',beverages:'carga_seca',truck_batt:'carga_seca',carrots:'frigorifico',unknown_c:null}))assert.equal(classifyCargoId(id),expected,id);
const original={id:'2026-11',product_id:'pass_2026_11',starts_at:'2026-11-01T00:00:00-03:00',ends_at:'2026-11-30T23:50:00-03:00',goals:[{id:'conteineres',target:3}],cargo_ids:{conteineres:['beverages_c'],carga_seca:['beverages','truck_batt_c','exhausts_c'],maquinario:['tractor_c']}};
const fixed=correctNovemberSnapshot(original);
assert.equal(fixed.starts_at,original.starts_at);assert.equal(fixed.ends_at,original.ends_at);assert.deepEqual(fixed.goals,original.goals);
assert.deepEqual(correctNovemberSnapshot(fixed),fixed);assert.deepEqual(original.cargo_ids.carga_seca,['beverages','truck_batt_c','exhausts_c']);
assert(!fixed.cargo_ids.carga_seca.includes('truck_batt_c'));assert(fixed.cargo_ids.carga_seca.includes('beverages'));assert(fixed.cargo_ids.maquinario.includes('tractor_c'));
assert.deepEqual(correctNovemberSnapshot({...original,id:'2026-10'}),{...original,id:'2026-10'});
console.log('PASS: 13 confirmed IDs, base variants unchanged, tractor counterexample, no suffix fallback, immutable dates/goals, isolated/idempotent saved-rule correction.');

