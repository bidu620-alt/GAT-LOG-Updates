(()=>{
  let hadCargo=false,refreshTimer1=null,refreshTimer2=null;
  const text=()=>String(document.getElementById('tripCargo')?.textContent||'').trim().toLowerCase();
  const loaded=()=>{const v=text();return !!v&&v!=='sem carga'&&v!=='aguardando'&&v!=='—'};
  const refreshAfterDelivery=()=>{
    clearTimeout(refreshTimer1);clearTimeout(refreshTimer2);
    refreshTimer1=setTimeout(()=>{try{if(typeof refreshProfileQuiet==='function')refreshProfileQuiet()}catch(_){}},1200);
    // A leitura de perfil da Central pode ficar em cache por alguns segundos.
    // Faz uma segunda confirmação apenas depois de uma entrega, sem polling contínuo.
    refreshTimer2=setTimeout(()=>{try{if(typeof refreshProfileQuiet==='function')refreshProfileQuiet()}catch(_){}},17000);
  };
  const check=()=>{
    const now=loaded();
    if(hadCargo&&!now)refreshAfterDelivery();
    hadCargo=now;
  };
  const start=()=>{
    const el=document.getElementById('tripCargo');if(!el)return;
    hadCargo=loaded();
    new MutationObserver(check).observe(el,{childList:true,subtree:true,characterData:true});
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
