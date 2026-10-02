# RoadLife ETS2 Dashboard

Base independente do novo painel RoadLife.

Princípios:
- não importa nem modifica `gat-dash/shared`, `gat-dash/dashboard2` ou `gat-dash/dashboard2-horizontal`;
- somente layout horizontal;
- proporção visual fixa 1000:205 para impedir deformação;
- instalação, configurações, WebView2, localStorage, host virtual e AppId próprios;
- camada de telemetria separada da camada visual.

Estrutura:
- `app/`: interface;
- `telemetry/`: normalização do payload TruckSim GPS;
- `assets/`: logo, frame, ícones, velocímetro, cargas, caminhões e reboques;
- `windows/`: wrapper WinForms/WebView2;
- `config/`: configuração do dashboard.
