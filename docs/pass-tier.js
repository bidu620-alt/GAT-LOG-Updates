(()=>{
 'use strict';
 function clearReward(){
  const hero=document.querySelector('.driver-hero');
  hero?.classList.remove('gat-pass-elite');
  hero?.removeAttribute('data-pass-theme');
  document.getElementById('gat-pass-title')?.remove();
  document.getElementById('gat-pass-tier')?.remove();
 }
 function clearAura(){
  const hero=document.querySelector('.driver-hero');
  hero?.removeAttribute('data-driver-aura');
  const avatar=hero?.querySelector('.driver-avatar');
  if(avatar?.hasAttribute('data-aura-title')){
   avatar.removeAttribute('title');avatar.removeAttribute('data-aura-title');
  }
 }
 function render(profile){
  const hero=document.querySelector('.driver-hero');if(!hero)return;
  const level=Number(profile?.level);
  if(Number.isSafeInteger(level)&&level>0){
   const aura=level>=100?'red':level>=80?'gold':level>=60?'violet':level>=40?'blue':level>=20?'green':'silver';
   const names={silver:'Prata',green:'Verde',blue:'Azul',violet:'Violeta',gold:'Dourada',red:'Vermelha'};
   hero.setAttribute('data-driver-aura',aura);
   const avatar=hero.querySelector('.driver-avatar');
   if(avatar){avatar.title='Aura '+names[aura]+' · Nível '+level;avatar.setAttribute('data-aura-title','1');}
  }else clearAura();
  const count=Number(profile?.pass_tier?.tier);
  if(!Number.isSafeInteger(count)||count<1){clearReward();return;}
  hero.classList.add('gat-pass-elite');
  const ident=hero.querySelector('.driver-ident');
  if(ident&&!document.getElementById('gat-pass-title')){
   const title=document.createElement('div');title.id='gat-pass-title';title.className='gat-pass-title';
   title.innerHTML='<span aria-hidden="true">★</span> <span data-pass-theme-name></span>';
   title.title='Tema da última temporada concluída. Muda somente quando você conclui outro passe.';
   ident.appendChild(title);
  }
  const latest=(profile.pass_tier.seasons||[]).map(season=>season.season_id).filter(id=>typeof id==='string'&&/^\d{4}-(0[1-9]|1[0-2])$/.test(id)).sort().at(-1)||'';
  const match=/^(\d{4})-(0[1-9]|1[0-2])$/.exec(latest);
  const month=match?['JAN','FEV','MAR','ABR','MAI','JUN','JUL','AGO','SET','OUT','NOV','DEZ'][Number(match[2])-1]+'/'+match[1]:'';
  const themes=["Horizonte Azul","Rota Violeta","Rota Esmeralda","Estrada de Cobre","Safira da Estrada","Aurora Boreal","Inverno na Estrada","Asfalto e Prata","Rota da Primavera","Chamas da Estrada","Elite da Estrada","Luzes da Estrada"];
  const themeName=match?themes[Number(match[2])-1]:'Elite da Estrada';
  if(match)hero.setAttribute('data-pass-theme',match[2]);else hero.removeAttribute('data-pass-theme');
  const themeLabel=hero.querySelector('[data-pass-theme-name]');
  if(themeLabel)themeLabel.textContent=themeName.toLocaleUpperCase('pt-BR')+(month?' · '+month:'');

 }

 let tierArtworkSeq=0;
 window.GATPassTierArtwork=profile=>{
  const count=Number(profile?.pass_tier?.tier);if(!Number.isSafeInteger(count)||count<1)return null;
  const element=document.createElement('span');element.className='gat-tier-art';element.setAttribute('aria-hidden','true');
  element.innerHTML='<svg viewBox="0 0 64 76" aria-hidden="true" focusable="false"><defs><linearGradient id="gatTierBlue" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#176ce5"/><stop offset=".5" stop-color="#082d9a"/><stop offset="1" stop-color="#071b59"/></linearGradient><linearGradient id="gatTierGold"><stop stop-color="#a46b16"/><stop offset=".5" stop-color="#ffe18a"/><stop offset="1" stop-color="#d49c29"/></linearGradient></defs><path d="M7 7H57V70L32 59 7 70Z" fill="url(#gatTierBlue)" stroke="url(#gatTierGold)" stroke-width="2"/><path d="M3 6H61" stroke="url(#gatTierGold)" stroke-width="3" stroke-linecap="round"/><path d="m32 14 2.5 5 5.5.8-4 3.9.9 5.5-4.9-2.6-4.9 2.6.9-5.5-4-3.9 5.5-.8Z" fill="#f6ce67"/><path d="M23 16q-10 8 1 15m17-15q10 8-1 15" fill="none" stroke="#dfb44b" stroke-width="1.5"/><text data-tier-number x="32" y="44" text-anchor="middle" fill="#ffe08a" font-size="13" font-weight="900" font-family="Arial,sans-serif"></text><text data-tier-month x="32" y="54" text-anchor="middle" fill="#eff7ff" font-size="5.8" font-weight="700" font-family="Arial,sans-serif"></text></svg>';
  const artId=++tierArtworkSeq;element.innerHTML=element.innerHTML.replaceAll('gatTierBlue','gatTierBlue-'+artId).replaceAll('gatTierGold','gatTierGold-'+artId);
  const number=element.querySelector('[data-tier-number]');number.textContent='TIER '+count;number.setAttribute('font-size',count>=1000?'8':count>=100?'10':'13');
  const latest=(profile.pass_tier.seasons||[]).map(s=>s.season_id).filter(s=>/^\d{4}-(0[1-9]|1[0-2])$/.test(s)).sort().at(-1)||'';
  const match=/^(\d{4})-(0[1-9]|1[0-2])$/.exec(latest);
  element.querySelector('[data-tier-month]').textContent=match?['JAN','FEV','MAR','ABR','MAI','JUN','JUL','AGO','SET','OUT','NOV','DEZ'][Number(match[2])-1]+'/'+match[1]:'';
  return element;
 };

 window.addEventListener('gat-driver-profile-updated',event=>render(event.detail?.profile));
 window.addEventListener('gat-account-change',()=>{clearReward();clearAura();});
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>render(window.GATCurrentDriverProfile),{once:true});
 else render(window.GATCurrentDriverProfile);
})();
