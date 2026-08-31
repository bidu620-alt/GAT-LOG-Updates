const CENTRAL={label:'CENTRAL GAT',url:'https://api.gatlogets2.com.br'};
const LIVE_PATH='/api/public/account-live',FRESH_MS=18000,REF_ZOOM=8;
const MAP_CONFIG=window.GAT_MAP_CONFIG||{};
const MAP_LABELS={base:'Mapa Base',promods:'ProMods',rbr:'RBR',rotas_brasil:'Rotas Brasil',eaa:'EAA',other:'Outro mapa'};
let mapDrivers=[],currentFilter='all',currentMap='base',firstPosition=true,activeImageLayer=null,activeVisualKey='base',selectedDriverKey='';
const truckMarkers=new Map(),driverRows=new Map();
const norm=s=>String(s||'').trim().toLowerCase();
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot',"'":'&#39;'}[c]));
const age=t=>{const d=Date.parse(t||'');return Number.isFinite(d)?Date.now()-d:Infinity};
const fresh=t=>age(t?.updated_at)<=FRESH_MS;
const rawOf=t=>t&&t.telemetry&&typeof t.telemetry==='object'?t.telemetry:{};
const deep=(o,path)=>{let cur=o;for(const part of String(path).split('.')){if(!cur||typeof cur!=='object'||!(part in cur))return undefined;cur=cur[part]}return cur};
const textValue=(t,...paths)=>{for(const src of [t,rawOf(t)])for(const path of paths){const v=deep(src,path);if(v!==undefined&&v!==null&&typeof v!=='object'&&String(v).trim())return String(v).trim()}return ''};
const numberValue=(t,...paths)=>{for(const src of [t,rawOf(t)])for(const path of paths){const v=deep(src,path),n=Number(v);if(v!==undefined&&v!==null&&Number.isFinite(n))return n}return 0};
const hasPosition=t=>{if(!fresh(t))return false;const x=numberValue(t,'map_x','truck.placement.x'),z=numberValue(t,'map_z','truck.placement.z');return Number.isFinite(x)&&Number.isFinite(z)&&(Math.abs(x)>.01||Math.abs(z)>.01)};
const truckText=t=>{const a=textValue(t,'truck_make','truck.make','Truck.Make'),b=textValue(t,'truck_model','truck.model','Truck.Model');return [a,b].filter(Boolean).join(' ')||'Caminhão não informado'};
const sourceOf=t=>textValue(t,'source_city','source','job.sourceCity','Job.SourceCity');
const destinationOf=t=>textValue(t,'destination_city','destination','job.destinationCity','Job.DestinationCity');

function mapKey(t){let k=norm(textValue(t,'gat_map','map_mode','gatMap')).replace(/[\s-]+/g,'_');if(k==='promods'||k==='pro_mods')return 'promods';if(k==='rbr')return 'rbr';if(k==='rotas_brasil'||k==='rotasbrasil'||k==='rots_brasil')return 'rotas_brasil';if(k==='eaa'||k==='mapa_eaa')return 'eaa';if(k==='other'||k==='outro'||k==='outro_mapa')return 'other';return 'base'}
function mapLabel(k){return MAP_LABELS[k]||MAP_LABELS.base}
function mapMatch(d){return d.map===currentMap}
function markerKey(d){return norm(d.t?.account_user||d.name)}

const liveMap=L.map('ets2Map',{crs:L.CRS.Simple,minZoom:0,maxZoom:8,zoomControl:true,attributionControl:false,preferCanvas:true});
const baseCfg=MAP_CONFIG.base||{};
const px=Array.isArray(baseCfg.pixelBounds)?baseCfg.pixelBounds:[0,192512,173568,0];
const mapBounds=L.latLngBounds(liveMap.unproject([px[0],px[1]],REF_ZOOM),liveMap.unproject([px[2],px[3]],REF_ZOOM));
const broadBounds=L.latLngBounds(liveMap.unproject([-900000,900000],REF_ZOOM),liveMap.unproject([900000,-900000],REF_ZOOM));
const baseLayer=L.tileLayer(baseCfg.tileUrl||'https://raw.githubusercontent.com/felix-d1strict/vtc-map/master/ets2map/coloured/{z}/{x}_{y}.png',{minZoom:Number(baseCfg.minZoom??0),maxZoom:Number(baseCfg.maxZoom??8),tileSize:Number(baseCfg.tileSize||512),bounds:mapBounds,noWrap:true,keepBuffer:8,updateWhenIdle:true,updateWhenZooming:false}).addTo(liveMap);
liveMap.setMaxBounds(mapBounds.pad(.08));
liveMap.setView(liveMap.unproject([90000,90000],REF_ZOOM),2);

