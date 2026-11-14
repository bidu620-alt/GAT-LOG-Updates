(()=>{'use strict';
 const root=document.getElementById('bidulog-streamers');if(!root)return;
 const list=root.querySelector('[data-stream-list]'),stage=root.querySelector('[data-stream-stage]'),summary=root.querySelector('[data-stream-summary]');
 const names={twitch:'Twitch',youtube:'YouTube'},MAX_AGE=20*60*1000;
 const JULIANO_ID='uLKxfxqM8BY',JULIANO_URL='https://www.youtube.com/watch?v='+JULIANO_ID;
 let config=[],channels=[],selected='',frameKey='',loading=false,refreshQueued=false;
 let configUpdatedAt=0,hiddenAt=0,playerReloadQueued=false;
 function node(tag,className,text){const n=document.createElement(tag);if(className)n.className=className;if(text)n.textContent=text;return n}

 function fresh(c){const age=Date.now()-Date.parse(c.checked_at||'');return Number.isFinite(age)&&age>=-60000&&age<MAX_AGE}
 function isLive(c){return c.state==='live'&&fresh(c)}
 function videoAvailable(c){return /^[A-Za-z0-9_-]{11}$/.test(c.featured_video_id||'')}
 function channelLink(c){
  const a=node('a','streamer-channel-link','Abrir canal ↗');
  a.href=c.url;a.target='_blank';a.rel='noopener noreferrer';return a;
 }
 function eligible(p,c){return p?.user===c.user&&(p.role==='streamer'||c.user==='biduzao'&&p.role==='owner')}
 function embed(c){
  if(isLive(c)&&c.platform==='twitch')return 'https://player.twitch.tv/?'+new URLSearchParams({channel:c.handle,parent:location.hostname,muted:'true',autoplay:'true'});
  const id=isLive(c)?c.video_id:c.featured_video_id;
  if(c.platform==='youtube'&&/^[A-Za-z0-9_-]{11}$/.test(id||''))return 'https://www.youtube.com/embed/'+id+'?autoplay=1&mute=1&playsinline=1';
  return '';
 }
 function draw(){
  const live=channels.filter(isLive),viewable=channels.filter(c=>isLive(c)||videoAvailable(c));
  summary.textContent=live.length?live.length+' motorista'+(live.length>1?'s':'')+' ao vivo':'Acompanhe os canais da comunidade';
  // Always prioritize a verified live broadcast over an old selection/recording.
  // Keep a manually selected live channel if several are broadcasting.
  if(live.length&&!live.some(c=>c.user===selected))selected=live[0].user;
  else if(!viewable.some(c=>c.user===selected))selected=viewable[0]?.user||'';
  const previousScroll=list.scrollTop;
  list.replaceChildren();
  for(const c of [...channels].sort((a,b)=>Number(isLive(b))-Number(isLive(a)))){
   const card=node('article','streamer-channel'+(c.user===selected?' selected':''));
   const top=node('div','streamer-channel-top'),avatar=node('span','streamer-avatar',c.name.charAt(0));
   const identity=node('div','streamer-identity');identity.append(node('strong','',c.name),node('small','',names[c.platform]));top.append(avatar,identity);
   const active=isLive(c),video=!active&&videoAvailable(c),label=active?'● AO VIVO':video?'▶ VÍDEO DISPONÍVEL':c.state==='offline'?'Offline':'Canal cadastrado';
   top.append(node('span','streamer-state'+(active?' is-live':video?' is-video':''),label));card.append(top);
   const actions=node('div','streamer-actions');
   if(active||video){
    const button=node('button','streamer-watch',c.user===selected?'Assistindo':video?'Assistir vídeo':'Assistir aqui');
    button.type='button';button.setAttribute('aria-pressed',String(c.user===selected));
    button.addEventListener('click',()=>{selected=c.user;draw()});actions.append(button);
   }
   actions.append(channelLink(c));card.append(actions);list.append(card);
  }
  list.scrollTop=previousScroll;
  const current=viewable.find(c=>c.user===selected);
  const url=current?embed(current):'',key=(current?.user||'')+'|'+(current&&isLive(current)?'live':'video')+'|'+url;
  // A background tab may suspend or dispose of an iframe without changing its URL.
  const reload=playerReloadQueued;playerReloadQueued=false;
  if(key===frameKey&&!reload&&stage.hasChildNodes())return;
  frameKey=key;stage.replaceChildren();
  if(!current||!url){
   const empty=node('div','streamer-empty');empty.append(node('span','streamer-empty-icon','▶'),node('h3','','A próxima viagem pode ser ao vivo.'),node('p','','Quando uma transmissão for confirmada, você poderá assistir aqui. Enquanto isso, conheça os canais ao lado.'));stage.append(empty);return;
  }
  const liveNow=isLive(current),header=node('div','streamer-player-heading');
  header.append(node('span','streamer-state '+(liveNow?'is-live':'is-video'),liveNow?'● AO VIVO':'▶ VÍDEO'),node('strong','',current.name),node('span','',names[current.platform]));stage.append(header);
  const wrap=node('div','streamer-player '+(current.platform==='twitch'?'is-twitch':''));
  const iframe=node('iframe');iframe.src=url;iframe.title=(liveNow?'Live de ':'Vídeo de ')+current.name+' no '+names[current.platform];iframe.allow='autoplay; fullscreen; picture-in-picture; encrypted-media';iframe.allowFullscreen=true;iframe.referrerPolicy='strict-origin-when-cross-origin';wrap.append(iframe);stage.append(wrap);
  const fallback=node('div','streamer-player-fallback');fallback.append(node('span','',liveNow?'Se o player não abrir, assista diretamente no canal.':'Vídeo da comunidade. Uma gravação não é contabilizada como transmissão ao vivo.'),channelLink(current));stage.append(fallback);
 }
 async function json(url){const r=await fetch(url,{cache:'no-store',signal:AbortSignal.timeout(8000)});if(!r.ok)throw Error();return r.json()}
 const validUser=/^[a-z0-9_-]{1,40}$/i,validHandle=/^[a-z0-9_.-]{1,100}$/i;
 function valid(c){
  return validUser.test(c?.user||'')&&validHandle.test(c?.handle||'')&&
    ((c.platform==='twitch'&&c.url==='https://www.twitch.tv/'+c.handle)||
     (c.platform==='youtube'&&c.url==='https://www.youtube.com/@'+c.handle+'/streams'));
 }
 async function channelConfig(){
  // Do not block a tab return on the channel registry every time; update it periodically.
  if(config.length&&Date.now()-configUpdatedAt<5*60*1000)return config;
  const fixed=await json('streamer-channels.json?v=20261114-juliano-2');
  const merged=new Map((fixed.channels||[]).filter(valid).map(c=>[c.user,c]));
  try{
   const directory=await json('https://streamers.bidulog.com.br/api/channels?t='+Math.floor(Date.now()/60000));
   if(directory.ok)for(const c of directory.channels||[]){
    if(!valid(c))continue;
    const previous=merged.get(c.user);
    merged.set(c.user,{...c,name:previous?.name||c.user});
   }
  }catch{} // A failed registry never removes the configured community channels.
  configUpdatedAt=Date.now();return [...merged.values()];
 }
 async function publicStatus(){
  // GitHub Pages uses a custom domain; try published aliases so a single failed
  // origin cannot make all active drivers look offline.
  const paths=[
   'https://gatlogets2.com.br/streamer-status.json',
   'https://bidu620-alt.github.io/GAT-LOG-Updates/streamer-status.json',
   'streamer-status.json'
  ],stamp=Math.floor(Date.now()/30000);
  return Promise.any(paths.map(async url=>{
   const data=await json(url+(url.includes('?')?'&':'?')+'t='+stamp);
   if(!Array.isArray(data?.channels)||!data.checked_at)throw Error('invalid_live_status');
   return data;
  }));
 }
 async function refresh(){
  if(loading){refreshQueued=true;return;}
  loading=true;refreshQueued=false;
  try{
   // Fetch the latest channel status in parallel instead of waiting for the registry.
   const [nextConfig,data]=await Promise.all([
    channelConfig().catch(()=>config),
    publicStatus().catch(()=>null)
   ]);
   if(nextConfig.length)config=nextConfig;
   const previous=channels;
   channels=await Promise.all(config.map(async c=>{
    const status=data?.channels?.find(x=>x.user===c.user&&x.platform===c.platform&&x.handle===c.handle);
    const prior=previous.find(x=>x.user===c.user&&x.platform===c.platform&&x.handle===c.handle);
    // Retain only a recently verified status when the public feed is inaccessible.
    let state=status&&fresh(status)?status.state:(!data&&prior&&fresh(prior)?prior.state:'unknown');
    let checkedAt=status?.checked_at||(!data?prior?.checked_at:null);
    let videoId=status?.video_id||(!data?prior?.video_id:null);
    if(c.platform==='twitch'){
     try{
      const response=await fetch('https://decapi.me/twitch/uptime/'+encodeURIComponent(c.handle),{signal:AbortSignal.timeout(8000),cache:'no-store'});
      if(response.ok){
       const text=(await response.text()).trim();
       if(text.toLowerCase()===(c.handle+' is offline').toLowerCase()){state='offline';checkedAt=new Date().toISOString()}
       else if(/^(?:\d+\s+(?:years?|months?|weeks?|days?|hours?|minutes?|seconds?)(?:,?\s+|$))+$/i.test(text)){state='live';checkedAt=new Date().toISOString()}
      }
     }catch{}
    }
    const featured=c.user==='julianos'?{featured_video_id:JULIANO_ID,featured_video_url:JULIANO_URL}:{};
    return {...c,...featured,name:c.name||c.user,state,checked_at:checkedAt,video_id:videoId};
   }));
   root.hidden=!channels.length;draw();
  }catch{
   // Keep the last known channels visible even if a request times out.
   if(channels.length){root.hidden=false;draw();}
   else root.hidden=true;
  }finally{
   loading=false;
   if(refreshQueued&&!document.hidden){refreshQueued=false;refresh();}
  }
 }
 function resume(){
  if(document.hidden)return;
  // If the browser suspended the player while in another tab, restore it.
  if(hiddenAt&&Date.now()-hiddenAt>20000)playerReloadQueued=true;
  hiddenAt=0;
  draw(); // Immediately update stale labels while the background request starts.
  refresh();
 }

 refresh();setInterval(()=>{if(!document.hidden)refresh();else if(channels.some(c=>c.state==='live'&&!fresh(c)))draw()},30000);
 document.addEventListener('visibilitychange',()=>{if(document.hidden){hiddenAt=Date.now();return;}resume()});
 window.addEventListener('focus',resume);
 window.addEventListener('pageshow',e=>{if(e.persisted)playerReloadQueued=true;resume()});
})();

