import fs from 'node:fs';
import test from 'node:test';
import assert from 'node:assert/strict';

const worker=fs.readFileSync('server-local/runtime/worker.js','utf8');
const schema=fs.readFileSync('server-local/runtime/schema.sql','utf8');

test('GAT Server 1.0.59 exposes read-only radio state and protected admin control',()=>{
  assert.match(worker,/const VERSION='1\.0\.59-local'/);
  assert.ok(worker.includes("p==='/api/public/radio'&&m==='GET'"));
  assert.ok(worker.includes("p==='/api/site/admin/radio'&&m==='POST'"));
  assert.ok(worker.includes('await requireAdmin(req,env,b)'));
  assert.ok(worker.includes('invalid_youtube_playlist'));
  assert.ok(worker.includes('playlist_required'));
  assert.ok(worker.includes("await audit(env,s.user,'radio_update','radio'"));
});

test('radio schema is singleton and preserves state across restarts',()=>{
  assert.ok(schema.includes('CREATE TABLE IF NOT EXISTS radio_state'));
  assert.ok(schema.includes('id INTEGER PRIMARY KEY CHECK (id = 1)'));
  assert.ok(schema.includes('revision INTEGER NOT NULL DEFAULT 0'));
  assert.ok(schema.includes('INSERT OR IGNORE INTO radio_state'));
});

test('radio accepts only YouTube playlist identifiers',()=>{
  assert.ok(worker.includes("['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com','youtu.be']"));
  assert.ok(worker.includes("x.searchParams.get('list')"));
  assert.ok(worker.includes('/^[A-Za-z0-9_-]{10,100}$/'));
});
