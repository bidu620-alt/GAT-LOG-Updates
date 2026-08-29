// GAT-LOG • Mapa Base próprio
// Reconstrói a imagem WebP exportada do ETS2 a partir de pequenos arquivos de texto
// hospedados no próprio GitHub Pages e mantém a telemetria X/Z por cima dela.
(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined')return;

  const cfg=(window.GAT_MAP_CONFIG||{}).base||{};
  if(cfg.type!=='gat-export'||!Array.isArray(cfg.imageChunks)||!cfg.imageChunks.length)return;

  const originalApplyLayerForMap=applyLayerForMap;
  const gatBounds=imageBounds(cfg);
  let gatBaseLayer=null;
  let gatBaseUrl='';
  let loading=null;
  let loadFailed=false;

  if(!gatBounds)return;

  if(!liveMap.getPane('gatExportBase')){
    const pane=liveMap.createPane('gatExportBase');
    pane.style.zIndex='180';
    pane.style.pointerEvents='none';
  }

  function note(text){
    const el=document.getElementById('mapBaseNote');
    if(el)el.textContent=text;
  }

  function removeLegacyBase(){
    try{if(typeof baseLayer!=='undefined'&&liveMap.hasLayer(baseLayer))liveMap.removeLayer(baseLayer)}catch(_){}
  }

  function removeGatBase(){
    if(gatBaseLayer&&liveMap.hasLayer(gatBaseLayer))liveMap.removeLayer(gatBaseLayer);
  }

  async function loadGatBase(){
    if(gatBaseLayer)return gatBaseLayer;
    if(loading)return loading;

    loading=(async()=>{
      const parts=await Promise.all(cfg.imageChunks.map(async url=>{
        const r=await fetch(url,{cache:'no-cache'});
        if(!r.ok)throw new Error('Falha ao carregar parte do mapa: '+url);
        return (await r.text()).trim();
      }));

      const b64=parts.join('').replace(/\s+/g,'');
      const raw=atob(b64);
      if(raw.length<12||raw.slice(0,4)!=='RIFF'||raw.slice(8,12)!=='WEBP'){
        throw new Error('Imagem reconstruída do mapa Base é inválida.');
      }
      const bytes=new Uint8Array(raw.length);
      for(let i=0;i<raw.length;i++)bytes[i]=raw.charCodeAt(i);

      gatBaseUrl=URL.createObjectURL(new Blob([bytes],{type:cfg.imageMime||'image/webp'}));
      gatBaseLayer=L.imageOverlay(gatBaseUrl,gatBounds,{
        pane:'gatExportBase',
        interactive:false,
        opacity:1
      });
      return gatBaseLayer;
    })().catch(err=>{
      console.error('GAT mapa base:',err);
      loadFailed=true;
      return null;
    });

    return loading;
  }

  function showGatBase(fit){
    removeLegacyBase();
    if(!gatBaseLayer)return false;
    if(!liveMap.hasLayer(gatBaseLayer))gatBaseLayer.addTo(liveMap);
    liveMap.setMaxBounds(gatBounds.pad(.06));
    document.getElementById('mapStage')?.classList.add('map-has-base-image','map-gat-export');
    if(fit)liveMap.fitBounds(gatBounds,{padding:[18,18],maxZoom:3});
    if(currentMap==='promods')note('PROMODS • MAPA BASE GAT COMO REFERÊNCIA • POSIÇÃO AO VIVO');
    else note('MAPA BASE • EXPORTADO DO SEU ETS2 • POSIÇÃO GAT AO VIVO');
    return true;
  }

  applyLayerForMap=function(){
    if(currentMap==='base'||currentMap==='promods'){
      if(activeImageLayer){
        try{liveMap.removeLayer(activeImageLayer)}catch(_){}
        activeImageLayer=null;
      }
      removeLegacyBase();

      const key='gat-export:'+currentMap;
      if(gatBaseLayer){
        const changed=activeVisualKey!==key;
        showGatBase(changed&&firstPosition);
        activeVisualKey=key;
      }else{
        activeVisualKey='gat-export-loading:'+currentMap;
        note(loadFailed?'MAPA BASE • FALHA AO CARREGAR • USANDO RESERVA':'MAPA BASE • CARREGANDO EXPORTAÇÃO DO ETS2...');
        loadGatBase().then(layer=>{
          if(layer&&(currentMap==='base'||currentMap==='promods')){
            showGatBase(firstPosition);
            activeVisualKey='gat-export:'+currentMap;
            liveMap.invalidateSize();
          }else if(!layer&&loadFailed){
            try{baseLayer.addTo(liveMap);liveMap.setMaxBounds(mapBounds.pad(.08))}catch(_){}
          }
        });
      }
      return;
    }

    removeGatBase();
    document.getElementById('mapStage')?.classList.remove('map-gat-export');
    originalApplyLayerForMap();
  };

  // O mapa antigo é removido imediatamente; a imagem exportada entra uma única vez.
  removeLegacyBase();
  activeVisualKey='';
  loadGatBase().then(layer=>{
    if(layer&&(currentMap==='base'||currentMap==='promods')){
      showGatBase(true);
      activeVisualKey='gat-export:'+currentMap;
      firstPosition=false;
      liveMap.invalidateSize();
    }else if(!layer&&loadFailed){
      try{baseLayer.addTo(liveMap)}catch(_){}
    }
  });

  window.addEventListener('beforeunload',()=>{
    if(gatBaseUrl)URL.revokeObjectURL(gatBaseUrl);
  });
})();
