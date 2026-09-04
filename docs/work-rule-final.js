(()=>{
  const CURRENT_COLLECTION_TEXT='Cada tipo de carga concluído entra uma vez na coleção. Repetir uma carga continua valendo normalmente no histórico, km, XP e Pontos GAT, mas não aumenta a coleção novamente.';

  const resetCargoSearchIfNeeded=()=>{
    const input=document.getElementById('fullCargoSearch');
    if(!input)return;

    input.setAttribute('autocomplete','off');
    input.setAttribute('name','gat-cargo-search');
    input.setAttribute('data-lpignore','true');
    input.setAttribute('data-1p-ignore','true');

    const account=String(document.getElementById('driverAccount')?.textContent||'').replace(/^@/,'').trim().toLowerCase();
    const loginUser=String(document.getElementById('loginUser')?.value||'').trim().toLowerCase();
    const value=String(input.value||'').trim().toLowerCase();
    const isDriverName=!!value&&((account&&value===account)||(loginUser&&value===loginUser));

    if(!input.dataset.gatCargoSearchReady||isDriverName){
      input.dataset.gatCargoSearchReady='1';
      if(input.value){
        input.value='';
        input.dispatchEvent(new Event('input',{bubbles:true}));
      }
    }
  };

  const apply=()=>{
    const min=document.getElementById('workMinKm');
    if(min&&min.textContent.trim()!=='Sem distância mínima') min.textContent='Sem distância mínima';

    const owner=document.getElementById('workOwnerMessage');
    if(owner&&/30 viagens|meta mensal|500 km/i.test(owner.textContent||'')) owner.textContent='A carga é reconhecida automaticamente pela telemetria. Cada entrega concluída fica registrada no histórico.';

    const rule=document.querySelector('.work-catalog-section .catalog-rule');
    if(rule){
      const wanted='<b>COLEÇÃO</b><span>DE CARGAS</span><b>SEM MÍN.</b><span>KM REAIS</span>';
      if(rule.innerHTML!==wanted) rule.innerHTML=wanted;
    }

    const progressTitle=document.querySelector('.monthly-progress-card h2');
    if(progressTitle&&progressTitle.textContent.trim()!=='Coleção de cargas') progressTitle.textContent='Coleção de cargas';

    const progressLead=document.querySelector('.monthly-progress-card .lead');
    if(progressLead&&progressLead.textContent.trim()!==CURRENT_COLLECTION_TEXT) progressLead.textContent=CURRENT_COLLECTION_TEXT;

    resetCargoSearchIfNeeded();
  };

  const start=()=>{
    apply();
    const root=document.getElementById('driverContent')||document.body;
    const observer=new MutationObserver(apply);
    observer.observe(root,{subtree:true,childList:true,characterData:true});
    setInterval(apply,1500);
  };

  window.addEventListener('gat-account-change',()=>{
    setTimeout(apply,0);
    setTimeout(apply,250);
    setTimeout(apply,800);
  });

  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',start,{once:true});
  else start();
})();
