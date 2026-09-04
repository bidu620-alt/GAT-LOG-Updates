from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# 1.0.53 - hotfix de ingestao ao vivo.
#
# Na 1.0.51, ao receber um pacote de uma missao ja ativa, o alias do gat_trip_id
# observado era gravado ANTES da linha canonica existir em open_trips. Como
# open_trip_aliases.trip_id possui FK para open_trips.trip_id, SQLite recusava o
# INSERT com FOREIGN KEY constraint failed. O host local envolve cada request em
# uma transacao; por isso o erro tambem revertia o UPDATE de telemetry_live, fazendo
# motoristas com viagem ativa aparecerem OFFLINE e podendo perder o evento final.
worker=once(worker,"const VERSION='1.0.52-local';","const VERSION='1.0.53-local';",'versao 1.0.52')

old=r"""     canonical=String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim();current.canonical_trip_id=canonical;
     if(observed){current.trip_id=observed;current.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}
     current.game_name=journeyGame(raw);current.context_key=rawContext;current.journey_fingerprint=rawFp;current.state='active';current.resumed_at=current.resumed_at||t;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(current),t,user).run();await saveOpenJourney(env,user,current,raw,t,'active');
     return{canonical,observed,fingerprint:rawFp,resumed:true};"""
new=r"""     canonical=String(current.canonical_trip_id||current.trip_id||current.job_latch_key||current.id||'').trim();current.canonical_trip_id=canonical;
     if(observed){current.trip_id=observed;current.job_latch_key=observed}
     current.game_name=journeyGame(raw);current.context_key=rawContext;current.journey_fingerprint=rawFp;current.state='active';current.resumed_at=current.resumed_at||t;
     // FK-safe: primeiro persiste a viagem canonica; somente depois cria/atualiza o alias observado.
     canonical=await saveOpenJourney(env,user,current,raw,t,'active')||canonical;
     if(observed&&canonical)await openJourneyAlias(env,user,observed,canonical,t);
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(current),t,user).run();
     return{canonical,observed,fingerprint:rawFp,resumed:true};"""
worker=once(worker,old,new,'ordem FK do alias na retomada de viagem ativa')

# Contratos: nunca voltar a criar o alias antes do pai open_trips neste caminho.
marker="canonical=await saveOpenJourney(env,user,current,raw,t,'active')||canonical;\n     if(observed&&canonical)await openJourneyAlias(env,user,observed,canonical,t);"
if marker not in worker:
    raise SystemExit('Patch v1.53 incompleto: ordem FK segura nao encontrada')
if "if(observed){current.trip_id=observed;current.job_latch_key=observed;await openJourneyAlias(env,user,observed,canonical,t)}" in worker:
    raise SystemExit('Patch v1.53 incompleto: alias antigo ainda executa antes do pai')
if "const VERSION='1.0.53-local'" not in worker:
    raise SystemExit('Patch v1.53 incompleto: versao')

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.53: ingestao ao vivo FK-safe para viagens ativas.')
