(()=>{
  const API='https://douglas.tail4577e8.ts.net';
  let catalog=[];
  const openCards=new Set();
  const FALLBACK={
    tractor:['Trator','Colheitadeira','Semeadora','Cultivador','Arado','Pulverizador','Enfardadeira','Ceifeira'],
    fuel:['GLP / LPG','Diesel','Gasolina','Querosene','Etanol','Propano','Butano'],
    food:['Frutas','Legumes','Carne','Frango','Peixe','Arroz','Farinha','Açúcar','Chocolate','Alimentos congelados'],
    drinks:['Água','Suco','Refrigerante','Cerveja','Vinho','Sidra','Chá','Café'],
    timber:['Madeira','Toras','Troncos','Tábuas','Vigas de madeira'],
    container:['Contêiner','Contêiner marítimo','Contêiner de carga'],
    heavy_machine:['Escavadeira','Bulldozer','Pá-carregadeira / Volvo L250H','Retroescavadeira','Guindaste','Dumper / caminhão fora-de-estrada'],
    vehicles:['Carros','Vans','Caminhões','Chassis de caminhão','Ônibus','Picapes'],
    motorcycles:['Motocicletas','Motos','Scooters'],
    chemicals:['Produtos químicos','Ácido sulfúrico','Ácido clorídrico','Cloro','Solventes','Resina','Fertilizante'],
    construction:['Cimento','Concreto','Tijolos','Telhas','Areia','Cascalho','Gesso','Mármore','Granito','Vigas de concreto'],
    steel:['Aço','Bobina de metal','Bobina de aço','Chapas de aço','Vigas de aço','Vergalhão','Ferro','Cobre','Alumínio','Lingotes'],
    paper:['Papel','Rolos de papel','Celulose','Papelão'],
    electronics:['Eletrônicos','Computadores','Servidores','TVs','Celulares','Eletrodomésticos'],
    furniture:['Móveis','Mesas','Cadeiras','Sofás','Colchões','Armários'],
    glass:['Vidro','Painéis de vidro','Chapas de vidro'],
    pipes:['Tubos','Tubos de aço','Tubos de concreto','Tubulação'],
    cables:['Cabos','Bobinas de cabo','Carretéis de cabo','Fios industriais'],
    industrial:['Equipamento industrial','Peças de máquinas','Gerador','Transformador','Compressor','Empilhadeira','Locomotiva'],
    mining:['Minério','Minério de ferro','Minério de cobre','Bauxita','Carvão','Minerais'],
    grain:['Grãos','Trigo','Milho','Cevada','Centeio','Aveia','Girassol','Sementes','Soja','Arroz'],
    rural:['Gado','Animais','Ovelhas','Porcos','Feno','Palha','Lã','Produtos rurais'],
    dairy:['Leite','Queijo','Iogurte','Manteiga','Creme de leite'],
    medical:['Medicamentos','Equipamento médico','Material médico','Vacinas','Suprimentos hospitalares'],
    scrap:['Sucata','Sucata metálica','Resíduos','Recicláveis','Plásticos usados','Papel usado'],
    road_machine:['Pavimentadora','Fresadora de asfalto','Rolo compactador','Motoniveladora','Compactador'],
    boats:['Barcos','Iates','Veleiros','Lanchas','Catamarãs'],
    aircraft:['Aeronaves','Helicópteros','Aviões','Planadores'],
    refrigerated:['Carga refrigerada','Alimentos congelados','Sorvete','Carne congelada','Peixe congelado','Peixe fresco','Carne fresca','Frutas frescas'],
    custom:['A carga exata que você digitar']
  };
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const sess=()=>{try{return JSON.parse(localStorage.getItem('gat_driver_account_v1')||sessionStorage.getItem('gat_driver_account_v1')||'null')}catch(_){return null}};
  const user=()=>{try{if(typeof key!=='undefined'&&key)return clean(key)}catch(_){}return clean(new URLSearchParams(location.search).get('u')||sess()?.user)};
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  function chips(values,live){return values.map(x=>'<span class="cargo-compat-chip'+(live?' live':'')+'">'+esc(x)+'</span>').join('')}
  function itemFor(card){const n=card.querySelector('.cargo-number')?.textContent||'';const pos=Number(n.replace(/\D/g,''));return catalog.find(x=>Number(x.position)===pos)||null}
  function cardKey(item){return String(item?.id||item?.position||'')}
  function decorate(){document.querySelectorAll('#workCatalogGrid .cargo-card').forEach(card=>{
    if(card.querySelector('.cargo-compat-wrap'))return;
    const item=itemFor(card);if(!item)return;
    const body=card.querySelector('.cargo-body');if(!body)return;
    const actual=Array.isArray(item.compatible_cargos)?item.compatible_cargos.filter(Boolean):[];
    const examples=Array.isArray(item.recognized_terms)&&item.recognized_terms.length?item.recognized_terms:(FALLBACK[item.id]||[]);
    const wrap=document.createElement('div');wrap.className='cargo-compat-wrap';
    const id='cargoCompat'+String(item.position||item.id).replace(/\W/g,'');
    const key=cardKey(item),isOpen=openCards.has(key);
    wrap.innerHTML='<button type="button" class="cargo-compat-toggle" aria-expanded="'+String(isOpen)+'" aria-controls="'+id+'"><span>'+(isOpen?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS')+'</span><span class="arrow">⌄</span></button><div class="cargo-compat-panel" id="'+id+'" '+(isOpen?'':'hidden')+'><span class="cargo-compat-title">O QUE CONTA NESTE TRABALHO</span>'+(actual.length?'<span class="cargo-compat-source">JÁ VISTAS NO ETS2 PELO GAT TELEMETRIA</span><div class="cargo-compat-chips">'+chips(actual,true)+'</div>':'')+'<span class="cargo-compat-source">TIPOS RECONHECIDOS</span><div class="cargo-compat-chips">'+chips(examples,false)+'</div><span class="cargo-compat-note">A lista verde vem das cargas que o GAT Telemetria encontrou de verdade no ETS2. Ela aumenta conforme os motoristas jogam.</span></div>';
    const btn=wrap.querySelector('button'),panel=wrap.querySelector('.cargo-compat-panel'),label=btn.querySelector('span:first-child');
    btn.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const open=btn.getAttribute('aria-expanded')==='true';const next=!open;btn.setAttribute('aria-expanded',String(next));panel.hidden=!next;if(label)label.textContent=next?'OCULTAR CARGAS COMPATÍVEIS':'VER CARGAS COMPATÍVEIS';if(next)openCards.add(key);else openCards.delete(key)});
    body.appendChild(wrap);
  })}
  async function load(){const u=user();if(!u)return;try{const r=await fetch(API+'/api/public/work/catalog?user='+encodeURIComponent(u),{cache:'no-store'}),d=await r.json();if(r.ok&&d?.ok&&Array.isArray(d.catalog)){catalog=d.catalog;decorate()}}catch(_){decorate()}}
  const mo=new MutationObserver(()=>decorate());document.addEventListener('DOMContentLoaded',()=>{const root=document.getElementById('workCatalogGrid');if(root)mo.observe(root,{childList:true,subtree:true});load()});setInterval(load,5000);
})();