const RAW='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/';
const GAT_SERVERS=[
  {label:'BIDUZAO - DOUGLAS',url:'https://douglas.tail4577e8.ts.net'},
  {label:'JC - JEAN',url:'https://jean-jc.tailf14a00.ts.net'}
];
const LIVE_PATH='/api/public/live';
const LOGIN_PATH='/api/public/site-login';
const SESSION_PATH='/api/public/site-session';
const TOKEN_KEY='gat_site_tokens_v1';
const REFRESH_MS=5000;
const FRESH_MS=20000;
let heroTrips=[];
let heroIndex=0;
let liveTimer=null;
let heroTimer=null;
let siteTokens=loadTokens();

async function bindRelease(manifest,versionId,buttonId){
  const versionEl=document.getElementById(versionId),button=document.getElementById(buttonId);
  try{
    const r=await fetch(RAW+manifest,{cache:'no-store'});
    if(!r.ok) throw new Error('manifest');
    const data=await r.json();
    versionEl.textContent='Versão '+(data.display_version||data.version||'atual');
    const url=data.setup_url||data.url||'';
    if(url){button.href=url;button.classList.remove('disabled');}
  }catch(_){versionEl.textContent='Versão disponível no GitHub';}
}
bindRelease('server_dotnet_version.json','serverVersion','serverDownload');
bindRelease('client_dotnet_version.json','clientVersion','clientDownload');

function loadTokens(){
  try{const x=JSON.parse(sessionStorage.getItem(TOKEN_KEY)||'{}');return x&&typeof x==='object'?x:{};}catch(_){return {};}
}
function saveTokens(){try{sessionStorage.setItem(TOKEN_KEY,JSON.stringify(siteTokens));}catch(_){}}
function clearTokens(){siteTokens={};try{sessionStorage.removeItem(TOKEN_KEY);}catch(_){}}
function tokenFor(server){return String(siteTokens[server.url]||'');}
function authHeaders(server,extra={}){const token=tokenFor(server);return token?{...extra,Authorization:'Bearer '+token}:extra;}
function setLoginStatus(text,type=''){const e=document.getElementById('loginStatus');e.textContent=text;e.className='login-status'+(type?' '+type:'');}

function stopLive(){if(liveTimer){clearInterval(liveTimer);liveTimer=null;}if(heroTimer){clearInterval(heroTimer);heroTimer=null;}}
function lockSite(message='Faça login para acessar o GAT LOG.'){
  stopLive();
  document.body.classList.add('site-locked');
  document.getElementById('loginPassword').value='';
  setLoginStatus(message,message.includes('expir')?'error':'');
}
function unlockSite(){
  document.body.classList.remove('site-locked');
  setLoginStatus('Acesso liberado.','ok');
  startLive();
}

async function validateSession(server){
  const token=tokenFor(server);if(!token)return false;
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),4000);
  try{
    const r=await fetch(server.url+SESSION_PATH,{cache:'no-store',signal:controller.signal,headers:{Authorization:'Bearer '+token}});
    return r.ok;
  }catch(_){return false;}finally{clearTimeout(timer);}
}

async function restoreSession(){
  const configured=GAT_SERVERS.filter(s=>tokenFor(s));
  if(!configured.length){lockSite('Faça login para acessar o GAT LOG.');return;}
  setLoginStatus('Validando sessão...');
  const checks=await Promise.all(configured.map(async s=>({server:s,ok:await validateSession(s)})));
  checks.filter(x=>!x.ok).forEach(x=>delete siteTokens[x.server.url]);
  saveTokens();
  if(checks.some(x=>x.ok)){unlockSite();}else{clearTokens();lockSite('Sua sessão expirou. Entre novamente.');}
}

async function loginServer(server,user,password){
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),6000);
  try{
    const r=await fetch(server.url+LOGIN_PATH,{method:'POST',cache:'no-store',signal:controller.signal,headers:{'Content-Type':'application/json'},body:JSON.stringify({user,password})});
    let data=null;try{data=await r.json();}catch(_){}
    if(r.ok&&data?.ok&&data.token)return {server,ok:true,token:data.token};
    if(r.status===401)return {server,ok:false,reason:'invalid'};
    if(r.status===404)return {server,ok:false,reason:'update'};
    if(r.status===503&&data?.error==='site_access_not_configured')return {server,ok:false,reason:'not_configured'};
    return {server,ok:false,reason:'unavailable'};
  }catch(_){return {server,ok:false,reason:'unavailable'};}finally{clearTimeout(timer);}
}

