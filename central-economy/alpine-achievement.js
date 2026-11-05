const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
export const ALPINE_ACHIEVEMENT={id:'travessia_alpes',title:'Travessia dos Alpes',difficulty:'Difícil',xp:1000};
export function matchesAlpine(d){
  const c=norm(d?.cargo),s=norm(d?.source),t=norm(d?.destination),w=Number(d?.weight_kg)||0;
  return (c.includes('locomotiva vossloh g6')||c.includes('locomotive vossloh g6'))&&s.includes('innsbruck')&&t.includes('klagenfurt')&&w>=60000&&w<=62000;
}
export async function awardAlpineAchievement(env,user,t){
  const key=`achievement:${user}:${ALPINE_ACHIEVEMENT.id}`;
  if(await env.DB.prepare('SELECT 1 FROM meta WHERE key=?').bind(key).first())return {xp:0,unlocked:[]};
  const rows=await env.DB.prepare(`SELECT id,cargo,source,destination,weight_kg,delivered_at FROM deliveries
    WHERE user=? AND weight_kg BETWEEN 60000 AND 62000
    AND lower(cargo) LIKE '%vossloh%g6%' AND lower(source) LIKE '%innsbruck%'
    AND lower(destination) LIKE '%klagenfurt%' ORDER BY delivered_at,id`).bind(user).all();
  const delivery=(rows.results||[]).find(matchesAlpine);
  if(!delivery)return {xp:0,unlocked:[]};
  const reward={...ALPINE_ACHIEVEMENT,unlocked:true,unlocked_at:delivery.delivered_at,delivery_id:delivery.id};
  // Both statements commit together. The existing unlock gates XP even on retries.
  const results=await env.DB.batch([
    env.DB.prepare('UPDATE profiles SET xp=xp+?,updated_at=? WHERE user=? AND NOT EXISTS(SELECT 1 FROM meta WHERE key=?)').bind(reward.xp,t,user,key),
    env.DB.prepare('INSERT OR IGNORE INTO meta(key,value) VALUES(?,?)').bind(key,JSON.stringify(reward))
  ]);
  return results[1]?.meta?.changes?{xp:reward.xp,unlocked:[reward]}:{xp:0,unlocked:[]};
}