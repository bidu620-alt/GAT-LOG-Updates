from pathlib import Path
import re

p = Path('native-0.1/main.go')
s = p.read_text()

if 'appVersion     = "2.0.4"' not in s:
    if 'appVersion     = "2.0.3"' not in s:
        raise SystemExit('base 2.0.3 nao encontrada')
    s = s.replace('appVersion     = "2.0.3"', 'appVersion     = "2.0.4"', 1)

if 'lastPresenceCheck' not in s:
    old = '\tlastServerProbe         time.Time\n\ttickBusy'
    new = '\tlastServerProbe         time.Time\n\tlastPresenceCheck       time.Time\n\ttickBusy'
    if old not in s:
        raise SystemExit('lastServerProbe nao encontrado')
    s = s.replace(old, new, 1)

if 'func getPlayersChecked' not in s:
    pattern = r'func getPlayers\(ep string\) \[\]string \{.*?\n\}\n\nfunc decodeServerCode'
    new = '''func getPlayersChecked(ep string) ([]string, bool) {
\tr := apiCall("GET", strings.TrimRight(ep, "/")+"/api/client/players", nil, 5*time.Second)
\tif r.Status != 200 || r.JSON == nil || !boolVal(r.JSON, "ok") {
\t\treturn nil, false
\t}
\tv, ok := r.JSON["players"].([]any)
\tif !ok {
\t\treturn []string{}, true
\t}
\tout := []string{}
\tseen := map[string]bool{}
\tfor _, x := range v {
\t\tname := strings.TrimSpace(fmt.Sprint(x))
\t\tif name != "" && !seen[name] {
\t\t\tseen[name] = true
\t\t\tout = append(out, name)
\t\t}
\t}
\treturn out, true
}

func getPlayers(ep string) []string {
\tplayers, _ := getPlayersChecked(ep)
\treturn players
}

func decodeServerCode'''
    s, n = re.subn(pattern, new, s, count=1, flags=re.S)
    if n != 1:
        raise SystemExit('getPlayers nao encontrada')

if 'func markAwaitingSession' not in s:
    old = '\trefreshServerInfoAsync()\n}\n\nfunc tickAsync()'
    new = '''\trefreshServerInfoAsync()
}

func markAwaitingSession(gatText string) {
\tmu.Lock()
\tinSession = false
\twaiting = true
\tlastAuto = time.Time{}
\tlastHeartbeat = time.Time{}
\tmu.Unlock()
\tshowSession(true)
\tif strings.TrimSpace(gatText) == "" {
\t\tgatText = "GAT LOG            ● AGUARDANDO SESSAO"
\t}
\tsetText(hGatStatus, gatText)
\tsetText(hTelStatus, "Telemetria         ● NAO ENVIANDO")
\tsetText(hCargo, "Aguardando voce entrar novamente na sessao do ETS2.")
}

func tickAsync()'''
    if old not in s:
        raise SystemExit('tickAsync nao encontrado')
    s = s.replace(old, new, 1)

old = '''\t\tinSession = true
\t\twaiting = false
\t\tlastHeartbeat = time.Time{}
\t\tmu.Unlock()'''
new = '''\t\tinSession = true
\t\twaiting = false
\t\tlastHeartbeat = time.Time{}
\t\tlastPresenceCheck = time.Time{}
\t\tmu.Unlock()'''
if new not in s:
    if old not in s:
        raise SystemExit('startDetectedSession nao encontrado')
    s = s.replace(old, new, 1)

if 'presenceDue := time.Since(lastPresenceCheck)' not in s:
    marker = '\t\ttele, e := getTelemetry()\n'
    block = '''\t\tmu.Lock()
\t\tepCheck, drvCheck := endpoint, driver
\t\tpresenceDue := time.Since(lastPresenceCheck) >= 3*time.Second
\t\tif presenceDue {
\t\t\tlastPresenceCheck = time.Now()
\t\t}
\t\tmu.Unlock()
\t\tif presenceDue && epCheck != "" && drvCheck != "" {
\t\t\tinfoCheck := getServerInfo(epCheck)
\t\t\tif infoCheck.Reachable && infoCheck.Supported {
\t\t\t\tif !infoCheck.Online {
\t\t\t\t\tmarkAwaitingSession("GAT LOG            ● SERVIDOR OFFLINE")
\t\t\t\t\treturn
\t\t\t\t}
\t\t\t\tplayersCheck, okPlayers := getPlayersChecked(epCheck)
\t\t\t\tif okPlayers && matchPlayer(drvCheck, playersCheck) == "" {
\t\t\t\t\tmarkAwaitingSession("GAT LOG            ● AGUARDANDO SESSAO")
\t\t\t\t\treturn
\t\t\t\t}
\t\t\t}
\t\t}

'''
    if marker not in s:
        raise SystemExit('ponto antes da telemetria nao encontrado')
    s = s.replace(marker, block + marker, 1)

