import {readFile,writeFile} from 'node:fs/promises';
import {pathToFileURL} from 'node:url';
const API='https://api.bidulog.com.br';
const validUser=/^[a-z0-9_-]{1,40}$/i,validHandle=/^[a-z0-9_.-]{1,100}$/i;
export function readAssignedObject(html,name){
 const marker=new RegExp('(?:var\\s+)?'+name+'\\s*=\\s*');
 const match=marker.exec(html);if(!match)return null;
 const start=html.indexOf('{',match.index+match[0].length);if(start<0)return null;
 let depth=0,quoted=false,escape=false;
 for(let i=start;i<html.length;i++){
  const c=html[i];if(quoted){if(escape)escape=false;else if(c==='\\')escape=true;else if(c==='"')quoted=false;continue;}
  if(c==='"'){quoted=true;continue;}if(c==='{')depth++;if(c==='}'&&--depth===0){try{return JSON.parse(html.slice(start,i+1))}catch{return null}}
 }
 return null;
}
export function findMetadata(value){
 if(!value||typeof value!=='object')return null;
 if(value.channelMetadataRenderer)return value.channelMetadataRenderer;
 for(const child of Object.values(value)){if(child&&typeof child==='object'){const found=findMetadata(child);if(found)return found}}
 return null;
}
export function classifyYouTube(player,channelId){
 if(!player||player.videoDetails?.channelId!==channelId)return {state:'unknown',reason:'channel_not_verified'};
 const broadcast=player.microformat?.playerMicroformatRenderer?.liveBroadcastDetails;
 const id=player.videoDetails?.videoId;
 if(broadcast?.isLiveNow===true&&!broadcast.endTimestamp&&/^[A-Za-z0-9_-]{11}$/.test(id)&&player.playabilityStatus?.status==='OK')
  return {state:'live',video_id:id,title:String(player.videoDetails.title||'').slice(0,180)};
 if(broadcast?.isLiveNow===false||broadcast?.endTimestamp)return {state:'offline'};
 return {state:'unknown',reason:'live_not_confirmed'};
}
export function classifyTwitch(text,handle){
 const s=text.trim();if(s.toLowerCase()===`${handle} is offline`.toLowerCase())return {state:'offline'};
 if(/^(?:\d+\s+(?:years?|months?|weeks?|days?|hours?|minutes?|seconds?)(?:,?\s+|$))+$/i.test(s))return {state:'live'};
 return {state:'unknown',reason:'provider_unavailable'};
}
async function response(url,json=false){
 const r=await fetch(url,{signal:AbortSignal.timeout(12000),headers:{'User-Agent':'BiduLog-StreamDirectory/1.0','Accept-Language':'en-US,en;q=0.9'}});
 if(!r.ok)throw Error('provider_http_'+r.status);return json?r.json():r.text();
}
async function youtube(channel){
 const html=await response(`https://www.youtube.com/@${channel.handle}/streams`);
 const metadata=findMetadata(readAssignedObject(html,'ytInitialData'));
 const channelId=metadata?.externalId;
 if(!/^UC[A-Za-z0-9_-]{22}$/.test(channelId||''))return {state:'unknown',reason:'channel_not_verified'};
 const vanity=metadata.vanityChannelUrl||'';
 if(!vanity.toLowerCase().endsWith('/@'+channel.handle.toLowerCase()))return {state:'unknown',reason:'channel_identity_mismatch'};
 if(process.env.YOUTUBE_DATA_API_KEY){
  // The uploads playlist is inexpensive to poll; verify broadcast state on videos.list.
  const key=encodeURIComponent(process.env.YOUTUBE_DATA_API_KEY);
  const uploads=await response(`https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&playlistId=UU${channelId.slice(2)}&maxResults=10&key=${key}`,true);
  const ids=(uploads.items||[]).map(x=>x.contentDetails?.videoId).filter(x=>/^[A-Za-z0-9_-]{11}$/.test(x)).join(',');
  if(ids){
   const videos=await response(`https://www.googleapis.com/youtube/v3/videos?part=snippet,liveStreamingDetails,status&id=${ids}&key=${key}`,true);
   const live=(videos.items||[]).find(v=>v.snippet?.channelId===channelId&&v.snippet.liveBroadcastContent==='live'&&v.liveStreamingDetails?.actualStartTime&&!v.liveStreamingDetails.actualEndTime&&v.status?.embeddable===true);
   if(live)return {state:'live',video_id:live.id,channel_id:channelId,title:live.snippet.title};
  }
 }
 const liveHtml=await response(`https://www.youtube.com/@${channel.handle}/live`);
 const player=readAssignedObject(liveHtml,'ytInitialPlayerResponse');
 return {...classifyYouTube(player,channelId),channel_id:channelId};
}
export async function collect(channels){
 let enabled;
 try{const rank=await response(API+'/api/public/ranking',true);if(!rank.ok)throw Error();enabled=new Set((rank.ranking||[]).map(x=>x.user));}catch{return channels.map(c=>({...c,state:'unknown',reason:'central_unavailable'}))}
 return Promise.all(channels.map(async c=>{
  if(!validUser.test(c.user)||!validHandle.test(c.handle)||!['youtube','twitch'].includes(c.platform))throw Error('Invalid channel configuration');
  try{
   const data=await response(API+'/api/public/driver?user='+encodeURIComponent(c.user),true);
   const p=data.profile;
   if(!data.ok||p?.user!==c.user||!enabled.has(c.user)||!(p.role==='streamer'||c.user==='biduzao'&&p.role==='owner'))return {...c,state:'ineligible'};
   const status=c.platform==='youtube'?await youtube(c):classifyTwitch(await response('https://decapi.me/twitch/uptime/'+c.handle),c.handle);
   return {...c,...status,checked_at:new Date().toISOString()};
  }catch{return {...c,state:'unknown',reason:'provider_unavailable',checked_at:new Date().toISOString()}}
 }));
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const config=JSON.parse(await readFile(new URL('../docs/streamer-channels.json',import.meta.url),'utf8'));
 const channels=await collect(config.channels);
 const result={version:1,checked_at:new Date().toISOString(),channels};
 await writeFile(new URL('../docs/streamer-status.json',import.meta.url),JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify(channels.map(({user,state,reason})=>({user,state,reason}))));
}

