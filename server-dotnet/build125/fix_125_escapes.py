from pathlib import Path
p=Path('/tmp/gat-src/cmd/agent/main.go')
s=p.read_text(encoding='utf-8')

def clean_between(text,start,end):
    a=text.find(start)
    if a<0: return text
    b=text.find(end,a+len(start))
    if b<0: b=len(text)
    part=text[a:b]
    part=part.replace('\\n','\n').replace('\\t','\t')
    return text[:a]+part+text[b:]

# Rotas adicionadas pelo 1.0.25.
s=s.replace('\\tm.HandleFunc("/api/public/work/catalog", a.publicWorkCatalog)\\n\\tm.HandleFunc("/api/site/work/select", a.siteWorkSelect)\\n',
            '\tm.HandleFunc("/api/public/work/catalog", a.publicWorkCatalog)\n\tm.HandleFunc("/api/site/work/select", a.siteWorkSelect)\n')

# Blocos novos que vieram de strings de patch.
s=clean_between(s,'type gatWorkItem struct {','func gatMissionBand()')
s=clean_between(s,'func (a *agent) publicWorkCatalog','func (a *agent) publicRanking')
s=clean_between(s,'func (a *agent) siteWorkTake','func (a *agent) publicVersion')
s=clean_between(s,'func (a *agent) accountWorkTake','func (a *agent) accountTelemetry')

# Linha de criação da entrega com XP.
s=s.replace('xpNow := gatXPForDistance(m.StartKm)\\n\\t\\t\\t\\tdelivery :=',
            'xpNow := gatXPForDistance(m.StartKm)\n\t\t\t\tdelivery :=')

p.write_text(s,encoding='utf-8')
print('fix 1.0.25 escapes aplicado')
