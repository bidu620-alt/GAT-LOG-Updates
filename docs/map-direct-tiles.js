// GAT-LOG • mapa Base em tiles PNG diretos do GitHub Pages.
// Evita abrir/decompactar ZIP no navegador e mantém o mapa visível mesmo acima do zoom nativo.
(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined')return;

  const cfg=(window.GAT_MAP_CONFIG||{}).base||{};
  if(cfg.type!=='gat-direct-tiles'||!cfg.tileUrl)return;

  const oldGameToLatLng=gameToLatLng;
  const oldApplyLayerForMap=applyLayerForMap;
  const TILE_SIZE=Number(cfg.tileSize||256);
  // O exportador GAT só possui PNGs até este nível (normalmente 6).
  // Leaflet pode continuar aproximando até o zoom do mapa (8), ampliando o último tile nativo.
  const NATIVE_MAX_ZOOM=Number(cfg.maxZoom??6);
  const DISPLAY_MAX_ZOOM=Math.max(NATIVE_MAX_ZOOM,Number(liveMap.options?.maxZoom??8),8);
  const MIN_ZOOM=Number(cfg.minZoom??0);
  const WORLD_PX=TILE_SIZE*Math.pow(2,NATIVE_MAX_ZOOM);
  const gameBounds=cfg.gameBounds||null;
  const directBounds=L.latLngBounds(
    liveMap.unproject([0,WORLD_PX],NATIVE_MAX_ZOOM),
    liveMap.unproject([WORLD_PX,0],NATIVE_MAX_ZOOM)
  );

  // Os tiles entram imediatamente, sem animação de opacidade entre níveis.
  try{liveMap._fadeAnimated=false}catch(_){}

  function note(text){
    const el=document.getElementById('mapBaseNote');
    if(el)el.textContent=text;
  }

  function baseSelected(){
    return currentMap==='base'||currentMap==='promods';
  }

  function gameToDirectLatLng(x,z){
    const b=gameBounds;
    if(!b||![b.xMin,b.xMax,b.zMin,b.zMax].every(v=>Number.isFinite(Number(v))))return oldGameToLatLng(x,z);
    const nx=(Number(x)-Number(b.xMin))/(Number(b.xMax)-Number(b.xMin));
    const nz=(Number(z)-Number(b.zMin))/(Number(b.zMax)-Number(b.zMin));
    const px=Math.max(0,Math.min(WORLD_PX,nx*WORLD_PX));
    const py=Math.max(0,Math.min(WORLD_PX,nz*WORLD_PX));
    return liveMap.unproject([px,py],NATIVE_MAX_ZOOM);
  }

  gameToLatLng=function(x,z){
    return baseSelected()?gameToDirectLatLng(x,z):oldGameToLatLng(x,z);
  };

  const directLayer=L.tileLayer(cfg.tileUrl,{
    tileSize:TILE_SIZE,
    minZoom:MIN_ZOOM,
    maxZoom:DISPLAY_MAX_ZOOM,
    minNativeZoom:MIN_ZOOM,
    maxNativeZoom:NATIVE_MAX_ZOOM,
    bounds:directBounds,
    noWrap:true,
    keepBuffer:12,
    updateWhenIdle:false,
    updateWhenZooming:true,
    updateInterval:80,
    pane:'tilePane'
  });

  directLayer.on('tileload',e=>{
    if(e&&e.tile){
      e.tile.style.opacity='1';
      e.tile.style.transition='none';
      e.tile.style.backfaceVisibility='hidden';
    }
  });

  function removeLegacyLayers(){
    try{if(typeof baseLayer!=='undefined'&&liveMap.hasLayer(baseLayer))liveMap.removeLayer(baseLayer)}catch(_){}
    if(activeImageLayer){
      try{liveMap.removeLayer(activeImageLayer)}catch(_){}
      activeImageLayer=null;
    }
  }

  function showDirectLayer(fit){
    removeLegacyLayers();
    if(!liveMap.hasLayer(directLayer))directLayer.addTo(liveMap);
    liveMap.setMaxBounds(directBounds.pad(.035));
    document.getElementById('mapStage')?.classList.add('map-has-base-image','map-gat-export','map-gat-tiles');
    if(fit)liveMap.fitBounds(directBounds,{padding:[18,18],maxZoom:2});
    note(currentMap==='promods'?'PROMODS • MAPA BASE GAT COMO REFERÊNCIA • TILES DIRETOS':'MAPA BASE • TILES ORIGINAIS DIRETOS • POSIÇÃO GAT AO VIVO');
  }

  applyLayerForMap=function(){
    if(baseSelected()){
      const changed=activeVisualKey!=='gat-direct:'+currentMap;
      activeVisualKey='gat-direct:'+currentMap;
      showDirectLayer(changed&&firstPosition);
      return;
    }

    if(liveMap.hasLayer(directLayer))liveMap.removeLayer(directLayer);
    document.getElementById('mapStage')?.classList.remove('map-gat-tiles','map-gat-export');
    oldApplyLayerForMap();
  };

  // O mapa antigo pode ter consumido o primeiro foco antes deste script carregar.
  // Reinicia o foco para que, havendo motorista com posição, a tela abra nele.
  removeLegacyLayers();
  activeVisualKey='gat-direct:'+currentMap;
  firstPosition=true;
  showDirectLayer(false);
  liveMap.setView(directBounds.getCenter(),2);
  liveMap.invalidateSize();

  setTimeout(()=>{
    try{
      renderPins();
      if(firstPosition)liveMap.fitBounds(directBounds,{padding:[18,18],maxZoom:2});
    }catch(_){
      liveMap.fitBounds(directBounds,{padding:[18,18],maxZoom:2});
    }
  },0);
})();
