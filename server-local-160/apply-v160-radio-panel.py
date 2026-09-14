from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/ui/GatLogServer')
main_path = root / 'MainForm.cs'
text = main_path.read_text(encoding='utf-8')

marker = 'GAT_RADIO_UI_V160'
if marker not in text:
    old = 'AddSideButton(panel, "CENTRAL DO SITE", "central", ref y);'
    new = old + '\n\t\t// GAT_RADIO_UI_V160\n\t\tAddSideButton(panel, "RÁDIO GAT", "radio", ref y);'
    if old not in text:
        raise SystemExit('Nao encontrei o botao CENTRAL DO SITE no MainForm.cs')
    text = text.replace(old, new, 1)

    old = 'NewPage("central").Controls.Add(new CentralPanel());'
    new = old + '\n\t\tNewPage("radio").Controls.Add(new RadioPanel());'
    if old not in text:
        raise SystemExit('Nao encontrei a pagina CENTRAL DO SITE no MainForm.cs')
    text = text.replace(old, new, 1)

main_path.write_text(text, encoding='utf-8')

required = [
    'GAT_RADIO_UI_V160',
    'AddSideButton(panel, "RÁDIO GAT", "radio", ref y);',
    'NewPage("radio").Controls.Add(new RadioPanel());',
]
for item in required:
    if item not in text:
        raise SystemExit('Patch 1.0.60 incompleto: ' + item)

print('GAT Server 1.0.60: pagina Radio GAT adicionada ao painel.')
