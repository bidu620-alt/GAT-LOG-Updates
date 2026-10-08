# RoadLife Telemetria — base consolidada de teste 1.0.68.16

`src/` contem o projeto C# completo recuperado uma unica vez do commit
8385ec8a5b92e200c9e7be4ed5d1083e9cefe45f e seus patches ate RoadLife 1.0.68.12.
As regras de velocidade foram corrigidas diretamente no fonte, sem os patches
incompativeis 1.0.68.13/14/15. Nao reconstruir os ZIPs nas proximas atualizacoes.

Compilar: `dotnet build RoadLife-Telemetria/src/GAT_TELEMETRIA.csproj -c Release`.
Testar: `dotnet run --project RoadLife-Telemetria/tests/RoadLimitTests.csproj -c Release`.
O workflow `build-roadlife-consolidated.yml` compila diretamente deste projeto e
produz um pacote de teste, sem publicar nem atualizar manifestos.

RoadLimitState usa relogio monotonicamente crescente: 60 segundos cumulativos sem
limite valido; sinais breves nao renovam o tempo. Novos limites validos sao sempre
imediatos. 60 segundos continuos de sinal valido renovam a tolerancia.
TelemetryEngine fornece o limite efetivo para voz, multas locais e Dashboard2.
O audio existente limite_060 e reutilizado. Caminhos dos dados persistentes foram
mantidos. O pacote faz backup dos arquivos substituidos e preserva o Dashboard
instalado, launcher e servidor TruckSimGPS. Veja LEIA-ME-TESTE.txt.

`Dashboard/` preserva os recursos presentes no GitHub como referencia. Nao sao
copiados sobre as personalizacoes do motorista pelo pacote de teste.

Validacao local: build .NET Framework 4.8 x64 passou; 21 testes passaram. Pendente:
teste no ETS2, voz, Central e atualizador. O worker cloudflare-central/worker.js
inspecionado calcula penalidade a partir da contagem de multas recebida, sem uma
regra propria de limite de velocidade; a versao realmente implantada deve ser
confirmada no teste de viagem. Nenhum banco, token ou dado pessoal foi incluido.
A migracao para release completo ainda exige consolidar launcher, TruckSimGPS,
recursos distribuidos e instalador que hoje dependem do pacote instalado.
