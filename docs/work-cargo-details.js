(()=>{
  const API='https://douglas.tail4577e8.ts.net';
  let catalog=[];
  const openCards=new Set();
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const sess=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const user=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}return clean(new URLSearchParams(location.search).get('u')||sess()?.user)};
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  function chips(values){return values.map(x=>'<span class="cargo-compat-chip live">'+esc(x)+'</span>').join('')}
  function itemFor(card){const n=card.querySelector('.cargo-number')?.textContent||'';const pos=Number(n.replace(/\D/g,''));return catalog.find(x=>Number(x.position)===pos)||null}
  function cardKey(item){return String(item?.id||item?.position||'')}
  function decorate(){
    document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(card=>{
      if(card.querySelector('.cargo-compat-wrap'))return;
      const item=itemFor(card);if(!item)return;
      const body=card.querySelector('.cargo-body');if(!body)return;
      const actual=Array.isArray(item.compatible_cargos)?[...new Set(item.compatible_cargos.map(x=>String(x||'').trim()).filter(Boolean))]:[];
      const wrap=document.createElement('div');wrap.className='cargo-compat-wrap';
      const id='cargoCompat'+String(item.position||item.id).replace(/\W/g,'');
      const key=cardKey(item),isOpen=openCards.has(key);
      const content=actual.length
        ? '<span class="cargo-compat-source">NOMES REAIS JÁ DETECTADOS NO ETS2</span><div class="cargo-compat-chips">'+chips(actual)+'</div><span class="cargo-compat-note">Estes nomes vieram diretamente das viagens enviadas pelo GAT Telemetria. Não mostramos mais nomes genéricos ou exemplos inventados.</span>'
        : '<span class="cargo-compat-source">AINDA SEM CARGA REAL REGISTRADA</span><span class="cargo-compat-note">O GAT ainda não encontrou no ETS2 uma carga real desta categoria. Quando um motorista pegar uma carga compatível, o nome exato do jogo aparecerá aqui automaticamente.</span>';
      wrap.innerHTML='<button type="button" class="cargo-compat-toggle" aria-expanded="'+String(isOpen)+'" aria-controls="'+id+'"><span>'+(isOpen?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS')+'</span><span class="arrow">⌄</span></button><div class="cargo-compat-panel" id="'+id+'" '+(isOpen?'':'hidden')+'><span class="cargo-compat-title">CARGAS COMPATÍVEIS CONFIRMADAS</span>'+content+'</div>';
      const btn=wrap.querySelector('button'),panel=wrap.querySelector('.cargo-compat-panel'),label=btn.querySelector('span:first-child');
      btn.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const open=btn.getAttribute('aria-expanded')==='true';const next=!open;btn.setAttribute('aria-expanded',String(next));panel.hidden=!next;if(label)label.textContent=next?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS';if(next)openCards.add(key);else openCards.delete(key)});
      body.appendChild(wrap);
    });
  }
  async function load(){
    const u=user();if(!u)return;
    try{
      const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(u),{cache:'no-store'}),d=await r.json();
      if(r.ok&&d?.ok&&Array.isArray(d.catalog)){catalog=d.catalog;decorate()}
    }catch(_){decorate()}
  }
  const mo=new MutationObserver(()=>decorate());
  document.addEventListener('DOMContentLoaded',()=>{const root=document.getElementById('workCatalogGrid');if(root)mo.observe(root,{childList:true,subtree:true});load()});
  setInterval(load,5000);
})();