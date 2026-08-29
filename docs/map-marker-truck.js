// GAT-LOG • marcador de caminhão com cor por motorista.
// O motorista Xuxa recebe rosa fixo; os demais recebem uma cor estável baseada no usuário.
(function(){
  if(typeof L==='undefined')return;

  const PALETTE=['#35d6a0','#4aa8ed','#ffad3d','#9b7cff','#ef5b5b','#2ec4b6','#f07ccf','#7bd389','#54c6eb','#f6bd60'];
  const XUXA_PINK='#ff2f9b';

  function normalize(value){
    return String(value||'').trim().toLowerCase();
  }

  function driverIdentity(d){
    return normalize(d?.t?.account_user||d?.name||'motorista');
  }

  function hashString(text){
    let h=2166136261;
    for(let i=0;i<text.length;i++){
      h^=text.charCodeAt(i);
      h=Math.imul(h,16777619);
    }
    return h>>>0;
  }

  function truckColor(d){
    const id=driverIdentity(d);
    if(id==='xuxa'||id.includes('xuxa'))return XUXA_PINK;
    return PALETTE[hashString(id)%PALETTE.length];
  }

  function svgTruck(color){
    const safe=String(color||'#35d6a0').replace(/[^#a-zA-Z0-9(),.%\s-]/g,'');
    return '<svg viewBox="0 0 96 96" aria-hidden="true">'+
      '<g stroke="#101722" stroke-width="3" stroke-linejoin="round">'+
        '<path fill="#171d27" d="M35 59h26l6 23H29z"/>'+
        '<rect x="25" y="65" width="12" height="18" rx="5" fill="#080b10"/>'+
        '<rect x="59" y="65" width="12" height="18" rx="5" fill="#080b10"/>'+
        '<path fill="'+safe+'" d="M24 20Q24 11 33 8h30q9 3 9 12v41q0 7-7 7H31q-7 0-7-7z"/>'+
        '<path fill="#111924" d="M29 25q2-9 9-11h20q7 2 9 11l-3 13H32z"/>'+
        '<path fill="#6fb4d8" d="M33 26q2-7 7-8h16q5 1 7 8l-2 8H35z"/>'+
        '<rect x="30" y="41" width="36" height="6" rx="2" fill="#0d131b"/>'+
        '<rect x="34" y="50" width="28" height="4" rx="2" fill="#121923"/>'+
        '<rect x="38" y="57" width="20" height="3" rx="1.5" fill="#191f28"/>'+
        '<rect x="21" y="30" width="6" height="16" rx="3" fill="'+safe+'"/>'+
        '<rect x="69" y="30" width="6" height="16" rx="3" fill="'+safe+'"/>'+
        '<path fill="#f8fbff" d="M29 54h8v5h-8zm30 0h8v5h-8z" stroke-width="1.6"/>'+
      '</g>'+
      '<path d="M48 3l-5 8h10z" fill="#fff" opacity=".92"/>'+
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
      iconSize:[58,58],
      iconAnchor:[29,29],
      popupAnchor:[0,-24],
      html:'<div class="gat-custom-truck-marker '+(moving?'':'idle')+'" style="--truck-color:'+color+'">'+
        '<div class="gat-custom-truck-rotor" style="transform:rotate('+deg.toFixed(1)+'deg)">'+svgTruck(color)+'</div>'+
        '<span class="gat-truck-label">'+label+'</span>'+
      '</div>'
    });
  };
})();
