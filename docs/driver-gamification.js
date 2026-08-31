(()=>{
  const n=v=>Number(v)||0;
  const fmt=v=>n(v).toLocaleString('pt-BR');
  const pct=v=>n(v).toLocaleString('pt-BR',{maximumFractionDigits:1})+'%';
  const esc2=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const km2=v=>Math.round(n(v)).toLocaleString('pt-BR')+' km';

  function injectStyle(){
    if(document.getElementById('gatGamificationStyle'))return;
    const s=document.createElement('style');s.id='gatGamificationStyle';s.textContent=`
.gat-game-grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:10px;margin-bottom:14px}.gat-game-card{border:1px solid #1d3146;border-radius:14px;background:linear-gradient(180deg,#0c1721,#081018);padding:14px}.gat-game-card small{display:block;color:#71869b;font-size:8px;font-weight:950}.gat-game-card b{display:block;margin-top:7px;font-size:19px;color:#eef6ff}.gat-game-card.good b{color:#63ddb3}.gat-game-card.bad b{color:#ff8da5}.gat-game-card.blue b{color:#75bdff}.gat-game-card.gold b{color:#ffd46b}.gat-game-list{display:grid;gap:8px}.gat-game-row{display:grid;grid-template-columns:minmax(0,1.6fr) 100px 110px 110px 110px;gap:8px;align-items:center;border:1px solid #1a2c3e;border-radius:12px;background:#0b141d;padding:11px}.gat-game-row strong{font-size:11px}.gat-game-row span{font-size:9px;color:#8093a8}.gat-game-row .bad{color:#ff8da5;font-weight:900}.gat-game-row .good{color:#67ddb5;font-weight:900}.gat-achievements{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}.gat-achievement{border:1px solid #20344a;border-radius:15px;background:#0b151f;padding:16px;opacity:.48}.gat-achievement.unlocked{opacity:1;border-color:#315f88;background:linear-gradient(180deg,#102338,#0a151f)}.gat-achievement .medal{font-size:28px}.gat-achievement b{display:block;margin:8px 0 5px}.gat-achievement small{color:#7f92a8;line-height:1.4}.gat-safety-score{display:flex;align-items:center;justify-content:space-between;border:1px solid #24425e;border-radius:16px;background:linear-gradient(135deg,#0b1b29,#0a1119);padding:16px;margin-bottom:14px}.gat-safety-score strong{font-size:30px;color:#79c1ff}.gat-safety-score span{color:#7f93a9;font-size:10px}.gat-perfect{color:#ffd46b!important;font-weight:950}.gat-game-empty{padding:22px;border:1px dashed #263b51;border-radius:13px;text-align:center;color:#74889e;font-size:10px}@media(max-width:900px){.gat-game-grid{grid-template-columns:repeat(2,1fr)}.gat-achievements{grid-template-columns:1fr 1fr}.gat-game-row{grid-template-columns:1fr 80px 90px}.gat-game-row span:nth-child(4),.gat-game-row span:nth-child(5){display:none}}@media(max-width:600px){.gat-game-grid,.gat-achievements{grid-template-columns:1fr}.gat-game-row{grid-template-columns:1fr 72px}.gat-game-row span:nth-child(3){display:none}}
`;
    document.head.appendChild(s);
  }

  function ensureTabs(){
    const nav=document.querySelector('.driver-tabs'),content=document.getElementById('driverContent');if(!nav||!content)return;
    const defs=[['infractions','INFRAÇÕES'],['statistics','ESTATÍSTICAS'],['achievements','CONQUISTAS']];
    for(const [id,label] of defs){
      if(!nav.querySelector('[data-tab="'+id+'"]')){const b=document.createElement('button');b.type='button';b.dataset.tab=id;b.textContent=label;nav.appendChild(b)}
      if(!content.querySelector('[data-panel="'+id+'"]')){const sec=document.createElement('section');sec.className='driver-tab-panel';sec.dataset.panel=id;sec.innerHTML='<article class="driver-card"><div class="card-title"><div><span class="eyebrow">GAT-LOG</span><h2>'+label+'</h2></div></div><div id="gat-'+id+'-body"></div></article>';content.appendChild(sec)}
    }
    nav.querySelectorAll('button').forEach(b=>{if(b.dataset.gatGameBound)return;b.dataset.gatGameBound='1';b.addEventListener('click',()=>{nav.querySelectorAll('button').forEach(x=>x.classList.remove('active'));content.querySelectorAll('.driver-tab-panel').forEach(x=>x.classList.remove('active'));b.classList.add('active');content.querySelector('[data-panel="'+b.dataset.tab+'"]')?.classList.add('active')})});
  }

  function parseDelivery(d){
    const distance=Math.max(0,n(d?.distance_km)),base=Math.max(0,n(d?.base_xp)||Math.floor(distance/100)*20),fines=Math.max(0,Math.round(n(d?.speed_fines)));
    const speed=Math.max(0,n(d?.gat_speed_penalty_points)||fines*3),cargo=Math.max(0,n(d?.gat_cargo_penalty_points)),truck=Math.max(0,n(d?.gat_truck_penalty_points)),penalty=Math.max(0,n(d?.gat_penalty_points)||n(d?.penalty_xp)||speed+cargo+truck),bonus=Math.max(0,n(d?.perfect_bonus_xp));
    const final=(d&&Object.prototype.hasOwnProperty.call(d,'xp_awarded')&&Number.isFinite(Number(d.xp_awarded)))?Math.max(0,Number(d.xp_awarded)):Math.max(0,base+bonus),score=(d&&Object.prototype.hasOwnProperty.call(d,'gat_points')&&Number.isFinite(Number(d.gat_points)))?Math.max(0,Number(d.gat_points)):Math.max(0,100-penalty);
    return {d,distance,base,fines,speed,cargo,truck,penalty,bonus,final,score,cargoDamage:Math.max(0,n(d?.cargo_damage_pct)),truckDamage:Math.max(0,n(d?.truck_damage_delta_pct)),perfect:!!d?.perfect_trip||bonus>0};
  }

  function fallbackSafety(list){
    const p=list.map(parseDelivery),fines=p.reduce((s,x)=>s+x.fines,0),lost=p.reduce((s,x)=>s+x.penalty,0),perfect=p.filter(x=>x.perfect).length,clean=p.filter(x=>x.penalty===0).length,noFineKm=p.filter(x=>x.fines===0).reduce((s,x)=>s+x.distance,0);
    const cd=p.filter(x=>x.cargoDamage>0),td=p.filter(x=>x.truckDamage>0),avgC=cd.length?cd.reduce((s,x)=>s+x.cargoDamage,0)/cd.length:0,avgT=td.length?td.reduce((s,x)=>s+x.truckDamage,0)/td.length:0;
    let score=0;if(p.length){score=(clean/p.length*100)*.7+(perfect/p.length*100)*.3-(fines/p.length)*8-avgC*1.5-avgT;score=Math.max(0,Math.min(100,score))}
    return {deliveries:p.length,speed_fines:fines,penalty_points:lost,penalty_xp:lost,perfect_trips:perfect,clean_trips:clean,no_fine_km:noFineKm,avg_cargo_damage_pct:avgC,avg_truck_damage_pct:avgT,score};
  }

  function achievementsFallback(p,st){
    return [
      {title:'Primeira Entrega',description:'Conclua sua primeira entrega GAT.',unlocked:n(p?.total_deliveries)>=1},
      {title:'Na Estrada',description:'Conclua 10 entregas GAT.',unlocked:n(p?.total_deliveries)>=10},
      {title:'Direção de Ouro',description:'Conclua 10 viagens perfeitas.',unlocked:n(st?.perfect_trips)>=10},
      {title:'Pé Leve',description:'Percorra 5.000 km em entregas sem multa.',unlocked:n(st?.no_fine_km)>=5000},
      {title:'Meta do Mês',description:'Conclua os 30 trabalhos do mês.',unlocked:n(p?.monthly_completed)>=30},
      {title:'Veterano GAT',description:'Ultrapasse 50.000 km acumulados.',unlocked:n(p?.total_km)>=50000}
    ];
  }

  function currentProfile(){try{return typeof profile!=='undefined'?profile:null}catch(_){return null}}

  function render(){
    ensureTabs();const p=currentProfile();if(!p)return;const history=Array.isArray(p.deliveries)?p.deliveries:[],parsed=history.map(parseDelivery),st=p.safety||fallbackSafety(history),lost=parsed.reduce((s,x)=>s+x.penalty,0);
    const infra=document.getElementById('gat-infractions-body');if(infra){
      const bad=[...parsed].reverse().filter(x=>x.penalty>0);
      infra.innerHTML=`<div class="gat-safety-score"><div><span>ÍNDICE DE DIREÇÃO SEGURA</span><strong>${n(st.score).toLocaleString('pt-BR',{maximumFractionDigits:1})}</strong></div><span>0 a 100</span></div><div class="gat-game-grid"><article class="gat-game-card bad"><small>MULTAS DE VELOCIDADE</small><b>${fmt(st.speed_fines)}</b></article><article class="gat-game-card bad"><small>PONTOS PERDIDOS</small><b>-${fmt(lost)}</b></article><article class="gat-game-card good"><small>VIAGENS LIMPAS</small><b>${fmt(st.clean_trips)}</b></article><article class="gat-game-card"><small>DANO MÉDIO DA CARGA</small><b>${pct(st.avg_cargo_damage_pct)}</b></article><article class="gat-game-card"><small>DANO MÉDIO DO CAMINHÃO</small><b>${pct(st.avg_truck_damage_pct)}</b></article></div><div class="gat-game-list">${bad.length?bad.slice(0,30).map(x=>`<div class="gat-game-row"><strong>${esc2((x.d?.source||'—')+' → '+(x.d?.destination||'—'))}</strong><span>${x.score} pts</span><span class="${x.fines?'bad':''}">${x.fines} multa${x.fines===1?'':'s'}</span><span class="${x.cargo?'bad':''}">Carga ${pct(x.cargoDamage)}</span><span class="${x.truck?'bad':''}">Caminhão +${pct(x.truckDamage)}</span></div>`).join(''):'<div class="gat-game-empty">Nenhuma infração registrada. Continue assim.</div>'}</div>`;
    }
    const stats=document.getElementById('gat-statistics-body');if(stats){
      const longest=parsed.reduce((a,b)=>b.distance>(a?.distance||0)?b:a,null),avg=parsed.length?parsed.reduce((s,x)=>s+x.distance,0)/parsed.length:0;
      stats.innerHTML=`<div class="gat-game-grid"><article class="gat-game-card blue"><small>KM TOTAL</small><b>${km2(p.total_km)}</b></article><article class="gat-game-card"><small>MÉDIA POR ENTREGA</small><b>${km2(avg)}</b></article><article class="gat-game-card"><small>MAIOR VIAGEM</small><b>${longest?km2(longest.distance):'—'}</b></article><article class="gat-game-card gold"><small>VIAGENS PERFEITAS</small><b>${fmt(st.perfect_trips)}</b></article><article class="gat-game-card good"><small>KM SEM MULTA</small><b>${km2(st.no_fine_km)}</b></article></div><div class="gat-game-grid"><article class="gat-game-card gold"><small>PONTOS GAT DO MÊS</small><b>${fmt(p.points)}</b></article><article class="gat-game-card"><small>ENTREGAS</small><b>${fmt(p.total_deliveries)}</b></article><article class="gat-game-card"><small>XP TOTAL</small><b>${fmt(p.xp)}</b></article><article class="gat-game-card bad"><small>PONTOS PERDIDOS</small><b>-${fmt(lost)}</b></article><article class="gat-game-card gold"><small>TAXA PERFEITA</small><b>${pct(n(st.deliveries)?n(st.perfect_trips)/n(st.deliveries)*100:0)}</b></article></div>`;
    }
    const ach=document.getElementById('gat-achievements-body');if(ach){const list=Array.isArray(p.achievements)?p.achievements:achievementsFallback(p,st);ach.innerHTML='<div class="gat-achievements">'+list.map(a=>`<article class="gat-achievement ${a.unlocked?'unlocked':''}"><div class="medal">${a.unlocked?'🏆':'🔒'}</div><b>${esc2(a.title||'Conquista')}</b><small>${esc2(a.description||'')}</small></article>`).join('')+'</div>'}
  }

  injectStyle();ensureTabs();
  if(typeof renderProfile==='function'){
    const base=renderProfile;renderProfile=function(){const r=base.apply(this,arguments);setTimeout(render,0);return r};
  }
  setTimeout(render,300);setInterval(render,5000);
})();
