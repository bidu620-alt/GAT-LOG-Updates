function renderRankEligibility(t){
  let box=q('rankingTelemetryStatus');
  if(!box){
    box=document.createElement('div');
    box.id='rankingTelemetryStatus';
    box.setAttribute('role','status');
    box.style.cssText='margin:14px 0;padding:14px 18px;border:1px solid #8c6728;border-radius:12px;background:#251d10;color:#ffe2a0;line-height:1.5;';
    document.querySelector('.driver-hero')?.insertAdjacentElement('afterend',box);
  }

  if(centralServicePause){
    box.hidden=false;
    box.textContent=centralServicePause.message+(centralServicePause.resumes_at?' Renovação: '+new Date(centralServicePause.resumes_at).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo'})+' (Brasília).':'');
    return;
  }

  const mission=profile?.current_mission;
  const rejectedAt=Date.parse(mission?.last_rejected_at||'');
  const startedAt=Date.parse(mission?.started_at||'');
  // Um motivo de rejeição pertence apenas à tentativa em que foi gravado.
  // Ao iniciar outra viagem, started_at fica mais novo e o aviso antigo não pode
  // continuar aparecendo como se a viagem atual tivesse falhado.
  const staleRejected=Number.isFinite(rejectedAt)&&Number.isFinite(startedAt)&&startedAt>rejectedAt;
  const priorRejected=!staleRejected?mission?.last_rejected_reason:null;
  const reason=mission?.rank_guard?.reason||(!mission?.rank_guard&&priorRejected)||t?.rank_status?.reason;

  // O aviso antigo de TruckSim GPS confundia motoristas mesmo com a telemetria funcionando.
  // A validação do ranking continua sendo feita pela Central GAT; apenas o aviso visual é ocultado.
  if(reason==='damage_data_incomplete'){
    box.hidden=true;
    box.textContent='';
    return;
  }

  const messages={
    client_update_required:'Ranking bloqueado: atualize o GAT Telemetria para a versão atual.',
    telemetry_disconnected:'Ranking indisponível: o jogo não está conectado à telemetria.',
    telemetry_gap:'Esta viagem não pontua: a telemetria ficou interrompida por mais de 2 minutos.',
    telemetry_not_verified_from_start:'Esta viagem não pontua: os dados da viagem não foram verificados desde o início.'
  };

  const text=messages[reason];
  box.hidden=!text;
  if(text){
    box.textContent=text;
    if(mission?.rank_guard?.reason)box.textContent+=' Depois de corrigir, faça uma nova viagem; esta tentativa permanece sem pontuação.';
  }else{
    box.textContent='';
  }
}

// Regra atual do ranking: sem distância mínima. A Central continua exigindo
// progresso real da viagem e as demais validações de telemetria/danos.
function applyNoMinimumKmVisual(){
  const min=document.getElementById('workMinKm');
  if(min)min.textContent='Sem mínimo';
  const rule=document.querySelector('.work-catalog-section .catalog-rule');
  if(rule){
    const bold=rule.querySelectorAll('b'),labels=rule.querySelectorAll('span');
    if(bold[1])bold[1].textContent='SEM MÍNIMO';
    if(labels[1])labels[1].textContent='KM PARA O RANK';
  }
}

// Compatibilidade visual PT-BR do catálogo de cargas.
// Ex.: a SCS usa "LPG" no catálogo; o ETS2 em português mostra "GLP".
// Isso altera somente pesquisa/exibição do site e não toca Telemetria/Central.
(()=>{
  const aliases={
    glp:'LPG',
    'gas liquefeito de petroleo':'LPG',
    'gas liquefeito':'LPG'
  };
  let busy=false;
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();

  function localizeVisible(){
    document.querySelectorAll('.full-cargo-card h3').forEach(el=>{
      if(norm(el.textContent)==='lpg')el.textContent='GLP';
    });
    const live=document.querySelector('#fullCargoLiveBox .full-cargo-live-name');
    if(live&&norm(live.textContent)==='lpg')live.textContent='GLP';
  }

  function bindSearch(){
    const input=document.getElementById('fullCargoSearch');
    if(!input||input.dataset.gatPtAliases==='1')return;
    input.dataset.gatPtAliases='1';
    input.addEventListener('input',()=>{
      if(busy)return;
      const original=input.value;
      const target=aliases[norm(original)];
      if(!target)return;
      busy=true;
      input.value=target;
      input.dispatchEvent(new Event('input',{bubbles:true}));
      input.value=original;
      busy=false;
      setTimeout(localizeVisible,0);
    });
  }

  document.addEventListener('DOMContentLoaded',()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual()});
  setInterval(()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual()},350);
})();
