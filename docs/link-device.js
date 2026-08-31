(()=>{
  const panel=document.querySelector('[data-panel="overview"]');
  if(!panel)return;
  const card=document.createElement('article');
  card.className='driver-card';
  card.innerHTML='<div class="card-title"><div><span class="eyebrow">GAT TELEMETRIA</span><h2>Vincular este computador</h2></div></div><p class="lead">Abra o GAT Telemetria no computador, copie o código de 8 caracteres exibido e confirme aqui. O código expira em 10 minutos.</p><form id="deviceLinkForm" class="login-form"><div class="login-field"><label for="deviceLinkCode">CÓDIGO DE VINCULAÇÃO</label><input id="deviceLinkCode" maxlength="8" autocomplete="one-time-code" placeholder="A1B2C3D4" required></div><button class="btn primary" type="submit">VINCULAR COMPUTADOR</button></form><div id="deviceLinkStatus" class="data-note">Somente o computador vinculado à sua Conta GAT poderá enviar telemetria em seu nome.</div>';
  panel.appendChild(card);
  const form=card.querySelector('#deviceLinkForm'),input=card.querySelector('#deviceLinkCode'),status=card.querySelector('#deviceLinkStatus');
  input.addEventListener('input',()=>{input.value=input.value.toUpperCase().replace(/[^A-F0-9]/g,'').slice(0,8)});
  form.addEventListener('submit',async e=>{
    e.preventDefault();
    const session=typeof getSession==='function'?getSession():null,code=input.value.trim();
    if(!session?.token){status.textContent='Entre na sua Conta GAT antes de vincular.';return}
    if(!/^[A-F0-9]{8}$/.test(code)){status.textContent='Digite o código de 8 caracteres mostrado no GAT Telemetria.';return}
    const button=form.querySelector('button');button.disabled=true;status.textContent='Vinculando com segurança...';
    try{
      const r=await fetch('https://api.gatlogets2.com.br/api/site/client/link',{method:'POST',headers:{'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify({token:session.token,pairing_code:code})});
      const data=await r.json().catch(()=>({}));
      if(r.ok&&data.ok){status.textContent='Computador vinculado a @'+data.user+'. Volte ao GAT Telemetria; a conexão será concluída automaticamente.';input.value='';return}
      status.textContent=data.error==='pairing_not_found'?'Código inválido ou expirado. Gere um novo no GAT Telemetria.':data.error==='device_already_linked'?'Este computador já está vinculado a outra conta.':'Não foi possível vincular ('+(data.error||r.status)+').';
    }catch(_){status.textContent='A API GAT não respondeu. Tente novamente.'}finally{button.disabled=false}
  });
})();
