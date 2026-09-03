from pathlib import Path

p = Path('docs/motorista.html')
s = p.read_text(encoding='utf-8')
css = '<link rel="stylesheet" href="profile-avatar.css?v=1">'
js = '<script src="profile-avatar.js?v=1"></script>'
enhancements = '<script src="motorista-enhancements.js?v=5"></script>'
directory_fix = '<script src="driver-directory-fix.js?v=1"></script>'
if css not in s:
    s = s.replace('</head>', css + '</head>', 1)
for asset in (js, enhancements, directory_fix):
    if asset not in s:
        s = s.replace('</body>', asset + '</body>', 1)
p.write_text(s, encoding='utf-8')
print('profile avatar and driver directory assets linked')
