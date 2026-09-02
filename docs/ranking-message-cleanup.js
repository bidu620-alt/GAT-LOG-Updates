(()=>{
  'use strict';
  const aliases={glp:'LPG','gas liquefeito de petroleo':'LPG','gas liquefeito':'LPG'};
  let busy=false;
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();

  function localizeVisible(){
    document.querySelectorAll('.full-cargo-card h3').forEach(el=>{if(norm(el.textContent)==='lpg')el.textContent='GLP'});
    const live=document.querySelector('#fullCargoLiveBox .full-cargo-live-name');
    if(live&&norm(live.textContent)==='lpg')live.textContent='GLP';
  }

  function bindSearch(){
    const input=document.getElementById('fullCargoSearch');
    if(!input||input.dataset.gatPtAliases==='1')return;
    input.dataset.gatPtAliases='1';
    input.addEventListener('input',()=>{
      if(busy)return;
      const original=input.value,target=aliases[norm(original)];
      if(!target)return;
      busy=true;
      input.value=target;
      input.dispatchEvent(new Event('input',{bubbles:true}));
      input.value=original;
      busy=false;
      queueMicrotask(localizeVisible);
    });
  }

  function start(){
    bindSearch();localizeVisible();
    const root=document.getElementById('workCatalogGrid')||document.body;
    new MutationObserver(()=>{bindSearch();localizeVisible()}).observe(root,{childList:true,subtree:true});
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();