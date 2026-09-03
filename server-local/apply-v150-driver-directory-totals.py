from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

worker=once(worker,"const VERSION='1.0.49-local';","const VERSION='1.0.50-local';",'versao local 1.0.49')

# A lista publica de motoristas precisa carregar estatisticas de carreira reais.
# monthly_goal era uma sobra da antiga meta X/30 e nao deve mais sair pela API.
worker=once(
    worker,
    "SELECT p.user,p.monthly_completed,p.monthly_goal,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km,COALESCE(s.points,0) AS points",
    "SELECT p.user,p.monthly_completed,p.total_deliveries,p.xp,p.perfect_trips,p.penalty_xp,p.speed_fines,p.total_km,COALESCE(s.points,0) AS points",
    'campos do ranking publico'
)
worker=once(
    worker,
    "scoring:{base_per_delivery:100,max_monthly:3000}",
    "scoring:{base_per_delivery:100}",
    'limite mensal antigo do ranking'
)

# A tela administrativa tambem deixa de expor monthly_goal e passa a receber entregas totais.
worker=once(
    worker,
    "p.monthly_completed,p.monthly_goal,p.xp,p.total_km,p.current_mission_json",
    "p.monthly_completed,p.total_deliveries,p.xp,p.total_km,p.current_mission_json",
    'consulta de motoristas do admin'
)
worker=once(
    worker,
    "monthly_completed:Number(x.monthly_completed||0),monthly_goal:Number(x.monthly_goal||30),xp:Number(x.xp||0)",
    "monthly_completed:Number(x.monthly_completed||0),total_deliveries:Number(x.total_deliveries||0),xp:Number(x.xp||0)",
    'retorno de motoristas do admin'
)

ranking_start=worker.find("if(p==='/api/public/ranking'&&m==='GET')")
ranking_end=worker.find("if(p==='/api/public/safety-ranking'", ranking_start)
ranking_segment=worker[ranking_start:ranking_end]
admin_start=worker.find("if(p==='/api/site/admin/drivers'&&m==='POST')")
admin_end=worker.find("if(p==='/api/site/admin/driver'&&m==='POST')", admin_start)
admin_segment=worker[admin_start:admin_end]

required=[
    "const VERSION='1.0.50-local'",
    'p.total_deliveries',
    'p.total_km',
    'scoring:{base_per_delivery:100}',
]
for marker in required:
    if marker not in worker:
        raise SystemExit('Patch v1.50 incompleto: '+marker)
for label,segment in [('ranking publico',ranking_segment),('admin drivers',admin_segment)]:
    if 'monthly_goal' in segment:
        raise SystemExit('monthly_goal ainda exposto em '+label)
if 'max_monthly:3000' in ranking_segment:
    raise SystemExit('limite mensal antigo ainda exposto no ranking')

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.50: ranking e lista de motoristas agora expõem entregas totais e km totais reais, sem monthly_goal.')
