from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
host_path=root/'host.mjs'
worker=worker_path.read_text(encoding='utf-8')
host=host_path.read_text(encoding='utf-8')


def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)


# GAT Server 1.0.57
# - XP continua independente: 20 XP por bloco completo de 100 km;
# - toda entrega que o servidor aceita para historico/XP tambem e elegivel a
#   Pontos GAT quando a distancia final for >= 500 km;
# - Pontos GAT = 100 - penalidades ja existentes (multa, carga e caminhao);
# - diario/integridade continua registrado para diagnostico, mas nao manda uma
#   entrega aceita para revisao manual de Pontos GAT;
# - valida a assinatura ANTES de restaurar campos do pacote final;
# - pacote assinado nao passa pelo telemetry_deferred antes de persistir a cadeia;
# - limpa uma unica vez somente o estado da cadeia no servidor para recuperar
#   clientes que ficaram presos em journal_chain_gap. Historico/contas/pontos ficam.

worker=once(worker,"const VERSION='1.0.56-local';","const VERSION='1.0.57-local';",'versao 1.0.56')

old_rank="const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr,distance=Math.max(0,rbr?(Number(m.rbr_start_remaining_km)||Number(m.planned_distance_km)||teleKm||0):(Number(details.distanceKm)||Number(m.planned_distance_km)||baseKm||0)),rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankDamageAutoRecovered=damageRankCanAutoScore(m,rankGuardReason),rankStartupAutoRecovered=startupRankCanAutoScore(m,rankGuardReason),rankEffectiveGuardReason=(rankDamageAutoRecovered||rankStartupAutoRecovered)?null:rankGuardReason,rankReason=adminTest?null:(rankEffectiveGuardReason||(m.trip_progress_confirmed===true?null:'trip_progress_unverified')||(m.state==='active'?null:'mission_not_active')||(distance<minKm?'distance_below_minimum':null)),rankEligible=!rankReason;"
new_rank="const details=pick(raw,'gameplay.jobDeliveredDetails','jobDeliveredDetails')||{},rbr=clean(m.map_mode).includes('rbr')||isRbr,distance=Math.max(0,rbr?(Number(m.rbr_start_remaining_km)||Number(m.planned_distance_km)||teleKm||0):(Number(details.distanceKm)||Number(m.planned_distance_km)||baseKm||0)),rankGuardReason=m.rank_guard?.reason||(!m.rank_guard?'telemetry_not_verified_from_start':null),rankReason=distance>=500?null:'gat_distance_below_500',rankEligible=!rankReason;"
worker=once(worker,old_rank,new_rank,'decisao final de Pontos GAT')

old_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,automatic_ranking_reason:(rankDamageAutoRecovered||rankStartupAutoRecovered)?rankGuardReason:null,ranking_recovered_from_damage_evidence:rankDamageAutoRecovered,ranking_recovered_from_start_evidence:rankStartupAutoRecovered,ranking_message:rankStartupAutoRecovered?'Pontos GAT calculados automaticamente: a telemetria foi comprovada perto do inicio e o progresso real da viagem foi confirmado.':(rankDamageAutoRecovered?'Pontos GAT calculados automaticamente com os danos ja validados durante a viagem.':(rankReason?rankingMessage(rankReason):'')),history_recorded:true"
new_audit="rank_verified:rankEligible,rank_eligible:rankEligible,ranking_eligible:rankEligible,ranking_reason:rankReason,automatic_ranking_reason:null,ranking_integrity_reason:rankGuardReason||null,ranking_recovered_from_damage_evidence:false,ranking_recovered_from_start_evidence:false,ranking_message:rankReason==='gat_distance_below_500'?'Viagem registrada e XP calculado normalmente, mas Pontos GAT exigem no minimo 500 km.':'Pontos GAT calculados automaticamente pela mesma entrega aceita para XP.',history_recorded:true"
worker=once(worker,old_audit,new_audit,'auditoria simplificada de Pontos GAT')

