(()=>{
  if(typeof renderDriverDetail!=='function'||typeof renderDetailHistory!=='function'||typeof adminPost!=='function')return;
  const num=v=>Number(v)||0;
  const fmt=v=>num(v).toLocaleString('pt-BR');
  const pct=v=>num(v).toLocaleString('pt-BR',{maximumFractionDigits:1})+'%';
  const safe=v=>typeof esc==='function'?esc(v):String(v??'');
  const date=v=>typeof fmtDate==='function'?fmtDate(v):String(v||'—');
  const km=v=>typeof fmtKm1==='function'?fmtKm1(v):Math.round(num(v))+' km';
  const weight=v=>typeof fmtWeight==='function'?fmtWeight(v):'—';

  function injectStyle(){
    if(document.getElementById('gatAdminDrivingAuditStyle'))return;
    const s=document.createElement('style');s.id='gatAdminDrivingAuditStyle';s.textContent=`
.gat-admin-driving-summary{margin:14px 0}.gat-admin-driving-summary h3{margin:4px 0 12px;font-size:18px}.gat-admin-driving-grid{display:grid;grid-template-columns:repeat(6,1fr);gap:8px}.gat-admin-driving-grid article{border:1px solid #1d3044;border-radius:13px;background:#0b141d;padding:12px}.gat-admin-driving-grid small{display:block;color:#71849a;font-size:8px;font-weight:950}.gat-admin-driving-grid b{display:block;margin-top:6px;font-size:17px}.gat-admin-driving-grid .bad b{color:#ff91a7}.gat-admin-driving-grid .good b{color:#60dcb1}.gat-admin-driving-grid .blue b{color:#75bdff}.gat-admin-driving-grid .gold b{color:#ffd46b}.gat-admin-xp-breakdown{display:flex;flex-wrap:wrap;gap:5px;margin-top:5px}.gat-admin-xp-chip{border:1px solid #2a3d52;border-radius:7px;padding:4px 6px;font-size:7px;font-weight:900;color:#9eb1c5;background:#0e1822;white-space:nowrap}.gat-admin-xp-chip.bad{border-color:#5a2b36;background:#1b1117;color:#ff91a7}.gat-admin-xp-chip.good{border-color:#205a49;background:#0c211b;color:#70dbb5}.gat-admin-xp-chip.gold{border-color:#6b5520;background:#211b0c;color:#ffd46b}.gat-admin-xp-final{font-size:12px;font-weight:950;color:#7fc4ff;white-space:nowrap}.gat-admin-adjust-xp{border:1px solid #3978ad!important;background:#10263a!important;color:#89c8ff!important}.gat-admin-adjust-xp:hover{background:#16344f!important}.gat-admin-audit-note{display:block;margin-top:4px;color:#778ba1;font-size:7px;white-space:nowrap}.detail-history-table.audit-mode{min-width:1320px}.detail-history-table.audit-mode th,.detail-history-table.audit-mode td{vertical-align:top}@media(max-width:900px){.gat-admin-driving-grid{grid-template-columns:1fr 1fr}}`;
    document.head.appendChild(s);
  }

  function ensureSummary(){
    let sec=document.getElementById('gatAdminDrivingSummary');if(sec)return sec;
    const stat=document.querySelector('.detail-stat-grid');if(!stat)return null;
    sec=document.createElement('section');sec.id='gatAdminDrivingSummary';sec.className='gat-admin-driving-summary';
    sec.innerHTML='<span class="eyebrow">AUDITORIA DE CONDUÇÃO</span><h3>Infrações, bônus e XP</h3><div id="gatAdminDrivingGrid" class="gat-admin-driving-grid"></div>';
    stat.insertAdjacentElement('afterend',sec);return sec;
  }

  function parse(d){
    const distance=Math.max(0,num(d?.distance_km)),calcBase=Math.floor(distance/100)*20;
    const base=Math.max(0,num(d?.base_xp)||calcBase),fines=Math.max(0,Math.round(num(d?.speed_fines)));
    const speed=Math.max(0,num(d?.speed_penalty_xp)||fines*3),cargo=Math.max(0,num(d?.cargo_penalty_xp)),truck=Math.max(0,num(d?.truck_penalty_xp));
    const penalty=Math.max(0,num(d?.penalty_xp)||speed+cargo+truck),bonus=Math.max(0,num(d?.perfect_bonus_xp)),expected=Math.max(0,base-penalty+bonus);
    const final=(d&&Object.prototype.hasOwnProperty.call(d,'xp_awarded')&&Number.isFinite(Number(d.xp_awarded)))?Math.max(0,Number(d.xp_awarded)):expected;
    return {distance,base,fines,speed,cargo,truck,penalty,bonus,expected,final,perfect:!!d?.perfect_trip||bonus>0,cargoDamage:Math.max(0,num(d?.cargo_damage_pct)),truckDamage:Math.max(0,num(d?.truck_damage_delta_pct))};
  }

  function renderSummary(history){
    ensureSummary();const root=document.getElementById('gatAdminDrivingGrid');if(!root)return;
    const list=(Array.isArray(history)?history:[]).map(parse),fines=list.reduce((s,x)=>s+x.fines,0),lost=list.reduce((s,x)=>s+x.penalty,0),clean=list.filter(x=>x.penalty===0).length,perfect=list.filter(x=>x.perfect).length;
    const cd=list.filter(x=>x.cargoDamage>0),td=list.filter(x=>x.truckDamage>0),avgC=cd.length?cd.reduce((s,x)=>s+x.cargoDamage,0)/cd.length:0,avgT=td.length?td.reduce((s,x)=>s+x.truckDamage,0)/td.length:0;
    root.innerHTML=`<article class="bad"><small>MULTAS</small><b>${fmt(fines)}</b></article><article class="bad"><small>XP PERDIDO</small><b>-${fmt(lost)}</b></article><article class="good"><small>VIAGENS LIMPAS</small><b>${fmt(clean)}</b></article><article class="gold"><small>VIAGENS PERFEITAS</small><b>${fmt(perfect)}</b></article><article><small>DANO MÉDIO CARGA</small><b>${pct(avgC)}</b></article><article class="blue"><small>DANO MÉDIO CAMINHÃO</small><b>${pct(avgT)}</b></article>`;
  }

  const baseRenderDriverDetail=renderDriverDetail;
  renderDriverDetail=function(data){const r=baseRenderDriverDetail.apply(this,arguments);try{renderSummary(data?.profile?.deliveries||[])}catch(_){}return r};

  renderDetailHistory=function(history,user){
    const rows=document.getElementById('detailHistoryRows'),table=rows?.closest('table'),list=[...(Array.isArray(history)?history:[])].reverse();if(!rows)return;
    if(table){table.classList.add('audit-mode');const head=table.querySelector('thead tr');if(head)head.innerHTML='<th>DATA</th><th>ROTA / CARGA</th><th>KM / PESO</th><th>XP BASE</th><th>VELOCIDADE</th><th>DANOS</th><th>XP FINAL</th><th>MISSÃO</th><th>AÇÕES</th>';}
    const count=document.getElementById('detailHistoryCount');if(count)count.textContent=list.length+' MOSTRADAS';rows.textContent='';
    if(!list.length){rows.innerHTML='<tr><td colspan="9"><div class="detail-empty">Nenhuma entrega GAT registrada.</div></td></tr>';renderSummary([]);return;}
    list.forEach(d=>{
      const x=parse(d),id=String(d?.id||d?.receipt_id||''),route=(d?.source||'—')+' → '+(d?.destination||'—'),canEdit=(typeof viewerRole==='undefined'||viewerRole!=='moderator'),manual=x.final!==x.expected;
      const tr=document.createElement('tr');
      tr.innerHTML=`<td>${safe(date(d?.completed_at))}</td><td class="route-cell"><b>${safe(route)}</b><small class="gat-admin-audit-note">${safe(d?.cargo||'Carga')}</small></td><td>${safe(km(d?.distance_km))}<small class="gat-admin-audit-note">${safe(weight(d?.weight_kg))}</small></td><td><b>${fmt(x.base)} XP</b>${x.bonus?'<div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip gold">PERFEITA +'+fmt(x.bonus)+' XP</span></div>':''}</td><td><div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip ${x.speed?'bad':''}">${x.fines} multa${x.fines===1?'':'s'}</span><span class="gat-admin-xp-chip ${x.speed?'bad':''}">${x.speed?'-'+fmt(x.speed):'0'} XP</span></div></td><td><div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip ${x.cargo?'bad':''}">Carga ${pct(x.cargoDamage)} ${x.cargo?'-'+fmt(x.cargo)+' XP':''}</span><span class="gat-admin-xp-chip ${x.truck?'bad':''}">Caminhão +${pct(x.truckDamage)} ${x.truck?'-'+fmt(x.truck)+' XP':''}</span></div></td><td><span class="gat-admin-xp-final">${fmt(x.final)} XP</span>${x.perfect?'<small class="gat-admin-audit-note" style="color:#ffd46b">VIAGEM PERFEITA</small>':''}${manual?'<small class="gat-admin-audit-note">ajuste manual</small>':''}</td><td>#${safe(d?.sequence||'—')}</td><td>${canEdit&&id?'<button class="gat-admin-adjust-xp" data-adjust-xp="'+safe(id)+'" data-user="'+safe(user)+'" data-current="'+x.final+'">AJUSTAR XP</button> ':''}${canEdit&&id?'<button data-delete-delivery="'+safe(id)+'" data-delivery-user="'+safe(user)+'">EXCLUIR</button>':'—'}</td>`;
      rows.appendChild(tr);
    });
    rows.querySelectorAll('[data-adjust-xp]').forEach(b=>b.onclick=()=>adjustXp(b.dataset.user,b.dataset.adjustXp,Number(b.dataset.current)||0));
    rows.querySelectorAll('[data-delete-delivery]').forEach(b=>b.onclick=()=>typeof deleteDelivery==='function'&&deleteDelivery(b.dataset.deliveryUser,b.dataset.deleteDelivery));
    renderSummary(history);
  };

  async function adjustXp(user,id,current){
    const raw=prompt('Novo XP FINAL desta entrega de @'+user+'\n\nAtual: '+current+' XP\nDigite o novo valor (mínimo 0):',String(current));if(raw===null)return;
    const value=Math.trunc(Number(raw));if(!Number.isFinite(value)||value<0||value>100000){alert('Informe um XP válido entre 0 e 100.000.');return;}
    if(!confirm('Corrigir esta entrega para '+value+' XP?\n\nA alteração ficará registrada no log de auditoria.'))return;
    if(typeof setDetailStatus==='function')setDetailStatus('Corrigindo XP da entrega...');
    try{
      const res=await adminPost('/api/site/admin/action',{action:'set_delivery_xp',target:user,delivery_id:id,delivery_xp:value});
      if(!res.r.ok||!res.data?.ok){if(typeof setDetailStatus==='function')setDetailStatus('Correção recusada: '+(res.data?.error||'HTTP '+res.r.status)+'.','err');return;}
      if(typeof setDetailStatus==='function')setDetailStatus('XP corrigido e auditoria registrada.','ok');
      if(typeof loadDrivers==='function')await loadDrivers();if(typeof loadDriverDetail==='function')await loadDriverDetail(false);
    }catch(_){if(typeof setDetailStatus==='function')setDetailStatus('Falha de comunicação ao corrigir XP.','err')}
  }

  injectStyle();ensureSummary();
})();
