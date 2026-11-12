(()=>{'use strict';
 const API='https://api.bidulog.com.br',PRODUCT='profile_customization';
 const names=['Horizonte Azul','Rota Violeta','Rota Esmeralda','Estrada de Cobre','Safira da Estrada','Aurora Boreal','Inverno na Estrada','Asfalto e Prata','Rota da Primavera','Chamas da Estrada','Elite da Estrada','Luzes da Estrada'];
 let current=null,dialog=null,draft=null,state=null,saving=false,editorUser='';
 const session=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch{return null}};
 const clean=v=>String(v||'').trim().toLowerCase();
 const mine=()=>!!current?.user&&clean(session()?.user)===clean(current.user);
 const defaults=()=>({theme:'auto',cover:'',position:50,darkness:65,background:'',background_position:50,background_darkness:70,background_fixed:true,bio:''});
 function background(c,host=document.body){
  host.querySelector(':scope > .gat-page-background')?.remove();
  if(host===document.body)host.classList.remove('gat-has-profile-background');
  if(!c.background||!/^data:image\/(webp|png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(c.background))return;
  const layer=document.createElement('div');layer.className='gat-page-background'+(c.background_fixed?' is-fixed':'');layer.setAttribute('aria-hidden','true');
  const img=document.createElement('img');img.alt='';img.src=c.background;img.style.objectPosition='50% '+Number(c.background_position??50)+'%';
  const shade=document.createElement('div');shade.style.opacity=String(Math.max(.3,Math.min(.9,Number(c.background_darkness)/100||.7)));layer.append(img,shade);host.prepend(layer);
  if(host===document.body)host.classList.add('gat-has-profile-background');
 }
 function biography(c,host,previewOnly=false){
  if(!previewOnly)document.getElementById('gat-driver-bio')?.remove();
  if(!c.bio)return;
  const section=document.createElement('section');section.className='gat-driver-bio';if(!previewOnly)section.id='gat-driver-bio';section.setAttribute('aria-label','Sobre o motorista');
  const label=document.createElement('h2');label.textContent='SOBRE O MOTORISTA';const text=document.createElement('p');text.textContent=Array.from(String(c.bio)).slice(0,600).join('');section.append(label,text);
  if(previewOnly)host.appendChild(section);else host.insertAdjacentElement('afterend',section);
 }
 function paint(hero,c){
  hero.querySelector('.gat-profile-cover')?.remove();hero.removeAttribute('data-selected-theme');
  const latest=(current?.pass_tier?.seasons||[]).map(s=>s.season_id).filter(s=>/^\d{4}-(0[1-9]|1[0-2])$/.test(s)).sort().at(-1),month=latest?.slice(-2);
  if(month){hero.setAttribute('data-pass-theme',month);const label=hero.querySelector('[data-pass-theme-name]');if(label)label.textContent=names[Number(month)-1].toLocaleUpperCase('pt-BR')+' · '+latest;}
  if(c.theme==='default'){hero.setAttribute('data-selected-theme','default');hero.removeAttribute('data-pass-theme')}
  else if(/^(0[1-9]|1[0-2])$/.test(c.theme)){
   hero.setAttribute('data-selected-theme',c.theme);hero.setAttribute('data-pass-theme',c.theme);
   const label=hero.querySelector('[data-pass-theme-name]');if(label)label.textContent=names[Number(c.theme)-1].toLocaleUpperCase('pt-BR');
  }
  if(c.cover&&/^data:image\/(webp|png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(c.cover)){
   const layer=document.createElement('div');layer.className='gat-profile-cover';layer.setAttribute('aria-hidden','true');
   const image=document.createElement('img');image.alt='';image.src=c.cover;image.style.objectPosition='50% '+Math.max(0,Math.min(100,Number(c.position)||0))+'%';
   const shade=document.createElement('div');shade.style.opacity=String(Math.max(.3,Math.min(.9,Number(c.darkness)/100||.65)));layer.append(image,shade);hero.prepend(layer);
  }
 }
 function update(profile){
  current=profile;const hero=document.querySelector('.driver-hero');if(!hero)return;
  // pass-tier.js has already rendered the earned theme and level aura.
  const custom={...defaults(),...profile?.profile_customization};paint(hero,custom);background(custom);biography(custom,hero);
  let button=document.getElementById('gat-customize-profile');
  if(!button){button=document.createElement('button');button.id='gat-customize-profile';button.type='button';button.className='profile-edit-btn';button.textContent='PERSONALIZAR PERFIL';button.addEventListener('click',open);hero.querySelector('.driver-badges')?.appendChild(button)}
  button.hidden=!mine();
  let inside=document.getElementById('gat-customize-from-edit');const panel=document.querySelector('#profileEditModal .profile-edit-panel');
  if(panel&&!inside){inside=document.createElement('button');inside.id='gat-customize-from-edit';inside.type='button';inside.className='profile-edit-btn';inside.textContent='PERSONALIZAR TEMA E CAPA';inside.addEventListener('click',()=>{document.getElementById('profileEditClose')?.click();open()});panel.querySelector('.profile-edit-head')?.after(inside)}
  if(inside)inside.hidden=!mine();
 }
 async function post(action,body={}){
  const token=session()?.token;if(!token)throw Error('Entre novamente na sua conta.');
  const response=await fetch(API+'/api/profile-customization/'+action,{method:'POST',cache:'no-store',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({...body,token}),signal:AbortSignal.timeout(20000)});
  const data=await response.json().catch(()=>({}));
  if(!response.ok||!data.ok){const errors={theme_not_unlocked:'Conclua o passe desse tema para desbloqueá-lo.',customization_not_owned:'Adquira a personalização para usar capa, fundo e mensagem.',bio_too_long:'A mensagem deve ter no máximo 600 caracteres.',invalid_session:'Sua sessão expirou. Entre novamente.',cover_too_large:'A imagem ficou grande demais. Escolha outra foto.',invalid_cover:'A foto não foi aceita. Use JPG, PNG ou WebP.',invalid_cover_settings:'Confira a posição e o escurecimento.'};throw Error(errors[data.error]||'Personalização indisponível agora. Tente novamente mais tarde.')}
  return data;
 }
 const node=(tag,cls,text)=>{const e=document.createElement(tag);if(cls)e.className=cls;if(text!==undefined)e.textContent=text;return e};
 function button(text,fn,cls='pc-button'){const b=node('button',cls,text);b.type='button';b.addEventListener('click',fn);return b}
 function close(){if(saving)return;dialog?.close();dialog?.remove();dialog=null;draft=null;editorUser='';}
 function message(text){const target=dialog?.querySelector('.pc-status');if(target)target.textContent=text;}
 function preview(){
  const host=dialog?.querySelector('.pc-preview');if(!host)return;host.textContent='';
  const source=document.querySelector('.driver-hero');if(!source)return;const copy=source.cloneNode(true);
  copy.querySelectorAll('[id]').forEach(el=>el.removeAttribute('id'));copy.querySelectorAll('button').forEach(el=>el.remove());
  copy.classList.add('pc-preview-hero');paint(copy,draft);host.appendChild(copy);biography(draft,host,true);background(draft,dialog.querySelector('.pc-preview-area'));
 }
 function themes(){
  const box=dialog.querySelector('.pc-themes');box.textContent='';
  const options=[['auto','Último tema conquistado'],['default','Padrão'],...names.map((name,i)=>[String(i+1).padStart(2,'0'),name])];
  for(const[id,label]of options){const unlocked=['auto','default'].includes(id)||state.themes.includes(id);const b=button(label,()=>{draft.theme=id;themes();preview()},'pc-theme');b.disabled=!unlocked;b.setAttribute('aria-pressed',String(draft.theme===id));b.title=unlocked?label:'Conclua o passe do mês '+id+' para desbloquear';if(!unlocked)b.appendChild(node('small','','Bloqueado · passe '+id));else if(draft.theme===id)b.appendChild(node('small','','Selecionado'));box.appendChild(b)}
 }
 function range(label,key,min,max){const wrap=node('label','pc-range',label);const value=node('span','',draft[key]+'%'),input=node('input');input.type='range';input.min=min;input.max=max;input.value=draft[key];input.disabled=!state.owned;input.addEventListener('input',()=>{draft[key]=Number(input.value);value.textContent=input.value+'%';preview()});wrap.append(value,input);return wrap;}
 async function prepare(file){
  if(!file||!['image/jpeg','image/png','image/webp'].includes(file.type))throw Error('Escolha uma foto JPG, PNG ou WebP.');if(file.size>10*1024*1024)throw Error('Escolha uma foto de até 10 MB.');
  const bitmap=await createImageBitmap(file);try{
   if(bitmap.width*bitmap.height>40000000)throw Error('A imagem tem resolução muito alta. Escolha outra foto.');
   const scale=Math.min(1,1600/bitmap.width,900/bitmap.height),canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(bitmap.width*scale));canvas.height=Math.max(1,Math.round(bitmap.height*scale));canvas.getContext('2d').drawImage(bitmap,0,0,canvas.width,canvas.height);
   for(const quality of [.85,.75,.65,.5,.35]){const result=canvas.toDataURL('image/webp',quality);if(result.length<=220000)return result}throw Error('Não foi possível reduzir a foto. Escolha outra imagem.');
  }finally{bitmap.close()}
 }
 async function save(){
  if(saving||!mine()||clean(editorUser)!==clean(session()?.user))return;
  saving=true;const submit=dialog.querySelector('.pc-save');submit.disabled=true;message('Salvando personalização…');
  try{const user=editorUser,result=await post('save',draft);if(clean(user)!==clean(session()?.user))return;
   if(window.GATCurrentDriverProfile?.user===user){window.GATCurrentDriverProfile.profile_customization=result.customization;window.dispatchEvent(new CustomEvent('gat-driver-profile-updated',{detail:{profile:window.GATCurrentDriverProfile,source:'customization'}}))}
   saving=false;close();
  }catch(error){message(error.message)}finally{saving=false;if(submit.isConnected)submit.disabled=false}
 }
 async function open(){
  if(!mine()||dialog)return;editorUser=current.user;
  dialog=document.createElement('dialog');dialog.className='pc-dialog';dialog.setAttribute('aria-labelledby','pc-title');
  const head=node('div','pc-head');const title=node('h2','','Personalizar perfil');title.id='pc-title';head.append(title,button('Fechar',close));
  dialog.append(head,node('p','pc-status','Carregando suas opções…'));document.body.appendChild(dialog);dialog.addEventListener('cancel',e=>{e.preventDefault();close()});dialog.showModal();
  try{state=await post('state');if(!dialog||clean(editorUser)!==clean(session()?.user))return;draft={...defaults(),...state.customization};
   const layout=node('div','pc-layout'),controls=node('div','pc-controls'),previewArea=node('div','pc-preview-area');
   controls.append(node('h3','','Tema do perfil'),node('p','pc-note','Padrão gratuito e temas conquistados nos passes.'),node('div','pc-themes'));
   controls.append(node('h3','','Foto de capa'));
   if(!state.owned){const offer=node('div','pc-offer');offer.append(node('strong','','R$ 10,00 ou 20.000 Gold'),node('p','','Compra única: capa, fundo da página e mensagem. Sem cobrança por troca.'));const link=node('a','pc-button','LIBERAR PERSONALIZAÇÃO');link.href='economia.html#loja';offer.append(link);controls.append(offer)}
   const file=node('input');file.type='file';file.accept='image/jpeg,image/png,image/webp';file.disabled=!state.owned;file.setAttribute('aria-label','Enviar foto de capa');
   file.addEventListener('change',async()=>{if(!file.files?.[0])return;const user=editorUser;try{message('Preparando foto…');const data=await prepare(file.files[0]);if(!dialog||editorUser!==user)return;draft.cover=data;preview();message('Foto pronta. Confira a prévia e salve.')}catch(e){message(e.message)}finally{file.value=''}});
   const remove=button('Remover foto',()=>{draft.cover='';preview();message('Foto removida da prévia. Salve para confirmar.');});remove.disabled=!state.owned&&!draft.cover;
   controls.append(file,remove,range('Posição vertical','position',0,100),range('Escurecer a foto','darkness',30,90),node('p','pc-note','A aura continua seguindo seu nível. Editar sua foto de motorista permanece gratuito.'));
   if(state.version>=2){
    controls.append(node('h3','','Fundo da página'),node('p','pc-note','Imagem nas laterais do perfil. Incluído na personalização já adquirida.'));
    const bgFile=node('input');bgFile.type='file';bgFile.accept='image/jpeg,image/png,image/webp';bgFile.disabled=!state.owned;bgFile.setAttribute('aria-label','Enviar imagem de fundo');
    bgFile.addEventListener('change',async()=>{if(!bgFile.files?.[0])return;const user=editorUser;try{message('Preparando fundo…');const data=await prepare(bgFile.files[0]);if(!dialog||editorUser!==user)return;draft.background=data;preview();message('Fundo pronto. Salve para confirmar.')}catch(e){message(e.message)}finally{bgFile.value=''}});
    const bgRemove=button('Restaurar fundo padrão',()=>{draft.background='';preview();});bgRemove.disabled=!state.owned;
    const fixed=node('label','pc-fixed'),check=node('input');check.type='checkbox';check.checked=draft.background_fixed;check.disabled=!state.owned;check.addEventListener('change',()=>{draft.background_fixed=check.checked;preview()});fixed.append(check,document.createTextNode(' Fundo fixo ao rolar a página'));
    controls.append(bgFile,bgRemove,range('Posição do fundo','background_position',0,100),range('Escurecer fundo','background_darkness',30,90),fixed,node('h3','','Sobre o motorista'));
    const bio=node('textarea','pc-bio');bio.rows=5;bio.value=draft.bio;bio.disabled=!state.owned;bio.setAttribute('aria-label','Mensagem sobre o motorista');bio.placeholder='Escreva sua mensagem para os visitantes…';
    const counter=node('span','pc-bio-count',Array.from(draft.bio).length+'/600 caracteres');counter.setAttribute('aria-live','polite');bio.addEventListener('input',()=>{draft.bio=Array.from(bio.value).slice(0,600).join('');if(bio.value!==draft.bio)bio.value=draft.bio;counter.textContent=Array.from(draft.bio).length+'/600 caracteres';preview()});
    controls.append(bio,counter,node('p','pc-note','Visível para quem visitar seu perfil. Até 600 caracteres, incluindo espaços e emojis.'));
   }else controls.append(node('p','pc-note','As opções de fundo e mensagem estarão disponíveis após a atualização da Central.'));
   previewArea.append(node('h3','','Prévia do seu perfil'),node('div','pc-preview'),node('p','pc-note','Sua escolha será vista por quem visitar seu perfil.'));
   layout.append(controls,previewArea);const actions=node('div','pc-actions');actions.append(button('Cancelar',close),button('Salvar personalização',save,'pc-button pc-save'));dialog.append(layout,actions);themes();preview();message('');
  }catch(error){message(error.message)}
 }
 window.addEventListener('gat-driver-profile-updated',event=>update(event.detail?.profile));window.addEventListener('gat-account-change',()=>{saving=false;close();current=null;document.getElementById('gat-customize-profile')?.remove();document.getElementById('gat-customize-from-edit')?.remove();document.querySelector('.driver-hero .gat-profile-cover')?.remove();document.getElementById('gat-driver-bio')?.remove();document.body.querySelector(':scope > .gat-page-background')?.remove();document.body.classList.remove('gat-has-profile-background')});
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>update(window.GATCurrentDriverProfile),{once:true});else update(window.GATCurrentDriverProfile);
})();
