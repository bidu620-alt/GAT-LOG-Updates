// GAT-LOG: configuração visual dos mapas.
// Para adicionar uma imagem de mapa depois, preencha imageUrl e gameBounds.
// gameBounds usa coordenadas do ETS2: xMin, zMin, xMax, zMax.
window.GAT_MAP_CONFIG = {
  base: {
    label: 'Mapa Base',
    type: 'tiles',
    tileUrl: 'https://raw.githubusercontent.com/felix-d1strict/vtc-map/master/ets2map/coloured/{z}/{x}_{y}.png',
    tileSize: 512,
    minZoom: 0,
    maxZoom: 8,
    pixelBounds: [0, 192512, 173568, 0]
  },
  promods: {
    label: 'ProMods',
    type: 'reference',
    reference: 'base',
    note: 'Mapa Base usado apenas como referência até entrar a base visual atual do ProMods.'
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
    note: 'Aguardando imagem/base visual do Rotas Brasil.'
  },
  eaa: {
    label: 'EAA',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando imagem/base visual do EAA.'
  },
  other: {
    label: 'Outro mapa',
    type: 'image',
    imageUrl: '',
    gameBounds: null,
    note: 'Aguardando base visual personalizada.'
  }
};
