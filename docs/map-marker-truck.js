// GAT-LOG • caminhão lateral compacto no mapa, com cor por motorista.
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
    return '<svg viewBox="0 0 72 40" aria-hidden="true">'+
      '<g stroke="#091019" stroke-width="1.8" stroke-linejoin="round" stroke-linecap="round">'+
        '<path d="M28 22h31l6 5v5H28z" fill="#1a222d"/>'+
        '<path d="M30 24h29l4 3H30z" fill="#344252"/>'+
        '<rect x="28" y="20" width="25" height="5" rx="2" fill="#202b37"/>'+
        '<path d="M7 13l5-8h13l8 7v18H7z" fill="'+safe+'"/>'+
        '<path d="M12 7h11l6 6H10z" fill="#89cce7"/>'+
        '<path d="M9 14h9v7H8z" fill="#0e1822"/>'+
        '<path d="M20 14h9v7h-9z" fill="#172634"/>'+
        '<rect x="8" y="23" width="22" height="4" rx="2" fill="#111923"/>'+
        '<path d="M6 28h27v4H6z" fill="#0f1720"/>'+
        '<rect x="4" y="17" width="4" height="7" rx="2" fill="'+safe+'"/>'+
        '<rect x="31" y="16" width="3" height="7" rx="1.5" fill="'+safe+'"/>'+
        '<path d="M8 25h5v3H8zm15 0h6v3h-6z" fill="#f7fbff" stroke-width=".8"/>'+
        '<circle cx="15" cy="32" r="6" fill="#05080c"/>'+
        '<circle cx="15" cy="32" r="2.6" fill="#697787"/>'+
        '<circle cx="42" cy="32" r="6" fill="#05080c"/>'+
        '<circle cx="42" cy="32" r="2.6" fill="#697787"/>'+
        '<circle cx="57" cy="32" r="6" fill="#05080c"/>'+
        '<circle cx="57" cy="32" r="2.6" fill="#697787"/>'+
      '</g>'+
      '<path d="M5 11L1 14l5 1z" fill="#fff" opacity=".9"/>'+
      '<path d="M11 9h11" stroke="#fff" stroke-width="1" opacity=".28"/>'+
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
      iconSize:[62,46],
      iconAnchor:[31,23],
      popupAnchor:[0,-19],
      html:'<div class="gat-custom-truck-marker '+(moving?'':'idle')+'" style="--truck-color:'+color+'">'+
        '<div class="gat-custom-truck-rotor" style="transform:rotate('+deg.toFixed(1)+'deg)">'+svgTruck(color)+'</div>'+
        '<span class="gat-truck-label">'+label+'</span>'+
      '</div>'
    });
  };
})();
