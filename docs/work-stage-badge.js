(()=>{
  function stageNow(){
    try{
      if(typeof profile!=='undefined'&&profile){
        const raw=profile.stage ?? profile.current_stage ?? profile.stage_number ?? profile.work_stage;
        const n=Math.floor(Number(raw));
        if(Number.isFinite(n)&&n>0)return n;
      }
    }catch(_){}
    return 1;
  }
  function ensureStyle(){
    if(document.getElementById('gatStageBadgeStyle'))return;
    const s=document.createElement('style');
    s.id='gatStageBadgeStyle';
    s.textContent='.gat-work-statuses{display:flex;align-items:center;gap:8px;flex-wrap:wrap;justify-content:flex-end}.gat-stage-badge{display:inline-flex;align-items:center;justify-content:center;gap:5px;min-width:48px;padding:6px 10px;border:1px solid #49a8ff;border-radius:999px;background:linear-gradient(135deg,#0877dd,#1557c8);color:#fff;font-size:11px;font-weight:950;box-shadow:0 5px 18px #0877dd35}.gat-stage-star{font-size:14px;line-height:1;color:#dff1ff;text-shadow:0 0 10px #8ed0ff}@media(max-width:560px){.gat-work-statuses{justify-content:flex-start}.gat-stage-badge{min-width:44px;padding:6px 9px}}';
    document.head.appendChild(s);
  }
  function apply(){
    ensureStyle();
    const state=document.getElementById('workState');
    if(!state)return;
    let wrap=state.parentElement;
    if(!wrap||!wrap.classList.contains('gat-work-statuses')){
      wrap=document.createElement('div');
      wrap.className='gat-work-statuses';
      state.parentNode.insertBefore(wrap,state);
      wrap.appendChild(state);
    }
    let badge=document.getElementById('gatWorkStageBadge');
    if(!badge){
      badge=document.createElement('span');
      badge.id='gatWorkStageBadge';
      badge.className='gat-stage-badge';
      wrap.insertBefore(badge,state);
    }
    const stage=stageNow();
    badge.innerHTML='<span class="gat-stage-star">★</span><span id="gatWorkStageNumber">'+stage+'</span>';
    badge.title='Estágio '+stage;
    badge.setAttribute('aria-label','Estágio '+stage);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply);else apply();
  setInterval(apply,1200);
})();