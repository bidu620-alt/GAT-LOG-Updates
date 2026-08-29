(function(){
  if(typeof L==='undefined'||typeof liveMap==='undefined')return;

  const cfg=(window.GAT_MAP_CONFIG||{}).base||{};
  const px=Array.isArray(cfg.pixelBounds)?cfg.pixelBounds:[0,192512,173568,0];
  const bounds=L.latLngBounds(liveMap.unproject([px[0],px[1]],8),liveMap.unproject([px[2],px[3]],8));

  if(!liveMap.getPane('gatBaseUnderlay')){
    const pane=liveMap.createPane('gatBaseUnderlay');
    pane.style.zIndex='180';
    pane.style.pointerEvents='none';
  }

  const underlay=L.tileLayer(cfg.underlayUrl||'https://cdn.jsdelivr.net/gh/felix-d1strict/vtc-map@master/ets2map/uncoloured/{z}/{x}_{y}.png',{
    minZoom:Number(cfg.minZoom??0),
    maxZoom:Number(cfg.maxZoom??8),
    tileSize:Number(cfg.tileSize||512),
    bounds:bounds,
    noWrap:true,
    keepBuffer:8,
    updateWhenIdle:true,
    updateWhenZooming:false,
    pane:'gatBaseUnderlay'
  });

  const citiesLayer=L.layerGroup();
  let citiesBuilt=false;

  function buildCities(){
    if(citiesBuilt||typeof g_cities_json!=='object'||!g_cities_json)return;
    citiesBuilt=true;
    Object.keys(g_cities_json).forEach(function(name){
      const c=g_cities_json[name];
      if(!c||!Number.isFinite(Number(c.x))||!Number.isFinite(Number(c.z)))return;
      const marker=L.marker(gameToLatLng(Number(c.x),Number(c.z)),{
        interactive:false,
        keyboard:false,
        icon:L.divIcon({
          className:'gat-city-icon',
          iconSize:[0,0],
          iconAnchor:[0,0],
          html:'<span class="gat-city-label">'+String(name).replace(/[&<>"']/g,function(ch){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]})+'</span>'
        })
      });
      citiesLayer.addLayer(marker);
    });
  }

  function baseVisible(){
    return typeof currentMap!=='undefined'&&(currentMap==='base'||currentMap==='promods');
  }

  function sync(){
    if(baseVisible()){
      if(!liveMap.hasLayer(underlay))underlay.addTo(liveMap);
    }else if(liveMap.hasLayer(underlay)){
      liveMap.removeLayer(underlay);
    }

    buildCities();
    const showCities=(typeof currentMap!=='undefined'&&currentMap==='base'&&liveMap.getZoom()>=4);
    if(showCities){
      if(!liveMap.hasLayer(citiesLayer))citiesLayer.addTo(liveMap);
    }else if(liveMap.hasLayer(citiesLayer)){
      liveMap.removeLayer(citiesLayer);
    }
  }

  liveMap.on('zoomend',sync);
  document.querySelectorAll('.map-world-tabs button').forEach(function(btn){
    btn.addEventListener('click',function(){setTimeout(sync,0)});
  });

  setTimeout(function(){
    sync();
    liveMap.invalidateSize();
  },120);
})();
