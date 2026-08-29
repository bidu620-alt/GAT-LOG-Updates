// GAT-LOG • revisão manual de trabalho pelo Admin.
// Mostra o trabalho esperado e permite confirmar manualmente a carga atual quando a missão ainda está ATRIBUÍDA.
(function(){
  if(typeof actionButtons!=='function'||typeof renderDrivers!=='function'||typeof runAction!=='function')return;

  const baseActionButtons=actionButtons;
  const baseRenderDrivers=renderDrivers;
  const baseRunAction=runAction;

  function missionExpected(d){
    const m=d?.current_mission||{};
    return String(m.title||m.category||m.custom_cargo||'Trabalho GAT').trim();
  }

  function canReview(d){
    const m=d?.current_mission;
    if(!m)return false;
    const state=String(m.state||'').toLowerCase();
    const cargo=String(d?.cargo||'').trim();
    return state==='assigned'&&!!d?.online&&!!cargo&&!d?.disabled;
  }

  actionButtons=function(d){
    let html=baseActionButtons(d);
    if(canReview(d)){
      html+='<button data-action="confirm_mission" data-user="'+esc(d.user)+'" class="approve">CONFIRMAR CARGA</button>';
    }
    return html;
  };

  renderDrivers=function(){
    baseRenderDrivers();
    try{
      const term=clean($('adminSearch').value);
      const list=drivers.filter(d=>!term||clean(d.user).includes(term)||String(d.truck||'').toLowerCase().includes(term)||String(d.cargo||'').toLowerCase().includes(term));
      const rows=Array.from($('adminRows').querySelectorAll('tr'));
      rows.forEach((tr,i)=>{
        const d=list[i],m=d?.current_mission,small=tr.querySelector('.mission-line small');
        if(!m||!small)return;
        const expected=missionExpected(d);
        const state=String(m.state||'').toLowerCase();
        small.textContent=expected+(state==='assigned'?' • aguardando validação':'');
        if(canReview(d)){
          const cargo=String(d.cargo||'').trim();
          tr.title='Trabalho esperado: '+expected+' | Carga atual: '+cargo;
        }
      });
    }catch(_){}
  };

  runAction=async function(action,user,extra={}){
    if(action==='confirm_mission'){
      const d=drivers.find(x=>clean(x.user)===clean(user));
      const expected=missionExpected(d||{});
      const cargo=String(d?.cargo||'').trim()||'carga não informada';
      const ok=confirm('CONFIRMAR CARGA MANUALMENTE?\n\nMotorista: @'+user+'\nTrabalho esperado: '+expected+'\nCarga atual: '+cargo+'\n\nUse esta opção somente se você conferiu que a carga pertence ao trabalho escolhido. A Central ainda exigirá uma viagem com pelo menos 250 km.');
      if(!ok)return;
    }
    return baseRunAction(action,user,extra);
  };
})();
