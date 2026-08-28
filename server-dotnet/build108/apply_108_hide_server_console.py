from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'

# Keep the 1.0.7 structure and only bump the agent version.
s = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.7"' not in s:
    raise SystemExit('agent version 1.0.7 not found')
s = s.replace('InternalVersion = "1.0.7"', 'InternalVersion = "1.0.8"', 1)
core.write_text(s, encoding='utf-8')

# Hide the ETS2 dedicated server console without changing its lifecycle.
a = agent.read_text(encoding='utf-8')
needle = '\tcmd := exec.Command(c.ServerExe)\n'
if needle not in a:
    raise SystemExit('dedicated server launch command not found')
if 'cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}' not in a:
    a = a.replace(
        needle,
        needle + '\tcmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}\n',
        1,
    )

# syscall is Windows-only here and is required for HideWindow / CREATE_NO_WINDOW.
if '"syscall"' not in a:
    marker = 'import (\n'
    if marker not in a:
        raise SystemExit('agent import block not found')
    a = a.replace(marker, marker + '\t"syscall"\n', 1)

agent.write_text(a, encoding='utf-8')
