# BiduLog 1.0.68.40 — TESTE PRIVADO DE LEITURA DE SAVE

Base pública verificada: 1.0.68.39. Esta compilação não foi publicada.

## Como abrir

1. Extraia o ZIP inteiro em uma pasta própria.
2. Feche o aplicativo BiduLog normal. Não execute os dois simultaneamente.
3. Abra `Aplicativo/GAT_TELEMETRIA.exe` (ou INICIAR-TESTE.cmd).
4. Use a conta habitual e abra o perfil do ETS2 que deseja testar.
5. Ao salvar ou ao ocorrer autosave, o app lê o save desse perfil. Não é preciso
   mudar `g_save_format` ou descriptografar arquivos manualmente.

O app inclui os recursos da instalação 1.0.68.39, TruckSim GPS 1.4.4 e o runtime
privado do leitor. Não precisa instalar Node. Não substitui os arquivos do app
normal nem publica atualizações. Reutiliza conta/configurações/fila da base:
feche-o antes de voltar à versão normal. Atualização automática do app de teste
está desativada. O funcionamento de jogo/TruckSim GPS permanece o da base.

## O que envia

A telemetria continua sendo enviada pelo fluxo autenticado habitual. Antes da
assinatura do pacote, `gat_save_evidence` acrescenta:

- Perfil ativo informado pelo TruckSim GPS, vinculado exatamente à pasta do save;
- Hash e data de game.sii/info.sii, idade e consistência temporal do par;
- Modo de veículo, current_job, current_bus_job e current_car_job;
- Atributos dos trabalhos, inclusive campos ainda desconhecidos de passageiros;
- Referências do veículo e acessórios, com arquivos de definição de modelo;
- Últimas 30 entradas disponíveis de cada histórico de viagens do jogador;
- Dependências de mods/DLCs do save e indicação de truncamento.

Não envia o arquivo game.sii completo, senhas, conta bancária do jogo, empresas
ou motoristas contratados. O histórico é limitado intencionalmente; o app preserva
as estruturas dos trabalhos e as referências relacionadas, não o mundo inteiro.

Só escolhe saves da pasta do perfil ativo, em profiles ou steam_profiles, no
Documents do Windows. Não escolhe outro perfil porque seu save é mais recente.
Sem perfil ativo confirmado, informa active_profile_unavailable. Em cada perfil,
escolhe o game.sii salvo mais recentemente. Lê somente e verifica se o arquivo
mudou durante a leitura. Nunca grava nos originais.

O save não é tempo real: atualiza quando o jogo salva. Até 120 segundos o snapshot
é marcado como recente; depois permanece disponível como antigo. A telemetria
continua em tempo real. Pacote completo vai quando o save muda e a cada 30 s;
os intermediários levam referência ao hash para reduzir tráfego. A coleta não
cria uma entrega nem concede XP/Gold no aplicativo.

## Diagnóstico para seu teste

A barra do app mostra `Save: atualizado`, `antigo` ou `indisponível`.
O último snapshot completo lido fica em:

`%LOCALAPPDATA%/ROADLIFE TELEMETRIA Cliente/save-reader-last.json`

Esse arquivo é diagnóstico: pode ficar da última leitura bem-sucedida. Confira
profile_id, captured_at e data do save antes de interpretá-lo como atual.

## Central: componente complementar incluído, NÃO aplicado

A Central atual pode receber o campo dentro da telemetria, mas precisa do receptor
incluído para guardar os snapshots completos por hash e devolver `save_capture`.
Este pacote não mudou o servidor em produção.

Na Central DE TESTE compatível, com serviço parado e backup completo:

```
node central/install-scs-bus-job-central.mjs CAMINHO_CENTRAL central/scs-job-compat.js
node central/install-save-reader-central.mjs CAMINHO_CENTRAL
```

Use Node 24. O segundo instalador espera que save-evidence.js esteja ao lado dele.
Os instaladores criam backups e recusam versões desconhecidas. Reinicie o serviço
após aplicar. Para voltar, pare o serviço e restaure os arquivos dos backups na
ordem inversa. O receptor cria somente save_evidence_snapshots na primeira
recepção válida, guardando até 100 snapshots por usuário. Não recalcula o passado.

O receptor verifica assinatura do diário, perfil, hash e idade; salva os dados
para cruzamento com os eventos normais. Autosave sozinho, desaparecimento de um
trabalho, parada de ônibus e simples presença de uma van NÃO autorizam créditos.
As regras e a conclusão permanecem na Central. Modalidades/entradas de histórico
cujo significado ainda não foi confirmado ficam pendentes de análise; este pacote
não promete contabilização nova baseada somente no save.

## Verificação realizada

- Leitura real dos game.sii e info.sii criptografados fornecidos, por C# chamando
  o runtime incluído; perfil BIDUZAO, cargo.skidder e Scania S 2016 identificados.
- Hash original conferido antes/depois: sem alteração.
- Testes de leitura: passageiros, carros, referências, campos desconhecidos,
  histórico, leitura de texto e separação de dados bancários.
- Testes do receptor: assinatura/perfil, idade, armazenamento idempotente e nenhum
  crédito automático.
- Compilação completa do app net48. Ainda não executado com uma viagem real no jogo.

Créditos: leitor SII baseado em @trucky/sii-decrypt-ts (MIT), adaptado para Node 24.
Licenças e código do leitor incluídos em SaveReader. Node distribuído com licença.
