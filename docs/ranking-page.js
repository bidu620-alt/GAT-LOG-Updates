(()=>{const API='https://api.gatlogets2.com.br';
const RANKING=API+'/api/public/ranking',SAFETY=API+'/api/public/safety-ranking';
const urlMonth=new URLSearchParams(location.search).get('month')||'';
let gat=null,safe=null,mode='gat',month=/^\d{4}-\d{2}$/.test(urlMonth)?urlMonth:'',gatReq=0;
const n=v=>Number(v)||0;
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const label=u=>{const s=String(u||'motorista').replace(/^@/,'');return s.charAt(0).toUpperCase()+s.slice(1)};
const level=xp=>Math.max(1,Math.floor(n(xp)/2000)+1);
function monthLabel(v){const m=/^(\d{4})-(\d{2})$/.exec(String(v||''));if(!m)return'Mês atual';const d=new Date(Date.UTC(+m[1],+m[2]-1,1));const s=d.toLocaleDateString('pt-BR',{month:'long',year:'numeric',timeZone:'UTC'});return s.charAt(0).toUpperCase()+s.slice(1)}
async function json(url){const c=new AbortController(),t=setTimeout(()=>c.abort(),6000);try{const r=await fetch(url,{cache:'no-store',signal:c.signal});const j=await r.json().catch(()=>null);return r.ok?j:null}finally{clearTimeout(t)}}
function statusText(){if(mode==='xp')return'XP e nível são dados acumulados da carreira e não zeram mensalmente.';if(mode==='safe')return'Cargas perfeitas são exibidas pela qualidade registrada na carreira.';const season=gat?.season||gat?.current_month||month;return 'Ranking mensal de '+monthLabel(season)+'. 99 km completos = 10 pontos + bônus de peso − multas − danos.'}
function metric(item){if(mode==='xp')return{main:n(item.xp).toLocaleString('pt-BR')+' XP',sub:'Nível '+level(item.xp)};if(mode==='safe')return{main:n(item.perfect_trips).toLocaleString('pt-BR')+' PERFEITAS',sub:n(item.speed_fines)+' multas • '+n(item.penalty_xp).toLocaleString('pt-BR')+' penalidade'};return{main:n(item.points).toLocaleString('pt-BR')+' PONTOS',sub:n(item.monthly_completed??item.total_deliveries).toLocaleString('pt-BR')+' entregas • '+Math.round(n(item.monthly_km)).toLocaleString('pt-BR')+' km'}}
function list(){if(mode==='safe'){const a=Array.isArray(safe?.ranking)?safe.ranking.slice():[];a.sort((a,b)=>n(b.perfect_trips)-n(a.perfect_trips)||n(a.penalty_xp)-n(b.penalty_xp)||n(a.speed_fines)-n(b.speed_fines)||String(a.user||'').localeCompare(String(b.user||''),'pt-BR'));return a}const a=Array.isArray(gat?.ranking)?gat.ranking.slice():[];if(mode==='xp')a.sort((a,b)=>n(b.xp)-n(a.xp)||String(a.user||'').localeCompare(String(b.user||''),'pt-BR'));return a}
function render(){const root=document.getElementById('rankList'),status=document.getElementById('rankingStatus');if(!root||!status)return;const rows=list();root.textContent='';status.textContent=rows.length?statusText():'Nenhum motorista classificado neste período.';rows.slice(0,50).forEach((item,i)=>{const a=document.createElement('a');a.className='ranking-row';a.href='motorista.html?u='+encodeURIComponent(item.user||'');const m=metric(item),initial=label(item.user).charAt(0);a.innerHTML='<span class="ranking-pos">'+(i+1)+'</span><span class="ranking-driver"><span class="ranking-avatar">'+esc(initial)+'</span><span><b>'+esc(label(item.user))+'</b><small>@'+esc(item.user||'motorista')+'</small></span></span><span class="ranking-score"><b>'+esc(m.main)+'</b><small>'+esc(m.sub)+'</small></span>';root.appendChild(a)})}
function syncHeader(){const season=gat?.season||gat?.current_month||month||'',title=document.getElementById('rankingSeasonTitle'),state=document.getElementById('rankingSeasonState');if(title)title.textContent=monthLabel(season);if(state)state.textContent=gat?.is_current_month===false?'Histórico mensal':'Ranking mensal ativo'}
function months(){const el=document.getElementById('rankMonth');if(!el||!gat)return;const values=[...new Set([gat.current_month,...(Array.isArray(gat.available_months)?gat.available_months:[])].filter(x=>/^\d{4}-\d{2}$/.test(String(x||''))))].sort().reverse();el.textContent='';for(const v of values){const o=document.createElement('option');o.value=v;o.textContent=monthLabel(v)+(v===gat.current_month?' • atual':'');el.appendChild(o)}const selected=gat.season||month||gat.current_month||values[0]||'';if(selected)el.value=selected;el.disabled=mode!=='gat'}
async function loadGat(m=month){const req=++gatReq,s=document.getElementById('rankingStatus');try{const d=await json(RANKING+(m?'?month='+encodeURIComponent(m):''));if(req!==gatReq)return;if(d?.ok){gat=d;month=d.season||m||d.current_month||'';months();syncHeader();render();return}if(s)s.textContent='Ranking temporariamente indisponível.'}catch(_){if(req!==gatReq)return;if(s)s.textContent='Aguardando conexão com a Central GAT.'}}
async function loadSafe(){if(safe)return render();try{const d=await json(SAFETY);if(d?.ok){safe=d;render();return}}catch(_){}const s=document.getElementById('rankingStatus');if(s)s.textContent='Cargas perfeitas temporariamente indisponíveis.'}
document.querySelectorAll('.rank-tab').forEach(btn=>btn.addEventListener('click',()=>{mode=btn.dataset.rankMode||'gat';document.querySelectorAll('.rank-tab').forEach(x=>x.classList.toggle('active',x===btn));const sel=document.getElementById('rankMonth');if(sel)sel.disabled=mode!=='gat';if(mode==='safe')loadSafe();else render()}));
document.getElementById('rankMonth')?.addEventListener('change',e=>{
  const chosen=e.target.value||'';
  if(!chosen)return;
  const url=new URL(location.href);
  url.searchParams.set('month',chosen);
  location.href=url.toString();
});
loadGat();
setInterval(()=>{if(!document.hidden)loadGat(month)},60000);
})();