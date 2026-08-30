from pathlib import Path

base=Path('server-dotnet/build134/apply_134_ui.py')
if not base.exists():
    raise SystemExit('UI 1.0.34 nao encontrada')
exec(compile(base.read_text(encoding='utf-8'),str(base),'exec'))

paths=[
    Path('server-dotnet/GatLogServer/MainForm.cs'),
    Path('server-dotnet/GatLogServer/GatLogServer.csproj'),
    Path('server-dotnet/GatLogInstaller/Program.cs'),
    Path('server-dotnet/GatLogInstaller/GatLogInstaller.csproj'),
]
for p in paths:
    s=p.read_text(encoding='utf-8')
    s=s.replace('1.0.34','1.0.35')
    p.write_text(s,encoding='utf-8')

ui=paths[0].read_text(encoding='utf-8')
installer=paths[2].read_text(encoding='utf-8')
proj=paths[3].read_text(encoding='utf-8')
if 'CurrentVersion = "1.0.35"' not in ui:
    raise SystemExit('CurrentVersion 1.0.35 ausente')
if 'GAT_LOG_SERVER_DOTNET_UPDATE_1.0.35_TESTE' not in proj:
    raise SystemExit('nome do atualizador 1.0.35 ausente')
if 'para 1.0.35?' not in installer:
    raise SystemExit('mensagem do atualizador 1.0.35 ausente')
print('UI/updater 1.0.35 preparado para regras oficiais')
