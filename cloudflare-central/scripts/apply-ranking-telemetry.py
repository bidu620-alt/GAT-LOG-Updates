from pathlib import Path
import re

p = Path('worker.js')
s = p.read_text(encoding='utf-8')
if 'advanceRankGuard' in s:
    raise SystemExit('ranking patch already applied')

def replace(old, new):
    global s
    if s.count(old) != 1:
        raise SystemExit('Expected one ranking anchor: ' + old[:100])
    s = s.replace(old, new, 1)

s = "import {rankingReadiness, rankingMessage, advanceRankGuard, restoreDeliveredTrailer} from './ranking-telemetry.js';\n" + s
replace('async function processMission(env,user,raw,t){', 'async function processMission(env,user,raw,t,previousAt){')
replace("trip_progress_confirmed:false,started_at:m.started_at||t};", "trip_progress_confirmed:false,rank_guard:{reason:rankingReadiness(raw).reason},started_at:t};")
marker = " const truckNow=truckDamageOf(raw),truckPartsNow=truckDamageParts(raw),trailerNow=trailerDamageOf(raw);"
replace(marker, """ if(m.state==='active'||m.state==='suspended'){
   const readiness=rankingReadiness(raw),next=advanceRankGuard(m.rank_guard,readiness,m.started_at===t?t:previousAt,t,Boolean(m.trip_progress_confirmed));
   if(JSON.stringify(next)!==JSON.stringify(m.rank_guard)){
     m.rank_guard=next;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }
 }
""" + marker)
marker = " if(m.state!=='active')return{type:'delivery_rejected',reason:'mission_not_active'};"
replace(marker, """ if(m.rank_guard?.reason||!m.rank_guard){
   const reason=m.rank_guard?.reason||'telemetry_not_verified_from_start';
   await resetAssigned(env,user,m,reason);
   return{type:'delivery_rejected',reason,rank_eligible:false,gat_points:0,xp:0,message:rankingMessage(reason)};
 }
""" + marker)
# Remove attempt-specific measurements on cancellation/rejection so another job has
# fresh baselines. Keep the selected work and the visible rejection reason.
replace("for(const k of['cargo','source','destination','weight_kg','planned_distance_km','rbr_start_remaining_km','map_mode','distance_source','started_at'])delete m[k];", """for(const k of['cargo','cargo_id','source','source_city_id','destination','destination_city_id','weight_kg','planned_distance_km','rbr_start_remaining_km','map_mode','distance_source','started_at','suspended_at','resumed_at','job_latch_key','start_remaining_km','start_odometer_km','trip_progress_confirmed','rank_guard'])delete m[k];for(const k of Object.keys(m))if(/^(truck_|trailer_).*damage_(start|max)_pct$/.test(k))delete m[k];""")
# Explain current eligibility in the public live response and preserve the actual
# trip rejection in the mission returned to the driver.
replace("return{driver,account_user:account||'',updated_at:updated,telemetry:raw,", "return{driver,account_user:account||'',updated_at:updated,rank_status:rankingReadiness(raw),telemetry:raw,")
replace('perfect_trip:!!perfect,xp_awarded:xp,gat_base_points:100', 'rank_verified:true,rank_client_version:raw.gat_client_version,perfect_trip:!!perfect,xp_awarded:xp,gat_base_points:100')
s = re.sub(r"const VERSION='[0-9.]+-cloudflare';", "const VERSION='1.0.52-cloudflare';", s, count=1)
p.write_text(s, encoding='utf-8')
print('Ranking requires supported telemetry and preserves server-verified trips across central updates.')
