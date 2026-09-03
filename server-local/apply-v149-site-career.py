from pathlib import Path

root=Path(__file__).resolve().parents[1]

def edit(path, replacements):
    p=root/path
    s=p.read_text(encoding='utf-8')
    for old,new,label in replacements:
        if old not in s:
            if new in s:
                continue
            raise SystemExit(f'{path}: nao encontrei {label}')
        s=s.replace(old,new,1)
    p.write_text(s,encoding='utf-8')

edit(Path('docs/driver-gamification.js'),[
    ("{title:'Meta do Mês',description:'Conclua os 30 trabalhos do mês.',unlocked:n(p?.monthly_completed)>=30},",
     "{title:'30 Entregas',description:'Complete 30 entregas ao longo da sua carreira GAT.',unlocked:n(p?.total_deliveries)>=30},",
     'conquista antiga de 30 por mes')
])

edit(Path('docs/work-catalog-completion.js'),[
    ("const p=currentProfile(),rows=Array.isArray(p?.deliveries)?p.deliveries:[];",
     "const p=currentProfile(),rows=Array.isArray(p?.cargo_history)?p.cargo_history:(Array.isArray(p?.deliveries)?p.deliveries:[]);",
     'fonte mensal/recente da colecao de cargas')
])

reason_fn="""  function rankReasonText(reason){
    const map={
      client_update_required:'GAT Telemetria desatualizado',telemetry_disconnected:'Telemetria desconectada',damage_data_incomplete:'Dados de dano incompletos',
      telemetry_gap:'Interrupção da telemetria',telemetry_not_verified_from_start:'Telemetria não confirmada desde o início',trip_progress_unverified:'Progresso da viagem não confirmado',
      mission_not_active:'Início da viagem não validado',distance_below_minimum:'Distância fora do requisito de pontuação'
    };
    return map[String(reason||'')]||'Viagem sem elegibilidade para Pontos GAT';
  }

"""
edit(Path('docs/motorista-enhancements.js'),[
    ("  function enhancedRenderDeliveries(history){\n",reason_fn+"  function enhancedRenderDeliveries(history){\n",'funcao do historico melhorado'),
    ("      const hasSavedFinal=x&&Object.prototype.hasOwnProperty.call(x,'xp_awarded')&&Number.isFinite(Number(x.xp_awarded));\n      const finalXp=hasSavedFinal?Math.max(0,Number(x.xp_awarded)):Math.max(0,base-totalPenalty);",
     "      const hasSavedFinal=x&&Object.prototype.hasOwnProperty.call(x,'xp_awarded')&&Number.isFinite(Number(x.xp_awarded));\n      const finalXp=hasSavedFinal?Math.max(0,Number(x.xp_awarded)):Math.max(0,base);\n      const rankFlag=x?.ranking_eligible??x?.rank_eligible,rankEligible=rankFlag===undefined?true:(rankFlag!==false&&rankFlag!==0);\n      const gatPoints=(x&&Object.prototype.hasOwnProperty.call(x,'gat_points')&&Number.isFinite(Number(x.gat_points)))?Math.max(0,Number(x.gat_points)):(rankEligible?Math.max(0,100-totalPenalty):0);",
     'calculo visual antigo de XP com penalidade'),
    ("      let chips=`<span class=\"gat-xp-chip\"><span>XP BASE</span><b>${fmt(base)}</b></span>`;\n      if(speedPenalty>0||fines>0)chips+=`<span class=\"gat-xp-chip penalty\"><span>VELOCIDADE${fines?' • '+fines+' multa'+(fines===1?'':'s'):''}</span><b>-${fmt(speedPenalty)} XP</b></span>`;\n      if(cargoPenalty>0||cargoDamage>0)chips+=`<span class=\"gat-xp-chip ${cargoPenalty>0?'penalty':''}\"><span>CARGA • ${fmtPct(cargoDamage)}</span><b>${cargoPenalty>0?'-'+fmt(cargoPenalty)+' XP':'0 XP'}</b></span>`;\n      if(truckPenalty>0||truckDamage>0)chips+=`<span class=\"gat-xp-chip ${truckPenalty>0?'penalty':''}\"><span>CAMINHÃO • +${fmtPct(truckDamage)}</span><b>${truckPenalty>0?'-'+fmt(truckPenalty)+' XP':'0 XP'}</b></span>`;\n      if(totalPenalty===0)chips+=`<span class=\"gat-xp-chip clean\"><b>SEM PENALIDADES</b></span>`;\n      chips+=`<span class=\"gat-xp-chip final\"><span>XP FINAL</span><b>${fmt(finalXp)}</b></span>`;",
     "      let chips=`<span class=\"gat-xp-chip final\"><span>XP DA VIAGEM</span><b>${fmt(finalXp)}</b></span>`;\n      if(rankEligible){\n        chips+=`<span class=\"gat-xp-chip ${gatPoints>0?'clean':'penalty'}\"><span>PONTOS GAT</span><b>${fmt(gatPoints)}</b></span>`;\n        if(speedPenalty>0||fines>0)chips+=`<span class=\"gat-xp-chip penalty\"><span>VELOCIDADE${fines?' • '+fines+' multa'+(fines===1?'':'s'):''}</span><b>-${fmt(speedPenalty)} PONTOS</b></span>`;\n        if(cargoPenalty>0||cargoDamage>0)chips+=`<span class=\"gat-xp-chip ${cargoPenalty>0?'penalty':''}\"><span>CARGA • ${fmtPct(cargoDamage)}</span><b>${cargoPenalty>0?'-'+fmt(cargoPenalty)+' PONTOS':'OK'}</b></span>`;\n        if(truckPenalty>0||truckDamage>0)chips+=`<span class=\"gat-xp-chip ${truckPenalty>0?'penalty':''}\"><span>CAMINHÃO • +${fmtPct(truckDamage)}</span><b>${truckPenalty>0?'-'+fmt(truckPenalty)+' PONTOS':'OK'}</b></span>`;\n      }else{\n        chips+=`<span class=\"gat-xp-chip penalty\"><span>PONTOS GAT</span><b>0 • ${esc(rankReasonText(x?.ranking_reason))}</b></span>`;\n      }",
     'chips antigos que descontavam XP')
])

