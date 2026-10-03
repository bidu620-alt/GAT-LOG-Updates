(()=>{
const API='https://api.gatlogets2.com.br';
const SESSION_KEY='gat_driver_account_v1';
const REGISTRY=window.GATEventRegistry||null;
const EVENT=REGISTRY?.eventById?.('halloween_2026')||null;
const START=Date.parse(EVENT?.start||'2026-09-30T00:00:00-03:00');
const END=Date.parse(EVENT?.end||'2026-11-01T00:00:00-03:00');
const ICON_DATA='../../assets/cargo/cargo-icon-defs.json?v=2';
const CARGOS=(EVENT?.cargos||[]).map(c=>[c.official,c.label,(c.ids||[])[0]||'']);
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let profile=null,done=new Map(),icons=null;

function session(){
 try{
  let raw=localStorage.getItem(SESSION_KEY);
  if(!raw){
   raw=sessionStorage.getItem(SESSION_KEY);
   if(raw){localStorage.setItem(SESSION_KEY,raw);sessionStorage.removeItem(SESSION_KEY)}
  }
  const s=JSON.parse(raw||'null');
  return s&&s.user?s:null
 }catch(_){return null}
}
function eventRows(p){
 const rows=Array.isArray(p?.deliveries)?p.deliveries:(Array.isArray(p?.cargo_history)?p.cargo_history:[]);
 return rows.filter(x=>{
  const t=Date.parse(x?.delivered_at||x?.completed_at||x?.date||'');
  return Number.isFinite(t)&&t>=START&&t<END
 })
}
function completion(p){
 const official=REGISTRY?.progress?.(p,'halloween_2026');
 if(official?.matched instanceof Map)return official.matched;
 const rows=eventRows(p),out=new Map();
 for(const row of rows){
  for(const cargo of EVENT?.cargos||[]){
   if(!out.has(cargo.official)&&REGISTRY?.cargoMatches?.(row,cargo))out.set(cargo.official,row)
  }
 }
 return out
}
async function loadIcons(){
 try{
  const r=await fetch(ICON_DATA,{cache:'no-store'});
  const j=await r.json();
  if(r.ok)icons=j
 }catch(_){}
}
function thumb(icon){
 if(!icons?.icons?.[icon]||!Array.isArray(icons?.sheets))return '<div class="event-cargo-fallback">GAT</div>';
 const p=icons.icons[icon],sh=icons.sheets[p.s],tile=icons.tile||[160,128];
 if(!sh)return '<div class="event-cargo-fallback">GAT</div>';
 return '<span class="event-cargo-thumb" style="background-image:url(\'../../assets/cargo/'+sh.file+'?v=2\');background-size:'+sh.width+'px '+sh.height+'px;background-position:'+(-p.x*tile[0])+'px '+(-p.y*tile[1])+'px"></span>';
}
function buildCards(active){
 const root=document.getElementById('eventCargoGrid');if(!root)return;root.textContent='';
 CARGOS.forEach(([official,label,icon],i)=>{
  const row=done.get(official),card=document.createElement('article');
  card.className='event-cargo'+(row?' done':'')+(!active?' locked':'');
  card.title='Nome oficial ETS2: '+official;
  card.innerHTML='<span class="event-cargo-number">'+String(i+1).padStart(2,'0')+'</span><div class="event-cargo-visual">'+thumb(icon)+'</div><h3>'+esc(label)+'</h3><div class="event-cargo-state">'+(row?'✓ CONCLUÍDA':active?'DISPONÍVEL':'AGUARDANDO EVENTO')+'</div>';
  root.appendChild(card)
 })
}
function updateProgress(){
 const n=done.size,pct=Math.round(n/Math.max(1,Number(EVENT?.goal)||30)*100);
 const a=document.getElementById('eventProgress'),b=document.getElementById('eventProgressTitle'),c=document.getElementById('eventProgressBar');
 if(a)a.textContent=n+'/30';if(b)b.textContent='('+n+'/30)';if(c)c.style.width=pct+'%'
}
async function fetchPrivateProfile(s){
 if(!s?.token)return null;
 try{
  const r=await fetch(API+'/api/site/profile',{
   method:'POST',cache:'no-store',
   headers:{'Content-Type':'application/json','Authorization':'Bearer '+s.token},
   body:JSON.stringify({token:s.token})
  });
  const j=await r.json();
  return r.ok&&j?.profile?j.profile:null
 }catch(_){return null}
}
async function fetchPublicProfile(user){
 if(!user)return null;
 try{
  const r=await fetch(API+'/api/public/driver?user='+encodeURIComponent(user),{cache:'no-store'});
  const j=await r.json();
  return r.ok&&j?.profile?j.profile:null
 }catch(_){return null}
}
async function loadMine(){
 const s=session(),accountLink=document.querySelector('.event-account-link');
 if(accountLink)accountLink.textContent=s?.user?'@'+s.user:'CONTA GAT';
 if(!s?.user){
  document.getElementById('eventAccountText').textContent='Entre na sua Conta GAT para registrar o progresso automaticamente.';
  done=new Map();updateProgress();buildCards(Date.now()>=START&&Date.now()<END);return
 }
 try{
  profile=await fetchPrivateProfile(s);
  let source='Central GAT';
  if(!profile){profile=await fetchPublicProfile(s.user);source='perfil público'}
  if(!profile)throw 0;
  done=completion(profile);
  updateProgress();buildCards(Date.now()>=START&&Date.now()<END);
  const rows=eventRows(profile);
  document.getElementById('eventAccountText').textContent='@'+s.user+' • '+done.size+'/30 reconhecidas • '+rows.length+' entregas no período • '+source+'.'
 }catch(_){
  document.getElementById('eventAccountText').textContent='Não foi possível carregar o progresso da Conta GAT agora.'
 }
}
async function loadHall(){
 if(Date.now()<START)return;
 const box=document.getElementById('eventHall');if(!box)return;
 try{
  const r=await fetch(API+'/api/public/ranking',{cache:'no-store'}),j=await r.json();
  const users=(j?.ranking||[]).map(x=>x.user).filter(Boolean).slice(0,60),winners=[];
  await Promise.all(users.map(async user=>{
   try{
    const p=await fetchPublicProfile(user);
    const official=REGISTRY?.progress?.(p,'halloween_2026');
    if(official?.completed||completion(p).size>=30)winners.push(user)
   }catch(_){}
  }));
  winners.sort((a,b)=>a.localeCompare(b,'pt-BR'));
  box.innerHTML=winners.length?winners.map(u=>'<b>30/30 • @'+esc(u)+'</b>').join(''):'<span>Ainda sem concluídos.</span>'
 }catch(_){box.innerHTML='<span>Hall indisponível no momento.</span>'}
}
function tick(){
 const now=Date.now(),locked=document.getElementById('eventLocked'),state=document.getElementById('eventState');
 if(now<START){
  if(locked)locked.hidden=false;if(state)state.textContent='ABRE 30/09 • 00:00';
  const ms=START-now,d=Math.floor(ms/86400000),h=Math.floor(ms%86400000/3600000),m=Math.floor(ms%3600000/60000),ss=Math.floor(ms%60000/1000);
  const c=document.getElementById('eventCountdown');if(c)c.textContent=d+'d '+String(h).padStart(2,'0')+'h '+String(m).padStart(2,'0')+'m '+String(ss).padStart(2,'0')+'s'
 }else if(now<END){
  if(locked)locked.hidden=true;if(state)state.textContent='● EVENTO ATIVO'
 }else{
  if(locked)locked.hidden=true;if(state)state.textContent='EVENTO ENCERRADO'
 }
}
async function init(){
 tick();await loadIcons();buildCards(Date.now()>=START&&Date.now()<END);updateProgress();
 await loadMine();loadHall();
 setInterval(tick,1000);
 setInterval(()=>{loadMine();loadHall()},30000);
 window.addEventListener('storage',e=>{if(e.key===SESSION_KEY)loadMine()});
 window.addEventListener('gat-account-change',()=>loadMine())
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();