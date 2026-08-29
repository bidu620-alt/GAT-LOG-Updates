(()=>{
  const XP_PER_MISSION=100;
  const XP_PER_LEVEL=2000;
  const fmt=n=>(Number(n)||0).toLocaleString('pt-BR');
  function applyXp(){
    try{
      if(typeof profile==='undefined'||!profile)return;
      const deliveries=Math.max(0,Number(profile.total_deliveries)||0);
      const derivedXp=deliveries*XP_PER_MISSION;
      const serverXp=Number(profile.xp);
      const xp=(profile.xp_rule_pending===false&&Number.isFinite(serverXp))?serverXp:derivedXp;
      const level=1+Math.floor(Math.max(0,xp)/XP_PER_LEVEL);
      const inside=((Math.max(0,xp)%XP_PER_LEVEL)+XP_PER_LEVEL)%XP_PER_LEVEL;
      const pct=Math.min(100,(inside/XP_PER_LEVEL)*100);
      profile.xp=xp;
      profile.level=level;
      profile.xp_rule_pending=false;
      const set=(id,text)=>{const e=document.getElementById(id);if(e)e.textContent=text};
      set('driverLevel','★ Nível '+level);
      set('driverXp','↗ '+fmt(xp)+' XP');
      set('statLevel',String(level));
      set('statXp',fmt(xp));
      set('xpLevelNow','Nível '+level);
      set('xpProgress',fmt(inside)+' / 2.000 XP');
      const bar=document.getElementById('xpBar');if(bar)bar.style.width=pct.toFixed(1)+'%';
    }catch(_){ }
  }
  document.addEventListener('DOMContentLoaded',applyXp);
  setInterval(applyXp,900);
})();
