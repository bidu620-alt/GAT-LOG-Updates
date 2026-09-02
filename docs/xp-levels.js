(()=>{
  const XP_PER_100KM=20,XP_PER_LEVEL=2000;
  const fmt=n=>(Number(n)||0).toLocaleString('pt-BR');
  const historyXp=list=>(Array.isArray(list)?list:[]).reduce((sum,d)=>{
    const hasSaved=d&&Object.prototype.hasOwnProperty.call(d,'xp_awarded')&&Number.isFinite(Number(d.xp_awarded));
    if(hasSaved)return sum+Math.max(0,Number(d.xp_awarded));
    const km=Math.max(0,Number(d?.distance_km)||0);
    return sum+Math.floor(km/100)*XP_PER_100KM;
  },0);
  function applyXp(){try{if(typeof profile==='undefined'||!profile)return;const serverXp=Number(profile.xp),derived=historyXp(profile.deliveries),xp=Number.isFinite(serverXp)?Math.max(0,serverXp):derived,level=1+Math.floor(xp/XP_PER_LEVEL),inside=xp%XP_PER_LEVEL,pct=Math.min(100,inside/XP_PER_LEVEL*100);const set=(id,text)=>{const e=document.getElementById(id);if(e)e.textContent=text};set('driverLevel','★ Nível '+level);set('driverXp','↗ '+fmt(xp)+' XP');set('statLevel',String(level));set('statXp',fmt(xp));set('xpLevelNow','Nível '+level);set('xpProgress',fmt(inside)+' / 2.000 XP');const bar=document.getElementById('xpBar');if(bar)bar.style.width=pct.toFixed(1)+'%'}catch(_){}}
  function loadScript(id,src){if(document.getElementById(id))return;const s=document.createElement('script');s.id=id;s.src=src;s.async=false;document.body.appendChild(s)}
  function loadEnhancements(){loadScript('gatMotoristaEnhancements','motorista-enhancements.js?v=4');loadScript('gatMotoristaNavFix','motorista-nav-fix.js?v=2');loadScript('gatMotoristaInfractions','motorista-infractions.js?v=3');loadScript('gatDriverGamification','driver-gamification.js?v=2');loadScript('gatSecretConquest','secret-conquest.js?v=1');loadScript('gatOfficialWorkRules','official-work-rules.js?v=4');loadScript('gatRegisteredCargoLite','work-cargo-registered-lite.js?v=2');loadScript('gatDeliveryRefreshLite','delivery-refresh-lite.js?v=1')}
  window.addEventListener('gat-driver-profile-updated',applyXp);
  document.addEventListener('DOMContentLoaded',()=>{applyXp();loadEnhancements()},{once:true});
  if(document.readyState!=='loading'){applyXp();loadEnhancements()}
})();