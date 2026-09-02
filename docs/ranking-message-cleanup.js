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
  const staleRejected=Number.isFinite(rejectedAt)&&Number.isFinite(startedAt)&&startedAt>rejectedAt;
  const currentReason=mission?.rank_guard?.reason||null;
  const previousReason=!currentReason&&!staleRejected&&mission?.state==='assigned'?mission?.last_rejected_reason:null;
  const liveReason=t?.rank_status?.reason||null;
  const reason=currentReason||liveReason||previousReason;

  if(reason==='damage_data_incomplete'){
    box.hidden=true;
    box.textContent='';
    return;
  }

  const currentMessages={
    client_update_required:'Ranking bloqueado: atualize o GAT Telemetria para a versão atual.',
    telemetry_disconnected:'Ranking indisponível: o jogo não está conectado à telemetria.',
    telemetry_gap:'Esta viagem não pontua: a telemetria ficou interrompida por mais de 2 minutos.',
    telemetry_not_verified_from_start:'Esta viagem não pontua: os dados da viagem não foram verificados desde o início.'
  };
  const previousMessages={
    telemetry_gap:'A última tentativa não pontuou porque a telemetria ficou interrompida por mais de 2 minutos.',
    telemetry_not_verified_from_start:'A última tentativa não pontuou porque a telemetria não foi confirmada desde o início. A Central 1.0.44 corrige isso para novas viagens.'
  };

  const text=previousReason&&reason===previousReason?(previousMessages[reason]||currentMessages[reason]):currentMessages[reason];
  box.hidden=!text;
  if(text){
    box.textContent=text;
    if(currentReason)box.textContent+=' Depois de corrigir, faça uma nova viagem; esta tentativa permanece sem pontuação.';
  }else{
    box.textContent='';
  }
}

function applyNoMinimumKmVisual(){
  const min=document.getElementById('workMinKm');
  if(min)min.textContent='Sem distância mínima';
  const rule=document.querySelector('.work-catalog-section .catalog-rule');
  if(rule)rule.innerHTML='<b>30</b><span>VIAGENS / MÊS</span><b>SEM MÍN.</b><span>KM REAIS</span>';
}

(()=>{
  const aliases={glp:'LPG','gas liquefeito de petroleo':'LPG','gas liquefeito':'LPG'};
  let busy=false;
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  function localizeVisible(){
    document.querySelectorAll('.full-cargo-card h3').forEach(el=>{if(norm(el.textContent)==='lpg')el.textContent='GLP'});
    const live=document.querySelector('#fullCargoLiveBox .full-cargo-live-name');if(live&&norm(live.textContent)==='lpg')live.textContent='GLP';
  }
  function bindSearch(){
    const input=document.getElementById('fullCargoSearch');if(!input||input.dataset.gatPtAliases==='1')return;
    input.dataset.gatPtAliases='1';input.addEventListener('input',()=>{if(busy)return;const original=input.value,target=aliases[norm(original)];if(!target)return;busy=true;input.value=target;input.dispatchEvent(new Event('input',{bubbles:true}));input.value=original;busy=false;setTimeout(localizeVisible,0)});
  }
  document.addEventListener('DOMContentLoaded',()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual()});
  setInterval(()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual()},350);
})();
