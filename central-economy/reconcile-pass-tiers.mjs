import {DatabaseSync} from 'node:sqlite';
import {writeFileSync} from 'node:fs';
import {currentTierSeason,findPassCompletion,readPassTier,syncPassTiers} from './pass-tiers.js';
const apply=process.argv.includes('--apply'),file=process.env.BIDULOG_TIER_DB;
if(!file)throw Error('Set BIDULOG_TIER_DB to the installed central.sqlite path.');
const sql=new DatabaseSync(file,{readOnly:!apply});sql.exec('PRAGMA busy_timeout=5000');
const db={sql},users=sql.prepare('SELECT user FROM accounts ORDER BY user').all(),season=currentTierSeason(),report=[];
if(apply)sql.exec('BEGIN IMMEDIATE');
try{
 for(const {user} of users){
  const existing=readPassTier(db,user),candidate=findPassCompletion(db,user,season);
  const tier=apply?syncPassTiers(db,user):existing;
  report.push({user,current_tier:existing.tier,eligible_current_pass:!!candidate,completion:candidate?.completed_at||null,resulting_tier:tier.tier});
 }
 if(apply)sql.exec('COMMIT');
}catch(e){if(apply)sql.exec('ROLLBACK');throw e}finally{sql.close()}
const result={applied:apply,season:season.id,users:report};
const output=process.env.BIDULOG_TIER_REPORT;if(output)writeFileSync(output,JSON.stringify(result,null,2));
console.log(JSON.stringify(result));
