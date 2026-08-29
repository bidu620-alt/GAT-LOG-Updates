from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.30"' in c:
    c=c.replace('InternalVersion = "1.0.30"','InternalVersion = "1.0.31"',1)
elif 'InternalVersion = "1.0.31"' not in c:
    raise SystemExit('versao 1.0.30 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

support=r'''
func gatCargoCatalogPath() string { return filepath.Join(core.DataDir(), "cargo_catalog.json") }

func loadGatCargoCatalog() map[string][]string {
	out:=map[string][]string{}
	_ = core.LoadJSON(gatCargoCatalogPath(), &out)
	if out==nil { out=map[string][]string{} }
	return out
}

func saveGatCargoCatalog(v map[string][]string) error { return core.SaveJSON(gatCargoCatalogPath(), v) }

func gatLearnCargoName(cargo string) {
	cargo=strings.TrimSpace(cargo); if cargo=="" { return }
	all:=loadGatCargoCatalog(); changed:=false
	for _,item:=range gatWorkCatalog() {
		if item.Custom { continue }
		m:=&gatMission{CatalogID:item.ID}
		if !gatCargoMatch(m,cargo) { continue }
		list:=all[item.ID]; exists:=false
		for _,old:=range list { if strings.EqualFold(strings.TrimSpace(old),cargo) { exists=true; break } }
		if !exists { all[item.ID]=append(list,cargo); changed=true }
	}
	if changed { _=saveGatCargoCatalog(all) }
}

func gatKnownCargoNames(all map[string][]string, id string) []string {
	list:=append([]string(nil),all[id]...)
	sort.Slice(list,func(i,j int) bool { return strings.ToLower(list[i])<strings.ToLower(list[j]) })
	if len(list)>40 { list=list[:40] }
	return list
}

'''
if 'func gatCargoCatalogPath() string' not in s:
    marker='func gatPublicCatalog(p *gatDriverProgress) []map[string]any {'
    pos=s.find(marker)
    if pos<0: raise SystemExit('gatPublicCatalog nao encontrado')
    s=s[:pos]+support+s[pos:]

start=s.find('func gatPublicCatalog(p *gatDriverProgress) []map[string]any {')
end=s.find('func gatMissionBand()',start)
if start<0 or end<0: raise SystemExit('bloco gatPublicCatalog nao encontrado')
new_catalog=r'''func gatPublicCatalog(p *gatDriverProgress) []map[string]any {
	out := make([]map[string]any,0,30)
	seen := loadGatCargoCatalog()
	for _, item := range gatWorkCatalog() {
		out = append(out,map[string]any{
			"id":item.ID,"position":item.Position,"title":item.Title,"category":item.Category,"icon":item.Icon,"custom":item.Custom,
			"completed":gatWorkCompletedThisMonth(p,item.ID),"min_km":250,"markets":[]string{"freight_market","cargo_market","quick_job","world_of_trucks"},
			"compatible_cargos":gatKnownCargoNames(seen,item.ID),"compat_source":"gat_telemetry",
		})
	}
	return out
}

'''
s=s[:start]+new_catalog+s[end:]

# Aprende nomes reais de carga enviados pela telemetria.
needle='\tgatProgressMu.Lock(); defer gatProgressMu.Unlock(); all := loadGatProgress(); p := ensureGatProgress(all, user)\n'
if '\tgatLearnCargoName(cargo)\n' not in s:
    pos=s.find(needle,s.find('func (a *agent) accountTelemetry('))
    if pos<0: raise SystemExit('lock da accountTelemetry nao encontrado')
    repl='\tgatProgressMu.Lock(); defer gatProgressMu.Unlock()\n\tgatLearnCargoName(cargo)\n\tall := loadGatProgress(); p := ensureGatProgress(all, user)\n'
    s=s[:pos]+repl+s[pos+len(needle):]

# O recibo final também alimenta o catálogo, caso a carga só apareça no fechamento.
trip=s.find('func (a *agent) accountTripComplete(')
if trip>=0 and 'gatLearnCargoName(q.Cargo)' not in s[trip:s.find('func (a *agent) accountTelemetry(',trip)]:
    needle2='\tgatProgressMu.Lock(); defer gatProgressMu.Unlock()\n\tall:=loadGatProgress(); p:=ensureGatProgress(all,user)\n'
    pos=s.find(needle2,trip)
    if pos<0: raise SystemExit('lock da accountTripComplete nao encontrado')
    repl2='\tgatProgressMu.Lock(); defer gatProgressMu.Unlock()\n\tgatLearnCargoName(q.Cargo)\n\tall:=loadGatProgress(); p:=ensureGatProgress(all,user)\n'
    s=s[:pos]+repl2+s[pos+len(needle2):]

checks=['InternalVersion = "1.0.31"','cargo_catalog.json','compatible_cargos','gatLearnCargoName(cargo)']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.31 aplicado: catalogo de cargas reais aprendidas pela telemetria')
