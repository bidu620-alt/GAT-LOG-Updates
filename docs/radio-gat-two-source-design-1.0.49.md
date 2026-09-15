# Rádio GAT — desenho 1.0.49

A aba Rádio GAT terá somente duas fontes:

- **Canal GAT**: fonte oficial/global. O usuário autorizado (Admin/Moderador) informa o link dentro do GAT Telemetria; a fonte é publicada para todos os motoristas.
- **Meu Vídeo**: fonte individual/local. Cada motorista escolhe seu próprio vídeo/playlist; isso não altera o Canal GAT nem afeta outros motoristas.

## Removido

- Canal Web.
- Controle da Rádio GAT no GAT LOG Server. O servidor de ETS2 não será mais a interface usada para escolher a mídia.

## Comportamento

- O Canal GAT pode aceitar vídeo/playlist do YouTube e, se mantido o suporte atual, stream direto MP3/AAC.
- Meu Vídeo é salvo apenas para o usuário/PC e não é enviado para os demais.
- Ao trocar de aba no GAT Telemetria, o player permanece vivo em segundo plano e não deve reiniciar a playlist.
- Pausar/parar acontece apenas por ação do usuário ou ao limpar/desligar a fonte.

## UI

Botões visíveis na aba Rádio GAT:

1. CANAL GAT
2. MEU VÍDEO

O botão CANAL WEB e seus controles devem ser removidos.
