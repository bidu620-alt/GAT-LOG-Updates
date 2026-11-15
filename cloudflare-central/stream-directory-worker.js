// BiduLog — independent public streamer directory.
// Owners authenticate against the existing Central; session tokens are never stored.
const CENTRAL = 'https://api.bidulog.com.br';
const ALLOWED = new Set(['https://bidulog.com.br','https://www.bidulog.com.br','https://gatlogets2.com.br']);
const JSON_HEADERS = {'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'};
const USER = /^[a-z0-9_-]{1,40}$/i;

function reply(data,status=200,origin=''){
 const h=new Headers(JSON_HEADERS);
 if(ALLOWED.has(origin)){h.set('Access-Control-Allow-Origin',origin);h.set('Vary','Origin');h.set('Access-Control-Allow-Methods','GET, POST, OPTIONS');h.set('Access-Control-Allow-Headers','Content-Type');}
 return new Response(JSON.stringify(data),{status,headers:h});
}
function parseChannel(value){
 let u;try{u=new URL(String(value||'').trim());}catch{return null;}
 if(u.protocol!=='https:' || u.username || u.password || u.search || u.hash)return null;
 const host=u.hostname.toLowerCase();
 if(['www.youtube.com','youtube.com','m.youtube.com'].includes(host)){
  const m=/^\/@([A-Za-z0-9_.-]{3,100})(?:\/streams)?\/?$/.exec(u.pathname);
  if(!m)return null;
  return {platform:'youtube',handle:m[1],url:'https://www.youtube.com/@'+m[1]+'/streams'};
 }
 if(['www.twitch.tv','twitch.tv'].includes(host)){
  const m=/^\/([A-Za-z0-9_]{3,25})\/?$/.exec(u.pathname);
  if(!m)return null;
  return {platform:'twitch',handle:m[1],url:'https://www.twitch.tv/'+m[1]};
 }
 return null;
}
async function fromCentral(path,method='GET',data){
 const response=await fetch(CENTRAL+path,{method,headers:{'Content-Type':'text/plain;charset=UTF-8','Accept':'application/json'},body:data?JSON.stringify(data):undefined,signal:AbortSignal.timeout(7500)});
 if(!response.ok)throw Error('central_unavailable');
 return response.json();
}

