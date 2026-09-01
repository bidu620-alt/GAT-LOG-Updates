from pathlib import Path

TARGET_VERSION = "1.0.40"
path = Path("server-local/ui/GatLogServer/MainForm.cs")
text = path.read_text(encoding="utf-8")

old = "1.0.39"
if old not in text:
    raise SystemExit(f"Versao antiga {old} nao encontrada em {path}")

text = text.replace(old, TARGET_VERSION)
path.write_text(text, encoding="utf-8")

if old in path.read_text(encoding="utf-8"):
    raise SystemExit("Ainda existem referencias visuais para 1.0.39 no painel")

print(f"Painel preparado para GAT Server {TARGET_VERSION}")
