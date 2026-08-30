// GAT-LOG • marcador frontal com a imagem escolhida pelo usuário.
// Xuxa recebe rosa fixo; os demais mantêm cor estável na borda e no nome.
(function(){
  if(typeof L==='undefined')return;

  const PALETTE=['#35d6a0','#4aa8ed','#ffad3d','#9b7cff','#ef5b5b','#2ec4b6','#f07ccf','#7bd389','#54c6eb','#f6bd60'];
  const XUXA_PINK='#ff2f9b';

  function normalize(value){return String(value||'').trim().toLowerCase()}
  function driverIdentity(d){return normalize(d?.t?.account_user||d?.name||'motorista')}
  function hashString(text){let h=2166136261;for(let i=0;i<text.length;i++){h^=text.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
  function truckColor(d){const id=driverIdentity(d);if(id==='xuxa'||id.includes('xuxa'))return XUXA_PINK;return PALETTE[hashString(id)%PALETTE.length]}

  window.GAT_TRUCK_COLOR=truckColor;

  markerIcon=function(d){
    const moving=typeof fresh==='function'?fresh(d.t):!!d?.t?.on_job;
    const color=truckColor(d);
    const label=typeof esc==='function'?esc(d.name):String(d.name||'Motorista');
    return L.divIcon({
      className:'gat-truck-icon',
      iconSize:[92,58],
      iconAnchor:[24,29],
      popupAnchor:[0,-24],
      html:'<div class="gat-front-truck-marker '+(moving?'':'idle')+'" style="--truck-color:'+color+'">'+
        '<div class="gat-front-truck-photo" aria-hidden="true"><img src="assets/truck-front-pink.png?v=1" alt=""></div>'+
        '<span class="gat-truck-label">'+label+'</span>'+
      '</div>'
    });
  };
})();