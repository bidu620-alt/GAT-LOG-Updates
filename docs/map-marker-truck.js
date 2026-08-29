// GAT-LOG • caminhão compacto no mapa, com cor por motorista.
// Xuxa recebe rosa fixo; os demais recebem uma cor estável baseada no usuário.
(function(){
  if(typeof L==='undefined')return;

  const PALETTE=['#35d6a0','#4aa8ed','#ffad3d','#9b7cff','#ef5b5b','#2ec4b6','#f07ccf','#7bd389','#54c6eb','#f6bd60'];
  const XUXA_PINK='#ff2f9b';

  function normalize(value){return String(value||'').trim().toLowerCase()}
  function driverIdentity(d){return normalize(d?.t?.account_user||d?.name||'motorista')}
  function hashString(text){let h=2166136261;for(let i=0;i<text.length;i++){h^=text.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
  function truckColor(d){const id=driverIdentity(d);if(id==='xuxa'||id.includes('xuxa'))return XUXA_PINK;return PALETTE[hashString(id)%PALETTE.length]}

  function svgTruck(color){
    const safe=String(color||'#35d6a0').replace(/[^#a-zA-Z0-9(),.%\s-]/g,'');
    return '<svg viewBox="0 0 42 58" aria-hidden="true">'+
      '<g stroke="#081018" stroke-width="2" stroke-linejoin="round">'+
        '<rect x="10" y="20" width="22" height="32" rx="4" fill="'+safe+'"/>'+
        '<rect x="8" y="5" width="26" height="18" rx="6" fill="'+safe+'"/>'+
        '<path d="M12 8h18l-2 7H14z" fill="#9bd7f2"/>'+
        '<rect x="13" y="25" width="16" height="4" rx="2" fill="#0d1822" opacity=".75"/>'+
        '<rect x="13" y="44" width="16" height="4" rx="2" fill="#0d1822" opacity=".75"/>'+
        '<rect x="5" y="10" width="5" height="10" rx="2" fill="#0a0d12"/>'+
        '<rect x="32" y="10" width="5" height="10" rx="2" fill="#0a0d12"/>'+
        '<rect x="6" y="27" width="4" height="10" rx="2" fill="#0a0d12"/>'+
        '<rect x="32" y="27" width="4" height="10" rx="2" fill="#0a0d12"/>'+
        '<rect x="6" y="41" width="4" height="9" rx="2" fill="#0a0d12"/>'+
        '<rect x="32" y="41" width="4" height="9" rx="2" fill="#0a0d12"/>'+
        '<path d="M13 18h5v3h-5zm11 0h5v3h-5z" fill="#f7fbff" stroke-width="1"/>'+
      '</g>'+
      '<path d="M21 1l-4 5h8z" fill="#fff" opacity=".9"/>'+
    '</svg>';
  }

  window.GAT_TRUCK_COLOR=truckColor;

  markerIcon=function(d){
    const moving=typeof fresh==='function'?fresh(d.t):!!d?.t?.on_job;
    const deg=typeof headingDeg==='function'?headingDeg(numberValue(d.t,'map_heading','truck.placement.heading')):0;
    const color=truckColor(d);
    const label=typeof esc==='function'?esc(d.name):String(d.name||'Motorista');
    return L.divIcon({
      className:'gat-truck-icon',
      iconSize:[44,44],
      iconAnchor:[22,22],
      popupAnchor:[0,-20],
      html:'<div class="gat-custom-truck-marker '+(moving?'':'idle')+'" style="--truck-color:'+color+'">'+
        '<div class="gat-custom-truck-rotor" style="transform:rotate('+deg.toFixed(1)+'deg)">'+svgTruck(color)+'</div>'+
        '<span class="gat-truck-label">'+label+'</span>'+
      '</div>'
    });
  };
})();
