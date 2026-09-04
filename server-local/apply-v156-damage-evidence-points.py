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
# GAT Server 1.0.56
# Regra de Pontos GAT:
# - se a Central JA comprovou a telemetria completa durante a viagem, uma falha
#   posterior somente nos campos de dano nao pode zerar os pontos;
# - os pontos sao calculados normalmente com os deltas de dano que ficaram
#   persistidos na missao;
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
'''
process_anchor="async function processMission(env,user,raw,t,previousAt,preflightTruckDamageReady=false){"
worker=once(worker,process_anchor,helper+'\n'+process_anchor,'processMission para inserir regra de evidencia de dano')

old_rank="rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankReason=adminTest?null:(rankGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;"
new_rank="rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankDamageAutoRecovered=damageRankCanAutoScore(m,rankGuardReason),rankEffectiveGuardReason=rankDamageAutoRecovered?null:rankGuardReason,rankReason=adminTest?null:(rankEffectiveGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;"
worker=once(worker,old_rank,new_rank,'decisao final do ranking')

old_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,ranking_message:rankReason?rankingMessage(rankReason):'',history_recorded:true"
new_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,automatic_ranking_reason:rankDamageAutoRecovered?rankGuardReason:null,ranking_recovered_from_damage_evidence:rankDamageAutoRecovered,ranking_message:rankDamageAutoRecovered?'Pontos GAT calculados automaticamente com os danos ja validados durante a viagem.':(rankReason?rankingMessage(rankReason):''),history_recorded:true"
worker=once(worker,old_audit,new_audit,'auditoria de ranking')

required=[
    "const VERSION='1.0.56-local'",
    'function damageRankCanAutoScore',
    "if(!g.verified_at)return false",
    "reason==='damage_data_incomplete'",
    "reason!=='telemetry_resume_pending'",
    "trigger==='damage_data_incomplete'",
    "trigger==='migrated_damage_data_incomplete'",
    'rankDamageAutoRecovered=damageRankCanAutoScore',
    'rankEffectiveGuardReason=rankDamageAutoRecovered?null:rankGuardReason',
    'ranking_recovered_from_damage_evidence:rankDamageAutoRecovered',
    'automatic_ranking_reason:rankDamageAutoRecovered?rankGuardReason:null',
    "'review_gat_points'",
    'gat_review_suggested_points',
    'gat_manual_review',
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch 1.0.56 incompleto: '+marker)

helper_segment=worker[worker.find('function damageRankCanAutoScore'):worker.find(process_anchor)]
for forbidden in ['telemetry_gap','telemetry_disconnected','client_update_required','local_journal_invalid','trip_progress_unverified']:
    if forbidden in helper_segment:
        raise SystemExit('1.0.56 nao pode liberar automaticamente suspeita: '+forbidden)

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.56: dano previamente validado pontua automaticamente; suspeitas continuam na revisao manual.')
