// GAT-LOG • caminhões frontais por motorista.
// xuxa01 usa sempre o caminhão rosa. Os demais variam entre azul, verde, laranja e roxo.
(function(){
  if(typeof L==='undefined')return;

  const XUXA={file:'assets/truck-pink.png',color:'#ff2f9b'};
  const POOL=[
    {file:'assets/truck-blue.png',color:'#2f7dff'},
    {file:'assets/truck-green.png',color:'#22c55e'},
    {file:'assets/truck-orange.png',color:'#ff7a00'},
    {file:'assets/truck-purple.png',color:'#a855f7'}
  ];

  // Cada abertura do mapa embaralha as quatro cores. Durante a mesma sessão,
  // cada motorista mantém sua imagem para não ficar piscando a cada atualização.
  const assigned=new Map();
  let deck=[];

  function normalize(value){return String(value||'').trim().toLowerCase()}
  function driverIdentity(d){return normalize(d?.t?.account_user||d?.account_user||d?.username||d?.name||'motorista')}
  function shuffle(){
    deck=POOL.slice();
    for(let i=deck.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[deck[i],deck[j]]=[deck[j],deck[i]]}
  }
  function truckSpec(d){
    const id=driverIdentity(d);
    if(id==='xuxa01')return XUXA;
    if(assigned.has(id))return assigned.get(id);
    if(!deck.length)shuffle();
    const spec=deck.shift();
    assigned.set(id,spec);
    return spec;
  }

  window.GAT_TRUCK_SPEC=truckSpec;
  window.GAT_TRUCK_COLOR=d=>truckSpec(d).color;

  markerIcon=function(d){
    const moving=typeof fresh==='function'?fresh(d.t):!!d?.t?.on_job;
    const spec=truckSpec(d);
    const label=typeof esc==='function'?esc(d.name):String(d.name||'Motorista');
    return L.divIcon({
      className:'gat-truck-icon',
      iconSize:[96,58],
      iconAnchor:[25,29],
      popupAnchor:[0,-24],
      html:'<div class="gat-front-truck-marker '+(moving?'':'idle')+'" style="--truck-color:'+spec.color+'">'+
        '<div class="gat-front-truck-photo" aria-hidden="true"><img src="'+spec.file+'?v=1" alt=""></div>'+
        '<span class="gat-truck-label">'+label+'</span>'+
      '</div>'
    });
  };
})();