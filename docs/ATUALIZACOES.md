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
6. Configuracoes e dados persistentes permanecem intactos.

## Regra importante

Nunca publicar no manifesto uma nova versao antes de preencher `patch_url` e `patch_sha256`. Assim nenhum motorista recebe aviso de uma atualizacao ainda incompleta.