core_reason="""function deliveryRankReason(reason){const map={client_update_required:'Telemetria desatualizada',telemetry_disconnected:'Telemetria desconectada',damage_data_incomplete:'Dados de dano incompletos',telemetry_gap:'Interrupção da telemetria',telemetry_not_verified_from_start:'Telemetria não confirmada desde o início',trip_progress_unverified:'Progresso não confirmado',mission_not_active:'Início não validado',distance_below_minimum:'Distância fora do requisito'};return map[String(reason||'')]||'Sem elegibilidade para Pontos GAT'}
"""
old_render="""function renderDeliveries(history){if(typeof window.GATEnhancedDeliveryRenderer==='function')return window.GATEnhancedDeliveryRenderer(history);const list=Array.isArray(history)?history:[];setText('deliveriesCount',list.length+' REGISTRADAS');const rows=q('deliveryRows');if(!rows)return;rows.textContent='';if(!list.length){const empty=document.createElement('div');empty.className='delivery-empty';empty.textContent='Nenhuma carga GAT concluída ainda.';rows.appendChild(empty);return}[...list].reverse().forEach(x=>{const r=document.createElement('div');r.className='delivery-row';const vals=[(x.source||'?')+' → '+(x.destination||'?'),x.cargo||'Carga',kg(x.weight_kg),km(x.distance_km),'#'+(x.sequence||'—'),'CONCLUÍDA'];vals.forEach((v,i)=>{const s=document.createElement('span');if(i===0){const b=document.createElement('b');b.textContent=v;s.appendChild(b)}else s.textContent=v;if(i===5)s.className='done';r.appendChild(s)});rows.appendChild(r)})}
"""
new_render="""function renderDeliveries(history){if(typeof window.GATEnhancedDeliveryRenderer==='function')return window.GATEnhancedDeliveryRenderer(history);const list=Array.isArray(history)?history:[];setText('deliveriesCount',list.length+' REGISTRADAS');const rows=q('deliveryRows');if(!rows)return;rows.textContent='';if(!list.length){const empty=document.createElement('div');empty.className='delivery-empty';empty.textContent='Nenhuma carga GAT concluída ainda.';rows.appendChild(empty);return}[...list].reverse().forEach(x=>{const r=document.createElement('div');r.className='delivery-row';const distance=Math.max(0,num(x?.distance_km)),xp=(x&&Number.isFinite(Number(x.xp_awarded)))?Math.max(0,Number(x.xp_awarded)):Math.floor(distance/100)*20,rankFlag=x?.ranking_eligible??x?.rank_eligible,rankEligible=rankFlag===undefined?true:(rankFlag!==false&&rankFlag!==0),gat=(x&&Number.isFinite(Number(x.gat_points)))?Math.max(0,Number(x.gat_points)):(rankEligible?Math.max(0,100-num(x?.penalty_xp)):0),vals=[(x.source||'?')+' → '+(x.destination||'?'),x.cargo||'Carga',kg(x.weight_kg),km(distance),'#'+(x.sequence||'—')];vals.forEach((v,i)=>{const s=document.createElement('span');if(i===0){const b=document.createElement('b');b.textContent=v;s.appendChild(b)}else s.textContent=v;r.appendChild(s)});const status=document.createElement('span');status.className='done';status.textContent='CONCLUÍDA';const score=document.createElement('b');score.style.cssText='display:block;margin-top:3px;font-size:9px;color:#75baff';score.textContent=xp.toLocaleString('pt-BR')+' XP • '+gat.toLocaleString('pt-BR')+' Pontos GAT';status.appendChild(score);if(!rankEligible){const why=document.createElement('small');why.style.cssText='display:block;margin-top:2px;color:#c58b99;font-size:8px';why.textContent=deliveryRankReason(x?.ranking_reason);status.appendChild(why)}r.appendChild(status);rows.appendChild(r)})}
"""
edit(Path('docs/motorista-core.js'),[
    ("function renderProfile(){if(!profile){renderProfilePlaceholder();return}const p=profile,user=cleanUser(p.user||key),name=prettyUser(user),collection=cargoCollectionCount(p.deliveries);",
     "function renderProfile(){if(!profile){renderProfilePlaceholder();return}const p=profile,user=cleanUser(p.user||key),name=prettyUser(user),collection=cargoCollectionCount(Array.isArray(p.cargo_history)?p.cargo_history:p.deliveries);",
     'colecao limitada ao historico visivel'),
    ("function renderDeliveries(history){",core_reason+"function renderDeliveries(history){",'inicio do renderer simples'),
    (old_render.replace("function renderDeliveries(history){",core_reason+"function renderDeliveries(history){",1),core_reason+new_render,'renderer simples sem XP/Pontos GAT')
])

