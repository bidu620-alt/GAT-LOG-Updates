from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/runtime')
worker_path = root / 'worker.js'
schema_path = root / 'schema.sql'
worker = worker_path.read_text(encoding='utf-8')
schema = schema_path.read_text(encoding='utf-8')
MARKER = 'GAT_LIVE_MEDIA_V163'

def once(text, old, new, label):
    if old not in text:
        raise SystemExit('Nao encontrei ' + label)
    return text.replace(old, new, 1)

if MARKER not in worker:
    worker = once(worker, "const VERSION='1.0.62-local';", "const VERSION='1.0.63-local';", 'versao 1.0.62')

    # Programacao oficial da radio e locucao ficam somente para owner/admin.
    radio_admin_old = "if(p==='/api/site/admin/radio'&&m==='POST'){\\n  const b=await body(req),s=await requireAdmin(req,env,b),current=await gatRadioState(env);"
    radio_admin_new = "if(p==='/api/site/admin/radio'&&m==='POST'){\\n  const b=await body(req),s=await requireAdmin(req,env,b,true),current=await gatRadioState(env);"
    worker = once(worker, radio_admin_old, radio_admin_new, 'permissao administrativa da Radio GAT')

    # WebView2 local HTTPS origin used by the live-media page.
    if "'https://gatlive.gatlogets2.local'" not in worker:
        worker = worker.replace(
            "const ORIGINS=new Set(['https://gatlogets2.com.br','https://www.gatlogets2.com.br','https://bidu620-alt.github.io']);",
            "const ORIGINS=new Set(['https://gatlogets2.com.br','https://www.gatlogets2.com.br','https://bidu620-alt.github.io','https://gatlive.gatlogets2.local']);",
            1
        )

    old_state = "async function gatRadioState(env){\n const row=await env.DB.prepare('SELECT enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at FROM radio_state WHERE id=1').first();\n return gatRadioPublic(row);\n}"
    new_state = """async function gatRadioState(env){
 const row=await env.DB.prepare('SELECT enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at FROM radio_state WHERE id=1').first();
 const out=gatRadioPublic(row);
 const live=await env.DB.prepare('SELECT source_revision,video_id,position_seconds,position_at FROM radio_runtime WHERE id=1').first();
 if(live&&Number(live.source_revision||-1)===Number(out.revision||0)){
  out.live_video_id=String(live.video_id||'');
  out.live_position_seconds=Math.max(0,Number(live.position_seconds||0));
  out.live_position_at=String(live.position_at||'');
 }else{
  out.live_video_id='';
  out.live_position_seconds=0;
  out.live_position_at='';
 }
 return out;
}"""
    worker = once(worker, old_state, new_state, 'gatRadioState 1.0.62')

    route_anchor = "async function route(req,env){const u=new URL(req.url),p=u.pathname,m=req.method;"
    helpers = r'''
// GAT_LIVE_MEDIA_V163 - sinalizacao WebRTC privada para Radio GAT ao vivo e transmissao opcional de rota.
function gatLiveKind(v){const k=String(v||'').trim().toLowerCase();return k==='route'||k==='radio_mic'?k:''}
function gatLiveQuality(v){const q=String(v||'').trim().toLowerCase();return q==='economico'?'economico':'normal'}
function gatLiveRow(x){return{stream_id:String(x?.stream_id||''),user:String(x?.user||''),kind:String(x?.kind||''),label:String(x?.label||''),quality:String(x?.quality||'normal'),created_at:String(x?.created_at||''),updated_at:String(x?.updated_at||'')}}
async function gatLiveGet(env,id){
 return await env.DB.prepare('SELECT stream_id,user,kind,label,quality,created_at,updated_at,active FROM live_streams WHERE stream_id=?').bind(String(id||'')).first();
}
async function gatLiveRequireFresh(env,id){
 const x=await gatLiveGet(env,id);
 if(!x||!Number(x.active)||Date.now()-Date.parse(x.updated_at||0)>25000)throw new HttpError(404,'stream_not_live');
 return x;
}
'''
    worker = once(worker, route_anchor, helpers + "\n" + route_anchor, 'inicio do roteador')

    admin_anchor = " if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}"
    routes = r''' if(p==='/api/site/admin/radio/position'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),videoId=String(b.video_id||'').trim(),seconds=Number(b.position_seconds);
  if(!/^[A-Za-z0-9_-]{11}$/.test(videoId)||!Number.isFinite(seconds)||seconds<0||seconds>86400)throw new HttpError(400,'invalid_radio_position');
  const radio=await env.DB.prepare('SELECT revision FROM radio_state WHERE id=1').first(),t=now();
  await env.DB.prepare('INSERT INTO radio_runtime(id,source_revision,video_id,position_seconds,position_at,updated_by) VALUES(1,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET source_revision=excluded.source_revision,video_id=excluded.video_id,position_seconds=excluded.position_seconds,position_at=excluded.position_at,updated_by=excluded.updated_by').bind(Number(radio?.revision||0),videoId,seconds,t,s.user).run();
  return json(req,{ok:true,video_id:videoId,position_seconds:seconds,position_at:t});
 }
 if(p==='/api/live/start'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),kind=gatLiveKind(b.kind);
  if(!kind)throw new HttpError(400,'invalid_stream_kind');
  if(kind==='radio_mic'&&!POWER.has(s.role))throw new HttpError(403,'forbidden');
  const quality=gatLiveQuality(b.quality),label=String(b.label|| (kind==='radio_mic'?'Locucao GAT':'Rota ao vivo')).trim().slice(0,80),t=now(),id=randomHex(12);
  if(kind==='radio_mic')await env.DB.prepare("UPDATE live_streams SET active=0,updated_at=? WHERE kind='radio_mic' AND active=1").bind(t).run();
  await env.DB.prepare('UPDATE live_streams SET active=0,updated_at=? WHERE user=? AND kind=? AND active=1').bind(t,s.user,kind).run();
  await env.DB.prepare('INSERT INTO live_streams(stream_id,user,kind,label,quality,created_at,updated_at,active) VALUES(?,?,?,?,?,?,?,1)').bind(id,s.user,kind,label,quality,t,t).run();
  return json(req,{ok:true,stream:gatLiveRow({stream_id:id,user:s.user,kind,label,quality,created_at:t,updated_at:t})});
 }
 if(p==='/api/live/heartbeat'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),id=String(b.stream_id||''),t=now();
  const r=await env.DB.prepare('UPDATE live_streams SET updated_at=? WHERE stream_id=? AND user=? AND active=1').bind(t,id,s.user).run();
  if(!Number(r?.changes||r?.meta?.changes||0))throw new HttpError(404,'stream_not_live');
  return json(req,{ok:true,updated_at:t});
 }
 if(p==='/api/live/stop'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),id=String(b.stream_id||''),x=await gatLiveGet(env,id);
  if(!x)throw new HttpError(404,'stream_not_found');
  if(x.user!==s.user&&!POWER.has(s.role))throw new HttpError(403,'forbidden');
  await env.DB.prepare('UPDATE live_streams SET active=0,updated_at=? WHERE stream_id=?').bind(now(),id).run();
  return json(req,{ok:true});
 }
 if(p==='/api/live/list'&&m==='POST'){
  const b=await body(req);await requireSession(req,env,b);
  const cutoff=new Date(Date.now()-25000).toISOString();
  const r=await env.DB.prepare('SELECT stream_id,user,kind,label,quality,created_at,updated_at FROM live_streams WHERE active=1 AND updated_at>=? ORDER BY kind,user').bind(cutoff).all();
  return json(req,{ok:true,streams:(r.results||[]).map(gatLiveRow),updated_at:now()});
 }
 if(p==='/api/live/signal'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),id=String(b.stream_id||''),target=clean(b.target),type=String(b.type||'').trim().toLowerCase(),payload=b.payload;
  if(!target||!['offer','answer'].includes(type)||!payload||typeof payload!=='object')throw new HttpError(400,'invalid_signal');
  const stream=await gatLiveRequireFresh(env,id);
  if(s.user===stream.user){if(target===s.user)throw new HttpError(400,'invalid_target')}
  else if(target!==stream.user)throw new HttpError(403,'invalid_target');
  const raw=JSON.stringify(payload);if(raw.length>90000)throw new HttpError(413,'signal_too_large');
  await env.DB.prepare('INSERT INTO live_signals(stream_id,sender,target,type,payload_json,created_at) VALUES(?,?,?,?,?,?)').bind(id,s.user,target,type,raw,now()).run();
  return json(req,{ok:true});
 }
 if(p==='/api/live/poll'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),id=String(b.stream_id||'');await gatLiveRequireFresh(env,id);
  const r=await env.DB.prepare('SELECT id,sender,type,payload_json,created_at FROM live_signals WHERE stream_id=? AND target=? ORDER BY id LIMIT 20').bind(id,s.user).all(),items=[];
  for(const x of (r.results||[])){let payload={};try{payload=JSON.parse(x.payload_json||'{}')}catch{}items.push({id:Number(x.id),sender:String(x.sender||''),type:String(x.type||''),payload,created_at:String(x.created_at||'')})}
  if(items.length){const ids=items.map(x=>x.id).filter(x=>Number.isFinite(x));for(const sid of ids)await env.DB.prepare('DELETE FROM live_signals WHERE id=? AND target=?').bind(sid,s.user).run()}
  return json(req,{ok:true,signals:items});
 }
'''
    worker = once(worker, admin_anchor, routes + admin_anchor, 'sessao administrativa')

