(()=>{
  // O catálogo do motorista é apenas de progresso. As cargas são classificadas
  // automaticamente pela Central GAT ou encaminhadas ao Admin/Moderador.
  // Não exibir exemplos, sugestões ou catálogo de referência nos cartões.
  const removeSuggestions=()=>{
    document.querySelectorAll('#workCatalogGrid .cargo-compat-wrap').forEach(x=>x.remove());
  };
  document.addEventListener('DOMContentLoaded',removeSuggestions);
  const root=document.getElementById('workCatalogGrid');
  if(root)new MutationObserver(removeSuggestions).observe(root,{childList:true,subtree:true});
  setInterval(removeSuggestions,1000);
})();
