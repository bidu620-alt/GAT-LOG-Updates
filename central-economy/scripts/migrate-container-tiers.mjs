import {DatabaseSync} from 'node:sqlite';
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {pathToFileURL} from 'node:url';

export const CORRECTED_CONTAINER_IDS=Object.freeze(['apples_c','carbn_pwdr_c','carrots_c','chem_sorb_c','clothes_c','exhausts_c','oil_filt_c','pet_food_c','plast_film_c','salt_spice_c','truck_batt_c','truck_rims_c','used_plast_c']);
export function correctNovemberSnapshot(original){
 const season=structuredClone(original);
 if(season.id!=='2026-11'||season.product_id!=='pass_2026_11')return season;
 if(!season.cargo_ids||!Array.isArray(season.cargo_ids.conteineres))throw Error('Unexpected saved pass rules.');
 const corrected=new Set(CORRECTED_CONTAINER_IDS);
 for(const category of Object.keys(season.cargo_ids))season.cargo_ids[category]=season.cargo_ids[category].filter(id=>!corrected.has(id));
 season.cargo_ids.conteineres=[...new Set([...season.cargo_ids.conteineres,...CORRECTED_CONTAINER_IDS])].sort();
 season.classification_revision='2026-11-06.1';
 return season;
}

if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const directory=process.env.BIDULOG_CONTAINER_CENTRAL;
 const file=process.env.BIDULOG_CONTAINER_DB;
 if(!directory||!file)throw Error('Set BIDULOG_CONTAINER_CENTRAL and BIDULOG_CONTAINER_DB.');
 const base=pathToFileURL(directory.replace(/[\\/]?$/,'/'));
 const {classifyCargoId}=await import(new URL('cargo-library/index.js',base));
 for(const id of CORRECTED_CONTAINER_IDS)if(classifyCargoId(id)!=='conteineres')throw Error('Install verified library before reconciling: '+id);
 const apply=process.argv.includes('--apply'),sql=new DatabaseSync(file,{readOnly:!apply});sql.exec('PRAGMA busy_timeout=5000');
 const db={sql},hasTiers=!!sql.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name='pass_tier_seasons'").get();
 const before=hasTiers?sql.prepare("SELECT id,rules_json FROM pass_tier_seasons WHERE id='2026-11'").all():[];
 const earned=hasTiers?sql.prepare('SELECT * FROM pass_tier_completions').all():[];
 const backup={season_snapshots:before,earned_tiers:earned};
 const backupFile=process.env.BIDULOG_CONTAINER_BACKUP;
 if(apply&&!backupFile)throw Error('Backup output required before updating saved rules.');
 if(apply)writeFileSync(backupFile,JSON.stringify(backup,null,2),{flag:'wx'});
 const changes=[];
 if(apply)sql.exec('BEGIN IMMEDIATE');
 try{
  if(hasTiers)for(const row of before){const original=JSON.parse(row.rules_json),corrected=correctNovemberSnapshot(original);if(JSON.stringify(corrected)!==JSON.stringify(original)){changes.push(row.id);if(apply)sql.prepare('UPDATE pass_tier_seasons SET rules_json=? WHERE id=?').run(JSON.stringify(corrected),row.id);}}
  if(apply&&hasTiers){
   if(!existsSync(new URL('pass-tiers.js',base)))throw Error('Installed Tier module missing.');
   const {syncPassTiers}=await import(new URL('pass-tiers.js',base));
   for(const {user}of sql.prepare('SELECT user FROM accounts ORDER BY user').all())syncPassTiers(db,user);
  }
  if(apply)sql.exec('COMMIT');
 }catch(e){if(apply)sql.exec('ROLLBACK');throw e}
 const {monthlyPassState}=await import(new URL('monthly-pass.js',base));
 const users=sql.prepare('SELECT user FROM accounts ORDER BY user').all();
 const report=users.map(({user})=>{const p=monthlyPassState(db,{user});return {user,containers:p.progress.goals.find(g=>g.id==='conteineres').current,completed_goals:p.progress.completed_goals,total_goals:p.progress.total_goals};});
 console.log(JSON.stringify({applied:apply,library_version:'2026-11-06.1',saved_seasons_updated:changes,tier_records_preserved:earned.length,users:report}));
 const reportFile=process.env.BIDULOG_CONTAINER_REPORT;if(reportFile)writeFileSync(reportFile,JSON.stringify({applied:apply,users:report},null,2));sql.close();
}

