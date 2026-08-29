// GAT-LOG: configuração visual dos mapas.
// O Mapa Base usa a pirâmide de tiles original exportada diretamente do ETS2.
window.GAT_MAP_CONFIG = {
  base: {
    label: 'Mapa Base',
    type: 'gat-zip-tiles',
    source: 'GAT Map Exportador ETS2',
    zipUrl: 'maps/base/GAT_MAPA_BASE.zip?v=1',
    zipRoot: 'GAT_MAPA_BASE',
    zipTileSize: 256,
    minZoom: 0,
    maxZoom: 6,
    // Limites reais confirmados no TileMapInfo.json da exportação.
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
