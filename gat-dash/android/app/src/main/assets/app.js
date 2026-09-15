(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const allowedRoles = new Set(['driver','moderator','admin','owner']);
  const SPEED_POINTS_KEY = 'gat_dash_speed_points_v1';
  const state = {
    user:'',role:'',telemetry:null,lastTelemetry:0,lastSpoken:0,lastLimit:0,
    voice:true,tolerance:3,host:'',platform:'web',
    speedPoints:loadSpeedPoints(),lastPos:null,lastObservedLimit:0,previewSpoken:{}
  };
  const native = window.chrome && window.chrome.webview ? 'windows' : (window.GatAndroid ? 'android' : 'web');
  state.platform = native;

  function nativePost(payload){
    try {
      if(native==='windows') window.chrome.webview.postMessage(payload);
      else if(native==='android') window.GatAndroid.postMessage(JSON.stringify(payload));
      else window.dispatchEvent(new CustomEvent('gat-native-message',{detail:payload}));
    } catch(e){ console.error(e); }
  }
  function setLoginStatus(text, cls=''){ const e=$('loginStatus'); e.textContent=text; e.className='login-status '+cls; }
  function showDash(){ $('loginScreen').classList.add('hidden'); $('dashScreen').classList.remove('hidden'); $('driverName').textContent=state.user+' • '+state.role; if(native==='android'&&!state.host) setTimeout(openSettings,350); }
  function showLogin(){ $('dashScreen').classList.add('hidden'); $('loginScreen').classList.remove('hidden'); }

  $('loginForm').addEventListener('submit',e=>{
    e.preventDefault();
    const user=$('loginUser').value.trim().toLowerCase(), password=$('loginPassword').value;
    if(!user||!password)return;
    $('loginButton').disabled=true; setLoginStatus('Entrando na Central GAT...');
    nativePost({type:'login',user,password});
  });

  window.gatDashLoginResult = function(result){
    $('loginButton').disabled=false;
    let r=result;
    if(typeof r==='string'){try{r=JSON.parse(r)}catch{r={ok:false,error:'invalid_response'}}}
    if(!r||!r.ok){const msg=r&&r.error==='invalid_credentials'?'Usuário ou senha incorretos.':r&&r.error==='too_many_attempts'?'Muitas tentativas. Aguarde alguns minutos.':'Não foi possível entrar na Central GAT.';setLoginStatus(msg,'error');return}
    if(!allowedRoles.has(String(r.role||'').toLowerCase())){setLoginStatus('Sua conta ainda não possui o cargo Motorista. Peça liberação ao ADM/Moderador.','error');return}
    state.user=r.user||''; state.role=String(r.role||'driver').toLowerCase(); setLoginStatus('Login autorizado.','ok'); $('loginPassword').value=''; showDash(); nativePost({type:'dashboardReady'});
  };

  window.gatDashLoginTransportError = function(message){ $('loginButton').disabled=false; setLoginStatus('Falha de conexão: '+(message||'Central indisponível'),'error'); };

  function fmt(n,d=0){n=Number(n);return Number.isFinite(n)?n.toFixed(d):'0'}
  function clamp(n,a,b){return Math.max(a,Math.min(b,n))}
  function first(obj,...keys){for(const k of keys){let v=obj; for(const p of k.split('.')) v=v&&v[p]; if(v!==undefined&&v!==null)return v}return undefined}
  function conditionFromWear(v){v=Number(v||0); if(v<=1.001)v*=100; return clamp(Math.round(100-v),0,100)}
  function formatEta(iso){if(!iso)return '—'; const d=new Date(iso); if(Number.isNaN(d.getTime()))return '—'; return d.toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'})}
  function gearText(t){const d=Number(first(t,'truck.displayedGear','Truck.DisplayedGear')||0); if(d<0)return 'R'+Math.abs(d); if(d===0)return 'N'; return 'D'+d}
  function setIndicator(id,on){$(id).classList.toggle('on',!!on)}

  function loadSpeedPoints(){
    try {
      const raw=localStorage.getItem(SPEED_POINTS_KEY); const arr=raw?JSON.parse(raw):[];
      return Array.isArray(arr)?arr.filter(p=>p&&Number.isFinite(Number(p.x))&&Number.isFinite(Number(p.z))&&Number(p.to)>0).slice(-800):[];
    } catch { return []; }
  }
  function saveSpeedPoints(){
    try { localStorage.setItem(SPEED_POINTS_KEY,JSON.stringify(state.speedPoints.slice(-800))); } catch {}
  }
  function normHeading(h){ h=Number(h||0); if(Math.abs(h)>1.5)h/=360; h%=1; if(h<0)h+=1; return h; }
  function headingDiff(a,b){ const d=Math.abs(normHeading(a)-normHeading(b)); return Math.min(d,1-d); }
  function getPosition(t){
    const x=Number(first(t,'truck.placement.x','Truck.Placement.X','truck.position.x','Truck.Position.X'));
    const z=Number(first(t,'truck.placement.z','Truck.Placement.Z','truck.position.z','Truck.Position.Z'));
    const h=normHeading(first(t,'truck.placement.heading','Truck.Placement.Heading','truck.position.heading','Truck.Position.Heading'));
    return Number.isFinite(x)&&Number.isFinite(z)?{x,z,h}:null;
  }
  function distance(a,b){const dx=a.x-b.x,dz=a.z-b.z;return Math.sqrt(dx*dx+dz*dz)}
  function learnSpeedPoint(pos,fromLimit,toLimit){
    const from=Math.round(Number(fromLimit||0)),to=Math.round(Number(toLimit||0));
    if(!pos||from<=0||to<=0||Math.abs(from-to)<2)return;
    let found=null;
    for(const p of state.speedPoints){
      if(Math.round(Number(p.to))!==to||headingDiff(p.h,pos.h)>.12)continue;
      if(distance(p,pos)<=80){found=p;break;}
    }
    if(found){
      const seen=Math.max(1,Number(found.seen||1));
      found.x=(Number(found.x)*seen+pos.x)/(seen+1); found.z=(Number(found.z)*seen+pos.z)/(seen+1);
      found.h=pos.h; found.from=from; found.to=to; found.seen=seen+1; found.updated=Date.now();
    } else {
      state.speedPoints.push({id:'sp-'+Date.now()+'-'+Math.random().toString(36).slice(2,7),x:pos.x,z:pos.z,h:pos.h,from,to,seen:1,updated:Date.now()});
      if(state.speedPoints.length>800)state.speedPoints=state.speedPoints.slice(-800);
    }
    saveSpeedPoints();
  }
  function canRepeatPreview(key,waitMs=600000){const last=Number(state.previewSpoken[key]||0);return Date.now()-last>waitMs}
  function markPreview(key){state.previewSpoken[key]=Date.now();}
  function speakPreview(text,key){
    if(!state.voice||!canRepeatPreview(key)||Date.now()-state.lastSpoken<3500)return false;
    state.lastSpoken=Date.now(); markPreview(key); nativePost({type:'speak',text}); return true;
  }
  function processPredictiveLimit(t,speed,limit,connected){
    if(!connected){state.lastPos=null;state.lastObservedLimit=0;return}
    const pos=getPosition(t); if(!pos)return;
    if(state.lastObservedLimit>0&&limit>0&&Math.abs(limit-state.lastObservedLimit)>=2){learnSpeedPoint(pos,state.lastObservedLimit,limit)}

    let best=null,bestDistance=Infinity;
    if(state.lastPos&&limit>0){
      const mvx=pos.x-state.lastPos.x,mvz=pos.z-state.lastPos.z,moved=Math.sqrt(mvx*mvx+mvz*mvz);
      if(moved>0.4){
        for(const p of state.speedPoints){
          const target=Number(p.to||0); if(target<=0||target>=limit-1)continue;
          if(headingDiff(pos.h,p.h)>.14)continue;
          const d=distance(pos,p); if(d<35||d>300)continue;
          const tx=Number(p.x)-pos.x,tz=Number(p.z)-pos.z; if(mvx*tx+mvz*tz<=0)continue;
          if(d<bestDistance){best=p;bestDistance=d;}
        }
      }
    }

    if(best){
      const target=Math.round(Number(best.to));
      const rounded=Math.max(50,Math.round(bestDistance/50)*50);
      $('gpsInstruction').textContent=`Limite ${target} km/h em ~${rounded} m`;
      if(bestDistance<=260&&bestDistance>115){
        speakPreview(`Atenção. Limite de ${target} quilômetros por hora em aproximadamente ${rounded} metros.`,`early:${best.id}`);
      } else if(bestDistance<=115&&speed>target+state.tolerance){
        speakPreview(`Reduza a velocidade. Limite de ${target} quilômetros por hora à frente.`,`near:${best.id}`,);
      }
    }

    state.lastPos=pos;
    if(limit>0)state.lastObservedLimit=limit;
  }

  window.gatDashPushTelemetry = function(payload){
    let t=payload; if(typeof t==='string'){try{t=JSON.parse(t)}catch{return}}
    if(!t||typeof t!=='object')return; state.telemetry=t; state.lastTelemetry=Date.now(); render(t);
  };
  window.gatDashTelemetryError = function(message){ $('telemetryStatus').textContent='TruckSim GPS: '+(message||'sem conexão'); $('gameDot').classList.remove('online'); $('gameStatus').textContent='ETS2 desconectado'; };

  function render(t){
    const truck=t.truck||t.Truck||{}, job=t.job||t.Job||{}, nav=t.navigation||t.Navigation||{}, game=t.game||t.Game||{};
    const speed=Math.abs(Number(first(t,'truck.speed','Truck.Speed')||0));
    const rpm=Number(first(t,'truck.engineRpm','Truck.EngineRpm')||0), rpmMax=Math.max(1,Number(first(t,'truck.engineRpmMax','Truck.EngineRpmMax')||2500));
    const fuel=Number(first(t,'truck.fuel','Truck.Fuel')||0), cap=Number(first(t,'truck.fuelCapacity','Truck.FuelCapacity')||0), fuelPct=cap>0?clamp(fuel/cap*100,0,100):0;
    const cruiseOn=!!first(t,'truck.cruiseControlOn','Truck.CruiseControlOn'), cruise=Number(first(t,'truck.cruiseControlSpeed','Truck.CruiseControlSpeed')||0);
    const limit=Number(first(t,'navigation.speedLimit','Navigation.SpeedLimit')||0), remainM=Number(first(t,'navigation.estimatedDistance','Navigation.EstimatedDistance')||0), remainKm=remainM/1000;
    const connected=!!first(t,'game.connected','Game.Connected');
    $('speedValue').textContent=Math.round(speed); $('rpmValue').textContent=Math.round(rpm).toLocaleString('pt-BR'); $('gearValue').textContent=gearText(t); $('gearMirror').textContent=gearText(t);
    $('speedGauge').style.setProperty('--gauge', clamp(speed/140*265,0,265)+'deg');
    $('cruiseValue').textContent=cruiseOn?Math.round(cruise)+' km/h':'OFF'; $('cruiseBox').style.opacity=cruiseOn?'1':'.55';
    $('fuelPct').textContent=Math.round(fuelPct)+'%'; $('fuelLiters').textContent=fmt(fuel,0)+' / '+fmt(cap,0)+' L';
    $('waterTemp').textContent=fmt(first(t,'truck.waterTemperature','Truck.WaterTemperature'),0)+'°C'; $('batteryVoltage').textContent=fmt(first(t,'truck.batteryVoltage','Truck.BatteryVoltage'),1)+' V';
    $('sourceCity').textContent=first(t,'job.sourceCity','Job.SourceCity')||'—'; $('destinationCity').textContent=first(t,'job.destinationCity','Job.DestinationCity')||'—';
    const tripDest=first(t,'job.destinationCity','Job.DestinationCity')||'Sem destino definido'; const tripCargoName=first(t,'job.cargo','Job.Cargo')||'Aguardando carga'; $('tripDestination').textContent=tripDest; $('tripCargo').textContent=tripCargoName; $('tripStatus').textContent=connected?'EM ANDAMENTO':'AGUARDANDO';
    $('cargoName').textContent=first(t,'job.cargo','Job.Cargo')||'Sem carga'; $('cargoMass').textContent=fmt(Number(first(t,'job.cargoMass','Job.CargoMass')||0)/1000,1)+' t';
    $('remainingKm').textContent=remainKm>0?fmt(remainKm,0)+' km':'0 km'; $('etaText').textContent=formatEta(first(t,'navigation.estimatedTime','Navigation.EstimatedTime')); $('tripDistance').textContent=remainKm>0?fmt(remainKm,0)+' km':'—'; $('tripRoad').textContent=limit>0?'LIMITE '+Math.round(limit)+' km/h':'GAT LOG • ETS2';
    $('gpsDestination').textContent=first(t,'job.destinationCity','Job.DestinationCity')||'Destino'; $('gpsDistance').textContent=remainKm>0?fmt(remainKm,0)+' km':'—'; $('gpsRemain').textContent=remainKm>0?fmt(remainKm,0)+' km':'—'; $('gpsEta').textContent='Chegada '+formatEta(first(t,'navigation.estimatedTime','Navigation.EstimatedTime'));
    $('gpsRoad').textContent=limit>0?'LIMITE '+Math.round(limit)+' km/h':'ROTA'; $('gpsInstruction').textContent=(first(t,'job.destinationCity','Job.DestinationCity')?'Siga para '+first(t,'job.destinationCity','Job.DestinationCity'):'Aguardando rota');
    $('speedLimitSign').textContent=limit>0?Math.round(limit):'—';
    const heading=Number(first(t,'truck.placement.heading','Truck.Placement.Heading')||0); $('truckArrow').style.transform=`translate(-50%,-50%) rotate(${heading*360}deg)`;
    setIndicator('highBeam',first(t,'truck.lightsBeamHighOn','Truck.LightsBeamHighOn')); setIndicator('blinkLeft',first(t,'truck.blinkerLeftOn','Truck.BlinkerLeftOn')); setIndicator('blinkRight',first(t,'truck.blinkerRightOn','Truck.BlinkerRightOn')); setIndicator('parkBrake',first(t,'truck.parkBrakeOn','Truck.ParkBrakeOn'));
    const wears=['Engine','Transmission','Cabin','Chassis','Wheels']; let sum=0,count=0;
    for(const name of wears){const raw=first(t,'truck.wear'+name,'Truck.Wear'+name); const c=raw===undefined?100:conditionFromWear(raw); $('wear'+name).textContent=c+'%'; sum+=c;count++;}
    const overall=Math.round(sum/count); $('overallCondition').textContent=overall+'%'; $('conditionText').textContent=overall>=95?'Sem avarias':overall>=80?'Desgaste leve':overall>=60?'Atenção ao caminhão':'Manutenção recomendada';
    $('gameDot').classList.toggle('online',connected); $('gameStatus').textContent=connected?(game.gameName||game.GameName||'ETS2 Conectado'):'ETS2 desconectado'; $('telemetryStatus').textContent='TruckSim GPS: '+(connected?'telemetria ativa':'servidor conectado • jogo aguardando');
    processPredictiveLimit(t,speed,limit,connected);
    applyRadar(speed,limit);
  }

  function applyRadar(speed,limit){
    const over=limit>0 && speed>limit+state.tolerance;
    $('overspeedAlert').classList.toggle('hidden',!over); $('gpsPanel').classList.toggle('overspeed',over); $('speedLimitSign').style.boxShadow=over?'0 0 28px #ff1f39':'0 0 14px #ff283d66';
    if(over && state.voice && Date.now()-state.lastSpoken>12000){state.lastSpoken=Date.now();state.lastLimit=limit; nativePost({type:'speak',text:`Atenção. Limite de ${Math.round(limit)} quilômetros por hora. Reduza a velocidade.`});}
  }

  function openSettings(){ $('settingsModal').classList.remove('hidden'); $('telemetryHost').value=state.host; $('voiceToggle').checked=state.voice; $('speedTolerance').value=String(state.tolerance); }
  function closeSettings(){ $('settingsModal').classList.add('hidden'); }
  $('settingsBtn').onclick=openSettings; $('closeSettings').onclick=closeSettings;
  $('saveSettings').onclick=()=>{state.host=$('telemetryHost').value.trim(); state.voice=$('voiceToggle').checked; state.tolerance=Number($('speedTolerance').value||3); nativePost({type:'setSettings',host:state.host,voice:state.voice,tolerance:state.tolerance}); closeSettings();};
  $('logoutBtn').onclick=()=>{state.user='';state.role='';nativePost({type:'logout'});closeSettings();showLogin();};
  window.gatDashSettings = function(s){if(!s)return;state.host=s.host||'';state.voice=s.voice!==false;state.tolerance=Number(s.tolerance??3);};
  window.gatDashNativeReady = function(platform,settings){state.platform=platform||state.platform;window.gatDashSettings(settings||{});};

  if(native==='web'){
    setLoginStatus('Esta interface precisa ser aberta pelo aplicativo GAT DASH para autenticar e receber a telemetria.','error'); $('loginButton').disabled=true;
  } else nativePost({type:'ready'});
})();
