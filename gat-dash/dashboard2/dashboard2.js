(()=>{
'use strict';
const $=id=>document.getElementById(id);
const STORE='roadlife_dashboard2_v1';
const state={theme:'green',layout:'horizontal',host:'127.0.0.1',lastTelemetry:0,polling:false,timer:null};
const truckMap={
  volvo:'assets/trucks/volvo.webp',
  scania:'assets/trucks/scania.webp',
  mercedes:'assets/trucks/mercedes.webp',
  daf:'assets/trucks/daf.webp',
  man:'assets/trucks/man.webp',
  iveco:'assets/trucks/iveco.webp',
  renault:'assets/trucks/renault.webp'
};
const cargoMap={
  tractor:'assets/cargo/tractor.webp',
  tractors:'assets/cargo/tractor.webp',
  lpg:'assets/cargo/lpg.webp',
  glp:'assets/cargo/lpg.webp',
  acid:'assets/cargo/acid.webp',
  arsenic:'assets/cargo/arsenic.webp',
  excavator:'assets/cargo/excavator.webp',
  lumber:'assets/cargo/lumber.webp',
  diesel:'assets/cargo/diesel.webp',
  dynamite:'assets/cargo/dynamite.webp',
  hwaste:'assets/cargo/hospital_waste.webp',
  hospital_waste:'assets/cargo/hospital_waste.webp'
};

function first(obj,...paths){for(const path of paths){let v=obj;for(const p of path.split('.')){if(v==null)break;v=v[p]}if(v!==undefined&&v!==null)return v}return undefined}
function n(v){v=Number(v);return Number.isFinite(v)?v:0}
function clamp(v,a,b){return Math.max(a,Math.min(b,v))}
function norm(v){return String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,'_').replace(/^_+|_+$/g,'')}
function pctDamage(v){let x=n(v);if(x<=1.001)x*=100;return clamp(x,0,100)}
function gearText(t){const g=n(first(t,'truck.displayedGear','Truck.DisplayedGear'));return g<0?'R'+Math.abs(g):g===0?'N':'D'+g}
function setText(id,v){const el=$(id);if(el)el.textContent=v}
function fmtKm(v){return Math.max(0,v).toLocaleString('pt-BR',{maximumFractionDigits:0})+' km'}
function fmtTimeDuration(seconds){seconds=Math.max(0,Math.round(n(seconds)));if(!seconds)return '—';const h=Math.floor(seconds/3600),m=Math.floor((seconds%3600)/60);return h?String(h).padStart(2,'0')+'h '+String(m).padStart(2,'0')+'min':String(m)+' min'}
function brasiliaNow(){const now=new Date();setText('brasiliaTime',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',second:'2-digit',hour12:false}).format(now));setText('brasiliaDate',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',weekday:'short',day:'2-digit',month:'2-digit',year:'numeric'}).format(now))}
function estimatedArrival(eta){if(!eta)return '—';const d=new Date(eta);if(Number.isNaN(d.getTime()))return '—';return new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',hour12:false}).format(d)}
function estimatedSeconds(eta){if(!eta)return 0;const d=new Date(eta);if(Number.isNaN(d.getTime()))return 0;return Math.max(0,(d.getTime()-Date.now())/1000)}
function chooseTruck(make,model){const k=norm(make+' '+model);for(const brand of Object.keys(truckMap))if(k.includes(brand))return truckMap[brand];return ''}
function chooseCargo(id,name){const a=norm(id),b=norm(name);for(const key of Object.keys(cargoMap))if(a===key||a.includes(key)||b===key||b.includes(key))return cargoMap[key];return ''}
function showMappedImage(imgId,fallbackId,src){const img=$(imgId),fallback=$(fallbackId);if(!img||!fallback)return;if(!src){img.hidden=true;fallback.hidden=false;return}img.onload=()=>{img.hidden=false;fallback.hidden=true};img.onerror=()=>{img.hidden=true;fallback.hidden=false};img.src=src}
function gameConnected(t){return !!first(t,'game.connected','Game.Connected')}

function render(t){
 const connected=gameConnected(t);
 const speed=Math.abs(n(first(t,'truck.speed','Truck.Speed')));
 const limit=n(first(t,'navigation.speedLimit','Navigation.SpeedLimit'));
 const fuel=n(first(t,'truck.fuel','Truck.Fuel')),cap=n(first(t,'truck.fuelCapacity','Truck.FuelCapacity'));
 const fuelPct=cap>0?clamp(fuel/cap*100,0,100):0;
 const source=first(t,'job.sourceCity','Job.SourceCity')||'—',dest=first(t,'job.destinationCity','Job.DestinationCity')||'—';
 const cargo=first(t,'job.cargo','Job.Cargo')||'Sem carga',cargoId=first(t,'job.cargoId','Job.CargoId','job.cargo_id','Job.Cargo_Id')||'';
 const mass=n(first(t,'job.cargoMass','Job.CargoMass'));
 const remainKm=n(first(t,'navigation.estimatedDistance','Navigation.EstimatedDistance'))/1000;
 const eta=first(t,'navigation.estimatedTime','Navigation.EstimatedTime');
 const make=first(t,'truck.make','Truck.Make','truck.makeName','Truck.MakeName')||'',model=first(t,'truck.model','Truck.Model','truck.modelName','Truck.ModelName')||'';
 const engine=pctDamage(first(t,'truck.wearEngine','Truck.WearEngine')),trans=pctDamage(first(t,'truck.wearTransmission','Truck.WearTransmission')),cabin=pctDamage(first(t,'truck.wearCabin','Truck.WearCabin')),chassis=pctDamage(first(t,'truck.wearChassis','Truck.WearChassis')),wheels=pctDamage(first(t,'truck.wearWheels','Truck.WearWheels'));
 const trailer=pctDamage(first(t,'trailer.wear','Trailer.Wear','trailer.damage','Trailer.Damage'));
 const cargoDamage=pctDamage(first(t,'job.cargoDamage','Job.CargoDamage','gameplay.jobDelivered.cargoDamage'));
 setText('speedValue',Math.round(speed));setText('speedLimit',limit>0?Math.round(limit):'—');setText('gearValue',gearText(t));
 setText('sourceCity',source);setText('destinationCity',dest);setText('cargoName',cargo);setText('cargoMass',(mass/1000).toLocaleString('pt-BR',{maximumFractionDigits:1})+' t');setText('remainingKm',fmtKm(remainKm));
 setText('fuelPct',Math.round(fuelPct)+'%');setText('fuelLiters',Math.round(fuel).toLocaleString('pt-BR')+' / '+Math.round(cap).toLocaleString('pt-BR')+' L');$('fuelBar').style.width=fuelPct+'%';
 setText('etaClock',estimatedArrival(eta));setText('etaDuration',fmtTimeDuration(estimatedSeconds(eta)));
 setText('truckName',make||'Caminhão');setText('truckModel',model||'Modelo não informado');
 setText('damageEngine',engine.toFixed(1).replace('.',',')+'%');setText('damageTransmission',trans.toFixed(1).replace('.',',')+'%');setText('damageCabin',cabin.toFixed(1).replace('.',',')+'%');setText('damageChassis',chassis.toFixed(1).replace('.',',')+'%');setText('damageWheels',wheels.toFixed(1).replace('.',',')+'%');setText('damageTrailer',trailer.toFixed(1).replace('.',',')+'%');setText('damageCargo',cargoDamage.toFixed(1).replace('.',',')+'%');
 setText('rpmValue',Math.round(n(first(t,'truck.engineRpm','Truck.EngineRpm'))).toLocaleString('pt-BR'));setText('waterTemp',Math.round(n(first(t,'truck.waterTemperature','Truck.WaterTemperature')))+'°C');setText('batteryVoltage',n(first(t,'truck.batteryVoltage','Truck.BatteryVoltage')).toFixed(1).replace('.',',')+' V');
 $('connectionDot').classList.toggle('online',connected);setText('connectionText',connected?'ETS2 ONLINE':'ETS2 DESCONECTADO');setText('telemetryStatus',connected?'TruckSim GPS: telemetria ativa':'TruckSim GPS: servidor conectado • jogo aguardando');
 showMappedImage('truckImage','truckFallback',chooseTruck(make,model));showMappedImage('cargoImage','cargoFallback',chooseCargo(cargoId,cargo));
}

async function poll(){
 if(state.polling)return;state.polling=true;
 try{
   const host=(state.host||'127.0.0.1').replace(/^https?:\/\//,'').replace(/\/$/,'');
   const url='http://'+host+(host.includes(':')?'':':31377')+'/api/ets2/telemetry?t='+Date.now();
   const r=await fetch(url,{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);
   const t=await r.json();state.lastTelemetry=Date.now();render(t);
 }catch(e){setText('telemetryStatus','TruckSim GPS: sem conexão');$('connectionDot').classList.remove('online');setText('connectionText','SEM TELEMETRIA')}
 finally{state.polling=false}
}
window.dashboard2PushTelemetry=payload=>{let t=payload;if(typeof t==='string'){try{t=JSON.parse(t)}catch{return}}if(t&&typeof t==='object'){state.lastTelemetry=Date.now();render(t)}};

function load(){try{const s=JSON.parse(localStorage.getItem(STORE)||'{}');Object.assign(state,{theme:s.theme||'green',layout:s.layout||'horizontal',host:s.host||'127.0.0.1'})}catch{}applyPrefs()}
function save(){localStorage.setItem(STORE,JSON.stringify({theme:state.theme,layout:state.layout,host:state.host}));applyPrefs()}
function applyPrefs(){document.body.dataset.theme=state.theme;document.body.dataset.layout=state.layout;$('telemetryHost').value=state.host}
$('settingsBtn').onclick=()=>$('settingsModal').classList.remove('hidden');$('closeSettings').onclick=()=>$('settingsModal').classList.add('hidden');
document.querySelectorAll('[data-theme-choice]').forEach(b=>b.onclick=()=>{state.theme=b.dataset.themeChoice;save()});
document.querySelectorAll('[data-layout-choice]').forEach(b=>b.onclick=()=>{state.layout=b.dataset.layoutChoice;save()});
$('saveSettings').onclick=()=>{state.host=$('telemetryHost').value.trim()||'127.0.0.1';save();$('settingsModal').classList.add('hidden');poll()};
setInterval(brasiliaNow,1000);setInterval(poll,1000);brasiliaNow();load();poll();
})();
