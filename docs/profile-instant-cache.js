(()=>{
  const PREFIX='gat_driver_profile_cache_v1:';
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const num=v=>Number(v)||0;
  const el=id=>document.getElementById(id);
  const kg=v=>num(v)>0?(num(v)/1000).toLocaleString('pt-BR',{maximumFractionDigits:1})+' t':'—';
  const km=v=>Math.round(num(v)).toLocaleString('pt-BR')+' km';

  function sessionUser(){
    try{
      const raw=localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1');
      return clean(JSON.parse(raw||'null')?.user);
    }catch{return ''}
  }
  const queryUser=clean(new URLSearchParams(location.search).get('u'));
  const currentUser=queryUser||sessionUser();
  const cacheKey=user=>PREFIX+clean(user);

  function saveProfile(p){
    const user=clean(p?.user||currentUser);
    if(!user||!p||typeof p!=='object')return;
    try{localStorage.setItem(cacheKey(user),JSON.stringify({saved_at:Date.now(),profile:p}))}catch{}
  }
  function readProfile(){
    if(!currentUser)return null;
    try{
      const data=JSON.parse(localStorage.getItem(cacheKey(currentUser))||'null');
      return data?.profile&&clean(data.profile.user||currentUser)===currentUser?data.profile:null;
    }catch{return null}
  }
  function text(id,value){const n=el(id);if(n)n.textContent=value}
  function width(id,value){const n=el(id);if(n)n.style.width=value}

  function renderDeliveries(history){
    if(!Array.isArray(history))return;
    text('deliveriesCount',history.length+' REGISTRADAS');
    const rows=el('deliveryRows');if(!rows)return;
    rows.textContent='';
    if(!history.length){const empty=document.createElement('div');empty.className='delivery-empty';empty.textContent='Nenhuma carga GAT concluída ainda.';rows.appendChild(empty);return;}
    [...history].reverse().forEach(x=>{
      const row=document.createElement('div');row.className='delivery-row';
      const values=[(x.source||'?')+' → '+(x.destination||'?'),x.cargo||'Carga',kg(x.weight_kg),km(x.distance_km),'#'+(x.sequence||'—'),'CONCLUÍDA'];
      values.forEach((value,i)=>{const s=document.createElement('span');if(i===0){const b=document.createElement('b');b.textContent=value;s.appendChild(b)}else{s.textContent=value}if(i===5)s.className='done';row.appendChild(s)});
      rows.appendChild(row);
    });
  }
  function renderCached(p){
    if(!p)return;
    const monthly=num(p.monthly_completed),goal=num(p.monthly_goal)||30,pct=Math.min(100,Math.round(monthly/goal*100));
    text('driverMonthly',monthly+' / '+goal);width('monthlyBar',pct+'%');text('driverMonthlyText',pct+'% concluído');
    text('progressMonthlyBig',monthly+' / '+goal);width('progressMonthlyBar',pct+'%');
    text('statDeliveries',num(p.total_deliveries));text('statKm',km(p.total_km));text('statPoints',num(p.points).toLocaleString('pt-BR'));
    text('statLevel',num(p.level)||1);text('statXp',num(p.xp).toLocaleString('pt-BR'));
    text('driverLevel','★ Nível '+(num(p.level)||1));text('driverXp','↗ '+num(p.xp).toLocaleString('pt-BR')+' XP');
    text('xpLevelNow','Nível '+(num(p.level)||1));text('xpProgress',num(p.xp).toLocaleString('pt-BR')+' XP');
    renderDeliveries(p.deliveries||[]);
    document.documentElement.dataset.gatProfileCached='1';
  }

  // Mostra o ultimo perfil confirmado antes de iniciar a consulta nova.
  renderCached(readProfile());

  // Guarda silenciosamente toda resposta de perfil valida recebida pelo site.
  const nativeFetch=window.fetch.bind(window);
  window.fetch=async(...args)=>{
    const response=await nativeFetch(...args);
    try{
      const input=args[0],url=typeof input==='string'?input:String(input?.url||'');
      if(url.includes('/api/site/profile')||url.includes('/api/public/driver?user=')){
        response.clone().json().then(data=>{
          if(data?.ok&&data?.profile){saveProfile(data.profile);renderCached(data.profile)}
        }).catch(()=>{});
      }
    }catch{}
    return response;
  };
})();
