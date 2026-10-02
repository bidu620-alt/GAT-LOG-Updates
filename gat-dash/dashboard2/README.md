# Dashboard2 • RoadLife ETS2

Primeiro protótipo isolado do novo dashboard. Não altera o GAT DASH atual.

## O que já funciona

- Telemetria direta do TruckSim GPS em `127.0.0.1:31377/api/ets2/telemetry`.
- Também aceita telemetria enviada pelo aplicativo com `window.dashboard2PushTelemetry(payload)`.
- Velocidade, limite, marcha, RPM, combustível, rota, carga, peso, distância, ETA e horário/data de Brasília.
- Danos de motor, transmissão, cabine, chassi, rodas, reboque e carga.
- Temas verde, vermelho, amarelo, branco e azul.
- Layout horizontal ou vertical.
- Ganchos para imagem dinâmica de caminhão e carga com fallback quando a imagem ainda não existir.

## Próxima etapa

Adicionar os arquivos WebP transparentes em:
- `assets/trucks/`
- `assets/cargo/`

Depois integrar o Dashboard2 como opção dentro do aplicativo principal, sem remover o dashboard clássico.