function gameToLatLng(x,z){return liveMap.unproject([Number(x)/.78125+89600,Number(z)/.78125+89600],REF_ZOOM)}
function imageBounds(cfg){const b=cfg&&cfg.gameBounds;if(!b||![b.xMin,b.zMin,b.xMax,b.zMax].every(v=>Number.isFinite(Number(v))))return null;return L.latLngBounds(gameToLatLng(Number(b.xMin),Number(b.zMin)),gameToLatLng(Number(b.xMax),Number(b.zMax)))}
function headingDeg(v){const h=Number(v);if(!Number.isFinite(h))return 0;if(Math.abs(h)<=1.01)return h*360;if(Math.abs(h)<=Math.PI*2+.01)return h*180/Math.PI;return h%360}
function markerIcon(d){const moving=fresh(d.t)&&d.t.on_job,deg=headingDeg(numberValue(d.t,'map_heading','truck.placement.heading'));return L.divIcon({className:'gat-truck-icon',iconSize:[48,48],iconAnchor:[24,24],html:'<div class="gat-truck-marker '+(moving?'':'idle')+'"><div class="gat-truck-arrow" style="transform:rotate('+deg.toFixed(1)+'deg)"><div class="gat-truck-body"></div></div><span class="gat-truck-label">'+esc(d.name)+'</span></div>'})}
function popupHtml(d){const t=d.t||{},src=sourceOf(t),dst=destinationOf(t),route=t.on_job?((src||'Origem')+' → '+(dst||'Destino')):'Online • sem carga',speed=Math.round(Math.abs(numberValue(t,'speed_kmh','truck.speedKmh','truck.speed_kmh','truck.speed'))),acct=String(t.account_user||'').trim();return '<div class="gat-popup"><b>'+esc(d.name)+'</b><small>'+esc(mapLabel(d.map))+' • Central GAT</small><strong>'+esc(truckText(t))+'</strong><small>'+esc(route)+'</small><small>'+speed+' km/h'+(acct?' • @'+esc(acct):'')+'</small></div>'}

async function fetchCentral(){const c=new AbortController(),timer=setTimeout(()=>c.abort(),4500);try{const r=await fetch(CENTRAL.url+LIVE_PATH,{cache:'no-store',signal:c.signal});if(!r.ok)throw 0;return await r.json()}catch(_){return null}finally{clearTimeout(timer)}}
function build(data){const tel=Array.isArray(data?.telemetry)?data.telemetry:[],seen=new Set(),out=[];tel.filter(fresh).forEach(t=>{const name=String(t.driver||t.account_user||'Motorista').trim(),k=norm(t.account_user||name);if(!k||seen.has(k))return;seen.add(k);out.push({server:CENTRAL.label,name,t,map:mapKey(t)})});return out}
function filteredList(){return mapDrivers.filter(d=>mapMatch(d)&&(currentFilter==='all'||(currentFilter==='trip'&&d.t?.on_job)||(currentFilter==='idle'&&!d.t?.on_job)))}

function setMapTab(key){currentMap=key||'base';document.querySelectorAll('.map-world-tabs button').forEach(x=>x.classList.toggle('active',(x.dataset.map||'base')===currentMap))}
function showNoPosition(d){const notice=document.getElementById('coordinateNotice');if(!notice)return;notice.style.display='block';const b=notice.querySelector('b'),span=notice.querySelector('span');if(b)b.textContent='SEM POSIÇÃO RECENTE';if(span)span.textContent=(d?.name||'Motorista')+' está online, mas ainda não enviou uma posição válida para o mapa.'}
function restoreNoticeText(){const notice=document.getElementById('coordinateNotice');if(!notice)return;const b=notice.querySelector('b'),span=notice.querySelector('span');if(b)b.textContent='AGUARDANDO POSIÇÃO DO MOTORISTA';if(span)span.textContent='Com o GAT Telemetria conectado, o caminhão aparece automaticamente na aba correspondente ao mapa escolhido.'}

function focusDriver(d){
  selectedDriverKey=markerKey(d);updateDriverList();
  if(!hasPosition(d.t)){showNoPosition(d);return}
  renderPins();
  const marker=truckMarkers.get(selectedDriverKey);
  if(!marker){showNoPosition(d);return}
  restoreNoticeText();
  const notice=document.getElementById('coordinateNotice');if(notice)notice.style.display='none';
  const zoom=Math.min(8,Math.max(6,liveMap.getZoom()));
  liveMap.flyTo(marker.getLatLng(),zoom,{animate:true,duration:.55});
  setTimeout(()=>marker.openPopup(),420);
}

