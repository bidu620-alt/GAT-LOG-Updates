import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {runInNewContext} from 'node:vm';
import {LocalDatabase} from '../database.mjs';
import {ensureEconomy} from '../economy-official.js';
import worker from '../worker.js';

test('events and career achievements use complete history, not latest 100 or current month', async () => {
 const db=new LocalDatabase(':memory:');
 try {
  db.sql.exec(readFileSync(new URL('../schema.sql',import.meta.url),'utf8'));
  ensureEconomy(db);
  const user='julianos',now='2026-11-03T12:00:00Z';
  db.sql.prepare('INSERT INTO accounts(user,created_at,updated_at) VALUES(?,?,?)').run(user,now,now);
  db.sql.prepare('INSERT INTO profiles(user,updated_at) VALUES(?,?)').run(user,now);
  const insert=db.sql.prepare(`INSERT INTO deliveries
   (user,source,destination,cargo,weight_kg,distance_km,delivered_at,raw_json)
   VALUES(?,?,?,?,?,?,?,?)`);
  const route=[
   ['Toys','toys','Porto','Barcelona'],
   ['Chocolate','chocolate','Barcelona','Gênova'],
   ['Chewing Gum','chewing_gums','Gênova','Viena'],
   ['Milk','milk','Viena','Varsóvia'],
   ['Clothes','clothes','Varsóvia','Budapeste'],
   ['Yoghurt','yoghurt','Budapeste','Tessalônica']
  ];
  // A matching delivery before event launch must never count.
  insert.run(user,'Porto','Barcelona','Toys',1000,600,'2026-10-04T11:00:00-03:00',JSON.stringify({mission:{cargo_id:'toys',state:'active'}}));
  for(let i=0;i<route.length;i++){
   const [cargo,id,source,destination]=route[i];
   const date='2026-10-'+String(6+i).padStart(2,'0')+'T11:00:00-03:00';
   insert.run(user,source,destination,cargo,10000,600,date,JSON.stringify({mission:{cargo_id:id,state:'active'},audit:{cargo_damage_pct:0}}));
  }
  // New November trips push every event step out of profile.deliveries (100).
  for(let i=0;i<150;i++){
   insert.run(user,'Dijon','Lyon','Pallets',5000,120,'2026-11-02T10:00:00-03:00',JSON.stringify({mission:{cargo_id:'empty_palet',state:'active'}}));
  }
  db.sql.prepare('INSERT INTO meta(key,value) VALUES(?,?)').run('achievement:'+user+':primeiros_passos',JSON.stringify({id:'primeiros_passos',title:'Primeiros Passos',xp:250,unlocked_at:'2026-10-07T11:00:00-03:00'}));
  const response=await worker.fetch(new Request('https://api.bidulog.com.br/api/public/driver?user='+user),{DB:db},{waitUntil:()=>{}});
  assert.equal(response.status,200);
  const data=await response.json(),p=data.profile;
  assert.equal(p.deliveries.length,100);
  assert.equal(p.event_history.length,157);
  assert.equal(p.event_history[0].cargo_id,'toys');
  assert.equal(p.event_history[0].raw_json,undefined);
  assert.equal(p.achievement_stats.deliveries,157);
  assert.ok(p.achievements.some(a=>a.id==='primeiros_passos'&&a.xp===250));
  const window={};
  runInNewContext(readFileSync(new URL('../../docs/event-registry.js',import.meta.url),'utf8'),{window});
  const progress=window.GATEventRegistry.progress(p,'children_2026');
  assert.equal(progress.count,6);
  assert.equal(progress.completed,true);
  assert.equal(progress.matched.size,6);
 } finally { db.close(); }
});
