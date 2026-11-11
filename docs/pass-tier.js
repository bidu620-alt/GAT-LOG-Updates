(()=>{
 'use strict';
 function render(profile){
  const hero=document.querySelector('.driver-hero');if(!hero)return;
  const count=Number(profile?.pass_tier?.tier),badge=document.getElementById('gat-pass-tier');
  if(!Number.isSafeInteger(count)||count<1){badge?.remove();return;}
  let element=badge;
  if(!element){
   element=document.createElement('div');element.id='gat-pass-tier';element.className='gat-pass-tier';
   element.setAttribute('role','img');
   element.innerHTML='<svg viewBox="0 0 64 76" aria-hidden="true" focusable="false"><defs><linearGradient id="gatTierBlue" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#176ce5"/><stop offset=".5" stop-color="#082d9a"/><stop offset="1" stop-color="#071b59"/></linearGradient><linearGradient id="gatTierGold"><stop stop-color="#a46b16"/><stop offset=".5" stop-color="#ffe18a"/><stop offset="1" stop-color="#d49c29"/></linearGradient></defs><path d="M7 7H57V70L32 59 7 70Z" fill="url(#gatTierBlue)" stroke="url(#gatTierGold)" stroke-width="2"/><path d="M3 6H61" stroke="url(#gatTierGold)" stroke-width="3" stroke-linecap="round"/><path d="m32 14 2.5 5 5.5.8-4 3.9.9 5.5-4.9-2.6-4.9 2.6.9-5.5-4-3.9 5.5-.8Z" fill="#f6ce67"/><path d="M23 16q-10 8 1 15m17-15q10 8-1 15" fill="none" stroke="#dfb44b" stroke-width="1.5"/><text data-tier-number x="32" y="44" text-anchor="middle" fill="#ffe08a" font-size="13" font-weight="900" font-family="Arial,sans-serif"></text><text data-tier-month x="32" y="54" text-anchor="middle" fill="#eff7ff" font-size="5.8" font-weight="700" font-family="Arial,sans-serif"></text></svg>';
   hero.appendChild(element);
  }
  const latest=profile.pass_tier.seasons?.at(-1)?.season_id||'';
  const match=/^(\d{4})-(0[1-9]|1[0-2])$/.exec(latest);
  const month=match?['JAN','FEV','MAR','ABR','MAI','JUN','JUL','AGO','SET','OUT','NOV','DEZ'][Number(match[2])-1]+'/'+match[1]:'';
  const label='Tier '+count+' — '+count+' passe'+(count===1?' mensal concluído':'s mensais concluídos')+(month?' — último passe '+month:'');
  element.title=label;element.setAttribute('aria-label',label);
  const number=element.querySelector('[data-tier-number]');number.textContent='TIER '+count;
  number.setAttribute('font-size',count>=1000?'8':count>=100?'10':'13');
  element.querySelector('[data-tier-month]').textContent=month;
 }
 window.addEventListener('gat-driver-profile-updated',event=>render(event.detail?.profile));
 window.addEventListener('gat-account-change',()=>document.getElementById('gat-pass-tier')?.remove());
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>render(window.GATCurrentDriverProfile),{once:true});
 else render(window.GATCurrentDriverProfile);
})();
