(()=>{
  const n=v=>Number(v)||0;
  const fmt=v=>n(v).toLocaleString('pt-BR');
  const pct=v=>n(v).toLocaleString('pt-BR',{maximumFractionDigits:1})+'%';
  const esc2=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const km2=v=>Math.round(n(v)).toLocaleString('pt-BR')+' km';

  function injectStyle(){
    if(document.getElementById('gatGamificationStyle'))return;
    const s=document.createElement('style');s.id='gatGamificationStyle';s.textContent=`
.gat-game-grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:10px;margin-bottom:14px}.gat-game-card{border:1px solid #1d3146;border-radius:14px;background:linear-gradient(180deg,#0c1721,#081018);padding:14px}.gat-game-card small{display:block;color:#71869b;font-size:8px;font-weight:950}.gat-game-card b{display:block;margin-top:7px;font-size:19px;color:#eef6ff}.gat-game-card.good b{color:#63ddb3}.gat-game-card.bad b{color:#ff8da5}.gat-game-card.blue b{color:#75bdff}.gat-game-card.gold b{color:#ffd46b}.gat-game-list{display:grid;gap:8px}.gat-game-row{display:grid;grid-template-columns:minmax(0,1.6fr) 100px 110px 110px 110px;gap:8px;align-items:center;border:1px solid #1a2c3e;border-radius:12px;background:#0b141d;padding:11px}.gat-game-row strong{font-size:11px}.gat-game-row span{font-size:9px;color:#8093a8}.gat-game-row .bad{color:#ff8da5;font-weight:900}.gat-game-row .good{color:#67ddb5;font-weight:900}.gat-achievements{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}.gat-achievement{border:1px solid #20344a;border-radius:15px;background:#0b151f;padding:16px;opacity:.48}.gat-achievement.unlocked{opacity:1;border-color:#315f88;background:linear-gradient(180deg,#102338,#0a151f)}.gat-achievement .medal{font-size:28px;width:54px;height:54px;border-radius:50%;display:grid;place-items:center;background:#101b26;border:1px solid #2a4056;box-shadow:inset 0 0 0 4px #0a1118}.gat-achievement.unlocked .medal{border-color:#4777a0;background:#10273a}.gat-achievement.halloween-achievement{border-color:#6a391c;background:radial-gradient(circle at 12% 18%,rgba(255,105,0,.12),transparent 28%),linear-gradient(180deg,#19100c,#0b1016)}.gat-achievement.halloween-achievement.unlocked{border-color:#e27624;box-shadow:0 0 24px rgba(255,103,0,.12)}.gat-achievement.halloween-achievement .medal{width:64px;height:64px;border-radius:50%;border:2px solid #e06d20;background:radial-gradient(circle,#512005,#1b0c05 68%);box-shadow:inset 0 0 0 5px #100804,0 0 20px rgba(255,91,0,.22);font-size:31px}.gat-achievement-progress{height:6px;border-radius:999px;background:#19212b;overflow:hidden;margin-top:11px}.gat-achievement-progress i{display:block;height:100%;background:linear-gradient(90deg,#ff6a00,#9b50ff);border-radius:inherit}.gat-achievement-date{display:block;color:#d69563;font-size:8px;margin-top:7px}.gat-achievement b{display:block;margin:8px 0 5px}.gat-achievement small{color:#7f92a8;line-height:1.4}.gat-achievement-meta{display:flex;flex-wrap:wrap;gap:6px;margin-top:10px}.gat-achievement-meta span{font-size:8px;font-weight:900;border:1px solid #2a4056;border-radius:999px;padding:5px 8px;color:#9db1c6;background:#0b151f}.gat-achievement-meta .xp{color:#ffd46b;border-color:#665126;background:#211a0c}.gat-safety-score{display:flex;align-items:center;justify-content:space-between;border:1px solid #24425e;border-radius:16px;background:linear-gradient(135deg,#0b1b29,#0a1119);padding:16px;margin-bottom:14px}.gat-safety-score strong{font-size:30px;color:#79c1ff}.gat-safety-score span{color:#7f93a9;font-size:10px}.gat-perfect{color:#ffd46b!important;font-weight:950}.gat-game-empty{padding:22px;border:1px dashed #263b51;border-radius:13px;text-align:center;color:#74889e;font-size:10px}@media(max-width:900px){.gat-game-grid{grid-template-columns:repeat(2,1fr)}.gat-achievements{grid-template-columns:1fr 1fr}.gat-game-row{grid-template-columns:1fr 80px 90px}.gat-game-row span:nth-child(4),.gat-game-row span:nth-child(5){display:none}}@media(max-width:600px){.gat-game-grid,.gat-achievements{grid-template-columns:1fr}.gat-game-row{grid-template-columns:1fr 72px}.gat-game-row span:nth-child(3){display:none}}
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
    const distance=Math.max(0,n(d?.distance_km)),base=Math.max(0,n(d?.base_xp)||Math.floor(distance)),fines=Math.max(0,Math.round(n(d?.speed_fines)));
    const speeding=Math.max(0,n(d?.gat_speeding_penalty_points)),speedingRatio=Math.max(0,n(d?.gat_speeding_ratio_pct)),speedingSeconds=Math.max(0,n(d?.gat_speeding_seconds));
    const traffic=Math.max(0,n(d?.gat_fine_penalty_points)||n(d?.gat_speed_penalty_points)||fines*3),cargo=Math.max(0,n(d?.gat_cargo_penalty_points)),truck=Math.max(0,n(d?.gat_truck_penalty_points));
    const rulePenalty=Math.max(0,n(d?.gat_penalty_points)||n(d?.penalty_xp)||traffic+cargo+truck),penalty=speeding+rulePenalty,bonus=Math.max(0,n(d?.perfect_bonus_xp));
    const final=(d&&Object.prototype.hasOwnProperty.call(d,'xp_awarded')&&Number.isFinite(Number(d.xp_awarded)))?Math.max(0,Number(d.xp_awarded)):Math.max(0,base+bonus),score=(d&&Object.prototype.hasOwnProperty.call(d,'gat_points')&&Number.isFinite(Number(d.gat_points)))?Math.max(0,Number(d.gat_points)):Math.max(0,base-penalty+bonus);
    return {d,distance,base,fines,speeding,speedingRatio,speedingSeconds,traffic,cargo,truck,penalty,bonus,final,score,cargoDamage:Math.max(0,n(d?.cargo_damage_pct)),truckDamage:Math.max(0,n(d?.truck_damage_delta_pct)),perfect:!!d?.perfect_trip||bonus>0};
  }

  function fallbackSafety(list){
    const p=list.map(parseDelivery),fines=p.reduce((s,x)=>s+x.fines,0),lost=p.reduce((s,x)=>s+x.penalty,0),speedLost=p.reduce((s,x)=>s+x.speeding,0),speedTrips=p.filter(x=>x.speeding>0).length,perfect=p.filter(x=>x.perfect).length,clean=p.filter(x=>x.penalty===0).length,noFineKm=p.filter(x=>x.fines===0).reduce((s,x)=>s+x.distance,0);
    const cd=p.filter(x=>x.cargoDamage>0),td=p.filter(x=>x.truckDamage>0),avgC=cd.length?cd.reduce((s,x)=>s+x.cargoDamage,0)/cd.length:0,avgT=td.length?td.reduce((s,x)=>s+x.truckDamage,0)/td.length:0;
    let score=0;if(p.length){score=(clean/p.length*100)*.7+(perfect/p.length*100)*.3-(speedTrips/p.length)*12-avgC*1.5-avgT;score=Math.max(0,Math.min(100,score))}
    return {deliveries:p.length,speed_fines:fines,speed_penalty_points:speedLost,speeding_trips:speedTrips,penalty_points:lost,penalty_xp:lost,perfect_trips:perfect,clean_trips:clean,no_fine_km:noFineKm,avg_cargo_damage_pct:avgC,avg_truck_damage_pct:avgT,score};
  }

  const normEvent=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  function eventAchievements(p){
    const registry=window.GATEventRegistry;
    return registry&&typeof registry.achievementList==='function'?registry.achievementList(p):[];
  }
  function achievementsFallback(p,st){
    const rows=Array.isArray(p?.deliveries)?p.deliveries:[];
    const parsed=rows.map(parseDelivery);
    const maxDistance=parsed.reduce((m,x)=>Math.max(m,x.distance),0);
    const maxWeight=rows.reduce((m,d)=>Math.max(m,Math.max(0,n(d?.weight_kg))),0);
    const cityKey=v=>normEvent(v);
    const cities=new Set();
    rows.forEach(d=>{const a=cityKey(d?.source),b=cityKey(d?.destination);if(a)cities.add(a);if(b)cities.add(b)});
    const hasOwn=(d,key)=>Object.prototype.hasOwnProperty.call(d||{},key)&&Number.isFinite(Number(d[key]));
    const exactZero=(d,key)=>hasOwn(d,key)&&Number(d[key])===0;
    const truckZero=d=>{
      if(Object.prototype.hasOwnProperty.call(d||{},'truck_damage_delta_pct')&&Number.isFinite(Number(d.truck_damage_delta_pct)))return Number(d.truck_damage_delta_pct)===0;
      if(Object.prototype.hasOwnProperty.call(d||{},'truck_overall_delta_pct')&&Number.isFinite(Number(d.truck_overall_delta_pct)))return Number(d.truck_overall_delta_pct)===0;
      return false;
    };
    const noFine=d=>Number.isFinite(Number(d?.speed_fines))&&Number(d.speed_fines)===0;
    const perfectCargo500=rows.some(d=>n(d?.distance_km)>=500&&exactZero(d,'cargo_damage_pct'));
    const impeccable1000=rows.some(d=>n(d?.distance_km)>=1000&&exactZero(d,'cargo_damage_pct')&&truckZero(d)&&noFine(d));
    const cleanCargoTrips=rows.filter(d=>exactZero(d,'cargo_damage_pct')).length;
    const safeSpeedTrips=rows.filter(d=>hasOwn(d,'gat_speeding_penalty_points')?n(d.gat_speeding_penalty_points)===0:true).length;
    const exemplarTrips=rows.filter(d=>{
      const speedOk=hasOwn(d,'gat_speeding_penalty_points')?n(d.gat_speeding_penalty_points)===0:true;
      const cargoOk=exactZero(d,'cargo_damage_pct');
      const truckOk=truckZero(d);
      return speedOk&&cargoOk&&truckOk;
    }).length;
    const heavy40Trips=rows.filter(d=>n(d?.weight_kg)>=40000).length;
    const marathonTrips=rows.filter(d=>n(d?.distance_km)>=2000).length;
    const estradeiro500Trips=rows.filter(d=>n(d?.distance_km)>=500).length;
    return [
      {id:'peso_pesado',title:'Peso Pesado',description:'Entregue uma carga válida de 30 toneladas ou mais.',unlocked:maxWeight>=30000,medal:'🏋️',difficulty:'Fácil',xpReward:250},
      {id:'entrega_perfeita',title:'Entrega Perfeita',description:'Conclua 500 km ou mais com 0% de dano na carga.',unlocked:perfectCargo500,medal:'🎯',difficulty:'Média',xpReward:500},
      {id:'explorador',title:'Explorador',description:'Realize entregas válidas envolvendo 25 cidades diferentes.',unlocked:cities.size>=25,medal:'🧭',difficulty:'Média',xpReward:500},
      {id:'gigante_estrada',title:'Gigante da Estrada',description:'Entregue uma carga válida de 50 toneladas ou mais.',unlocked:maxWeight>=50000,medal:'🦾',difficulty:'Média',xpReward:500},
      {id:'carga_extrema',title:'Carga Extrema',description:'Entregue uma carga válida de 60 toneladas ou mais.',unlocked:maxWeight>=60000,medal:'🚧',difficulty:'Difícil',xpReward:1000},
      {id:'longa_jornada',title:'Longa Jornada',description:'Conclua uma viagem válida de 3.000 km ou mais.',unlocked:maxDistance>=3000,medal:'🛣️',difficulty:'Difícil',xpReward:1000},
      {id:'viagem_impecavel',title:'Viagem Impecável',description:'Conclua 1.000 km ou mais sem excesso de velocidade, sem dano na carga e sem dano no caminhão.',unlocked:impeccable1000,medal:'💎',difficulty:'Difícil',xpReward:1000},
      {id:'veterano',title:'Veterano',description:'Complete 500 entregas válidas.',unlocked:n(p?.total_deliveries)>=500,medal:'🥇',difficulty:'Difícil',xpReward:1000},
      {id:'horizonte_distante',title:'Horizonte Distante',description:'Conclua uma viagem válida de 5.000 km ou mais.',unlocked:maxDistance>=5000,medal:'🌍',difficulty:'Muito difícil',xpReward:2000},
      {id:'lenda_estrada',title:'Lenda da Estrada',description:'Complete 1.500 entregas válidas.',unlocked:n(p?.total_deliveries)>=1500,medal:'👑',difficulty:'Muito difícil',xpReward:2000},

      {id:'primeiros_passos',title:'Primeiros Passos',description:'Complete 10 entregas válidas.',unlocked:n(p?.total_deliveries)>=10,medal:'🚚',difficulty:'Fácil',xpReward:250},
      {id:'estradeiro',title:'Estradeiro',description:'Complete 50 entregas válidas. Distância mínima: 500 km por entrega.',unlocked:estradeiro500Trips>=50,medal:'🛣️',difficulty:'Média',xpReward:500},
      {id:'rodagem_inicial',title:'Rodagem Inicial',description:'Atinja 5.000 km totais na carreira.',unlocked:n(p?.total_km)>=5000,medal:'🧭',difficulty:'Fácil',xpReward:250},
      {id:'pe_na_estrada',title:'Pé na Estrada',description:'Atinja 25.000 km totais na carreira.',unlocked:n(p?.total_km)>=25000,medal:'🌎',difficulty:'Média',xpReward:500},
      {id:'rodador_nato',title:'Rodador Nato',description:'Atinja 100.000 km totais na carreira.',unlocked:n(p?.total_km)>=100000,medal:'🏁',difficulty:'Difícil',xpReward:1000},
      {id:'carga_limpa',title:'Carga Limpa',description:'Conclua 10 viagens com 0% de dano na carga.',unlocked:cleanCargoTrips>=10,medal:'📦',difficulty:'Média',xpReward:500},
      {id:'direcao_segura',title:'Direção Segura',description:'Conclua 10 viagens sem penalidade por excesso de velocidade.',unlocked:safeSpeedTrips>=10,medal:'🛡️',difficulty:'Média',xpReward:500},
      {id:'motorista_exemplar',title:'Motorista Exemplar',description:'Conclua 10 viagens sem excesso de velocidade e sem danos.',unlocked:exemplarTrips>=10,medal:'⭐',difficulty:'Difícil',xpReward:1000},
      {id:'rei_do_peso',title:'Rei do Peso',description:'Conclua 10 cargas de 40 toneladas ou mais.',unlocked:heavy40Trips>=10,medal:'🏋️',difficulty:'Difícil',xpReward:1000},
      {id:'maratonista',title:'Maratonista',description:'Conclua 5 viagens de 2.000 km ou mais.',unlocked:marathonTrips>=5,medal:'🏆',difficulty:'Muito difícil',xpReward:2000},
    ];
  }

  function currentProfile(){try{return window.GATCurrentDriverProfile||null}catch(_){return null}}

  function render(){
    ensureTabs();const p=currentProfile();if(!p)return;const history=Array.isArray(p.deliveries)?p.deliveries:[],parsed=history.map(parseDelivery),st=p.safety||fallbackSafety(history),lost=parsed.reduce((s,x)=>s+x.penalty,0);
    const infra=document.getElementById('gat-infractions-body');if(infra){
      const bad=[...parsed].reverse().filter(x=>x.penalty>0);
      const speedLost=parsed.reduce((s,x)=>s+x.speeding,0),speedTrips=parsed.filter(x=>x.speeding>0).length;
      infra.innerHTML=`<div class="gat-safety-score"><div><span>ÍNDICE DE DIREÇÃO SEGURA</span><strong>${n(st.score).toLocaleString('pt-BR',{maximumFractionDigits:1})}</strong></div><span>0 a 100</span></div><div class="gat-game-grid"><article class="gat-game-card bad"><small>EXCESSO DE VELOCIDADE</small><b>-${fmt(speedLost)} pts</b></article><article class="gat-game-card bad"><small>VIAGENS COM EXCESSO</small><b>${fmt(speedTrips)}</b></article><article class="gat-game-card bad"><small>PONTOS PERDIDOS</small><b>-${fmt(lost)}</b></article><article class="gat-game-card"><small>DANO MÉDIO DA CARGA</small><b>${pct(st.avg_cargo_damage_pct)}</b></article><article class="gat-game-card"><small>DANO MÉDIO DO CAMINHÃO</small><b>${pct(st.avg_truck_damage_pct)}</b></article></div><div class="gat-game-list">${bad.length?bad.slice(0,30).map(x=>`<div class="gat-game-row"><strong>${esc2((x.d?.source||'—')+' → '+(x.d?.destination||'—'))}</strong><span>${x.score} pts</span><span class="${x.speeding?'bad':''}">${x.speeding?`Excesso ${pct(x.speedingRatio)} • -${fmt(x.speeding)} pts`:'Sem excesso'}</span><span class="${x.cargo?'bad':''}">Carga ${pct(x.cargoDamage)}</span><span class="${x.truck?'bad':''}">Caminhão +${pct(x.truckDamage)}</span></div>`).join(''):'<div class="gat-game-empty">Nenhuma penalidade registrada. Continue assim.</div>'}</div>`;
    }
    const stats=document.getElementById('gat-statistics-body');if(stats){
      const longest=parsed.reduce((a,b)=>b.distance>(a?.distance||0)?b:a,null),avg=parsed.length?parsed.reduce((s,x)=>s+x.distance,0)/parsed.length:0;
      stats.innerHTML=`<div class="gat-game-grid"><article class="gat-game-card blue"><small>KM TOTAL</small><b>${km2(p.total_km)}</b></article><article class="gat-game-card"><small>MÉDIA POR ENTREGA</small><b>${km2(avg)}</b></article><article class="gat-game-card"><small>MAIOR VIAGEM</small><b>${longest?km2(longest.distance):'—'}</b></article><article class="gat-game-card gold"><small>VIAGENS PERFEITAS</small><b>${fmt(st.perfect_trips)}</b></article><article class="gat-game-card good"><small>KM SEM MULTA</small><b>${km2(st.no_fine_km)}</b></article></div><div class="gat-game-grid"><article class="gat-game-card gold"><small>PONTOS GAT DO MÊS</small><b>${fmt(p.points)}</b></article><article class="gat-game-card"><small>ENTREGAS</small><b>${fmt(p.total_deliveries)}</b></article><article class="gat-game-card"><small>XP TOTAL</small><b>${fmt(p.xp)}</b></article><article class="gat-game-card bad"><small>PONTOS PERDIDOS</small><b>-${fmt(lost)}</b></article><article class="gat-game-card gold"><small>TAXA PERFEITA</small><b>${pct(n(st.deliveries)?n(st.perfect_trips)/n(st.deliveries)*100:0)}</b></article></div>`;
    }
    const ach=document.getElementById('gat-achievements-body');if(ach){const eventRows=eventAchievements(p),eventKeys=new Set(eventRows.map(a=>normEvent(a.title))),isEventCopy=a=>eventRows.some(e=>{const title=normEvent(a.title||a.name||a.label||a.event_title||''),id=normEvent(a.id||''),eventId=normEvent(a.eventId||'');return (title&&title===normEvent(e.title))||(eventId&&eventId===normEvent(e.eventId))||id===normEvent(e.id)||id===normEvent('event_'+e.eventId)||(a.kind==='event'&&title&&eventKeys.has(title))}),calculated=[...achievementsFallback(p,st),...eventRows],saved=(Array.isArray(p.achievements)?p.achievements:[]).filter(a=>!isEventCopy(a)),savedById=new Map(saved.map(a=>[a.id||a.title,a])),list=calculated.map(a=>savedById.has(a.id)?(a.kind==='event'?{...savedById.get(a.id),...a}:{...a,...savedById.get(a.id)}):a),known=new Set(list.map(a=>a.id||a.title));saved.forEach(a=>{const id=a.id||a.title;if(id&&!known.has(id)){list.push(a);known.add(id)}});const html='<div class="gat-achievements">'+list.map(a=>{const isEvent=a.kind==='event',isHalloween=isEvent&&String(a.eventId||'').includes('halloween');const kind=isHalloween?' halloween-achievement':'';const medal=a.unlocked?(a.medal||'🏆'):'🔒';const goal=Math.max(1,n(a.goal)||1);const pctEvent=isEvent?Math.min(100,Math.round((n(a.progress)/goal)*100)):null;const date=isEvent&&a.unlocked&&a.completedAt?'<span class="gat-achievement-date">Concluída em '+new Date(a.completedAt).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo'})+'</span>':'';const progress=isEvent?'<div class="gat-achievement-progress"><i style="width:'+pctEvent+'%"></i></div>':'';const meta=(a.difficulty||a.xpReward)?`<div class="gat-achievement-meta">${a.difficulty?`<span>${esc2(a.difficulty)}</span>`:''}${a.xpReward?`<span class="xp">+${fmt(a.xpReward)} XP</span>`:''}</div>`:'';return `<article class="gat-achievement ${a.unlocked?'unlocked':''}${kind}"><div class="medal">${medal}</div><b>${esc2(a.title||'Conquista')}</b><small>${esc2(a.description||'')}</small>${meta}${progress}${date}</article>`}).join('')+'</div>';const sig=list.map(a=>[a.id||a.title,a.description,!!a.unlocked,a.progress||0,a.goal||0,a.completedAt||''].join('|')).join('||');if(ach.dataset.gatAchievementSig!==sig){ach.innerHTML=html;ach.dataset.gatAchievementSig=sig}}
  }

  injectStyle();ensureTabs();
  if(typeof renderProfile==='function'){
    const base=renderProfile;renderProfile=function(){const r=base.apply(this,arguments);setTimeout(render,0);return r};
  }
  setTimeout(render,300);setInterval(render,5000);
})();
