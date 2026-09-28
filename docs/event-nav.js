(()=>{const START=Date.parse('2026-10-01T00:00:00-03:00'),END=Date.parse('2026-11-01T00:00:00-03:00');
function theme(on){
  document.body?.classList.toggle('gat-halloween',on);
  let l=document.getElementById('gatHalloweenTheme');
  if(on&&!l){l=document.createElement('link');l.id='gatHalloweenTheme';l.rel='stylesheet';l.href='halloween-theme.css?v=1';document.head.appendChild(l)}
  if(!on&&l)l.remove();
}
function apply(){
  const now=Date.now(),launched=now>=START,themeActive=launched&&now<END;
  document.querySelectorAll('.event-month-link').forEach(a=>a.style.display=launched?'':'none');
  theme(themeActive);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
setInterval(apply,60000);
})();