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
  const reason=mission?.rank_guard?.reason||(!mission?.rank_guard&&mission?.last_rejected_reason)||t?.rank_status?.reason;

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

// MODO DE TESTE TEMPORARIO: somente a conta @biduzao pode trocar livremente
// o Trabalho GAT atual para testar entregas em sequencia. Outros perfis nao mudam.
(()=>{
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const session=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const pageUser=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}const u=new URLSearchParams(location.search).get('u');return clean(u||session()?.user)};
  const enabled=()=>{const s=session();return !!s?.token&&clean(s.user)==='biduzao'&&pageUser()==='biduzao'};
  async function switchWork(card,button){
    const s=session();if(!enabled()||!s?.token)return;
    let custom='';
    if(card.classList.contains('custom-card')){custom=(prompt('CARGA PERSONALIZADA DE TESTE\n\nDigite o nome da carga como aparece no ETS2.')||'').trim();if(custom.length<2)return}
    const status=document.getElementById('workCatalogStatus');
    if(status)status.textContent='Modo teste: trocando trabalho...';
    button.disabled=true;
    try{
      const r=await fetch('https://api.gatlogets2.com.br/api/site/work/select',{method:'POST',cache:'no-store',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:s.token,work_id:card.dataset.workId,custom_cargo:custom})});
      const data=await r.json().catch(()=>null);
      if(r.ok&&data?.ok){if(status)status.textContent='Trabalho trocado. Recarregando...';setTimeout(()=>location.reload(),250);return}
      if(status)status.textContent='Não foi possível trocar: '+(data?.error||('HTTP '+r.status));
    }catch(_){if(status)status.textContent='Central GAT não respondeu ao trocar o trabalho.'}
    button.disabled=false;
  }
  function unlock(){
    if(!enabled())return;
    const msg=document.getElementById('workOwnerMessage');if(msg)msg.textContent='MODO TESTE ADMIN • Escolha qualquer trabalho. Você pode trocar de trabalho sem concluir o atual.';
    const km=document.getElementById('workMinKm');if(km)km.textContent='0 km • TESTE ADMIN';
    document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(card=>{
      if(card.classList.contains('selected'))return;
      card.classList.remove('locked');
      const b=card.querySelector('.cargo-select');if(!b)return;
      b.disabled=false;b.textContent='TESTAR TRABALHO';
      b.onclick=e=>{e.preventDefault();e.stopPropagation();switchWork(card,b)};
    });
  }
  document.addEventListener('DOMContentLoaded',unlock);
  window.addEventListener('gat-account-change',()=>setTimeout(unlock,100));
  setInterval(unlock,400);
})();

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

  document.addEventListener('DOMContentLoaded',()=>{bindSearch();localizeVisible()});
  setInterval(()=>{bindSearch();localizeVisible()},350);
})();
