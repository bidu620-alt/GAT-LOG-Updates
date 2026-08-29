(()=>{
  const API='https://douglas.tail4577e8.ts.net';
  let catalog=[],official=null;
  const openCards=new Set();
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const sess=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const user=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}return clean(new URLSearchParams(location.search).get('u')||sess()?.user)};
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const unique=values=>[...new Set((values||[]).map(x=>String(x||'').trim()).filter(Boolean))];
  function chips(values,kind){return values.map(x=>'<span class="cargo-compat-chip '+(kind||'')+'">'+esc(x)+'</span>').join('')}
  function itemFor(card){const n=card.querySelector('.cargo-number')?.textContent||'';const pos=Number(n.replace(/\D/g,''));return catalog.find(x=>Number(x.position)===pos)||null}
  function cardKey(item){return String(item?.id||item?.position||'')}
  function officialFor(item){
    const rows=official?.categories?.[item?.id];
    if(!Array.isArray(rows))return [];
    return unique(rows.map(x=>typeof x==='string'?x:x?.name));
  }
  function redraw(){document.querySelectorAll('#workCatalogGrid .cargo-compat-wrap').forEach(x=>x.remove());decorate()}
  function decorate(){
    document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(card=>{
      if(card.querySelector('.cargo-compat-wrap'))return;
      const item=itemFor(card);if(!item)return;
      const body=card.querySelector('.cargo-body');if(!body)return;
      const seen=Array.isArray(item.compatible_cargos)?unique(item.compatible_cargos):[];
      const base=officialFor(item);
      const wrap=document.createElement('div');wrap.className='cargo-compat-wrap';
      const id='cargoCompat'+String(item.position||item.id).replace(/\W/g,'');
      const key=cardKey(item),isOpen=openCards.has(key);
      const ver=String(official?.reference_game_version||'').trim();
      let content='';
      if(base.length){
        content+='<span class="cargo-compat-source">CARGAS OFICIAIS DO ETS2'+(ver?' • BASE '+esc(ver):'')+' • '+base.length+'</span><div class="cargo-compat-chips">'+chips(base,'official')+'</div>';
      }else if(official){
        content+='<span class="cargo-compat-source">SEM CARGA OFICIAL CLASSIFICADA NESTA CATEGORIA</span>';
      }else{
        content+='<span class="cargo-compat-source">CARREGANDO LISTA OFICIAL DO ETS2...</span>';
      }
      if(seen.length){
        content+='<span class="cargo-compat-source cargo-live-source">NOMES JÁ VISTOS NO SEU ETS2 PELO GAT TELEMETRIA • '+seen.length+'</span><div class="cargo-compat-chips">'+chips(seen,'live')+'</div>';
      }
      content+='<span class="cargo-compat-note">Azul = catálogo oficial do ETS2 separado por categoria. Verde = nome exatamente como chegou da telemetria dos motoristas, inclusive tradução do jogo ou carga de mod.</span>';
      wrap.innerHTML='<button type="button" class="cargo-compat-toggle" aria-expanded="'+String(isOpen)+'" aria-controls="'+id+'"><span>'+(isOpen?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS')+'</span><span class="arrow">⌄</span></button><div class="cargo-compat-panel" id="'+id+'" '+(isOpen?'':'hidden')+'><span class="cargo-compat-title">O QUE CONTA NESTE TRABALHO</span>'+content+'</div>';
      const btn=wrap.querySelector('button'),panel=wrap.querySelector('.cargo-compat-panel'),label=btn.querySelector('span:first-child');
      btn.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const open=btn.getAttribute('aria-expanded')==='true';const next=!open;btn.setAttribute('aria-expanded',String(next));panel.hidden=!next;if(label)label.textContent=next?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS';if(next)openCards.add(key);else openCards.delete(key)});
      body.appendChild(wrap);
    });
  }
  async function loadOfficial(){
    try{
      const r=await fetch('ets2-official-cargos.json?v=1',{cache:'no-store'}),d=await r.json();
      if(r.ok&&d?.categories){official=d;redraw()}
    }catch(_){decorate()}
  }
  async function load(){
    const u=user();if(!u){decorate();return}
    try{
      const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(u),{cache:'no-store'}),d=await r.json();
      if(r.ok&&d?.ok&&Array.isArray(d.catalog)){catalog=d.catalog;decorate()}
    }catch(_){decorate()}
  }
  const mo=new MutationObserver(()=>decorate());
  document.addEventListener('DOMContentLoaded',()=>{const root=document.getElementById('workCatalogGrid');if(root)mo.observe(root,{childList:true,subtree:true});loadOfficial();load()});
  setInterval(load,5000);
})();