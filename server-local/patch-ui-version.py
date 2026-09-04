from pathlib import Path

TARGET_VERSION = "1.0.56"
path = Path("server-local/ui/GatLogServer/MainForm.cs")
text = path.read_text(encoding="utf-8")

old = "1.0.39"
if old not in text:
    raise SystemExit(f"Versao antiga {old} nao encontrada em {path}")

text = text.replace(old, TARGET_VERSION)
path.write_text(text, encoding="utf-8")

if old in path.read_text(encoding="utf-8"):
    raise SystemExit("Ainda existem referencias visuais para 1.0.39 no painel")

assembly = Path("server-local/ui/Properties/AssemblyInfo.cs")
a = assembly.read_text(encoding="utf-8")
for old_line,new_line in [
    ('AssemblyFileVersion("1.0.48.0")','AssemblyFileVersion("1.0.56.0")'),
    ('AssemblyInformationalVersion("1.0.48.0+jeanjc-rank-repair")','AssemblyInformationalVersion("1.0.56.0+damage-evidence-points")'),
    ('AssemblyVersion("1.0.48.0")','AssemblyVersion("1.0.56.0")'),
]:
    if old_line not in a:
        raise SystemExit(f"Versao base nao encontrada em {assembly}: {old_line}")
    a=a.replace(old_line,new_line,1)
assembly.write_text(a,encoding="utf-8")

print(f"Painel preparado para GAT Server {TARGET_VERSION}")
