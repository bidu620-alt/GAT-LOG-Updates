from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/runtime')
worker_path = root / 'worker.js'
worker = worker_path.read_text(encoding='utf-8')
MARKER = 'GAT_RADIO_STREAM_V162'

if MARKER not in worker:
    if "const VERSION='1.0.61-local';" not in worker:
        raise SystemExit('Backend Radio 1.0.61 nao encontrado antes do patch 1.0.62.')
    worker = worker.replace("const VERSION='1.0.61-local';", "const VERSION='1.0.62-local';", 1)

    start = worker.find('// GAT_RADIO_VIDEO_V161')
    end = worker.find('async function gatRadioState(env){', start)
    if start < 0 or end < 0:
        raise SystemExit('Nao encontrei helpers da Radio GAT 1.0.61.')

    helpers = r'''// GAT_RADIO_STREAM_V162 - YouTube (video/playlist) + radio online por URL direta MP3/AAC.
function gatRadioValidPlaylistId(id){return /^[A-Za-z0-9_-]{10,120}$/.test(String(id||''))}
function gatRadioValidVideoId(id){return /^[A-Za-z0-9_-]{11}$/.test(String(id||''))}
function gatRadioLooksPlaylistId(id){const s=String(id||'').toUpperCase();return /^(PL|UU|LL|FL|OL|RD)/.test(s)}
function gatRadioParse(value){
 const raw=String(value||'').trim();
 if(!raw)return{type:'',id:'',url:''};
 if(gatRadioValidVideoId(raw))return{type:'video',id:raw,url:`https://www.youtube.com/watch?v=${raw}`};
 if(gatRadioValidPlaylistId(raw)&&gatRadioLooksPlaylistId(raw))return{type:'playlist',id:raw,url:`https://www.youtube.com/playlist?list=${raw}`};
 let candidate=raw;
 if(/^(www\.|youtube\.com|youtu\.be)/i.test(candidate))candidate='https://'+candidate;
 try{
  const x=new URL(candidate);
  if(!['http:','https:'].includes(x.protocol))return{type:'',id:'',url:''};
  if(x.username||x.password)return{type:'',id:'',url:''};
  const host=x.hostname.toLowerCase().replace(/^www\./,'');
  const isYoutube=['youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(host);
  if(isYoutube){
   const path=x.pathname.replace(/^\/+|\/+$/g,''),list=String(x.searchParams.get('list')||'').trim(),v=String(x.searchParams.get('v')||'').trim();
   // Se um link watch tiver tambem list=, prioriza a playlist completa.
   if(gatRadioValidPlaylistId(list)&&gatRadioLooksPlaylistId(list))return{type:'playlist',id:list,url:`https://www.youtube.com/playlist?list=${list}`};
   if(host==='youtu.be'){
    const id=path.split('/')[0];
    return gatRadioValidVideoId(id)?{type:'video',id,url:`https://www.youtube.com/watch?v=${id}`}:{type:'',id:'',url:''};
   }
   if(path==='watch'&&gatRadioValidVideoId(v))return{type:'video',id:v,url:`https://www.youtube.com/watch?v=${v}`};
   const parts=path.split('/');
   if(['shorts','live','embed'].includes(parts[0])&&gatRadioValidVideoId(parts[1]))return{type:'video',id:parts[1],url:`https://www.youtube.com/watch?v=${parts[1]}`};
   return{type:'',id:'',url:''};
  }
  // A Central nao baixa nem retransmite o audio; apenas distribui a URL aos clientes.
  const url=x.href;
  return{type:'stream',id:url,url};
 }catch{}
 return{type:'',id:'',url:''};
}
function gatRadioPublic(row){
 const parsed=gatRadioParse(row?.playlist_url||'');
 const legacyId=String(row?.playlist_id||'');
 const type=parsed.type||(legacyId?'playlist':'');
 const id=parsed.id||legacyId;
 const url=parsed.url||String(row?.playlist_url||'');
 const provider=type==='stream'?'radio_online':'youtube';
 return{enabled:!!Number(row?.enabled||0),source:provider,source_type:type,source_url:url,stream_url:type==='stream'?url:'',video_id:type==='video'?id:'',playlist_url:type==='playlist'?url:'',playlist_id:type==='playlist'?id:'',label:String(row?.label||'Radio GAT'),revision:Number(row?.revision||0),updated_by:String(row?.updated_by||''),updated_at:String(row?.updated_at||'')};
}
'''
    worker = worker[:start] + helpers + worker[end:]

    worker = worker.replace("if(rawUrl&&!parsed.id)throw new HttpError(400,'invalid_youtube_url');", "if(rawUrl&&!parsed.id)throw new HttpError(400,'invalid_media_url');", 1)

required = [
    MARKER,
    "const VERSION='1.0.62-local'",
    'function gatRadioParse(value)',
    "type:'stream'",
    "stream_url:type==='stream'?url:''",
    "source:provider",
    "invalid_media_url",
    "gatRadioLooksPlaylistId",
]
for item in required:
    if item not in worker:
        raise SystemExit('Patch Radio Stream 1.0.62 incompleto: ' + item)

worker_path.write_text(worker, encoding='utf-8')
print('GAT Server 1.0.62: Canal GAT aceita YouTube e radio online por URL direta MP3/AAC.')