function rowHtml(d){const src=sourceOf(d.t),dst=destinationOf(d.t),route=d.t?.on_job?((src||'Origem')+' → '+(dst||'Destino')):'Online • sem carga',speed=Math.round(Math.abs(numberValue(d.t,'speed_kmh','truck.speedKmh','truck.speed_kmh','truck.speed'))),pos=hasPosition(d.t);return '<div class="map-driver-avatar">'+esc(String(d.name||'?').charAt(0).toUpperCase())+'</div><div><b>'+esc(d.name||'Motorista')+'</b><small>'+esc(route)+'</small><small class="map-driver-truck">'+esc(truckText(d.t))+' • '+esc(mapLabel(d.map))+'</small></div><div class="map-driver-speed"><strong>'+speed+'</strong><br>km/h<i class="'+(pos?'positioned':'')+'"></i></div>'}
function rowSignature(d){return [d.name,d.t?.on_job,sourceOf(d.t),destinationOf(d.t),truckText(d.t),Math.round(Math.abs(numberValue(d.t,'speed_kmh','truck.speedKmh','truck.speed_kmh','truck.speed'))),hasPosition(d.t),d.map,markerKey(d)===selectedDriverKey].join('|')}
function updateDriverList(){
  const root=document.getElementById('mapDrivers'),list=filteredList(),keep=new Set();
  document.getElementById('mapDriverCount').textContent=list.length;
  const placeholder=root.querySelector('.map-loading');if(placeholder&&list.length)placeholder.remove();
  list.forEach(d=>{
    const key=markerKey(d);keep.add(key);let item=driverRows.get(key);
    if(!item){item=document.createElement('button');item.type='button';item.className='map-driver-item';item.dataset.driverKey=key;item.addEventListener('click',()=>focusDriver(item._driver));driverRows.set(key,item)}
    item._driver=d;item.classList.toggle('selected',key===selectedDriverKey);item.title=hasPosition(d.t)?'Mostrar motorista no mapa':'Motorista sem posição recente';
    const sig=rowSignature(d);if(item.dataset.signature!==sig){item.innerHTML=rowHtml(d);item.dataset.signature=sig}
    root.appendChild(item);
  });
  for(const [key,item] of driverRows){if(!keep.has(key)){item.remove();driverRows.delete(key)}}
  if(!list.length&&!root.querySelector('.map-loading')){const empty=document.createElement('div');empty.className='map-loading';empty.textContent='Nenhum motorista nesse filtro.';root.appendChild(empty)}
}

function desiredVisual(){const cfg=MAP_CONFIG[currentMap]||{};if(currentMap==='base'||currentMap==='promods'||cfg.type==='tiles'||cfg.type==='reference')return {key:'base',cfg:baseCfg,label:mapLabel(currentMap)};if(cfg.type==='image'&&cfg.imageUrl&&imageBounds(cfg))return {key:'image:'+currentMap+':'+cfg.imageUrl,cfg,label:mapLabel(currentMap)};return {key:'blank:'+currentMap,cfg,label:mapLabel(currentMap)}}
function updateMapNote(v){const note=document.getElementById('mapBaseNote');if(!note)return;if(currentMap==='base')note.textContent='MAPA BASE • POSIÇÃO GAT EM TEMPO REAL';else if(v.key==='base')note.textContent=v.label.toUpperCase()+' • MAPA BASE COMO REFERÊNCIA • POSIÇÃO GAT AO VIVO';else if(v.key.startsWith('image:'))note.textContent=v.label.toUpperCase()+' • BASE VISUAL GAT • POSIÇÃO AO VIVO';else note.textContent=v.label.toUpperCase()+' • AGUARDANDO BASE VISUAL • POSIÇÃO GAT JÁ PREPARADA'}
function applyLayerForMap(){
  const v=desiredVisual();updateMapNote(v);if(v.key===activeVisualKey)return;
  if(liveMap.hasLayer(baseLayer))liveMap.removeLayer(baseLayer);if(activeImageLayer){liveMap.removeLayer(activeImageLayer);activeImageLayer=null}
  if(v.key==='base'){baseLayer.addTo(liveMap);liveMap.setMaxBounds(mapBounds.pad(.08))}
  else if(v.key.startsWith('image:')){const bounds=imageBounds(v.cfg);activeImageLayer=L.imageOverlay(v.cfg.imageUrl,bounds,{interactive:false,opacity:1}).addTo(liveMap);liveMap.setMaxBounds(bounds.pad(.05));if(firstPosition)liveMap.fitBounds(bounds,{padding:[20,20]})}
  else liveMap.setMaxBounds(broadBounds);
  activeVisualKey=v.key;document.getElementById('mapStage')?.classList.toggle('map-has-base-image',!!activeImageLayer);
}

