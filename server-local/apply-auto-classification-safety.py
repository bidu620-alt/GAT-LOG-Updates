"""Compatibilidade do build antigo.

A classificacao automatica foi removida no GAT Server 1.0.55. Este passo fica
como no-op para builds antigos que ainda chamam o arquivo pelo nome historico.
"""
from pathlib import Path
import sys

path=Path(sys.argv[1])
worker=path.read_text(encoding='utf-8')
if 'API_OPEN_CARGO_V1' not in worker:
    raise RuntimeError('Politica de carga livre nao foi aplicada antes do passo de compatibilidade.')
print('Cargo classification safety skipped: open cargo policy active:',path)
