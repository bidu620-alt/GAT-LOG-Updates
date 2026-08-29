from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.25"' in c:
    c=c.replace('InternalVersion = "1.0.25"','InternalVersion = "1.0.26"',1)
elif 'InternalVersion = "1.0.26"' not in c:
    raise SystemExit('versao 1.0.25 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Cada card do catalogo funciona como uma categoria: as cargas reais do ETS2
# sao reconhecidas por aliases/subcargas em PT/EN. Sufixo * significa prefixo.
new_catalog=r'''func gatWorkCatalog() []gatWorkItem {
	return []gatWorkItem{
		{ID:"tractor",Position:1,Title:"Trator e máquinas agrícolas",Category:"Agrícola",Icon:"🚜",Keywords:[]string{
			"trator","tratores","tractor","tractors","maquina agricola","maquinas agricolas","agricultural machine","agricultural machinery","farm machine","farm machinery","agric*","colheit*","harvester","combine harvester","combine","semeadora","seeder","seed drill","cultivador","cultivator","arado","plough","plow","pulverizador","sprayer","enfardadeira","baler","ceifeira","mower","forage harvester",
		}},
		{ID:"fuel",Position:2,Title:"Combustível",Category:"Tanque",Icon:"⛽",Keywords:[]string{
			"combust*","fuel","fuel oil","diesel","biodiesel","gasolina","gasoline","petrol","querosene","kerosene","jet fuel","aviation fuel","etanol","ethanol","glp","gpl","lpg","gas liquefeito","gas liquefeito de petroleo","liquefied petroleum gas","propano","propane","butano","butane",
		}},
		{ID:"food",Position:3,Title:"Alimentos",Category:"Alimentos",Icon:"🥫",Keywords:[]string{
			"alimento*","food","foods","food products","packaged food","groceries","grocery","comida","mantimentos","fruit*","fruta*","maca","macas","apple","apples","laranja","laranjas","orange","oranges","banana","bananas","pera","peras","pear","pears","uva","uvas","grape","grapes","vegetable*","legume*","batata","batatas","potato","potatoes","cenoura","cenouras","carrot","carrots","tomate","tomates","tomato","tomatoes","cebola","cebolas","onion","onions","carne","meat","beef","pork","frango","chicken","peixe","fish","salmao","salmon","pescada","hake","polvo","octopus","farinha","flour","acucar","sugar","arroz","rice","chocolate","doces","sweets","sorvete","ice cream","frozen food","congelados","queijo","cheese","iogurte","yogurt","manteiga","butter",
		}},
		{ID:"drinks",Position:4,Title:"Bebidas",Category:"Bebidas",Icon:"🥤",Keywords:[]string{
			"bebida*","drink","drinks","beverage*","agua","water","bottled water","agua engarrafada","suco","juice","refrigerante","soft drink","soda","cerveja","beer","vinho","wine","sidra","cider","cha","tea","cafe","coffee",
		}},
		{ID:"timber",Position:5,Title:"Madeira e toras",Category:"Madeira",Icon:"🪵",Keywords:[]string{
			"madeira","toras","tora","logs","log","timber","lumber","wood","wooden beams","vigas de madeira","tree trunks","troncos","boards","planks","tabuas",
		}},
		{ID:"container",Position:6,Title:"Contêiner",Category:"Contêiner",Icon:"📦",Keywords:[]string{
			"container","containers","conteiner","conteineres","shipping container","freight container",
		}},
		{ID:"heavy_machine",Position:7,Title:"Máquinas pesadas",Category:"Máquinas",Icon:"🏗️",Keywords:[]string{
			"maquina pesada","maquinas pesadas","heavy machine","heavy machinery","construction machine","escavadeira","excavator","bulldozer","dozer","pa carregadeira","carregadeira","wheel loader","front loader","loader","retroescavadeira","backhoe","guindaste","crane","mobile crane","dumper","dump truck","haul truck","crawler","volvo l250h","l250h","volvo ec220e","ec220e","volvo ew240e","ew240e","volvo a25g","a25g",
		}},
		{ID:"vehicles",Position:8,Title:"Veículos",Category:"Automotivo",Icon:"🚗",Keywords:[]string{
			"veiculo*","vehicle*","automovel","automoveis","automobile*","carro","carros","car","cars","van","vans","caminhao","caminhoes","truck","trucks","truck chassis","chassis de caminhao","chassis","onibus","bus","buses","pickup","pick up","picape",
		}},
		{ID:"motorcycles",Position:9,Title:"Motocicletas",Category:"Automotivo",Icon:"🏍️",Keywords:[]string{
			"motocic*","motorcycle*","motorbike*","moto","motos","scooter","scooters",
		}},
		{ID:"chemicals",Position:10,Title:"Produtos químicos",Category:"Químicos",Icon:"⚗️",Keywords:[]string{
			"quimic*","chemical*","produto quimico","produtos quimicos","acid","acido","sulfuric acid","acido sulfurico","hydrochloric acid","acido cloridrico","sodium hydroxide","soda caustica","chlorine","cloro","solvent","solvente","resin","resina","fertilizer","fertilizante","adubo",
		}},
		{ID:"construction",Position:11,Title:"Material de construção",Category:"Construção",Icon:"🧱",Keywords:[]string{
			"material de construcao","construction material","building material","cimento","cement","concreto","concrete","tijolo","tijolos","brick","bricks","telha","telhas","roof tiles","areia","sand","cascalho","gravel","gesso","plaster","drywall","marmore","marble","granito","granite","prefabricated","pre fabricado","prefabricado","concrete beams","vigas de concreto",
		}},
		{ID:"steel",Position:12,Title:"Aço e metais",Category:"Metais",Icon:"🔩",Keywords:[]string{
			"aco","steel","metal","metals","metalico","bobina de metal","metal coil","steel coil","bobina de aco","chapa de aco","steel sheet","sheet metal","viga de aco","steel beam","steel beams","vergalhao","rebar","ferro","iron","cobre","copper","aluminio","aluminium","aluminum","chumbo","lead","zinco","zinc","lingote","ingot","ingots",
		}},
		{ID:"paper",Position:13,Title:"Papel e celulose",Category:"Papel",Icon:"📄",Keywords:[]string{
			"papel","paper","paper rolls","rolos de papel","celulose","pulp","cardboard","papelao","tissue","paperboard",
		}},
		{ID:"electronics",Position:14,Title:"Eletrônicos",Category:"Eletrônicos",Icon:"💻",Keywords:[]string{
			"eletron*","electronic*","computer","computers","computador","computadores","server","servers","televisao","televisor","television","tv","tvs","aparelhos eletronicos","electronic components","componentes eletronicos","mobile phones","celulares","smartphones","appliance*","eletrodomest*",
		}},
		{ID:"furniture",Position:15,Title:"Móveis",Category:"Móveis",Icon:"🛋️",Keywords:[]string{
			"movel","moveis","furniture","mobilia","table","tables","mesa","mesas","chair","chairs","cadeira","cadeiras","sofa","sofas","mattress","mattresses","colchao","colchoes","cabinet","armario","armarios",
		}},
		{ID:"glass",Position:16,Title:"Vidro",Category:"Vidro",Icon:"🪟",Keywords:[]string{
			"vidro","vidros","glass","window glass","vidro de janela","glass panels","paineis de vidro","glass sheets","chapas de vidro",
		}},
		{ID:"pipes",Position:17,Title:"Tubos",Category:"Industrial",Icon:"🧰",Keywords:[]string{
			"tubo","tubos","pipe","pipes","tube","tubes","pipeline","oleoduto","gasoduto","concrete pipes","tubos de concreto","steel pipes","tubos de aco",
		}},
		{ID:"cables",Position:18,Title:"Cabos e bobinas",Category:"Industrial",Icon:"🧵",Keywords:[]string{
			"cabo","cabos","cable","cables","bobina de cabo","bobinas de cabo","cable reel","cable reels","industrial cable reel","carretel de cabo","carreteis de cabo","wire reel","wire reels","fio industrial","fios industriais","spool","spools",
		}},
		{ID:"industrial",Position:19,Title:"Equipamento industrial",Category:"Industrial",Icon:"⚙️",Keywords:[]string{
			"industrial","equipamento industrial","industrial equipment","equipment","machine parts","pecas de maquina","machinery parts","gerador","generator","transformador","transformer","heat exchanger","trocador de calor","pressure tank","tanque de pressao","compressor","compressor unit","forklift","empilhadeira","locomotiva","locomotive",
		}},
		{ID:"mining",Position:20,Title:"Minério e carvão",Category:"Mineração",Icon:"⛏️",Keywords:[]string{
			"minerio","ore","iron ore","minerio de ferro","copper ore","minerio de cobre","bauxite","bauxita","carvao","coal","mineral","minerals","mining material","material de mineracao",
		}},
		{ID:"grain",Position:21,Title:"Grãos e cereais",Category:"Agrícola",Icon:"🌾",Keywords:[]string{
			"grao","graos","grain","grains","cereal","cereals","trigo","wheat","milho","corn","maize","cevada","barley","centeio","rye","aveia","oats","girassol","sunflower","sementes","seeds","soja","soy","soybeans","arroz","rice",
		}},
		{ID:"rural",Position:22,Title:"Animais e produtos rurais",Category:"Rural",Icon:"🐄",Keywords:[]string{
			"animal","animais","livestock","gado","cattle","vaca","vacas","cow","cows","ovelha","ovelhas","sheep","porco","porcos","pig","pigs","feno","hay","palha","straw","lã","la","wool","produtos rurais","farm products",
		}},
		{ID:"dairy",Position:23,Title:"Leite e laticínios",Category:"Alimentos",Icon:"🥛",Keywords:[]string{
			"leite","milk","latic*","dairy","queijo","cheese","iogurte","yogurt","yoghurt","manteiga","butter","creme de leite","cream","cream cheese",
		}},
		{ID:"medical",Position:24,Title:"Medicamentos e material médico",Category:"Saúde",Icon:"🩺",Keywords:[]string{
			"medicamento*","medicine*","medical","medical equipment","equipamento medico","medical supplies","material medico","pharma*","farmaceut*","hospital supplies","suprimentos hospitalares","vacina","vacinas","vaccine","vaccines","remedio","remedios",
		}},
		{ID:"scrap",Position:25,Title:"Sucata e recicláveis",Category:"Reciclagem",Icon:"♻️",Keywords:[]string{
			"sucata","scrap","metal scrap","sucata metalica","waste","residuo","residuos","garbage","lixo","recycl*","recicl*","recycled material","material reciclado","used plastics","plasticos usados","waste paper","papel usado",
		}},
		{ID:"road_machine",Position:26,Title:"Máquinas rodoviárias",Category:"Máquinas",Icon:"🚧",Keywords:[]string{
			"maquina rodoviaria","maquinas rodoviarias","road machine","road machinery","paver","asphalt paver","pavimentadora","asphalt miller","milling machine","fresadora de asfalto","rolo compactador","road roller","roller","grader","motoniveladora","compactor","compactador",
		}},
		{ID:"boats",Position:27,Title:"Barcos e iates",Category:"Especial",Icon:"🛥️",Keywords:[]string{
			"barco","barcos","boat","boats","iate","iates","yacht","yachts","sailboat","veleiro","motorboat","lancha","catamaran","catamara",
		}},
		{ID:"aircraft",Position:28,Title:"Aeronaves e helicópteros",Category:"Especial",Icon:"🚁",Keywords:[]string{
			"aeronave","aeronaves","aircraft","helicoptero","helicopteros","helicopter","helicopters","aviao","avioes","airplane","airplanes","aeroplane","aeroplanes","glider","planador",
		}},
		{ID:"refrigerated",Position:29,Title:"Carga refrigerada",Category:"Refrigerada",Icon:"❄️",Keywords:[]string{
			"refriger*","frozen","congel*","chilled","resfri*","cold storage","carga fria","frozen food","alimentos congelados","sorvete","ice cream","frozen meat","carne congelada","frozen fish","peixe congelado","fresh fish","peixe fresco","fresh meat","carne fresca","fresh fruit","frutas frescas",
		}},
		{ID:"custom",Position:30,Title:"Carga personalizada",Category:"Livre",Icon:"✚",Custom:true},
	}
}

'''
start=s.find('func gatWorkCatalog() []gatWorkItem {')
end=s.find('func gatWorkByID(', start)
if start < 0 or end < 0:
    raise SystemExit('catalogo 1.0.25 nao encontrado')
s=s[:start]+new_catalog+s[end:]

new_match=r'''func gatNormalizeCargoText(value string) string {
	s := strings.ToLower(strings.TrimSpace(value))
	r := strings.NewReplacer(
		"á","a","à","a","â","a","ã","a","ä","a",
		"é","e","è","e","ê","e","ë","e",
		"í","i","ì","i","î","i","ï","i",
		"ó","o","ò","o","ô","o","õ","o","ö","o",
		"ú","u","ù","u","û","u","ü","u",
		"ç","c","ñ","n",
		"-"," ","_"," ","/"," ","\\"," ","."," ",","," ",";"," ",":"," ","("," ",")"," ","["," ","]"," ","{"," ","}"," ","+"," ","&"," e ",
	)
	s = r.Replace(s)
	return strings.Join(strings.Fields(s), " ")
}

func gatCargoKeywordMatch(cargo, keyword string) bool {
	c := gatNormalizeCargoText(cargo)
	raw := strings.TrimSpace(keyword)
	if c == "" || raw == "" { return false }
	prefix := strings.HasSuffix(raw,"*")
	if prefix { raw = strings.TrimSuffix(raw,"*") }
	k := gatNormalizeCargoText(raw)
	if k == "" { return false }
	if prefix {
		for _, word := range strings.Fields(c) { if strings.HasPrefix(word,k) { return true } }
		return false
	}
	return strings.Contains(" "+c+" ", " "+k+" ")
}

func gatCargoMatch(m *gatMission, cargo string) bool {
	if m == nil || gatNormalizeCargoText(cargo) == "" { return false }
	if strings.TrimSpace(m.CustomCargo) != "" { return gatCargoKeywordMatch(cargo,m.CustomCargo) }
	item, ok := gatWorkByID(m.CatalogID); if !ok { return false }
	for _, kw := range item.Keywords { if gatCargoKeywordMatch(cargo,kw) { return true } }
	return false
}

'''
start=s.find('func gatCargoMatch(m *gatMission, cargo string) bool {')
end=s.find('func gatWorkCompletedThisMonth(', start)
if start < 0 or end < 0:
    raise SystemExit('gatCargoMatch 1.0.25 nao encontrado')
s=s[:start]+new_match+s[end:]

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.26: categorias de carga com aliases/subcargas PT/EN e normalizacao')
