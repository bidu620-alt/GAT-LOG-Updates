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

// Mantem o ultimo perfil confirmado visivel enquanto uma nova leitura da Central
// esta em andamento. Isso evita o flash 0/30 e 0 registradas ao trocar de pagina,
// voltar para Motorista ou atualizar o navegador.
const GAT_PROFILE_CACHE_MAX_AGE=10*60*1000;
function gatProfileCacheUser(){
  try{return cleanUser((typeof key!=='undefined'&&key)||getSession()?.user||'')}catch(_){return ''}
}
function gatProfileCacheKey(){
  const user=gatProfileCacheUser();
  return user?'gat_driver_profile_visual_v1:'+new Date().toISOString().slice(0,7)+':'+user:'';
}
function saveGatProfileVisualCache(){
  try{
    const k=gatProfileCacheKey();
    if(!k||typeof profile==='undefined'||!profile||!profile.user||!Array.isArray(profile.deliveries))return;
    localStorage.setItem(k,JSON.stringify({saved_at:Date.now(),profile}));
  }catch(_){}
}
function restoreGatProfileVisualCache(force=false){
  try{
    const k=gatProfileCacheKey();if(!k)return false;
    const cached=JSON.parse(localStorage.getItem(k)||'null');
    if(!cached?.profile||Date.now()-Number(cached.saved_at||0)>GAT_PROFILE_CACHE_MAX_AGE)return false;
    const live=typeof profile!=='undefined'?profile:null,c=cached.profile;
    const liveEmpty=!live||(Number(live.total_deliveries)||0)===0&&(Number(live.monthly_completed)||0)===0&&(!Array.isArray(live.deliveries)||live.deliveries.length===0);
    const cachedHasData=(Number(c.total_deliveries)||0)>0||(Number(c.monthly_completed)||0)>0||(Array.isArray(c.deliveries)&&c.deliveries.length>0);
    if(force||(liveEmpty&&cachedHasData)){
      profile=c;
      if(typeof renderProfile==='function')renderProfile();
      return true;
    }
  }catch(_){}
  return false;
}

(()=>{
  const aliases={glp:'LPG','gas liquefeito de petroleo':'LPG','gas liquefeito':'LPG'};
  let busy=false,lastProfileSignature='';
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  function localizeVisible(){
    document.querySelectorAll('.full-cargo-card h3').forEach(el=>{if(norm(el.textContent)==='lpg')el.textContent='GLP'});
    const live=document.querySelector('#fullCargoLiveBox .full-cargo-live-name');if(live&&norm(live.textContent)==='lpg')live.textContent='GLP';
  }
  function bindSearch(){
    const input=document.getElementById('fullCargoSearch');if(!input||input.dataset.gatPtAliases==='1')return;
    input.dataset.gatPtAliases='1';input.addEventListener('input',()=>{if(busy)return;const original=input.value,target=aliases[norm(original)];if(!target)return;busy=true;input.value=target;input.dispatchEvent(new Event('input',{bubbles:true}));input.value=original;busy=false;setTimeout(localizeVisible,0)});
  }
  function keepProfileStable(){
    try{
      const p=typeof profile!=='undefined'?profile:null;
      if(p&&p.user&&Array.isArray(p.deliveries)){
        const sig=[p.user,p.monthly_completed,p.total_deliveries,p.total_km,p.xp,p.points,p.deliveries.length,p.current_mission?.trip_id||''].join('|');
        if(sig!==lastProfileSignature){
          const hasData=(Number(p.total_deliveries)||0)>0||(Number(p.monthly_completed)||0)>0||p.deliveries.length>0;
          if(hasData){lastProfileSignature=sig;saveGatProfileVisualCache()}
          else restoreGatProfileVisualCache(false);
        }
      }else restoreGatProfileVisualCache(false);
    }catch(_){}
  }
  // motorista.js inicia a consulta antes deste arquivo. Restauramos o ultimo perfil
  // imediatamente enquanto a resposta atual ainda esta a caminho.
  restoreGatProfileVisualCache(true);
  document.addEventListener('DOMContentLoaded',()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual();restoreGatProfileVisualCache(false)});
  window.addEventListener('pageshow',()=>restoreGatProfileVisualCache(false));
  document.addEventListener('visibilitychange',()=>{if(!document.hidden)restoreGatProfileVisualCache(false)});
  setInterval(()=>{bindSearch();localizeVisible();applyNoMinimumKmVisual();keepProfileStable()},250);
})();