# Valida o pacote original assinado antes de qualquer restauracao interna.
restore_anchor="   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);\n   const readiness=rankingReadiness(raw);"
restore_new="   const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);\n   if(packetState.journal_present&&!packetState.journal_verified)raw.gat_journal_invalid=true;\n   restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);\n   const readiness=rankingReadiness(raw);"
worker=once(worker,restore_anchor,restore_new,'validacao antes da restauracao de entrega')

late_packet="   const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);\n   if(packetState.journal_present&&!packetState.journal_verified)raw.gat_journal_invalid=true;\n   let openJourneyState=null,missionEvent=null;"
worker=once(worker,late_packet,"   let openJourneyState=null,missionEvent=null;",'remocao da validacao tardia duplicada')

old_defer="if(!queuedTimeOK&&!delivered&&!cancelled&&prevRaw&&Date.parse(receivedAt)-Date.parse(previous.updated_at)<15000&&signature(raw)===signature(prevRaw))"
new_defer="if(!packetState.journal_present&&!queuedTimeOK&&!delivered&&!cancelled&&prevRaw&&Date.parse(receivedAt)-Date.parse(previous.updated_at)<15000&&signature(raw)===signature(prevRaw))"
worker=once(worker,old_defer,new_defer,'telemetry_deferred para pacotes assinados')

# Recupera uma unica vez a cadeia que ficou quebrada nas versoes 1.0.54-1.0.56.
helper=r'''
function resetJournalStateV157(db){
  const key='journal_state_reset_v157_direct_points';
  if(db.sql.prepare('SELECT value FROM meta WHERE key=?').get(key))return;
  const t=new Date().toISOString();
  db.sql.exec('BEGIN IMMEDIATE');
  try{
    db.sql.prepare('DELETE FROM telemetry_journal_state').run();
    db.sql.prepare("INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)").run(key,t);
    db.sql.exec('COMMIT');
  }catch(e){try{db.sql.exec('ROLLBACK')}catch{}throw e;}
}
'''
host=once(host,'async function main(){',helper+'\nasync function main(){','helper de reset do journal')
host=once(host,"const db=new LocalDatabase(path);validateDatabase(db.sql);ensureAutomaticCargoCatalog(db);","const db=new LocalDatabase(path);validateDatabase(db.sql);ensureAutomaticCargoCatalog(db);resetJournalStateV157(db);",'ativacao do reset do journal')

required_worker=[
    "const VERSION='1.0.57-local'",
    "rankReason=distance>=500?null:'gat_distance_below_500'",
    "gatPoints=rankEligible?Math.max(0,100-pointPenalty):0",
    "baseXP=Math.floor(distance/100)*20",
    "ranking_integrity_reason:rankGuardReason||null",
    "Pontos GAT calculados automaticamente pela mesma entrega aceita para XP.",
    "if(!packetState.journal_present&&!queuedTimeOK",
    "const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);",
    'restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);',
]
for marker in required_worker:
    if marker not in worker:raise SystemExit('Patch 1.0.57 incompleto no worker: '+marker)

call=worker.index("const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);")
restore=worker.index('restoreDeliveredTrailer(raw,prevRaw,previousSampleAt,t,delivered,loaded);')
if call>restore:raise SystemExit('1.0.57 precisa validar journal antes de restaurar o pacote')
if worker.count("const packetState=await inspectClientPacket(env,account,device,String(b.token||''),raw,t);")!=1:
    raise SystemExit('1.0.57 encontrou quantidade inesperada de validacoes do pacote')

for forbidden in [
    'rankEffectiveGuardReason=(rankDamageAutoRecovered||rankStartupAutoRecovered)?null:rankGuardReason',
    "rankReason=adminTest?null:(rankEffectiveGuardReason",
]:
    if forbidden in worker:raise SystemExit('1.0.57 ainda possui trava antiga de Pontos GAT: '+forbidden)

for marker in ['function resetJournalStateV157','journal_state_reset_v157_direct_points','DELETE FROM telemetry_journal_state','resetJournalStateV157(db);']:
    if marker not in host:raise SystemExit('Patch 1.0.57 incompleto no host: '+marker)

worker_path.write_text(worker,encoding='utf-8')
host_path.write_text(host,encoding='utf-8')
print('GAT Server 1.0.57: XP normal; Pontos GAT diretos em viagens de 500 km ou mais; journal corrigido.')
