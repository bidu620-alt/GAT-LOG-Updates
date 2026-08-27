# GAT-LOG Server Native 0.1

Versão instalada do GAT-LOG Server para Windows, sem PowerShell.

Arquitetura:
- `GAT_LOG_SERVER.exe`: interface Win32 nativa.
- `GAT_LOG_AGENT.exe`: agente local de telemetria/API 5055 e monitoramento do ETS2.
- `GAT_LOG_SERVER_SETUP_0.1.exe`: instalador/atualizador.

Instalação: `%LOCALAPPDATA%\Programs\GAT-LOG Server`
Dados persistentes: `%LOCALAPPDATA%\GAT-LOG`

O agente continua funcionando quando a interface é fechada. O canal de atualização usa `server_native_version.json` na raiz do repositório.
