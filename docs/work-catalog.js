(()=>{
  const API='https://api.gatlogets2.com.br',GRID='workCatalogGrid',STATUS='workCatalogStatus';
  let items=[];
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const session=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const pageUser=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}const u=new URLSearchParams(location.search).get('u');return clean(u||session()?.user)};
  const own=()=>!!session()?.token&&clean(session().user)===pageUser();
  const adminTest=()=>pageUser()==='biduzao';
  const current=()=>{try{return typeof profile!=='undefined'?profile?.current_mission:null}catch(_){return null}};
  const live=()=>{try{return typeof lastLive!=='undefined'?lastLive:null}catch(_){return null}};
  const isFresh=t=>{try{if(typeof fresh==='function')return fresh(t)}catch(_){}const d=Date.parse(t?.updated_at||'');return Number.isFinite(d)&&Date.now()-d<20000};
  const liveCargo=t=>{try{if(typeof cargoOf==='function')return cargoOf(t)}catch(_){}return String(t?.cargo_name||t?.cargo||t?.telemetry?.job?.cargoName||'').trim()};
  const liveSource=t=>String(t?.source_city||t?.source||t?.telemetry?.job?.sourceCity||t?.telemetry?.job?.source?.cityName||'').trim();
  const liveDestination=t=>String(t?.destination_city||t?.destination||t?.telemetry?.job?.destinationCity||t?.telemetry?.job?.destination?.cityName||'').trim();
  const liveOnJob=t=>{if(!t)return false;const raw=t.telemetry||{},vals=[t.job_latched,raw.job_latched,t.on_job,raw.on_job,raw.onJob,raw.job?.onJob,raw.job?.active];return vals.some(v=>v===true||v===1||String(v).toLowerCase()==='true')||!!liveCargo(t)};

  function injectStyle(){
    if(document.getElementById('gatAutoCatalogStyle'))return;
    const s=document.createElement('style');s.id='gatAutoCatalogStyle';s.textContent=`
.auto-cargo-state{margin:12px 0 16px;border:1px solid #245a86;border-radius:14px;background:linear-gradient(135deg,#0b1c2b,#0a131c);padding:14px 16px;display:none}
.auto-cargo-state.show{display:block}.auto-cargo-state.ok{border-color:#237a52}.auto-cargo-state.pending{border-color:#8a6725}
.auto-cargo-head{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:8px}.auto-cargo-head b{font-size:12px;color:#78c4ff;letter-spacing:.04em}.auto-cargo-live{font-size:9px;font-weight:900;color:#5fe0aa}
.auto-cargo-name{font-size:17px;font-weight:950;color:#f2f8ff;margin-bottom:5px}.auto-cargo-route{font-size:10px;color:#8fa5ba;margin-bottom:12px}.auto-cargo-result{border-top:1px solid #18364f;padding-top:11px}.auto-cargo-result strong{display:block;font-size:12px;color:#fff}.auto-cargo-result small{display:block;margin-top:4px;color:#8fa5ba;font-size:9px;line-height:1.5}
.cargo-card.auto-active{outline:2px solid #2b8bd0;box-shadow:0 0 0 3px rgba(43,139,208,.12)}
`;
    document.head.appendChild(s);
    const legacy=document.getElementById('takeWorkButton');if(legacy)legacy.style.display='none';
  }
  function ensureValidation(){let box=document.getElementById('workValidationState');if(box)return box;const liveBox=document.querySelector('.work-live');if(!liveBox)return null;box=document.createElement('div');box.id='workValidationState';box.className='work-validation idle';liveBox.insertAdjacentElement('afterend',box);return box}
  function ensureAutoState(){let box=document.getElementById('workAutoCargoState');if(box)return box;box=document.createElement('div');box.id='workAutoCargoState';box.className='auto-cargo-state';const anchor=ensureValidation()||document.querySelector('.work-live');if(anchor)anchor.insertAdjacentElement('afterend',box);return box}

  function renderAutoState(){
    const box=ensureAutoState();if(!box)return;const t=live(),cargo=liveCargo(t),freshNow=isFresh(t),m=current(),src=liveSource(t),dst=liveDestination(t);
    if(!own()||!freshNow||!liveOnJob(t)||!cargo){box.className='auto-cargo-state';box.innerHTML='';return}
    let cls='',title='CLASSIFICANDO AUTOMATICAMENTE...',note='A Central GAT está comparando esta carga com os 30 trabalhos do mês.';
    if(m?.pending_classification){cls=' pending';title='CARGA A CLASSIFICAR';note='A viagem será registrada normalmente. Admin ou Moderador escolherá a categoria correta depois da entrega; você não precisa refazer a viagem.'}
    else if(m?.classification_mode==='automatic'){cls=' ok';title='✓ '+String(m.title||m.category||'Trabalho reconhecido');note=m.xp_only?'Esta categoria já foi concluída no mês. A viagem continua válida para XP, sem duplicar o x/30.':'Categoria reconhecida automaticamente. Ao concluir uma viagem válida, este trabalho ficará verde no catálogo.'}
    else if(m){cls=' ok';title=String(m.title||'MISSÃO GAT EM ANDAMENTO');note='Existe uma missão especial/administrativa vinculada a esta viagem.'}
    box.className='auto-cargo-state show'+cls;box.innerHTML='<div class="auto-cargo-head"><b>CARGA DETECTADA PELO GAT TELEMETRIA</b><span class="auto-cargo-live">● AO VIVO</span></div><div class="auto-cargo-name">'+esc(cargo)+'</div><div class="auto-cargo-route">'+esc(src||'Origem detectada')+' → '+esc(dst||'Destino detectado')+'</div><div class="auto-cargo-result"><strong>'+esc(title)+'</strong><small>'+esc(note)+'</small></div>';
  }

  function validation(){
    const box=ensureValidation();if(!box)return;const m=current(),t=live(),cargo=liveCargo(t),freshNow=isFresh(t),state=String(m?.state||'').toLowerCase();let cls='idle',icon='○',title='CATÁLOGO AUTOMÁTICO',detail='Pegue qualquer carga no ETS2. Não é necessário escolher um trabalho no site.';
    if(freshNow&&liveOnJob(t)&&cargo){cls='checking';icon='…';title='CARGA DETECTADA';detail=cargo+' • classificação automática em andamento.'}
    if(m?.pending_classification){cls='checking';icon='…';title='AGUARDANDO CLASSIFICAÇÃO';detail=(cargo?cargo+' • ':'')+'A viagem continuará sendo registrada e ficará disponível para Admin/Moderador classificar após a entrega.'}
    else if(m?.classification_mode==='automatic'){cls=state==='active'?'valid':'waiting';icon=state==='active'?'✓':'●';title=m.xp_only?'CATEGORIA RECONHECIDA • SOMENTE XP':'CATEGORIA RECONHECIDA AUTOMATICAMENTE';detail=(m.title||m.category||'Trabalho GAT')+(state==='active'?' • viagem em andamento.':' • aguardando a Telemetria iniciar a viagem.')}
    else if(m){cls=state==='active'?'valid':'waiting';icon=state==='active'?'✓':'●';title='MISSÃO GAT';detail=(m.title||'Missão especial')+' • '+(state==='active'?'em andamento.':'aguardando início.')}
    box.className='work-validation '+cls;box.innerHTML='<span class="work-validation-icon">'+icon+'</span><div><b>'+esc(title)+'</b><small>'+esc(detail)+'</small></div>';renderAutoState();
  }

  function top(){const m=current(),set=(id,t)=>{const e=document.getElementById(id);if(e)e.textContent=t};set('workMarket','Classificação automática');set('workMinKm',adminTest()?'MODO TESTE ADMIN':'500 km mínimos');set('workWeight','Peso > 0');set('workFreedom',m?.pending_classification?'Aguardando classificação':m?.title||'Pegue qualquer carga no ETS2');const msg=document.getElementById('workOwnerMessage');if(msg){if(!own())msg.textContent='Este é um perfil público. O catálogo é preenchido automaticamente pelas entregas válidas do motorista.';else if(adminTest())msg.textContent='Modo de teste do proprietário ativo. Pegue qualquer carga no ETS2; o GAT classifica automaticamente ou envia para Cargas a Classificar.';else msg.textContent='Não escolha trabalho. Pegue qualquer carga no ETS2 e cumpra as regras da viagem. O GAT reconhece a categoria automaticamente; nomes desconhecidos ficam salvos para classificação do Admin/Moderador.'}validation()}
  async function getCatalog(){const user=pageUser();if(!user)return null;const c=new AbortController(),timer=setTimeout(()=>c.abort(),5500);try{const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(user),{cache:'no-store',signal:c.signal});const d=await r.json().catch(()=>null);return r.ok&&d?.ok?d:null}catch(_){return null}finally{clearTimeout(timer)}}
  function render(){const root=document.getElementById(GRID);if(!root)return;root.textContent='';const m=current(),active=String(m?.catalog_id||'');if(!items.length){root.innerHTML='<div class="catalog-hint">Catálogo aguardando a Central GAT.</div>';return}items.forEach(item=>{const selected=active===String(item.id),done=!!item.completed,card=document.createElement('article');card.dataset.workId=String(item.id||'');card.className='cargo-card'+(done?' completed':'')+(selected?' selected auto-active':'')+(item.custom?' custom-card':'');const state=done?'CONCLUÍDO':selected?(m?.xp_only?'REPETIÇÃO • XP':'EM ANDAMENTO'):'DISPONÍVEL';card.innerHTML='<div class="cargo-visual"><span class="cargo-number">#'+String(item.position||0).padStart(2,'0')+'</span><span class="cargo-state">'+state+'</span><span class="cargo-icon">'+esc(item.icon||'🚚')+'</span></div><div class="cargo-body"><small>'+esc(item.category||'Carga')+'</small><h3>'+esc(item.title||'Trabalho GAT')+'</h3><div class="cargo-meta"><span>≥ 500 KM</span><span>'+(done?'REPETIÇÃO = SÓ XP':'AUTOMÁTICO')+'</span><span>20 XP / 100 KM</span></div></div>';root.appendChild(card)})}
  async function refresh(){top();const d=await getCatalog(),status=document.getElementById(STATUS);if(!d){if(status)status.textContent='Catálogo aguardando a Central GAT.';render();validation();return}items=Array.isArray(d.catalog)?d.catalog:[];render();validation();if(status)status.textContent=items.filter(x=>x.completed).length+' / 30 concluídos • classificação automática pelas cargas entregues • mínimo 500 km.'}
  injectStyle();document.addEventListener('DOMContentLoaded',refresh);window.addEventListener('gat-account-change',()=>setTimeout(refresh,300));setInterval(validation,1000);setInterval(refresh,5000);
})();
