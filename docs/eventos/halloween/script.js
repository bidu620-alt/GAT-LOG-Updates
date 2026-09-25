(()=>{
const API='https://api.gatlogets2.com.br';
const SESSION_KEY='gat_driver_account_v1';
const START=Date.parse('2026-09-30T00:00:00-03:00');
const END=Date.parse('2026-11-01T00:00:00-03:00');
const ICON_DATA='../../assets/cargo/cargo-icon-defs.json?v=1';
const CARGOS=[
 ['Logs','Toras','logs'],['Lumber','Madeira serrada','lumber'],['Sawdust Panels','Painéis de serragem','sawpanels'],['Wood Shavings','Cavacos de madeira','wshavings'],
 ['Potatoes','Batatas','potatoes'],['Sugar','Açúcar','sugar'],['Beef','Carne bovina','beef_meat'],['Diesel','Combustível diesel','diesel'],
 ['Petrol','Gasolina','petrol'],['Kerosene','Querosene','kerosene'],['LPG','GLP','lpg'],['Fuel Tanker','Tanque de combustível','fueltanker'],
 ['Chemicals','Produtos químicos','chemicals'],['Hot Chemicals','Produtos químicos quentes','hchemicals'],['Acid','Ácido','acid'],['Arsenic','Arsênico','arsenic'],
 ['Chlorine','Cloro','chlorine'],['Hydrochloric Acid','Ácido clorídrico','hydrochlor'],['Pesticides','Pesticidas','pesticide'],['Sulphuric Acid','Ácido sulfúrico','sulfuric'],
 ['Hospital Waste','Resíduos hospitalares','hwaste'],['Scrap Metals','Sucata metálica','scrap_metals'],['Used Car Batteries','Baterias usadas','used_battery'],['Cement','Cimento','cement'],
 ['Iron Pipes (large)','Tubos de ferro','iron_pipes'],['Glass Panels','Painéis de vidro','glass'],['Coal','Carvão','coal'],['Ore','Minério','ore'],
 ['Low Bed Semi-trailers','Semirreboques prancha baixa','overweight'],['Excavator','Escavadeira','excavator']
];
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let profile=null,done=new Map(),icons=null;

function session(){try{const s=JSON.parse(sessionStorage.getItem(SESSION_KEY)||'null');return s&&s.user&&s.token?s:null}catch(_){return null}}
function eventRows(p){const rows=Array.isArray(p?.deliveries)?p.deliveries:(Array.isArray(p?.cargo_history)?p.cargo_history:[]);return rows.filter(x=>{const t=Date.parse(x?.delivered_at||x?.completed_at||x?.date||'');return Number.isFinite(t)&&t>=START&&t<END})}
function completion(rows){const out=new Map();for(const row of rows){const n=norm(row?.cargo||row?.cargo_name||row?.name);for(const [official] of CARGOS){if(n===norm(official)&&!out.has(official))out.set(official,row)}}return out}
async function loadIcons(){try{const r=await fetch(ICON_DATA,{cache:'force-cache'});const j=await r.json();if(r.ok)icons=j}catch(_){}}
function thumb(icon){
 if(!icons?.icons?.[icon]||!Array.isArray(icons?.sheets))return '<div class="event-cargo-fallback">GAT</div>';
 const p=icons.icons[icon],sh=icons.sheets[p.s],tile=icons.tile||[160,128];if(!sh)return '<div class="event-cargo-fallback">GAT</div>';
 return '<span class="event-cargo-thumb" style="background-image:url(\'../../assets/cargo/'+sh.file+'?v=1\');background-size:'+sh.width+'px '+sh.height+'px;background-position:'+(-p.x*tile[0])+'px '+(-p.y*tile[1])+'px"></span>';
}
function buildCards(active){
 const root=document.getElementById('eventCargoGrid');root.textContent='';
 CARGOS.forEach(([official,label,icon],i)=>{const row=done.get(official),card=document.createElement('article');card.className='event-cargo'+(row?' done':'')+(!active?' locked':'');card.title='Nome oficial ETS2: '+official;
 card.innerHTML='<span class="event-cargo-number">'+String(i+1).padStart(2,'0')+'</span><div class="event-cargo-visual">'+thumb(icon)+'</div><h3>'+esc(label)+'</h3><div class="event-cargo-state">'+(row?'✓ CONCLUÍDA':active?'DISPONÍVEL':'AGUARDANDO EVENTO')+'</div>';
 root.appendChild(card)});
}
function updateProgress(){const n=done.size,pct=Math.round(n/30*100);document.getElementById('eventProgress').textContent=n+'/30';document.getElementById('eventProgressTitle').textContent='('+n+'/30)';document.getElementById('eventProgressBar').style.width=pct+'%'}
async function loadMine(){
 const s=session();if(!s){document.getElementById('eventAccountText').textContent='Entre na sua Conta GAT para registrar o progresso automaticamente.';return}
 try{const r=await fetch(API+'/api/site/profile',{method:'POST',cache:'no-store',headers:{'Content-Type':'application/json','Authorization':'Bearer '+s.token},body:JSON.stringify({token:s.token})});const j=await r.json();if(!r.ok||!j?.profile)throw 0;profile=j.profile;done=completion(eventRows(profile));updateProgress();buildCards(Date.now()>=START&&Date.now()<END);document.getElementById('eventAccountText').textContent='@'+s.user+' • progresso atualizado pela Central GAT.'}catch(_){document.getElementById('eventAccountText').textContent='Não foi possível carregar sua Conta GAT agora.'}
}
async function loadHall(){
 if(Date.now()<START)return;const box=document.getElementById('eventHall');
 try{const r=await fetch(API+'/api/public/ranking',{cache:'no-store'}),j=await r.json();const users=(j?.ranking||[]).map(x=>x.user).filter(Boolean).slice(0,60);const winners=[];
 await Promise.all(users.map(async user=>{try{const rr=await fetch(API+'/api/public/driver?user='+encodeURIComponent(user),{cache:'no-store'}),jj=await rr.json();const rows=eventRows(jj?.profile);if(completion(rows).size===30)winners.push(user)}catch(_){}}));
 winners.sort((a,b)=>a.localeCompare(b,'pt-BR'));box.innerHTML=winners.length?winners.map(u=>'<b>30/30 • @'+esc(u)+'</b>').join(''):'<span>Ainda sem concluídos.</span>';
 }catch(_){box.innerHTML='<span>Hall indisponível no momento.</span>'}
}
function tick(){
 const now=Date.now(),locked=document.getElementById('eventLocked'),state=document.getElementById('eventState');
 if(now<START){locked.hidden=false;state.textContent='ABRE 30/09 • 00:00';const ms=START-now,d=Math.floor(ms/86400000),h=Math.floor(ms%86400000/3600000),m=Math.floor(ms%3600000/60000),s=Math.floor(ms%60000/1000);document.getElementById('eventCountdown').textContent=d+'d '+String(h).padStart(2,'0')+'h '+String(m).padStart(2,'0')+'m '+String(s).padStart(2,'0')+'s';}
 else if(now<END){locked.hidden=true;state.textContent='● EVENTO ATIVO';}
 else{locked.hidden=true;state.textContent='EVENTO ENCERRADO';}
}
async function init(){tick();await loadIcons();buildCards(Date.now()>=START&&Date.now()<END);updateProgress();loadMine();loadHall();setInterval(tick,1000);setInterval(()=>{loadMine();loadHall()},60000)}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();