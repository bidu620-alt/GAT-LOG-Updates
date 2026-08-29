from pathlib import Path
p=Path('/tmp/gat-src/cmd/agent/main.go')
s=p.read_text(encoding='utf-8')

if 'type gatAdminAuditEntry struct {' not in s:
    marker='func gatAdminAuditPath() string {'
    pos=s.find(marker)
    if pos<0:
        raise SystemExit('gatAdminAuditPath nao encontrado')
    defs='''var gatAdminMu sync.Mutex\n\ntype gatAdminAuditEntry struct {\n\tAt      string `json:"at"`\n\tActor   string `json:"actor"`\n\tAction  string `json:"action"`\n\tTarget  string `json:"target"`\n\tDetails string `json:"details,omitempty"`\n}\n\ntype gatAdminRequest struct {\n\tToken            string  `json:"token"`\n\tAction           string  `json:"action,omitempty"`\n\tTarget           string  `json:"target,omitempty"`\n\tPassword         string  `json:"password,omitempty"`\n\tRole             string  `json:"role,omitempty"`\n\tMonthlyCompleted int     `json:"monthly_completed,omitempty"`\n\tTotalDeliveries  int     `json:"total_deliveries,omitempty"`\n\tTotalKm          float64 `json:"total_km,omitempty"`\n\tDeliveryID       string  `json:"delivery_id,omitempty"`\n}\n\n'''
    s=s[:pos]+defs+s[pos:]
else:
    if 'MonthlyCompleted int' not in s:
        raise SystemExit('gatAdminRequest existe mas nao possui campos da ficha detalhada')

# Migra qualquer missao antiga (1.0.24 ou anterior) para o novo catalogo sem deixar
# um trabalho antigo bloquear a escolha de um card.
needle='''\tif p.CurrentMission != nil && p.CurrentMission.CatalogID != "" {\n\t\tp.CurrentMission.Market = "any"\n\t\tp.CurrentMission.MinKm = 500\n\t\tp.CurrentMission.MinWeightKg = 0\n\t\tp.CurrentMission.MaxWeightKg = 0\n\t}\n'''
replacement='''\tif p.CurrentMission != nil {\n\t\tif p.CurrentMission.CatalogID == "" {\n\t\t\tp.CurrentMission = nil\n\t\t\tp.LastOnJob = false\n\t\t} else {\n\t\t\tp.CurrentMission.Market = "any"\n\t\t\tp.CurrentMission.MinKm = 500\n\t\t\tp.CurrentMission.MinWeightKg = 0\n\t\t\tp.CurrentMission.MaxWeightKg = 0\n\t\t}\n\t}\n'''
if needle in s:
    s=s.replace(needle,replacement,1)
elif 'p.CurrentMission.CatalogID == ""' not in s:
    raise SystemExit('ponto de migracao da missao antiga nao encontrado')

p.write_text(s,encoding='utf-8')
print('fix 1.0.25 admin defs e migracao de missao antiga aplicado')