schema_marker = '-- GAT_LIVE_MEDIA_V163'
if schema_marker not in schema:
    schema += r'''

-- GAT_LIVE_MEDIA_V163
CREATE TABLE IF NOT EXISTS radio_runtime (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  source_revision INTEGER NOT NULL DEFAULT 0,
  video_id TEXT NOT NULL DEFAULT '',
  position_seconds REAL NOT NULL DEFAULT 0,
  position_at TEXT NOT NULL DEFAULT '',
  updated_by TEXT NOT NULL DEFAULT ''
);
INSERT OR IGNORE INTO radio_runtime(id,source_revision,video_id,position_seconds,position_at,updated_by)
VALUES(1,0,'',0,'','');

CREATE TABLE IF NOT EXISTS live_streams (
  stream_id TEXT PRIMARY KEY,
  user TEXT NOT NULL,
  kind TEXT NOT NULL CHECK(kind IN ('route','radio_mic')),
  label TEXT NOT NULL DEFAULT '',
  quality TEXT NOT NULL DEFAULT 'normal',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  active INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS idx_live_streams_active ON live_streams(active,kind,updated_at);
CREATE INDEX IF NOT EXISTS idx_live_streams_user ON live_streams(user,kind,active);

CREATE TABLE IF NOT EXISTS live_signals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  stream_id TEXT NOT NULL,
  sender TEXT NOT NULL,
  target TEXT NOT NULL,
  type TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_live_signals_target ON live_signals(stream_id,target,id);
'''

required = [
    MARKER,
    "const VERSION='1.0.63-local'",
    "p==='/api/live/start'",
    "p==='/api/live/list'",
    "p==='/api/live/signal'",
    "p==='/api/live/poll'",
    "p==='/api/site/admin/radio/position'",
    "radio_runtime",
    "live_streams",
    "live_signals",
    "gatlive.gatlogets2.local",
]
for marker in required:
    if marker not in worker and marker not in schema:
        raise SystemExit('Patch Live Media 1.0.63 incompleto: ' + marker)

worker_path.write_text(worker, encoding='utf-8')
schema_path.write_text(schema, encoding='utf-8')
print('GAT Server 1.0.63: Radio ao vivo, sincronizacao e sinalizacao P2P de rota adicionadas.')
