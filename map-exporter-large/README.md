# GAT Map Exporter Large

Projeto experimental para exportar mapas grandes do ETS2 (ex.: RBR/EAA) em tiles web sem depender de uma imagem gigante em memoria.

Base upstream: `ETS2LA/tilemap`, commit `5424eced8758970c129027bfb9b3b3b23c6c5b40`.

## Objetivo

Gerar a mesma estrutura usada pelo GAT-LOG:

- `TileMapInfo.json`
- `Tiles/<zoom>/<x>/<y>.png`
- arquivos auxiliares exportados pelo TsMap

## Melhorias GAT 0.1

- build x64;
- cada PNG continua sendo renderizado individualmente;
- exportacao pode ser retomada na mesma pasta;
- tiles existentes e validos sao pulados;
- escrita atomica `.tmp -> .png`;
- progresso visual reduzido para cada 25 tiles;
- `GC` a cada 100 tiles para liberar objetos de renderizacao;
- falhas de tiles individuais vao para `GAT_MAP_EXPORT_ERRORS.log` sem perder o restante.

## Por que isso

O exportador upstream ja trabalha tile por tile. O maior risco em mapas enormes nao e um canvas unico, e sim a duracao da exportacao, dezenas de milhares de arquivos, repaints da interface e acumulacao de memoria/objetos ao longo do processo. A primeira versao GAT ataca esses pontos sem reescrever o parser do ETS2.

## QA previsto

1. mapa base zoom 0-6;
2. RBR/EAA zoom 0-6;
3. retomada interrompendo e executando novamente na mesma pasta;
4. zoom 7;
5. zoom 8 apenas depois dos testes anteriores;
6. validar `TileMapInfo.json` e estrutura `Tiles` com o uploader R2 do GAT.

A compatibilidade de um mod especifico depende do parser upstream conseguir ler os arquivos/setores daquela versao do mapa; isso precisa ser testado com o mapa real instalado.
