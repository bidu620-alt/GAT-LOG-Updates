(()=>{
  const API='https://api.gatlogets2.com.br',GRID='workCatalogGrid',STATUS='workCatalogStatus';
  let items=[];
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  const session=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const pageUser=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}const u=new URLSearchParams(location.search).get('u');return clean(u||session()?.user)};
  const own=()=>!!session()?.token&&clean(session().user)===pageUser();
  const current=()=>{try{return typeof profile!=='undefined'?profile?.current_mission:null}catch(_){return null}};
  const live=()=>{try{return typeof lastLive!=='undefined'?lastLive:null}catch(_){return null}};
  const isFresh=t=>{try{if(typeof fresh==='function')return fresh(t)}catch(_){}const d=Date.parse(t?.updated_at||'');return Number.isFinite(d)&&Date.now()-d<20000};
  const liveCargo=t=>{try{if(typeof cargoOf==='function')return cargoOf(t)}catch(_){}return String(t?.cargo_name||t?.cargo||t?.telemetry?.job?.cargoName||'').trim()};
  const liveSource=t=>String(t?.source_city||t?.source||t?.telemetry?.job?.sourceCity||t?.telemetry?.job?.source?.cityName||'').trim();
  const liveDestination=t=>String(t?.destination_city||t?.destination||t?.telemetry?.job?.destinationCity||t?.telemetry?.job?.destination?.cityName||'').trim();
  const liveOnJob=t=>{if(!t)return false;const raw=t.telemetry||{},vals=[t.job_latched,raw.job_latched,t.on_job,raw.on_job,raw.onJob,raw.job?.onJob,raw.job?.active];return vals.some(v=>v===true||v===1||String(v).toLowerCase()==='true')||!!liveCargo(t)};

  function injectSuggestionStyle(){
    if(document.getElementById('gatWorkSuggestionStyle'))return;
    const s=document.createElement('style');s.id='gatWorkSuggestionStyle';s.textContent=`
.work-telemetry-suggestion{margin:12px 0 16px;border:1px solid #245a86;border-radius:14px;background:linear-gradient(135deg,#0b1c2b,#0a131c);padding:14px 16px;display:none}
.work-telemetry-suggestion.show{display:block}
.work-telemetry-suggestion .suggest-head{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:8px}
.work-telemetry-suggestion .suggest-head b{font-size:12px;color:#78c4ff;letter-spacing:.04em}
.work-telemetry-suggestion .suggest-live{font-size:9px;font-weight:900;color:#5fe0aa}
.work-telemetry-suggestion .suggest-cargo{font-size:17px;font-weight:950;color:#f2f8ff;margin-bottom:5px}
.work-telemetry-suggestion .suggest-route{font-size:10px;color:#8fa5ba;margin-bottom:12px}
.work-telemetry-suggestion .suggest-match{border-top:1px solid #18364f;padding-top:11px;display:flex;align-items:center;justify-content:space-between;gap:12px}
.work-telemetry-suggestion .suggest-match strong{display:block;font-size:12px;color:#fff}
.work-telemetry-suggestion .suggest-match small{display:block;margin-top:3px;color:#7e94aa;font-size:9px}
.work-telemetry-suggestion button{border:1px solid #2f83c5;background:#1167a5;color:#fff;border-radius:8px;padding:10px 14px;font-weight:900;font-size:9px;cursor:pointer}
.work-telemetry-suggestion button:hover{filter:brightness(1.08)}
.cargo-card.gat-suggested{outline:2px solid #2b8bd0;box-shadow:0 0 0 3px rgba(43,139,208,.12)}
.cargo-card.gat-suggested .cargo-state{color:#76c7ff}
`;
    document.head.appendChild(s);
  }

  function ensureValidation(){let box=document.getElementById('workValidationState');if(box)return box;const liveBox=document.querySelector('.work-live');if(!liveBox)return null;box=document.createElement('div');box.id='workValidationState';box.className='work-validation idle';box.innerHTML='<span class="work-validation-icon">○</span><div><b>AGUARDANDO TRABALHO</b><small>Escolha uma categoria de trabalho.</small></div>';liveBox.insertAdjacentElement('afterend',box);return box}

  function compatibleNames(item){
    const out=[];
    const add=v=>{
      if(Array.isArray(v)){v.forEach(add);return}
      if(v&&typeof v==='object'){Object.values(v).forEach(add);return}
      if(typeof v!=='string')return;
      const x=v.trim();if(!x)return;
      if((x.startsWith('[')||x.startsWith('{'))){try{add(JSON.parse(x));return}catch(_){}}
      out.push(x);
    };
    add(item?.compatible_cargos);add(item?.compatible_cargos_json);add(item?.cargos);add(item?.cargo_names);add(item?.suggestions);add(item?.examples);add(item?.sample_cargos);
    return [...new Set(out)];
  }

  function cargoScore(item,cargo){
    const actual=norm(cargo);if(!actual)return 0;
    const candidates=[...compatibleNames(item),item?.title,item?.category].map(norm).filter(Boolean);
    let best=0;
    const at=new Set(actual.split(' ').filter(x=>x.length>2));
    for(const c of candidates){
      if(actual===c)best=Math.max(best,100);
      else if(actual.includes(c)||c.includes(actual))best=Math.max(best,88);
      else{
        const ct=new Set(c.split(' ').filter(x=>x.length>2));
        let hit=0;for(const x of at)if(ct.has(x))hit++;
        const denom=Math.max(1,Math.min(at.size||1,ct.size||1));
        best=Math.max(best,Math.round(hit/denom*70));
      }
    }
    return best;
  }

  function bestSuggestion(cargo){
    return items
      .map(x=>({item:x,score:cargoScore(x,cargo)}))
      .sort((a,b)=>b.score-a.score||Number(a.item?.position||0)-Number(b.item?.position||0))[0]||null;
  }

  function ensureSuggestion(){
    let box=document.getElementById('workTelemetrySuggestion');if(box)return box;
    box=document.createElement('div');box.id='workTelemetrySuggestion';box.className='work-telemetry-suggestion';
    const anchor=ensureValidation()||document.querySelector('.work-live');
    if(anchor)anchor.insertAdjacentElement('afterend',box);
    return box;
  }

  function renderSuggestion(){
    const box=ensureSuggestion();if(!box)return;
    document.querySelectorAll('.cargo-card.gat-suggested').forEach(x=>x.classList.remove('gat-suggested'));
    const t=live(),cargo=liveCargo(t),freshNow=isFresh(t),m=current();
    if(m||!own()||!freshNow||!liveOnJob(t)||!cargo||!items.length){box.className='work-telemetry-suggestion';box.innerHTML='';return}
    const pick=bestSuggestion(cargo),src=liveSource(t),dst=liveDestination(t);
    if(!pick){box.className='work-telemetry-suggestion';box.innerHTML='';return}
    const confident=pick.score>=35;
    const card=[...document.querySelectorAll('.cargo-card')].find(x=>x.dataset.workId===String(pick.item.id||''));
    if(confident&&card)card.classList.add('gat-suggested');
    box.className='work-telemetry-suggestion show';
    box.innerHTML=
      '<div class="suggest-head"><b>CARGA DETECTADA PELO GAT TELEMETRIA</b><span class="suggest-live">● AO VIVO</span></div>'+ 
      '<div class="suggest-cargo">'+esc(cargo)+'</div>'+ 
      '<div class="suggest-route">'+esc(src||'Origem detectada')+' → '+esc(dst||'Destino detectado')+'</div>'+ 
      '<div class="suggest-match"><div><strong>'+(confident?'Trabalho sugerido: '+esc(pick.item.title||pick.item.category||'Trabalho GAT'):'Não encontrei correspondência segura')+'</strong><small>'+(confident?'Compatibilidade '+pick.score+'% • confirme para marcar este trabalho.':'Veja os trabalhos abaixo e marque manualmente o mais adequado.')+'</small></div>'+(confident?'<button type="button" id="workSuggestionConfirm">MARCAR ESTE TRABALHO</button>':'')+'</div>';
    const b=document.getElementById('workSuggestionConfirm');if(b)b.onclick=()=>select(pick.item);
  }

  function validation(){const box=ensureValidation();if(!box)return;const m=current(),t=live(),state=String(m?.state||'').toLowerCase(),cargo=liveCargo(t),freshNow=isFresh(t),xpOnly=!!m?.xp_only;let cls='idle',icon='○',title='ESCOLHA UM TRABALHO',detail='Selecione uma das 30 categorias do catálogo.';
    if(m){title=xpOnly?'VIAGEM EXTRA • SOMENTE XP':'TRABALHO SELECIONADO';detail=xpOnly?'Este trabalho já foi concluído no mês. Esta nova viagem soma somente XP ao nível e não altera Pontos GAT nem x/30.':'Escolha uma carga coerente com a categoria e com pelo menos 500 km. As sugestões servem de guia; a contagem técnica usa a viagem registrada pela Telemetria.';icon='●';cls='waiting';
      if(state==='active'){cls='valid';icon='✓';title=xpOnly?'VIAGEM EXTRA • XP EM ANDAMENTO':'TRABALHO CORRETO • EM ANDAMENTO';detail=xpOnly?(cargo?cargo+' • ':'')+'Viagem válida para XP. Pontos GAT e progresso 30/30 permanecem iguais.':(cargo?cargo+' • ':'')+'Viagem registrada. O GAT está acompanhando a distância e aguardando entrega ou cancelamento real.'}
      else if(freshNow&&liveOnJob(t)){cls='checking';icon='…';title='VERIFICANDO VIAGEM';detail=(cargo?cargo+' • ':'')+'Aguardando a Telemetria registrar carga, peso e distância mínima na missão.'}
      else if(!freshNow){cls='waiting';icon='○';title=xpOnly?'VIAGEM EXTRA • AGUARDANDO TELEMETRIA':'TRABALHO SELECIONADO • AGUARDANDO TELEMETRIA';detail='Abra o ETS2 e o GAT Telemetria para registrar a viagem.'}
    }else if(freshNow&&liveOnJob(t)&&cargo){cls='checking';icon='…';title='CARGA DETECTADA';detail=cargo+' • veja abaixo o trabalho sugerido pelo GAT Telemetria.'}
    box.className='work-validation '+cls;box.innerHTML='<span class="work-validation-icon">'+icon+'</span><div><b>'+esc(title)+'</b><small>'+esc(detail)+'</small></div>';
    renderSuggestion();
  }

  function top(){const m=current(),set=(id,t)=>{const e=document.getElementById(id);if(e)e.textContent=t};set('workMarket','Todos os mercados');set('workMinKm','500 km mínimos');set('workWeight','Peso > 0 • sem mínimo extra');set('workFreedom',m?(m.custom_cargo||m.title||m.category||'Carga escolhida'):'Escolha um trabalho abaixo • rota única no mês');const msg=document.getElementById('workOwnerMessage');if(msg){if(!own())msg.textContent='Este é um perfil público. Entre na sua própria Conta GAT para escolher um trabalho.';else if(m?.xp_only)msg.textContent='Viagem extra por XP. Este trabalho já contou no mês: ao entregar, somente o XP será somado ao nível. Pontos GAT, x/30, perfeitas, multas e penalidades do ranking não mudam.';else if(m)msg.textContent='Trabalho selecionado. Use as sugestões como referência e escolha uma carga coerente com a categoria. O nome da carga não bloqueia a contagem: a Telemetria registra a viagem real, com peso maior que zero e pelo menos 500 km.';else msg.textContent='Pegue a carga no ETS2. Quando o GAT Telemetria detectar a viagem, esta tela sugere o trabalho mais compatível para você confirmar. Também é possível escolher manualmente.'}validation()}
  async function getCatalog(){const user=pageUser();if(!user)return null;const c=new AbortController(),t=setTimeout(()=>c.abort(),5500);try{const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(user),{cache:'no-store',signal:c.signal});const data=await r.json().catch(()=>null);if(!r.ok||!data?.ok)return null;return data}catch(_){return null}finally{clearTimeout(t)}}
  async function select(item){if(!own()){alert('Entre na sua própria Conta GAT para escolher o trabalho.');return}if(current()){alert('Você já possui um trabalho atual. Conclua ou peça ao Admin para resetar antes de escolher outro.');return}let custom='';if(item.custom){custom=(prompt('CRIAR CARGA PERSONALIZADA\n\nDigite o nome da carga como ela aparece no ETS2. Ex.: Volvo L250H, Transformador, Helicóptero etc.')||'').trim();if(custom.length<2)return}const s=session(),status=document.getElementById(STATUS);if(status)status.textContent=item.completed?'Preparando viagem extra por XP...':'Selecionando '+item.title+'...';try{const r=await fetch(API+'/api/site/work/select',{method:'POST',cache:'no-store',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:s.token,work_id:item.id,custom_cargo:custom})});const data=await r.json().catch(()=>null);if(r.ok&&data?.ok){try{if(typeof profile!=='undefined'&&profile){profile.current_mission=data.mission;profile.monthly_completed=Number(data.completed)||0;profile.monthly_goal=30;profile.xp_per_100_km=20;if(typeof renderMission==='function')renderMission(profile.current_mission);if(typeof updateOwnerArea==='function')updateOwnerArea()}}catch(_){}items=Array.isArray(data.catalog)?data.catalog:items;render();top();validation();if(status)status.textContent=data.xp_only?'Viagem extra selecionada. Ao entregar uma viagem válida, somente o XP será somado ao nível; Ranking GAT e x/30 não mudam.':'Trabalho marcado. A carga já detectada pelo GAT Telemetria será validada na próxima atualização.';return}const er=data?.error||('HTTP '+r.status);if(status)status.textContent=er==='mission_already_active'?'Você já possui um trabalho em andamento.':er==='route_already_used'?'Essa rota já foi usada neste mês. Escolha outro par de cidades.':'Não foi possível selecionar o trabalho: '+er}catch(_){if(status)status.textContent='Central GAT não respondeu ao selecionar o trabalho.'}}
  function render(){const root=document.getElementById(GRID);if(!root)return;root.textContent='';const m=current(),active=String(m?.catalog_id||'');if(!items.length){root.innerHTML='<div class="catalog-hint">Catálogo aguardando a Central GAT.</div>';return}items.forEach(item=>{const selected=active===item.id,done=!!item.completed,repeatSelected=selected&&!!m?.xp_only,locked=!!active&&!selected,card=document.createElement('article');card.dataset.workId=String(item.id||'');card.className='cargo-card'+(selected?' selected':'')+(done?' completed':'')+(locked?' locked':'')+(item.custom?' custom-card':'');const state=repeatSelected?'XP APENAS':done?'CONCLUÍDO':selected?'SELECIONADO':locked?'AGUARDANDO':'DISPONÍVEL',button=repeatSelected?'VIAGEM EXTRA XP':selected?'TRABALHO ATUAL':locked?'CONCLUA O ATUAL':done?'FAZER POR XP':item.custom?'CRIAR E PEGAR':'PEGAR TRABALHO';card.innerHTML='<div class="cargo-visual"><span class="cargo-number">#'+String(item.position||0).padStart(2,'0')+'</span><span class="cargo-state">'+state+'</span><span class="cargo-icon">'+esc(item.icon||'🚚')+'</span></div><div class="cargo-body"><small>'+esc(item.category||'Carga')+'</small><h3>'+esc(item.title||'Trabalho GAT')+'</h3><div class="cargo-meta"><span>≥ 500 KM</span><span>'+(done?'REPETIÇÃO = SÓ XP':'CATEGORIA = GUIA')+'</span><span>20 XP / 100 KM</span></div></div><button class="cargo-select" type="button" '+((selected||locked||!own())?'disabled':'')+'>'+button+'</button>';card.querySelector('button').onclick=()=>select(item);root.appendChild(card)});renderSuggestion()}
  async function refresh(){top();const d=await getCatalog();const status=document.getElementById(STATUS);if(!d){if(status)status.textContent='Catálogo aguardando a Central GAT.';render();validation();return}items=Array.isArray(d.catalog)?d.catalog:[];render();validation();if(status)status.textContent=(current()?'1 trabalho selecionado • ':'')+items.filter(x=>x.completed).length+' / 30 concluídos • pegue uma carga no ETS2 para receber uma sugestão automática • mínimo 500 km.'}
  injectSuggestionStyle();document.addEventListener('DOMContentLoaded',refresh);window.addEventListener('gat-account-change',()=>setTimeout(refresh,300));setInterval(validation,1000);setInterval(refresh,5000);
})();
