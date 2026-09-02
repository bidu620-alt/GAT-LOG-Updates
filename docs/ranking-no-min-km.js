(()=>{
  function apply(){
    const min=document.getElementById('workMinKm');
    if(min)min.textContent='Sem distância mínima';
    const rule=document.querySelector('.work-catalog-section .catalog-rule');
    if(rule)rule.innerHTML='<b>30</b><span>VIAGENS / MÊS</span><b>SEM MÍN.</b><span>KM REAIS</span>';
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
  setInterval(apply,1000);
})();
