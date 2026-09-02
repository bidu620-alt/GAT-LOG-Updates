(()=>{
  const API='https://api.gatlogets2.com.br';
  const STYLE_ID='gatRegisteredCargoLiteStyle';
  let learnedCache=null;
  let learnedPromise=null;

  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));

  function style(){
    if(document.getElementById(STYLE_ID))return;
    const s=document.createElement('style');
    s.id=STYLE_ID;
    s.textContent=`
.cargo-reg-lite{margin-top:12px;border-top:1px solid #17344b;padding-top:10px}
.cargo-reg-lite button{width:100%;display:flex;align-items:center;justify-content:space-between;gap:10px;border:1px solid #225170;background:#0b1b28;color:#7fc8ff;border-radius:9px;padding:9px 11px;font:700 11px/1.2 "Segoe UI",sans-serif;letter-spacing:.04em;cursor:pointer}
.cargo-reg-lite button:hover{background:#102638;border-color:#2d79a9}
.cargo-reg-lite button .arrow{font-size:15px;transition:transform .18s ease}
.cargo-reg-lite button[aria-expanded="true"] .arrow{transform:rotate(180deg)}
.cargo-reg-lite-panel{margin-top:8px;padding:11px;border:1px solid #17384f;border-radius:10px;background:#07131d}
.cargo-reg-lite-panel[hidden]{display:none}
.cargo-reg-lite-title{display:block;color:#7b96aa;font-size:9px;font-weight:700;letter-spacing:.06em;margin-bottom:7px}
.cargo-reg-lite-chips{display:flex;flex-wrap:wrap;gap:6px}
.cargo-reg-lite-chip{display:inline-flex;align-items:center;min-height:25px;padding:4px 8px;border:1px solid #11634e;border-radius:999px;background:#08251c;color:#72efc0;font-size:10px;font-weight:650}
.cargo-reg-lite-note{display:block;color:#7890a2;font-size:9px;line-height:1.45}
.cargo-card.completed .cargo-reg-lite button{border-color:#17634d;color:#71e5b7}
`;
    document.head.appendChild(s);
  }

  async function fetchLearned(force=false){
    if(!force&&Array.isArray(learnedCache))return learnedCache;
    if(learnedPromise)return learnedPromise;
    learnedPromise=(async()=>{
      try{
        const r=await fetch(API+'/api/public/work/learned-cargos',{cache:'no-store'});
        const d=await r.json().catch(()=>null);
        learnedCache=r.ok&&d?.ok&&Array.isArray(d.cargos)?d.cargos:[];
      }catch(_){learnedCache=[]}
      learnedPromise=null;
      return learnedCache;
    })();
    return learnedPromise;
  }

  function workId(card){return String(card?.dataset?.workId||'').trim()}

  function mountCard(card){
    if(!card||card.querySelector('.cargo-reg-lite'))return;
    const body=card.querySelector('.cargo-body');
    if(!body)return;
    const wrap=document.createElement('div');
    wrap.className='cargo-reg-lite';
    wrap.innerHTML='<button type="button" aria-expanded="false"><span>VER CARGAS REGISTRADAS</span><span class="arrow">⌄</span></button><div class="cargo-reg-lite-panel" hidden><span class="cargo-reg-lite-note">Clique para consultar as cargas registradas nesta categoria.</span></div>';
    const btn=wrap.querySelector('button'),panel=wrap.querySelector('.cargo-reg-lite-panel'),label=btn.querySelector('span:first-child');
    btn.addEventListener('click',async e=>{
      e.preventDefault();e.stopPropagation();
      const opening=btn.getAttribute('aria-expanded')!=='true';
      btn.setAttribute('aria-expanded',String(opening));
      panel.hidden=!opening;
      label.textContent=opening?'OCULTAR CARGAS REGISTRADAS':'VER CARGAS REGISTRADAS';
      if(!opening)return;
      panel.innerHTML='<span class="cargo-reg-lite-note">Consultando cargas registradas...</span>';
      const all=await fetchLearned(true);
      const id=workId(card);
      const rows=all.filter(x=>String(x?.work_id||'')===id).sort((a,b)=>String(a?.cargo_name||'').localeCompare(String(b?.cargo_name||''),'pt-BR'));
      if(!rows.length){panel.innerHTML='<span class="cargo-reg-lite-note">Nenhuma carga registrada nesta categoria ainda.</span>';return}
      panel.innerHTML='<span class="cargo-reg-lite-title">CARGAS REGISTRADAS • '+rows.length+'</span><div class="cargo-reg-lite-chips">'+rows.map(x=>'<span class="cargo-reg-lite-chip">'+esc(x.cargo_name||'Carga')+'</span>').join('')+'</div>';
    });
    body.appendChild(wrap);
  }

  function mount(){
    document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(mountCard);
  }

  function start(){
    style();mount();
    const root=document.getElementById('workCatalogGrid');
    if(root){
      const obs=new MutationObserver(()=>mount());
      obs.observe(root,{childList:true,subtree:false});
    }
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
