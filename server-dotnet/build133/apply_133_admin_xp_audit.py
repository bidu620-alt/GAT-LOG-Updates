from pathlib import Path
import re

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.32"' in c:
    c=c.replace('InternalVersion = "1.0.32"','InternalVersion = "1.0.33"',1)
elif 'InternalVersion = "1.0.33"' not in c:
    raise SystemExit('versao 1.0.32 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Campo usado pelo Admin para corrigir o XP final de uma entrega especifica.
if 'DeliveryXP' not in s:
    m=re.search(r'(\tDeliveryID\s+string\s+`json:"delivery_id,omitempty"`\n)',s)
    if not m:
        raise SystemExit('DeliveryID de gatAdminRequest nao encontrado')
    s=s[:m.end()]+'\tDeliveryXP       int     `json:"delivery_xp,omitempty"`\n'+s[m.end():]

# Corrige formulas antigas do painel Admin: XP sempre deve vir do historico real das entregas.
s=s.replace('p.Month = gatMonth(); p.MonthlyCompleted = q.MonthlyCompleted; p.TotalDeliveries = q.TotalDeliveries; p.TotalKm = q.TotalKm; p.XP = p.TotalDeliveries * 100',
            'p.Month = gatMonth(); p.MonthlyCompleted = q.MonthlyCompleted; p.TotalDeliveries = q.TotalDeliveries; p.TotalKm = q.TotalKm; p.XP = gatTotalXPFromHistory(p)')
s=s.replace('if p.TotalDeliveries > 0 { p.TotalDeliveries-- }; p.XP = p.TotalDeliveries * 100',
            'if p.TotalDeliveries > 0 { p.TotalDeliveries-- }; p.XP = gatTotalXPFromHistory(p)')

case=r'''\tcase "set_delivery_xp":
\t\tif actorRole == "moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
\t\tid := strings.TrimSpace(q.DeliveryID)
\t\tif id == "" { jsonOut(w,400,map[string]any{"ok":false,"error":"delivery_id_required"}); return }
\t\tif q.DeliveryXP < 0 || q.DeliveryXP > 100000 { jsonOut(w,400,map[string]any{"ok":false,"error":"invalid_delivery_xp"}); return }
\t\tgatProgressMu.Lock()
\t\tall := loadGatProgress(); p := ensureGatProgress(all, target); found := -1; oldXP := 0
\t\tfor i := range p.Deliveries {
\t\t\tif p.Deliveries[i].ID == id || strings.EqualFold(strings.TrimSpace(p.Deliveries[i].ReceiptID), id) {
\t\t\t\tfound = i; oldXP = p.Deliveries[i].XPAwarded; p.Deliveries[i].XPAwarded = q.DeliveryXP; break
\t\t\t}
\t\t}
\t\tif found < 0 { gatProgressMu.Unlock(); jsonOut(w,404,map[string]any{"ok":false,"error":"delivery_not_found"}); return }
\t\tp.XP = gatTotalXPFromHistory(p)
\t\terr := saveGatProgress(all); gatProgressMu.Unlock()
\t\tif err != nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
\t\tappendGatAdminAudit(actor, action, target, fmt.Sprintf("delivery=%s xp=%d->%d", id, oldXP, q.DeliveryXP))
'''
if 'case "set_delivery_xp":' not in s:
    marker='\tcase "delete_delivery":\n'
    if marker not in s:
        raise SystemExit('case delete_delivery nao encontrado')
    s=s.replace(marker,case+marker,1)

checks=['InternalVersion = "1.0.33"','DeliveryXP','case "set_delivery_xp":','p.XP = gatTotalXPFromHistory(p)','xp=%d->%d']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target:
        raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.33 aplicado: auditoria e correcao manual de XP por entrega')
