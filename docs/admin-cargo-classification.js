(()=>{
  // GAT Server 1.0.55: cargas sao abertas e nunca dependem de classificacao.
  // Mantemos este arquivo como stub para clientes com HTML em cache, sem criar painel,
  // consultar fila ou oferecer acao de classificacao.
  const removeLegacy=()=>{
    document.getElementById('cargoClassificationPanel')?.remove();
    document.getElementById('cargoClassificationStyle')?.remove();
  };
  document.addEventListener('DOMContentLoaded',removeLegacy);
  window.addEventListener('gat-account-change',removeLegacy);
})();
