import test,{after} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {LocalDatabase} from './runtime/database.mjs';
import {repairLapealMowerDelivery} from './runtime/host.mjs';

const folders=[];after(()=>{for(const d of folders)rmSync(d,{recursive:true,force:true});});
const schema=readFileSync(new URL('./runtime/schema.sql',import.meta.url),'utf8');
const migration=readFileSync(new URL('./runtime/migrations/0004_read_efficiency.sql',import.meta.url),'utf8');

test('reparo Lapeal insere a entrega confirmada uma unica vez e preserva a missao atual',()=>{
  const dir=mkdtempSync(join(tmpdir(),'gat-lapeal-repair-'));folders.push(dir);
  const db=new LocalDatabase(join(dir,'central.sqlite'));try{
    db.sql.exec(schema);db.sql.exec(migration);
    const at='2026-09-03T01:23:14.770Z';
    db.sql.prepare("INSERT INTO accounts(user,role,created_at,updated_at) VALUES('lapeal67','driver',?,?)").run(at,at);
    const current=JSON.stringify({id:'current-jcb',cargo:'Retroescavadeira JCB 4CX',state:'active'});
    db.sql.prepare("INSERT INTO profiles(user,monthly_goal,current_mission_json,updated_at) VALUES('lapeal67',30,?,?)").run(current,at);

    assert.equal(repairLapealMowerDelivery(db),true);
    assert.equal(repairLapealMowerDelivery(db),false);

    const rows=db.sql.prepare("SELECT * FROM deliveries WHERE user='lapeal67'").all();
    assert.equal(rows.length,1);const d=rows[0];
    assert.equal(d.cargo,'Mower Conditioner Krone BiG M 450');
    assert.equal(d.source,'Málaga');assert.equal(d.destination,'A Coruña');
    assert.equal(d.weight_kg,15500);assert.equal(d.distance_km,1135);assert.equal(d.xp,220);
    assert.equal(d.perfect,0);assert.equal(d.penalty_xp,0);assert.equal(d.speed_fines,0);
    const raw=JSON.parse(d.raw_json);assert.equal(raw.delivery_details.distanceKm,1135);assert.equal(raw.delivery_details.cargoDamage,0);assert.equal(raw.audit.gat_points,100);assert.equal(raw.audit.repair_verified_receipt,true);

    const p=db.sql.prepare("SELECT monthly_completed,total_deliveries,total_km,xp,points,current_mission_json FROM profiles WHERE user='lapeal67'").get();
    assert.equal(p.monthly_completed,1);assert.equal(p.total_deliveries,1);assert.equal(p.total_km,1135);assert.equal(p.xp,220);assert.equal(p.points,100);assert.equal(p.current_mission_json,current);
    assert.equal(db.sql.prepare("SELECT COUNT(*) n FROM routes_completed WHERE user='lapeal67' AND route_key='malaga>a coruna'").get().n,1);
    assert.ok(db.sql.prepare("SELECT value FROM meta WHERE key='repair_lapeal_mower_2026_09_03_v1'").get());
  }finally{db.close();}
});
