const RAW='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/';
const GAT_SERVERS=[
  {label:'BIDUZAO - DOUGLAS',url:'https://douglas.tail4577e8.ts.net'},
  {label:'JC - JEAN',url:'https://jean-jc.tailf14a00.ts.net'}
];
const ACCOUNT_AUTHORITY='https://douglas.tail4577e8.ts.net';
const LIVE_PATH='/api/public/live';
const ACCOUNT_LOGIN='/api/account/login';
const ACCOUNT_REGISTER='/api/account/register';
const ACCOUNT_SESSION='/api/account/session';
const ACCOUNT_TOKEN_KEY='gat_driver_account_v1';
const REFRESH_MS=5000;
const FRESH_MS=20000;
let heroTrips=[];
let heroIndex=0;
let accountSession=loadAccountSession();

async function bindRelease(manifest,versionId,buttonId){
  const versionEl=document.getElementById(versionId),button=document.getElementById(buttonId);
  try{
    const r=await fetch(RAW+manifest,{cache:'no-store'});
    if(!r.ok)throw new Error('manifest');
    const data=await r.json();
    versionEl.textContent='Versão '+(data.display_version||data.version||'atual');
    const url=data.setup_url||data.url||'';
    if(url){button.href=url;button.classList.remove('disabled');}
  }catch(_){versionEl.textContent='Versão disponível no GitHub';}
}
bindRelease('server_dotnet_version.json','serverVersion','serverDownload');
bindRelease('client_dotnet_version.json','clientVersion','clientDownload');

function loadAccountSession(){
  try{const x=JSON.parse(sessionStorage.getItem(ACCOUNT_TOKEN_KEY)||'null');return x&&x.user&&x.token?x:null;}catch(_){return null;}
}
function saveAccountSession(user,token){accountSession={user,token};try{sessionStorage.setItem(ACCOUNT_TOKEN_KEY,JSON.stringify(accountSession));}catch(_){}updateAccountButton();}
function clearAccountSession(){accountSession=null;try{sessionStorage.removeItem(ACCOUNT_TOKEN_KEY);}catch(_){}updateAccountButton();}
function setLoginStatus(text,type=''){const e=document.getElementById('loginStatus');e.textContent=text;e.className='login-status'+(type?' '+type:'');}
function updateAccountButton(){
  const b=document.getElementById('accountButton');
  b.textContent=accountSession?.user?'@'+accountSession.user:'ENTRAR / CADASTRAR';
  document.getElementById('heroAccountButton').textContent=accountSession?.user?'Conta GAT: @'+accountSession.user:'Criar Conta GAT';
}
function showAccountModal(tab='login'){
  const m=document.getElementById('accountModal');m.classList.add('open');m.setAttribute('aria-hidden','false');
  showAccountTab(tab);
}
function hideAccountModal(){const m=document.getElementById('accountModal');m.classList.remove('open');m.setAttribute('aria-hidden','true');}
function showAccountTab(tab){
  const login=tab!=='register';
  document.getElementById('loginForm').classList.toggle('hidden',!login);
  document.getElementById('registerForm').classList.toggle('hidden',login);
  document.getElementById('tabLogin').classList.toggle('active',login);
  document.getElementById('tabRegister').classList.toggle('active',!login);
  document.getElementById('accountTitle').textContent=login?'Entrar na Conta GAT':'Criar Conta GAT';
  document.getElementById('accountIntro').textContent=login?'Use a mesma conta no site e no GAT Telemetria.':'Crie seu usuário uma vez. Depois use este mesmo login no GAT Telemetria.';
  setLoginStatus(accountSession?.user?'Conta atual: @'+accountSession.user:'Cadastre uma conta ou entre com uma conta existente.',accountSession?.user?'ok':'');
}

