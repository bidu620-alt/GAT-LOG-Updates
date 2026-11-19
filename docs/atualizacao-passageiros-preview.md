# Atualização de passageiros — prévia para homologação

Base: commit f207b96. Preparada em 19/11/2026. Não publicada.

## Aplicativo

Captura a configuração `bus_job` quando o provedor a oferece. Preserva seus
atributos desconhecidos e registra fabricante, modelo, ID e evento do SDK em
`gat_passenger_capture`. Impede que o latch de frete oculte um serviço de ônibus
sem peso de carga. Não altera pacotes já assinados na fila offline.

Esta atualização não inclui um novo plugin nativo SCS. Se o TruckSim GPS
instalado não exportar `bus_job`, o aplicativo não pode fabricar esses dados.
O pacote é uma compilação de teste do aplicativo, não um instalador completo:
precisa dos recursos e do TruckSim GPS da instalação existente. Não substitua
a instalação pública antes de homologar. A versão numérica permanece a da base;
o InformationalVersion identifica `passengers-preview.20261119`.

## Central

O adaptador de passageiros preserva configuração bruta e identificação do veículo
no início da viagem e no histórico. Exige recibo com distância positiva e ignora
flags/recibos genéricos remanescentes de fretes anteriores. A marca econômica
continua independente do modelo. Não foram criados IDs fictícios das modalidades.

Para uma cópia de homologação da Central compatível, com Node 24 e serviço parado:

```powershell
node .\central\install-scs-bus-job-central.mjs 'CAMINHO_DA_CENTRAL_DE_TESTE' .\central\scs-job-compat.js
```

O instalador verifica os trechos esperados, valida sintaxe, cria backups dos
arquivos alterados e recusa versões incompatíveis. Não edita SQLite, Gold, XP,
eventos ou manifestos públicos. Faça também backup completo do diretório de dados
antes da homologação. Para rollback, com a Central parada, restaure os arquivos
do backup indicado pelo instalador; remova o módulo novo se ele não existia antes.

## Limites e validação no jogo

O suporte continua condicionado aos campos reais do provedor. Os eventos
`bus_job.delivered`/`cancelled` são formatos de adaptador aceitos, não uma promessa
de que o SDK instalado os publica exatamente assim. Não há captura real das três
modalidades neste pacote. A fila geral de classificação de vans/mods ainda não
foi implementada: `car_job` pode ser courier, e um modelo de van não prova passageiros.

Homologar com banco copiado e um perfil de teste:

1. Abrir uma viagem de cada modalidade oficial e salvar JSON bruto do provedor
   e da Central no início, nas paradas e na conclusão.
2. Conferir se `bus_job` existe, qual evento conclui a viagem e seus atributos.
   Uma parada não deve concluir a viagem inteira.
3. Conferir marca/modelo/ID no histórico e um único registro na conclusão.
4. Reenviar o pacote de conclusão e confirmar ausência de duplicata.
5. Testar cancelamento, troca entre frete e ônibus e reenviar fila offline antiga.
6. Confirmar XP, pontos e Gold com a marca adquirida/selecionada antes da saída.
7. Para vans/mods, coletar ID bruto de carga e definição do mod antes de cadastrar
   equivalências. Não atribuir passageiros apenas por nome ou peso.

Somente após essas capturas definir a versão pública, atualizar os manifestos,
produzir instaladores completos e distribuir. Não há reprocessamento retroativo.

## Verificação automatizada

Testes de integração da Central: abertura, progresso, conclusão única, recibo
ausente, flags antigas e instalador idempotente com rollback/backup.
Regressões de garagem/marcas, economia e rotas HTTP. Teste C# de captura de
passageiros e compilação completa do aplicativo net48.
