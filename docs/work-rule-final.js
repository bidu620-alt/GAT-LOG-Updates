(()=>{
  const apply=()=>{
    const min=document.getElementById('workMinKm');
    if(min&&min.textContent.trim()!=='Sem distância mínima') min.textContent='Sem distância mínima';
    const owner=document.getElementById('workOwnerMessage');
    if(owner&&/30 viagens|meta mensal/i.test(owner.textContent||'')) owner.textContent='A carga é reconhecida automaticamente pela telemetria.';
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
