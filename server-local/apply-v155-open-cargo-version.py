from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')
old="const VERSION='1.0.54-local';"
new="const VERSION='1.0.55-local';"
if old not in worker:
    raise SystemExit('Nao encontrei a versao 1.0.54-local para finalizar a 1.0.55')
worker=worker.replace(old,new,1)
for marker in ['API_OPEN_CARGO_V1',"open_cargo:true","cargo_policy:'open'","category:'Carga detectada'"]:
    if marker not in worker:
        raise SystemExit('Politica de carga livre ausente na 1.0.55: '+marker)
if 'cargo_not_compatible' in worker:
    raise SystemExit('A 1.0.55 ainda contem recusa por compatibilidade de carga')
if 'delivery_completed_pending_classification' in worker:
    raise SystemExit('A 1.0.55 ainda contem entrega pendente de classificacao')
worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.55: carga livre finalizada.')
