// GAT-LOG • estabiliza a camada ATS depois que o ZIP é ativado.
(function(){
  if(typeof applyLayerForMap!=='function')return;
  const previousApplyLayerForMap=applyLayerForMap;

  applyLayerForMap=function(){
    if(typeof currentMap!=='undefined'&&currentMap==='ats'&&activeVisualKey==='gat-ats-zip')return;
    previousApplyLayerForMap();
    if(typeof currentMap!=='undefined'&&currentMap==='ats')activeVisualKey='gat-ats-zip';
  };
})();
