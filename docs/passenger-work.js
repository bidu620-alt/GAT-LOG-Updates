(function(){
 'use strict';
 function rawOf(row){try{return typeof row.raw_json==='string'?JSON.parse(row.raw_json):row.raw_json||{}}catch{return {}}}
 function transport(row){const raw=rawOf(row||{});return row?.transport_type==='passengers'||row?.job_kind==='bus_job'||raw.mission?.job_kind==='bus_job'?'passengers':'cargo';}
 window.GATTransportType=transport;
 function span(text){const el=document.createElement('span');el.textContent=text;return el;}
 window.GATPassengerWork=function(history,mission){
  const work=document.querySelector('[data-panel="work"]');if(!work)return;
  let card=document.getElementById('passengerWork');
  if(!card){card=document.createElement('article');card.id='passengerWork';card.className='driver-card';card.style.margin='16px 0';const title=document.createElement('h2');title.textContent='🚌 Transporte de passageiros';card.appendChild(title);const note=document.createElement('p');note.textContent='Cargas e passageiros compartilham a mesma carreira, os quilômetros, XP e Gold. A chave é da marca do veículo.';card.appendChild(note);const state=document.createElement('p');state.id='passengerCurrent';card.appendChild(state);const rows=document.createElement('div');rows.id='passengerWorkRows';card.appendChild(rows);work.insertBefore(card,work.querySelector('.work-catalog-section'));}
  const state=document.getElementById('passengerCurrent');state.textContent=mission?.job_kind==='bus_job'?'Em andamento: '+(mission.source||'Origem')+' → '+(mission.destination||'Destino'):'Sem transporte de passageiros ativo. No aplicativo .42, confirme o serviço quando o jogo não informar o trabalho.';
  const rows=document.getElementById('passengerWorkRows');rows.replaceChildren();const list=(Array.isArray(history)?history:[]).filter(x=>transport(x)==='passengers').slice(-10).reverse();
  if(!list.length){rows.appendChild(span('Nenhum transporte de passageiros concluído no histórico exibido.'));return;}
  for(const item of list){const row=document.createElement('div');row.className='delivery-row';const raw=rawOf(item),vehicle=item.vehicle_evidence||raw.audit?.vehicle_evidence||{};const make=vehicle.vehicle_make||vehicle.make||'',model=vehicle.vehicle_model||vehicle.model||'';
   row.append(span((item.source||'?')+' → '+(item.destination||'?')),span([make,model].filter(Boolean).join(' ')||'Ônibus'),span(Number(item.distance_km||0).toLocaleString('pt-BR',{maximumFractionDigits:1})+' km'),span(Number(item.xp_awarded??item.xp??0).toLocaleString('pt-BR')+' XP'),span(Number(item.gold_awarded||0).toLocaleString('pt-BR')+' Gold'),span('CONCLUÍDA'));rows.appendChild(row);
  }
 };
})();
