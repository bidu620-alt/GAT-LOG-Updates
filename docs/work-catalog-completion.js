(()=>{
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  let lastSig='';

  function currentProfile(){try{return typeof profile!=='undefined'?profile:null}catch(_){return null}}

  function completedSet(){
    const p=currentProfile();
    const rows=Array.isArray(p?.deliveries)?p.deliveries:[];
    const set=new Set();
    rows.forEach(x=>{
      const name=norm(x?.cargo||x?.cargo_name||x?.name);
      if(name)set.add(name);
    });
    return set;
  }

  function signature(){
    const p=currentProfile();
    const rows=Array.isArray(p?.deliveries)?p.deliveries:[];
    return rows.map(x=>norm(x?.cargo||x?.cargo_name||x?.name)).filter(Boolean).sort().join('|');
  }

  function ensureStyle(){
    if(document.getElementById('gatCargoCompletionStyle'))return;
    const s=document.createElement('style');
    s.id='gatCargoCompletionStyle';
    s.textContent=`
.cargo-reg-lite{display:none!important}
.full-cargo-card.catalog-completed{border-color:#1f8a5b!important;box-shadow:0 0 0 1px rgba(31,138,91,.24),0 14px 34px rgba(0,0,0,.18)!important;background:linear-gradient(180deg,rgba(15,69,49,.22),rgba(7,20,17,.35))!important}
.full-cargo-card.catalog-completed .cargo-visual{background:linear-gradient(135deg,rgba(20,111,73,.45),rgba(8,39,29,.25))!important;border-bottom-color:#1d6d4d!important}
.full-cargo-card.catalog-completed .cargo-state{background:#123d2d!important;color:#75efb6!important;border-color:#237454!important}
.full-cargo-card.catalog-completed .cargo-number{color:#75efb6!important}
.full-cargo-card.catalog-completed .cargo-body h3{color:#dfffee!important}
`;
    document.head.appendChild(s);
  }

  function cleanMeta(card){
    card.querySelectorAll('.cargo-meta span').forEach(el=>{
      if(norm(el.textContent)==='catalogo em portugues')el.remove();
    });
    card.querySelectorAll('.cargo-reg-lite').forEach(el=>el.remove());
  }

  function cardNames(card){
    const pt=norm(card.querySelector('.cargo-body h3')?.textContent||'');
    const title=String(card.getAttribute('title')||'');
    const official=norm(title.replace(/^Nome oficial SCS:\s*/i,''));
    return [pt,official].filter(Boolean);
  }

  function apply(){
    ensureStyle();
    const done=completedSet();
    document.querySelectorAll('#workCatalogGrid .full-cargo-card').forEach(card=>{
      cleanMeta(card);
      const completed=cardNames(card).some(n=>done.has(n));
      card.classList.toggle('catalog-completed',completed);
      const state=card.querySelector('.cargo-state');
      if(state)state.textContent=completed?'✓ CONCLUÍDA':'OFICIAL';
    });
  }

  function start(){
    ensureStyle();
    const root=document.getElementById('workCatalogGrid');
    if(root)new MutationObserver(()=>setTimeout(apply,0)).observe(root,{childList:true,subtree:true});
    lastSig=signature();
    apply();
    setInterval(()=>{
      const sig=signature();
      if(sig!==lastSig){lastSig=sig;apply();return}
      document.querySelectorAll('.cargo-reg-lite').forEach(el=>el.remove());
    },700);
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
