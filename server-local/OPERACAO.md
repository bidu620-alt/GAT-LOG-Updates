# Central GAT no PC — migração concluída em 2026-09-01

A API pública https://api.gatlogets2.com.br responde pelo GAT Servidor 1.0.39-local
no PC de Douglas, usando SQLite local. O site estático permanece no GitHub Pages.

## Estado confirmado

- Troca concluída às 16:58 UTC pelo workflow 33534951804.
- API definitiva: versão 1.0.39-local, storage local-sqlite, paused false.
- Ranking: 8 motoristas; catálogo: 30 trabalhos; CORS GET e OPTIONS verificados.
- Túnel gat-central-douglas: 6daa92f9-60d0-4d18-abc3-34a14e9ebee1.
- Origem do túnel: http://127.0.0.1:5056.
- DNS do domínio principal e www preservados.
- D1 e Worker antigos preservados com GAT_MIGRATION_PAUSED=1.
- Exportação SQL completa validada antes da importação; contas e hashes preservados.
- Instalador personalizado do túnel e exportação SQL não estão no repositório público.

## Operação

O PC, sua conexão com a internet, o serviço Cloudflared e a central local precisam
ficar ativos para login, telemetria e ranking. Marcar no painel “Iniciar a central
quando eu entrar no Windows”. Essa opção inicia no login, não antes dele.
Fechar o painel mantém a central; “Parar central” interrompe a API.
O serviço Cloudflared não substitui o processo da central.

Dados: %LOCALAPPDATA%\GAT-LOG\Central\central.sqlite.
Usar “Criar backup” e “Abrir backups” no painel. Há backups automáticos no início
e a cada 6 horas, mantendo 14 cópias locais. Cópias no mesmo disco não protegem
contra perda do disco; preservar cópia externa conforme a operação exigir.

## Desenvolvimento e recuperação

O código compartilhado em cloudflare-central é montado por server-local/build-api.py
para a API local. Alterar esse código não atualiza o PC automaticamente: construir
e distribuir uma atualização compatível, preservando o banco local.

A publicação Cloudflare deixou de ser automática. O workflow legado é manual,
restrito por entrada explícita e destina-se somente à cópia pausada.
O monitor de cota D1 também ficou manual; a proteção da API antiga permanece ativa.
Nenhuma configuração de plano pago foi alterada.

Não reativar D1 como fallback automático: desde a troca, o banco local é a fonte
dos novos registros. Retornar à nuvem exige parar novas gravações, exportar o banco
local atual e planejar a migração. Nunca restaurar a exportação antiga sobre o banco
local com viagens novas.

O checkpoint de roteamento anterior está preservado privadamente, e criptografado
nos logs do workflow 33534825095 (job 99946458734). Sem credenciais em texto aberto.

As regras de ranking validam os sete campos reais de danos e a prontidão da
telemetria. Atualizar somente o aplicativo não torna elegível uma viagem cujo
plugin não envia esses campos.
