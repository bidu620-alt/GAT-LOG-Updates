import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {LocalDatabase} from '../database.mjs';
import {economyAction,ensureEconomy,state} from '../economy-official.js';
import {companyAction,companyPublic,ensureCompanies} from '../company-community.js';
import {EMOJI_PACK,hasEmojiPack} from '../emoji-access.js';
const db=new LocalDatabase(':memory:');
db.sql.exec(`CREATE TABLE accounts(user TEXT PRIMARY KEY,role TEXT,disabled INTEGER DEFAULT 0);CREATE TABLE profiles(user TEXT PRIMARY KEY,avatar_url TEXT,current_mission_json TEXT);CREATE TABLE audit(at TEXT,actor TEXT,action TEXT,target TEXT,details TEXT);CREATE TABLE telemetry_live(account_user TEXT,updated_at TEXT,telemetry_json TEXT);CREATE TABLE deliveries(id INTEGER PRIMARY KEY,user TEXT,delivered_at TEXT,raw_json TEXT,distance_km REAL,penalty_xp INTEGER DEFAULT 0,perfect INTEGER DEFAULT 0,speed_fines INTEGER DEFAULT 0);`);
for(const [u,role] of [['biduzao','owner'],['owner','driver'],['alice','driver'],['bob','driver'],['charlie','driver'],['admin','admin']]){db.sql.prepare('INSERT INTO accounts VALUES(?,?,0)').run(u,role);db.sql.prepare('INSERT INTO profiles(user) VALUES(?)').run(u)}
const session=user=>db.sql.prepare('SELECT user,role FROM accounts WHERE user=?').get(user),act=(user,path,b={})=>economyAction(db,session(user),path,{...b,op_id:b.op_id||randomUUID()}),co=(user,path,b={})=>companyAction(db,session(user),path,{...b,op_id:randomUUID()});
ensureEconomy(db);ensureCompanies(db);

const vectors=[['Scania','R','scania','','scania'],['Mercedes-Benz','Actros','mercedes.actros','','mercedes'],['Volvo','FH6','volvo','','volvo'],['Marcopolo','G8 1800 DD','marcopolo.g8','','onibus'],['Scania','Marcopolo G7','scania.k360','','onibus'],['Mercedes-Benz','Paradiso 1200','mb.o500','','onibus'],['Busscar','Vissta Buss','busscar','','onibus'],['Volvo','B12R','volvo.b12r','','onibus'],['Scania','K 360','scania','','onibus'],['Custom','Desconhecido','x','coach','onibus']];
const {evidence}=await import('../economy-official.js');for(const [make,model,id,type,expected] of vectors)assert.equal(evidence({game:{connected:true},truck:{make,model,id,type}}).brand,expected);
const catalog=state(db,session('alice')).catalog,bus=catalog.find(p=>p.id==='brand:onibus');assert.equal(bus.cash_centavos,1000);assert.equal(bus.gold_units,2000000);assert.equal(state(db,session('biduzao')).garage.length,8);
db.sql.prepare("UPDATE eco_wallets SET gold_units=2000000 WHERE user='alice'").run();const order=act('alice','purchase',{product_id:'brand:onibus',currency:'gold',terms_accepted:true});assert.equal(state(db,session('alice')).gold_balance,0);assert.deepEqual(state(db,session('alice')).garage.map(g=>g.brand_key),['onibus']);assert.throws(()=>act('alice','purchase',{product_id:'brand:onibus',currency:'gold',terms_accepted:true}),/product_already_owned/);
act('biduzao','admin/supplier',{name:'Fornecedor de teste',document:'12345678901234',address:'Endereco de teste 123',enable_pix:true});const pix=act('bob','purchase',{product_id:'brand:onibus',currency:'pix',terms_accepted:true});assert.equal(pix.amount_centavos,1000);act('biduzao','admin/confirm-payment',{order_id:pix.order_id,manual_without_id:true,bank_statement_checked:true,received_reais:10,manual_note:'Confirmacao manual para o teste do onibus.'});assert.equal(state(db,session('bob')).garage[0].brand_key,'onibus');
console.log('PASS bus: 10 telemetry classifications, R$10/20000 Gold, owner access, purchases, Pix fulfilment and no duplicate key.');db.close();