async function submitLogin(event){
  event.preventDefault();
  const user=document.getElementById('loginUser').value.trim();
  const password=document.getElementById('loginPassword').value;
  const button=document.getElementById('loginButton');
  if(!user||!password){setLoginStatus('Preencha usuário e senha.','error');return;}
  button.disabled=true;button.textContent='ENTRANDO...';setLoginStatus('Validando acesso nos GAT Servers...');
  try{
    const results=await Promise.all(GAT_SERVERS.map(s=>loginServer(s,user,password)));
    const ok=results.filter(x=>x.ok);
    if(ok.length){
      siteTokens={};ok.forEach(x=>siteTokens[x.server.url]=x.token);saveTokens();
      document.getElementById('loginPassword').value='';
      unlockSite();
      return;
    }
    clearTokens();
    if(results.some(x=>x.reason==='invalid'))setLoginStatus('Usuário ou senha incorretos.','error');
    else if(results.some(x=>x.reason==='not_configured'))setLoginStatus('O login do site ainda não foi configurado no GAT Server. Atualize para 1.0.11 e configure em CONTA / SENHA.','error');
    else if(results.some(x=>x.reason==='update'))setLoginStatus('O GAT Server precisa da versão 1.0.11 para liberar o login do site.','error');
    else setLoginStatus('Não foi possível conectar aos GAT Servers.','error');
  }finally{button.disabled=false;button.textContent='ENTRAR';}
}

document.getElementById('loginForm').addEventListener('submit',submitLogin);
document.getElementById('logoutButton').addEventListener('click',()=>{clearTokens();lockSite('Você saiu do GAT LOG.');});

function escText(v,fallback='—'){const s=String(v??'').trim();return s||fallback;}
function num(v){const n=Number(v);return Number.isFinite(n)?n:0;}
function ageMs(iso){const t=Date.parse(iso||'');return Number.isFinite(t)?Date.now()-t:Infinity;}
function isFresh(t){return ageMs(t.updated_at)<=FRESH_MS;}
function normalize(s){return String(s||'').trim().toLocaleLowerCase('pt-BR');}
function formatWeight(kg){const n=num(kg);return n>=1000?(n/1000).toLocaleString('pt-BR',{maximumFractionDigits:1})+' t':Math.round(n)+' kg';}
function formatKm(v){return Math.max(0,Math.round(num(v))).toLocaleString('pt-BR')+' km';}
function formatSpeed(v){return Math.max(0,Math.round(num(v)))+' km/h';}
function el(tag,cls,text){const x=document.createElement(tag);if(cls)x.className=cls;if(text!==undefined)x.textContent=text;return x;}

async function fetchServer(server){
  if(!tokenFor(server))return {server,data:null,error:'auth'};
  const controller=new AbortController();
  const timer=setTimeout(()=>controller.abort(),4500);
  try{
    const r=await fetch(server.url+LIVE_PATH,{cache:'no-store',signal:controller.signal,headers:authHeaders(server)});
    if(r.status===401){delete siteTokens[server.url];saveTokens();return {server,data:null,error:'auth'};}
    if(!r.ok)throw new Error('HTTP '+r.status);
    const data=await r.json();
    if(!data||data.ok!==true)throw new Error('resposta inválida');
    return {server,data,error:null};
  }catch(e){return {server,data:null,error:e?.message||'offline'};}
  finally{clearTimeout(timer);}
}

function renderServerCards(results){
  const root=document.getElementById('serverCards');root.textContent='';
  results.forEach(({server,data,error})=>{
    const card=el('article','server-live-card '+(data?.online?'online':'offline'));
    const top=el('div','server-live-top');
    const names=el('div');names.append(el('small','mini','GAT SERVER'),el('h3','',data?.server_name||server.label));
    top.append(names,el('span','pill '+(data?.online?'cyan':'red'),data?.online?'ONLINE':'OFFLINE'));
    card.append(top);
    const meta=el('div','server-live-meta');
    if(data){
      meta.append(metric('Jogadores',(data.player_count??data.players?.length??0)+' / '+(data.max_players??'—')),metric('ID da sala',escText(data.session_id)),metric('Telemetrias',String((data.telemetry||[]).filter(isFresh).length)));
    }else{
      const msg=el('div','server-error');
      msg.textContent=error==='auth'?'Login não validado neste servidor':error?.includes('404')?'GAT Server 1.0.11 necessário':'Servidor/API indisponível';
      card.append(msg);
    }
    card.append(meta);root.append(card);
  });
}
function metric(name,value){const d=el('div');d.append(el('small','',name),el('b','',value));return d;}

function buildDrivers(results){
  const out=[];
  results.forEach(({server,data})=>{
    if(!data)return;
    const players=Array.isArray(data.players)?data.players:[];
    const telemetry=Array.isArray(data.telemetry)?data.telemetry:[];
    const byName=new Map(telemetry.map(t=>[normalize(t.driver),t]));
    players.forEach(p=>out.push({server:server.label,endpoint:server.url,driver:p,t:byName.get(normalize(p))||null,online:true}));
    telemetry.filter(t=>isFresh(t)&&!players.some(p=>normalize(p)===normalize(t.driver))).forEach(t=>out.push({server:server.label,endpoint:server.url,driver:t.driver,t,online:true}));
  });
  const seen=new Set();
  return out.filter(d=>{const k=d.endpoint+'|'+normalize(d.driver);if(seen.has(k))return false;seen.add(k);return true;});
}

