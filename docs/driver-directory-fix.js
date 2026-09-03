(()=>{
  const API='https://api.gatlogets2.com.br';
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const fmtInt=v=>Math.max(0,Math.trunc(Number(v)||0)).toLocaleString('pt-BR');
  const fmtKm=v=>Math.round(Math.max(0,Number(v)||0)).toLocaleString('pt-BR')+' km';
  let busy=false;

  async function fetchRanking(){
    const c=new AbortController(),timer=setTimeout(()=>c.abort(),5000);
    try{
      const r=await fetch(API+'/api/public/ranking',{cache:'no-store',signal:c.signal});
      if(!r.ok)return null;
      const data=await r.json().catch(()=>null);
      return data?.ok&&Array.isArray(data.ranking)?data.ranking:null;
    }catch(_){return null}finally{clearTimeout(timer)}
  }

  function renderCard(card,item){
    const meta=card.querySelector('.gat-driver-item-meta');
    if(!meta)return;
    const b=meta.querySelector('b'),small=meta.querySelector('small');
    const hasDeliveries=item&&Object.prototype.hasOwnProperty.call(item,'total_deliveries')&&Number.isFinite(Number(item.total_deliveries));
    const hasKm=item&&Object.prototype.hasOwnProperty.call(item,'total_km')&&Number.isFinite(Number(item.total_km));
    if(b){
      if(hasDeliveries){const d=Math.max(0,Math.trunc(Number(item.total_deliveries)));b.textContent=fmtInt(d)+' entrega'+(d===1?'':'s')}
      else b.textContent='— entregas';
    }
    if(small)small.textContent=hasKm?fmtKm(item.total_km):'— km';
  }

  async function refresh(){
    if(busy||document.hidden)return;
    const cards=[...document.querySelectorAll('#gatDriverList .gat-driver-item')];
    if(!cards.length)return;
    busy=true;
    try{
      const ranking=await fetchRanking();
      if(!ranking)return;
      const byUser=new Map(ranking.map(item=>[clean(item?.user),item]).filter(([u])=>u));
      for(const card of cards)renderCard(card,byUser.get(clean(card.dataset.user))||null);
    }finally{busy=false}
  }

  const start=()=>{
    const root=document.getElementById('gatDriverDirectory')||document.body;
    new MutationObserver(()=>{setTimeout(refresh,40)}).observe(root,{childList:true,subtree:true});
    refresh();
    setInterval(refresh,60000);
  };

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});
  else start();
})();
