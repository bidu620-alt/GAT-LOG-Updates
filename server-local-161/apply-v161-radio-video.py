from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/runtime')
worker_path = root / 'worker.js'
worker = worker_path.read_text(encoding='utf-8')
MARKER = 'GAT_RADIO_VIDEO_V161'

if MARKER not in worker:
    if "const VERSION='1.0.59-local';" not in worker:
        raise SystemExit('Backend Radio 1.0.59 nao encontrado antes do patch 1.0.61.')
    worker = worker.replace("const VERSION='1.0.59-local';", "const VERSION='1.0.61-local';", 1)

    start = worker.find('function gatRadioPlaylistId(value){')
    end = worker.find('async function gatRadioState(env){', start)
    if start < 0 or end < 0:
        raise SystemExit('Nao encontrei helper da Radio GAT 1.0.59.')

    helpers = r'''// GAT_RADIO_VIDEO_V161 - uma fonte pode ser playlist ou video individual do YouTube.
function gatRadioValidPlaylistId(id){return /^[A-Za-z0-9_-]{10,120}$/.test(String(id||''))}
function gatRadioValidVideoId(id){return /^[A-Za-z0-9_-]{11}$/.test(String(id||''))}
function gatRadioParse(value){
 const raw=String(value||'').trim();
 if(!raw)return{type:'',id:'',url:''};
 try{
  const x=new URL(raw),host=x.hostname.toLowerCase().replace(/^www\./,'');
  if(!['youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(host))return{type:'',id:'',url:''};
  const path=x.pathname.replace(/^\/+|\/+$/g,''),list=String(x.searchParams.get('list')||'').trim(),v=String(x.searchParams.get('v')||'').trim();
  if(host==='youtu.be'){
   const id=path.split('/')[0];
   return gatRadioValidVideoId(id)?{type:'video',id,url:`https://www.youtube.com/watch?v=${id}`}:{type:'',id:'',url:''};
  }
  if(path==='playlist'&&gatRadioValidPlaylistId(list))return{type:'playlist',id:list,url:`https://www.youtube.com/playlist?list=${list}`};
  if(path==='watch'&&gatRadioValidVideoId(v))return{type:'video',id:v,url:`https://www.youtube.com/watch?v=${v}`};
  const parts=path.split('/');
  if(['shorts','live','embed'].includes(parts[0])&&gatRadioValidVideoId(parts[1]))return{type:'video',id:parts[1],url:`https://www.youtube.com/watch?v=${parts[1]}`};
  if(gatRadioValidPlaylistId(list))return{type:'playlist',id:list,url:`https://www.youtube.com/playlist?list=${list}`};
 }catch{}
 return{type:'',id:'',url:''};
}
function gatRadioPublic(row){
 const parsed=gatRadioParse(row?.playlist_url||'');
 const legacyId=String(row?.playlist_id||'');
 const type=parsed.type||(legacyId?'playlist':'');
 const id=parsed.id||legacyId;
 const url=parsed.url||String(row?.playlist_url||'');
 return{enabled:!!Number(row?.enabled||0),source:'youtube',source_type:type,source_url:url,video_id:type==='video'?id:'',playlist_url:type==='playlist'?url:'',playlist_id:type==='playlist'?id:'',label:String(row?.label||'Radio GAT'),revision:Number(row?.revision||0),updated_by:String(row?.updated_by||''),updated_at:String(row?.updated_at||'')};
}
'''
    worker = worker[:start] + helpers + worker[end:]

    route_start = worker.find(" if(p==='/api/site/admin/radio'&&m==='POST'){")
    route_end = worker.find(" if(p==='/api/site/admin/session'&&m==='POST')", route_start)
    if route_start < 0 or route_end < 0:
        raise SystemExit('Nao encontrei rota admin da Radio GAT 1.0.59.')

    admin_route = r''' if(p==='/api/site/admin/radio'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),current=await gatRadioState(env);
  const rawUrl=String(b.source_url!==undefined?b.source_url:(b.playlist_url!==undefined?b.playlist_url:(current.source_url||current.playlist_url||''))).trim();
  const parsed=gatRadioParse(rawUrl);
  const enabled=b.enabled!==undefined?(b.enabled===true||b.enabled===1||String(b.enabled).toLowerCase()==='true'):current.enabled;
  if(rawUrl&&!parsed.id)throw new HttpError(400,'invalid_youtube_url');
  if(enabled&&!parsed.id)throw new HttpError(400,'source_required');
  const sourceUrl=parsed.id?parsed.url:'',sourceId=parsed.id||'',label=String(b.label||current.label||'Radio GAT').trim().slice(0,80)||'Radio GAT',t=now();
  await env.DB.prepare('INSERT INTO radio_state(id,enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at) VALUES(1,?,?,?,?,1,?,?) ON CONFLICT(id) DO UPDATE SET enabled=excluded.enabled,playlist_url=excluded.playlist_url,playlist_id=excluded.playlist_id,label=excluded.label,revision=radio_state.revision+1,updated_by=excluded.updated_by,updated_at=excluded.updated_at').bind(enabled?1:0,sourceUrl,sourceId,label,s.user,t).run();
  await audit(env,s.user,'radio_update','radio',{enabled:!!enabled,source_type:parsed.type,source_id:sourceId,label});
  return json(req,{ok:true,radio:await gatRadioState(env)});
 }
'''
    worker = worker[:route_start] + admin_route + worker[route_end:]

required = [
    MARKER,
    "const VERSION='1.0.61-local'",
    'function gatRadioParse(value)',
    "source_type:type",
    "source_url:url",
    "video_id:type==='video'?id:''",
    "b.source_url!==undefined",
    "invalid_youtube_url",
    "source_required",
]
for item in required:
    if item not in worker:
        raise SystemExit('Patch Radio 1.0.61 incompleto: ' + item)

worker_path.write_text(worker, encoding='utf-8')
print('GAT Server 1.0.61: Radio GAT aceita video individual ou playlist do YouTube, preservando compatibilidade com 1.0.59.')
