(()=>{
  const CURRENT_COLLECTION_TEXT='Cada tipo de carga concluído entra uma vez na coleção. Repetir uma carga continua valendo normalmente no histórico, km, XP e Pontos GAT, mas não aumenta a coleção novamente.';
  const apply=()=>{
    const min=document.getElementById('workMinKm');
    if(min&&min.textContent.trim()!=='Sem distância mínima') min.textContent='Sem distância mínima';

    const owner=document.getElementById('workOwnerMessage');
    if(owner&&/30 viagens|meta mensal|500 km/i.test(owner.textContent||'')) owner.textContent='A carga é reconhecida automaticamente pela telemetria.';

    const rule=document.querySelector('.work-catalog-section .catalog-rule');
    if(rule){
      const wanted='<b>COLEÇÃO</b><span>DE CARGAS</span><b>SEM MÍN.</b><span>KM REAIS</span>';
      if(rule.innerHTML!==wanted) rule.innerHTML=wanted;
    }

    const progressTitle=document.querySelector('.monthly-progress-card h2');
    if(progressTitle&&progressTitle.textContent.trim()!=='Coleção de cargas') progressTitle.textContent='Coleção de cargas';

    const progressLead=document.querySelector('.monthly-progress-card .lead');
    if(progressLead&&progressLead.textContent.trim()!==CURRENT_COLLECTION_TEXT) progressLead.textContent=CURRENT_COLLECTION_TEXT;
  };

  const start=()=>{
    apply();
    const root=document.getElementById('driverContent')||document.body;
    const observer=new MutationObserver(apply);
    observer.observe(root,{subtree:true,childList:true,characterData:true});
    setInterval(apply,1500);
  };

  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',start,{once:true});
  else start();
})();
