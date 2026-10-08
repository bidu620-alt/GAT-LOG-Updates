# RoadLife Telemetria — teste 1.0.68.17 baseado no V5 do site

A base autoritativa e ROADLIFE_TELEMETRIA_1.0.68.12_TESTE_V5.exe, enviado pelo usuario
em 2026-10-08 e igual ao download docs/index.html no commit
8385ec8a5b92e200c9e7be4ed5d1083e9cefe45f.
SHA256 instalador: 5bb53169697f13c40197867b678739d0b12d840d99720e43b4e73b75ca4ed2ce.
SHA256 aplicativo V5: ce46d5c552cf7af2f95dda979e87709d0175c43beb72956fdd1728183114465c.

O fonte em src foi recuperado do executavel V5 com ILSpy 11.1.0.9782. E codigo
recuperado por descompilacao, nao os arquivos originais de desenvolvimento. Foi
recompilado com dependencias NuGet equivalentes ao V5 (hashes iguais de
Newtonsoft.Json e WebView2.Core). Nao usar mais a cadeia de patches de 1.0.28.
A tentativa anterior 1.0.68.16 nao correspondia ao V5 e foi substituida aqui.

Compilar: dotnet build RoadLife-Telemetria/src/GAT_TELEMETRIA.csproj -c Release
Testar: dotnet run --project RoadLife-Telemetria/tests/RoadLimitTests.csproj -c Release
O workflow compila este projeto diretamente e produz ZIP de teste, sem publicar.

Preservados do V5: interface e nomes RoadLife, caminho ROADLIFE TELEMETRIA Cliente,
formato dos arquivos de dados, fila, conta, configuracoes e integracao Dashboard2.
22 arquivos de suporte permanecem identicos ao fonte recuperado; no MainForm,
a interface anterior a VoiceCleanPoll coincide exatamente apos ajustar apenas
versao e fallback. Nenhum dado pessoal ou credencial foi copiado.

Mudancas: limite efetivo compartilhado por voz, Dashboard2 e multas; fallback 60;
memoria cumulativa de ausencia por 60 segundos; sinais breves nao renovam o tempo;
60 segundos de sinal valido continuo renovam a tolerancia. Todo limite valido
novo e aplicado imediatamente. Sintese antiga de 56 removida; reutiliza MP3 de 60.

O ZIP nao altera recursos personalizados, launcher, TruckSimGPS ou DLLs existentes.
Inclui opcao de restaurar exatamente o executavel V5. Os recursos Dashboard de
referencia no PR anterior nao representam integralmente os recursos V5; pacote
de teste usa recursos instalados. Consolidacao de todos os recursos e instalador
independente permanece pendente antes de publicar o release completo.

Validacao: build zero erros, sete avisos legados; 21 testes passaram. Validacao de
aplicacao/backup/restore feita em pasta simulada. Usuario confirmou em 2026-10-08 que o teste funcionou e autorizou a publicacao. Validacao adicional do instalador UPDATE passou em instalacao simulada. As proximas verificacoes do motorista incluem
viagens, penalidades e atualizador. A publicacao ativa client_dotnet_version.json, latest.json e update/latest.json com o mesmo pacote UPDATE e SHA256. Veja RELEASE-1.0.68.17.md.