// Live status is calculated on demand in Cloudflare rather than waiting for
// GitHub Actions' best-effort (sometimes delayed) 10-minute cron.
const LIVE_CACHE_SECONDS = 90;
const STATIC_STREAMS = [
 {user:'biduzao',name:'Biduzao',platform:'twitch',handle:'bidugamess'},
 {user:'gensey',name:'Gensey',platform:'youtube',handle:'GenseyGamer'},
 {user:'jeanjc',name:'JeanJC',platform:'youtube',handle:'JcGamerOriginal'},
 {user:'diolinoledes',name:'Diolinoledes',platform:'youtube',handle:'diolinoledes'},
 {user:'julianos',name:'Juliano',platform:'youtube',handle:'JulianoSouza_lives-io9tp'}
];
const ID_VIDEO=/^[a-zA-Z0-9_-]{11}$/;
const ID_CHANNEL=/^UC[a-zA-Z0-9_-]{22}$/;
async function streamText(url){
 const r=await fetch(url,{headers:{'User-Agent':'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36','Accept-Language':'en-US,en;q=0.9'},signal:AbortSignal.timeout(6500)});
 if(!r.ok)throw Error('provider_http_'+r.status);
 return r.text();
}
function assignedObject(html,name){
 const m=new RegExp('(?:var\\s+)?'+name+'\\s*=\\s*').exec(html);
 if(!m)return null;
 const begin=html.indexOf('{',m.index+m[0].length);
 if(begin<0)return null;
 let depth=0,quote=false,escaped=false;
 for(let i=begin;i<html.length;i++){
  const ch=html[i];
  if(quote){if(escaped)escaped=false;else if(ch==='\\')escaped=true;else if(ch==='"')quote=false;continue;}
  if(ch==='"'){quote=true;continue;}
  if(ch==='{')depth++;
  if(ch==='}'&&--depth===0){try{return JSON.parse(html.slice(begin,i+1))}catch{return null;}}
 }
 return null;
}
function metadataObject(node){
 if(!node||typeof node!=='object')return null;
 if(node.channelMetadataRenderer)return node.channelMetadataRenderer;
 for(const child of Object.values(node)){if(child&&typeof child==='object'){const found=metadataObject(child);if(found)return found;}}
 return null;
}
function channelIdFromHtml(html,initial){
 const metadata=metadataObject(initial);
 return [metadata?.externalId,initial?.header?.c4TabbedHeaderRenderer?.channelId,
  html.match(/["']externalChannelId["']\s*:\s*["'](UC[A-Za-z0-9_-]{22})["']/)?.[1],
  html.match(/["']externalId["']\s*:\s*["'](UC[A-Za-z0-9_-]{22})["']/)?.[1],
  html.match(/<meta[^>]*itemprop=["']channelId["'][^>]*content=["'](UC[A-Za-z0-9_-]{22})["']/i)?.[1]
 ].find(v=>ID_CHANNEL.test(v||''))||null;
}
function liveVideoFromTab(initial){
 const visited=new WeakSet();
 function hasBadge(item){
  if(item.isLiveNow===true)return true;
  const badges=[...(item.badges||[]),...(item.thumbnailOverlays||[]),...(item.overlays||[])];
  return badges.some(b=>{
   if(b?.metadataBadgeRenderer?.style==='BADGE_STYLE_TYPE_LIVE_NOW')return true;
   if(b?.thumbnailOverlayTimeStatusRenderer?.style==='LIVE')return true;
   const v=b?.thumbnailOverlayBadgeViewModel||b?.badgeViewModel;
   const txt=v?.text?.content||v?.text||v?.label||'';
   return typeof txt==='string'&&/^(LIVE|AO VIVO)$/i.test(txt.trim());
  });
 }
 function walk(n){
  if(!n||typeof n!=='object'||visited.has(n))return null;
  visited.add(n);
  for(const item of [n.videoRenderer,n.gridVideoRenderer,n.lockupViewModel]){
   if(!item)continue;
   const id=item.videoId||item.contentId;
   if(ID_VIDEO.test(id||'')&&!item.upcomingEventData&&hasBadge(item))return id;
  }
  for(const val of Object.values(n))if(val&&typeof val==='object'){
   if(Array.isArray(val)){for(const x of val){const id=walk(x);if(id)return id;}}
   else {const id=walk(val);if(id)return id;}
  }
  return null;
 }
 return walk(initial);
}
function playerLive(player,channelId,handle){
 if(!player)return null;
 const details=player.videoDetails||{},micro=player.microformat?.playerMicroformatRenderer||{};
 if(!ID_CHANNEL.test(details.channelId||''))return null;
 if(channelId&&details.channelId!==channelId)return null;
 if(!channelId){
  const urls=[micro.ownerProfileUrl,micro.ownerChannelUrl].filter(Boolean);
  if(!urls.some(raw=>{
   try{return decodeURIComponent(new URL(raw,'https://www.youtube.com').pathname).replace(/\/$/,'').toLowerCase()==='/@'+handle.toLowerCase();}
   catch{return false;}
  }))return null;
 }
 const live=micro.liveBroadcastDetails;
 if(live?.isLiveNow===true&&!live.endTimestamp&&ID_VIDEO.test(details.videoId||'')&&player.playabilityStatus?.status==='OK')
  return {state:'live',video_id:details.videoId};
 if(live?.isLiveNow===false||live?.endTimestamp)return {state:'offline'};
 return null;
}
async function youtubeStatus(channel){
 const streams=await streamText('https://www.youtube.com/@'+encodeURIComponent(channel.handle)+'/streams');
 const initial=assignedObject(streams,'ytInitialData');
 const meta=metadataObject(initial);
 const channelId=channelIdFromHtml(streams,initial);
 if(!channelId)return {state:'unknown',reason:'channel_not_verified'};
 if(meta?.vanityChannelUrl){
  try{
   const vanity=new URL(meta.vanityChannelUrl);
   if(decodeURIComponent(vanity.pathname).replace(/\/$/,'').toLowerCase().startsWith('/@') &&
      decodeURIComponent(vanity.pathname).replace(/\/$/,'').toLowerCase()!=='/@'+channel.handle.toLowerCase())
    return {state:'unknown',reason:'channel_identity_mismatch'};
  }catch{}
 }
 const tabVideo=liveVideoFromTab(initial);
 if(tabVideo)return {state:'live',video_id:tabVideo,source:'verified_channel_tab'};
 // The /live page may require login for anonymous access; preserve the
 // independently-verified recent-live DecAPI fallback used by GitHub.
 let fromPlayer=null;
 try{const html=await streamText('https://www.youtube.com/@'+encodeURIComponent(channel.handle)+'/live');
  fromPlayer=playerLive(assignedObject(html,'ytInitialPlayerResponse'),channelId,channel.handle);
  if(fromPlayer?.state==='live')return {...fromPlayer,source:'youtube_player'};
 }catch{}
 const latest='https://decapi.me/youtube/latest_video?id='+encodeURIComponent(channelId)+'&format=%7Bid%7D';
 try{
  const [all,other]=await Promise.all([streamText(latest),streamText(latest+'&no_livestream=1')]);
  const a=all.trim(),b=other.trim();
  if(ID_VIDEO.test(a)&&ID_VIDEO.test(b)&&a!==b)
   return {state:'live',video_id:a,source:'decapi'};
 }catch{}
 return fromPlayer||{state:'unknown',reason:'live_not_confirmed'};
}
async function statusOf(c){
 try{
  if(c.platform==='youtube')return {...c,...await youtubeStatus(c)};
  const result=(await streamText('https://decapi.me/twitch/uptime/'+encodeURIComponent(c.handle))).trim();
  if(result.toLowerCase()===(c.handle+' is offline').toLowerCase())return {...c,state:'offline'};
  if(/^(?:\d+\s+(?:years?|months?|weeks?|days?|hours?|minutes?|seconds?)(?:,?\s+|$))+$/i.test(result))
   return {...c,state:'live'};
  return {...c,state:'unknown',reason:'provider_unavailable'};
 }catch(e){return {...c,state:'unknown',reason:'provider_unavailable'};}
}
async function publicStatus(env,ctx,origin){
 const cacheKey=new Request('https://streamers.bidulog.com.br/__public_status_v2');
 try{
  const cached=await caches.default.match(cacheKey);
  if(cached)return reply(await cached.json(),200,origin);
 }catch{}
 const channels=new Map(STATIC_STREAMS.map(c=>[c.user,c]));
 try{
  const stored=await env.DB.prepare('SELECT user,platform,handle,url FROM bidulog_stream_channels ORDER BY user').all();
  for(const c of stored.results||[]){
   const allowed=/^[a-z0-9_.-]{1,100}$/i.test(c.handle||'')&&USER.test(c.user||'')&&
    (c.platform==='youtube'||c.platform==='twitch');
   if(allowed)channels.set(c.user,{...channels.get(c.user),user:c.user,platform:c.platform,handle:c.handle});
  }
 }catch{}
 const entries=await Promise.all([...channels.values()].map(statusOf));
 const timestamp=new Date().toISOString();
 const result={version:2,checked_at:timestamp,channels:entries.map(c=>({...c,checked_at:timestamp}))};
 if(ctx?.waitUntil)ctx.waitUntil(caches.default.put(cacheKey,new Response(JSON.stringify(result),{
  headers:{'Content-Type':'application/json','Cache-Control':'public, max-age='+LIVE_CACHE_SECONDS}
 })).catch(()=>{}));
 return reply(result,200,origin);
}

export default {async fetch(request,env,ctx){
 const origin=request.headers.get('Origin')||'';
 const url=new URL(request.url);
 if(request.method==='OPTIONS')return ALLOWED.has(origin)?reply({ok:true},200,origin):reply({ok:false,error:'origin_denied'},403,origin);
 if(request.method==='GET'&&url.pathname==='/api/status')return publicStatus(env,ctx,origin);
 if(request.method==='GET'&&url.pathname==='/api/channels'){
  try{
   const r=await env.DB.prepare('SELECT user,platform,handle,url,updated_at FROM bidulog_stream_channels ORDER BY user').all();
   return reply({ok:true,channels:r.results||[]},200,origin);
  }catch{return reply({ok:false,error:'directory_unavailable'},503,origin);}
 }
 if(request.method==='GET'&&url.pathname==='/health')return reply({ok:true,service:'bidulog-streams'},200,origin);
 if(request.method==='POST'&&url.pathname==='/api/channel'){
  if(!ALLOWED.has(origin))return reply({ok:false,error:'origin_denied'},403,origin);
  try{
   const b=await request.json();
   const user=String(b.user||'').trim().toLowerCase();
   if(!USER.test(user))return reply({ok:false,error:'invalid_user'},400,origin);
   const token=String(b.token||'');
   if(token.length<20||token.length>400)return reply({ok:false,error:'invalid_session'},401,origin);
   let session;
   try{session=await fromCentral('/api/site/admin/session','POST',{token});}
   catch{return reply({ok:false,error:'session_unavailable'},503,origin);}
   if(!session.ok||session.role!=='owner')return reply({ok:false,error:'owner_required'},403,origin);
   const value=String(b.url||'').trim();
   if(!value){
    await env.DB.prepare('DELETE FROM bidulog_stream_channels WHERE user=?').bind(user).run();
    return reply({ok:true,user,removed:true},200,origin);
   }
   const channel=parseChannel(value);
   if(!channel)return reply({ok:false,error:'invalid_channel_url'},400,origin);
   let driver;
   try{driver=await fromCentral('/api/public/driver?user='+encodeURIComponent(user));}
   catch{return reply({ok:false,error:'driver_unavailable'},503,origin);}
   if(!driver.ok||driver.profile?.user!==user||!(driver.profile?.role==='streamer'||user==='biduzao'&&driver.profile?.role==='owner'))
    return reply({ok:false,error:'streamer_role_required'},409,origin);
   const when=new Date().toISOString();
   await env.DB.prepare('INSERT INTO bidulog_stream_channels(user,platform,handle,url,updated_at) VALUES(?,?,?,?,?) ON CONFLICT(user) DO UPDATE SET platform=excluded.platform,handle=excluded.handle,url=excluded.url,updated_at=excluded.updated_at')
    .bind(user,channel.platform,channel.handle,channel.url,when).run();
   return reply({ok:true,user,channel:{user,...channel,updated_at:when}},200,origin);
  }catch{return reply({ok:false,error:'save_failed'},500,origin);}
 }
 return reply({ok:false,error:'not_found'},404,origin);
}};