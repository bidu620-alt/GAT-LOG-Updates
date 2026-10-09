# RoadBETS2 Telemetria 1.0.68.21

Teste local antes de publicar os manifestos do atualizador.

- Novo nome nas janelas e mensagens do aplicativo.
- Logo RoadBETS2 em PNG com alpha real, usada no aplicativo e na coluna esquerda.
- Dashboard2 com Viewbox Uniform: largura e altura sempre escalam na mesma proporcao.
- Voz original com tolerancia de 1 km/h; velocidade branca ate o limite + 1 km/h e vermelha acima.
- Mantidos o launcher, nomes internos dos executaveis, pasta de dados, credenciais e protocolo da API para compatibilidade.

Compilacao sem erros, com os sete avisos ja presentes na base. Passaram os 21 testes de limite e as verificacoes dos limiares de voz e cores. Renderizacao WPF conferida em 1200x297, 720x180 e 900x450, com igualdade de escala nos dois eixos.

Install-Brand-Test.ps1 valida sha256.json, guarda backup dos arquivos substituidos e permite -Mode Restore. A validacao no jogo ainda depende do usuario. O pacote nao muda os manifestos publicos.
