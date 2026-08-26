# GAT-LOG-Updates

Repositorio oficial de atualizacoes do **GAT LOG BETA Servidor** e do **GAT Telemetria Cliente**.

Versoes preparadas para o novo sistema:

- Servidor: **1.9.3**
- Cliente: **1.7**

## Pastas

- `manifests/` - informa a versao publicada e o patch disponivel.
- `updater/` - atualizador generico usado pelos aplicativos.
- `releases/` - patches das novas versoes.
- `docs/` - documentacao do processo.

Os aplicativos consultam o GitHub automaticamente. Quando existir uma versao maior, o botao muda para **ATUALIZAR PARA X**.

As atualizacoes devem preservar configuracoes, historico, motoristas, servidores cadastrados, tokens e demais dados persistentes.
