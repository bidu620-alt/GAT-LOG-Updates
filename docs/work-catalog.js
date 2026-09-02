(()=>{
  const CATALOG_URL='ets2-official-cargos.json';
  const GRID='workCatalogGrid',STATUS='workCatalogStatus';
  let items=[],visible=80,query='';

  const clean=v=>String(v||'').trim();
  const norm=v=>clean(v).normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const currentLive=()=>{try{return typeof lastLive!=='undefined'?lastLive:null}catch(_){return null}};
  const liveCargo=t=>{try{if(typeof cargoOf==='function')return clean(cargoOf(t))}catch(_){}return clean(t?.cargo_name||t?.cargo||t?.telemetry?.job?.cargoName)};
  const liveSource=t=>{try{if(typeof sourceOf==='function')return clean(sourceOf(t))}catch(_){}return clean(t?.source_city||t?.source||t?.telemetry?.job?.sourceCity)};
  const liveDestination=t=>{try{if(typeof destinationOf==='function')return clean(destinationOf(t))}catch(_){}return clean(t?.destination_city||t?.destination||t?.telemetry?.job?.destinationCity)};
  const isFresh=t=>{try{if(typeof fresh==='function')return fresh(t)}catch(_){}const d=Date.parse(t?.updated_at||'');return Number.isFinite(d)&&Date.now()-d<45000};
  const liveOnJob=t=>!!(t&&isFresh(t)&&liveCargo(t));

  function injectStyle(){
    if(document.getElementById('gatFullCargoCatalogStyle'))return;
    const s=document.createElement('style');s.id='gatFullCargoCatalogStyle';s.textContent=`
.full-cargo-toolbar{display:flex;align-items:center;gap:10px;margin:14px 0 16px}.full-cargo-search{flex:1;min-width:0;border:1px solid #27445d;border-radius:11px;background:#09131d;color:#eef7ff;padding:11px 13px;font:700 11px/1.2 "Segoe UI",sans-serif;outline:none}.full-cargo-search:focus{border-color:#3187c2;box-shadow:0 0 0 3px rgba(49,135,194,.12)}.full-cargo-count{white-space:nowrap;color:#7893a9;font-size:10px;font-weight:800}.full-cargo-live{margin:12px 0 16px;border:1px solid #24628f;border-radius:14px;background:linear-gradient(135deg,#0a1a27,#081119);padding:14px 16px;display:none}.full-cargo-live.show{display:block}.full-cargo-live.match{border-color:#21855c}.full-cargo-live-head{display:flex;justify-content:space-between;gap:12px;align-items:center}.full-cargo-live-head b{font-size:10px;color:#79c9ff;letter-spacing:.05em}.full-cargo-live-head span{font-size:9px;color:#62e5b0;font-weight:900}.full-cargo-live-name{font-size:18px;color:#fff;font-weight:950;margin:7px 0 4px}.full-cargo-live-route{font-size:10px;color:#8499ad}.full-cargo-live-result{margin-top:10px;padding-top:10px;border-top:1px solid #17344a;font-size:10px;color:#89a0b4}.full-cargo-live.match .full-cargo-live-result{color:#68e2ad}.full-cargo-card.current{outline:2px solid #2b9a68;box-shadow:0 0 0 3px rgba(43,154,104,.12)}.full-cargo-card .cargo-body small{display:flex;gap:6px;flex-wrap:wrap}.full-cargo-card .cargo-body h3{font-size:13px;line-height:1.3}.cargo-dlc{color:#80a9c8}.cargo-weight{color:#8298aa}.full-cargo-more{grid-column:1/-1;display:flex;justify-content:center;padding:12px}.full-cargo-more button{border:1px solid #2a5d80;border-radius:9px;background:#0d2232;color:#7fc9ff;padding:10px 16px;font-size:10px;font-weight:900;cursor:pointer}.full-cargo-empty{grid-column:1/-1;padding:22px;border:1px dashed #294052;border-radius:12px;color:#7890a6;text-align:center;font-size:11px}@media(max-width:700px){.full-cargo-toolbar{align-items:stretch;flex-direction:column}.full-cargo-count{padding-left:2px}}
`;
    document.head.appendChild(s);
  }

  function patchCopy(total){
    const workCard=document.querySelector('.work-driver-card');
    const eyebrow=workCard?.querySelector('.card-title .eyebrow');if(eyebrow)eyebrow.textContent='CATÁLOGO COMPLETO DE CARGAS';
    const title=document.getElementById('workTitle');if(title&&!liveOnJob(currentLive()))title.textContent='Aguardando carga do ETS2';
    const market=document.getElementById('workMarket');if(market)market.textContent='Todos os mercados';
    const min=document.getElementById('workMinKm');if(min)min.textContent='500 km reais';
    const weight=document.getElementById('workWeight');if(weight)weight.textContent='Peso > 0';
    const freedom=document.getElementById('workFreedom');if(freedom&&!liveOnJob(currentLive()))freedom.textContent='Qualquer carga oficial do ETS2';
    const lead=workCard?.querySelector(':scope > .lead');if(lead)lead.textContent='Pegue uma carga normalmente no ETS2. O GAT Telemetria identifica o nome real da carga; não é mais necessário escolher categoria no site.';
    const owner=document.getElementById('workOwnerMessage');if(owner)owner.textContent='A carga é reconhecida pela telemetria. A meta mensal continua sendo 30 viagens válidas, mas os tipos de carga não ficam limitados a 30 categorias.';
    const head=document.querySelector('.work-catalog-section .catalog-head');
    const he=head?.querySelector('.eyebrow');if(he)he.textContent='CARGAS OFICIAIS ETS2';
    const h2=head?.querySelector('h2');if(h2)h2.textContent='Todas as cargas do jogo';
    const p=head?.querySelector('p');if(p)p.textContent='Lista única com '+total+' cargas oficiais. Pesquise pelo nome e use qualquer carga compatível com as regras da viagem; não há divisão por categoria para o motorista.';
    const rule=head?.querySelector('.catalog-rule');if(rule)rule.innerHTML='<b>30</b><span>VIAGENS / MÊS</span><b>500 km</b><span>REAIS MÍN.</span>';
    const progressLead=document.querySelector('.monthly-progress-card .lead');if(progressLead)progressLead.textContent='A meta mensal continua em 30 viagens válidas. O número 30 representa a quantidade de trabalhos do mês e não limita os tipos de carga: o motorista pode usar qualquer carga oficial reconhecida pelo GAT Telemetria.';
  }

  function ensureToolbar(){
    const section=document.querySelector('.work-catalog-section'),grid=document.getElementById(GRID);if(!section||!grid)return;
    let bar=document.getElementById('fullCargoToolbar');if(bar)return;
    bar=document.createElement('div');bar.id='fullCargoToolbar';bar.className='full-cargo-toolbar';
    bar.innerHTML='<input id="fullCargoSearch" class="full-cargo-search" type="search" autocomplete="off" placeholder="Pesquisar carga..." aria-label="Pesquisar todas as cargas do ETS2"><span id="fullCargoCount" class="full-cargo-count"></span>';
    grid.insertAdjacentElement('beforebegin',bar);
    const input=document.getElementById('fullCargoSearch');input.addEventListener('input',()=>{query=input.value;visible=80;render()});
  }

  function ensureLiveBox(){
    let box=document.getElementById('fullCargoLiveBox');if(box)return box;
    box=document.createElement('div');box.id='fullCargoLiveBox';box.className='full-cargo-live';
    const anchor=document.querySelector('.work-live');if(anchor)anchor.insertAdjacentElement('afterend',box);
    return box;
  }

  function exactMatch(name){const n=norm(name);if(!n)return null;return items.find(x=>norm(x.name)===n)||null}

  function renderLive(){
    const box=ensureLiveBox();if(!box)return;
    const t=currentLive(),cargo=liveCargo(t);if(!liveOnJob(t)||!cargo){box.className='full-cargo-live';box.innerHTML='';const title=document.getElementById('workTitle');if(title)title.textContent='Aguardando carga do ETS2';return}
    const src=liveSource(t),dst=liveDestination(t),match=exactMatch(cargo);
    box.className='full-cargo-live show'+(match?' match':'');
    box.innerHTML='<div class="full-cargo-live-head"><b>CARGA ATUAL • GAT TELEMETRIA</b><span>● AO VIVO</span></div><div class="full-cargo-live-name">'+esc(cargo)+'</div><div class="full-cargo-live-route">'+esc(src||'Origem detectada')+' → '+esc(dst||'Destino detectado')+'</div><div class="full-cargo-live-result">'+(match?'✓ Carga encontrada no catálogo oficial'+(match.dlc?' • '+esc(match.dlc):''):'Carga detectada pela telemetria. O nome ao vivo continua válido mesmo quando a tradução usada no jogo difere do nome do catálogo.')+'</div>';
    const title=document.getElementById('workTitle');if(title)title.textContent='Trabalho atual • '+cargo;
    const freedom=document.getElementById('workFreedom');if(freedom)freedom.textContent=cargo;
    document.querySelectorAll('.full-cargo-card.current').forEach(el=>el.classList.remove('current'));
    if(match){const card=document.querySelector('[data-cargo-key="'+CSS.escape(norm(match.name))+'"]');if(card)card.classList.add('current')}
  }

  function flatCatalog(data){
    const out=[];const titles=data?.category_titles||{};const cats=data?.categories||{};
    Object.entries(cats).forEach(([category,rows])=>{if(!Array.isArray(rows))return;rows.forEach((row,i)=>{const name=clean(row?.name);if(!name)return;out.push({id:category+'-'+i,name,dlc:clean(row?.dlc),weight:clean(row?.weight),category,title:clean(titles[category])})})});
    const unique=new Map();out.forEach(x=>{const k=norm(x.name);if(k&&!unique.has(k))unique.set(k,x)});
    return [...unique.values()].sort((a,b)=>a.name.localeCompare(b.name,'pt-BR',{sensitivity:'base'}));
  }

  function filtered(){const qn=norm(query);if(!qn)return items;return items.filter(x=>norm(x.name+' '+x.dlc+' '+x.title+' '+x.category).includes(qn))}

  function render(){
    const root=document.getElementById(GRID);if(!root)return;ensureToolbar();const rows=filtered(),show=rows.slice(0,visible),liveName=liveCargo(currentLive()),liveNorm=norm(liveName);root.textContent='';
    const count=document.getElementById('fullCargoCount');if(count)count.textContent=(query?rows.length+' encontradas • ':'')+items.length+' cargas no catálogo';
    if(!show.length){root.innerHTML='<div class="full-cargo-empty">Nenhuma carga encontrada com essa pesquisa.</div>';renderLive();return}
    show.forEach((item,index)=>{const card=document.createElement('article');card.className='cargo-card full-cargo-card'+(liveNorm&&norm(item.name)===liveNorm?' current':'');card.dataset.cargoKey=norm(item.name);card.innerHTML='<div class="cargo-visual"><span class="cargo-number">#'+String(index+1).padStart(3,'0')+'</span><span class="cargo-state">OFICIAL</span><span class="cargo-icon">🚚</span></div><div class="cargo-body"><small>'+(item.dlc?'<span class="cargo-dlc">'+esc(item.dlc)+'</span>':'<span class="cargo-dlc">Jogo base / oficial</span>')+(item.weight?'<span class="cargo-weight">• '+esc(item.weight)+' t</span>':'')+'</small><h3>'+esc(item.name)+'</h3><div class="cargo-meta"><span>CARGA REAL ETS2</span><span>SEM CATEGORIA OBRIGATÓRIA</span></div></div>';root.appendChild(card)});
    if(rows.length>show.length){const more=document.createElement('div');more.className='full-cargo-more';more.innerHTML='<button type="button">MOSTRAR MAIS ('+(rows.length-show.length)+')</button>';more.querySelector('button').onclick=()=>{visible+=80;render()};root.appendChild(more)}
    renderLive();
  }

  async function load(){
    injectStyle();ensureToolbar();const status=document.getElementById(STATUS);if(status)status.textContent='Carregando catálogo oficial de cargas do ETS2...';
    try{const r=await fetch(CATALOG_URL+'?v=full-cargo-1',{cache:'no-store'}),data=await r.json();if(!r.ok)throw new Error('HTTP '+r.status);items=flatCatalog(data);patchCopy(items.length);render();if(status)status.textContent=items.length+' cargas oficiais disponíveis • lista única • sem limite de categorias.'}
    catch(_){items=[];patchCopy(0);if(status)status.textContent='Não foi possível carregar o catálogo oficial agora.';render()}
  }

  document.addEventListener('DOMContentLoaded',load);
  window.addEventListener('gat-account-change',()=>setTimeout(()=>{patchCopy(items.length);render()},300));
  setInterval(()=>{if(items.length){patchCopy(items.length);renderLive()}},1000);
})();
