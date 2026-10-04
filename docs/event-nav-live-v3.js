(()=>{const EVENTS=[
{id:'children',start:Date.parse('2026-10-05T00:00:00-03:00'),end:Date.parse('2026-10-13T00:00:00-03:00'),href:'eventos/dia-das-criancas/',label:'Dia das Crianças'}
];
const qs=new URLSearchParams(location.search);
function apply(){
 const now=Date.now(),a=EVENTS.find(e=>now>=e.start&&now<e.end)||null;
 document.querySelectorAll('.event-month-link').forEach(x=>{
   x.style.display=a?'':'none';
   if(a){x.href=a.href;x.textContent=a.label}
 });
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
setInterval(apply,60000);
})();