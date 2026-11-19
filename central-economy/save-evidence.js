// Server-side receipt of save evidence. It never grants points or marks a job
// delivered merely because a job pointer disappeared from an autosave.
export function inspectSaveEvidence(raw,verified,at){
 const e=raw?.gat_save_evidence;
 if(!e||e.source!=='ets2_save_readonly'||e.schema!==1)return {status:'not_sent'};
 if(!verified)return {status:'rejected',reason:'journal_not_verified'};
 if(e.status==='unavailable')return {status:'unavailable',reason:String(e.reason||'reader_unavailable').slice(0,120)};
 const profile=String(e.profile_id||'').toUpperCase(),active=String(raw.gat_profile_id||'').toUpperCase();
 if(!profile||profile!==active)return {status:'rejected',reason:'profile_mismatch'};
 const hash=e.save?.game_sha256||e.game_sha256;
 if(!/^[a-f0-9]{64}$/.test(hash||''))return {status:'rejected',reason:'invalid_save_hash'};
 const modified=e.save?.game_modified_at||e.game_modified_at;
 const age=(Date.parse(at)-Date.parse(modified))/1000;
 if(!Number.isFinite(age)||age< -30)return {status:'rejected',reason:'invalid_save_time'};
 const jobs=e.jobs||{};
 const mode=e.vehicle_mode;
 const kind=mode==='bus'?(jobs.current_bus_job?'bus_job':null):mode==='car'?(jobs.current_car_job?'car_job':null):mode==='truck'?(jobs.current_job?'job':null):
  Object.values(jobs).filter(Boolean).length===1?(jobs.current_bus_job?'bus_job':jobs.current_car_job?'car_job':'job'):null;
 return {status:age>120?'stored_stale':e.full_snapshot===true?'stored_pending_rules':'snapshot_reference',
  reason:age>120?'autosave_older_than_120_seconds':kind==='bus_job'?'passenger_service_requires_completion_evidence':kind==='car_job'?'car_job_is_not_necessarily_passengers':'save_is_supporting_evidence',
  profile_id:profile,save_hash:hash,job_kind:e.full_snapshot===true?kind:e.job_kind||null,age_seconds:Math.round(age),
  eligible_for_automatic_credit:false};
}
export async function receiveSaveEvidence(db,user,raw,verified,at){
 const result=inspectSaveEvidence(raw,verified,at),e=raw?.gat_save_evidence;
 if(!result.status.startsWith('stored'))return result;
 if(e.full_snapshot!==true)return result;
 const json=JSON.stringify(e);
 if(json.length>220000)return {status:'rejected',reason:'save_evidence_too_large'};
 await db.prepare(`CREATE TABLE IF NOT EXISTS save_evidence_snapshots (
  user TEXT NOT NULL,profile_id TEXT NOT NULL,save_hash TEXT NOT NULL,
  first_received_at TEXT NOT NULL,last_received_at TEXT NOT NULL,job_kind TEXT,
  status TEXT NOT NULL,reason TEXT,payload_json TEXT NOT NULL,
  PRIMARY KEY(user,profile_id,save_hash))`).run();
 await db.prepare(`INSERT INTO save_evidence_snapshots VALUES(?,?,?,?,?,?,?,?,?)
  ON CONFLICT(user,profile_id,save_hash) DO UPDATE SET last_received_at=excluded.last_received_at,
  status=excluded.status,reason=excluded.reason`).bind(user,result.profile_id,result.save_hash,at,at,result.job_kind,result.status,result.reason,json).run();
 await db.prepare(`DELETE FROM save_evidence_snapshots WHERE user=? AND rowid NOT IN
  (SELECT rowid FROM save_evidence_snapshots WHERE user=? ORDER BY last_received_at DESC LIMIT 100)`).bind(user,user).run();
 return result;
}
