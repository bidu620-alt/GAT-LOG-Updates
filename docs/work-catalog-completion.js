(()=>{
  const CATALOG_URL='ets2-official-cargos.json';
  const ICON_DATA_URL='assets/cargo/cargo-icon-defs.json?v=1';
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  let lastSig='',variants=new Map(),totalEntries=0,applying=false;
  let iconDefs=[],iconPos={},sheets=[],tile=[160,128],iconsReady=false,sheetReady=new Set();

  const DLC_PT={
    'Launch':'Jogo base','Beyond the Baltic Sea':'Além do Mar Báltico','Heavy Cargo Pack':'Pacote de Cargas Pesadas',
    'High Power Cargo Pack':'Pacote de Cargas de Alta Potência','Scandinavia':'Escandinávia','Italia':'Itália','Greece':'Grécia',
    'Farm Machinery':'Máquinas Agrícolas','Forest Machinery':'Máquinas Florestais','Special Transport':'Transporte Especial',
    'Volvo Construction Equipment':'Equipamentos de Construção Volvo','Bobcat Cargo Pack':'Pacote de Cargas Bobcat',
    'JCB Equipment Pack':'Pacote de Equipamentos JCB','KRONE Agriculture Equipment':'Equipamentos Agrícolas KRONE'
  };
  const DLC_SOURCE={
    'bobcat cargo pack':'dlc_bobcat','pacote de cargas bobcat':'dlc_bobcat',
    'jcb equipment pack':'dlc_jcb','pacote de equipamentos jcb':'dlc_jcb',
    'krone agriculture equipment':'dlc_krone_agriculture','equipamentos agricolas krone':'dlc_krone_agriculture',
    'volvo construction equipment':'dlc_volvo_construction','equipamentos de construcao volvo':'dlc_volvo_construction',
    'farm machinery':'dlc_farm_machinery','maquinas agricolas':'dlc_farm_machinery',
    'heavy cargo pack':'dlc_heavy_cargo','pacote de cargas pesadas':'dlc_heavy_cargo',
    'special transport':'dlc_oversize','transporte especial':'dlc_oversize',
    'greece':'dlc_greece','grecia':'dlc_greece'
  };

  function dlcPt(v){
    let out=String(v||'').trim();
    Object.entries(DLC_PT).sort((a,b)=>b[0].length-a[0].length).forEach(([en,pt])=>{out=out.replace(en,pt)});
    return out;
  }

  function currentProfile(){try{return typeof profile!=='undefined'?profile:null}catch(_){return null}}
  function history(){
    const p=currentProfile(),rows=Array.isArray(p?.deliveries)?p.deliveries:[];
    return rows.map(x=>({name:norm(x?.cargo||x?.cargo_name||x?.name),weightT:(Number(x?.weight_kg)||0)/1000})).filter(x=>x.name);
  }
  function signature(){return history().map(x=>x.name+'@'+x.weightT.toFixed(2)).sort().join('|')}

  function ensureStyle(){
    if(document.getElementById('gatCargoCompletionStyle'))return;
    const s=document.createElement('style');
    s.id='gatCargoCompletionStyle';
    s.textContent=`
.cargo-reg-lite{display:none!important}
.full-cargo-card{overflow:hidden!important;border:1px solid rgba(57,134,181,.58)!important;background:linear-gradient(180deg,#0a1722 0%,#07111a 100%)!important;box-shadow:0 15px 32px rgba(0,0,0,.20)!important;transition:border-color .2s ease,box-shadow .2s ease,transform .2s ease!important}
.full-cargo-card:hover{transform:translateY(-2px)!important;border-color:rgba(82,174,230,.78)!important;box-shadow:0 18px 38px rgba(0,0,0,.27)!important}
.full-cargo-card .cargo-visual{position:relative!important;height:174px!important;min-height:174px!important;overflow:hidden!important;border-bottom:1px solid rgba(62,143,190,.34)!important;background:radial-gradient(circle at 50% 52%,rgba(34,129,181,.24),transparent 54%),linear-gradient(145deg,#0d2637 0%,#081722 70%)!important}
.full-cargo-card .cargo-visual:before{content:"";position:absolute;inset:0;background:linear-gradient(115deg,rgba(255,255,255,.035),transparent 38%,rgba(39,154,218,.055));pointer-events:none}
.full-cargo-card .cargo-number,.full-cargo-card .cargo-state{z-index:4!important}
.full-cargo-card .cargo-number{font-weight:900!important;letter-spacing:.02em!important;color:#8fc8e8!important}
.full-cargo-card .cargo-state{border:1px solid rgba(105,173,213,.22)!important;background:rgba(3,14,22,.64)!important;color:#b9cddd!important;font-weight:900!important;letter-spacing:.04em!important}
.full-cargo-card .cargo-thumb{position:absolute;z-index:2;left:50%;top:52%;width:160px;height:128px;transform:translate(-50%,-50%) scale(1.08);background-repeat:no-repeat;filter:drop-shadow(0 12px 12px rgba(0,0,0,.48));image-rendering:auto;pointer-events:none}
.full-cargo-card.has-cargo-thumb .cargo-icon{display:none!important}
.full-cargo-card .cargo-body{padding-top:18px!important}
.full-cargo-card .cargo-body small{display:flex!important;gap:6px!important;align-items:center!important;flex-wrap:wrap!important;color:#7fb6d7!important;font-weight:800!important;text-transform:uppercase!important;letter-spacing:.025em!important}
.full-cargo-card .cargo-body h3{font-size:1.04rem!important;line-height:1.25!important;margin-top:8px!important;color:#f5f9fc!important}
.full-cargo-card .cargo-meta{margin-top:14px!important}
.full-cargo-card .cargo-meta span{border:1px solid rgba(67,145,191,.46)!important;background:rgba(5,22,32,.50)!important;color:#77b7df!important;border-radius:8px!important;font-weight:800!important}
.full-cargo-card.current:not(.catalog-completed){border-color:#1fa6ff!important;box-shadow:0 0 0 1px rgba(31,166,255,.30),0 18px 42px rgba(0,122,216,.22)!important}
.full-cargo-card.current:not(.catalog-completed) .cargo-visual{background:radial-gradient(circle at 50% 50%,rgba(29,152,230,.35),transparent 56%),linear-gradient(145deg,#0e3150,#081a2a)!important;border-bottom-color:#168edb!important}
.full-cargo-card.current:not(.catalog-completed) .cargo-state{background:#138bd3!important;border-color:#35b0fa!important;color:white!important}
.full-cargo-card.current:not(.catalog-completed) .cargo-number{color:#43b9ff!important}
.full-cargo-card.current:not(.catalog-completed) .cargo-body small{color:#31aaf3!important}
.full-cargo-card.catalog-completed{border-color:#31b867!important;box-shadow:0 0 0 1px rgba(49,184,103,.24),0 18px 40px rgba(4,88,45,.20)!important;background:linear-gradient(180deg,#0a2018 0%,#07140f 100%)!important}
.full-cargo-card.catalog-completed .cargo-visual{background:radial-gradient(circle at 50% 52%,rgba(45,180,101,.27),transparent 54%),linear-gradient(145deg,#0d3324,#091a14)!important;border-bottom-color:#25844f!important}
.full-cargo-card.catalog-completed .cargo-state{background:#123d2d!important;color:#75efb6!important;border-color:#237454!important}
.full-cargo-card.catalog-completed .cargo-number{color:#75efb6!important}
.full-cargo-card.catalog-completed .cargo-body small{color:#55c982!important}
.full-cargo-card.catalog-completed .cargo-body h3{color:#e8fff1!important}
.full-cargo-card.catalog-completed .cargo-meta span{border-color:rgba(48,168,94,.50)!important;color:#69df96!important;background:rgba(8,42,27,.45)!important}
@media(max-width:720px){.full-cargo-card .cargo-visual{height:158px!important;min-height:158px!important}.full-cargo-card .cargo-thumb{transform:translate(-50%,-50%) scale(.98)}}
`;
    document.head.appendChild(s);
  }

  function cleanMeta(card){
    card.querySelectorAll('.cargo-meta span').forEach(el=>{if(norm(el.textContent)==='catalogo em portugues')el.remove()});
    card.querySelectorAll('.cargo-reg-lite').forEach(el=>el.remove());
  }

  function officialName(card){
    const title=String(card.getAttribute('title')||'');
    return title.replace(/^Nome oficial SCS:\s*/i,'').trim();
  }
  function cardNames(card){
    const pt=norm(card.querySelector('.cargo-body h3')?.textContent||'');
    const official=norm(officialName(card));
    return [pt,official].filter(Boolean);
  }

  function parseWeight(spec){
    const nums=String(spec||'').replace(',','.').match(/\d+(?:\.\d+)?/g)?.map(Number)||[];
    if(!nums.length)return null;
    return nums.length===1?{min:nums[0],max:nums[0]}:{min:Math.min(nums[0],nums[1]),max:Math.max(nums[0],nums[1])};
  }
  function weightMatch(spec,t){
    if(!(t>0))return false;
    const r=parseWeight(spec);if(!r)return false;
    return t>=r.min-.6&&t<=r.max+.6;
  }
  function cardWeight(card){
    const spec=card.dataset.cargoVariantWeight||card.querySelector('.cargo-weight')?.textContent||'';
    const r=parseWeight(spec);return r?((r.min+r.max)/2):0;
  }
  function sourceHint(card){
    const raw=norm(card.dataset.cargoVariantDlc||card.querySelector('.cargo-dlc')?.textContent||'');
    if(!raw)return '';
    for(const [k,v] of Object.entries(DLC_SOURCE))if(raw.includes(k))return v;
    return '';
  }

  function expandKey(v){
    return norm(v)
      .replace(/\bconcr\b/g,'concrete').replace(/\bcent\b/g,'centring').replace(/\batl\b/g,'atlantic')
      .replace(/\bflt\b/g,'fillet').replace(/\bcont\b/g,'container').replace(/\bmed\b/g,'medical')
      .replace(/\bexc\b/g,'excavator').replace(/\bwload\b/g,'wheel loader').replace(/\bbhl\b/g,'backhoe loader')
      .replace(/\bmexc\b/g,'mini excavator').replace(/\bft\b/g,'tractor').replace(/\btr\b/g,'truck')
      .replace(/\bfrsh\b/g,'fresh').replace(/\bfroz\b/g,'frozen').replace(/\bmtl\b/g,'metal')
      .replace(/\balu\b/g,'aluminium').replace(/\bhi\b/g,'high').replace(/\bvolt\b/g,'voltage')
      .replace(/\bpwdr\b/g,'powder').replace(/\bpackag\b/g,'packaging').replace(/\bprotec\b/g,'protective')
      .replace(/\bwshavings\b/g,'wood shavings').replace(/\bnonalco\b/g,'non alcoholic').replace(/\s+/g,' ').trim();
  }
  function words(v){return new Set(expandKey(v).split(' ').filter(Boolean))}
  function jaccard(a,b){
    const A=words(a),B=words(b);if(!A.size||!B.size)return 0;let i=0;A.forEach(x=>{if(B.has(x))i++});return i/(A.size+B.size-i);
  }
  function dice(a,b){
    a=expandKey(a).replace(/\s/g,'');b=expandKey(b).replace(/\s/g,'');if(a===b)return 1;if(a.length<2||b.length<2)return 0;
    const m=new Map();for(let i=0;i<a.length-1;i++){const g=a.slice(i,i+2);m.set(g,(m.get(g)||0)+1)}
    let hit=0;for(let i=0;i<b.length-1;i++){const g=b.slice(i,i+2),n=m.get(g)||0;if(n){hit++;m.set(g,n-1)}}
    return 2*hit/((a.length-1)+(b.length-1));
  }
  function nameScore(name,d){
    const a=expandKey(name),b=expandKey(d.token),c=expandKey(d.id);if(!a)return -999;
    if(a===b)return 1000;
    let s=dice(a,b)*240+jaccard(a,b)*180;
    if(a===c)s+=320;
    if(a.includes(b)||b.includes(a))s+=75;
    const nums=a.split(' ').filter(x=>/\d/.test(x));if(nums.length){const hay=b+' '+c;const ok=nums.filter(x=>hay.includes(x)).length;s+=ok*75-(nums.length-ok)*95}
    for(const brand of ['bobcat','jcb','volvo','krone','daf','scania','iveco'])if(a.includes(brand)){s+=(b+' '+c).includes(brand)?90:-80}
    return s;
  }
  function resolveDefinition(card){
    if(!iconsReady||!iconDefs.length)return null;
    const name=officialName(card)||card.querySelector('.cargo-body h3')?.textContent||'';
    const hint=sourceHint(card),target=cardWeight(card);
    let best=null,bestScore=-1e9;
    for(const d of iconDefs){
      let s=nameScore(name,d);
      if(hint)s+=d.source===hint?115:(d.source==='base'?0:-35);
      if(target>0&&d.mass_t>0){const ratio=Math.abs(Math.log((target+.2)/(d.mass_t+.2)));s+=Math.max(-35,35-ratio*38)}
      if(s>bestScore){bestScore=s;best=d}
    }
    return bestScore>=120?best:null;
  }
  function applyThumb(card){
    if(!iconsReady)return;
    const d=resolveDefinition(card);if(!d)return;
    const p=iconPos[d.icon]||iconPos.fallback;if(!p||!sheetReady.has(p.s))return;const sh=sheets[p.s];if(!sh)return;
    let el=card.querySelector('.cargo-thumb');if(!el){el=document.createElement('span');el.className='cargo-thumb';card.querySelector('.cargo-visual')?.appendChild(el)}
    if(!el)return;
    el.style.backgroundImage="url('assets/cargo/"+sh.file+"?v=1')";el.style.backgroundSize=sh.width+'px '+sh.height+'px';
    el.style.backgroundPosition=(-p.x*tile[0])+'px '+(-p.y*tile[1])+'px';
    el.dataset.icon=d.icon;card.dataset.cargoIcon=d.icon;card.classList.add('has-cargo-thumb');
  }

  function expandVariants(){
    if(!variants.size)return;
    const root=document.getElementById('workCatalogGrid');if(!root)return;
    [...root.querySelectorAll('.full-cargo-card:not([data-variant-expanded])')].forEach(card=>{
      const name=officialName(card),group=variants.get(norm(name));
      if(!group||group.length<2){card.dataset.variantExpanded='single';return}
      group.forEach(v=>{
        const clone=card.cloneNode(true);
        clone.dataset.variantExpanded='1';clone.dataset.cargoVariantWeight=String(v.weight||'');clone.dataset.cargoVariantDlc=String(v.dlc||'');
        clone.dataset.cargoKey=norm(v.name)+'|'+norm(v.dlc)+'|'+norm(v.weight);clone.title='Nome oficial SCS: '+v.name;
        clone.querySelector('.cargo-thumb')?.remove();clone.classList.remove('has-cargo-thumb');
        const dlc=clone.querySelector('.cargo-dlc');if(dlc)dlc.textContent=dlcPt(v.dlc)||'Jogo base / oficial';
        const weight=clone.querySelector('.cargo-weight');if(weight)weight.textContent=v.weight?'• '+v.weight+' t':'';
        card.parentNode.insertBefore(clone,card);
      });
      card.remove();
    });
    [...root.querySelectorAll('.full-cargo-card')].forEach((card,i)=>{const n=card.querySelector('.cargo-number'),desired='#'+String(i+1).padStart(3,'0');if(n&&n.textContent!==desired)n.textContent=desired});
    const count=document.getElementById('fullCargoCount');
    if(count&&totalEntries){count.textContent=count.textContent.replace(/\d+ cargas no catálogo/,totalEntries+' cargas no catálogo');if(!root.querySelector('.full-cargo-more')&&/encontradas/.test(count.textContent))count.textContent=count.textContent.replace(/^\d+ encontradas/,root.querySelectorAll('.full-cargo-card').length+' encontradas')}
  }

  function completedCards(){
    const cards=[...document.querySelectorAll('#workCatalogGrid .full-cargo-card')],rows=history(),done=new Set();
    rows.forEach(row=>{
      const named=cards.filter(card=>cardNames(card).includes(row.name));if(!named.length)return;
      const dup=named.filter(card=>card.dataset.cargoVariantWeight!==undefined&&card.dataset.cargoVariantWeight!=='');
      if(dup.length>1&&row.weightT>0){const candidates=dup.filter(card=>weightMatch(card.dataset.cargoVariantWeight,row.weightT));if(candidates.length){candidates.sort((a,b)=>{const ra=parseWeight(a.dataset.cargoVariantWeight),rb=parseWeight(b.dataset.cargoVariantWeight);return ((ra?.max||999)-(ra?.min||0))-((rb?.max||999)-(rb?.min||0))});done.add(candidates[0]);return}}
      if(named.length===1)done.add(named[0]);else if(row.weightT<=0)named.forEach(card=>done.add(card));
    });
    return done;
  }

  function apply(){
    if(applying)return;applying=true;
    try{
      ensureStyle();expandVariants();const done=completedCards();
      document.querySelectorAll('#workCatalogGrid .full-cargo-card').forEach(card=>{
        cleanMeta(card);const completed=done.has(card);card.classList.toggle('catalog-completed',completed);applyThumb(card);
        const state=card.querySelector('.cargo-state');const current=card.classList.contains('current');const desired=current?'CARGA ATUAL':(completed?'✓ CONCLUÍDA':'OFICIAL');if(state&&state.textContent!==desired)state.textContent=desired;
      });
    }finally{applying=false}
  }

  async function loadVariants(){
    try{
      const r=await fetch(CATALOG_URL+'?v=variants-3',{cache:'no-store'}),data=await r.json();if(!r.ok)return;
      const all=[];Object.values(data?.categories||{}).forEach(rows=>{if(Array.isArray(rows))rows.forEach(x=>{if(x?.name)all.push({name:String(x.name),dlc:String(x.dlc||''),weight:String(x.weight||'')})})});
      totalEntries=Number(data?.total_entries)||all.length;const groups=new Map();all.forEach(x=>{const k=norm(x.name);if(!groups.has(k))groups.set(k,[]);groups.get(k).push(x)});variants=new Map([...groups].filter(([,rows])=>rows.length>1));apply();
    }catch(_){}
  }
  async function loadIcons(){
    try{
      const r=await fetch(ICON_DATA_URL,{cache:'force-cache'}),data=await r.json();if(!r.ok)return;
      iconDefs=Array.isArray(data?.definitions)?data.definitions:[];iconPos=data?.icons||{};sheets=Array.isArray(data?.sheets)?data.sheets:[];tile=data?.tile||tile;iconsReady=!!iconDefs.length&&!!sheets.length;
      if(iconsReady)sheets.forEach((sh,i)=>{const img=new Image();img.onload=()=>{sheetReady.add(i);apply()};img.onerror=()=>{};img.src='assets/cargo/'+sh.file+'?v=1'});
      apply();
    }catch(_){}
  }

  function start(){
    ensureStyle();const root=document.getElementById('workCatalogGrid');if(root)new MutationObserver(()=>setTimeout(apply,0)).observe(root,{childList:true,subtree:false});
    lastSig=signature();apply();loadVariants();loadIcons();
    setInterval(()=>{const sig=signature();if(sig!==lastSig){lastSig=sig;apply();return}document.querySelectorAll('.cargo-reg-lite').forEach(el=>el.remove());if(iconsReady)document.querySelectorAll('#workCatalogGrid .full-cargo-card:not(.has-cargo-thumb)').forEach(applyThumb)},900);
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
