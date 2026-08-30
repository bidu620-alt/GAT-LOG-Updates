(()=>{
  function clean(v){return String(v||'').replace(/^@/,'').trim().toLowerCase()}
  function getSessionNow(){try{if(typeof getSession==='function')return getSession()}catch(_){}try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}}
  function activateWork(){const b=document.querySelector('.driver-tabs [data-tab="work"]');if(!b)return false;b.click();setTimeout(()=>b.scrollIntoView({behavior:'smooth',block:'center'}),80);return true;}
  function goMyWork(){
    const s=getSessionNow(),user=clean(s?.user);
    if(!user){if(typeof showAccountModal==='function')showAccountModal('login');return;}
    const current=clean(new URL(location.href).searchParams.get('u'));
    if((!current||current===user)&&activateWork())return;
    location.href='motorista.html?u='+encodeURIComponent(user)+'&tab=work';
  }
  function apply(){
    const old=document.querySelector('.gat-driver-directory-nav');
    if(old)old.remove();
    const nav=document.querySelector('.topbar nav');
    if(nav&&!document.getElementById('gatTopMyWork')){
      const btn=document.createElement('button');
      btn.id='gatTopMyWork';btn.type='button';btn.textContent='MEU TRABALHO';
      btn.style.cssText='margin-left:6px;border:1px solid #49a8ff;border-radius:10px;background:linear-gradient(135deg,#0877dd,#1557c8);color:#fff;padding:9px 13px;font-size:10px;font-weight:950;cursor:pointer;box-shadow:0 5px 18px #0877dd35;white-space:nowrap';
      btn.addEventListener('click',goMyWork);
      const ranking=[...nav.querySelectorAll('a')].find(a=>/ranking/i.test(a.textContent||''));
      if(ranking)ranking.insertAdjacentElement('afterend',btn);else nav.appendChild(btn);
    }
    const params=new URLSearchParams(location.search);
    if(params.get('tab')==='work')setTimeout(activateWork,120);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply);else apply();
  setTimeout(apply,400);
  setTimeout(apply,1200);
})();
