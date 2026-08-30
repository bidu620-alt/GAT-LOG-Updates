// GAT-LOG: configuração visual dos mapas.
// Mapas exportados pelo GAT Map Exportador usam pirâmides PNG diretas.
window.GAT_MAP_CONFIG = {
  base: {
    label: 'Mapa Base',
    type: 'gat-direct-tiles',
    source: 'GAT Map Exportador ETS2',
    tileUrl: 'maps/base/tiles/{z}/{x}/{y}.png?v=1',
    tileSize: 256,
    minZoom: 0,
    maxZoom: 6,
    pixelBounds: [0, 65536, 65536, 0],
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
    type: 'gat-direct-tiles',
    source: 'GAT Map Exportador ETS2 + Cloudflare R2',
    tileUrl: 'https://maps.gatlogets2.com.br/rbr/Tiles/{z}/{x}/{y}.png?v=2',
    tileSize: 256,
    minZoom: 0,
    // Usa o nível 7 como máximo nativo e amplia ele no zoom 8.
    // Isso evita a tela preta que aparecia no último nível do export RBR.
    maxZoom: 7,
    gameBounds: {
      xMin: -311579.156,
      zMin: -116616.6,
      xMax: -23590.125,
      zMax: 171372.438
    }
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
