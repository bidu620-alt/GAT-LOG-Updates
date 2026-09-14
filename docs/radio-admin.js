const RADIO_API='https://api.gatlogets2.com.br';
const RADIO_SESSION_KEY='gat_driver_account_v1';
const $r=id=>document.getElementById(id);
let radioCurrent={enabled:false,playlist_url:'',playlist_id:'',label:'Rádio GAT',revision:0,updated_by:'',updated_at:''};

function radioSession(){try{return JSON.parse(localStorage.getItem(RADIO_SESSION_KEY)||sessionStorage.getItem(RADIO_SESSION_KEY)||'null')}catch(_){return null}}
function radioRoleLabel(v){return ({owner:'PROPRIETÁRIO',admin:'ADMIN',moderator:'MODERADOR'})[String(v||'').toLowerCase()]||String(v||'').toUpperCase()}
function radioDate(v){const d=new Date(v||'');return Number.isFinite(d.getTime())?d.toLocaleString('pt-BR'):'—'}
function radioStatus(text,kind=''){$r('radioStatus').textContent=text;$r('radioStatus').className='admin-status'+(kind?' '+kind:'')}
async function radioPost(path,extra={}){const s=radioSession(),c=new AbortController(),t=setTimeout(()=>c.abort(),6500);try{const res=await fetch(RADIO_API+path,{method:'POST',cache:'no-store',signal:c.signal,headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:String(s?.token||''),...extra})});return{res,data:await res.json().catch(()=>null)}}finally{clearTimeout(t)}}

function renderRadio(){
  const r=radioCurrent||{};
  $r('radioPlaylist').value=r.playlist_url||'';
  $r('radioLabel').value=r.label||'Rádio GAT';
  $r('radioLivePill').textContent=r.enabled?'AO VIVO':'DESLIGADA';
  $r('radioLivePill').className='radio-pill '+(r.enabled?'on':'off');
  $r('radioState').textContent=r.enabled?'AO VIVO':'DESLIGADA';
  $r('radioRevision').textContent=Number(r.revision||0).toLocaleString('pt-BR');
  $r('radioUpdatedBy').textContent=r.updated_by?'@'+r.updated_by:'—';
  $r('radioUpdatedAt').textContent=radioDate(r.updated_at);
}

async function loadRadio(){
  radioStatus('Atualizando programação...');
  try{
    const res=await fetch(RADIO_API+'/api/public/radio?t='+Date.now(),{cache:'no-store'}),data=await res.json().catch(()=>null);
    if(!res.ok||!data?.ok||!data?.radio){radioStatus('Não foi possível carregar a Rádio GAT (HTTP '+res.status+').','err');return}
    radioCurrent=data.radio;renderRadio();radioStatus('Programação sincronizada com a Central GAT.','ok');
  }catch(_){radioStatus('Central GAT não respondeu.','err')}
}

async function saveRadio(enabled){
  const playlist=$r('radioPlaylist').value.trim(),label=$r('radioLabel').value.trim()||'Rádio GAT';
  if(enabled&& !playlist){radioStatus('Cole uma playlist do YouTube antes de ligar a rádio.','err');return}
  radioStatus(enabled?'Salvando e ligando a Rádio GAT...':'Salvando programação...');
  try{
    const out=await radioPost('/api/site/admin/radio',{playlist_url:playlist,label,enabled});
    if(out.res.status===401){radioStatus('Sua sessão expirou. Entre novamente no Painel Admin.','err');return}
    if(out.res.status===403){radioStatus('Sua conta não possui permissão de Admin/Moderador.','err');return}
    if(!out.res.ok||!out.data?.ok){
      const code=out.data?.error||('HTTP '+out.res.status);
      const msg=code==='invalid_youtube_playlist'?'Use um link válido de playlist do YouTube.':code==='playlist_required'?'Defina uma playlist antes de ligar a rádio.':'Alteração recusada: '+code+'.';
      radioStatus(msg,'err');return;
    }
    radioCurrent=out.data.radio||radioCurrent;renderRadio();radioStatus(enabled?'Rádio GAT ligada para os motoristas.':'Programação salva.','ok');
  }catch(_){radioStatus('Falha de comunicação ao salvar a rádio.','err')}
}

async function turnRadioOff(){
  radioStatus('Desligando Rádio GAT...');
  try{
    const out=await radioPost('/api/site/admin/radio',{playlist_url:$r('radioPlaylist').value.trim(),label:$r('radioLabel').value.trim()||'Rádio GAT',enabled:false});
    if(!out.res.ok||!out.data?.ok){radioStatus('Não foi possível desligar: '+(out.data?.error||'HTTP '+out.res.status)+'.','err');return}
    radioCurrent=out.data.radio||{...radioCurrent,enabled:false};renderRadio();radioStatus('Rádio GAT desligada. Os clientes pausam na próxima sincronização.','ok');
  }catch(_){radioStatus('Central GAT não respondeu.','err')}
}

async function initRadioAdmin(){
  const s=radioSession();
  if(!s?.token){$r('radioGateText').textContent='Entre no Painel Admin com sua Conta GAT antes de controlar a rádio.';$r('radioLoginLink').hidden=false;return}
  try{
    const out=await radioPost('/api/site/admin/session');
    if(out.res.status===401){$r('radioGateText').textContent='Sua sessão expirou. Entre novamente.';$r('radioLoginLink').hidden=false;return}
    if(out.res.status===403){$r('radioGateText').textContent='Esta Conta GAT não possui permissão de Admin ou Moderador.';return}
    if(!out.res.ok||!out.data?.ok){$r('radioGateText').textContent='Não foi possível validar sua permissão agora.';return}
    $r('radioGate').hidden=true;$r('radioContent').hidden=false;$r('radioUser').textContent='@'+out.data.user;$r('radioRole').textContent=radioRoleLabel(out.data.role);await loadRadio();
  }catch(_){$r('radioGateText').textContent='Central GAT indisponível no momento.'}
}

$r('radioSave').addEventListener('click',()=>saveRadio(radioCurrent.enabled));
$r('radioOn').addEventListener('click',()=>saveRadio(true));
$r('radioOff').addEventListener('click',turnRadioOff);
initRadioAdmin();
setInterval(()=>{if(!$r('radioContent').hidden)loadRadio()},30000);
