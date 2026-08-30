from pathlib import Path

fixed=Path(__file__).with_name('apply_134_stability_gamification_fixed.py')
if not fixed.exists():
    raise SystemExit('patch corrigido 1.0.34 nao encontrado')
exec(compile(fixed.read_text(encoding='utf-8'),str(fixed),'exec'))
