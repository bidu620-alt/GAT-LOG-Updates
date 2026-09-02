(()=>{
  const API=(typeof DRIVER_API!=='undefined'&&DRIVER_API)||'https://api.gatlogets2.com.br';
  const FRESH_MS=20000;
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const n=v=>Number(v)||0;
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const pretty=v=>{const s=clean(v).replace(/[._-]+/g,' ');return s.replace(/\b\w/g,c=>c.toUpperCase())||'Motorista'};
  const fmt=v=>n(v).toLocaleString('pt-BR');
  const fmtKm=v=>Math.round(n(v)).toLocaleString('pt-BR')+' km';
  const fmtPct=v=>n(v).toLocaleString('pt-BR',{minimumFractionDigits:0,maximumFractionDigits:1})+'%';

  function injectStyle(){
    if(document.getElementById('gatDriverEnhancementStyle'))return;
    const s=document.createElement('style');
    s.id='gatDriverEnhancementStyle';
    s.textContent=`
.gat-driver-directory{margin:0 0 20px;border:1px solid #1c3047;border-radius:18px;background:linear-gradient(180deg,#0b141e,#080e15);overflow:hidden;box-shadow:0 16px 44px #0004}
.gat-driver-directory-nav{display:flex;align-items:center;gap:8px;padding:10px;border-bottom:1px solid #182a3c;background:#09111a}
.gat-my-work-tab{border:1px solid #4aa8ff;border-radius:11px;background:linear-gradient(135deg,#0877dd,#1557c8);box-shadow:0 6px 24px #0877dd42;color:#fff;padding:12px 18px;font-size:10px;font-weight:950;letter-spacing:.04em;cursor:pointer}
.gat-my-work-tab:hover{filter:brightness(1.1)}
.gat-all-drivers-tab{border:1px solid #263a50;border-radius:11px;background:#101a25;color:#a8bad0;padding:12px 16px;font-size:10px;font-weight:950;letter-spacing:.04em}
.gat-driver-directory-body{padding:16px}
.gat-directory-head{display:flex;align-items:center;justify-content:space-between;gap:14px;margin-bottom:12px}
.gat-directory-head h2{margin:3px 0 0;font-size:21px}
.gat-directory-head small{display:block;color:#6c8197;font-size:9px;font-weight:900}
.gat-directory-count{color:#70b8ff;font-size:10px;font-weight:950;white-space:nowrap}
.gat-driver-search{width:100%;box-sizing:border-box;margin-bottom:12px;border:1px solid #21364c;border-radius:11px;background:#0d1721;color:#eaf3ff;padding:11px 12px;font-size:11px;outline:none}
.gat-driver-search:focus{border-color:#368ee5;box-shadow:0 0 0 3px #1d7ed01d}
.gat-driver-list{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:9px}
.gat-driver-item{display:grid;grid-template-columns:42px minmax(0,1fr) auto;gap:10px;align-items:center;padding:11px;border:1px solid #1b2c3d;border-radius:13px;background:#0c141d;color:#dce8f6;text-decoration:none;transition:.15s ease}
.gat-driver-item:hover{transform:translateY(-1px);border-color:#2d6da6;background:#0e1b28}
.gat-driver-avatar{width:40px;height:40px;border-radius:50%;display:grid;place-items:center;background:linear-gradient(145deg,#1a5080,#102d49);border:1px solid #3d7ebc;color:#e4f2ff;font-weight:950}
.gat-driver-item-main{min-width:0}.gat-driver-item-main b{display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-size:12px}.gat-driver-item-main small{display:block;margin-top:3px;color:#6f849a;font-size:9px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.gat-driver-item-meta{text-align:right}.gat-driver-item-meta b{display:block;color:#7fbeff;font-size:10px}.gat-driver-item-meta small{display:block;margin-top:4px;color:#6d8094;font-size:8px;font-weight:900}
.gat-online-dot,.gat-offline-dot{display:inline-block;width:6px;height:6px;border-radius:50%;margin-right:4px;vertical-align:1px}.gat-online-dot{background:#42d6a0;box-shadow:0 0 8px #42d6a0}.gat-offline-dot{background:#536476}
.gat-directory-status{padding:18px;border:1px dashed #26394d;border-radius:12px;color:#76899f;text-align:center;font-size:10px}
.gat-delivery-row{grid-template-rows:auto auto}.gat-delivery-row .gat-xp-breakdown{grid-column:1/-1;display:flex;flex-wrap:wrap;gap:7px;margin-top:2px;padding-top:10px;border-top:1px dashed #203247}
.gat-xp-chip{display:inline-flex;gap:5px;align-items:center;border:1px solid #25394f;border-radius:9px;background:#0e1924;padding:6px 8px;color:#93a5b9;font-size:8px;font-weight:850}.gat-xp-chip b{color:#dceafb;font-size:9px}.gat-xp-chip.penalty{border-color:#5b2a35;background:#1b1117;color:#d48b9b}.gat-xp-chip.penalty b{color:#ff8ca5}.gat-xp-chip.clean{border-color:#1f5949;background:#0c211b;color:#78dcb8}.gat-xp-chip.final{border-color:#235b8e;background:#0d2034;color:#86c5ff}.gat-xp-chip.final b{color:#a8d6ff}
.gat-delivery-xp{display:block;margin-top:3px;color:#75baff!important;font-size:9px;font-weight:950}
@media(max-width:700px){.gat-driver-directory-nav{overflow:auto}.gat-directory-head{align-items:flex-start;flex-direction:column}.gat-driver-list{grid-template-columns:1fr}.gat-delivery-row .gat-xp-breakdown{min-width:640px}.gat-my-work-tab,.gat-all-drivers-tab{white-space:nowrap}}
`;
    document.head.appendChild(s);
  }

  function sessionNow(){
    try{if(typeof getSession==='function')return getSession()}catch(_){}
    try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}
  }

  function activateWorkTab(){
    const b=document.querySelector('.driver-tabs [data-tab="work"]');
    if(!b)return false;
    b.click();
    setTimeout(()=>b.scrollIntoView({behavior:'smooth',block:'center'}),80);
    return true;
  }

  function goMyWork(){
    const s=sessionNow(),user=clean(s?.user);
    if(!user){
      if(typeof showAccountModal==='function')showAccountModal('login');
      return;
    }
    const u=new URL(location.href),shown=clean(u.searchParams.get('u'));
    if(shown===user&&activateWorkTab()){
      u.searchParams.set('tab','work');
      history.replaceState(null,'',u.pathname+'?'+u.searchParams.toString());
      return;
    }
    location.href='motorista.html?u='+encodeURIComponent(user)+'&tab=work';
  }

  function ensureDirectory(){
    if(document.getElementById('gatDriverDirectory'))return;
    const main=document.querySelector('main.driver-page');
    if(!main)return;
    const back=main.querySelector('.back-link');
    const sec=document.createElement('section');
    sec.id='gatDriverDirectory';
    sec.className='gat-driver-directory';
    sec.innerHTML=`<div class="gat-driver-directory-nav"><button id="gatMyWorkTab" class="gat-my-work-tab" type="button">MEU TRABALHO</button><span class="gat-all-drivers-tab">TODOS OS MOTORISTAS</span></div><div class="gat-driver-directory-body"><div class="gat-directory-head"><div><small>COMUNIDADE GAT-LOG</small><h2>Motoristas</h2></div><span id="gatDriverCount" class="gat-directory-count">CARREGANDO...</span></div><input id="gatDriverSearch" class="gat-driver-search" type="search" placeholder="Buscar motorista..." autocomplete="off"><div id="gatDriverList" class="gat-driver-list"><div class="gat-directory-status">Carregando motoristas da Central GAT...</div></div></div>`;
    if(back)back.insertAdjacentElement('afterend',sec);else main.prepend(sec);
    document.getElementById('gatMyWorkTab')?.addEventListener('click',goMyWork);
    document.getElementById('gatDriverSearch')?.addEventListener('input',e=>filterDirectory(e.target.value));
  }

  let directoryItems=[];
  function filterDirectory(value){
    const term=clean(value);
    document.querySelectorAll('#gatDriverList .gat-driver-item').forEach(el=>{
      const user=clean(el.dataset.user),name=clean(el.dataset.name);
      el.hidden=!!term&&!user.includes(term)&&!name.includes(term);
    });
  }

  function liveUsers(data){
    const out=new Map(),list=Array.isArray(data?.telemetry)?data.telemetry:[];
    for(const t of list){
      const user=clean(t?.account_user||t?.driver);
      if(!user)continue;
      const time=Date.parse(t?.updated_at||'');
      const isFresh=Number.isFinite(time)&&Date.now()-time<=FRESH_MS;
      const prev=out.get(user);
      if(!prev||time>(prev.time||0))out.set(user,{online:isFresh,time:Number.isFinite(time)?time:0});
    }
    return out;
  }

  async function getJson(path,timeout=5000){
    const c=new AbortController(),timer=setTimeout(()=>c.abort(),timeout);
    try{const r=await fetch(API+path,{cache:'no-store',signal:c.signal});const data=await r.json().catch(()=>null);return r.ok?data:null}catch(_){return null}finally{clearTimeout(timer)}
  }

  async function loadDirectory(){
    ensureDirectory();
    const list=document.getElementById('gatDriverList'),count=document.getElementById('gatDriverCount');
    if(!list)return;
    try{
      const [rankingData,liveData]=await Promise.all([getJson('/api/public/ranking'),getJson('/api/public/account-live',4500)]);
      const rank=Array.isArray(rankingData?.ranking)?rankingData.ranking:[];
      const live=liveUsers(liveData),byUser=new Map();
      for(const item of rank){
        const user=clean(item?.user);if(!user)continue;
        byUser.set(user,{...item,user});
      }
      for(const [user] of live){if(!byUser.has(user))byUser.set(user,{user,total_deliveries:0,monthly_km:0});}
      const own=clean(sessionNow()?.user);if(own&&!byUser.has(own))byUser.set(own,{user:own,total_deliveries:0,monthly_km:0});
      directoryItems=[...byUser.values()].sort((a,b)=>pretty(a.user).localeCompare(pretty(b.user),'pt-BR',{sensitivity:'base',numeric:true}));
      count.textContent=directoryItems.length+' MOTORISTA'+(directoryItems.length===1?'':'S');
      if(!directoryItems.length){list.innerHTML='<div class="gat-directory-status">Nenhum motorista registrado na Central GAT ainda.</div>';return;}
      list.textContent='';
      for(const item of directoryItems){
        const user=clean(item.user),name=pretty(user),on=live.get(user)?.online===true,deliveries=n(item.total_deliveries);
        const a=document.createElement('a');
        a.className='gat-driver-item';a.href='motorista.html?u='+encodeURIComponent(user);a.dataset.user=user;a.dataset.name=name;
        a.innerHTML=`<div class="gat-driver-avatar">${esc(name.charAt(0))}</div><div class="gat-driver-item-main"><b>${esc(name)}${own===user?' <span style="color:#69b6ff">• VOCÊ</span>':''}</b><small>@${esc(user)} • <span class="${on?'gat-online-dot':'gat-offline-dot'}"></span>${on?'ONLINE':'OFFLINE'}</small></div><div class="gat-driver-item-meta"><b>${deliveries} entrega${deliveries===1?'':'s'}</b><small>${fmtKm(item.monthly_km||0)}</small></div>`;
        list.appendChild(a);
      }
    }catch(_){
      count.textContent='INDISPONÍVEL';
      list.innerHTML='<div class="gat-directory-status">Não foi possível carregar a lista de motoristas agora.</div>';
    }
  }

  function penaltyValue(x,key,fallback=0){
    const raw=x?.[key];
    return raw===undefined||raw===null?fallback:Math.max(0,n(raw));
  }

  function enhancedRenderDeliveries(history){
    const count=document.getElementById('deliveriesCount'),rows=document.getElementById('deliveryRows');
    if(!rows)return;
    const list=Array.isArray(history)?history:[];
    if(count)count.textContent=list.length+' REGISTRADAS';
    rows.textContent='';
    if(!list.length){rows.innerHTML='<div class="delivery-empty">Nenhuma carga GAT concluída ainda.</div>';return;}
    [...list].reverse().forEach(x=>{
      const distance=Math.max(0,n(x?.distance_km));
      const calculatedBase=Math.floor(distance/100)*20;
      const base=penaltyValue(x,'base_xp',calculatedBase)||calculatedBase;
      const fines=Math.max(0,Math.round(n(x?.speed_fines)));
      const speedPenalty=penaltyValue(x,'speed_penalty_xp',fines*3);
      const cargoPenalty=penaltyValue(x,'cargo_penalty_xp',0);
      const truckPenalty=penaltyValue(x,'truck_penalty_xp',0);
      const totalPenalty=penaltyValue(x,'penalty_xp',speedPenalty+cargoPenalty+truckPenalty);
      const hasSavedFinal=x&&Object.prototype.hasOwnProperty.call(x,'xp_awarded')&&Number.isFinite(Number(x.xp_awarded));
      const finalXp=hasSavedFinal?Math.max(0,Number(x.xp_awarded)):Math.max(0,base-totalPenalty);
      const cargoDamage=Math.max(0,n(x?.cargo_damage_pct));
      const truckDamage=Math.max(0,n(x?.truck_damage_delta_pct));
      const route=(x?.source||'?')+' → '+(x?.destination||'?');
      const row=document.createElement('div');row.className='delivery-row gat-delivery-row';
      row.innerHTML=`<span><b>${esc(route)}</b></span><span>${esc(x?.cargo||'Carga')}</span><span>${typeof kg==='function'?kg(x?.weight_kg):'—'}</span><span>${fmtKm(distance)}</span><span>#${esc(x?.sequence||'—')}</span><span class="done">CONCLUÍDA<b class="gat-delivery-xp">${fmt(finalXp)} XP</b></span>`;
      const detail=document.createElement('div');detail.className='gat-xp-breakdown';
      let chips=`<span class="gat-xp-chip"><span>XP BASE</span><b>${fmt(base)}</b></span>`;
      if(speedPenalty>0||fines>0)chips+=`<span class="gat-xp-chip penalty"><span>VELOCIDADE${fines?' • '+fines+' multa'+(fines===1?'':'s'):''}</span><b>-${fmt(speedPenalty)} XP</b></span>`;
      if(cargoPenalty>0||cargoDamage>0)chips+=`<span class="gat-xp-chip ${cargoPenalty>0?'penalty':''}"><span>CARGA • ${fmtPct(cargoDamage)}</span><b>${cargoPenalty>0?'-'+fmt(cargoPenalty)+' XP':'0 XP'}</b></span>`;
      if(truckPenalty>0||truckDamage>0)chips+=`<span class="gat-xp-chip ${truckPenalty>0?'penalty':''}"><span>CAMINHÃO • +${fmtPct(truckDamage)}</span><b>${truckPenalty>0?'-'+fmt(truckPenalty)+' XP':'0 XP'}</b></span>`;
      if(totalPenalty===0)chips+=`<span class="gat-xp-chip clean"><b>SEM PENALIDADES</b></span>`;
      chips+=`<span class="gat-xp-chip final"><span>XP FINAL</span><b>${fmt(finalXp)}</b></span>`;
      detail.innerHTML=chips;row.appendChild(detail);rows.appendChild(row);
    });
  }

  function installDeliveryRenderer(){
    try{window.GATEnhancedDeliveryRenderer=enhancedRenderDeliveries}catch(_){}
    try{if(typeof profile!=='undefined'&&profile)enhancedRenderDeliveries(profile.deliveries||[])}catch(_){}
  }

  function openRequestedTab(){
    const wanted=new URLSearchParams(location.search).get('tab');
    if(wanted==='work')setTimeout(activateWorkTab,100);
  }

  injectStyle();
  ensureDirectory();
  installDeliveryRenderer();
  openRequestedTab();
  loadDirectory();
  window.addEventListener('gat-account-change',()=>{loadDirectory();});
  setInterval(loadDirectory,30000);
})();
