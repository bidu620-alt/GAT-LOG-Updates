import test from 'node:test';
import assert from 'node:assert/strict';
import {budgetState} from '../budget-guard.js';
test('migration pause survives quota renewal without changing normal budget checks',()=>{
  const at=Date.now(),snapshot={date_utc:new Date(at).toISOString().slice(0,10),checked_at:new Date(at).toISOString(),rows_read:0,rows_written:0};
  const env={GAT_D1_BUDGET:JSON.stringify(snapshot)};
  assert.equal(budgetState(env,at).paused,false);
  assert.equal(budgetState({...env,GAT_MIGRATION_PAUSED:'1'},at).reason,'migration');
  assert.equal(budgetState({GAT_MIGRATION_PAUSED:'1'},at+86400000).reason,'migration');
});
