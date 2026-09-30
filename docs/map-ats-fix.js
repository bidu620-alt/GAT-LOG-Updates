// GAT-LOG • compatibilidade do ATS legado em ZIP.
// Quando o ATS usa tiles diretos no Cloudflare R2, este patch não interfere.
(function(){
  const cfg=(window.GAT_MAP_CONFIG||{}).ats||{};
  if(cfg.type!=='gat-zip-tiles')return;
  if(typeof applyLayerForMap!=='function')return;
  const previousApplyLayerForMap=applyLayerForMap;

  applyLayerForMap=function(){
    if(typeof currentMap!=='undefined'&&currentMap==='ats'&&activeVisualKey==='gat-ats-zip')return;
    previousApplyLayerForMap();
    if(typeof currentMap!=='undefined'&&currentMap==='ats')activeVisualKey='gat-ats-zip';
  };
})();
