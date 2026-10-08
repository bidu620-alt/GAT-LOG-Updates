# RoadLife 1.0.68.17 — publicação

O usuario confirmou em 2026-10-08 que o pacote baseado no V5 funcionou e autorizou
sua publicacao. O instalador UPDATE transporta exatamente o mesmo aplicativo
GAT_TELEMETRIA_APP.exe testado no ZIP 1.0.68.17, sem recompilar ou alterar sua logica.

Pacote: releases/ROADLIFE_TELEMETRIA_UPDATE_1.0.68.17.exe
SHA256: ba73c7659205d1338e172e5f605cfdfcc4a409e5343dc5ca16e1500379cd3b4c

Validação adicional: executado silenciosamente em instalacao simulada com V5,
com retorno zero, backup do aplicativo anterior conferido, hash do aplicativo
instalado igual ao testado e arquivos de dashboard/historico preservados.

O pacote exige a RoadLife V5 instalada e valida a existencia do Dashboard2.
Nao e instalador completo. O download V5 continua disponivel para novas instalacoes.
Atualizador do aplicativo usa client_dotnet_version.json. Manifestos compatíveis
latest.json e update/latest.json apontam para o mesmo pacote e SHA256.

Para a proxima atualizacao, alterar src, testar, compilar o instalador em Installer,
publicar pacote imutavel em releases e conferir SHA256 antes de alterar os manifestos.
O workflow RoadLife consolidated test valida codigo e produz o instalador sem
reconstruir a base 1.0.28. O fonte e os recursos personalizados do motorista sao
preservados conforme a base V5; recursos integrais para um instalador completo
continuam sendo trabalho separado.
