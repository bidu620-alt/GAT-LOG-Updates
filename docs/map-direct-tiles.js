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

// GAT-LOG • American Truck Simulator carregado de um único ZIP exportado.
(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined')return;
  const cfg=(window.GAT_MAP_CONFIG||{}).ats||{};
  if(cfg.type!=='gat-zip-tiles'||!cfg.zipUrl)return;

  MAP_LABELS.ats=cfg.label||'American Truck Simulator';

  const previousMapKey=mapKey;
  mapKey=function(t){
    const explicit=norm(textValue(t,'gat_map','map_mode','gatMap')).replace(/[\s-]+/g,'_');
    if(explicit==='ats'||explicit==='american_truck_simulator'||explicit==='americantrucksimulator'||explicit==='american_truck')return 'ats';
    const game=norm(textValue(t,'game_name','gameName','game.gameName','Game.GameName','game.id','game.productName'));
    if(game==='ats'||game.includes('american truck'))return 'ats';
    return previousMapKey(t);
  };

  const previousRenderMapCounts=renderMapCounts;
  renderMapCounts=function(){
    previousRenderMapCounts();
    const el=document.getElementById('mapCountAts');
    if(el)el.textContent=mapDrivers.reduce((n,d)=>n+(d.map==='ats'?1:0),0);
  };

  const tabs=document.querySelector('.map-world-tabs');
  let atsButton=document.querySelector('.map-world-tabs button[data-map="ats"]');
  if(tabs&&!atsButton){
    atsButton=document.createElement('button');
    atsButton.type='button';
    atsButton.dataset.map='ats';
    atsButton.innerHTML='ATS <b id="mapCountAts">0</b>';
    const baseButton=tabs.querySelector('button[data-map="base"]');
    if(baseButton&&baseButton.nextSibling)tabs.insertBefore(atsButton,baseButton.nextSibling);else tabs.appendChild(atsButton);
    atsButton.addEventListener('click',()=>{
      setMapTab('ats');selectedDriverKey='';firstPosition=true;restoreNoticeText();updateDriverList();renderPins();updateStats(true);
    });
  }

  const previousGameToLatLng=gameToLatLng;
  const previousApplyLayerForMap=applyLayerForMap;
  const TILE_SIZE=Number(cfg.zipTileSize||cfg.tileSize||256);
  const MAX_ZOOM=Number(cfg.maxZoom??6);
  const MIN_ZOOM=Number(cfg.minZoom??0);
  const DISPLAY_MAX=Math.max(MAX_ZOOM,Number(cfg.displayMaxZoom??8));
  const ZIP_ROOT=String(cfg.zipRoot||'GAT_MAPA_ATS').replace(/\/$/,'');
  const WORLD_PX=TILE_SIZE*Math.pow(2,MAX_ZOOM);
  const atsBounds=L.latLngBounds(liveMap.unproject([0,WORLD_PX],MAX_ZOOM),liveMap.unproject([WORLD_PX,0],MAX_ZOOM));
  let gameBounds=cfg.gameBounds||null,zipPromise=null,zipReady=false,zipFailed=false,jszipPromise=null;
  const tileUrlCache=new Map();

  function selected(){return currentMap==='ats'}
  function note(text){const el=document.getElementById('mapBaseNote');if(el)el.textContent=text}
  function ensureJSZip(){
    if(window.JSZip)return Promise.resolve(window.JSZip);
    if(jszipPromise)return jszipPromise;
    jszipPromise=new Promise((resolve,reject)=>{
      const s=document.createElement('script');
      s.src='https://cdn.jsdelivr.net/npm/jszip@3.10.1/dist/jszip.min.js';s.async=true;
      s.onload=()=>window.JSZip?resolve(window.JSZip):reject(new Error('JSZip não carregou'));
      s.onerror=()=>reject(new Error('Falha ao carregar JSZip'));
      document.head.appendChild(s);
    });
    return jszipPromise;
  }

  async function loadZip(){
    if(zipPromise)return zipPromise;
    note('AMERICAN TRUCK SIMULATOR • BAIXANDO MAPA GAT...');
    zipPromise=(async()=>{
      const JSZipLib=await ensureJSZip();
      const response=await fetch(cfg.zipUrl,{cache:'no-cache'});
      if(!response.ok)throw new Error('Falha ao baixar mapa ATS: HTTP '+response.status);
      const zip=await JSZipLib.loadAsync(await response.arrayBuffer());
      const infoFile=zip.file(ZIP_ROOT+'/TileMapInfo.json')||zip.file('TileMapInfo.json');
      if(infoFile){
        try{
          const info=JSON.parse(await infoFile.async('text'));
          if([info.x1,info.x2,info.y1,info.y2].every(v=>Number.isFinite(Number(v))))gameBounds={xMin:Number(info.x1),xMax:Number(info.x2),zMin:Number(info.y1),zMax:Number(info.y2)};
        }catch(err){console.warn('GAT ATS TileMapInfo:',err)}
      }
      zipReady=true;zipFailed=false;
      if(selected())note('AMERICAN TRUCK SIMULATOR • MAPA GAT • POSIÇÃO AO VIVO');
      return zip;
    })().catch(err=>{
      console.error('GAT mapa ATS:',err);zipFailed=true;zipReady=false;zipPromise=null;
      if(selected())note('AMERICAN TRUCK SIMULATOR • ARQUIVO DO MAPA AINDA NÃO PUBLICADO');
      throw err;
    });
    return zipPromise;
  }

  function gameToAtsLatLng(x,z){
    const b=gameBounds;
    if(!b||![b.xMin,b.xMax,b.zMin,b.zMax].every(v=>Number.isFinite(Number(v))))return previousGameToLatLng(x,z);
    const nx=(Number(x)-Number(b.xMin))/(Number(b.xMax)-Number(b.xMin));
    const nz=(Number(z)-Number(b.zMin))/(Number(b.zMax)-Number(b.zMin));
    const px=Math.max(0,Math.min(WORLD_PX,nx*WORLD_PX));
    const py=Math.max(0,Math.min(WORLD_PX,nz*WORLD_PX));
    return liveMap.unproject([px,py],MAX_ZOOM);
  }
  gameToLatLng=function(x,z){return selected()?gameToAtsLatLng(x,z):previousGameToLatLng(x,z)};

  const AtsZipLayer=L.GridLayer.extend({
    createTile:function(coords,done){
      const img=document.createElement('img');img.width=TILE_SIZE;img.height=TILE_SIZE;img.alt='';img.setAttribute('role','presentation');img.style.width='100%';img.style.height='100%';
      const maxIndex=Math.pow(2,coords.z)-1;
      if(coords.z<MIN_ZOOM||coords.z>MAX_ZOOM||coords.x<0||coords.y<0||coords.x>maxIndex||coords.y>maxIndex){setTimeout(()=>done(null,img),0);return img}
      const path=ZIP_ROOT+'/Tiles/'+coords.z+'/'+coords.x+'/'+coords.y+'.png';
      (async()=>{
        try{
          let objectUrl=tileUrlCache.get(path);
          if(!objectUrl){const zip=await loadZip(),entry=zip.file(path);if(!entry)throw new Error('Tile ATS ausente: '+path);objectUrl=URL.createObjectURL(await entry.async('blob'));tileUrlCache.set(path,objectUrl)}
          img.onload=()=>done(null,img);img.onerror=()=>done(new Error('Falha ao desenhar '+path),img);img.src=objectUrl;
        }catch(err){done(err,img)}
      })();
      return img;
    }
  });
  const atsLayer=new AtsZipLayer({tileSize:TILE_SIZE,minZoom:MIN_ZOOM,maxZoom:DISPLAY_MAX,minNativeZoom:MIN_ZOOM,maxNativeZoom:MAX_ZOOM,bounds:atsBounds,noWrap:true,keepBuffer:4,updateWhenIdle:false,updateWhenZooming:true,pane:'tilePane'});

  function showAts(fit){
    previousApplyLayerForMap();
    if(!liveMap.hasLayer(atsLayer))atsLayer.addTo(liveMap);
    liveMap.setMaxZoom(DISPLAY_MAX);liveMap.setMaxBounds(atsBounds.pad(.035));
    document.getElementById('mapStage')?.classList.add('map-has-base-image','map-gat-export','map-gat-tiles');
    if(fit)liveMap.fitBounds(atsBounds,{padding:[18,18],maxZoom:2});
    if(zipReady)note('AMERICAN TRUCK SIMULATOR • MAPA GAT • POSIÇÃO AO VIVO');else if(!zipFailed)note('AMERICAN TRUCK SIMULATOR • CARREGANDO MAPA GAT...');
    loadZip().then(()=>{if(selected()){atsLayer.redraw();liveMap.invalidateSize()}}).catch(()=>{});
  }

  applyLayerForMap=function(){
    if(selected()){
      const changed=activeVisualKey!=='gat-ats-zip';activeVisualKey='gat-ats-zip';showAts(changed&&firstPosition);return;
    }
    if(liveMap.hasLayer(atsLayer))liveMap.removeLayer(atsLayer);
    previousApplyLayerForMap();
  };

  window.addEventListener('beforeunload',()=>{for(const url of tileUrlCache.values())URL.revokeObjectURL(url);tileUrlCache.clear()});
})();
