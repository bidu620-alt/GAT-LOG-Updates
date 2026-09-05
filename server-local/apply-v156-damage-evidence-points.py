from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')


def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)


# ---------------------------------------------------------------------------
# GAT Server 1.0.56 - hotfix de ranking
# Regra de Pontos GAT:
# - se a Central JA comprovou a telemetria completa durante a viagem, uma falha
#   posterior somente nos campos de dano nao pode zerar os pontos;
# - uma viagem que ficou presa em telemetry_not_verified_from_start tambem pode
#   ser liberada, mas SOMENTE quando ha prova de que a Central a viu perto do
#   inicio e confirmou progresso real depois;
# - a prova de inicio exige distancia restante inicial entre 80% e 125% da
#   distancia planejada e evidencia de telemetria de dano pronta no inicio
#   (preflight) ou pelo menos uma amostra completa dentro da janela inicial;
# - se o primeiro problema foi damage_data_incomplete, a liberacao exige o
#   preflight de danos do pacote idle anterior, evitando aprovar plugin antigo
#   que foi atualizado no meio da rota;
# - qualquer outra suspeita continua com 0 automatico e segue para a revisao
#   manual existente (owner/admin/moderator), com pontuacao sugerida pelo servidor.
# ---------------------------------------------------------------------------
worker=once(worker,"const VERSION='1.0.55-local';","const VERSION='1.0.56-local';",'versao 1.0.55')

helper=r'''function damageRankCanAutoScore(m,reason){
 const g=m?.rank_guard||{};
 if(!g.verified_at)return false;
 if(reason==='damage_data_incomplete')return true;
 if(reason!=='telemetry_resume_pending')return false;
 const trigger=String(g.resume_trigger||g.transient_invalid_reason||'');
 return trigger==='damage_data_incomplete'||trigger==='migrated_damage_data_incomplete';
}
function startupRankCanAutoScore(m,reason){
 const g=m?.rank_guard||{};
 if(reason!=='telemetry_not_verified_from_start'||g.verified_at||m?.trip_progress_confirmed!==true)return false;
 const lastInvalid=String(g.last_invalid_reason||'');
 if(lastInvalid&&lastInvalid!=='damage_data_incomplete')return false;
 const preflight=g.preflight_truck_damage_ready===true;
 const startupSample=Math.max(0,Number(g.valid_samples)||0)>=1;
 if(!preflight&&!startupSample)return false;
 if(lastInvalid==='damage_data_incomplete'&&!preflight)return false;
 const planned=Math.max(0,Number(m?.planned_distance_km)||0);
 const start=Math.max(0,Number(m?.start_remaining_km)||0,Number(m?.rbr_start_remaining_km)||0);
 if(planned<=0||start<=0)return false;
 const ratio=start/planned;
 return ratio>=0.80&&ratio<=1.25;
}
'''
process_anchor="async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){"
worker=once(worker,process_anchor,helper+'\n'+process_anchor,'processMission para inserir regras de evidencia do ranking')

old_rank="rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankReason=adminTest?null:(rankGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;"
new_rank="rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankDamageAutoRecovered=damageRankCanAutoScore(m,rankGuardReason),rankStartupAutoRecovered=startupRankCanAutoScore(m,rankGuardReason),rankEffectiveGuardReason=(rankDamageAutoRecovered||rankStartupAutoRecovered)?null:rankGuardReason,rankReason=adminTest?null:(rankEffectiveGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;"
worker=once(worker,old_rank,new_rank,'decisao final do ranking')

old_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,ranking_message:rankReason?rankingMessage(rankReason):'',history_recorded:true"
new_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,automatic_ranking_reason:(rankDamageAutoRecovered||rankStartupAutoRecovered)?rankGuardReason:null,ranking_recovered_from_damage_evidence:rankDamageAutoRecovered,ranking_recovered_from_start_evidence:rankStartupAutoRecovered,ranking_message:rankStartupAutoRecovered?'Pontos GAT calculados automaticamente: a telemetria foi comprovada perto do inicio e o progresso real da viagem foi confirmado.':(rankDamageAutoRecovered?'Pontos GAT calculados automaticamente com os danos ja validados durante a viagem.':(rankReason?rankingMessage(rankReason):'')),history_recorded:true"
worker=once(worker,old_audit,new_audit,'auditoria de ranking')

required=[
    "const VERSION='1.0.56-local'",
    'function damageRankCanAutoScore',
    'function startupRankCanAutoScore',
    "if(!g.verified_at)return false",
    "reason==='damage_data_incomplete'",
    "reason!=='telemetry_resume_pending'",
    "trigger==='damage_data_incomplete'",
    "trigger==='migrated_damage_data_incomplete'",
    "reason!=='telemetry_not_verified_from_start'",
    "lastInvalid&&lastInvalid!=='damage_data_incomplete'",
    'preflight_truck_damage_ready===true',
    'm?.trip_progress_confirmed!==true',
    'ratio>=0.80&&ratio<=1.25',
    'rankDamageAutoRecovered=damageRankCanAutoScore',
    'rankStartupAutoRecovered=startupRankCanAutoScore',
    'rankEffectiveGuardReason=(rankDamageAutoRecovered||rankStartupAutoRecovered)?null:rankGuardReason',
    'ranking_recovered_from_damage_evidence:rankDamageAutoRecovered',
    'ranking_recovered_from_start_evidence:rankStartupAutoRecovered',
    'automatic_ranking_reason:(rankDamageAutoRecovered||rankStartupAutoRecovered)?rankGuardReason:null',
    "'review_gat_points'",
    'gat_review_suggested_points',
    'gat_manual_review',
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch 1.0.56 incompleto: '+marker)

# A recuperacao por inicio so entende telemetry_not_verified_from_start. Se o
# rank_guard ja guarda outro motivo, a funcao retorna false sem abrir excecoes.
start_segment=worker[worker.find('function startupRankCanAutoScore'):worker.find(process_anchor)]
for forbidden in ['telemetry_gap','telemetry_disconnected','client_update_required','local_journal_invalid','trip_progress_unverified']:
    if forbidden in start_segment:
        raise SystemExit('1.0.56 nao pode liberar automaticamente suspeita: '+forbidden)

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.56 hotfix: dano previamente validado e inicio comprovado pontuam automaticamente; suspeitas continuam na revisao manual.')
