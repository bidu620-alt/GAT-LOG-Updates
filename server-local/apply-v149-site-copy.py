from pathlib import Path

root=Path(__file__).resolve().parents[1]

def patch(rel,replacements):
    p=root/rel
    s=p.read_text(encoding='utf-8')
    for old,new,label in replacements:
        if old in s:
            s=s.replace(old,new)
        elif new not in s:
            raise SystemExit(f'{rel}: nao encontrei {label}')
    p.write_text(s,encoding='utf-8')

patch(Path('docs/motorista.html'),[
    ('Pegue qualquer carga no ETS2 e cumpra as regras da viagem. A Central GAT identifica automaticamente em qual dos 30 trabalhos a carga se encaixa. Se o nome ainda não for conhecido, a entrega fica salva para classificação do Admin ou Moderador.',
     'Pegue qualquer carga no ETS2. A Central GAT registra a entrega, identifica a carga e depois avalia separadamente a elegibilidade para Pontos GAT. Se o nome ainda não for conhecido, a viagem continua salva no histórico e pode ser classificada depois.',
     'texto antigo de 30 trabalhos'),
    ('CATÁLOGO GAT • 30 CATEGORIAS','CATÁLOGO GAT • CARGAS OFICIAIS','titulo antigo de 30 categorias'),
    ('Cada entrega válida é classificada automaticamente; cargas ainda desconhecidas ficam aguardando classificação da equipe GAT sem perder a viagem.',
     'Cada entrega concluída fica registrada; cargas ainda desconhecidas ficam aguardando classificação da equipe GAT sem perder histórico, km ou XP.',
     'texto antigo de entrega valida')
])

patch(Path('docs/work-catalog.js'),[
    ("if(min)min.textContent='500 km reais';","if(min)min.textContent='Sem distância mínima';",'distancia minima antiga'),
    ("if(owner)owner.textContent='A carga é reconhecida pela telemetria. A meta mensal continua sendo 30 viagens válidas, mas os tipos de carga não ficam limitados a 30 categorias.';",
     "if(owner)owner.textContent='A carga é reconhecida pela telemetria. Cada entrega concluída entra no histórico; a Central avalia os Pontos GAT separadamente.';",
     'meta mensal no catalogo'),
    ("if(rule)rule.innerHTML='<b>30</b><span>VIAGENS / MÊS</span><b>500 km</b><span>REAIS MÍN.</span>';",
     "if(rule)rule.innerHTML='<b>COLEÇÃO</b><span>DE CARGAS</span><b>SEM MÍN.</b><span>KM REAIS</span>';",
     'regra visual 30/500'),
    ("if(progressTitle)progressTitle.textContent='30 viagens';","if(progressTitle)progressTitle.textContent='Coleção de cargas';",'titulo 30 viagens'),
    ("if(progressLead)progressLead.textContent='A meta mensal continua em 30 viagens válidas. O catálogo em português é apenas visual e não altera o nome recebido do jogo.';",
     "if(progressLead)progressLead.textContent='Cada tipo de carga concluído entra uma vez na coleção. Repetir uma carga continua valendo no histórico, km, XP e Pontos GAT, mas não aumenta a coleção novamente.';",
     'lead de meta mensal')
])

patch(Path('docs/work-rule-final.js'),[
    ("if(owner&&/30 viagens|meta mensal|500 km/i.test(owner.textContent||'')) owner.textContent='A carga é reconhecida automaticamente pela telemetria.';",
     "if(owner&&/30 viagens|meta mensal|500 km/i.test(owner.textContent||'')) owner.textContent='A carga é reconhecida automaticamente pela telemetria. Cada entrega concluída fica registrada no histórico.';",
     'fallback de regra antiga')
])

checks={
 'docs/motorista.html':['30 trabalhos','30 CATEGORIAS'],
 'docs/work-catalog.js':['meta mensal continua sendo 30','VIAGENS / MÊS',"textContent='30 viagens'",'meta mensal continua em 30'],
}
for rel,bad in checks.items():
    s=(root/rel).read_text(encoding='utf-8')
    for marker in bad:
        if marker in s: raise SystemExit(f'{rel}: regra antiga ainda presente: {marker}')
print('Site 1.0.49: copias antigas de meta mensal e 30 categorias removidas.')
