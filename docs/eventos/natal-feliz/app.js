const categories=[
{file:'doces',name:'Doces de Natal',description:'Uma carga de doçura para espalhar sorrisos.',cargos:['Chocolate','Chicletes','Balas de goma','Sorvete','Mel','Açúcar','Xarope de bordo']},
{file:'brinde',name:'Brinde de Natal',description:'Leve o espírito da celebração para a estrada.',cargos:['Bebidas','Água engarrafada','Café gelado enlatado','Suco concentrado','Cerveja sem álcool','Limonada','Leite','Leite de soja','Leite de coco']},
{file:'ceia',name:'Ceia de Natal',description:'Entregue os sabores que reúnem todo mundo.',cargos:['Carne bovina','Carne de frango','Carne suína','Carne bovina enlatada','Carne suína enlatada','Sardinhas enlatadas','Atum enlatado','Feijão','Feijão enlatado','Macarrão','Batatas','Tomates','Cebolas','Maçãs','Uvas','Laranjas','Peras','Alimentos embalados','Filé de salmão','Palitos de peixe empanados','Azeitonas','Azeite de oliva','Óleo comestível','Amêndoas','Couve-flor','Manjericão','Caviar','Arroz','Farinha','Trigo','Queijo','Iogurte','Salsichas','Peixe fresco','Nhoque','Alho','Sal e especiarias']},
{file:'brinquedos',name:'Brinquedos',description:'Leve brinquedos e espalhe a alegria do Natal.',cargos:['Brinquedos']}
];
document.querySelector('#cards').innerHTML=categories.map(({file,name,description,cargos})=>`<article class="card"><div class="badge"><img src="assets/${file}.webp" alt="Selo ${name}" loading="lazy"></div><h3>${name}</h3><p>${description}</p><div class="card-reward"><span>Meta: 5 entregas</span></div><div class="seal-label">Recompensa: selo da categoria</div><details class="cargo-list"><summary aria-label="Cargas aceitas de ${name}: ${cargos.length} cargas" title="Mostrar ou ocultar cargas aceitas"><span class="cargo-chevron" aria-hidden="true"></span></summary><ul>${cargos.map(cargo=>`<li>${cargo}</li>`).join('')}</ul></details></article>`).join('');
document.querySelectorAll('.price').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('.price').forEach(b=>{b.classList.remove('selected');b.setAttribute('aria-pressed','false')});button.classList.add('selected');button.setAttribute('aria-pressed','true');document.querySelector('#entry-note').textContent='Entrada selecionada: '+button.dataset.value}));





