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
- Corrigido o topo de Trabalho Atual para não voltar a exibir textos antigos de homologação, 800 km ou World of Trucks exclusivo.

## 2026-09-15 — GAT Telemetria BETA 1.0.46
- Nova tela Início com motoristas online e em rota.
- Sobreposição do caminhão expandida com carga, destino, peso, distância restante, ETA e danos.
- Tolerância do alerta de velocidade agora é persistida.
- Botão rápido para mutar/desmutar o alerta por voz, com estado salvo.

## 2026-09-15 — GAT Telemetria BETA 1.0.47
- Corrigida leitura de números decimais em Windows configurado para pt-BR, evitando valores como 8110 km/h.
- Velocidade média validada para não gerar números absurdos.
- Distância restante agora aparece corretamente no overlay estreito.
- Tempo estimado (ETA) volta a ser calculado com distância restante e velocidade média válidas.
- Mantidos tolerância salva e botão de mutar/desmutar o alerta de velocidade.

## 2026-09-15 — GAT Telemetria TESTE 1.0.50
- Removido o Canal Web da aba GAT DASH; permanecem Canal GAT e Meu Vídeo.
- Removido o Canal Web residual do player flutuante.
- Corrigida a sobreposição do caminhão para ser arrastada pelo topo, inclusive ao clicar nos textos do cabeçalho.
- Mantidos recursos da 1.0.49, além das correções de velocidade, distância e ETA anteriores.

## 2026-09-15 — GAT Telemetria BETA 1.0.61
- GAT DASH completo agora abre em uma janela independente com proporção preservada ao aumentar ou diminuir.
- Melhor compatibilidade com monitores e resoluções diferentes, sem cortar ou deformar o DASH.
- Sobreposição de vídeo sem a barra branca do Windows; a barra GAT própria permite mover, minimizar e fechar.
- Redimensionamento pelas bordas e salvamento da última posição/tamanho continuam disponíveis.
- Rádio/TV mantém somente Canal GAT e Meu Vídeo.

## 2026-09-16 — GAT Telemetria BETA 1.0.66
- Nova arquitetura de voz com 122 falas gerais em arquivos individuais, eliminando a seleção por cortes de tempo do áudio longo.
- 12 avisos individuais de limite de velocidade, de 20 a 130 km/h.
- Cada evento escolhe somente falas do seu grupo: batida, combustível, abastecimento, entrega, chuva, radar, piadas e demais categorias.
- Detecção de batida mais sensível ao aumento de dano.
- Controle de volume da voz e mute continuam independentes da Rádio GAT e do ETS2.
- GAT DASH completo proporcional e sobreposição de vídeo sem borda preservados.
- A pasta voicepack166 existente em AppData é preservada durante a atualização.

## 2026-09-16 — GAT Telemetria BETA 1.0.67
- Novo gerenciador de pacotes em Configurações > Voz e alertas.
- SUBSTITUIR VOZ permite trocar os MP3s mantendo a numeração dos eventos.
- ADICIONAR FALAS permite incluir novas variações por grupo, sem substituir as falas existentes.
- BAIXAR MODELO TXT abre o modelo oficial com 122 falas gerais, 12 limites e estrutura dos grupos.
- RESTAURAR BACKUP recupera o pacote anterior após uma substituição.
- Pacotes personalizados existentes são preservados durante atualizações.
- Mantidos volume independente, mute, GAT DASH proporcional e sobreposição de vídeo sem borda.
