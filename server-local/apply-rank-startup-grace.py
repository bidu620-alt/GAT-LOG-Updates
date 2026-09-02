"""Corrige a ativacao inicial do rank_guard na Central local.

O primeiro pacote de uma carga pode chegar antes de o TruckSim GPS preencher os sete
campos de dano. A missao deve ficar em confirmacao, e nao ser condenada nesse pacote.
"""
from pathlib import Path
import sys

path = Path(sys.argv[1])
worker = path.read_text(encoding='utf-8')

old = "rank_guard:{reason:adminTest?null:rankingReadiness(raw).reason},delivery_details_start:"
new = "rank_guard:adminTest?{reason:null,verified_at:t,last_sample_at:t,valid_samples:2}:{reason:'telemetry_not_verified_from_start',valid_samples:rankingReadiness(raw).eligible?1:0,startup_started_at:t,last_sample_at:t,last_invalid_reason:rankingReadiness(raw).reason||null},last_rejected_reason:undefined,last_rejected_at:undefined,delivery_details_start:"

if old not in worker:
    raise RuntimeError('Nao encontrei a ativacao do rank_guard para aplicar a janela inicial.')
worker = worker.replace(old, new, 1)

required = [
    "startup_started_at:t",
    "valid_samples:rankingReadiness(raw).eligible?1:0",
    "last_rejected_reason:undefined",
    "last_rejected_at:undefined"
]
for item in required:
    if item not in worker:
        raise RuntimeError('Patch de inicio do ranking incompleto: ' + item)

path.write_text(worker, encoding='utf-8')
print('Rank guard local: primeiro pacote transitorio nao condena a viagem; duas leituras validas confirmam.')
