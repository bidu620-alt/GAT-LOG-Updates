from pathlib import Path
import re

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.28"' in c:
    c=c.replace('InternalVersion = "1.0.28"','InternalVersion = "1.0.29"',1)
elif 'InternalVersion = "1.0.29"' not in c:
    raise SystemExit('versao 1.0.28 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# TruckSim GPS expõe GameplayEvents persistentes por alternância (toggle).
# Guardamos o último valor visto para detectar a borda real de entrega/cancelamento.
if 'LastJobDelivered' not in s:
    m=re.search(r'(\tLastOnJob\s+bool\s+`json:"last_on_job"`\n)',s)
    if not m: raise SystemExit('campo LastOnJob nao encontrado')
    ins=(m.group(1)+
         '\tLastJobDelivered bool          `json:"last_job_delivered"`\n'+
         '\tLastJobCancelled bool          `json:"last_job_cancelled"`\n')
    s=s[:m.start()]+ins+s[m.end():]

# Calcula as bordas depois de carregar o progresso salvo do motorista.
needle='\tgatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user)\n'
if 'deliveredEvent :=' not in s:
    if needle not in s: raise SystemExit('ponto de progresso da telemetria nao encontrado')
    repl=(needle+
          '\tjobDeliveredFlag := gatTelemetryBool(tel, "gameplay.jobDelivered", "job_delivered")\n'+
          '\tjobCancelledFlag := gatTelemetryBool(tel, "gameplay.jobCancelled", "job_cancelled")\n'+
          '\tdeliveredEvent := jobDeliveredFlag != p.LastJobDelivered\n'+
          '\tcancelledEvent := jobCancelledFlag != p.LastJobCancelled\n')
    s=s.replace(needle,repl,1)

# Antes da lógica antiga de término: evento oficial entregue força a conclusão;
# evento cancelado impede que um cancelamento perto do destino seja contado.
anchor='\tif m != nil {\n'
if 'EVENTO OFICIAL DE ENTREGA' not in s:
    pos=s.find(anchor,s.find('func (a *agent) accountTelemetry'))
    if pos<0: raise SystemExit('bloco da missao na telemetria nao encontrado')
    pos_end=pos+len(anchor)
    extra=('\t\t// EVENTO OFICIAL DE ENTREGA/CANCELAMENTO DO TRUCKSIM GPS.\n'
           '\t\t// Mantém a lógica antiga como fallback, mas não depende mais de o restante chegar a <= 15 km.\n'
           '\t\tif m.State == "active" && deliveredEvent {\n'
           '\t\t\tm.LastKm = 0\n'
           '\t\t\tonJob = false\n'
           '\t\t\tp.LastOnJob = true\n'
           '\t\t} else if m.State == "active" && cancelledEvent {\n'
           '\t\t\tm.LastKm = 999999\n'
           '\t\t\tonJob = false\n'
           '\t\t\tp.LastOnJob = true\n'
           '\t\t}\n')
    s=s[:pos_end]+extra+s[pos_end:]

# Persiste os toggles para que cada evento seja processado uma única vez.
needle='\tp.LastOnJob = onJob\n'
if 'p.LastJobDelivered = jobDeliveredFlag' not in s:
    if needle not in s: raise SystemExit('atualizacao LastOnJob nao encontrada')
    repl=('\tp.LastJobDelivered = jobDeliveredFlag\n'
          '\tp.LastJobCancelled = jobCancelledFlag\n'+needle)
    s=s.replace(needle,repl,1)

# Diagnóstico devolvido ao cliente/Admin.
old='validation := map[string]any{"on_job": onJob,'
if old in s and 'job_delivered_event' not in s:
    s=s.replace(old,'validation := map[string]any{"job_delivered_event": deliveredEvent, "job_cancelled_event": cancelledEvent, "on_job": onJob,',1)

checks=['LastJobDelivered','deliveredEvent :=','EVENTO OFICIAL DE ENTREGA','p.LastJobDelivered = jobDeliveredFlag','InternalVersion = "1.0.29"']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.29 aplicado: entrega/cancelamento por GameplayEvents')
