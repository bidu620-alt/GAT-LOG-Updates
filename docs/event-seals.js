(()=>{'use strict';
 const definitions=[
  {id:'halloween_2026',title:'Halloween',file:'halloween',aliases:['Halloween 2026']},
  {id:'children_2026',title:'Dia das Crianças',file:'dia-das-criancas',aliases:['Dia das Crianças 2026']},
  {id:'special_cargo_2026',title:'Rei das Escoltas',file:'rei-das-escoltas',aliases:['O Rei do Special Transport','Carga Especial — Rei das Escoltas']},
  {id:'energia_total_2026',title:'Energia Total',file:'energia-total',aliases:['Operação Energia Total','Operação Energia Total — Refinaria e Combustíveis']},
  {id:'alimentando_cidades_2026',title:'Alimentando as Cidades',file:'alimentando-as-cidades',aliases:['Alimentando as Cidades — Cadeia de Alimentos']},
  {id:'fazenda_bidu_2026',title:'Fazenda do Bidu',file:'fazenda-do-bidu',aliases:['Fazenda do Bidu — Do Campo ao Mundo']},
  {id:'iceland_2026',title:'Desbravador da Islândia',file:'desbravador-da-islandia',aliases:['Islândia — Terra de Fogo e Gelo']}
 ];
 const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]/g,'');
 function collect(profile,registry){
  let computed=[];try{computed=registry?.achievementList(profile)||[]}catch{}
  const saved=Array.isArray(profile?.achievements)?profile.achievements:[];
  const events=definitions.flatMap(def=>{
   const keys=new Set([def.id,'event_'+def.id].map(norm));
   const titles=new Set([def.title,...def.aliases].map(norm));
   const matches=a=>a.kind!=='event_cargo'&&(keys.has(norm(a.id))||keys.has(norm(a.eventId))||titles.has(norm(a.title)));
   const earned=[...saved,...computed].filter(a=>a&&a.unlocked===true&&matches(a));
   if(!earned.length)return [];
   const dated=earned.find(a=>a.completedAt||a.unlocked_at||a.completed_at);
   return [{...def,completedAt:dated?.completedAt||dated?.unlocked_at||dated?.completed_at||null}];
  });
  const tier=Number(profile?.pass_tier?.tier);
  const pass=Number.isSafeInteger(tier)&&tier>0?[{id:'pass_tier',kind:'pass_tier',tier,title:'Tier '+tier,completedAt:profile.pass_tier.latest_completed_at||null,profile}]:[];
  return [...pass,...events];
 }
 const dateLabel=value=>{
  if(value==null||value==='')return 'Data de conclusão não disponível';
  const date=new Date(value);return Number.isFinite(date.getTime())?'Conquistado em '+date.toLocaleDateString('pt-BR',{timeZone:'America/Sao_Paulo'}):'Data de conclusão não disponível';
 };
 let earned=[],signature='';
 function reset(){document.getElementById('gat-event-seals')?.remove();document.getElementById('gat-event-seals-dialog')?.remove();earned=[];signature='';}
 function makeSeal(seal){
  const button=document.createElement('button');button.type='button';button.className='gat-event-seal';
  const label=seal.title+' — '+dateLabel(seal.completedAt);button.title=label;button.setAttribute('aria-label',label);
  if(seal.kind==='pass_tier'){
   const art=window.GATPassTierArtwork?.(seal.profile);if(art)button.appendChild(art);
   button.addEventListener('click',()=>openCollection(seal.id));return button;
  }
  const image=document.createElement('img');image.src='assets/event-seals/'+seal.file+'.webp';image.alt='';image.width=48;image.height=48;image.decoding='async';
  button.appendChild(image);button.addEventListener('click',()=>openCollection(seal.id));return button;
 }
 function openCollection(selected){
  document.getElementById('gat-event-seals-dialog')?.remove();
  const dialog=document.createElement('dialog');dialog.id='gat-event-seals-dialog';dialog.className='gat-seals-dialog';dialog.setAttribute('aria-labelledby','gat-seals-title');
  const head=document.createElement('div');head.className='gat-seals-dialog-head';
  const title=document.createElement('h2');title.id='gat-seals-title';title.textContent='Selos e Conquistas';
  const close=document.createElement('button');close.type='button';close.className='gat-seals-close';close.textContent='Fechar';close.addEventListener('click',()=>dialog.close());head.append(title,close);
  const grid=document.createElement('div');grid.className='gat-seals-collection';
  for(const seal of earned){
   const item=document.createElement('article');item.className='gat-seals-detail'+(seal.id===selected?' selected':'');
   let img;
   if(seal.kind==='pass_tier'){img=window.GATPassTierArtwork?.(seal.profile)||document.createElement('span');img.classList.add('gat-tier-detail');}
   else{img=document.createElement('img');img.src='assets/event-seals/'+seal.file+'.webp';img.alt='';img.width=96;img.height=96;}
   const name=document.createElement('h3');name.textContent=seal.title;
   const date=document.createElement('p');date.textContent=dateLabel(seal.completedAt);
   item.append(img,name,date);if(seal.kind==='pass_tier'){const info=document.createElement('p');info.textContent=seal.tier+' passe'+(seal.tier===1?' mensal concluído':'s mensais concluídos');item.appendChild(info)}grid.appendChild(item);
  }
  dialog.append(head,grid);dialog.addEventListener('click',event=>{if(event.target===dialog)dialog.close()});document.body.appendChild(dialog);dialog.showModal();
 }
 function render(profile){
  const hero=document.querySelector('.driver-hero');if(!hero)return;
  const next=collect(profile,window.GATEventRegistry),sig=JSON.stringify([profile?.user,next.map(({profile,...seal})=>seal)]);if(sig===signature)return;
  reset();earned=next;signature=sig;if(!earned.length)return;
  const section=document.createElement('section');section.id='gat-event-seals';section.className='gat-event-seals';section.setAttribute('aria-label','Selos e conquistas do motorista');
  const label=document.createElement('div');label.className='gat-seals-label';
  const heading=document.createElement('h2');heading.textContent='SELOS E CONQUISTAS';
  const total=document.createElement('span');total.textContent=earned.length+' conquistado'+(earned.length===1?'':'s');label.append(heading,total);
  const row=document.createElement('div');row.className='gat-seals-row';row.setAttribute('role','group');row.setAttribute('aria-label','Passes e eventos concluídos');earned.slice(0,8).forEach(seal=>row.appendChild(makeSeal(seal)));
  const all=document.createElement('button');all.className='gat-seals-all';all.type='button';all.textContent=(earned.length>8?'+'+(earned.length-8)+' · ':'')+'Ver todos';all.addEventListener('click',()=>openCollection());
  section.append(label,row,all);hero.insertAdjacentElement('afterend',section);
 }
 window.addEventListener('gat-driver-profile-updated',event=>render(event.detail?.profile));window.addEventListener('gat-account-change',reset);
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>render(window.GATCurrentDriverProfile),{once:true});else render(window.GATCurrentDriverProfile);
})();