function renderPins(){
  applyLayerForMap();const positioned=mapDrivers.filter(d=>mapMatch(d)&&hasPosition(d.t)),active=new Set(),latlngs=[];
  positioned.forEach(d=>{const key=markerKey(d),ll=gameToLatLng(numberValue(d.t,'map_x','truck.placement.x'),numberValue(d.t,'map_z','truck.placement.z'));active.add(key);latlngs.push(ll);let marker=truckMarkers.get(key);if(!marker){marker=L.marker(ll,{icon:markerIcon(d),zIndexOffset:d.t?.on_job?500:300}).addTo(liveMap);truckMarkers.set(key,marker)}else{marker.setLatLng(ll);marker.setIcon(markerIcon(d));marker.setZIndexOffset(d.t?.on_job?500:300)}marker.bindPopup(popupHtml(d),{closeButton:false,offset:[0,-12]})});
  for(const [key,marker] of truckMarkers){if(!active.has(key)){liveMap.removeLayer(marker);truckMarkers.delete(key)}}
  if(selectedDriverKey&&!active.has(selectedDriverKey))selectedDriverKey='';
  const notice=document.getElementById('coordinateNotice');if(notice&&selectedDriverKey===''){notice.style.display=positioned.length?'none':'block';if(!positioned.length)restoreNoticeText()}
  if(firstPosition&&latlngs.length){firstPosition=false;if(latlngs.length===1)liveMap.setView(latlngs[0],6);else liveMap.fitBounds(L.latLngBounds(latlngs),{padding:[55,55],maxZoom:6})}
}

function renderMapCounts(){const counts={base:0,promods:0,rbr:0,rotas_brasil:0,eaa:0,other:0};mapDrivers.forEach(d=>counts[d.map]=(counts[d.map]||0)+1);const set=(id,v)=>{const e=document.getElementById(id);if(e)e.textContent=v};set('mapCountBase',counts.base);set('mapCountPromods',counts.promods);set('mapCountRbr',counts.rbr);set('mapCountRotas',counts.rotas_brasil);set('mapCountEaa',counts.eaa);set('mapCountOther',counts.other)}
function updateStats(ok){const visible=mapDrivers.filter(mapMatch),online=visible.length,trips=visible.filter(d=>d.t?.on_job).length,positioned=visible.filter(d=>hasPosition(d.t)).length;document.getElementById('mapOnline').textContent=online;document.getElementById('mapTrips').textContent=trips;document.getElementById('mapTelemetry').textContent=positioned;document.getElementById('mapClock').textContent=new Date().toLocaleTimeString('pt-BR');const badge=document.getElementById('mapLiveState');badge.textContent=ok?'● CENTRAL AO VIVO':'● CENTRAL OFFLINE';badge.classList.toggle('online',ok)}
async function refresh(){const data=await fetchCentral();if(data)mapDrivers=build(data);renderMapCounts();updateDriverList();renderPins();updateStats(!!data)}

document.querySelectorAll('.map-filters button').forEach(b=>b.addEventListener('click',()=>{document.querySelectorAll('.map-filters button').forEach(x=>x.classList.remove('active'));b.classList.add('active');currentFilter=b.dataset.filter;selectedDriverKey='';updateDriverList()}));
document.querySelectorAll('.map-world-tabs button').forEach(b=>b.addEventListener('click',()=>{setMapTab(b.dataset.map||'base');selectedDriverKey='';firstPosition=true;restoreNoticeText();updateDriverList();renderPins();updateStats(true)}));
document.querySelectorAll('.map-look-tabs button').forEach(b=>b.addEventListener('click',()=>{document.querySelectorAll('.map-look-tabs button').forEach(x=>x.classList.remove('active'));b.classList.add('active');const stage=document.getElementById('mapStage'),look=b.dataset.look==='realistic'?'realistic':'dark';stage.classList.toggle('map-look-realistic',look==='realistic');stage.classList.toggle('map-look-dark',look==='dark')}));
window.addEventListener('resize',()=>liveMap.invalidateSize());
setMapTab('base');setTimeout(()=>liveMap.invalidateSize(),250);refresh();setInterval(refresh,3000);
