(()=>{'use strict';
const $=id=>document.getElementById(id),STORE='roadlife_dashboard_horizontal_v1';
const state={opacity:.94};
function post(o){try{if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(o)}catch{}}
function first(obj,...paths){for(const path of paths){let v=obj;for(const p of path.split('.')){if(v==null)break;v=v[p]}if(v!==undefined&&v!==null)return v}}
function num(v){v=Number(v);return Number.isFinite(v)?v:0}
function clamp(v,a,b){return Math.max(a,Math.min(b,v))}
function set(id,v){const e=$(id);if(e)e.textContent=v}
function dmg(v){let x=num(v);if(x<=1.001)x*=100;return clamp(x,0,100)}
function fmtKm(v){return Math.max(0,num(v)).toLocaleString('pt-BR',{maximumFractionDigits:0})+' km'}
function gear(t){const g=num(first(t,'truck.displayedGear','Truck.DisplayedGear'));return g<0?'R'+Math.abs(g):g===0?'N':'D'+g}
function brTime(){const d=new Date();set('brasiliaTime',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',hour12:false}).format(d));set('brasiliaDate',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',day:'2-digit',month:'2-digit',year:'numeric'}).format(d))}
function arrival(v){if(!v)return '—';const d=new Date(v);return Number.isNaN(d.getTime())?'—':new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',hour12:false}).format(d)}
function pct(id,v){const e=$(id);if(e)e.style.width=clamp(v,0,100)+'%'}
function norm(v){return String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase()}
function cargoArt(name){
 const n=norm(name),art=$('cargoArt'),glyph=$('cargoGraphic');if(!art||!glyph)return;
 art.className='cargo-art generic';glyph.textContent='▰';
 if(n.includes('arroz')||n.includes('rice'))art.className='cargo-art rice';
 else if(n.includes('diesel')||n.includes('combust')||n.includes('fuel'))glyph.textContent='◉';
 else if(n.includes('trator')||n.includes('tractor')||n.includes('maquina'))glyph.textContent='⚙';
 else if(n.includes('madeira')||n.includes('lumber')||n.includes('wood'))glyph.textContent='▥';
 else if(n.includes('gado')||n.includes('cattle'))glyph.textContent='◇';
}
function load(){try{Object.assign(state,JSON.parse(localStorage.getItem(STORE)||'{}'))}catch{}document.body.style.opacity=String(state.opacity);$('opacityRange').value=Math.round(state.opacity*100)}
function save(){localStorage.setItem(STORE,JSON.stringify(state));document.body.style.opacity=String(state.opacity)}
function render(t){
 const connected=!!first(t,'game.connected','Game.Connected');
 const speed=Math.abs(num(first(t,'truck.speed','Truck.Speed'))),limit=num(first(t,'navigation.speedLimit','Navigation.SpeedLimit'));
 const fuel=num(first(t,'truck.fuel','Truck.Fuel')),cap=num(first(t,'truck.fuelCapacity','Truck.FuelCapacity')),fp=cap>0?clamp(fuel/cap*100,0,100):0;
 const source=first(t,'job.sourceCity','Job.SourceCity')||'—',dest=first(t,'job.destinationCity','Job.DestinationCity')||'—';
 const cargo=first(t,'job.cargo','Job.Cargo')||'Sem carga',mass=num(first(t,'job.cargoMass','Job.CargoMass'));
 const remain=num(first(t,'navigation.estimatedDistance','Navigation.EstimatedDistance'))/1000;
 const planned=num(first(t,'job.plannedDistanceKm','Job.PlannedDistanceKm','job.distanceKm','Job.DistanceKm'));
 const eta=first(t,'navigation.estimatedTime','Navigation.EstimatedTime');
 const make=first(t,'truck.make','Truck.Make','truck.makeName','Truck.MakeName')||'',model=first(t,'truck.model','Truck.Model','truck.modelName','Truck.ModelName')||'';
 const wears=['Engine','Transmission','Cabin','Chassis','Wheels'].map(x=>dmg(first(t,'truck.wear'+x,'Truck.Wear'+x)));
 const truckDamage=Math.max(...wears),trailerDamage=dmg(first(t,'trailer.wear','Trailer.Wear','trailer.damage','Trailer.Damage')),cargoDamage=dmg(first(t,'job.cargoDamage','Job.CargoDamage','gameplay.jobDelivered.cargoDamage'));
 set('speedValue',Math.round(speed));set('speedLimit',limit>0?Math.round(limit):'—');set('gearValue',gear(t));set('waterTemp',Math.round(num(first(t,'truck.waterTemperature','Truck.WaterTemperature')))+'°C');
 set('cruiseValue',first(t,'truck.cruiseControlOn','Truck.CruiseControlOn')?Math.round(num(first(t,'truck.cruiseControlSpeed','Truck.CruiseControlSpeed'))):'OFF');
 set('sourceCity',source);set('destinationCity',dest);set('cargoName',cargo);set('dynamicCargoName',cargo);cargoArt(cargo);
 const massText=mass>0?(mass/1000).toLocaleString('pt-BR',{maximumFractionDigits:1})+' t':'0 t';set('cargoMass',massText);set('dynamicCargoMass',massText);
 set('remainingKm',fmtKm(remain));set('remainingKm2',fmtKm(remain));set('tripKm',planned>0?fmtKm(planned):'—');set('etaClock',arrival(eta));
 set('fuelPct',Math.round(fp)+'%');set('fuelLiters',Math.round(fuel).toLocaleString('pt-BR')+' / '+Math.round(cap).toLocaleString('pt-BR')+' L');pct('fuelBar',fp);
 set('truckDisplay',((make+' '+model).trim()||'Aguardando'));
 set('damageTruck',truckDamage.toFixed(1).replace('.',',')+'%');set('damageTrailer',trailerDamage.toFixed(1).replace('.',',')+'%');set('damageCargo',cargoDamage.toFixed(1).replace('.',',')+'%');
 $('connectionDot').classList.toggle('on',connected);set('connectionText',connected?'ONLINE':'SEM TELEMETRIA');
 const total=planned>0?planned:remain,progress=total>0?clamp((total-remain)/total*100,0,100):0;pct('routeProgress',progress);set('routeStartText',progress>0?Math.round(progress)+'%':'0 km');
}
window.dashboardHorizontalPushTelemetry=p=>{let t=p;if(typeof t==='string'){try{t=JSON.parse(t)}catch{return}}if(t&&typeof t==='object')render(t)};
$('settingsBtn').onclick=()=>$('settingsModal').classList.remove('hidden');$('closeSettings').onclick=()=>$('settingsModal').classList.add('hidden');
$('minimizeBtn').onclick=()=>post({type:'minimize'});$('closeBtn').onclick=()=>post({type:'close'});
$('opacityRange').oninput=e=>{state.opacity=Number(e.target.value)/100;save()};
document.querySelectorAll('.drag-zone,.route,.journey-bottom').forEach(el=>el.addEventListener('mousedown',e=>{if(e.button===0&&!e.target.closest('button,input'))post({type:'drag'})}));
setInterval(brTime,1000);brTime();load();post({type:'ready',layout:'horizontal'});
})();