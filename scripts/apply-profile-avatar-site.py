from pathlib import Path

p = Path('docs/motorista.html')
s = p.read_text(encoding='utf-8')
css = '<link rel="stylesheet" href="profile-avatar.css?v=1">'
js = '<script src="profile-avatar.js?v=1"></script>'
if css not in s:
    s = s.replace('</head>', css + '</head>', 1)
if js not in s:
    s = s.replace('</body>', js + '</body>', 1)
p.write_text(s, encoding='utf-8')
print('profile avatar site assets linked')
