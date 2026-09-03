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
.gat-admin-driving-summary{margin:14px 0}.gat-admin-driving-summary h3{margin:4px 0 12px;font-size:18px}.gat-admin-driving-grid{display:grid;grid-template-columns:repeat(6,1fr);gap:8px}.gat-admin-driving-grid article{border:1px solid #1d3044;border-radius:13px;background:#0b141d;padding:12px}.gat-admin-driving-grid small{display:block;color:#71849a;font-size:8px;font-weight:950}.gat-admin-driving-grid b{display:block;margin-top:6px;font-size:17px}.gat-admin-driving-grid .bad b{color:#ff91a7}.gat-admin-driving-grid .good b{color:#60dcb1}.gat-admin-driving-grid .blue b{color:#75bdff}.gat-admin-driving-grid .gold b{color:#ffd46b}.gat-admin-xp-breakdown{display:flex;flex-wrap:wrap;gap:5px;margin-top:5px}.gat-admin-xp-chip{border:1px solid #2a3d52;border-radius:7px;padding:4px 6px;font-size:7px;font-weight:900;color:#9eb1c5;background:#0e1822;white-space:nowrap}.gat-admin-xp-chip.bad{border-color:#5a2b36;background:#1b1117;color:#ff91a7}.gat-admin-xp-chip.good{border-color:#205a49;background:#0c211b;color:#70dbb5}.gat-admin-xp-chip.gold{border-color:#6b5520;background:#211b0c;color:#ffd46b}.gat-admin-xp-final{font-size:12px;font-weight:950;color:#7fc4ff;white-space:nowrap}.gat-admin-adjust-xp{border:1px solid #3978ad!important;background:#10263a!important;color:#89c8ff!important}.gat-admin-adjust-xp:hover{background:#16344f!important}.gat-admin-audit-note{display:block;margin-top:4px;color:#778ba1;font-size:7px;white-space:normal}.gat-admin-gat-score{display:block;font-size:12px;font-weight:950;color:#ff91a7;white-space:nowrap}.gat-admin-gat-score.ok{color:#68ddb1}.gat-admin-gat-suggested{display:inline-block;margin-top:5px;padding:4px 6px;border:1px solid #82671f;border-radius:7px;background:#251e0c;color:#ffd46b;font-size:8px;font-weight:950;white-space:nowrap}.gat-admin-gat-reviewed{display:inline-block;margin-top:5px;padding:4px 6px;border:1px solid #215b48;border-radius:7px;background:#0c211a;color:#71ddb7;font-size:8px;font-weight:950}.gat-admin-gat-kept{display:inline-block;margin-top:5px;padding:4px 6px;border:1px solid #5a2b36;border-radius:7px;background:#1b1117;color:#ff91a7;font-size:8px;font-weight:950}.gat-admin-gat-approve{border:1px solid #257053!important;background:#0d2b21!important;color:#71ddb7!important}.gat-admin-gat-keep{border:1px solid #6a3742!important;background:#251318!important;color:#ff9aac!important}.gat-admin-review-actions{display:flex;flex-wrap:wrap;gap:5px;margin-bottom:6px}.detail-history-table.audit-mode{min-width:1540px}.detail-history-table.audit-mode th,.detail-history-table.audit-mode td{vertical-align:top}@media(max-width:900px){.gat-admin-driving-grid{grid-template-columns:1fr 1fr}}`;
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

  function rankReasonText(v){
    const m={damage_data_incomplete:'Dados de dano incompletos',telemetry_not_verified_from_start:'Telemetria não confirmada desde o início',telemetry_gap:'Interrupção da telemetria',telemetry_resume_pending:'Retomada ainda não validada',telemetry_disconnected:'Telemetria desconectada',trip_progress_unverified:'Progresso não confirmado',client_update_required:'GAT Telemetria desatualizado',mission_not_active:'Início da viagem não validado',distance_below_minimum:'Distância fora do requisito'};
    return m[String(v||'')]||String(v||'Falha na validação automática');
  }

  function gatInfo(d){
    const current=Math.max(0,Math.min(100,Math.round(num(d?.gat_points))));
    const status=String(d?.gat_review_status||d?.gat_manual_review?.status||'');
    const actor=String(d?.gat_review_actor||d?.gat_manual_review?.actor||'');
    const at=d?.gat_review_at||d?.gat_manual_review?.at||'';
    const serverSuggested=Number(d?.gat_review_suggested_points),hasServer=Number.isFinite(serverSuggested);
    const fields=['gat_base_points','gat_speed_penalty_points','gat_cargo_penalty_points','gat_truck_penalty_points'];
    const hasLocal=fields.every(k=>d&&Object.prototype.hasOwnProperty.call(d,k)&&Number.isFinite(Number(d[k])));
    const local=hasLocal?Math.max(0,Math.min(100,Math.round(Number(d.gat_base_points))-Math.max(0,Math.round(Number(d.gat_speed_penalty_points)))-Math.max(0,Math.round(Number(d.gat_cargo_penalty_points)))-Math.max(0,Math.round(Number(d.gat_truck_penalty_points))))):null;
    const suggested=hasServer?Math.max(0,Math.min(100,Math.round(serverSuggested))):local;
    const reason=String(d?.automatic_ranking_reason||d?.ranking_reason||'');
    const reviewable=d?.gat_reviewable===true||(current===0&&!!reason&&suggested!==null&&!status);
    return{current,status,actor,at,suggested,reason,reviewable};
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
    if(table){table.classList.add('audit-mode');const head=table.querySelector('thead tr');if(head)head.innerHTML='<th>DATA</th><th>ROTA / CARGA</th><th>KM / PESO</th><th>XP BASE</th><th>VELOCIDADE</th><th>DANOS</th><th>XP FINAL</th><th>PONTOS GAT</th><th>MISSÃO</th><th>AÇÕES</th>';}
    const count=document.getElementById('detailHistoryCount');if(count)count.textContent=list.length+' MOSTRADAS';rows.textContent='';
    if(!list.length){rows.innerHTML='<tr><td colspan="10"><div class="detail-empty">Nenhuma entrega GAT registrada.</div></td></tr>';renderSummary([]);return;}
    list.forEach(d=>{
      const x=parse(d),g=gatInfo(d),id=String(d?.id||d?.receipt_id||''),route=(d?.source||'—')+' → '+(d?.destination||'—'),canEdit=(typeof viewerRole==='undefined'||viewerRole!=='moderator'),canReview=(typeof viewerRole==='undefined'||['owner','admin','moderator'].includes(String(viewerRole))),manual=x.final!==x.expected;
      let gatHtml='<span class="gat-admin-gat-score '+(g.current>0?'ok':'')+'">'+fmt(g.current)+'/100</span>';
      if(g.status==='approved')gatHtml+='<span class="gat-admin-gat-reviewed">VALIDADO PELA MODERAÇÃO</span><small class="gat-admin-audit-note">@'+safe(g.actor)+(g.at?' • '+safe(date(g.at)):'')+'</small>';
      else if(g.status==='kept_zero')gatHtml+='<span class="gat-admin-gat-kept">REVISADO • MANTIDO 0</span><small class="gat-admin-audit-note">@'+safe(g.actor)+(g.at?' • '+safe(date(g.at)):'')+'</small>';
      else if(g.reviewable&&g.suggested!==null)gatHtml+='<span class="gat-admin-gat-suggested">PONTUAÇÃO SUGERIDA: '+fmt(g.suggested)+'/100</span><small class="gat-admin-audit-note">Automático: '+safe(rankReasonText(g.reason))+'</small>';
      else if(g.current===0&&g.reason)gatHtml+='<small class="gat-admin-audit-note">'+safe(rankReasonText(g.reason))+'</small>';
      let actions='';
      if(canReview&&id&&g.reviewable&&g.suggested!==null){actions+='<div class="gat-admin-review-actions">'+(g.suggested>0?'<button class="gat-admin-gat-approve" data-gat-review="approve" data-gat-delivery="'+safe(id)+'" data-gat-user="'+safe(user)+'" data-gat-suggested="'+g.suggested+'">CONFIRMAR '+g.suggested+'</button>':'')+'<button class="gat-admin-gat-keep" data-gat-review="keep_zero" data-gat-delivery="'+safe(id)+'" data-gat-user="'+safe(user)+'" data-gat-suggested="'+g.suggested+'">MANTER 0</button></div>';}
      if(canEdit&&id)actions+='<button class="gat-admin-adjust-xp" data-adjust-xp="'+safe(id)+'" data-user="'+safe(user)+'" data-current="'+x.final+'">AJUSTAR XP</button> <button data-delete-delivery="'+safe(id)+'" data-delivery-user="'+safe(user)+'">EXCLUIR</button>';
      if(!actions)actions='—';
      const tr=document.createElement('tr');
      tr.innerHTML=`<td>${safe(date(d?.completed_at))}</td><td class="route-cell"><b>${safe(route)}</b><small class="gat-admin-audit-note">${safe(d?.cargo||'Carga')}</small></td><td>${safe(km(d?.distance_km))}<small class="gat-admin-audit-note">${safe(weight(d?.weight_kg))}</small></td><td><b>${fmt(x.base)} XP</b>${x.bonus?'<div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip gold">PERFEITA +'+fmt(x.bonus)+' XP</span></div>':''}</td><td><div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip ${x.speed?'bad':''}">${x.fines} multa${x.fines===1?'':'s'}</span><span class="gat-admin-xp-chip ${x.speed?'bad':''}">${x.speed?'-'+fmt(x.speed):'0'} XP</span></div></td><td><div class="gat-admin-xp-breakdown"><span class="gat-admin-xp-chip ${x.cargo?'bad':''}">Carga ${pct(x.cargoDamage)} ${x.cargo?'-'+fmt(x.cargo)+' XP':''}</span><span class="gat-admin-xp-chip ${x.truck?'bad':''}">Caminhão +${pct(x.truckDamage)} ${x.truck?'-'+fmt(x.truck)+' XP':''}</span></div></td><td><span class="gat-admin-xp-final">${fmt(x.final)} XP</span>${x.perfect?'<small class="gat-admin-audit-note" style="color:#ffd46b">VIAGEM PERFEITA</small>':''}${manual?'<small class="gat-admin-audit-note">ajuste manual</small>':''}</td><td>${gatHtml}</td><td>#${safe(d?.sequence||'—')}</td><td>${actions}</td>`;
      rows.appendChild(tr);
    });
    rows.querySelectorAll('[data-gat-review]').forEach(b=>b.onclick=()=>reviewGatPoints(b.dataset.gatUser,b.dataset.gatDelivery,b.dataset.gatReview,Number(b.dataset.gatSuggested)||0));
    rows.querySelectorAll('[data-adjust-xp]').forEach(b=>b.onclick=()=>adjustXp(b.dataset.user,b.dataset.adjustXp,Number(b.dataset.current)||0));
    rows.querySelectorAll('[data-delete-delivery]').forEach(b=>b.onclick=()=>typeof deleteDelivery==='function'&&deleteDelivery(b.dataset.deliveryUser,b.dataset.deleteDelivery));
    renderSummary(history);
  };

  async function reviewGatPoints(user,id,decision,suggested){
    const approve=decision==='approve',text=approve?'CONFIRMAR '+suggested+'/100 PONTOS GAT?':'MANTER ESTA ENTREGA COM 0/100 PONTOS GAT?';
    if(!confirm(text+'\n\nMotorista: @'+user+'\n\nA pontuação sugerida é calculada pela Central usando as penalidades que ficaram salvas na viagem. A revisão ficará registrada com seu usuário, data, pontuação anterior e decisão.'))return;
    if(typeof setDetailStatus==='function')setDetailStatus(approve?'Validando Pontos GAT...':'Registrando revisão...');
    try{
      const res=await adminPost('/api/site/admin/action',{action:'review_gat_points',target:user,delivery_id:id,review_decision:decision});
      if(!res.r.ok||!res.data?.ok){const code=res.data?.error||'HTTP '+res.r.status,msg=code==='gat_review_already_done'?'Esta entrega já foi revisada.':code==='gat_review_no_saved_breakdown'?'A Central não possui dados suficientes para sugerir uma pontuação segura nesta entrega.':'Revisão recusada: '+code+'.';if(typeof setDetailStatus==='function')setDetailStatus(msg,'err');return;}
      const approved=Math.max(0,num(res.data.approved_points));
      if(typeof setDetailStatus==='function')setDetailStatus(approve?'Pontos GAT validados: '+approved+'/100.':'Revisão registrada: pontuação mantida em 0/100.','ok');
      if(typeof loadDrivers==='function')await loadDrivers();if(typeof loadDriverDetail==='function')await loadDriverDetail(false);
    }catch(_){if(typeof setDetailStatus==='function')setDetailStatus('Falha de comunicação ao revisar Pontos GAT.','err')}
  }

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
