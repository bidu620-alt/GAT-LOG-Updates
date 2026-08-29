from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.24"' in c:
    c=c.replace('InternalVersion = "1.0.24"','InternalVersion = "1.0.25"',1)
elif 'InternalVersion = "1.0.25"' not in c:
    raise SystemExit('versao 1.0.24 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# 30 trabalhos por mes. Cada motorista escolhe a ordem e varios podem escolher o mesmo trabalho.
s=s.replace('"monthly_goal": 40','"monthly_goal": 30')
s=s.replace('"goal": 40','"goal": 30')
s=s.replace('p.MonthlyCompleted >= 40','p.MonthlyCompleted >= 30')
s=s.replace('q.MonthlyCompleted > 40','q.MonthlyCompleted > 30')

# Catalogo e metadados persistidos na missao/entrega.
old='''type gatMission struct {
\tID          string  `json:"id"`
\tMonth       string  `json:"month"`
\tSequence    int     `json:"sequence"`
'''
new='''type gatMission struct {
\tID          string  `json:"id"`
\tMonth       string  `json:"month"`
\tSequence    int     `json:"sequence"`
\tCatalogID   string  `json:"catalog_id,omitempty"`
\tTitle       string  `json:"title,omitempty"`
\tCategory    string  `json:"category,omitempty"`
\tCustomCargo string  `json:"custom_cargo,omitempty"`
'''
if old in s:
    s=s.replace(old,new,1)
elif 'CatalogID   string' not in s:
    raise SystemExit('gatMission patch point not found')

old='''type gatDelivery struct {
\tID          string  `json:"id"`
\tMissionID   string  `json:"mission_id"`
\tSequence    int     `json:"sequence"`
'''
new='''type gatDelivery struct {
\tID          string  `json:"id"`
\tMissionID   string  `json:"mission_id"`
\tSequence    int     `json:"sequence"`
\tCatalogID   string  `json:"catalog_id,omitempty"`
\tTitle       string  `json:"title,omitempty"`
\tCategory    string  `json:"category,omitempty"`
\tXPAwarded   int     `json:"xp_awarded,omitempty"`
'''
if old in s:
    s=s.replace(old,new,1)
elif 'XPAwarded   int' not in s:
    raise SystemExit('gatDelivery patch point not found')

catalog=r'''
type gatWorkItem struct {
\tID       string   `json:"id"`
\tPosition int      `json:"position"`
\tTitle    string   `json:"title"`
\tCategory string   `json:"category"`
\tIcon     string   `json:"icon"`
\tKeywords []string `json:"-"`
\tCustom   bool     `json:"custom"`
}

func gatWorkCatalog() []gatWorkItem {
\treturn []gatWorkItem{
\t\t{ID:"tractor",Position:1,Title:"Trator e máquinas agrícolas",Category:"Agrícola",Icon:"🚜",Keywords:[]string{"trator","tractor","agric","colheit","harvester","combine"}},
\t\t{ID:"fuel",Position:2,Title:"Combustível",Category:"Tanque",Icon:"⛽",Keywords:[]string{"combust","fuel","diesel","petrol","gasoline","gasolina","kerosene"}},
\t\t{ID:"food",Position:3,Title:"Alimentos",Category:"Alimentos",Icon:"🥫",Keywords:[]string{"alimento","food","grocer","comida","fruta","fruit","vegetable","legume"}},
\t\t{ID:"drinks",Position:4,Title:"Bebidas",Category:"Bebidas",Icon:"🥤",Keywords:[]string{"bebida","drink","beverage","água","agua","water","juice","suco"}},
\t\t{ID:"timber",Position:5,Title:"Madeira e toras",Category:"Madeira",Icon:"🪵",Keywords:[]string{"madeira","toras","logs","timber","lumber","wood"}},
\t\t{ID:"container",Position:6,Title:"Contêiner",Category:"Contêiner",Icon:"📦",Keywords:[]string{"container","contêiner","conteiner"}},
\t\t{ID:"heavy_machine",Position:7,Title:"Máquinas pesadas",Category:"Máquinas",Icon:"🏗️",Keywords:[]string{"escavadeira","excavator","bulldozer","carregadeira","loader","heavy machine","maquina pesada","máquina pesada"}},
\t\t{ID:"vehicles",Position:8,Title:"Veículos",Category:"Automotivo",Icon:"🚗",Keywords:[]string{"veículo","veiculo","vehicle","cars","carros","automoveis","automóveis"}},
\t\t{ID:"motorcycles",Position:9,Title:"Motocicletas",Category:"Automotivo",Icon:"🏍️",Keywords:[]string{"motocic","motorcycle","motorbike","motos"}},
\t\t{ID:"chemicals",Position:10,Title:"Produtos químicos",Category:"Químicos",Icon:"⚗️",Keywords:[]string{"químic","quimic","chemical","chemicals","acid","ácido","acido"}},
\t\t{ID:"construction",Position:11,Title:"Material de construção",Category:"Construção",Icon:"🧱",Keywords:[]string{"constru","construction","cimento","cement","concrete","tijolo","brick","telha","tiles"}},
\t\t{ID:"steel",Position:12,Title:"Aço e metais",Category:"Metais",Icon:"🔩",Keywords:[]string{"aço","aco","steel","metal","bobina","coil","beams","viga"}},
\t\t{ID:"paper",Position:13,Title:"Papel e celulose",Category:"Papel",Icon:"📄",Keywords:[]string{"papel","paper","celulose","pulp"}},
\t\t{ID:"electronics",Position:14,Title:"Eletrônicos",Category:"Eletrônicos",Icon:"💻",Keywords:[]string{"eletrôn","eletron","electronic","computer","computador"}},
\t\t{ID:"furniture",Position:15,Title:"Móveis",Category:"Móveis",Icon:"🛋️",Keywords:[]string{"móveis","moveis","furniture","mobília","mobilia"}},
\t\t{ID:"glass",Position:16,Title:"Vidro",Category:"Vidro",Icon:"🪟",Keywords:[]string{"vidro","glass"}},
\t\t{ID:"pipes",Position:17,Title:"Tubos",Category:"Industrial",Icon:"🧰",Keywords:[]string{"tubos","tubo","pipes","pipe","tubes"}},
\t\t{ID:"cables",Position:18,Title:"Cabos e bobinas",Category:"Industrial",Icon:"🧵",Keywords:[]string{"cabo","cable","bobina de cabo","cable reel","reel"}},
\t\t{ID:"industrial",Position:19,Title:"Equipamento industrial",Category:"Industrial",Icon:"⚙️",Keywords:[]string{"industrial","equipamento","equipment","machine parts","peças de máquina","pecas de maquina"}},
\t\t{ID:"mining",Position:20,Title:"Minério e carvão",Category:"Mineração",Icon:"⛏️",Keywords:[]string{"minério","minerio","ore","carvão","carvao","coal"}},
\t\t{ID:"grain",Position:21,Title:"Grãos e cereais",Category:"Agrícola",Icon:"🌾",Keywords:[]string{"grão","grao","grain","trigo","wheat","milho","corn","barley","cevada"}},
\t\t{ID:"rural",Position:22,Title:"Animais e produtos rurais",Category:"Rural",Icon:"🐄",Keywords:[]string{"gado","cattle","livestock","animal","feno","hay","palha","straw"}},
\t\t{ID:"dairy",Position:23,Title:"Leite e laticínios",Category:"Alimentos",Icon:"🥛",Keywords:[]string{"leite","milk","dairy","latic"}},
\t\t{ID:"medical",Position:24,Title:"Medicamentos e material médico",Category:"Saúde",Icon:"🩺",Keywords:[]string{"medic","medical","pharma","hospital","vacina","vaccine"}},
\t\t{ID:"scrap",Position:25,Title:"Sucata e recicláveis",Category:"Reciclagem",Icon:"♻️",Keywords:[]string{"sucata","scrap","waste","resíduo","residuo","recycl"}},
\t\t{ID:"road_machine",Position:26,Title:"Máquinas rodoviárias",Category:"Máquinas",Icon:"🚧",Keywords:[]string{"paver","asphalt","rolo","roller","road machine","máquina rodoviária","maquina rodoviaria"}},
\t\t{ID:"boats",Position:27,Title:"Barcos e iates",Category:"Especial",Icon:"🛥️",Keywords:[]string{"barco","boat","iate","yacht","boats"}},
\t\t{ID:"aircraft",Position:28,Title:"Aeronaves e helicópteros",Category:"Especial",Icon:"🚁",Keywords:[]string{"helicóp","helicop","helicopter","aircraft","avião","aviao","airplane"}},
\t\t{ID:"refrigerated",Position:29,Title:"Carga refrigerada",Category:"Refrigerada",Icon:"❄️",Keywords:[]string{"refriger","frozen","congel","chilled","resfri"}},
\t\t{ID:"custom",Position:30,Title:"Carga personalizada",Category:"Livre",Icon:"✚",Custom:true},
\t}
}

func gatWorkByID(id string) (gatWorkItem, bool) {
\tid = strings.TrimSpace(strings.ToLower(id))
\tfor _, item := range gatWorkCatalog() { if item.ID == id { return item, true } }
\treturn gatWorkItem{}, false
}

func gatCargoMatch(m *gatMission, cargo string) bool {
\tif m == nil { return false }
\tc := strings.ToLower(strings.TrimSpace(cargo))
\tif c == "" { return false }
\tif strings.TrimSpace(m.CustomCargo) != "" { return strings.Contains(c, strings.ToLower(strings.TrimSpace(m.CustomCargo))) }
\titem, ok := gatWorkByID(m.CatalogID); if !ok { return false }
\tfor _, kw := range item.Keywords { if strings.Contains(c, strings.ToLower(kw)) { return true } }
\treturn false
}

func gatWorkCompletedThisMonth(p *gatDriverProgress, id string) bool {
\tif p == nil { return false }
\tfor _, d := range p.Deliveries { if d.CatalogID == id && gatDeliveryMonth(d.CompletedAt) == p.Month { return true } }
\treturn false
}

func gatXPForDistance(km float64) int {
\tif km < 0 { km = 0 }
\treturn int(km/100.0) * 20
}

func gatTotalXPFromHistory(p *gatDriverProgress) int {
\tif p == nil { return 0 }
\ttotal := 0
\tfor i := range p.Deliveries {
\t\tx := p.Deliveries[i].XPAwarded
\t\tif x <= 0 { x = gatXPForDistance(p.Deliveries[i].DistanceKm); p.Deliveries[i].XPAwarded = x }
\t\ttotal += x
\t}
\treturn total
}

func gatPublicCatalog(p *gatDriverProgress) []map[string]any {
\tout := make([]map[string]any,0,30)
\tfor _, item := range gatWorkCatalog() {
\t\tout = append(out,map[string]any{"id":item.ID,"position":item.Position,"title":item.Title,"category":item.Category,"icon":item.Icon,"custom":item.Custom,"completed":gatWorkCompletedThisMonth(p,item.ID),"min_km":500,"markets":[]string{"freight_market","cargo_market","quick_job","world_of_trucks"}})
\t}
\treturn out
}

'''
if 'type gatWorkItem struct {' not in s:
    marker='func gatMissionBand() (float64, float64) {'
    pos=s.find(marker)
    if pos<0: raise SystemExit('gatMissionBand marker not found')
    s=s[:pos]+catalog+s[pos:]

# XP passa a ser 20 a cada 100 km de entregas validas, reconstruivel pelo historico.
s=s.replace('p.XP = p.TotalDeliveries * 100','p.XP = gatTotalXPFromHistory(p)')
s=s.replace('p.XP = q.TotalDeliveries * 100','p.XP = gatTotalXPFromHistory(p)')
s=s.replace('q.TotalDeliveries*100','p.XP')
s=s.replace('p.XP = p.TotalDeliveries * 100;','p.XP = gatTotalXPFromHistory(p);')

# Perfil e ranking agora usam meta 30 e deixam claro XP por KM.
old='''\t\t"level": gatLevel(p.XP), "points": p.Points, "xp_rule_pending": false,'''
new='''\t\t"level": gatLevel(p.XP), "points": p.Points, "xp_rule_pending": false, "xp_per_100_km": 20,'''
if old in s: s=s.replace(old,new,1)

# Rotas do catalogo/selecionar trabalho.
route='\tm.HandleFunc("/api/public/work/catalog", a.publicWorkCatalog)\n\tm.HandleFunc("/api/site/work/select", a.siteWorkSelect)\n'
if '/api/public/work/catalog' not in s:
    needle='\tm.HandleFunc("/api/public/ranking", a.publicRanking)\n'
    if needle not in s: raise SystemExit('ranking route not found')
    s=s.replace(needle,needle+route,1)

support=r'''
func (a *agent) publicWorkCatalog(w http.ResponseWriter, r *http.Request) {
\tif gatAccountCors(w,r) { return }
\tif r.Method != http.MethodGet { jsonOut(w,405,map[string]any{"ok":false,"error":"method_not_allowed"}); return }
\tuser := strings.TrimSpace(r.URL.Query().Get("user"))
\tgatProgressMu.Lock(); defer gatProgressMu.Unlock()
\tvar p *gatDriverProgress
\tif user != "" { all:=loadGatProgress(); p=ensureGatProgress(all,user); _=saveGatProgress(all) }
\tjsonOut(w,200,map[string]any{"ok":true,"monthly_goal":30,"min_km":500,"xp_per_100_km":20,"catalog":gatPublicCatalog(p)})
}

type gatSiteWorkSelectRequest struct { Token string `json:"token"`; WorkID string `json:"work_id"`; CustomCargo string `json:"custom_cargo,omitempty"` }

func (a *agent) siteWorkSelect(w http.ResponseWriter, r *http.Request) {
\tif gatAccountCors(w,r) { return }
\tif r.Method != http.MethodPost { jsonOut(w,405,map[string]any{"ok":false,"error":"method_not_allowed"}); return }
\tvar q gatSiteWorkSelectRequest
\tif decode(r,&q)!=nil || strings.TrimSpace(q.Token)=="" { jsonOut(w,400,map[string]any{"ok":false,"error":"bad_request"}); return }
\tuser,ok:=verifyLocalDriverAccountToken(strings.TrimSpace(q.Token)); if !ok { jsonOut(w,401,map[string]any{"ok":false,"error":"unauthorized"}); return }
\titem,ok:=gatWorkByID(q.WorkID); if !ok { jsonOut(w,400,map[string]any{"ok":false,"error":"invalid_work"}); return }
\tcustom:=strings.TrimSpace(q.CustomCargo)
\tif item.Custom && len(custom)<2 { jsonOut(w,400,map[string]any{"ok":false,"error":"custom_cargo_required"}); return }
\tgatProgressMu.Lock(); defer gatProgressMu.Unlock()
\tall:=loadGatProgress(); p:=ensureGatProgress(all,user)
\tif p.MonthlyCompleted>=30 { jsonOut(w,200,map[string]any{"ok":true,"finished_month":true,"completed":p.MonthlyCompleted,"goal":30,"mission":nil}); return }
\tif p.CurrentMission!=nil { jsonOut(w,409,map[string]any{"ok":false,"error":"mission_already_active","mission":p.CurrentMission}); return }
\tif gatWorkCompletedThisMonth(p,item.ID) { jsonOut(w,409,map[string]any{"ok":false,"error":"work_already_completed"}); return }
\tnow:=time.Now().UTC(); p.CurrentMission=&gatMission{ID:fmt.Sprintf("%s-%s-%s",p.Month,accountKey(user),item.ID),Month:p.Month,Sequence:item.Position,CatalogID:item.ID,Title:item.Title,Category:item.Category,CustomCargo:custom,Market:"any",MinKm:500,MinWeightKg:0,MaxWeightKg:0,State:"assigned",AssignedAt:now.Format(time.RFC3339)}
\tp.LastOnJob=false
\tif err:=saveGatProgress(all); err!=nil { jsonOut(w,500,map[string]any{"ok":false,"error":"save_error"}); return }
\tjsonOut(w,200,map[string]any{"ok":true,"completed":p.MonthlyCompleted,"goal":30,"mission":p.CurrentMission,"catalog":gatPublicCatalog(p),"xp_per_100_km":20})
}

'''
if 'func (a *agent) publicWorkCatalog(' not in s:
    marker='func (a *agent) publicRanking('
    pos=s.find(marker)
    if pos<0: raise SystemExit('publicRanking marker not found')
    s=s[:pos]+support+s[pos:]

# O trabalho antigo gerado automaticamente deixa de ser usado. Se chamado por cliente antigo,
# devolve orientacao para escolher no catalogo, sem sortear faixa/peso.
start=s.find('func (a *agent) siteWorkTake(')
if start>=0:
    end=s.find('\nfunc ',start+5)
    if end<0: raise SystemExit('siteWorkTake end not found')
    oldfn=s[start:end]
    newfn='''func (a *agent) siteWorkTake(w http.ResponseWriter, r *http.Request) {\n\t_, user, ok := gatSiteToken(w,r); if !ok { return }\n\tgatProgressMu.Lock(); defer gatProgressMu.Unlock(); all:=loadGatProgress(); p:=ensureGatProgress(all,user); _=saveGatProgress(all)\n\tjsonOut(w,http.StatusOK,map[string]any{"ok":true,"choose_from_catalog":true,"completed":p.MonthlyCompleted,"goal":30,"mission":p.CurrentMission,"catalog":gatPublicCatalog(p),"agent_version":core.InternalVersion})\n}\n'''
    s=s[:start]+newfn+s[end:]

# A rota autenticada antiga tambem nao sorteia mais trabalho automaticamente.
start=s.find('func (a *agent) accountWorkTake(')
if start>=0:
    end=s.find('\nfunc ',start+5)
    if end<0: raise SystemExit('accountWorkTake end not found')
    oldfn=s[start:end]
    newfn='''func (a *agent) accountWorkTake(w http.ResponseWriter, r *http.Request) {\n\tif r.Method != http.MethodPost { jsonOut(w,405,map[string]any{"ok":false,"error":"method_not_allowed"}); return }\n\tuser,ok:=gatAuthUser(w,r); if !ok { return }; gatProgressMu.Lock(); defer gatProgressMu.Unlock(); all:=loadGatProgress(); p:=ensureGatProgress(all,user); _=saveGatProgress(all)\n\tjsonOut(w,200,map[string]any{"ok":true,"choose_from_catalog":true,"completed":p.MonthlyCompleted,"goal":30,"mission":p.CurrentMission,"catalog":gatPublicCatalog(p)})\n}\n'''
    s=s[:start]+newfn+s[end:]

# Trabalho atual: qualquer mercado, minimo 500 km e carga deve corresponder ao card escolhido.
old='''\t\tcanStart := onJob
\t\tif rulesEnabled { canStart = canStart && gatIsWorldOfTrucks(market) && km >= m.MinKm && mass >= m.MinWeightKg && mass <= m.MaxWeightKg && mass > 0 }
'''
new='''\t\tcanStart := onJob && km >= 500 && gatCargoMatch(m,cargo)
\t\tvalidation["distance_ok"] = km >= 500
\t\tvalidation["weight_ok"] = true
\t\tvalidation["cargo_ok"] = gatCargoMatch(m,cargo)
'''
if old in s:
    s=s.replace(old,new,1)
elif 'canStart := onJob && km >= 500 && gatCargoMatch(m,cargo)' not in s:
    raise SystemExit('canStart patch point not found')

# Nao converter missao do catalogo para a regra antiga de WoT/peso na virada oficial.
old='''\tif p.CurrentMission != nil {
\t\tif gatRulesEnabled() {
\t\t\tif p.CurrentMission.Market != "world_of_trucks" || p.CurrentMission.MinKm < 800 {
\t\t\t\tminW, maxW := gatMissionBand()
\t\t\t\tp.CurrentMission.Market = "world_of_trucks"
\t\t\t\tp.CurrentMission.MinKm = 800
\t\t\t\tp.CurrentMission.MinWeightKg = minW
\t\t\t\tp.CurrentMission.MaxWeightKg = maxW
\t\t\t\tgatClearMissionTrip(p.CurrentMission)
\t\t\t}
\t\t} else {
\t\t\tp.CurrentMission.MinKm = 0
\t\t\tp.CurrentMission.Market = "test_any"
\t\t\tp.CurrentMission.MinWeightKg = 0
\t\t\tp.CurrentMission.MaxWeightKg = 0
\t\t}
\t}
'''
new='''\tif p.CurrentMission != nil && p.CurrentMission.CatalogID != "" {
\t\tp.CurrentMission.Market = "any"
\t\tp.CurrentMission.MinKm = 500
\t\tp.CurrentMission.MinWeightKg = 0
\t\tp.CurrentMission.MaxWeightKg = 0
\t}
'''
if old in s:
    s=s.replace(old,new,1)

# Entrega recebe os metadados do card e XP por distancia.
old='''delivery := gatDelivery{ID: m.ID, MissionID: m.ID, Sequence: m.Sequence, CompletedAt: m.CompletedAt, Cargo: m.Cargo, Source: m.Source, Destination: m.Destination, WeightKg: m.WeightKg, DistanceKm: m.StartKm}'''
new='''xpNow := gatXPForDistance(m.StartKm)\n\t\t\t\tdelivery := gatDelivery{ID:m.ID,MissionID:m.ID,Sequence:m.Sequence,CatalogID:m.CatalogID,Title:m.Title,Category:m.Category,XPAwarded:xpNow,CompletedAt:m.CompletedAt,Cargo:m.Cargo,Source:m.Source,Destination:m.Destination,WeightKg:m.WeightKg,DistanceKm:m.StartKm}'''
if old in s:
    s=s.replace(old,new,1)
elif 'XPAwarded:xpNow' not in s:
    raise SystemExit('delivery completion patch point not found')

# Depois de anexar a entrega, recalcula XP pelo historico.
old='''p.TotalDeliveries++; p.XP = gatTotalXPFromHistory(p); p.TotalKm += m.StartKm; p.MonthlyKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true'''
new='''p.TotalDeliveries++; p.TotalKm += m.StartKm; p.MonthlyKm += m.StartKm; p.MonthlyCompleted++; p.XP = gatTotalXPFromHistory(p); p.CurrentMission = nil; completedNow = true'''
if old in s: s=s.replace(old,new,1)

# Resposta de telemetria informa XP recebido pela distancia.
old='''\txpAwarded := 0
\tif completedNow { xpAwarded = 100 }
'''
new='''\txpAwarded := 0
\tif completedNow && len(p.Deliveries)>0 { xpAwarded = p.Deliveries[len(p.Deliveries)-1].XPAwarded }
'''
if old in s: s=s.replace(old,new,1)

# Remocao administrativa recalcula XP e KM mensal pelo historico.
s=s.replace('if p.TotalDeliveries > 0 { p.TotalDeliveries-- }; p.XP = gatTotalXPFromHistory(p)','if p.TotalDeliveries > 0 { p.TotalDeliveries-- }; p.XP = gatTotalXPFromHistory(p)')
needle='''\t\tif removed.DistanceKm > 0 { p.TotalKm -= removed.DistanceKm; if p.TotalKm < 0 { p.TotalKm = 0 } }
'''
if needle in s and 'p.MonthlyKm = gatMonthlyKmFromHistory(p)' not in s[s.find(needle):s.find(needle)+400]:
    s=s.replace(needle,needle+'\t\tp.MonthlyKm = gatMonthlyKmFromHistory(p)\n',1)

# Validacao administrativa usa meta 30.
s=s.replace('q.TotalDeliveries < q.MonthlyCompleted','q.TotalDeliveries < q.MonthlyCompleted')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.25: catalogo 30 trabalhos, 500 km, qualquer mercado e XP por distancia')
