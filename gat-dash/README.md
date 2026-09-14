# GAT DASH 1.0.1

Dashboard oficial GAT LOG para Euro Truck Simulator 2 / American Truck Simulator.

## O que esta versão entrega

- Android em modo horizontal.
- Windows/PC em janela dashboard.
- Tela de login usando a mesma conta da Central/GAT Telemetria (`/api/account/login`).
- Sem cadastro dentro do GAT DASH.
- Contas `driver`, `moderator`, `admin` e `owner` entram; `visitor` aguarda liberação de cargo.
- Senha usada somente no login HTTPS e nunca salva pelo GAT DASH.
- Telemetria lida do TruckSim GPS em `/api/ets2/telemetry` (porta padrão 31377).
- Velocidade, RPM, marcha, combustível, cruise control, temperatura, bateria, rota/carga, distância, ETA, luz alta, setas e freio de mão.
- Condição do caminhão com os campos GAT `WearEngine`, `WearTransmission`, `WearCabin`, `WearChassis` e `WearWheels`.
- Radar visual usando `navigation.speedLimit`.
- Alerta por voz quando a velocidade ultrapassa limite + tolerância.
- Aprendizado local dos pontos onde o limite muda usando posição e direção do caminhão.
- Depois que um ponto é aprendido, aviso antecipado em aproximadamente 250 m e reforço perto de 100 m quando for necessário reduzir.
- GPS grande no lado direito com destino, distância, ETA, limite e visual de rota.

## Como funciona o aviso antecipado

Na primeira passagem por um ponto ainda desconhecido, o GAT DASH aprende onde a telemetria mudou de um limite para outro. Nas próximas passagens pelo mesmo sentido da via, ele reconhece o ponto à frente e avisa antes. Os pontos ficam salvos localmente no aparelho/PC e não são inferidos apenas porque o motorista freou.

## Conexão

### PC
O GAT DASH tenta automaticamente `127.0.0.1:31377`. O TruckSim GPS Server precisa estar aberto.

### Android
No primeiro login, abra Configurações e informe o IP do PC que está rodando TruckSim GPS, por exemplo `192.168.1.100`. Para uso remoto, pode ser usado o IP Tailscale do PC quando as duas pontas estiverem conectadas.

## Observação do GPS

A API REST atual do TruckSim GPS fornece destino, distância restante, ETA, limite da via e posição/orientação do caminhão, mas não fornece a geometria completa da rota/ruas. Por isso o aviso antecipado 1.0.1 usa os pontos aprendidos em viagens anteriores. Um mapa turn-by-turn exato exigirá expor a geometria de rota no TruckSim GPS GAT em uma evolução posterior.
