# Passe Mensal BiduLog

`index.html` apresenta a edição atual. O código visual compartilhado fica em `passe.css` e `passe.js`; as imagens ficam em `assets/`.

Cada edição mantém sua própria pasta `AAAA-MM/`, com `index.html` e `metas.json`. A primeira edição é novembro de 2026 (`2026-11`). Não mover ou reiniciar as datas de uma edição publicada ao atualizar seu layout.

- Pré-venda: permitida antes da abertura; a compra reserva o acesso.
- Início: 01/11/2026 às 00h00, America/Sao_Paulo.
- Encerramento: 30/11/2026 às 23h50, America/Sao_Paulo; entregas nesse instante ou depois dele não contam.
- Aquisição: 2.000 de ouro ou R$ 5,00 via Mercado Pago; sem renovação automática.
- Progresso: calculado pela central sobre todas as entregas válidas do período, sem recorte das 100 recentes.
- Recompensas: ainda não definidas. A consulta da página não concede XP nem outra recompensa.

`metas.json` contém a apresentação. Preço, propriedade, datas, classificação de cargas e cobrança são validados no servidor, em `central-economy/monthly-pass.js`; editar somente o navegador nunca libera um passe.

Para uma nova edição, criar uma nova pasta, definir o período e configurar o produto correspondente na central. Não reutilizar o identificador ou o pagamento de uma edição anterior.
