(()=>{'use strict';
 const BASE='https://streamers.bidulog.com.br',SESSION='gat_driver_account_v1';
 const root=document.getElementById('streamerAdminPanel'),form=document.getElementById('streamerAdminForm');
 if(!root||!form)return;
 const user=document.getElementById('streamerAdminUser'),url=document.getElementById('streamerAdminUrl');
 const msg=document.getElementById('streamerAdminStatus'),list=document.getElementById('streamerAdminList');
 let entries=[],busy=false;
 function owner(){return typeof viewerRole!=='undefined'&&viewerRole==='owner'}
 function session(){try{return JSON.parse(localStorage.getItem(SESSION)||sessionStorage.getItem(SESSION)||'null')}catch{return null}}
 function status(message,error=false){msg.textContent=message;msg.classList.toggle('err',error)}
 async function request(path,body){
  const init={cache:'no-store',signal:AbortSignal.timeout(12000)};
  if(body){init.method='POST';init.headers={'Content-Type':'text/plain;charset=UTF-8'};init.body=JSON.stringify(body)}
  const response=await fetch(BASE+path,init),data=await response.json().catch(()=>({}));
  if(!response.ok||!data.ok)throw new Error(data.error||'HTTP '+response.status);
  return data;
 }
 function errorText(error){
  const code=String(error.message||error);
  const map={streamer_role_required:'Primeiro altere o cargo deste motorista para Streamer e aguarde a Central confirmar.',invalid_channel_url:'Use o link permanente do canal: youtube.com/@Canal ou twitch.tv/Canal. O endereço de um vídeo não serve.',owner_required:'Somente o proprietário pode cadastrar canais.',session_unavailable:'A Central está indisponível para validar seu acesso.',driver_unavailable:'Não foi possível verificar o motorista na Central.',invalid_session:'Entre novamente na sua Conta BiduLog.',origin_denied:'Acesse o painel pelo domínio oficial bidulog.com.br.'};
  return map[code]||'Falha ao salvar o canal: '+code;
 }
 async function load(){
  try{const data=await request('/api/channels?t='+Date.now());entries=Array.isArray(data.channels)?data.channels:[];render();if(!busy)status(entries.length+' canal(is) cadastrados.')}
  catch(error){if(!busy)status('Não foi possível consultar os canais: '+error.message,true)}
 }
 function render(){
  list.replaceChildren();
  if(!entries.length){const p=document.createElement('p');p.textContent='Nenhum canal adicional registrado. Os canais antigos continuam funcionando.';list.append(p);return}
  for(const entry of entries){
   const row=document.createElement('div');row.className='streamer-admin-entry';
   const who=document.createElement('strong');who.textContent='@'+entry.user;
   const link=document.createElement('a');link.href=entry.url;link.target='_blank';link.rel='noopener noreferrer';link.textContent=entry.platform==='youtube'?'YouTube':'Twitch';
   const edit=document.createElement('button');edit.type='button';edit.textContent='EDITAR';edit.addEventListener('click',()=>{user.value=entry.user;url.value=entry.url;user.focus()});
   row.append(who,link,edit);
   if(owner()){
    const remove=document.createElement('button');remove.type='button';remove.textContent='REMOVER';remove.addEventListener('click',async()=>{
     if(busy||!confirm('Remover o canal de @'+entry.user+' da lista automática?'))return;
     await save(entry.user,'');
    });row.append(remove);
   }
   list.append(row);
  }
 }
 async function save(target,channel){
  if(busy)return;
  const token=session()?.token;
  if(!owner()||!token){status('Entre com a conta do proprietário para alterar canais.',true);return}
  busy=true;form.querySelector('button[type=submit]').disabled=true;status('Validando cargo e salvando canal...');
  try{await request('/api/channel',{token,user:target,url:channel});status(channel?'Canal de @'+target+' salvo! As verificações da live são automáticas.':'Canal removido.');await load();if(channel){user.value=target;url.value=channel}}
  catch(error){status(errorText(error),true)}
  finally{busy=false;form.querySelector('button[type=submit]').disabled=false}
 }
 form.addEventListener('submit',event=>{
  event.preventDefault();
  const target=user.value.trim().replace(/^@/,'').toLowerCase();
  const channel=url.value.trim();
  if(!/^[a-z0-9_-]{1,40}$/i.test(target)){status('Informe o usuário BiduLog válido.',true);return}
  save(target,channel);
 });
 // Shortcut next to the existing role selection on each driver row.
 function addShortcuts(){
  if(!owner())return;
  const rows=document.querySelectorAll('#adminRows select[data-role-user]');
  for(const select of rows){
   const host=select.closest('td')||select.parentElement;
   if(!host||host.querySelector('[data-streamer-shortcut]'))continue;
   const b=document.createElement('button');b.type='button';b.className='streamer-admin-shortcut';b.dataset.streamerShortcut=select.dataset.roleUser;b.textContent='🎥 CANAL';
   b.addEventListener('click',()=>{
    document.querySelector('.admin-tabs [data-tab="streamers"]')?.click();
    user.value=select.dataset.roleUser;
    const saved=entries.find(c=>c.user===user.value);url.value=saved?.url||'';
    url.focus();
   });host.append(b);
  }
 }
 const rows=document.getElementById('adminRows');
 if(rows)new MutationObserver(addShortcuts).observe(rows,{childList:true,subtree:true});
 document.querySelector('.admin-tabs [data-tab="streamers"]')?.addEventListener('click',load);
 window.addEventListener('gat-account-change',()=>{setTimeout(()=>{addShortcuts();render()},250)});
 setTimeout(()=>{load();addShortcuts()},300);
})();