(()=>{
  'use strict';
  if(typeof applyProfile!=='function')return;
  const baseApplyProfile=applyProfile;
  let regressiveSince=0,lastSignature='';
  const n=v=>Number(v)||0;
  const month=v=>{const d=new Date(v);return Number.isFinite(d.getTime())?d.toISOString().slice(0,7):''};

  function isRegressive(next){
    if(typeof profile==='undefined'||!profile||!next)return false;
    const current=profile;
    if(n(next.total_deliveries)<n(current.total_deliveries))return true;
    if(n(next.total_km)+0.5<n(current.total_km))return true;
    if(n(next.xp)<n(current.xp))return true;
    const saved=typeof driverStore!=='undefined'&&driverStore?.savedAt?driverStore.savedAt(key):Date.now();
    if(month(saved)===month(Date.now())&&n(next.monthly_completed)<n(current.monthly_completed))return true;
    const a=Array.isArray(next.deliveries)?next.deliveries.length:0,b=Array.isArray(current.deliveries)?current.deliveries.length:0;
    if(a<b&&n(next.total_deliveries)<=n(current.total_deliveries))return true;
    return false;
  }

  applyProfile=function(next,options={}){
    const source=String(options?.source||'server');
    if(source==='server'&&isRegressive(next)){
      const sig=[next?.user,next?.monthly_completed,next?.total_deliveries,next?.total_km,next?.xp,Array.isArray(next?.deliveries)?next.deliveries.length:0].join('|');
      if(sig!==lastSignature){lastSignature=sig;regressiveSince=Date.now()}
      const age=Date.now()-regressiveSince;
      if(age<45000){
        try{setSyncStatus('A Central respondeu com uma versão anterior do perfil. Mantendo os últimos dados confirmados e atualizando novamente.')}catch(_){}
        setTimeout(()=>{try{refreshProfileQuiet(false)}catch(_){}},2500);
        return false;
      }
    }else{regressiveSince=0;lastSignature=''}
    return baseApplyProfile(next,options);
  };
})();