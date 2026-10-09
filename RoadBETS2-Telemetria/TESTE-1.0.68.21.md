# RoadBETS2 Telemetria 1.0.68.21

Teste local antes de publicar os manifestos do atualizador.

- Novo nome nas janelas e mensagens do aplicativo.
- Logo RoadBETS2 em PNG com alpha real, usada no aplicativo. A coluna esquerda mostra ROADBETS2 TELEMETRIA em texto.
- Dashboard2 com o redimensionamento original restaurado; somente o texto da coluna esquerda se ajusta proporcionalmente ao espaco disponivel.
- Voz original com tolerancia de 1 km/h; velocidade branca ate o limite + 1 km/h e vermelha acima.
- Mantidos o launcher, nomes internos dos executaveis, pasta de dados, credenciais e protocolo da API para compatibilidade.

Compilacao sem erros, com os sete avisos ja presentes na base. Passaram os 21 testes de limite e as verificacoes dos limiares de voz e cores. Renderizacao WPF conferida em 1200x297, 720x180 e 900x450, com o redimensionamento original e o nome em texto na coluna esquerda.

Install-Brand-Test.ps1 valida sha256.json, guarda backup dos arquivos substituidos e permite -Mode Restore. A validacao no jogo ainda depende do usuario. O pacote nao muda os manifestos publicos.
