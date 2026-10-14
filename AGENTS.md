# Instruções para eventos GAT

## Regra definida pelo proprietário em 13/10/2026

Esta regra se aplica aos próximos eventos lançados. Não alterar retroativamente a elegibilidade ou o progresso dos eventos já existentes, incluindo Fazenda do Bidu, sem pedido explícito do proprietário.

- Todo novo evento deve definir uma data e hora de início (`start`) em formato ISO 8601 com fuso explícito. Exibir datas ao usuário no fuso America/Sao_Paulo.
- A data deve corresponder ao lançamento do evento, não à data de criação de um rascunho. Fixar o marco no lançamento; atualizações de layout, correções e novas publicações não reiniciam o evento.
- Somente entregas concluídas em ou após o marco (`completed_at`/`delivered_at` >= `start`) podem contribuir para progresso e conquistas. Entregas anteriores continuam no histórico do motorista, mas não contam nesse novo evento.
- Aplicar o filtro temporal no servidor antes de limitar, agrupar ou selecionar as entregas por categoria. O frontend deve aplicar o mesmo marco.
- Um evento permanente mantém seu marco inicial; permanente não significa aceitar todo o histórico anterior.
- Não ativar um novo evento sem marco de início válido.
- Ao validar a implementação, conferir entregas anteriores, exatamente no marco e posteriores, além de histórico longo, contagem por categoria e preservação do marco após atualização.
