(()=>{'use strict';
const API='https://api.gatlogets2.com.br';
const q=id=>document.getElementById(id),n=v=>Number(v)||0;
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const fmt=v=>n(v).toLocaleString('pt-BR');
const monthNow=new Intl.DateTimeFormat('sv-SE',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit'}).format(new Date());
let data=null,mode='gat',scope='month',period=monthNow,request=0,months=[monthNow],years=[monthNow.slice(0,4)];
const monthLabel=m=>new Intl.DateTimeFormat('pt-BR',{month:'long',year:'numeric',timeZone:'UTC'}).format(new Date(m+'-01T12:00:00Z'));
const periodLabel=()=>scope==='total'?'Histórico total':scope==='year'?'Ano '+period:monthLabel(period);
function controls(){q('rankMonth').innerHTML=(scope==='total'?['total']:scope==='year'?years:months).map(v=>'<option value="'+esc(v)+'">'+esc(scope==='total'?'Toda a carreira':scope==='year'?v:monthLabel(v))+'</option>').join('');q('rankMonth').value=period;q('rankMonth').disabled=scope==='total';q('rankingSeasonTitle').textContent=periodLabel();q('rankingSeasonState').textContent='Classificação por '+(scope==='month'?'mês':scope==='year'?'ano':'histórico total');}
function render(){if(!data)return;const list=[...data.ranking];
const field=mode==='xp'?'xp':mode==='safe'?'perfect_trips':mode==='achievements'?'achievements':'points';
list.sort((a,b)=>n(b[field])-n(a[field])||(mode==='safe'?n(a.penalty_xp)-n(b.penalty_xp)||n(a.speed_fines)-n(b.speed_fines):mode==='gat'?n(b.monthly_completed)-n(a.monthly_completed)||n(b.monthly_km)-n(a.monthly_km)||n(b.perfect_trips)-n(a.perfect_trips)||n(a.penalty_xp)-n(b.penalty_xp)||n(a.speed_fines)-n(b.speed_fines):0)||String(a.user).localeCompare(String(b.user),'pt-BR'));
q('rankingStatus').textContent=periodLabel()+'. '+(mode==='achievements'?'Quantidade de conquistas desbloqueadas no período, sem conceder novos pontos ou XP.':mode==='xp'?'XP do período; nível acumulado da carreira.':'Valores registrados nas entregas, sem mudar as regras de pontuação.')+(data.legacy?' A Central ainda precisa receber a atualização para os outros períodos e conquistas.':'');
q('rankList').innerHTML=list.slice(0,50).map((item,i)=>{const user=String(item.user||'motorista'),name=user.charAt(0).toUpperCase()+user.slice(1);let main,sub;
if(mode==='xp'){main=fmt(item.xp)+' XP';sub='Nível '+Math.max(1,Math.floor(n(item.career_xp??item.xp)/2000)+1)+' de carreira';}
else if(mode==='safe'){main=fmt(item.perfect_trips)+' PERFEITAS';sub=fmt(item.speed_fines)+' multas • '+fmt(item.penalty_xp)+' penalidade';}
else if(mode==='achievements'){main=fmt(item.achievements)+' CONQUISTAS';sub='Desbloqueadas no período';}
else{main=fmt(item.points)+' PONTOS';sub=fmt(item.monthly_completed)+' entregas • '+fmt(Math.round(n(item.monthly_km)))+' km';}
return '<a class="ranking-row" href="motorista.html?u='+encodeURIComponent(user)+'"><span class="ranking-pos">'+(i+1)+'</span><span class="ranking-driver"><span class="ranking-avatar">'+esc(name[0])+'</span><span><b>'+esc(name)+'</b><small>@'+esc(user)+'</small></span></span><span class="ranking-score"><b>'+esc(main)+'</b><small>'+esc(sub)+'</small></span></a>';}).join('');if(!list.length)q('rankingStatus').textContent='Nenhum motorista registrado neste período.';}
async function fetchJson(url){const response=await fetch(url,{cache:'no-store',signal:AbortSignal.timeout(10000)});if(!response.ok)throw Error('HTTP '+response.status);return response.json();}
async function load(){const serial=++request;data=null;q('rankList').textContent='';q('rankingStatus').textContent='Carregando '+periodLabel()+'…';controls();try{let result;
try{result=await fetchJson(API+'/api/public/ranking-period?'+new URLSearchParams({scope,period}));}
catch(error){if(scope!=='month'||mode==='achievements')throw error;result=await fetchJson(API+'/api/public/ranking?month='+period);result.legacy=true;}
if(serial!==request)return;if(!result.ok||!Array.isArray(result.ranking))throw Error('Dados indisponíveis');data=result;months=result.available_months||months;years=result.available_years||[...new Set(months.map(m=>m.slice(0,4)))];controls();render();}
catch(error){if(serial===request){q('rankingStatus').textContent='Este ranking está indisponível. A Central pode precisar receber a atualização dos períodos e conquistas.';}}}
q('rankScope').addEventListener('change',()=>{scope=q('rankScope').value;period=scope==='total'?'total':scope==='year'?monthNow.slice(0,4):monthNow;load();});
q('rankMonth').addEventListener('change',()=>{period=q('rankMonth').value;load();});
document.querySelectorAll('.rank-tab').forEach(btn=>btn.addEventListener('click',()=>{mode=btn.dataset.rankMode;document.querySelectorAll('.rank-tab').forEach(x=>x.classList.toggle('active',x===btn));if(data&&!data.legacy)render();else load();}));
load();})();