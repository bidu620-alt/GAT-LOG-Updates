(()=>{
function fmt(t){
  if(!Number.isFinite(t))return '';
  return new Date(t).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo',day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'});
}
function render(profile){
  const card=document.getElementById('achievementHalloween2026');if(!card)return;
  const r=window.GATEventRegistry?.progress?.(profile,'halloween_2026')||{count:0,goal:30,completed:false,completedAt:null};
  const unlocked=!!r.completed;
  card.classList.toggle('unlocked',unlocked);card.classList.toggle('locked',!unlocked);
  const bar=document.getElementById('achievementHalloweenBar');if(bar)bar.style.width=Math.min(100,Math.round((Number(r.count)||0)/Math.max(1,Number(r.goal)||30)*100))+'%';
  const state=document.getElementById('achievementHalloweenState');if(state)state.textContent=unlocked?'CONQUISTA DESBLOQUEADA • '+r.goal+'/'+r.goal:'Bloqueado • '+r.count+'/'+r.goal;
  const date=document.getElementById('achievementHalloweenDate');if(date)date.textContent=unlocked?'Concluída em '+fmt(r.completedAt):'Complete o Evento Halloween para liberar este selo.';
  const total=document.getElementById('achievementCount');if(total)total.textContent=(unlocked?'1':'0')+' CONQUISTA'+(unlocked?'':'S');
}
window.addEventListener('gat-driver-profile-updated',e=>render(e.detail?.profile||null));
})();