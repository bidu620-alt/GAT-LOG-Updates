from pathlib import Path

p = Path('/tmp/gat-src/cmd/ui/main.go')
s = p.read_text(encoding='utf-8')

old_sig = 'func checkUpdate() {'
new_sig = 'func checkUpdate(showCurrent bool) {'
if old_sig not in s:
    raise SystemExit('checkUpdate signature not found')
s = s.replace(old_sig, new_sig, 1)

old_block = '''\tif strings.TrimSpace(rv.Version) == core.InternalVersion {\n\t\tmsgbox("Você já está usando a versão "+core.InternalVersion+".", "GAT-LOG | Atualização", MB_OK|MB_ICONINFORMATION)\n\t\treturn\n\t}'''
new_block = '''\tif strings.TrimSpace(rv.Version) == core.InternalVersion {\n\t\tif showCurrent {\n\t\t\tmsgbox("Você já está usando a versão "+core.InternalVersion+".", "GAT-LOG | Atualização", MB_OK|MB_ICONINFORMATION)\n\t\t}\n\t\treturn\n\t}'''
if old_block not in s:
    raise SystemExit('current-version block not found')
s = s.replace(old_block, new_block, 1)

p.write_text(s, encoding='utf-8')
