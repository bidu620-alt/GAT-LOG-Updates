# Biblioteca interna de cargas BiduLog

Biblioteca independente para reutilização em eventos e passes. Não possui página, endpoint público, menu ou instalação no site. Não altera o catálogo original, o banco de dados nem as regras do passe atualmente carregadas.

## Pastas

- `catalog-groups/`: cópias dos 30 grupos originais, com todas as 380 entradas e seus 413 códigos. Campos e agrupamentos do catálogo de origem são preservados.
- `categories/`: oito seleções internas por código: Cegonheiro, Madeireiro, Frigorífico, Carga Seca/Geral, Maquinário & Construção, Agronegócio, Granel & Mineração e Contêineres.
- `review.json`: códigos presentes no catálogo que não foram selecionados para essas oito categorias; permanecem disponíveis nos grupos originais para outros eventos. Não existe classificação automática por nome ou por sufixo.
- `manifest.json`: versão da biblioteca, contagens e referência verificável do catálogo de origem.
- `index.js`: funções de consulta somente leitura para a central.

## Uso interno

```js
import {getCargoIds, matchesCategory, classifyCargoId, getCatalogGroup} from './cargo-library/index.js';
const cargasDoEvento = getCargoIds('madeireiro');
const contaNoEvento = matchesCategory('madeireiro', cargoIdDaEntrega);
const categoriaDoPasse = classifyCargoId(cargoIdDaEntrega); // null: não selecionado
const grupoOriginal = getCatalogGroup('fuel'); // evento de combustível, por exemplo
```

O consumidor deve fornecer o código exato que veio da telemetria. `cargo.logs` e `logs` são normalizados para o mesmo código; nomes localizados, nomes parecidos e sufixos genéricos não são aceitos como prova.

As oito seleções são exclusivas entre si. Rodagem Bruta e Peso Pesado não são categorias de mercadoria: seus critérios de distância e peso continuam no consumidor. Datas, elegibilidade, propriedade do passe, prêmio e progresso não pertencem à biblioteca.

Exemplos: `beverages_c` está em Contêineres; `beverages` em Carga Seca. `truck_batt` está em Carga Seca; `condensator` e `locomotive` em Maquinário. As seleções são explícitas e revisáveis; o catálogo de origem pode ter rótulos amplos ou inconsistentes, que não são corrigidos aqui.

## Ativação

A biblioteca está pronta para importação futura. Criar ou instalar estes arquivos NÃO ativa alterações no passe/eventos existentes. A conexão a cada consumidor deve ser implementada e testada separadamente. Não há necessidade de reiniciar a central apenas para armazenar a biblioteca.

Para uma atualização do catálogo, gere e revise uma nova versão da biblioteca antes de substituir a versão em uso. Não modifique o arquivo original para encaixá-lo nas categorias do passe.
