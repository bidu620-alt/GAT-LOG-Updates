(()=>{
  const PORTAL_SERVERS=[
    {label:'GAT CENTRAL CLOUD',url:'https://api.gatlogets2.com.br'},
  ];
  const PORTAL_LIVE='/api/public/live';
  const PORTAL_FRESH=20000;
  const NS='http://www.w3.org/2000/svg';

  const txt=(v,f='—')=>{const s=String(v??'').trim();return s||f;};
  const norm=v=>String(v||'').trim().toLocaleLowerCase('pt-BR');
  const age=v=>{const t=Date.parse(v||'');return Number.isFinite(t)?Date.now()-t:Infinity;};
  const fresh=t=>t&&age(t.updated_at)<=PORTAL_FRESH;
  const mk=(tag,cls,text)=>{const n=document.createElement(tag);if(cls)n.className=cls;if(text!==undefined)n.textContent=text;return n;};

  async function fetchPortalServer(server){
    const controller=new AbortController();
    const timer=setTimeout(()=>controller.abort(),4500);
    try{
      const r=await fetch(server.url+PORTAL_LIVE,{cache:'no-store',signal:controller.signal});
      if(!r.ok)throw new Error('HTTP '+r.status);
      const data=await r.json();
      if(!data||data.ok!==true)throw new Error('invalid');
      return {server,data};
    }catch(_){return {server,data:null};}
    finally{clearTimeout(timer);}
  }

  function buildPortalDrivers(results){
    const out=[];
    results.forEach(({server,data})=>{
      if(!data)return;
      const players=Array.isArray(data.players)?data.players:[];
      const telemetry=Array.isArray(data.telemetry)?data.telemetry:[];
      const byName=new Map(telemetry.map(t=>[norm(t.driver),t]));
      players.forEach(p=>out.push({server:server.label,endpoint:server.url,driver:p,t:byName.get(norm(p))||null}));
      telemetry.filter(t=>fresh(t)&&!players.some(p=>norm(p)===norm(t.driver))).forEach(t=>out.push({server:server.label,endpoint:server.url,driver:t.driver,t}));
    });
    const seen=new Set();
    return out.filter(d=>{const key=d.endpoint+'|'+norm(d.driver);if(seen.has(key))return false;seen.add(key);return true;});
  }

  function renderDriverDirectory(drivers){
    const root=document.getElementById('driverDirectoryGrid');
    const badge=document.getElementById('driverDirectoryCount');
    if(!root||!badge)return;
    root.textContent='';
    badge.textContent=drivers.length+' MOTORISTA'+(drivers.length===1?'':'S');
    if(!drivers.length){root.append(mk('article','live-placeholder','Nenhum motorista online neste momento.'));return;}

    drivers.forEach(d=>{
      const t=d.t,isFresh=fresh(t),onJob=isFresh&&Boolean(t.on_job);
      const card=mk('article','driver-profile'+(onJob?' route':''));
      const head=mk('div','driver-profile-head');
      const avatar=mk('div','driver-avatar',(txt(d.driver,'?').charAt(0)||'?').toUpperCase());
      const names=mk('div','driver-profile-name');
      names.append(mk('small','',d.server),mk('h3','',txt(d.driver,'Motorista')));
      head.append(avatar,names,mk('span','pill '+(onJob?'cyan':isFresh?'blue':'amber'),onJob?'EM ROTA':isFresh?'ONLINE':'SEM TELEMETRIA'));
      card.append(head);

      const account=mk('span','driver-account '+(isFresh&&t.account_user?'':'none'),isFresh&&t.account_user?'Conta GAT @'+t.account_user:'Conta GAT não vinculada');
      card.append(account);

      const info=mk('div','driver-profile-info');
      const server=mk('div');server.append(mk('small','','SERVIDOR'),mk('b','',d.server));
      const cargo=mk('div');cargo.append(mk('small','','CARGA'),mk('b','',isFresh?txt(t.cargo,'Sem carga'):'—'));
      info.append(server,cargo);card.append(info);

      const route=mk('div','driver-profile-route',onJob?txt(t.source,'Origem')+' → '+txt(t.destination,'Destino'):(isFresh?'Online • sem viagem ativa':'Aguardando telemetria'));
      card.append(route);
      root.append(card);
    });
  }

  function renderCompany(results,drivers){
    const onlineServers=results.filter(x=>x.data?.online).length;
    const activeTrips=drivers.filter(d=>fresh(d.t)&&d.t.on_job).length;
    const accounts=new Set(drivers.filter(d=>fresh(d.t)&&String(d.t.account_user||'').trim()).map(d=>norm(d.t.account_user)));
    const set=(id,v)=>{const e=document.getElementById(id);if(e)e.textContent=v;};
    set('companyDrivers',drivers.length);
    set('companyRoutes',activeTrips);
    set('companyServers',onlineServers);
    set('companyAccounts',accounts.size);
    const status=document.getElementById('companyStatus');
    if(status){
      if(!results.some(x=>x.data))status.textContent='Os GAT Servers não responderam agora. Os números serão atualizados automaticamente quando voltarem.';
      else status.textContent=drivers.length+' motorista'+(drivers.length===1?'':'s')+' online • '+activeTrips+' viagem'+(activeTrips===1?'':'s')+' em andamento • '+onlineServers+' de '+PORTAL_SERVERS.length+' servidores online.';
    }
  }

  function hashPoint(label,salt){
    let h=2166136261;
    const s=String(label||'rota')+'|'+salt;
    for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619);}
    const a=Math.abs(h>>>0);
    return {x:10+(a%78),y:10+(Math.floor(a/97)%42)};
  }

  function svgNode(tag,attrs){
    const n=document.createElementNS(NS,tag);
    Object.entries(attrs||{}).forEach(([k,v])=>n.setAttribute(k,String(v)));
    return n;
  }

  function renderRouteMap(drivers){
    const trips=drivers.filter(d=>fresh(d.t)&&d.t.on_job).slice(0,8);
    const root=document.getElementById('routeMap');
    const list=document.getElementById('mapRouteList');
    const badge=document.getElementById('mapTripCount');
    if(!root||!list||!badge)return;
    badge.textContent=trips.length+' ROTA'+(trips.length===1?'':'S');
    root.textContent='';list.textContent='';
    if(!trips.length){
      root.append(mk('div','map-empty','Aguardando uma viagem ativa...'));
      list.append(mk('div','map-route-empty','Nenhuma rota ativa neste momento.'));
      return;
    }

    const svg=svgNode('svg',{viewBox:'0 0 100 60',preserveAspectRatio:'none','aria-label':'Mapa esquemático das rotas GAT'});
    trips.forEach((d,i)=>{
      const t=d.t;
      let a=hashPoint(t.source,i+'a');
      let b=hashPoint(t.destination,i+'b');
      if(Math.abs(a.x-b.x)<9&&Math.abs(a.y-b.y)<7)b={x:Math.min(92,b.x+18),y:Math.min(52,b.y+10)};
      svg.append(svgNode('line',{x1:a.x,y1:a.y,x2:b.x,y2:b.y,class:'route-svg-line'}));
      svg.append(svgNode('circle',{cx:a.x,cy:a.y,r:1.25,class:'route-svg-origin'}));
      svg.append(svgNode('circle',{cx:b.x,cy:b.y,r:1.4,class:'route-svg-destination'}));
      const la=svgNode('text',{x:a.x+1.7,y:a.y-1.1,class:'route-svg-label'});la.textContent=txt(t.source,'Origem');svg.append(la);
      const lb=svgNode('text',{x:b.x+1.7,y:b.y+2.5,class:'route-svg-label'});lb.textContent=txt(t.destination,'Destino');svg.append(lb);
      const mid=svgNode('text',{x:(a.x+b.x)/2,y:(a.y+b.y)/2-1.3,class:'route-svg-driver'});mid.textContent=txt(t.account_user?'@'+t.account_user:d.driver,'Motorista');svg.append(mid);

      const item=mk('div','map-route-item');
      item.append(mk('span','route-driver',t.account_user?'@'+t.account_user:txt(d.driver,'Motorista')),mk('strong','',txt(t.source,'Origem')+' → '+txt(t.destination,'Destino')),mk('small','',txt(t.cargo,'Sem carga')+' • '+d.server));
      list.append(item);
    });
    root.append(svg);
  }

  async function refreshPortal(){
    const results=await Promise.all(PORTAL_SERVERS.map(fetchPortalServer));
    const drivers=buildPortalDrivers(results);
    renderDriverDirectory(drivers);
    renderCompany(results,drivers);
    renderRouteMap(drivers);
  }

  refreshPortal();
  setInterval(refreshPortal,6500);
})();
