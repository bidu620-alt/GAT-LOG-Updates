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
export default {async fetch(request,env){
 const origin=request.headers.get('Origin')||'';
 const url=new URL(request.url);
 if(request.method==='OPTIONS')return ALLOWED.has(origin)?reply({ok:true},200,origin):reply({ok:false,error:'origin_denied'},403,origin);
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