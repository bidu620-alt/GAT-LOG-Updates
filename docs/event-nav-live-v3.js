(()=>{const START=Date.parse('2026-09-30T00:00:00-03:00'),END=Date.parse('2026-11-01T00:00:00-03:00'),PREVIEW_KEY='gat_halloween_preview';
const qs=new URLSearchParams(location.search);
if(qs.get('halloween')==='preview')try{sessionStorage.setItem(PREVIEW_KEY,'1')}catch(_){}
if(qs.get('halloween')==='off')try{sessionStorage.removeItem(PREVIEW_KEY)}catch(_){}
function preview(){try{return sessionStorage.getItem(PREVIEW_KEY)==='1'}catch(_){return qs.get('halloween')==='preview'}}
function theme(on){
 document.body?.classList.toggle('gat-halloween',on);
 let l=document.getElementById('gatHalloweenTheme');
 if(on&&!l){l=document.createElement('link');l.id='gatHalloweenTheme';l.rel='stylesheet';l.href='halloween-theme-live-v3.css?v=1';document.head.appendChild(l)}
 if(!on&&l)l.remove();
}
function apply(){
 const now=Date.now(),isPreview=preview(),launched=now>=START||isPreview,themeActive=(now>=START&&now<END)||isPreview;
 document.querySelectorAll('.event-month-link').forEach(a=>a.style.display=launched?'':'none');
 theme(themeActive);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
setInterval(apply,60000);
})();