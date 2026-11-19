import test from 'node:test';import assert from 'node:assert/strict';import {createHash,createHmac} from 'node:crypto';import {readFileSync} from 'node:fs';import {LocalDatabase} from '../database.mjs';import worker from '../worker.js';
test('authenticated forward gap recovers next packet without accepting tampering or replay',async()=>{
 const db=new LocalDatabase(':memory:');db.sql.exec(readFileSync(new URL('../schema.sql',import.meta.url),'utf8'));
 const user='journaltest',token='journal-token',device='journal-device-012345',time=new Date().toISOString();
 db.sql.prepare('INSERT INTO accounts(user,role,created_at,updated_at) VALUES(?,?,?,?)').run(user,'driver',time,time);
 db.sql.prepare('INSERT INTO profiles(user,updated_at) VALUES(?,?)').run(user,time);
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)').run(createHash('sha256').update(token).digest('hex'),user,user,device,time,time);
 const signed=(seq,prev)=>{
  const r={game:{connected:true},truck:{make:'Volvo',model:'FH'},gat_client_version:'1.0.68.40',gat_packet_id:'journal-test-'+seq, gat_collected_at:new Date(Date.now()-10000+seq*1000).toISOString()};
  const payload=JSON.stringify(r),hash=createHash('sha256').update(payload).digest('hex');
  const chain=createHmac('sha256',createHash('sha256').update(`GAT-JOURNAL-V1|${token}|${device}`).digest()).update(`${r.gat_packet_id}|${r.gat_collected_at}||${seq}|${prev}|${hash}`).digest('hex');
  return {...r,gat_journal_version:'3',gat_journal_payload:payload,gat_journal_payload_sha256:hash,gat_journal_seq:seq,gat_journal_prev:prev,gat_journal_chain:chain};
 };
 async function send(telemetry){const r=await worker.fetch(new Request('https://api.bidulog.com.br/api/client/telemetry',{method:'POST',body:JSON.stringify({driver:user,device_id:device,token,telemetry})}),{DB:db},{});assert.equal(r.status,200);return r.json();}
 try{
  const a=signed(1,'');await send(a);
  const gap=signed(3,'lost-packet-chain');const b=await send(gap);assert.equal(b.rank_status.reason,'local_journal_invalid');
  const st=db.sql.prepare('SELECT * FROM telemetry_journal_state WHERE user=?').get(user);assert.equal(st.last_seq,3);
  const next=signed(4,gap.gat_journal_chain);const c=await send(next);assert.notEqual(c.rank_status.reason,'local_journal_invalid');
  const changed=signed(5,next.gat_journal_chain);changed.truck.make='MAN';await send(changed);
  assert.equal(db.sql.prepare('SELECT last_seq FROM telemetry_journal_state WHERE user=?').get(user).last_seq,4);
  const replay=signed(2,'');await send(replay);assert.equal(db.sql.prepare('SELECT last_seq FROM telemetry_journal_state WHERE user=?').get(user).last_seq,4);
 }finally{db.close();}
});
