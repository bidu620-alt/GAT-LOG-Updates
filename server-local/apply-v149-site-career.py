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
     "      const hasSavedFinal=x&&Object.prototype.hasOwnProperty.call(x,'xp_awarded')&&Number.isFinite(Number(x.xp_awarded));\n      const finalXp=hasSavedFinal?Math.max(0,Number(x.xp_awarded)):Math.max(0,base);\n      const rankFlag=x?.ranking_eligible??x?.rank_eligible,rankEligible=rankFlag===undefined?true:rankFlag!==false;\n      const gatPoints=(x&&Object.prototype.hasOwnProperty.call(x,'gat_points')&&Number.isFinite(Number(x.gat_points)))?Math.max(0,Number(x.gat_points)):(rankEligible?Math.max(0,100-totalPenalty):0);",
     'calculo visual antigo de XP com penalidade'),
    ("      let chips=`<span class=\"gat-xp-chip\"><span>XP BASE</span><b>${fmt(base)}</b></span>`;\n      if(speedPenalty>0||fines>0)chips+=`<span class=\"gat-xp-chip penalty\"><span>VELOCIDADE${fines?' • '+fines+' multa'+(fines===1?'':'s'):''}</span><b>-${fmt(speedPenalty)} XP</b></span>`;\n      if(cargoPenalty>0||cargoDamage>0)chips+=`<span class=\"gat-xp-chip ${cargoPenalty>0?'penalty':''}\"><span>CARGA • ${fmtPct(cargoDamage)}</span><b>${cargoPenalty>0?'-'+fmt(cargoPenalty)+' XP':'0 XP'}</b></span>`;\n      if(truckPenalty>0||truckDamage>0)chips+=`<span class=\"gat-xp-chip ${truckPenalty>0?'penalty':''}\"><span>CAMINHÃO • +${fmtPct(truckDamage)}</span><b>${truckPenalty>0?'-'+fmt(truckPenalty)+' XP':'0 XP'}</b></span>`;\n      if(totalPenalty===0)chips+=`<span class=\"gat-xp-chip clean\"><b>SEM PENALIDADES</b></span>`;\n      chips+=`<span class=\"gat-xp-chip final\"><span>XP FINAL</span><b>${fmt(finalXp)}</b></span>`;",
     "      let chips=`<span class=\"gat-xp-chip final\"><span>XP DA VIAGEM</span><b>${fmt(finalXp)}</b></span>`;\n      if(rankEligible){\n        chips+=`<span class=\"gat-xp-chip ${gatPoints>0?'clean':'penalty'}\"><span>PONTOS GAT</span><b>${fmt(gatPoints)}</b></span>`;\n        if(speedPenalty>0||fines>0)chips+=`<span class=\"gat-xp-chip penalty\"><span>VELOCIDADE${fines?' • '+fines+' multa'+(fines===1?'':'s'):''}</span><b>-${fmt(speedPenalty)} PONTOS</b></span>`;\n        if(cargoPenalty>0||cargoDamage>0)chips+=`<span class=\"gat-xp-chip ${cargoPenalty>0?'penalty':''}\"><span>CARGA • ${fmtPct(cargoDamage)}</span><b>${cargoPenalty>0?'-'+fmt(cargoPenalty)+' PONTOS':'OK'}</b></span>`;\n        if(truckPenalty>0||truckDamage>0)chips+=`<span class=\"gat-xp-chip ${truckPenalty>0?'penalty':''}\"><span>CAMINHÃO • +${fmtPct(truckDamage)}</span><b>${truckPenalty>0?'-'+fmt(truckPenalty)+' PONTOS':'OK'}</b></span>`;\n      }else{\n        chips+=`<span class=\"gat-xp-chip penalty\"><span>PONTOS GAT</span><b>0 • ${esc(rankReasonText(x?.ranking_reason))}</b></span>`;\n      }",
     'chips antigos que descontavam XP')
])

# Protecoes contra regressao visual da filosofia nova.
for rel in ['docs/driver-gamification.js','docs/motorista-enhancements.js','docs/work-catalog-completion.js']:
    s=(root/rel).read_text(encoding='utf-8')
    if 'Conclua os 30 trabalhos do mês' in s or 'Meta do Mês' in s:
        raise SystemExit(rel+': meta mensal antiga ainda presente')
if 'cargo_history' not in (root/'docs/work-catalog-completion.js').read_text(encoding='utf-8'):
    raise SystemExit('catalogo nao usa historico de carreira')
if 'rankReasonText' not in (root/'docs/motorista-enhancements.js').read_text(encoding='utf-8'):
    raise SystemExit('historico nao mostra decisao da Central')
print('Site 1.0.49: historico, XP, Pontos GAT e colecao separados visualmente.')
