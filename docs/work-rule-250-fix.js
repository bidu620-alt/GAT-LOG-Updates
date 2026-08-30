(()=>{
  function fixText(){
    const min=document.getElementById('workMinKm');if(min)min.textContent='250 km';
    const msg=document.getElementById('workOwnerMessage');if(msg&&/500\s*km/i.test(msg.textContent||''))msg.textContent=String(msg.textContent).replace(/500\s*km/gi,'250 km');
  }
  if(typeof applyWorkRuleView==='function'){
    const base=applyWorkRuleView;
    window.applyWorkRuleView=function(m){const r=base(m);fixText();return r};
  }
  if(typeof updateOwnerArea==='function'){
    const base=updateOwnerArea;
    window.updateOwnerArea=function(){const r=base.apply(this,arguments);fixText();return r};
  }
  fixText();setTimeout(fixText,300);setInterval(fixText,2500);
})();
