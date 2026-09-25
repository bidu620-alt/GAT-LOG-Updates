(()=>{
const API='https://api.gatlogets2.com.br';
const SESSION_KEY='gat_driver_account_v1';
const START=Date.parse('2026-09-30T00:00:00-03:00');
const END=Date.parse('2026-11-01T00:00:00-03:00');
const CARGOS=[
 ['Logs','Toras','🪵'],['Lumber','Madeira serrada','🪚'],['Sawdust Panels','Painéis de serragem','🟫'],['Wood Shavings','Cavacos de madeira','🪵'],
 ['Potatoes','Batatas','🥔'],['Sugar','Açúcar','🧂'],['Beef','Carne bovina','🥩'],['Diesel','Combustível diesel','⛽'],
 ['Petrol','Gasolina','⛽'],['Kerosene','Querosene','🛢️'],['LPG','GLP','🔥'],['Fuel Tanker','Tanque de combustível','🚛'],
 ['Chemicals','Produtos químicos','🧪'],['Hot Chemicals','Produtos químicos quentes','☣️'],['Acid','Ácido','⚗️'],['Arsenic','Arsênico','☠️'],
 ['Chlorine','Cloro','🧪'],['Hydrochloric Acid','Ácido clorídrico','☣️'],['Pesticides','Pesticidas','🧴'],['Sulphuric Acid','Ácido sulfúrico','⚗️'],
 ['Hospital Waste','Resíduos hospitalares','☣️'],['Scrap Metals','Sucata metálica','⚙️'],['Used Car Batteries','Baterias usadas','🔋'],['Cement','Cimento','🧱'],
 ['Iron Pipes (large)','Tubos de ferro','🧰'],['Glass Panels','Painéis de vidro','🪟'],['Coal','Carvão','⚫'],['Ore','Minério','⛏️'],
 ['Low Bed Semi-trailers','Semirreboques prancha baixa','🚚'],['Excavator','Escavadeira','🚜']
];
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let profile=null,done=new Map();

function session(){try{const s=JSON.parse(sessionStorage.getItem(SESSION_KEY)||'null');return s&&s.user&&s.token?s:null}catch(_){return null}}
function eventRows(p){const rows=Array.isArray(p?.deliveries)?p.deliveries:(Array.isArray(p?.cargo_history)?p.cargo_history:[]);return rows.filter(x=>{const t=Date.parse(x?.delivered_at||x?.completed_at||x?.date||'');return Number.isFinite(t)&&t>=START&&t<END})}
function completion(rows){const out=new Map();for(const row of rows){const n=norm(row?.cargo||row?.cargo_name||row?.name);for(const [official] of CARGOS){if(n===norm(official)&&!out.has(official))out.set(official,row)}}return out}
function buildCards(active){
 const root=document.getElementById('eventCargoGrid');root.textContent='';
 CARGOS.forEach(([official,label,icon],i)=>{const row=done.get(official),card=document.createElement('article');card.className='event-cargo'+(row?' done':'')+(!active?' locked':'');card.title='Nome oficial ETS2: '+official;
 card.innerHTML='<span class="event-cargo-number">'+String(i+1).padStart(2,'0')+'</span><div class="event-cargo-icon">'+icon+'</div><h3>'+esc(label)+'</h3><div class="event-cargo-state">'+(row?'✓ CONCLUÍDA':active?'🎃 DISPONÍVEL':'🔒 AGUARDANDO EVENTO')+'</div>';
 root.appendChild(card)});
}
function updateProgress(){const n=done.size,pct=Math.round(n/30*100);document.getElementById('eventProgress').textContent=n+'/30';document.getElementById('eventProgressTitle').textContent='('+n+'/30)';document.getElementById('eventProgressBar').style.width=pct+'%'}
async function loadMine(){
 const s=session();if(!s){document.getElementById('eventAccountText').innerHTML='Entre na <a href="index.html">Conta GAT</a> para registrar o progresso automaticamente.';return}
 try{const r=await fetch(API+'/api/site/profile',{method:'POST',cache:'no-store',headers:{'Content-Type':'application/json','Authorization':'Bearer '+s.token},body:JSON.stringify({token:s.token})});const j=await r.json();if(!r.ok||!j?.profile)throw 0;profile=j.profile;done=completion(eventRows(profile));updateProgress();buildCards(Date.now()>=START&&Date.now()<END);document.getElementById('eventAccountText').textContent='@'+s.user+' • progresso atualizado pela Central GAT.'}catch(_){document.getElementById('eventAccountText').textContent='Não foi possível carregar sua Conta GAT agora.'}
}
async function loadHall(){
 if(Date.now()<START)return;const box=document.getElementById('eventHall');
 try{const r=await fetch(API+'/api/public/ranking',{cache:'no-store'}),j=await r.json();const users=(j?.ranking||[]).map(x=>x.user).filter(Boolean).slice(0,60);const winners=[];
 await Promise.all(users.map(async user=>{try{const rr=await fetch(API+'/api/public/driver?user='+encodeURIComponent(user),{cache:'no-store'}),jj=await rr.json();const rows=eventRows(jj?.profile);if(completion(rows).size===30)winners.push(user)}catch(_){}}));
 winners.sort((a,b)=>a.localeCompare(b,'pt-BR'));box.innerHTML=winners.length?winners.map(u=>'<b>🎃 @'+esc(u)+' • 30/30</b>').join(''):'<span>Ainda sem concluídos.</span>';
 }catch(_){box.innerHTML='<span>Hall indisponível no momento.</span>'}
}
function tick(){
 const now=Date.now(),locked=document.getElementById('eventLocked'),state=document.getElementById('eventState');
 if(now<START){locked.hidden=false;state.textContent='ABRE 30/09 • 00:00';const ms=START-now,d=Math.floor(ms/86400000),h=Math.floor(ms%86400000/3600000),m=Math.floor(ms%3600000/60000),s=Math.floor(ms%60000/1000);document.getElementById('eventCountdown').textContent=d+'d '+String(h).padStart(2,'0')+'h '+String(m).padStart(2,'0')+'m '+String(s).padStart(2,'0')+'s';}
 else if(now<END){locked.hidden=true;state.textContent='● EVENTO ATIVO';}
 else{locked.hidden=true;state.textContent='EVENTO ENCERRADO';}
}
function init(){tick();buildCards(Date.now()>=START&&Date.now()<END);updateProgress();loadMine();loadHall();setInterval(tick,1000);setInterval(()=>{loadMine();loadHall()},60000)}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();