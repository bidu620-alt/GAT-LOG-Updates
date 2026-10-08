(()=>{
  let catalog=[];
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  const id=v=>String(v||'').trim().toLowerCase().replace(/^cargo\./,'');
  function rawOf(row){
    if(row?.raw_json&&typeof row.raw_json==='object')return row.raw_json;
    try{return JSON.parse(row?.raw_json||'{}')}catch(_){return {}}
  }
  function idOf(row){
    const raw=rawOf(row),t=row?.telemetry||row||{};
    return id(row?.cargo_id||row?.cargoId||t?.cargo_id||t?.cargoId||t?.job?.cargoId||t?.job?.cargo?.id||t?.game?.job?.cargoId||raw?.cargo_identity?.cargo_id_raw||raw?.mission?.cargo_id||raw?.cargo_id||raw?.job?.cargoId||'');
  }
  function names(item){return [item?.namePt,item?.name,...(item?.aliases||[])].map(norm).filter(Boolean)}
  function match(items,row){
    const cargoId=idOf(row);
    if(cargoId)return items.filter(item=>(item.cargoIds||[]).some(value=>id(value)===cargoId));
    const name=norm(row?.cargo_name_raw||row?.cargo||row?.cargo_name||row?.name);
    if(!name)return [];
    const canonical=items.filter(item=>[item.namePt,item.name].map(norm).includes(name));
    return canonical.length?canonical:items.filter(item=>names(item).includes(name));
  }
  function unique(items,row){
    const found=match(items,row);
    if(found.length===1)return found[0];
    // Old histories may not have IDs. Never guess between same-name cargo types.
    return null;
  }
  globalThis.GatCargoIdentity={norm,id,idOf,names,match,unique,setCatalog:items=>{catalog=items},getCatalog:()=>catalog};
})();
