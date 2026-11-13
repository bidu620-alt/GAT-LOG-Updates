(()=>{'use strict';
 const root=document.getElementById('bidulog-streamers');if(!root)return;
 const list=root.querySelector('[data-stream-list]'),stage=root.querySelector('[data-stream-stage]'),summary=root.querySelector('[data-stream-summary]');
 const names={twitch:'Twitch',youtube:'YouTube'},MAX_AGE=20*60*1000;
 let config=[],channels=[],selected='',frameKey='',loading=false;
 function node(tag,className,text){const n=document.createElement(tag);if(className)n.className=className;if(text)n.textContent=text;return n}
 function channelLink(c){const a=node('a','streamer-channel-link','Abrir canal ↗');a.href=c.url;a.target='_blank';a.rel='noopener noreferrer';return a}
 function fresh(c){const age=Date.now()-Date.parse(c.checked_at||'');return Number.isFinite(age)&&age>=-60000&&age<MAX_AGE}
 function eligible(p,c){return p?.user===c.user&&(p.role==='streamer'||c.user==='biduzao'&&p.role==='owner')}
 function embed(c){
  if(c.platform==='twitch')return 'https://player.twitch.tv/?'+new URLSearchParams({channel:c.handle,parent:location.hostname,muted:'true',autoplay:'true'});
  if(c.platform==='youtube'&&/^[A-Za-z0-9_-]{11}$/.test(c.video_id||''))return 'https://www.youtube.com/embed/'+c.video_id+'?autoplay=1&mute=1&playsinline=1';
  return '';
 }
 function draw(){
  const live=channels.filter(c=>c.state==='live'&&fresh(c));
  summary.textContent=live.length?live.length+' motorista'+(live.length>1?'s':'')+' ao vivo':'Acompanhe os canais da comunidade';
  if(!live.some(c=>c.user===selected))selected=live[0]?.user||'';
  list.replaceChildren();
  for(const c of [...channels].sort((a,b)=>(b.state==='live')-(a.state==='live'))){
   const card=node('article','streamer-channel'+(c.user===selected?' selected':''));
   const top=node('div','streamer-channel-top');const avatar=node('span','streamer-avatar',c.name.charAt(0));
   const identity=node('div','streamer-identity');identity.append(node('strong','',c.name),node('small','',names[c.platform]));top.append(avatar,identity);
   const active=c.state==='live'&&fresh(c),label=active?'● AO VIVO':c.state==='offline'?'Offline':'Canal cadastrado';
   top.append(node('span','streamer-state'+(active?' is-live':''),label));card.append(top);
   const actions=node('div','streamer-actions');
   if(active){const button=node('button','streamer-watch',c.user===selected?'Assistindo':'Assistir aqui');button.type='button';button.setAttribute('aria-pressed',String(c.user===selected));button.addEventListener('click',()=>{selected=c.user;draw()});actions.append(button)}
   actions.append(channelLink(c));card.append(actions);list.append(card);
  }
  const current=live.find(c=>c.user===selected);
  const url=current?embed(current):'',key=url||'empty';if(key===frameKey)return;frameKey=key;stage.replaceChildren();
  if(!current||!url){
   const empty=node('div','streamer-empty');empty.append(node('span','streamer-empty-icon','▶'),node('h3','','A próxima viagem pode ser ao vivo.'),node('p','','Quando uma transmissão for confirmada, você poderá assistir aqui. Enquanto isso, conheça os canais ao lado.'));stage.append(empty);return;
  }
  const header=node('div','streamer-player-heading');header.append(node('span','streamer-state is-live','● AO VIVO'),node('strong','',current.name),node('span','',names[current.platform]));stage.append(header);
  const wrap=node('div','streamer-player '+(current.platform==='twitch'?'is-twitch':''));
  const iframe=node('iframe');iframe.src=url;iframe.title='Live de '+current.name+' na '+names[current.platform];iframe.allow='autoplay; fullscreen; picture-in-picture; encrypted-media';iframe.allowFullscreen=true;iframe.referrerPolicy='strict-origin-when-cross-origin';wrap.append(iframe);stage.append(wrap);
  const fallback=node('div','streamer-player-fallback');fallback.append(node('span','','Se o player não abrir, assista diretamente no canal.'),channelLink(current));stage.append(fallback);
 }
 async function json(url){const r=await fetch(url,{cache:'no-store',signal:AbortSignal.timeout(8000)});if(!r.ok)throw Error();return r.json()}
 const validUser=/^[a-z0-9_-]{1,40}$/i,validHandle=/^[a-z0-9_.-]{1,100}$/i;
 function valid(c){
  return validUser.test(c?.user||'')&&validHandle.test(c?.handle||'')&&
    ((c.platform==='twitch'&&c.url==='https://www.twitch.tv/'+c.handle)||
     (c.platform==='youtube'&&c.url==='https://www.youtube.com/@'+c.handle+'/streams'));
 }
 async function channelConfig(){
  const fixed=await json('streamer-channels.json?v=20261112-1');
  const merged=new Map((fixed.channels||[]).filter(valid).map(c=>[c.user,c]));
  try{
   const directory=await json('https://streamers.bidulog.com.br/api/channels?t='+Math.floor(Date.now()/60000));
   if(directory.ok)for(const c of directory.channels||[]){
    if(!valid(c))continue;
    const previous=merged.get(c.user);
    merged.set(c.user,{...c,name:previous?.name||c.user});
   }
  }catch{} // The community's existing channels remain available during registry outages.
  return [...merged.values()];
 }
 async function refresh(){
  if(loading)return;loading=true;
  try{
   config=await channelConfig();
   let data;try{data=await json('https://bidu620-alt.github.io/GAT-LOG-Updates/streamer-status.json?t='+Math.floor(Date.now()/60000))}catch{data={channels:[]}}
   channels=(await Promise.all(config.map(async c=>{
    try{const account=await json('https://api.bidulog.com.br/api/public/driver?user='+encodeURIComponent(c.user));if(!account.ok||!eligible(account.profile,c))return null;
     const status=data.channels?.find(x=>x.user===c.user&&x.platform===c.platform&&x.handle===c.handle);
     let state=status&&fresh(status)?status.state:'unknown',checkedAt=status?.checked_at;
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
     return {...c,name:c.name||account.profile?.display_name||c.user,state,checked_at:checkedAt,video_id:status?.video_id};
    }catch{return null}
   }))).filter(Boolean);root.hidden=!channels.length;draw();
  }catch{root.hidden=true}finally{loading=false}
 }
 refresh();setInterval(()=>{if(!document.hidden)refresh();else if(channels.some(c=>c.state==='live'&&!fresh(c)))draw()},90000);
 document.addEventListener('visibilitychange',()=>{if(!document.hidden)refresh()});
})();

