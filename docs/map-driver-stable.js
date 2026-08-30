// GAT-LOG • mantém a lista lateral de motoristas estável entre atualizações.
(()=>{
  if(typeof filteredList!=='function')return;
  filteredList=function(){
    return mapDrivers
      .filter(d=>mapMatch(d)&&(currentFilter==='all'||(currentFilter==='trip'&&d.t?.on_job)||(currentFilter==='idle'&&!d.t?.on_job)))
      .slice()
      .sort((a,b)=>String(a?.name||'').localeCompare(String(b?.name||''),'pt-BR',{sensitivity:'base',numeric:true}));
  };
})();
