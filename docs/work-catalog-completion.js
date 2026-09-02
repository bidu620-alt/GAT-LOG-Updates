(()=>{
  const CATALOG_URL='ets2-official-cargos.json';
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  let lastSig='',variants=new Map(),totalEntries=0,applying=false;

  const DLC_PT={
    'Launch':'Jogo base','Beyond the Baltic Sea':'Além do Mar Báltico','Heavy Cargo Pack':'Pacote de Cargas Pesadas',
    'High Power Cargo Pack':'Pacote de Cargas de Alta Potência','Scandinavia':'Escandinávia','Italia':'Itália','Greece':'Grécia',
    'Farm Machinery':'Máquinas Agrícolas','Forest Machinery':'Máquinas Florestais','Special Transport':'Transporte Especial',
    'Volvo Construction Equipment':'Equipamentos de Construção Volvo','Bobcat Cargo Pack':'Pacote de Cargas Bobcat',
    'JCB Equipment Pack':'Pacote de Equipamentos JCB','KRONE Agriculture Equipment':'Equipamentos Agrícolas KRONE'
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
.full-cargo-card.catalog-completed{border-color:#1f8a5b!important;box-shadow:0 0 0 1px rgba(31,138,91,.24),0 14px 34px rgba(0,0,0,.18)!important;background:linear-gradient(180deg,rgba(15,69,49,.22),rgba(7,20,17,.35))!important}
.full-cargo-card.catalog-completed .cargo-visual{background:linear-gradient(135deg,rgba(20,111,73,.45),rgba(8,39,29,.25))!important;border-bottom-color:#1d6d4d!important}
.full-cargo-card.catalog-completed .cargo-state{background:#123d2d!important;color:#75efb6!important;border-color:#237454!important}
.full-cargo-card.catalog-completed .cargo-number{color:#75efb6!important}
.full-cargo-card.catalog-completed .cargo-body h3{color:#dfffee!important}
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

  function expandVariants(){
    if(!variants.size)return;
    const root=document.getElementById('workCatalogGrid');if(!root)return;
    [...root.querySelectorAll('.full-cargo-card:not([data-variant-expanded])')].forEach(card=>{
      const name=officialName(card),group=variants.get(norm(name));
      if(!group||group.length<2){card.dataset.variantExpanded='single';return}
      group.forEach(v=>{
        const clone=card.cloneNode(true);
        clone.dataset.variantExpanded='1';
        clone.dataset.cargoVariantWeight=String(v.weight||'');
        clone.dataset.cargoVariantDlc=String(v.dlc||'');
        clone.dataset.cargoKey=norm(v.name)+'|'+norm(v.dlc)+'|'+norm(v.weight);
        clone.title='Nome oficial SCS: '+v.name;
        const dlc=clone.querySelector('.cargo-dlc');if(dlc)dlc.textContent=dlcPt(v.dlc)||'Jogo base / oficial';
        const weight=clone.querySelector('.cargo-weight');if(weight)weight.textContent=v.weight?'• '+v.weight+' t':'';
        card.parentNode.insertBefore(clone,card);
      });
      card.remove();
    });
    [...root.querySelectorAll('.full-cargo-card')].forEach((card,i)=>{
      const n=card.querySelector('.cargo-number'),desired='#'+String(i+1).padStart(3,'0');if(n&&n.textContent!==desired)n.textContent=desired;
    });
    const count=document.getElementById('fullCargoCount');
    if(count&&totalEntries){
      count.textContent=count.textContent.replace(/\d+ cargas no catálogo/,totalEntries+' cargas no catálogo');
      if(!root.querySelector('.full-cargo-more')&&/encontradas/.test(count.textContent)){
        count.textContent=count.textContent.replace(/^\d+ encontradas/,root.querySelectorAll('.full-cargo-card').length+' encontradas');
      }
    }
  }

  function completedCards(){
    const cards=[...document.querySelectorAll('#workCatalogGrid .full-cargo-card')],rows=history(),done=new Set();
    rows.forEach(row=>{
      const named=cards.filter(card=>cardNames(card).includes(row.name));
      if(!named.length)return;
      const dup=named.filter(card=>card.dataset.cargoVariantWeight!==undefined&&card.dataset.cargoVariantWeight!=='');
      if(dup.length>1&&row.weightT>0){
        const candidates=dup.filter(card=>weightMatch(card.dataset.cargoVariantWeight,row.weightT));
        if(candidates.length){
          candidates.sort((a,b)=>{
            const ra=parseWeight(a.dataset.cargoVariantWeight),rb=parseWeight(b.dataset.cargoVariantWeight);
            return ((ra?.max||999)-(ra?.min||0))-((rb?.max||999)-(rb?.min||0));
          });
          done.add(candidates[0]);return;
        }
      }
      if(named.length===1)done.add(named[0]);
      else if(row.weightT<=0)named.forEach(card=>done.add(card));
    });
    return done;
  }

  function apply(){
    if(applying)return;applying=true;
    try{
      ensureStyle();expandVariants();
      const done=completedCards();
      document.querySelectorAll('#workCatalogGrid .full-cargo-card').forEach(card=>{
        cleanMeta(card);
        const completed=done.has(card);
        card.classList.toggle('catalog-completed',completed);
        const state=card.querySelector('.cargo-state'),desired=completed?'✓ CONCLUÍDA':'OFICIAL';
        if(state&&state.textContent!==desired)state.textContent=desired;
      });
    }finally{applying=false}
  }

  async function loadVariants(){
    try{
      const r=await fetch(CATALOG_URL+'?v=variants-2',{cache:'no-store'}),data=await r.json();if(!r.ok)return;
      const all=[];Object.values(data?.categories||{}).forEach(rows=>{if(Array.isArray(rows))rows.forEach(x=>{if(x?.name)all.push({name:String(x.name),dlc:String(x.dlc||''),weight:String(x.weight||'')})})});
      totalEntries=Number(data?.total_entries)||all.length;
      const groups=new Map();all.forEach(x=>{const k=norm(x.name);if(!groups.has(k))groups.set(k,[]);groups.get(k).push(x)});
      variants=new Map([...groups].filter(([,rows])=>rows.length>1));
      apply();
    }catch(_){}
  }

  function start(){
    ensureStyle();
    const root=document.getElementById('workCatalogGrid');
    if(root)new MutationObserver(()=>setTimeout(apply,0)).observe(root,{childList:true,subtree:false});
    lastSig=signature();apply();loadVariants();
    setInterval(()=>{
      const sig=signature();if(sig!==lastSig){lastSig=sig;apply();return}
      document.querySelectorAll('.cargo-reg-lite').forEach(el=>el.remove());
    },700);
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
