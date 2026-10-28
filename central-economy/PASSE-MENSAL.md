# Passe mensal de novembro de 2026

O módulo `monthly-pass.js` define a edição `2026-11` e o produto `pass_2026_11`. A rota pública `GET /api/passe/config` informa o período, os preços e as metas. `POST /api/economy/pass-state` exige a sessão do próprio motorista e calcula o progresso integral do mês, com filtro temporal no servidor antes da leitura.

O passe pode ser adquirido antes da abertura. O progresso permanece zerado até 01/11/2026 às 00h00 no horário de Brasília. A janela termina em 30/11/2026 às 23h50 (limite exclusivo). O acesso adquirido e as entregas permanecem registrados após o encerramento.

O fluxo existente de economia desconta 200.000 unidades internas (2.000 de ouro) atomicamente. O Mercado Pago utiliza o preço fixo de 500 centavos, a mesma autenticação, assinatura de webhook e validações de valor/recebedor já utilizadas na central. Pagamentos aprovados após o encerramento são encaminhados ao atendimento, sem liberar silenciosamente um passe expirado. Não executar compras reais para testar a instalação.

Arquivos da central local necessários: `worker.js`, `economy-official.js`, `mercadopago.mjs` e `monthly-pass.js`. O host existente continua iniciando `worker.js`; não há mudança de serviço, segredo ou esquema de banco para o passe.

Validação (Node 24 com SQLite):

```
node --test central-economy/tests/monthly-pass.test.mjs central-economy/tests/pass-routes.test.mjs central-economy/tests/mercadopago-regression.test.mjs
```

Os testes usam banco em memória e pagamentos simulados. Cobrem as fronteiras de datas, pré-venda, histórico com mais de 100 entregas, peso estritamente acima de 40 t, identificação de cargas, compra idempotente, rollback, autenticação e validação do pagamento.

As recompensas permanecem em definição. O código não cria bônus de XP, ouro ou itens por concluir metas.
