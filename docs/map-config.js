// GAT-LOG: configuração visual dos mapas.
// O Mapa Base abaixo foi exportado diretamente do ETS2 para o GAT.
window.GAT_MAP_CONFIG = {
  base: {
    label: 'Mapa Base',
    type: 'gat-export',
    source: 'GAT Map Exportador ETS2',
    imageChunks: [
      'maps/base/base-00.b64?v=2',
      'maps/base/base-01.b64?v=2',
      'maps/base/base-02.b64?v=2',
      'maps/base/base-03.b64?v=2',
      'maps/base/base-04.b64?v=2'
    ],
    imageMime: 'image/webp',
    nativeImageSize: [1024, 1024],
    // Limites lidos do TileMapInfo.json enviado com a exportação.
    gameBounds: {
      xMin: -113177.313,
      zMin: -122648.086,
      xMax: 97925.625,
      zMax: 88454.85
    }
  },
  promods: {
    label: 'ProMods',
    type: 'reference',
    reference: 'base',
    note: 'Mapa Base usado apenas como referência até entrar a exportação do ProMods.'
  },
  rbr: {
    label: 'RBR',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando imagem/base visual do RBR.'
  },
  rotas_brasil: {
    label: 'Rotas Brasil',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando base visual personalizada.'
  },
  eaa: {
    label: 'EAA',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando base visual do EAA.'
  },
  other: {
    label: 'Outro mapa',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando base visual personalizada.'
  }
};
