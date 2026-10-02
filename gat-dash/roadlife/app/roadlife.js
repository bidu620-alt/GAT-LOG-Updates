(()=>{'use strict';
const $=id=>document.getElementById(id),STORE='roadlife_dashboard_v1';
const state={opacity:.95};
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const fmtKm=v=>Math.max(0,Number(v)||0).toLocaleString('pt-BR',{maximumFractionDigits:0})+' km';
const set=(id,v)=>{const e=$(id);if(e)e.textContent=v};
function arrival(v){if(!v)return '—';const d=new Date(v);return Number.isNaN(d.getTime())?'—':new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',hour12:false}).format(d)}
function clock(){const d=new Date();set('timeValue',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',hour:'2-digit',minute:'2-digit',hour12:false}).format(d));set('dateValue',new Intl.DateTimeFormat('pt-BR',{timeZone:'America/Sao_Paulo',day:'2-digit',month:'2-digit',year:'numeric'}).format(d))}
function post(o){try{if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(o)}catch{}}
function load(){try{Object.assign(state,JSON.parse(localStorage.getItem(STORE)||'{}'))}catch{}document.body.style.opacity=state.opacity;$('opacityRange').value=Math.round(state.opacity*100)}
function save(){localStorage.setItem(STORE,JSON.stringify(state));document.body.style.opacity=state.opacity}
function renderRaw(raw){
 const t=window.RoadLifeTelemetry.normalize(raw);
 set('connectionText',t.connected?'ONLINE':'SEM TELEMETRIA');$('connectionDot').classList.toggle('on',t.connected);
 set('speedValue',Math.round(t.speed));set('speedLimit',t.speedLimit>0?Math.round(t.speedLimit):'—');set('gearValue',t.gear);set('temperatureValue',Math.round(t.temperature)+'°C');set('cruiseValue',t.cruiseOn?Math.round(t.cruiseSpeed):'OFF');
 set('sourceCity',t.source);set('destinationCity',t.destination);set('cargoName',t.cargo);set('dynamicCargoName',t.cargo);
 const mass=t.cargoMass>0?(t.cargoMass/1000).toLocaleString('pt-BR',{maximumFractionDigits:1})+' t':'0 t';set('cargoMass',mass);set('dynamicCargoMass',mass);
 set('remainingKm',fmtKm(t.remainingKm));set('remainingKm2',fmtKm(t.remainingKm));set('tripKm',t.tripKm>0?fmtKm(t.tripKm):'—');set('etaValue',arrival(t.eta));
 set('fuelLiters',Math.round(t.fuel).toLocaleString('pt-BR')+' / '+Math.round(t.fuelCapacity).toLocaleString('pt-BR')+' L');set('fuelPct',Math.round(t.fuelPct)+'%');$('fuelBar').style.width=clamp(t.fuelPct,0,100)+'%';
 set('truckValue',((t.truckMake+' '+t.truckModel).trim()||'Aguardando'));
 set('damageTruck',t.truckDamage.toFixed(1).replace('.',',')+'%');set('damageTrailer',t.trailerDamage.toFixed(1).replace('.',',')+'%');set('damageCargo',t.cargoDamage.toFixed(1).replace('.',',')+'%');
 const total=t.tripKm>0?t.tripKm:t.remainingKm,progress=total>0?clamp((total-t.remainingKm)/total*100,0,100):0;$('routeProgress').style.width=progress+'%';$('routeKnob').style.left=progress+'%';set('routeStart',progress>0?Math.round(progress)+'%':'0 km');
}
window.roadLifePushTelemetry=p=>{let raw=p;if(typeof raw==='string'){try{raw=JSON.parse(raw)}catch{return}}if(raw&&typeof raw==='object')renderRaw(raw)};
$('settingsBtn').onclick=e=>{e.stopPropagation();$('settingsModal').classList.remove('hidden')};$('closeSettings').onclick=()=>$('settingsModal').classList.add('hidden');
$('minimizeBtn').onclick=e=>{e.stopPropagation();post({type:'minimize'})};$('closeBtn').onclick=e=>{e.stopPropagation();post({type:'close'})};
$('opacityRange').oninput=e=>{state.opacity=Number(e.target.value)/100;save()};
$('dashboard').addEventListener('mousedown',e=>{if(e.button===0&&!e.target.closest('button,input,.modal'))post({type:'drag'})});
setInterval(clock,1000);clock();load();
})();