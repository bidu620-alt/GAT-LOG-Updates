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
// The YouTube channel page can use several metadata layouts; never rely on one renderer.
export function extractChannelId(html){
 const initial=readAssignedObject(html,'ytInitialData');
 const meta=findMetadata(initial);
 const candidates=[
  meta?.externalId,
  initial?.metadata?.channelMetadataRenderer?.externalId,
  initial?.header?.c4TabbedHeaderRenderer?.channelId,
  html.match(/<meta[^>]*itemprop=["']channelId["'][^>]*content=["'](UC[A-Za-z0-9_-]{22})["']/i)?.[1],
  html.match(/["']externalChannelId["']\s*:\s*["'](UC[A-Za-z0-9_-]{22})["']/)?.[1],
  html.match(/["']externalId["']\s*:\s*["'](UC[A-Za-z0-9_-]{22})["']/)?.[1]
 ];
 return candidates.find(id=>/^UC[A-Za-z0-9_-]{22}$/.test(id||''))||null;
}
// The /live player may demand login even when the public /streams channel tab is available.
// Only explicit LIVE badges on videos belonging to the verified channel tab count.
export function findLiveInChannelTab(initial){
 if(!initial||typeof initial!=='object')return null;
 const seen=new WeakSet();
 function hasLiveBadge(item){
  if(!item||typeof item!=='object')return false;
  if(item.isLiveNow===true)return true;
  const items=[...(item.badges||[]),...(item.thumbnailOverlays||[]),...(item.overlays||[])];
  return items.some(b=>{
   const badge=b?.metadataBadgeRenderer;
   if(badge?.style==='BADGE_STYLE_TYPE_LIVE_NOW')return true;
   if(b?.thumbnailOverlayTimeStatusRenderer?.style==='LIVE')return true;
   const tag=b?.thumbnailOverlayBadgeViewModel||b?.badgeViewModel;
   const label=tag?.text?.content||tag?.text||tag?.label||'';
   return typeof label==='string'&&/^(LIVE|AO VIVO)$/i.test(label.trim());
  });
 }
 function walk(node){
  if(!node||typeof node!=='object'||seen.has(node))return null;
  seen.add(node);
  for(const item of [node.videoRenderer,node.gridVideoRenderer,node.lockupViewModel]){
   if(!item)continue;
   const id=item.videoId||item.contentId;
   if(/^[A-Za-z0-9_-]{11}$/.test(id||'')&&!item.upcomingEventData&&hasLiveBadge(item)){
    const title=item.title?.runs?.map(x=>x.text).join('')||item.metadata?.lockupMetadataViewModel?.title?.content||'';
    return {video_id:id,title:String(title).slice(0,180)};
   }
  }
  for(const value of Object.values(node)){
   if(value&&typeof value==='object'){
    if(Array.isArray(value)){for(const child of value){const found=walk(child);if(found)return found}}
    else {const found=walk(value);if(found)return found}
   }
  }
  return null;
 }
 return walk(initial);
}
export function ownerMatchesHandle(player,handle){
 if(!handle||!player)return false;
 const micro=player.microformat?.playerMicroformatRenderer||{};
 return [micro.ownerProfileUrl,micro.ownerChannelUrl].some(raw=>{
  if(!raw)return false;
  try{
   const u=new URL(raw,'https://www.youtube.com');
   return /^(?:www\.|m\.)?youtube\.com$/.test(u.hostname)&&decodeURIComponent(u.pathname).replace(/\/$/,'').toLowerCase()==='/@'+handle.toLowerCase();
  }catch{return false}
 });
}
export function classifyYouTube(player,channelId,handle=''){
 const actual=player?.videoDetails?.channelId;
 if(!/^UC[A-Za-z0-9_-]{22}$/.test(actual||'')||
    (channelId&&actual!==channelId)||
    (!channelId&&!ownerMatchesHandle(player,handle)))
  return {state:'unknown',reason:'channel_not_verified'};
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
 const r=await fetch(url,{signal:AbortSignal.timeout(12000),headers:{'User-Agent':'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36','Accept-Language':'en-US,en;q=0.9'}});
 if(!r.ok)throw Error('provider_http_'+r.status);return json?r.json():r.text();
}
async function youtube(channel){
 const html=await response(`https://www.youtube.com/@${channel.handle}/streams`);
 const metadata=findMetadata(readAssignedObject(html,'ytInitialData'));
 const channelId=extractChannelId(html);
 const tabLive=channelId?findLiveInChannelTab(readAssignedObject(html,'ytInitialData')):null;
 const vanity=metadata?.vanityChannelUrl||'';
 if(channel.user==='gensey'){
  const title=html.split('<title>')[1]?.split('</title>')[0]?.slice(0,120)||'';
  console.log('youtube_streams_diagnostic',JSON.stringify({user:channel.user,bytes:html.length,title,hasInitialData:!!readAssignedObject(html,'ytInitialData'),hasMetadata:!!metadata,channelId:channelId||null,vanity:vanity||null,hasExternalChannelId:html.includes('externalChannelId'),tabLiveId:tabLive?.video_id||null}));
 }
 if(tabLive) return {state:'live',video_id:tabLive.video_id,title:tabLive.title,channel_id:channelId};
 // Reject a channel page that explicitly identifies a different handle.
 if(vanity){
  try{
   const url=new URL(vanity);
   const handlePath=decodeURIComponent(url.pathname).replace(/\/$/,'').toLowerCase();
   if(handlePath.startsWith('/@')&&handlePath!=='/@'+channel.handle.toLowerCase())
    return {state:'unknown',reason:'channel_identity_mismatch'};
  }catch{return {state:'unknown',reason:'invalid_channel_url'}}
 }
 if(process.env.YOUTUBE_DATA_API_KEY&&channelId){
  try{
   // Optional official API lookup; a missing key or quota error must not block the public fallback.
   const key=encodeURIComponent(process.env.YOUTUBE_DATA_API_KEY);
   const uploads=await response(`https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&playlistId=UU${channelId.slice(2)}&maxResults=10&key=${key}`,true);
   const ids=(uploads.items||[]).map(x=>x.contentDetails?.videoId).filter(x=>/^[A-Za-z0-9_-]{11}$/.test(x)).join(',');
   if(ids){
    const videos=await response(`https://www.googleapis.com/youtube/v3/videos?part=snippet,liveStreamingDetails,status&id=${ids}&key=${key}`,true);
    const live=(videos.items||[]).find(v=>v.snippet?.channelId===channelId&&v.snippet.liveBroadcastContent==='live'&&v.liveStreamingDetails?.actualStartTime&&!v.liveStreamingDetails.actualEndTime&&v.status?.embeddable===true);
    if(live)return {state:'live',video_id:live.id,channel_id:channelId,title:live.snippet.title};
   }
  }catch(err){console.warn('YouTube API fallback for',channel.user,err.message)}
 }
 // /live opens the active broadcast; verify its owner before publishing the video.
 const liveHtml=await response(`https://www.youtube.com/@${channel.handle}/live`);
 const player=readAssignedObject(liveHtml,'ytInitialPlayerResponse');
 const status=classifyYouTube(player,channelId,channel.handle);
 if(channel.user==='gensey'){
  const m=player?.microformat?.playerMicroformatRenderer||{};
  console.log('youtube_live_diagnostic',JSON.stringify({user:channel.user,bytes:liveHtml.length,hasPlayer:!!player,playerStatus:player?.playabilityStatus?.status,ownerProfileUrl:m.ownerProfileUrl||null,playerChannelId:player?.videoDetails?.channelId||null,videoId:player?.videoDetails?.videoId||null,liveDetails:m.liveBroadcastDetails||null,reason:status.reason||null}));
 }
 return {...status,channel_id:channelId||player?.videoDetails?.channelId||undefined};
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

