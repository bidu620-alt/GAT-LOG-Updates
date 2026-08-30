// GAT-LOG • camadas de tiles PNG diretos para mapas exportados.
(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined')return;

  const configs=window.GAT_MAP_CONFIG||{};
  const oldGameToLatLng=gameToLatLng;
  const oldApplyLayerForMap=applyLayerForMap;
  const directLayers=new Map();
  const directBoundsByKey=new Map();
  const DEFAULT_MAP_MAX_ZOOM=Number(liveMap.options?.maxZoom??8);

  try{liveMap._fadeAnimated=false}catch(_){}

  function directKey(){
    if(currentMap==='promods')return 'base';
    const cfg=configs[currentMap]||{};
    return cfg.type==='gat-direct-tiles'&&cfg.tileUrl?currentMap:'';
  }

  function getCfg(key){return configs[key]||{}}

  function worldPx(cfg){
    const tileSize=Number(cfg.tileSize||256);
    const nativeMax=Number(cfg.maxZoom??8);
    return tileSize*Math.pow(2,nativeMax);
  }

  function getBounds(key){
    if(directBoundsByKey.has(key))return directBoundsByKey.get(key);
    const cfg=getCfg(key),nativeMax=Number(cfg.maxZoom??8),size=worldPx(cfg);
    const bounds=L.latLngBounds(
      liveMap.unproject([0,size],nativeMax),
      liveMap.unproject([size,0],nativeMax)
    );
    directBoundsByKey.set(key,bounds);
    return bounds;
  }

  function gameToDirectLatLng(key,x,z){
    const cfg=getCfg(key),b=cfg.gameBounds;
    if(!b||![b.xMin,b.xMax,b.zMin,b.zMax].every(v=>Number.isFinite(Number(v))))return oldGameToLatLng(x,z);
    const size=worldPx(cfg),nativeMax=Number(cfg.maxZoom??8);
    const nx=(Number(x)-Number(b.xMin))/(Number(b.xMax)-Number(b.xMin));
    const nz=(Number(z)-Number(b.zMin))/(Number(b.zMax)-Number(b.zMin));
    const px=Math.max(0,Math.min(size,nx*size));
    const py=Math.max(0,Math.min(size,nz*size));
    return liveMap.unproject([px,py],nativeMax);
  }

  gameToLatLng=function(x,z){
    const key=directKey();
    return key?gameToDirectLatLng(key,x,z):oldGameToLatLng(x,z);
  };

  function getDisplayMax(cfg){
    const nativeMax=Number(cfg.maxZoom??8);
    return Math.max(nativeMax,Number(cfg.displayMaxZoom??DEFAULT_MAP_MAX_ZOOM),DEFAULT_MAP_MAX_ZOOM);
  }

  function getLayer(key){
    if(directLayers.has(key))return directLayers.get(key);
    const cfg=getCfg(key),tileSize=Number(cfg.tileSize||256),nativeMax=Number(cfg.maxZoom??8),minZoom=Number(cfg.minZoom??0),displayMax=getDisplayMax(cfg);
    const layer=L.tileLayer(cfg.tileUrl,{
      tileSize,
      minZoom,
      maxZoom:displayMax,
      minNativeZoom:minZoom,
      maxNativeZoom:nativeMax,
      bounds:getBounds(key),
      noWrap:true,
      keepBuffer:12,
      updateWhenIdle:false,
      updateWhenZooming:true,
      updateInterval:80,
      pane:'tilePane'
    });
    layer.on('tileload',e=>{
      if(e&&e.tile){
        e.tile.style.opacity='1';
        e.tile.style.transition='none';
        e.tile.style.backfaceVisibility='hidden';
      }
    });
    directLayers.set(key,layer);
    return layer;
  }

  function removeDirectLayers(exceptKey){
    for(const [key,layer] of directLayers){
      if(key!==exceptKey&&liveMap.hasLayer(layer))liveMap.removeLayer(layer);
    }
  }

  function removeLegacyLayers(){
    try{if(typeof baseLayer!=='undefined'&&liveMap.hasLayer(baseLayer))liveMap.removeLayer(baseLayer)}catch(_){}
    if(activeImageLayer){
      try{liveMap.removeLayer(activeImageLayer)}catch(_){}
      activeImageLayer=null;
    }
  }

  function note(text){const el=document.getElementById('mapBaseNote');if(el)el.textContent=text}

  function showDirectLayer(key,fit){
    const cfg=getCfg(key),layer=getLayer(key),bounds=getBounds(key),displayMax=getDisplayMax(cfg);
    removeLegacyLayers();
    removeDirectLayers(key);
    liveMap.setMaxZoom(displayMax);
    if(!liveMap.hasLayer(layer))layer.addTo(liveMap);
    liveMap.setMaxBounds(bounds.pad(.035));
    document.getElementById('mapStage')?.classList.add('map-has-base-image','map-gat-export','map-gat-tiles');
    const label=(configs[currentMap]?.label||configs[key]?.label||key).toUpperCase();
    if(currentMap==='promods')note('PROMODS • MAPA BASE GAT COMO REFERÊNCIA • TILES DIRETOS');
    else note(label+' • TILES GAT • POSIÇÃO AO VIVO');
    if(fit)liveMap.fitBounds(bounds,{padding:[18,18],maxZoom:2});
  }

  applyLayerForMap=function(){
    const key=directKey();
    if(key){
      const visual='gat-direct:'+currentMap;
      const changed=activeVisualKey!==visual;
      activeVisualKey=visual;
      showDirectLayer(key,changed&&firstPosition);
      return;
    }

    removeDirectLayers('');
    liveMap.setMaxZoom(DEFAULT_MAP_MAX_ZOOM);
    if(liveMap.getZoom()>DEFAULT_MAP_MAX_ZOOM)liveMap.setZoom(DEFAULT_MAP_MAX_ZOOM);
    document.getElementById('mapStage')?.classList.remove('map-gat-tiles','map-gat-export');
    oldApplyLayerForMap();
  };

  const initialKey=directKey();
  if(initialKey){
    removeLegacyLayers();
    activeVisualKey='gat-direct:'+currentMap;
    firstPosition=true;
    showDirectLayer(initialKey,false);
    liveMap.setView(getBounds(initialKey).getCenter(),2);
    liveMap.invalidateSize();
    setTimeout(()=>{
      try{renderPins();if(firstPosition)liveMap.fitBounds(getBounds(initialKey),{padding:[18,18],maxZoom:2})}
      catch(_){liveMap.fitBounds(getBounds(initialKey),{padding:[18,18],maxZoom:2})}
    },0);
  }
})();
