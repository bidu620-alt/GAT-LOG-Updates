from pathlib import Path

p = Path('native-0.1/main.go')
s = p.read_text()

if 'appVersion     = "2.0.5"' not in s:
    if 'appVersion     = "2.0.4"' not in s:
        raise SystemExit('base 2.0.4 nao encontrada')
    s = s.replace('appVersion     = "2.0.4"', 'appVersion     = "2.0.5"', 1)

old = '''\t\t\t\t\tcase "not_in_server", "invalid_session", "session_expired":
\t\t\t\t\t\tmu.Lock()
\t\t\t\t\t\tinSession = false
\t\t\t\t\t\twaiting = true
\t\t\t\t\t\tlastAuto = time.Time{}
\t\t\t\t\t\tmu.Unlock()
\t\t\t\t\t\tshowSession(false)
\t\t\t\t\t\tsetText(hLoginMsg, "Sessao perdida. Reconectando automaticamente...")
\t\t\t\t\t\tsetText(hEnter, "RECONECTANDO...")'''
new = '''\t\t\t\t\tcase "not_in_server", "invalid_session", "session_expired":
\t\t\t\t\t\tmarkAwaitingSession("GAT LOG            ● AGUARDANDO SESSAO")'''
if new not in s:
    if old not in s:
        raise SystemExit('bloco not_in_server da fila nao encontrado')
    s = s.replace(old, new, 1)

old = '''\t\t\t\tif code == "not_in_server" || code == "invalid_session" || code == "session_expired" {
\t\t\t\t\tmu.Lock()
\t\t\t\t\tinSession = false
\t\t\t\t\twaiting = true
\t\t\t\t\tlastAuto = time.Time{}
\t\t\t\t\tmu.Unlock()
\t\t\t\t\tshowSession(false)
\t\t\t\t\tsetText(hLoginMsg, "Sessao perdida. Reconectando automaticamente...")
\t\t\t\t\tsetText(hEnter, "RECONECTANDO...")
\t\t\t\t\treturn
\t\t\t\t}'''
new = '''\t\t\t\tif code == "not_in_server" || code == "invalid_session" || code == "session_expired" {
\t\t\t\t\tmarkAwaitingSession("GAT LOG            ● AGUARDANDO SESSAO")
\t\t\t\t\treturn
\t\t\t\t}'''
if new not in s:
    if old not in s:
        raise SystemExit('bloco not_in_server live nao encontrado')
    s = s.replace(old, new, 1)

if 'appVersion     = "2.0.5"' not in s or s.count('markAwaitingSession("GAT LOG            ● AGUARDANDO SESSAO")') < 3:
    raise SystemExit('validacao 2.0.5 falhou')

p.write_text(s)
