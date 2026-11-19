import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {LocalDatabase} from '../database.mjs';
import worker from '../worker.js';

const schema=readFileSync(new URL('../schema.sql',import.meta.url),'utf8');
function setup(){
 const db=new LocalDatabase(':memory:');
 db.sql.exec(schema);
 const now=new Date().toISOString(),token='bus-job-token',user='testcoach',device='coach-client-1234';
 db.sql.prepare('INSERT INTO accounts(user,role,created_at,updated_at) VALUES(?,?,?,?)').run(user,'driver',now,now);
 db.sql.prepare('INSERT INTO profiles(user,current_mission_json,updated_at) VALUES(?,?,?)').run(user,null,now);
 db.sql.prepare('INSERT INTO client_tokens(token_hash,driver,account_user,device_id,created_at,last_seen_at) VALUES(?,?,?,?,?,?)')
  .run(createHash('sha256').update(token).digest('hex'),user,user,device,now,now);
 let step=0;
 async function send(telemetry){
  const r=await worker.fetch(new Request('https://api.bidulog.com.br/api/client/telemetry',{
   method:'POST',headers:{'Content-Type':'text/plain'},body:JSON.stringify({driver:user,device_id:device,token,telemetry})
  }),{DB:db},{});
  const data=await r.json();
  assert.equal(r.status,200,JSON.stringify(data));
  return data;
 }
 const bus=(state,odometer,details)=>{
  step++;
  return {
   game:{connected:true,paused:false},
   truck:{id:'volvo',make:'Volvo',model:'9700',odometer},
   bus_job:{active:state==='active',sourceCity:'Aracaju',destinationCity:'Maceió',plannedDistanceKm:210},
   gameplay:{onJob:state==='active',jobCancelled:false,...(details?{busJobDeliveredDetails:details}:{})},
   ...(state==='delivered'?{gat_scs_event:'bus_job.delivered'}:{}),
   gat_client_version:'1.0.68.39',
   gat_collected_at:new Date(Date.now()-10000+step*1000).toISOString(),
   gat_packet_id:'bus-test-'+step.toString().padStart(20,'0')
  };
 };
 return {db,user,send,bus};
}
test('authenticated Central: start, track and complete bus_job once without freight weight',async()=>{
 const {db,user,send,bus}=setup();
 try{
  const a=await send(bus('active',12000));
  assert.equal(a.mission_event?.type,'mission_in_progress',JSON.stringify(a));
  const started=JSON.parse(db.sql.prepare('SELECT current_mission_json FROM profiles WHERE user=?').get(user).current_mission_json);
  assert.equal(started.job_kind,'bus_job');
  assert.equal(started.category,'Transporte de passageiros');
  assert.equal(started.weight_kg,0);
  assert.equal(started.cargo,'Passageiros');
  const b=await send(bus('active',12010));
  assert.equal(b.mission_event?.type,'mission_in_progress',JSON.stringify(b));
  const c=await send(bus('delivered',12200,{distanceKm:200,revenue:10000,earnedXp:300}));
  assert.equal(c.mission_event?.type,'delivery_completed',JSON.stringify(c));
  const r=db.sql.prepare('SELECT cargo,weight_kg,raw_json FROM deliveries WHERE user=?').all(user);
  assert.equal(r.length,1);
  assert.equal(r[0].cargo,'Passageiros');
  assert.equal(r[0].weight_kg,0);
  const raw=JSON.parse(r[0].raw_json);
  assert.equal(raw.mission.job_kind,'bus_job');
  assert.equal(raw.audit.job_kind,'bus_job');
  assert.equal(raw.mission.start_vehicle_evidence.model,'9700');
  assert.equal(raw.audit.vehicle_evidence.make,'Volvo');
  assert.equal(raw.audit.transport_evidence.classification,'passenger_official');
  assert.equal(raw.audit.transport_evidence.configuration.plannedDistanceKm,210);
  const duplicate=await send(bus('delivered',12200,{distanceKm:200,revenue:10000,earnedXp:300}));
  assert.notEqual(duplicate.mission_event?.type,'delivery_completed',JSON.stringify(duplicate));
  assert.equal(db.sql.prepare('SELECT COUNT(*) AS n FROM deliveries WHERE user=?').get(user).n,1);
 } finally {db.close()}
});
test('bus_job delivered flag without a receipt never generates an invented delivery',async()=>{
 const {db,user,send,bus}=setup();
 try{
  await send(bus('active',1000));
  await send(bus('active',1020));
  const response=await send(bus('delivered',1090));
  assert.notEqual(response.mission_event?.type,'delivery_completed',JSON.stringify(response));
  assert.equal(db.sql.prepare('SELECT COUNT(*) AS n FROM deliveries WHERE user=?').get(user).n,0);
 }finally{db.close()}
});
test('unverified bus_job without a route does not create a mission',async()=>{
 const {db,user,send}=setup();
 try{
  const response=await send({game:{connected:true},bus_job:{active:true},gat_client_version:'1.0.68.39'});
  assert.notEqual(response.mission_event?.type,'delivery_completed');
  assert.equal(db.sql.prepare('SELECT current_mission_json FROM profiles WHERE user=?').get(user).current_mission_json,null);
 }finally{db.close()}
});
