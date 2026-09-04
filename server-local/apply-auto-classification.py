"""Compatibilidade do build antigo: aplica a politica nova de carga livre.

O nome do arquivo foi mantido para nao quebrar instaladores/workflows antigos,
mas classificacao de carga deixou de existir como regra do GAT Server.
"""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name('apply-open-cargo.py')), run_name='__main__')