async function accountRequest(path,body){
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),8000);
  try{
    const r=await fetch(ACCOUNT_AUTHORITY+path,{method:'POST',cache:'no-store',signal:controller.signal,headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
    let data=null;try{data=await r.json();}catch(_){}
    return {status:r.status,ok:r.ok,data};
  }catch(e){return {status:0,ok:false,data:null,error:e};}finally{clearTimeout(timer);}
}

async function submitLogin(event){
  event.preventDefault();
  const user=document.getElementById('loginUser').value.trim();
  const password=document.getElementById('loginPassword').value;
  const button=document.getElementById('loginButton');
  if(!user||!password){setLoginStatus('Preencha usuário e senha.','error');return;}
  button.disabled=true;button.textContent='ENTRANDO...';setLoginStatus('Validando Conta GAT...');
  try{
    const r=await accountRequest(ACCOUNT_LOGIN,{user,password});
    if(r.ok&&r.data?.ok&&r.data?.token){
      saveAccountSession(r.data.user||user,r.data.token);
      document.getElementById('loginPassword').value='';
      setLoginStatus('Conta conectada: @'+(r.data.user||user),'ok');
      setTimeout(hideAccountModal,500);
      return;
    }
    if(r.status===401)setLoginStatus('Usuário ou senha incorretos.','error');
    else if(r.status===404)setLoginStatus('O servidor de contas ainda precisa da versão 1.0.12.','error');
    else setLoginStatus('Não foi possível entrar na Conta GAT agora.','error');
  }finally{button.disabled=false;button.textContent='ENTRAR';}
}

async function submitRegister(event){
  event.preventDefault();
  const user=document.getElementById('registerUser').value.trim();
  const password=document.getElementById('registerPassword').value;
  const confirm=document.getElementById('registerConfirm').value;
  const button=document.getElementById('registerButton');
  if(user.length<3){setLoginStatus('O usuário precisa ter pelo menos 3 caracteres.','error');return;}
  if(password.length<6){setLoginStatus('A senha precisa ter pelo menos 6 caracteres.','error');return;}
  if(password!==confirm){setLoginStatus('As duas senhas não conferem.','error');return;}
  button.disabled=true;button.textContent='CADASTRANDO...';setLoginStatus('Criando Conta GAT...');
  try{
    const r=await accountRequest(ACCOUNT_REGISTER,{user,password});
    if(r.ok&&r.data?.ok&&r.data?.token){
      saveAccountSession(r.data.user||user,r.data.token);
      document.getElementById('registerPassword').value='';document.getElementById('registerConfirm').value='';
      setLoginStatus('Conta criada com sucesso: @'+(r.data.user||user),'ok');
      setTimeout(hideAccountModal,650);
      return;
    }
    const err=r.data?.error||'';
    if(r.status===409||err==='user_exists')setLoginStatus('Esse usuário já existe. Escolha outro ou entre na conta.','error');
    else if(err==='invalid_user')setLoginStatus('Use letras, números, ponto, _ ou - no usuário.','error');
    else if(err==='weak_password')setLoginStatus('A senha precisa ter pelo menos 6 caracteres.','error');
    else if(r.status===404)setLoginStatus('O servidor de contas ainda precisa da versão 1.0.12.','error');
    else setLoginStatus('Não foi possível criar a conta agora.','error');
  }finally{button.disabled=false;button.textContent='CADASTRAR MOTORISTA';}
}

async function restoreAccount(){
  if(!accountSession?.token){updateAccountButton();return;}
  try{
    const r=await fetch(ACCOUNT_AUTHORITY+ACCOUNT_SESSION,{cache:'no-store',headers:{Authorization:'Bearer '+accountSession.token}});
    const data=await r.json().catch(()=>null);
    if(r.ok&&data?.ok&&data.user){accountSession.user=data.user;try{sessionStorage.setItem(ACCOUNT_TOKEN_KEY,JSON.stringify(accountSession));}catch(_){}updateAccountButton();return;}
  }catch(_){}
  clearAccountSession();
}

document.getElementById('accountButton').addEventListener('click',()=>{
  if(accountSession?.user){
    if(confirm('Conta GAT conectada: @'+accountSession.user+'\n\nDeseja sair desta conta no site?'))clearAccountSession();
    return;
  }
  showAccountModal('login');
});
document.getElementById('heroAccountButton').addEventListener('click',()=>showAccountModal(accountSession?.user?'login':'register'));
document.getElementById('accountClose').addEventListener('click',hideAccountModal);
document.getElementById('accountModal').addEventListener('click',e=>{if(e.target.id==='accountModal')hideAccountModal();});
document.getElementById('tabLogin').addEventListener('click',()=>showAccountTab('login'));
document.getElementById('tabRegister').addEventListener('click',()=>showAccountTab('register'));
document.getElementById('loginForm').addEventListener('submit',submitLogin);
document.getElementById('registerForm').addEventListener('submit',submitRegister);

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
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),4500);
  try{
    const r=await fetch(server.url+LIVE_PATH,{cache:'no-store',signal:controller.signal});
    if(!r.ok)throw new Error('HTTP '+r.status);
    const data=await r.json();
    if(!data||data.ok!==true)throw new Error('resposta inválida');
    return {server,data,error:null};
  }catch(e){return {server,data:null,error:e?.message||'offline'};}finally{clearTimeout(timer);}
}
function metric(name,value){const d=el('div');d.append(el('small','',name),el('b','',value));return d;}