if 'stateInfo := getServerInfo(s.Endpoint)' not in s:
    old = '''\tsettings.LastServer = s.Endpoint
\tsaveSettings()
\tif !isEts2Running() {'''
    new = '''\tsettings.LastServer = s.Endpoint
\tsaveSettings()
\tstateInfo := getServerInfo(s.Endpoint)
\tif !stateInfo.Reachable {
\t\tsetText(hGatStatus, "GAT LOG            ● SERVIDOR OFFLINE")
\t\tsetText(hTelStatus, "Telemetria         ● NAO ENVIANDO")
\t\treturn
\t}
\tif stateInfo.Supported && !stateInfo.Online {
\t\tsetText(hGatStatus, "GAT LOG            ● SERVIDOR OFFLINE")
\t\tsetText(hTelStatus, "Telemetria         ● NAO ENVIANDO")
\t\treturn
\t}
\tif stateInfo.Supported && stateInfo.Online {
\t\tsetText(hGatStatus, "GAT LOG            ● AGUARDANDO SESSAO")
\t\tsetText(hTelStatus, "Telemetria         ● NAO ENVIANDO")
\t}
\tif !isEts2Running() {'''
    if old not in s:
        raise SystemExit('tryConnect nao encontrado')
    s = s.replace(old, new, 1)

old = '''\t\t\t\t\tif isQueueableFailure(lastQ) {
\t\t\t\t\t\tq := queueOfflineTelemetry(tele)
\t\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● RECONECTANDO")'''
new = '''\t\t\t\t\tif isQueueableFailure(lastQ) {
\t\t\t\t\t\tq := queueOfflineTelemetry(tele)
\t\t\t\t\t\tif lastQ.Status == 0 {
\t\t\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● SERVIDOR OFFLINE")
\t\t\t\t\t\t} else {
\t\t\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● RECONECTANDO")
\t\t\t\t\t\t}'''
if new not in s:
    if old not in s:
        raise SystemExit('fila lastQ nao encontrada')
    s = s.replace(old, new, 1)

old = '''\t\t\t\tif isQueueableFailure(r) {
\t\t\t\t\tq := queueOfflineTelemetry(tele)
\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● RECONECTANDO")'''
new = '''\t\t\t\tif isQueueableFailure(r) {
\t\t\t\t\tq := queueOfflineTelemetry(tele)
\t\t\t\t\tif r.Status == 0 {
\t\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● SERVIDOR OFFLINE")
\t\t\t\t\t} else {
\t\t\t\t\t\tsetText(hGatStatus, "GAT LOG            ● RECONECTANDO")
\t\t\t\t\t}'''
if new not in s:
    if old not in s:
        raise SystemExit('fila live nao encontrada')
    s = s.replace(old, new, 1)

old = '''\t\tif child == hTruckStatus || child == hGatStatus || child == hTelStatus {
\t\t\tcol = rgb(116, 211, 255)
\t\t}'''
new = '''\t\tif child == hTruckStatus || child == hTelStatus {
\t\t\tcol = rgb(116, 211, 255)
\t\t}
\t\tif child == hGatStatus {
\t\t\tgatText := strings.ToUpper(getText(hGatStatus))
\t\t\tswitch {
\t\t\tcase strings.Contains(gatText, "OFFLINE"), strings.Contains(gatText, "INACESSIVEL"), strings.Contains(gatText, "SEM CONEXAO"):
\t\t\t\tcol = rgb(255, 82, 82)
\t\t\tcase strings.Contains(gatText, "AGUARDANDO"), strings.Contains(gatText, "RECONECTANDO"):
\t\t\t\tcol = rgb(255, 194, 59)
\t\t\tcase strings.Contains(gatText, "CONECTADO"):
\t\t\t\tcol = rgb(73, 232, 132)
\t\t\t}
\t\t}'''
if new not in s:
    if old not in s:
        raise SystemExit('cor hGatStatus nao encontrada')
    s = s.replace(old, new, 1)

for item in ('appVersion     = "2.0.4"', 'func getPlayersChecked', 'func markAwaitingSession', 'presenceDue := time.Since(lastPresenceCheck)', '● AGUARDANDO SESSAO'):
    if item not in s:
        raise SystemExit('validacao falhou: ' + item)

p.write_text(s)