function renderDrivers(drivers){
  const root=document.getElementById('driverGrid');root.textContent='';
  document.getElementById('driverCountBadge').textContent=drivers.length+' MOTORISTA'+(drivers.length===1?'':'S');
  if(!drivers.length){root.append(el('article','live-placeholder','Nenhum motorista online neste momento.'));return;}
  drivers.forEach(d=>{
    const t=d.t,fresh=t&&isFresh(t),onJob=fresh&&Boolean(t.on_job);
    const card=el('article','driver-card '+(onJob?'route':'waiting'));
    const head=el('div','driver-head');
    const ident=el('div');ident.append(el('small','mini',d.server),el('h3','',escText(d.driver,'Motorista')));
    head.append(ident,el('span','pill '+(onJob?'cyan':fresh?'blue':'amber'),onJob?'EM ROTA':fresh?'ONLINE':'SEM TELEMETRIA'));
    card.append(head);
    if(fresh){
      const route=el('div','driver-route');route.append(el('strong','',escText(t.source,'Origem')),el('span','','→'),el('strong','',escText(t.destination,'Destino')));card.append(route);
      const grid=el('div','driver-metrics');
      grid.append(metric('Carga',escText(t.cargo,'Sem carga')),metric('Peso',formatWeight(t.cargo_mass_kg)),metric('Restante',formatKm(t.remaining_km)),metric('Velocidade',formatSpeed(t.speed_kmh)));
      card.append(grid);
      const foot=el('div','driver-foot');foot.textContent='Atualizado há '+Math.max(0,Math.round(ageMs(t.updated_at)/1000))+' s';card.append(foot);
    }else{
      const wait=el('div','driver-wait');wait.textContent='Jogador está na sessão, mas o site ainda não recebeu telemetria recente deste motorista.';card.append(wait);
    }
    root.append(card);
  });
}

function renderHero(){
  const driver=document.getElementById('heroDriver'),state=document.getElementById('heroState');
  if(!heroTrips.length){driver.textContent='Aguardando uma viagem em andamento';document.getElementById('heroSource').textContent='Origem';document.getElementById('heroDestination').textContent='Destino';document.getElementById('heroSpeed').textContent='— km/h';document.getElementById('heroCargo').textContent='—';state.textContent='AO VIVO';return;}
  const d=heroTrips[heroIndex%heroTrips.length];heroIndex++;
  driver.textContent=d.driver+' • '+d.server;
  document.getElementById('heroSource').textContent=escText(d.t.source,'Origem');
  document.getElementById('heroDestination').textContent=escText(d.t.destination,'Destino');
  document.getElementById('heroSpeed').textContent=formatSpeed(d.t.speed_kmh);
  document.getElementById('heroCargo').textContent=escText(d.t.cargo,'Sem carga');
  state.textContent='EM ROTA';
}

async function refreshLive(){
  if(document.body.classList.contains('site-locked'))return;
  const results=await Promise.all(GAT_SERVERS.map(fetchServer));
  if(!GAT_SERVERS.some(s=>tokenFor(s))){clearTokens();lockSite('Sua sessão expirou. Entre novamente.');return;}
  renderServerCards(results);
  const drivers=buildDrivers(results);renderDrivers(drivers);
  const responding=results.filter(x=>x.data),online=results.filter(x=>x.data?.online);
  const playerCount=results.reduce((n,x)=>n+(x.data?.player_count??(Array.isArray(x.data?.players)?x.data.players.length:0)),0);
  heroTrips=drivers.filter(d=>d.t&&isFresh(d.t)&&d.t.on_job);
  document.getElementById('statServers').textContent=online.length+' online';
  document.getElementById('statServersSub').textContent=responding.length+' de '+GAT_SERVERS.length+' respondendo';
  document.getElementById('statDrivers').textContent=playerCount+' online';
  document.getElementById('statRoutes').textContent=heroTrips.length+' viagem'+(heroTrips.length===1?'':'s');
  const live=document.getElementById('globalLive');live.innerHTML='<i></i> '+(responding.length?'PAINEL AO VIVO':'SEM CONEXÃO');
  document.getElementById('lastUpdate').textContent='ATUALIZADO '+new Date().toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit',second:'2-digit'});
  renderHero();
}

function startLive(){
  refreshLive();
  if(!liveTimer)liveTimer=setInterval(refreshLive,REFRESH_MS);
  if(!heroTimer)heroTimer=setInterval(renderHero,7000);
}

restoreSession();
