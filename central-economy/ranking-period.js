export async function rankingPeriod(db, params, currentMonth) {
 const scope=params.get('scope')||'month';
 const period=scope==='total'?'total':params.get('period')||(scope==='year'?currentMonth.slice(0,4):currentMonth);
 if(!['month','year','total'].includes(scope)||(scope==='month'&&!/^\d{4}-(0[1-9]|1[0-2])$/.test(period))||(scope==='year'&&!/^\d{4}$/.test(period))) return {ok:false,error:'invalid_period'};
 const dateFilter=column=>scope==='total'?'1=1':`strftime('${scope==='year'?'%Y':'%Y-%m'}',${column},'-3 hours')=?`;
 const bind=scope==='total'?[]:[period];
 const result=await db.prepare(`SELECT p.user,p.xp AS career_xp,p.total_km,
   COALESCE(d.completed,0) AS monthly_completed,COALESCE(d.km,0) AS monthly_km,
   COALESCE(d.perfect,0) AS perfect_trips,COALESCE(d.penalties,0) AS penalty_xp,COALESCE(d.fines,0) AS speed_fines,
   COALESCE(d.points,0) AS points,
   CASE WHEN ?='total' THEN p.xp ELSE COALESCE(d.xp,0)+COALESCE(c.reward_xp,0) END AS xp,
   COALESCE(c.achievements,0) AS achievements
   FROM profiles p JOIN accounts a ON a.user=p.user
   LEFT JOIN (SELECT user,COUNT(*) completed,SUM(distance_km) km,SUM(perfect) perfect,SUM(penalty_xp) penalties,SUM(speed_fines) fines,SUM(xp) xp,
   SUM(COALESCE(CAST(json_extract(raw_json,'$.audit.gat_points') AS INTEGER),MAX(0,100-penalty_xp))) points
   FROM deliveries WHERE ${dateFilter('delivered_at')} GROUP BY user) d ON d.user=p.user
   LEFT JOIN (SELECT substr(key,13,instr(substr(key,13),':')-1) user,COUNT(*) achievements,
   SUM(COALESCE(json_extract(value,'$.xp'),0)) reward_xp FROM meta
   WHERE key LIKE 'achievement:%' AND json_valid(value) AND ${dateFilter("json_extract(value,'$.unlocked_at')")} GROUP BY user) c ON c.user=p.user
   WHERE a.disabled=0 ORDER BY points DESC,monthly_completed DESC,monthly_km DESC,perfect_trips DESC,penalty_xp ASC,speed_fines ASC,p.user ASC`).bind(scope,...bind,...bind).all();
 const dates=await db.prepare("SELECT DISTINCT strftime('%Y-%m',delivered_at,'-3 hours') month_key FROM deliveries WHERE delivered_at IS NOT NULL ORDER BY month_key DESC").all();
 const available=[...new Set([currentMonth,...(dates.results||[]).map(x=>x.month_key).filter(Boolean)])].sort().reverse();
 return {ok:true,scope,period,season:period,current_month:currentMonth,available_months:available,available_years:[...new Set(available.map(x=>x.slice(0,4)))],ranking:result.results||[]};
}
