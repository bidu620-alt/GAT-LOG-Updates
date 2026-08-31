(()=>{
  const API='https://api.gatlogets2.com.br';
  let catalog=[],official=null;
  const openCards=new Set();
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const sess=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const user=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}return clean(new URLSearchParams(location.search).get('u')||sess()?.user)};
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const CURATED={
    tractor:['Trator','Colheitadeira','Pulverizador','Enfardadeira','Implementos agrícolas','Forage Harvester','Tractors'],
    fuel:['Diesel','Gasolina / Petrol','Querosene / Kerosene','GLP / LPG','Óleo combustível / Fuel Oil','Propano / Propane'],
    food:['Alimentos embalados','Açúcar','Batatas','Tomates','Laranjas','Carne','Chocolate','Massas'],
    drinks:['Bebidas','Água engarrafada','Suco concentrado','Cerveja sem álcool','Beverages','Bottled Water'],
    timber:['Toras','Madeira serrada','Madeira embalada','Vigas de madeira','Painéis de madeira','Logs','Lumber'],
    container:['Contêineres','IBC Containers','Large Containers','Containerized Trees'],
    heavy_machine:['Escavadeira','Carregadeira','Retroescavadeira','Dumper articulado','Máquina pesada','Backhoe Loader','Articulated Hauler'],
    vehicles:['Carros','Vans','Caminhões','Ônibus','Veículos novos','Cars','Trucks'],
    motorcycles:['Motocicletas','Motos','Scooters','Motorcycles'],
    chemicals:['Produtos químicos','Ácidos','Solventes','Soda cáustica','Fertilizantes químicos','Chemicals'],
    construction:['Tijolos / Bricks','Cimento / Cement','Cascalho / Gravel','Telhas / Roof Tiles','Vigas de concreto','Blocos de concreto','Concreto','Materiais de construção'],
    steel:['Bobinas de aço','Vigas de aço','Barras de aço','Chapas metálicas','Aço inoxidável','Steel Coils','Steel Beams'],
    paper:['Papel','Rolos de papel','Celulose','Paper','Paper Rolls'],
    electronics:['Eletrônicos','Computadores','Equipamentos eletrônicos','Electronics'],
    furniture:['Móveis','Mesas','Cadeiras','Armários','Furniture'],
    glass:['Vidro','Painéis de vidro','Glass Panels','Glass'],
    pipes:['Tubos','Tubos de concreto','Tubos metálicos','Tubulações','Pipes','Concrete Pipes'],
    cables:['Cabos','Bobinas de cabo','Cabos elétricos','Cable Reels','Cables'],
    industrial:['Geradores','Transformadores','Componentes industriais','Equipamento industrial','Industrial Equipment'],
    mining:['Carvão','Minério','Coal','Ore'],
    grain:['Trigo','Cevada','Milho','Grãos','Cereais','Wheat','Barley','Corn'],
    rural:['Gado','Porcos','Ovelhas','Feno','Palha','Animais e produtos rurais'],
    dairy:['Leite','Queijo','Manteiga','Iogurte','Creme','Milk','Cheese'],
    medical:['Medicamentos','Suprimentos médicos','Material hospitalar','Vacinas','Medical Supplies'],
    scrap:['Sucata','Metal reciclado','Recicláveis','Scrap Metal','Recyclables'],
    road_machine:['Pavimentadora','Rolo compactador','Máquina rodoviária','Road Machinery'],
    boats:['Barcos','Iates','Veleiros','Boats','Yachts'],
    aircraft:['Helicóptero','Aeronave','Helicopter','Aircraft'],
    refrigerated:['Alimentos congelados','Carne congelada','Peixe congelado','Sorvete','Frozen Food','Refrigerated Cargo'],
    custom:['Carga de mod','Carga personalizada','Qualquer outra carga oficial','Carga não listada']
  };
  const OFFICIAL_DENY={food:[/locomotive/i]};
  function curatedRows(item){return (CURATED[item?.id]||[]).map(name=>({name,dlc:''}))}
  function officialRows(item){
    const rows=official?.categories?.[item?.id];
    if(!Array.isArray(rows))return [];
    const out=[],seen=new Set(),deny=OFFICIAL_DENY[item?.id]||[];
    rows.forEach(x=>{
      const name=String(typeof x==='string'?x:x?.name||'').trim();
      if(!name||deny.some(re=>re.test(name)))return;
      const k=name.toLowerCase();
      if(seen.has(k))return;
      seen.add(k);
      const dlc=String(typeof x==='string'?'':x?.dlc||'').trim();
      out.push({name,dlc});
    });
    return out;
  }
  function chips(rows,kind='official'){return rows.map(x=>'<span class="cargo-compat-chip '+kind+'" title="'+esc(x.dlc?('Origem/DLC: '+x.dlc):'Exemplo recomendado')+'">'+esc(x.name)+(x.dlc?'<small>'+esc(x.dlc)+'</small>':'')+'</span>').join('')}
  function itemFor(card){const n=card.querySelector('.cargo-number')?.textContent||'';const pos=Number(n.replace(/\D/g,''));return catalog.find(x=>Number(x.position)===pos)||null}
  function cardKey(item){return String(item?.id||item?.position||'')}
  function redraw(){document.querySelectorAll('#workCatalogGrid .cargo-compat-wrap').forEach(x=>x.remove());decorate()}
  function decorate(){
    document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(card=>{
      if(card.querySelector('.cargo-compat-wrap'))return;
      const item=itemFor(card);if(!item)return;
      const body=card.querySelector('.cargo-body');if(!body)return;
      const safe=curatedRows(item),base=officialRows(item);
      if(!safe.length&&!base.length)return;
      const wrap=document.createElement('div');wrap.className='cargo-compat-wrap';
      const id='cargoCompat'+String(item.position||item.id).replace(/\W/g,'');
      const key=cardKey(item),isOpen=openCards.has(key);
      let content='<span class="cargo-compat-source">EXEMPLOS RECOMENDADOS • '+safe.length+'</span><div class="cargo-compat-chips">'+chips(safe,'recommended')+'</div>';
      if(base.length)content+='<span class="cargo-compat-source">CATÁLOGO OFICIAL DE REFERÊNCIA • '+base.length+'</span><div class="cargo-compat-chips">'+chips(base,'official')+'</div>';
      content+='<span class="cargo-compat-source">A categoria orienta a escolha. A contagem técnica usa a viagem registrada pela Telemetria: carga detectada, peso maior que zero e distância mínima.</span>';
      wrap.innerHTML='<button type="button" class="cargo-compat-toggle" aria-expanded="'+String(isOpen)+'" aria-controls="'+id+'"><span>'+(isOpen?'OCULTAR SUGESTÕES DE CARGA':'VER SUGESTÕES DE CARGA')+'</span><span class="arrow">⌄</span></button><div class="cargo-compat-panel" id="'+id+'" '+(isOpen?'':'hidden')+'>'+content+'</div>';
      const btn=wrap.querySelector('button'),panel=wrap.querySelector('.cargo-compat-panel'),label=btn.querySelector('span:first-child');
      btn.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const open=btn.getAttribute('aria-expanded')==='true';const next=!open;btn.setAttribute('aria-expanded',String(next));panel.hidden=!next;if(label)label.textContent=next?'OCULTAR SUGESTÕES DE CARGA':'VER SUGESTÕES DE CARGA';if(next)openCards.add(key);else openCards.delete(key)});
      body.appendChild(wrap);
    });
  }
  async function loadOfficial(){
    try{
      const r=await fetch('ets2-official-cargos.json?v=5',{cache:'no-store'}),d=await r.json();
      if(r.ok&&d?.categories){official=d;redraw()}
    }catch(_){decorate()}
  }
  async function load(){
    const u=user();if(!u){decorate();return}
    try{
      const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(u),{cache:'no-store'}),d=await r.json();
      if(r.ok&&d?.ok&&Array.isArray(d.catalog)){catalog=d.catalog;decorate()}
    }catch(_){decorate()}
  }
  const mo=new MutationObserver(()=>decorate());
  document.addEventListener('DOMContentLoaded',()=>{const root=document.getElementById('workCatalogGrid');if(root)mo.observe(root,{childList:true,subtree:true});loadOfficial();load()});
  setInterval(load,5000);
})();
