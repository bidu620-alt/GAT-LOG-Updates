// GAT-LOG • mapa Base em tiles diretamente do ZIP exportado pelo ETS2.
// O ZIP contém TileMapInfo.json e a pirâmide Tiles/{z}/{x}/{y}.png.
(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined'||typeof JSZip==='undefined')return;

  const cfg=(window.GAT_MAP_CONFIG||{}).base||{};
  if(cfg.type!=='gat-zip-tiles'||!cfg.zipUrl)return;

  const oldGameToLatLng=gameToLatLng;
  const oldApplyLayerForMap=applyLayerForMap;
  const TILE_SIZE=Number(cfg.zipTileSize||256);
  const MAX_ZOOM=Number(cfg.maxZoom??6);
  const MIN_ZOOM=Number(cfg.minZoom??0);
  const ZIP_ROOT=String(cfg.zipRoot||'GAT_MAPA_BASE').replace(/\/$/,'');
  const WORLD_PX=TILE_SIZE*Math.pow(2,MAX_ZOOM);
  const zipBounds=L.latLngBounds(
    liveMap.unproject([0,WORLD_PX],MAX_ZOOM),
    liveMap.unproject([WORLD_PX,0],MAX_ZOOM)
  );

  let gameBounds=cfg.gameBounds||null;
  let zipPromise=null;
  let zipReady=false;
  let zipFailed=false;
  const tileUrlCache=new Map();

  function note(text){
    const el=document.getElementById('mapBaseNote');
    if(el)el.textContent=text;
  }

  function baseSelected(){
    return currentMap==='base'||currentMap==='promods';
  }

  async function loadZip(){
    if(zipPromise)return zipPromise;
    note('MAPA BASE • BAIXANDO TILES ORIGINAIS DO ETS2...');
    zipPromise=(async()=>{
      const response=await fetch(cfg.zipUrl,{cache:'no-cache'});
      if(!response.ok)throw new Error('Falha ao baixar ZIP do mapa: HTTP '+response.status);
      const data=await response.arrayBuffer();
      const zip=await JSZip.loadAsync(data);

      const infoFile=zip.file(ZIP_ROOT+'/TileMapInfo.json')||zip.file('TileMapInfo.json');
      if(infoFile){
        try{
          const info=JSON.parse(await infoFile.async('text'));
          if([info.x1,info.x2,info.y1,info.y2].every(v=>Number.isFinite(Number(v)))){
            gameBounds={
              xMin:Number(info.x1), xMax:Number(info.x2),
              zMin:Number(info.y1), zMax:Number(info.y2)
            };
          }
        }catch(err){console.warn('GAT TileMapInfo:',err)}
      }

      zipReady=true;
      zipFailed=false;
      if(baseSelected())note(currentMap==='promods'?'PROMODS • MAPA BASE GAT COMO REFERÊNCIA • TILES ORIGINAIS':'MAPA BASE • TILES ORIGINAIS DO ETS2 • POSIÇÃO GAT AO VIVO');
      return zip;
    })().catch(err=>{
      console.error('GAT mapa ZIP:',err);
      zipFailed=true;
      zipReady=false;
      note('MAPA BASE • FALHA AO ABRIR ZIP • USANDO MAPA DE RESERVA');
      throw err;
    });
    return zipPromise;
  }

  function gameToZipLatLng(x,z){
    const b=gameBounds;
    if(!b||![b.xMin,b.xMax,b.zMin,b.zMax].every(v=>Number.isFinite(Number(v))))return oldGameToLatLng(x,z);
    const nx=(Number(x)-Number(b.xMin))/(Number(b.xMax)-Number(b.xMin));
    const nz=(Number(z)-Number(b.zMin))/(Number(b.zMax)-Number(b.zMin));
    const px=Math.max(0,Math.min(WORLD_PX,nx*WORLD_PX));
    const py=Math.max(0,Math.min(WORLD_PX,nz*WORLD_PX));
    return liveMap.unproject([px,py],MAX_ZOOM);
  }

  // Mantém os outros mapas com a transformação antiga; Base/ProMods usam o TileMapInfo real.
  gameToLatLng=function(x,z){
    return baseSelected()?gameToZipLatLng(x,z):oldGameToLatLng(x,z);
  };

  const ZipTileLayer=L.GridLayer.extend({
    createTile:function(coords,done){
      const img=document.createElement('img');
      img.width=TILE_SIZE;
      img.height=TILE_SIZE;
      img.alt='';
      img.setAttribute('role','presentation');
      img.style.width='100%';
      img.style.height='100%';

      const maxIndex=Math.pow(2,coords.z)-1;
      if(coords.z<MIN_ZOOM||coords.z>MAX_ZOOM||coords.x<0||coords.y<0||coords.x>maxIndex||coords.y>maxIndex){
        setTimeout(()=>done(null,img),0);
        return img;
      }

      const path=ZIP_ROOT+'/Tiles/'+coords.z+'/'+coords.x+'/'+coords.y+'.png';
      (async()=>{
        try{
          let objectUrl=tileUrlCache.get(path);
          if(!objectUrl){
            const zip=await loadZip();
            const entry=zip.file(path);
            if(!entry)throw new Error('Tile ausente: '+path);
            const blob=await entry.async('blob');
            objectUrl=URL.createObjectURL(blob);
            tileUrlCache.set(path,objectUrl);
          }
          img.onload=()=>done(null,img);
          img.onerror=()=>done(new Error('Falha ao desenhar '+path),img);
          img.src=objectUrl;
        }catch(err){
          done(err,img);
        }
      })();
      return img;
    }
  });

  const zipLayer=new ZipTileLayer({
    tileSize:TILE_SIZE,
    minZoom:MIN_ZOOM,
    maxZoom:MAX_ZOOM,
    minNativeZoom:MIN_ZOOM,
    maxNativeZoom:MAX_ZOOM,
    bounds:zipBounds,
    noWrap:true,
    keepBuffer:3,
    updateWhenIdle:false,
    updateWhenZooming:false,
    pane:'tilePane'
  });

  function removeLegacyLayers(){
    try{if(typeof baseLayer!=='undefined'&&liveMap.hasLayer(baseLayer))liveMap.removeLayer(baseLayer)}catch(_){}
    if(activeImageLayer){
      try{liveMap.removeLayer(activeImageLayer)}catch(_){}
      activeImageLayer=null;
    }
  }

  function showZipLayer(fit){
    removeLegacyLayers();
    if(!liveMap.hasLayer(zipLayer))zipLayer.addTo(liveMap);
    liveMap.setMaxBounds(zipBounds.pad(.035));
    document.getElementById('mapStage')?.classList.add('map-has-base-image','map-gat-export','map-gat-tiles');
    if(fit)liveMap.fitBounds(zipBounds,{padding:[18,18],maxZoom:2});
    if(zipReady)note(currentMap==='promods'?'PROMODS • MAPA BASE GAT COMO REFERÊNCIA • TILES ORIGINAIS':'MAPA BASE • TILES ORIGINAIS DO ETS2 • POSIÇÃO GAT AO VIVO');
    else if(!zipFailed)note('MAPA BASE • CARREGANDO TILES ORIGINAIS DO ETS2...');
    loadZip().then(()=>{if(baseSelected()){zipLayer.redraw();liveMap.invalidateSize()}}).catch(()=>{
      try{if(typeof baseLayer!=='undefined'&&!liveMap.hasLayer(baseLayer))baseLayer.addTo(liveMap)}catch(_){}
    });
  }

  applyLayerForMap=function(){
    if(baseSelected()){
      const changed=activeVisualKey!=='gat-zip:'+currentMap;
      activeVisualKey='gat-zip:'+currentMap;
      showZipLayer(changed&&firstPosition);
      return;
    }

    if(liveMap.hasLayer(zipLayer))liveMap.removeLayer(zipLayer);
    document.getElementById('mapStage')?.classList.remove('map-gat-tiles','map-gat-export');
    oldApplyLayerForMap();
  };

  // Substitui imediatamente a base provisória pela pirâmide original.
  removeLegacyLayers();
  activeVisualKey='';
  showZipLayer(true);
  liveMap.invalidateSize();

  window.addEventListener('beforeunload',()=>{
    for(const url of tileUrlCache.values())URL.revokeObjectURL(url);
    tileUrlCache.clear();
  });
})();
