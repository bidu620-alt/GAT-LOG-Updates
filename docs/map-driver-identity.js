// GAT-LOG • identidade visual do motorista no mapa.
// Prioriza o usuário da conta GAT; o nome interno do perfil do ETS2 fica apenas como fallback.
(function(){
  if(typeof build!=='function')return;

  build=function(data){
    const tel=Array.isArray(data?.telemetry)?data.telemetry:[],seen=new Set(),out=[];
    tel.filter(fresh).forEach(t=>{
      const account=String(t.account_user||'').trim();
      const driver=String(t.driver||'').trim();
      const name=account||driver||'Motorista';
      const k=norm(account||driver||name);
      if(!k||seen.has(k))return;
      seen.add(k);
      out.push({server:CENTRAL.label,name,t,map:mapKey(t)});
    });
    return out;
  };
})();
