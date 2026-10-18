# BiduTruck 1.0.68.31 — Percurso da viagem

O campo existente "Percurso da viagem" passa a acompanhar o hodômetro desde a detecção da carga. Não usa a diferença entre distância planejada e GPS restante e não para quando ultrapassa o planejado ou sua margem de 25%.

O estado visual utiliza uma leitura isolada do início da viagem já registrado no diário. Trocas de carga reiniciam apenas o contador da nova viagem; desconexões e leituras inválidas não apagam o último valor. A referência do diário permite retomar uma carga em andamento depois de reiniciar o app. A amostra final é lida antes de descartar a referência da viagem concluída.

A distância restante do GPS, o nome dos campos, o layout, os pacotes enviados, as evidências do diário, o Gold e a validação da central não foram alterados. O valor do painel é informativo; não representa a distância validada para crédito.

Validação: 39 verificações automatizadas de limite de velocidade e estado do contador; compilação net48 x64 concluída. Cenários do contador incluem ultrapassar 1.000 e 1.250 km, mudança de carga, reinício com diário, leitura inválida, oscilação do hodômetro e troca de veículo.

O pacote de teste atualiza somente o executável e seu arquivo de configuração, com verificação SHA256, backup e opção de restaurar. Requer fechar a telemetria e permissão de administrador. Não altera manifestos do atualizador automático. A validação final no ETS2 ainda deve ser feita durante uma carga.
