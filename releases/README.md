# Pacotes de atualizacao

Esta pasta guarda os patches de cada versao do GAT LOG.

Padrao sugerido:

- `releases/cliente/1.8.ps1`
- `releases/servidor/1.9.4.ps1`

Ao publicar uma nova versao, o manifesto correspondente deve receber a nova versao, a URL RAW do patch e o SHA256 do arquivo. O aplicativo detecta a mudanca automaticamente e habilita o botao de atualizacao.

Os patches devem substituir somente arquivos do programa. Configuracoes, motoristas, tokens, historico, servidores cadastrados e dados persistentes nao devem ser apagados.
