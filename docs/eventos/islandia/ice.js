(()=>{'use strict';
 const API='https://api.bidulog.com.br',names=['Akureyri','Blönduós','Borgarnes','Egilsstaðir','Höfn í Hornafirði','Hólmavík','Húsavík','Ísafjörður','Keflavík','Ólafsvík','Patreksfjörður','Reiquiavique','Selfoss','Thórshöfn'];
 const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const el=id=>document.getElementById(id);let loading=false;
 function session(){try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch{return null}}
 function draw(cities){el('cities').innerHTML=cities.map(c=>`<div class="city ${c.completed?'done':''}"><span class="check" aria-label="${c.completed?'Concluída':'Pendente'}">${c.completed?'✓':''}</span><span>${esc(c.label)}</span></div>`).join('')}
 draw(names.map(label=>({label,completed:false})));
 async function refresh(){
  if(loading||document.hidden)return;loading=true;el('refresh').disabled=true;
  try{
   const s=session(),url=API+'/api/eventos/islandia/'+(s?.token?'state':'config');
   const response=await fetch(url,s?.token?{method:'POST',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:s.token}),cache:'no-store',signal:AbortSignal.timeout(15000)}:{cache:'no-store',signal:AbortSignal.timeout(15000)});
   if(!response.ok)throw Error(response.status===401?'Sua sessão expirou. Entre novamente pelo perfil.':'Evento aguardando ativação na Central.');
   const d=await response.json();if(!d.ok||!d.event)throw Error('Evento aguardando ativação na Central.');
   const active=d.event.active===true,owned=d.owned===true;
   el('buy').hidden=!active||owned;
   el('availability').textContent=owned?'EVENTO ADQUIRIDO • acesso liberado':active?'Compra única • escolha Gold ou pagamento em reais na loja.':'Evento em preparação. As compras ainda não foram liberadas.';
   if(d.event.starts_at)el('start').textContent='Início do evento: '+new Date(d.event.starts_at).toLocaleString('pt-BR',{timeZone:'America/Sao_Paulo'})+' (Brasília). Evento permanente, sem prazo de encerramento.';
   if(d.progress){const p=d.progress;el('count').textContent=p.count+' / 14';el('bar').style.width=p.count/14*100+'%';document.querySelector('[role=progressbar]').setAttribute('aria-valuenow',p.count);draw(p.cities?.length?p.cities:names.map(label=>({label,completed:false})));el('status').textContent=p.completed?'Conquista desbloqueada: Desbravador da Islândia!':owned?'Complete entregas para marcar as cidades. Seu progresso é salvo na Central.':'Adquira o evento para começar a registrar suas cidades.';}
  }catch(e){el('availability').textContent=e.message==='Failed to fetch'?'Não foi possível consultar a Central agora.':e.message;el('status').textContent='A contagem não pôde ser atualizada. Nenhum progresso salvo foi apagado.';el('buy').hidden=true;
  }finally{loading=false;el('refresh').disabled=false;}
 }
 el('refresh').addEventListener('click',refresh);window.addEventListener('gat-account-change',()=>{el('count').textContent='— / 14';el('bar').style.width='0';draw(names.map(label=>({label,completed:false})));refresh()});
 refresh();setInterval(refresh,30000);document.addEventListener('visibilitychange',()=>{if(!document.hidden)refresh()});
})();