function renderServerCards(results){
  const root=document.getElementById('serverCards');root.textContent='';
  results.forEach(({server,data,error})=>{
    const card=el('article','server-live-card '+(data?.online?'online':'offline'));
    const top=el('div','server-live-top');
    const names=el('div');names.append(el('small','mini','GAT SERVER'),el('h3','',data?.server_name||server.label));
    top.append(names,el('span','pill '+(data?.online?'cyan':'red'),data?.online?'ONLINE':'OFFLINE'));card.append(top);
    const meta=el('div','server-live-meta');
    if(data){meta.append(metric('Jogadores',(data.player_count??data.players?.length??0)+' / '+(data.max_players??'—')),metric('ID da sala',escText(data.session_id)),metric('Telemetrias',String((data.telemetry||[]).filter(isFresh).length)));}
    else{const msg=el('div','server-error');msg.textContent=error?.includes('401')?'Atualize o GAT Server para 1.0.12':error?.includes('404')?'Aguardando GAT Server 1.0.12':'Servidor/API indisponível';card.append(msg);}
    card.append(meta);root.append(card);
  });
}

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
    const ident=el('div');
    const account=fresh&&String(t.account_user||'').trim()?' • @'+String(t.account_user).trim():'';
    ident.append(el('small','mini',d.server+account),el('h3','',escText(d.driver,'Motorista')));
    head.append(ident,el('span','pill '+(onJob?'cyan':fresh?'blue':'amber'),onJob?'EM ROTA':fresh?'ONLINE':'SEM TELEMETRIA'));card.append(head);
    if(fresh){
      const route=el('div','driver-route');route.append(el('strong','',escText(t.source,'Origem')),el('span','','→'),el('strong','',escText(t.destination,'Destino')));card.append(route);
      const grid=el('div','driver-metrics');grid.append(metric('Carga',escText(t.cargo,'Sem carga')),metric('Peso',formatWeight(t.cargo_mass_kg)),metric('Restante',formatKm(t.remaining_km)),metric('Velocidade',formatSpeed(t.speed_kmh)));card.append(grid);
      const foot=el('div','driver-foot');foot.textContent=(t.account_user?'Conta GAT @'+t.account_user+' • ':'')+'Atualizado há '+Math.max(0,Math.round(ageMs(t.updated_at)/1000))+' s';card.append(foot);
    }else{const wait=el('div','driver-wait');wait.textContent='Jogador está na sessão, mas o site ainda não recebeu telemetria recente deste motorista.';card.append(wait);}
    root.append(card);
  });
}

function renderHero(){
  const driver=document.getElementById('heroDriver'),state=document.getElementById('heroState');
  if(!heroTrips.length){driver.textContent='Aguardando uma viagem em andamento';document.getElementById('heroSource').textContent='Origem';document.getElementById('heroDestination').textContent='Destino';document.getElementById('heroSpeed').textContent='— km/h';document.getElementById('heroCargo').textContent='—';state.textContent='AO VIVO';return;}
  const d=heroTrips[heroIndex%heroTrips.length];heroIndex++;
  const account=d.t.account_user?'@'+d.t.account_user+' • ':'';
  driver.textContent=account+d.driver+' • '+d.server;
  document.getElementById('heroSource').textContent=escText(d.t.source,'Origem');document.getElementById('heroDestination').textContent=escText(d.t.destination,'Destino');
  document.getElementById('heroSpeed').textContent=formatSpeed(d.t.speed_kmh);document.getElementById('heroCargo').textContent=escText(d.t.cargo,'Sem carga');state.textContent='EM ROTA';
}

async function refreshLive(){
  const results=await Promise.all(GAT_SERVERS.map(fetchServer));renderServerCards(results);
  const drivers=buildDrivers(results);renderDrivers(drivers);
  const responding=results.filter(x=>x.data),online=results.filter(x=>x.data?.online);
  const playerCount=results.reduce((n,x)=>n+(x.data?.player_count??(Array.isArray(x.data?.players)?x.data.players.length:0)),0);
  heroTrips=drivers.filter(d=>d.t&&isFresh(d.t)&&d.t.on_job);
  document.getElementById('statServers').textContent=online.length+' online';document.getElementById('statServersSub').textContent=responding.length+' de '+GAT_SERVERS.length+' respondendo';
  document.getElementById('statDrivers').textContent=playerCount+' online';document.getElementById('statRoutes').textContent=heroTrips.length+' viagem'+(heroTrips.length===1?'':'s');
  document.getElementById('globalLive').innerHTML='<i></i> '+(responding.length?'PAINEL AO VIVO':'SEM CONEXÃO');
  document.getElementById('lastUpdate').textContent='ATUALIZADO '+new Date().toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit',second:'2-digit'});renderHero();
}

restoreAccount();
updateAccountButton();
refreshLive();
setInterval(refreshLive,REFRESH_MS);
setInterval(renderHero,7000);
