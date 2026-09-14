from pathlib import Path

path=Path('docs/admin.html')
text=path.read_text(encoding='utf-8')

if 'href="radio-admin.html"' not in text:
    old='<a class="admin-nav active" href="admin.html">Admin</a></nav>'
    new='<a class="admin-nav active" href="admin.html">Admin</a><a href="radio-admin.html">Rádio GAT</a></nav>'
    if old not in text:
        raise SystemExit('Nao encontrei a navegacao do Painel Admin para adicionar Radio GAT.')
    text=text.replace(old,new,1)

path.write_text(text,encoding='utf-8')
print('Site GAT: link Radio GAT adicionado ao Painel Admin para o artefato publicado.')
