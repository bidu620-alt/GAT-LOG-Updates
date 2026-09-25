(()=>{
const START=Date.parse('2026-09-30T00:00:00-03:00');
const END=Date.parse('2026-11-01T00:00:00-03:00');
const CARGOS=[
 'Logs','Lumber','Sawdust Panels','Wood Shavings','Potatoes','Sugar','Beef','Diesel','Petrol','Kerosene',
 'LPG','Fuel Tanker','Chemicals','Hot Chemicals','Acid','Arsenic','Chlorine','Hydrochloric Acid','Pesticides',
 'Sulphuric Acid','Hospital Waste','Scrap Metals','Used Car Batteries','Cement','Iron Pipes (large)',
 'Glass Panels','Coal','Ore','Low Bed Semi-trailers','Excavator'
];
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();

function deliveryTime(row){
  const t=Date.parse(row?.delivered_at||row?.completed_at||row?.date||'');
  return Number.isFinite(t)?t:NaN;
}
function progress(profile){
  const rows=Array.isArray(profile?.deliveries)?profile.deliveries:(Array.isArray(profile?.cargo_history)?profile.cargo_history:[]);
  const matched=new Map();
  for(const row of rows){
    const t=deliveryTime(row);
    if(!Number.isFinite(t)||t<START||t>=END)continue;
    const cargo=norm(row?.cargo||row?.cargo_name||row?.name);
    for(const official of CARGOS){
      if(cargo===norm(official)&&!matched.has(official))matched.set(official,{row,t});
    }
  }
  let completedAt=null;
  if(matched.size===CARGOS.length){
    completedAt=Math.max(...[...matched.values()].map(x=>x.t));
  }
  return {count:matched.size,completedAt};
}
function fmt(t){
  if(!Number.isFinite(t))return '';
  return new Date(t).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo',day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'});
}
function render(profile){
  const card=document.getElementById('achievementHalloween2026');
  if(!card)return;
  const {count,completedAt}=progress(profile);
  const unlocked=count===30;
  card.classList.toggle('unlocked',unlocked);
  card.classList.toggle('locked',!unlocked);
  const bar=document.getElementById('achievementHalloweenBar');
  if(bar)bar.style.width=Math.min(100,Math.round(count/30*100))+'%';
  const state=document.getElementById('achievementHalloweenState');
  if(state)state.textContent=unlocked?'CONQUISTA DESBLOQUEADA • 30/30':'Bloqueado • '+count+'/30';
  const date=document.getElementById('achievementHalloweenDate');
  if(date)date.textContent=unlocked?'Concluída em '+fmt(completedAt):'Complete o Evento Hallowin para liberar este selo.';
  const total=document.getElementById('achievementCount');
  if(total)total.textContent=(unlocked?'1':'0')+' CONQUISTA'+(unlocked?'':'S');
}
window.addEventListener('gat-driver-profile-updated',e=>render(e.detail?.profile||null));
})();