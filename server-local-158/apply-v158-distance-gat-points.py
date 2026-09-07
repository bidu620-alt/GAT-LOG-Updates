from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')


def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)


# GAT Server 1.0.58
# - XP permanece exatamente igual: 20 XP por bloco completo de 100 km;
# - Pontos GAT passam a ser progressivos: 10 pontos por bloco completo de 99 km;
# - nao existe mais a exigencia de 500 km para Pontos GAT;
# - abaixo de 99 km a entrega continua aceita e gera 0 Pontos GAT, sem revisao manual;
# - as penalidades existentes continuam sendo descontadas do valor-base por distancia;
# - Pontos GAT podem passar de 100 em viagens longas.

worker=once(worker,"const VERSION='1.0.57-local';","const VERSION='1.0.58-local';",'versao 1.0.57')

old_rank="rankReason=distance>=500?null:'gat_distance_below_500',rankEligible=!rankReason"
new_rank="rankReason=null,rankEligible=true"
worker=once(worker,old_rank,new_rank,'limite antigo de 500 km')

old_points="gatPoints=rankEligible?Math.max(0,100-pointPenalty):0"
new_points="gatBasePoints=Math.floor(distance/99)*10,gatPoints=rankEligible?Math.max(0,gatBasePoints-pointPenalty):0"
worker=once(worker,old_points,new_points,'pontuacao fixa de 100 pontos')

worker=once(worker,'gat_base_points:100','gat_base_points:gatBasePoints','auditoria do valor-base GAT')

old_message="ranking_message:rankReason==='gat_distance_below_500'?'Viagem registrada e XP calculado normalmente, mas Pontos GAT exigem no minimo 500 km.':'Pontos GAT calculados automaticamente pela mesma entrega aceita para XP.'"
new_message="ranking_message:'Pontos GAT calculados por distancia: 10 pontos a cada 99 km completos, menos as penalidades da viagem.'"
worker=once(worker,old_message,new_message,'mensagem da regra de 500 km')

# Mantem compatibilidade do objeto scoring e publica a nova formula para o site/painel.
old_scoring="scoring:{base_per_delivery:100}"
new_scoring="scoring:{base_per_delivery:0,distance_block_km:99,points_per_block:10}"
worker=once(worker,old_scoring,new_scoring,'metadados antigos de pontuacao')

required=[
    "const VERSION='1.0.58-local'",
    'rankReason=null,rankEligible=true',
    'gatBasePoints=Math.floor(distance/99)*10',
    'gatPoints=rankEligible?Math.max(0,gatBasePoints-pointPenalty):0',
    'gat_base_points:gatBasePoints',
    'baseXP=Math.floor(distance/100)*20',
    'distance_block_km:99',
    'points_per_block:10',
    'Pontos GAT calculados por distancia: 10 pontos a cada 99 km completos, menos as penalidades da viagem.',
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch 1.0.58 incompleto: '+marker)

for forbidden in [
    "rankReason=distance>=500?null:'gat_distance_below_500'",
    'gatPoints=rankEligible?Math.max(0,100-pointPenalty):0',
    'gat_base_points:100',
    'Pontos GAT exigem no minimo 500 km',
]:
    if forbidden in worker:
        raise SystemExit('1.0.58 ainda possui regra antiga: '+forbidden)

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.58: 10 Pontos GAT por cada 99 km completos; XP preservado; penalidades preservadas.')