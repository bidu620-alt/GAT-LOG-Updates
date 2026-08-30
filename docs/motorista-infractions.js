(()=>{
  const n=v=>Number(v)||0;
  const fmt=v=>n(v).toLocaleString('pt-BR');
  const pct=v=>n(v).toLocaleString('pt-BR',{maximumFractionDigits:1})+'%';
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));

  function injectStyle(){
    if(document.getElementById('gatInfractionsStyle'))return;
    const s=document.createElement('style');s.id='gatInfractionsStyle';s.textContent=`
.gat-infraction-grid{display:grid;grid-template-columns:repeat(6,1fr);gap:9px;margin-bottom:14px}.gat-infraction-grid article{background:#0b131c;border:1px solid #1b2d40;border-radius:15px;padding:15px}.gat-infraction-grid small{display:block;color:#71849a;font-size:8px;font-weight:950}.gat-infraction-grid b{display:block;margin-top:7px;font-size:20px;color:#e8f2ff}.gat-infraction-grid article.bad b{color:#ff91a7}.gat-infraction-grid article.good b{color:#62deb4}.gat-infraction-grid article.blue b{color:#74bcff}.gat-infraction-list{display:grid;gap:8px;margin-top:14px}.gat-infraction-row{display:grid;grid-template-columns:1.3fr .8fr .8fr .8fr .8fr;gap:9px;align-items:center;border:1px solid #1a2b3d;border-radius:13px;background:#0c141d;padding:12px}.gat-infraction-row strong{font-size:11px}.gat-infraction-row small{display:block;color:#708399;font-size:8px;margin-top:3px}.gat-infraction-chip{border:1px solid #304154;border-radius:8px;padding:6px 8px;font-size:8px;font-weight:900;color:#a9bbce;background:#0e1822}.gat-infraction-chip.bad{border-color:#5b2a35;background:#1b1117;color:#ff91a7}.gat-infraction-chip.good{border-color:#1f5949;background:#0c211b;color:#73dcb7}.gat-infraction-empty{padding:28px;text-align:center;border:1px dashed #294157;border-radius:14px;color:#7c91a8;background:#0b141d}.gat-infraction-note{margin-top:12px;color:#65798f;font-size:9px;line-height:1.55}@media(max-width:1100px){.gat-infraction-grid{grid-template-columns:repeat(3,1fr)}}@media(max-width:760px){.gat-infraction-grid{grid-template-columns:1fr 1fr}.gat-infraction-row{grid-template-columns:1fr 1fr}.gat-infraction-row>div:first-child{grid-column:1/-1}}`;
    document.head.appendChild(s);
  }

  function ensurePanel(){
    const tabs=document.querySelector('.driver-tabs');if(!tabs)return null;
    let btn=tabs.querySelector('[data-tab="infractions"]');
    if(!btn){
      btn=document.createElement('button');btn.type='button';btn.dataset.tab='infractions';btn.textContent='INFRAÇÕES';
      const work=tabs.querySelector('[data-tab="work"]');if(work)tabs.insertBefore(btn,work);else tabs.appendChild(btn);
      btn.addEventListener('click',()=>{
        document.querySelectorAll('.driver-tabs button').forEach(x=>x.classList.remove('active'));
        document.querySelectorAll('.driver-tab-panel').forEach(x=>x.classList.remove('active'));
        btn.classList.add('active');document.querySelector('[data-panel="infractions"]')?.classList.add('active');
      });
    }
    let panel=document.querySelector('[data-panel="infractions"]');
    if(!panel){
      panel=document.createElement('section');panel.className='driver-tab-panel';panel.dataset.panel='infractions';
      panel.innerHTML=`<article class="driver-card"><div class="card-title"><div><span class="eyebrow">QUALIDADE DA CONDUÇÃO</span><h2>Infrações e penalidades</h2></div><span class="pill blue">REGRAS GAT</span></div><div id="gatInfractionStats" class="gat-infraction-grid"></div><div id="gatInfractionList" class="gat-infraction-list"></div><p class="gat-infraction-note">Velocidade acima de 91 km/h gera multa quando mantida por 5 segundos. Danos e multas descontam pontos fixos do XP da entrega.</p></article>`;
      const workPanel=document.querySelector('[data-panel="work"]');if(workPanel)workPanel.insertAdjacentElement('beforebegin',panel);else document.getElementById('driverContent')?.appendChild(panel);
    }
    return panel;
  }

  function dataOf(d){
    const distance=Math.max(0,n(d?.distance_km));
    const base=Math.max(0,n(d?.base_xp)||Math.floor(distance/100)*20);
    const fines=Math.max(0,Math.round(n(d?.speed_fines)));
    const speed=Math.max(0,n(d?.speed_penalty_xp)||fines*3);
    const cargo=Math.max(0,n(d?.cargo_penalty_xp));
    const truck=Math.max(0,n(d?.truck_penalty_xp));
    const penalty=Math.max(0,n(d?.penalty_xp)||speed+cargo+truck);
    const final=(d&&Object.prototype.hasOwnProperty.call(d,'xp_awarded')&&Number.isFinite(Number(d.xp_awarded)))?Math.max(0,Number(d.xp_awarded)):Math.max(0,base-penalty);
    return {distance,base,fines,speed,cargo,truck,penalty,final,cargoDamage:Math.max(0,n(d?.cargo_damage_pct)),truckDamage:Math.max(0,n(d?.truck_damage_delta_pct))};
  }

  function render(){
    ensurePanel();
    const stats=document.getElementById('gatInfractionStats'),root=document.getElementById('gatInfractionList');if(!stats||!root)return;
    let list=[];try{list=Array.isArray(profile?.deliveries)?profile.deliveries:[]}catch(_){}
    const parsed=list.map(d=>({d,...dataOf(d)}));
    const totalFines=parsed.reduce((s,x)=>s+x.fines,0),lost=parsed.reduce((s,x)=>s+x.penalty,0),clean=parsed.filter(x=>x.penalty===0).length;
    const cargoSamples=parsed.filter(x=>x.cargoDamage>0),truckSamples=parsed.filter(x=>x.truckDamage>0);
    const avgCargo=cargoSamples.length?cargoSamples.reduce((s,x)=>s+x.cargoDamage,0)/cargoSamples.length:0;
    const avgTruck=truckSamples.length?truckSamples.reduce((s,x)=>s+x.truckDamage,0)/truckSamples.length:0;
    stats.innerHTML=`<article class="bad"><small>MULTAS DE VELOCIDADE</small><b>${fmt(totalFines)}</b></article><article class="bad"><small>XP PERDIDO</small><b>-${fmt(lost)}</b></article><article class="good"><small>VIAGENS SEM PENALIDADE</small><b>${fmt(clean)}</b></article><article><small>DANO MÉDIO DA CARGA</small><b>${pct(avgCargo)}</b></article><article><small>DANO MÉDIO DO CAMINHÃO</small><b>${pct(avgTruck)}</b></article><article class="blue"><small>ENTREGAS ANALISADAS</small><b>${fmt(parsed.length)}</b></article>`;
    const bad=[...parsed].reverse().filter(x=>x.penalty>0||x.fines>0||x.cargoDamage>0||x.truckDamage>0).slice(0,12);
    root.textContent='';
    if(!bad.length){root.innerHTML='<div class="gat-infraction-empty">Nenhuma infração registrada nas entregas disponíveis.</div>';return;}
    bad.forEach(x=>{const route=(x.d?.source||'?')+' → '+(x.d?.destination||'?'),el=document.createElement('div');el.className='gat-infraction-row';el.innerHTML=`<div><strong>${esc(route)}</strong><small>${esc(x.d?.cargo||'Carga')} • ${Math.round(x.distance).toLocaleString('pt-BR')} km</small></div><div class="gat-infraction-chip ${x.fines?'bad':''}">VELOCIDADE<br>${x.fines?x.fines+' multa'+(x.fines===1?'':'s'):'0 multas'}</div><div class="gat-infraction-chip ${x.cargoDamage?'bad':''}">CARGA<br>${pct(x.cargoDamage)}</div><div class="gat-infraction-chip ${x.truckDamage?'bad':''}">CAMINHÃO<br>+${pct(x.truckDamage)}</div><div class="gat-infraction-chip ${x.penalty?'bad':'good'}">PENALIDADE<br>${x.penalty?'-'+fmt(x.penalty)+' XP':'0 XP'}</div>`;root.appendChild(el)});
  }

  injectStyle();ensurePanel();
  try{if(typeof renderProfile==='function'){const base=renderProfile;renderProfile=function(){const r=base.apply(this,arguments);setTimeout(render,0);return r}}}catch(_){}
  render();let last='';setInterval(()=>{let sig='';try{sig=JSON.stringify((profile?.deliveries||[]).map(x=>[x.id,x.xp_awarded,x.penalty_xp,x.speed_fines,x.cargo_damage_pct,x.truck_damage_delta_pct]))}catch(_){}if(sig!==last){last=sig;render()}},1500);
})();
