(()=>{
  function ensureStyle(){if(document.getElementById('gatOfficialRulesStyle'))return;const s=document.createElement('style');s.id='gatOfficialRulesStyle';s.textContent='.gat-rules-link{display:inline-flex;align-items:center;gap:6px;border:1px solid #2d6a9b;border-radius:10px;background:#0d2032;color:#7fc1ff;padding:8px 10px;font-size:9px;font-weight:950;text-decoration:none}.gat-rules-link:hover{filter:brightness(1.12)}';document.head.appendChild(s)}
  function apply(){
    ensureStyle();
    document.getElementById('gatWorkStageBadge')?.remove();
    const wrap=document.querySelector('.gat-work-statuses');if(wrap&&wrap.children.length===1){const state=wrap.firstElementChild;wrap.parentNode.insertBefore(state,wrap);wrap.remove()}
    const min=document.getElementById('workMinKm');if(min)min.textContent='Sem distância mínima';
    const rule=document.querySelector('.work-catalog-section .catalog-rule');if(rule)rule.innerHTML='<b>SEM MÍN.</b><span>KM REAIS</span><b>AUTOMÁTICO</b><span>PELA TELEMETRIA</span>';
    const nav=document.querySelector('.topbar nav');if(nav&&!nav.querySelector('a[href="regras.html"]')){const a=document.createElement('a');a.href='regras.html';a.textContent='Regras';const ranking=[...nav.querySelectorAll('a')].find(x=>/ranking/i.test(x.textContent||''));if(ranking)ranking.insertAdjacentElement('afterend',a);else nav.appendChild(a)}
    const head=document.querySelector('.work-catalog-section .catalog-head');if(head&&!document.getElementById('gatRulesLink')){const a=document.createElement('a');a.id='gatRulesLink';a.className='gat-rules-link';a.href='regras.html';a.textContent='REGRAS OFICIAIS →';head.appendChild(a)}
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
  setTimeout(apply,300);
  setTimeout(apply,1200);
})();