edit(Path('docs/work-catalog.js'),[
    ("const min=document.getElementById('workMinKm');if(min)min.textContent='500 km reais';",
     "const min=document.getElementById('workMinKm');if(min)min.textContent='Sem distância mínima';",
     'distancia minima antiga no catalogo'),
    ("const owner=document.getElementById('workOwnerMessage');if(owner)owner.textContent='A carga é reconhecida pela telemetria. A meta mensal continua sendo 30 viagens válidas, mas os tipos de carga não ficam limitados a 30 categorias.';",
     "const owner=document.getElementById('workOwnerMessage');if(owner)owner.textContent='A carga é reconhecida pela telemetria. Toda entrega concluída entra no histórico; a validação de Pontos GAT acontece separadamente.';",
     'texto da meta mensal no catalogo'),
    ("const rule=head?.querySelector('.catalog-rule');if(rule)rule.innerHTML='<b>30</b><span>VIAGENS / MÊS</span><b>500 km</b><span>REAIS MÍN.</span>';",
     "const rule=head?.querySelector('.catalog-rule');if(rule)rule.innerHTML='<b>HISTÓRICO</b><span>PERMANENTE</span><b>XP</b><span>POR KM</span>';",
     'regra visual 30 por mes'),
    ("const progressTitle=document.querySelector('.monthly-progress-card h2');if(progressTitle)progressTitle.textContent='30 viagens';",
     "const progressTitle=document.querySelector('.monthly-progress-card h2');if(progressTitle)progressTitle.textContent='Coleção de cargas';",
     'titulo 30 viagens'),
    ("const progressLead=document.querySelector('.monthly-progress-card .lead');if(progressLead)progressLead.textContent='A meta mensal continua em 30 viagens válidas. O catálogo em português é apenas visual e não altera o nome recebido do jogo.';",
     "const progressLead=document.querySelector('.monthly-progress-card .lead');if(progressLead)progressLead.textContent='Cada carga diferente concluída entra uma vez na coleção. Repetições continuam registradas normalmente no histórico.';",
     'lead da meta mensal')
])

