# Sistema de atualizacoes do GAT LOG

## Estrutura

- `manifests/cliente.json`: informa a versao atual do Cliente.
- `manifests/servidor.json`: informa a versao atual do Servidor.
- `updater/cliente_update.ps1`: recebe e valida o patch do Cliente.
- `updater/servidor_update.ps1`: recebe e valida o patch do Servidor.
- `releases/`: patches das novas versoes.

## Fluxo

1. O aplicativo consulta seu manifesto ao iniciar.
2. Se a versao publicada for maior que a instalada, aparece `ATUALIZAR PARA X`.
3. Ao confirmar, o aplicativo baixa o atualizador pelo GitHub.
4. O atualizador valida o SHA256 do patch antes de executa-lo.
5. O patch cria backup e troca somente os arquivos necessarios.
6. O patch atualiza tambem o arquivo local `.sha256` do programa, usado pelo inicializador para impedir a execucao de um script alterado ou corrompido.
7. Configuracoes e dados persistentes permanecem intactos.

## Regra importante

Nunca publicar no manifesto uma nova versao antes de preencher `patch_url` e `patch_sha256`. Assim nenhum motorista recebe aviso de uma atualizacao ainda incompleta.

O Servidor deve preservar `%LOCALAPPDATA%\GAT-LOG`. O Cliente deve preservar `servers.json`, `credentials.json` e os demais dados existentes em `%LOCALAPPDATA%\GAT Telemetria Cliente`.

## 2026-08-29 — preparação oficial
- Virada mensal em horário de Brasília.
- Ranking desempata por KM do mês, não KM total.
- Site mostra claramente HOMOLOGAÇÃO/OFICIAL.
- Mapa e motorista usam fallback da telemetria bruta.

## 2026-08-29 — catálogo GAT 1.0.25
- 30 trabalhos por mês em um catálogo comum para todos os motoristas.
- O motorista escolhe a ordem; outros motoristas podem escolher o mesmo trabalho para fazer em comboio.
- Distância mínima de 500 km.
- Aceita Mercado de Fretes, Mercado de Cargas, Trabalho Rápido e World of Trucks.
- XP por distância: 20 XP a cada 100 km completos de uma entrega GAT válida.
- Trabalho 30 permite criar uma carga personalizada pelo nome detectado no ETS2.
- Cards visuais do catálogo publicados na área Trabalho Atual.
