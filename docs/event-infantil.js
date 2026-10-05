(()=>{
function q(id){return document.getElementById(id)}
function render(){
 const panel=q('children-event-panel'); if(!panel)return;
 const profile=window.GATCurrentDriverProfile||null, registry=window.GATEventRegistry;
 const event=registry?.eventById?.('children_2026'); if(!event)return;
 const p=profile?registry.progress(profile,'children_2026'):{count:0,goal:event.goal,completed:false};
 q('childrenEventProgress').textContent=p.count+'/'+p.goal;
 q('childrenEventBar').style.width=(p.goal?Math.min(100,(p.count/p.goal)*100):0)+'%';
 q('childrenEventStatus').textContent=p.completed?'EVENTO CONCLUÍDO':'EM ANDAMENTO';
 q('childrenEventStatus').className='pill '+(p.completed?'green':'amber');
 const list=q('childrenEventSteps');list.textContent='';
 event.cargos.forEach((c,i)=>{const done=!!p.matched?.has(c.official),item=document.createElement('article');item.className='children-event-step '+(done?'done':'');item.innerHTML='<div class="children-step-icon">'+['🧸','🍫','🍬','🥛','👕','🍶'][i]+'</div><div><small>ENTREGA '+String(i+1).padStart(2,'0')+'</small><h3></h3><p></p></div><strong>'+(done?'✓ CONCLUÍDA':i===p.count?'PRÓXIMA':'BLOQUEADA')+'</strong>';item.querySelector('h3').textContent=c.label;item.querySelector('p').textContent=c.origin+' → '+c.destination;list.appendChild(item)})
}
function bind(){const nav=document.querySelector('.driver-tabs');if(nav&&!nav.dataset.childrenBound){nav.dataset.childrenBound='1';nav.addEventListener('click',e=>{if(e.target.closest('[data-tab="children-event"]'))render()})}window.addEventListener('gat-driver-profile-updated',render);render()}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
})();