edit(Path('docs/motorista.html'),[
    ('<article><small>PONTOS</small><b id="statPoints">—</b></article>','<article><small>PONTOS GAT</small><b id="statPoints">—</b></article>','rotulo pontos'),
    ('São considerados blocos completos de 100 km efetivamente dirigidos em uma entrega GAT válida.','São considerados blocos completos de 100 km registrados em uma entrega concluída. A validação dos Pontos GAT é separada do XP.','texto de XP condicionado'),
    ('<span class="eyebrow">META PRINCIPAL GAT</span><h2>Coleção de cargas</h2>','<span class="eyebrow">CARREIRA GAT</span><h2>Coleção de cargas</h2>','meta principal visual'),
    ('Pegue qualquer carga no ETS2 e cumpra as regras da viagem. A Central GAT identifica automaticamente em qual dos 30 trabalhos a carga se encaixa. Se o nome ainda não for conhecido, a entrega fica salva para classificação do Admin ou Moderador.','Pegue qualquer carga no ETS2. A Central GAT identifica a carga automaticamente. Se o nome ainda não for conhecido, a entrega continua salva no histórico e pode ser classificada depois.','30 trabalhos no texto de trabalho'),
    ('CATÁLOGO GAT • 30 CATEGORIAS','CARGAS OFICIAIS ETS2','30 categorias no cabecalho'),
    ('Cada entrega válida é classificada automaticamente; cargas ainda desconhecidas ficam aguardando classificação da equipe GAT sem perder a viagem.','Cada entrega concluída é registrada primeiro; a classificação da carga e a validação dos Pontos GAT acontecem separadamente.','entrega valida condicionando catalogo'),
    ('motorista-core.js?v=3','motorista-core.js?v=4','cache motorista core'),
    ('work-catalog.js?v=pt-catalog-7','work-catalog.js?v=pt-catalog-8','cache work catalog'),
    ('work-catalog-completion.js?v=3','work-catalog-completion.js?v=4','cache completion'),
    ('work-rule-final.js?v=2','work-rule-final.js?v=3','cache regra final')
])

# Protecoes contra regressao visual da filosofia nova.
for rel in ['docs/driver-gamification.js','docs/motorista-enhancements.js','docs/work-catalog-completion.js','docs/motorista-core.js','docs/work-catalog.js','docs/motorista.html']:
    s=(root/rel).read_text(encoding='utf-8')
    for forbidden in ['Conclua os 30 trabalhos do mês','Meta do Mês','30 viagens válidas','30 CATEGORIAS','30 trabalhos a carga']:
        if forbidden in s:
            raise SystemExit(rel+': regra mensal antiga ainda presente: '+forbidden)
if 'cargo_history' not in (root/'docs/work-catalog-completion.js').read_text(encoding='utf-8') or 'cargo_history' not in (root/'docs/motorista-core.js').read_text(encoding='utf-8'):
    raise SystemExit('colecao nao usa historico de carreira')
if 'ranking_reason' not in (root/'docs/motorista-core.js').read_text(encoding='utf-8'):
    raise SystemExit('historico principal nao mostra decisao da Central')
print('Site 1.0.49: historico, XP, Pontos GAT e colecao separados visualmente.')
