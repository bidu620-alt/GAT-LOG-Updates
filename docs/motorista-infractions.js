(()=>{
  const n=v=>Number(v)||0;
  const fmt=v=>n(v).toLocaleString('pt-BR');
  const pct=v=>n(v).toLocaleString('pt-BR',{minimumFractionDigits:0,maximumFractionDigits:2})+'%';
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const has=(o,k)=>!!o&&Object.prototype.hasOwnProperty.call(o,k)&&Number.isFinite(Number(o[k]));

  function injectStyle(){
    if(document.getElementById('gatInfractionsStyle'))return;
    const s=document.createElement('style');s.id='gatInfractionsStyle';s.textContent=`
.gat-infraction-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:9px;margin-bottom:14px}.gat-infraction-grid article{background:#0b131c;border:1px solid #1b2d40;border-radius:15px;padding:15px}.gat-infraction-grid small{display:block;color:#71849a;font-size:8px;font-weight:950}.gat-infraction-grid b{display:block;margin-top:7px;font-size:20px;color:#e8f2ff}.gat-infraction-grid article.bad b{color:#ff91a7}.gat-infraction-grid article.good b{color:#62deb4}.gat-infraction-grid article.blue b{color:#74bcff}.gat-infraction-list{display:grid;gap:8px;margin-top:14px}.gat-infraction-row{display:grid;grid-template-columns:1.25fr .65fr .7fr .75fr .75fr .75fr;gap:9px;align-items:center;border:1px solid #1a2b3d;border-radius:13px;background:#0c141d;padding:12px}.gat-infraction-row strong{font-size:11px}.gat-infraction-row small{display:block;color:#708399;font-size:8px;margin-top:3px}.gat-infraction-chip{border:1px solid #304154;border-radius:8px;padding:7px 8px;font-size:8px;font-weight:900;color:#a9bbce;background:#0e1822;line-height:1.45}.gat-infraction-chip.bad{border-color:#5b2a35;background:#1b1117;color:#ff91a7}.gat-infraction-chip.good{border-color:#1f5949;background:#0c211b;color:#73dcb7}.gat-infraction-chip.blue{border-color:#254f73;background:#0c1d2b;color:#75bfff}.gat-damage-parts{grid-column:1/-1;display:flex;flex-wrap:wrap;gap:6px;padding-top:2px}.gat-damage-parts span{font-size:8px;color:#8ea2b8;background:#0a1118;border:1px solid #1c3044;border-radius:999px;padding:5px 8px}.gat-infraction-empty{padding:28px;text-align:center;border:1px dashed #294157;border-radius:14px;color:#7c91a8;background:#0b141d}.gat-infraction-note{margin-top:12px;color:#65798f;font-size:9px;line-height:1.55}.gat-history-audit{grid-column:1/-1;display:flex;flex-wrap:wrap;gap:6px;margin-top:2px;padding-top:8px;border-top:1px dashed #203247}.gat-history-audit span{font-size:8px;font-weight:850;color:#91a4b9;border:1px solid #263a4f;background:#0b151f;border-radius:999px;padding:5px 8px}.gat-history-audit .bad{color:#ff91a7;border-color:#59303a;background:#1b1117}.gat-history-audit .good{color:#72ddb8;border-color:#215644;background:#0b2019}.gat-history-audit .blue{color:#7fc0ff;border-color:#285173;background:#0b1d2a}@media(max-width:1200px){.gat-infraction-grid{grid-template-columns:repeat(4,1fr)}.gat-infraction-row{grid-template-columns:1.3fr .7fr .7fr .7fr .7fr}.gat-infraction-row .trailer-chip{display:none}}@media(max-width:900px){.gat-infraction-grid{grid-template-columns:repeat(2,1fr)}.gat-infraction-row{grid-template-columns:1fr 1fr}.gat-infraction-row>div:first-child,.gat-damage-parts{grid-column:1/-1}}@media(max-width:560px){.gat-infraction-grid{grid-template-columns:1fr 1fr}}`;
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
      panel.innerHTML=`<article class="driver-card"><div class="card-title"><div><span class="eyebrow">QUALIDADE DA CONDUÇÃO</span><h2>Infrações e penalidades</h2></div><span class="pill blue">REGRAS GAT</span></div><div id="gatInfractionStats" class="gat-infraction-grid"></div><div id="gatInfractionList" class="gat-infraction-list"></div><p class="gat-infraction-note">O dano do caminhão é somente o dano novo ocorrido durante a viagem: usa o maior aumento entre motor, câmbio, cabine, chassi e rodas. As porcentagens das peças não são somadas. O reboque fica registrado para auditoria e não gera penalidade enquanto não houver regra específica.</p></article>`;
      const workPanel=document.querySelector('[data-panel="work"]');if(workPanel)workPanel.insertAdjacentElement('beforebegin',panel);else document.getElementById('driverContent')?.appendChild(panel);
    }
    return panel;
  }

  function dataOf(d){
    const distance=Math.max(0,n(d?.distance_km));
    const base=Math.max(0,n(d?.base_xp)||Math.floor(distance/100)*20);
    const fines=Math.max(0,Math.round(n(d?.speed_fines)));
    const speed=Math.max(0,n(d?.speed_penalty_xp)||n(d?.gat_speed_penalty_points)||fines*3);
    const cargo=Math.max(0,n(d?.cargo_penalty_xp)||n(d?.gat_cargo_penalty_points));
    const truck=Math.max(0,n(d?.truck_penalty_xp)||n(d?.gat_truck_penalty_points));
    const penalty=Math.max(0,n(d?.penalty_xp)||speed+cargo+truck);
    const penaltyPoints=has(d,'gat_penalty_points')?Math.max(0,n(d.gat_penalty_points)):Math.min(100,penalty);
    const gatPoints=has(d,'gat_points')?Math.max(0,n(d.gat_points)):Math.max(0,100-penaltyPoints);
    const final=has(d,'xp_awarded')?Math.max(0,n(d.xp_awarded)):Math.max(0,base-penalty);
    return {
      distance,base,fines,speed,cargo,truck,penalty,penaltyPoints,gatPoints,final,
      cargoDamage:Math.max(0,n(d?.cargo_damage_pct)),
      truckDamage:Math.max(0,n(d?.truck_damage_delta_pct)),
      trailerDamage:Math.max(0,n(d?.trailer_damage_delta_pct)),
      engine:Math.max(0,n(d?.truck_engine_damage_delta_pct)),
      transmission:Math.max(0,n(d?.truck_transmission_damage_delta_pct)),
      cabin:Math.max(0,n(d?.truck_cabin_damage_delta_pct)),
      chassis:Math.max(0,n(d?.truck_chassis_damage_delta_pct)),
      wheels:Math.max(0,n(d?.truck_wheels_damage_delta_pct))
    };
  }

  function partsHtml(x){
    const parts=[['Motor',x.engine],['Câmbio',x.transmission],['Cabine',x.cabin],['Chassi',x.chassis],['Rodas',x.wheels]];
    if(!parts.some(([,v])=>v>0))return '';
    return '<div class="gat-damage-parts">'+parts.map(([k,v])=>'<span>'+k+' +'+pct(v)+'</span>').join('')+'</div>';
  }

  function renderHistory(list){
    const root=document.getElementById('deliveryRows');if(!root)return;
    const history=[...(Array.isArray(list)?list:[])].reverse();
    const rows=[...root.querySelectorAll('.delivery-row')];
    rows.forEach((row,i)=>{
      row.querySelector('.gat-history-audit')?.remove();
      const d=history[i];if(!d)return;
      const x=dataOf(d),audit=document.createElement('div');audit.className='gat-history-audit';
      const hasAudit=has(d,'gat_points')||has(d,'cargo_damage_pct')||has(d,'truck_damage_delta_pct')||has(d,'trailer_damage_delta_pct');
      if(!hasAudit){audit.innerHTML='<span>Entrega antiga • danos detalhados indisponíveis</span>';row.appendChild(audit);return;}
      const pieces=[`<span class="${x.gatPoints>=100?'good':'blue'}">Pontos GAT ${fmt(x.gatPoints)}/100</span>`,
        `<span class="${x.cargoDamage>0?'bad':''}">Carga ${pct(x.cargoDamage)}</span>`,
        `<span class="${x.truckDamage>0?'bad':''}">Caminhão +${pct(x.truckDamage)}</span>`,
        `<span>Reboque +${pct(x.trailerDamage)}</span>`];
      if(x.penaltyPoints>0)pieces.push(`<span class="bad">-${fmt(x.penaltyPoints)} pts</span>`);
      audit.innerHTML=pieces.join('');
      row.appendChild(audit);
    });
  }

  function render(){
    ensurePanel();
    const stats=document.getElementById('gatInfractionStats'),root=document.getElementById('gatInfractionList');if(!stats||!root)return;
    let list=[];try{list=Array.isArray(profile?.deliveries)?profile.deliveries:[]}catch(_){}
    const parsed=list.map(d=>({d,...dataOf(d)}));
    const totalFines=parsed.reduce((s,x)=>s+x.fines,0);
    const lostPoints=parsed.reduce((s,x)=>s+x.penaltyPoints,0);
    const lostXp=parsed.reduce((s,x)=>s+x.penalty,0);
    const clean=parsed.filter(x=>x.penaltyPoints===0).length;
    const avgCargo=parsed.length?parsed.reduce((s,x)=>s+x.cargoDamage,0)/parsed.length:0;
    const avgTruck=parsed.length?parsed.reduce((s,x)=>s+x.truckDamage,0)/parsed.length:0;
    const avgTrailer=parsed.length?parsed.reduce((s,x)=>s+x.trailerDamage,0)/parsed.length:0;
    stats.innerHTML=`<article class="bad"><small>MULTAS DE VELOCIDADE</small><b>${fmt(totalFines)}</b></article><article class="bad"><small>PONTOS GAT PERDIDOS</small><b>-${fmt(lostPoints)}</b></article><article class="bad"><small>XP PERDIDO</small><b>-${fmt(lostXp)}</b></article><article class="good"><small>VIAGENS SEM PENALIDADE</small><b>${fmt(clean)}</b></article><article><small>DANO MÉDIO DA CARGA</small><b>${pct(avgCargo)}</b></article><article><small>DANO NOVO MÉDIO DO CAMINHÃO</small><b>${pct(avgTruck)}</b></article><article><small>DANO NOVO MÉDIO DO REBOQUE</small><b>${pct(avgTrailer)}</b></article><article class="blue"><small>ENTREGAS ANALISADAS</small><b>${fmt(parsed.length)}</b></article>`;
    const bad=[...parsed].reverse().filter(x=>x.penaltyPoints>0||x.fines>0||x.cargoDamage>0||x.truckDamage>0||x.trailerDamage>0).slice(0,15);
    root.textContent='';
    if(!bad.length){root.innerHTML='<div class="gat-infraction-empty">Nenhuma infração ou dano registrado nas entregas disponíveis.</div>';}
    bad.forEach(x=>{
      const route=(x.d?.source||'?')+' → '+(x.d?.destination||'?'),el=document.createElement('div');el.className='gat-infraction-row';
      el.innerHTML=`<div><strong>${esc(route)}</strong><small>${esc(x.d?.cargo||'Carga')} • ${Math.round(x.distance).toLocaleString('pt-BR')} km</small></div><div class="gat-infraction-chip ${x.fines?'bad':''}">VELOCIDADE<br>${x.fines?x.fines+' multa'+(x.fines===1?'':'s'):'0 multas'}</div><div class="gat-infraction-chip ${x.cargoDamage>0?'bad':''}">CARGA<br>${pct(x.cargoDamage)}</div><div class="gat-infraction-chip ${x.truckDamage>0?'bad':''}">CAMINHÃO<br>+${pct(x.truckDamage)}</div><div class="gat-infraction-chip trailer-chip">REBOQUE<br>+${pct(x.trailerDamage)}</div><div class="gat-infraction-chip ${x.penaltyPoints?'bad':'good'}">PONTOS<br>${fmt(x.gatPoints)}/100${x.penaltyPoints?'<br>-'+fmt(x.penaltyPoints):''}</div>${partsHtml(x)}`;
      root.appendChild(el);
    });
    renderHistory(list);
  }

  injectStyle();ensurePanel();
  try{if(typeof renderProfile==='function'){const base=renderProfile;renderProfile=function(){const r=base.apply(this,arguments);setTimeout(render,0);return r}}}catch(_){}
  render();let last='';setInterval(()=>{let sig='';try{sig=JSON.stringify((profile?.deliveries||[]).map(x=>[x.id,x.gat_points,x.gat_penalty_points,x.xp_awarded,x.penalty_xp,x.speed_fines,x.cargo_damage_pct,x.truck_damage_delta_pct,x.trailer_damage_delta_pct,x.truck_engine_damage_delta_pct,x.truck_transmission_damage_delta_pct,x.truck_cabin_damage_delta_pct,x.truck_chassis_damage_delta_pct,x.truck_wheels_damage_delta_pct]))}catch(_){}if(sig!==last){last=sig;render()}},1500);
})();
