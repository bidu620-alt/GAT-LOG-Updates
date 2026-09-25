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
    if "const VERSION='1.0.62-local';" in worker:
        worker = worker.replace("const VERSION='1.0.62-local';", "const VERSION='1.0.63-local';", 1)
    elif "const VERSION='1.0.42-cloudflare';" in worker:
        worker = worker.replace("const VERSION='1.0.42-cloudflare';", "const VERSION='1.0.43-cloudflare';", 1)

    # WebView2 usa origem virtual local para o player WebRTC.
    if "'https://live.gatlogets2.local'" not in worker and "const ORIGINS=new Set([" in worker:
        worker = worker.replace("const ORIGINS=new Set([", "const ORIGINS=new Set(['https://live.gatlogets2.local',", 1)

    anchor = 'async function route(req,env){const u=new URL(req.url),p=u.pathname,m=req.method;'
    if anchor not in worker:
        raise SystemExit('Roteador HTTP nao encontrado.')

    helpers = r'''
// GAT_LIVE_MEDIA_V163
const LIVE_TTL_MS=30000;
const LIVE_SIGNAL_TTL_MS=120000;
function gatLiveKind(v){v=String(v||'').trim().toLowerCase();return v==='route'||v==='radio_mic'?v:''}
function gatLiveId(kind,user){return kind+':'+clean(user)}
async function gatLiveCleanup(env){
 const liveCut=new Date(Date.now()-LIVE_TTL_MS).toISOString();
 const signalCut=new Date(Date.now()-LIVE_SIGNAL_TTL_MS).toISOString();
 try{await env.DB.prepare('UPDATE live_stream_sessions SET active=0 WHERE active=1 AND updated_at<?').bind(liveCut).run()}catch{}
 try{await env.DB.prepare('DELETE FROM live_stream_signals WHERE created_at<?').bind(signalCut).run()}catch{}
}
async function gatLivePublic(env){
 await gatLiveCleanup(env);
 const r=await env.DB.prepare("SELECT stream_id,account_user,kind,started_at,updated_at FROM live_stream_sessions WHERE active=1 AND updated_at>=? ORDER BY kind,started_at")
  .bind(new Date(Date.now()-LIVE_TTL_MS).toISOString()).all();
 return (r.results||[]).map(x=>({stream_id:String(x.stream_id||''),user:String(x.account_user||''),kind:String(x.kind||''),started_at:String(x.started_at||''),updated_at:String(x.updated_at||'')}));
}
async function gatLiveOwned(env,streamId,user){
 const r=await env.DB.prepare('SELECT stream_id,account_user,kind,active,updated_at FROM live_stream_sessions WHERE stream_id=?').bind(String(streamId||'')).first();
 return r&&Number(r.active||0)===1&&clean(r.account_user)===clean(user)?r:null;
}
'''

    worker = worker.replace(anchor, helpers + '\n' + anchor, 1)

    public_candidates = [
        " if(p==='/api/public/radio'&&m==='GET')return json(req,{ok:true,radio:await gatRadioState(env)});",
        " if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'cloudflare-workers-d1',mission_min_km:MIN_KM,test_mode:false});",
        " if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'local-sqlite',mission_min_km:MIN_KM,test_mode:false});"
    ]
    public_anchor = next((x for x in public_candidates if x in worker), None)
    if not public_anchor:
        raise SystemExit('Endpoint publico para inserir /api/public/live nao encontrado.')
    public_route = public_anchor + "\n if(p==='/api/public/live'&&m==='GET')return json(req,{ok:true,streams:await gatLivePublic(env),time:now()});"
    worker = worker.replace(public_anchor, public_route, 1)

    session_anchor = " if((p==='/api/account/session'||p==='/api/site/session')&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}"
    if session_anchor not in worker:
        raise SystemExit('Endpoint de sessao nao encontrado.')

    routes = r'''
 if(p==='/api/live/start'&&m==='POST'){
  const b=await body(req),kind=gatLiveKind(b.kind);
  if(!kind)throw new HttpError(400,'invalid_live_kind');
  const s=kind==='radio_mic'?await requireAdmin(req,env,b):await requireSession(req,env,b);
  const streamId=gatLiveId(kind,s.user),t=now();
  await env.DB.prepare('INSERT INTO live_stream_sessions(stream_id,account_user,kind,active,started_at,updated_at) VALUES(?,?,?,1,?,?) ON CONFLICT(stream_id) DO UPDATE SET active=1,started_at=excluded.started_at,updated_at=excluded.updated_at,account_user=excluded.account_user,kind=excluded.kind')
   .bind(streamId,s.user,kind,t,t).run();
  await env.DB.prepare('DELETE FROM live_stream_signals WHERE stream_id=?').bind(streamId).run();
  return json(req,{ok:true,stream:{stream_id:streamId,user:s.user,kind,started_at:t}});
 }
 if(p==='/api/live/heartbeat'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),streamId=String(b.stream_id||'');
  const owned=await gatLiveOwned(env,streamId,s.user);if(!owned)throw new HttpError(404,'live_stream_not_found');
  await env.DB.prepare('UPDATE live_stream_sessions SET updated_at=? WHERE stream_id=?').bind(now(),streamId).run();
  return json(req,{ok:true});
 }
 if(p==='/api/live/stop'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),streamId=String(b.stream_id||'');
  const owned=await gatLiveOwned(env,streamId,s.user);if(!owned)throw new HttpError(404,'live_stream_not_found');
  await env.DB.prepare('UPDATE live_stream_sessions SET active=0,updated_at=? WHERE stream_id=?').bind(now(),streamId).run();
  await env.DB.prepare('DELETE FROM live_stream_signals WHERE stream_id=?').bind(streamId).run();
  return json(req,{ok:true});
 }
 if(p==='/api/live/signal'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),streamId=String(b.stream_id||''),to=clean(b.to_user);
  if(!streamId||!to)throw new HttpError(400,'live_signal_target_required');
  const stream=await env.DB.prepare('SELECT stream_id,account_user,kind,active,updated_at FROM live_stream_sessions WHERE stream_id=?').bind(streamId).first();
  if(!stream||Number(stream.active||0)!==1||Date.now()-Date.parse(stream.updated_at)>LIVE_TTL_MS)throw new HttpError(404,'live_stream_not_found');
  const owner=clean(stream.account_user),from=clean(s.user);
  if(from!==owner&&to!==owner)throw new HttpError(403,'live_signal_forbidden');
  let payload=b.payload;
  if(typeof payload==='string'){try{payload=JSON.parse(payload)}catch{throw new HttpError(400,'invalid_live_signal')}}
  const encoded=JSON.stringify(payload||{});
  if(encoded.length>24000)throw new HttpError(413,'live_signal_too_large');
  await env.DB.prepare('INSERT INTO live_stream_signals(stream_id,from_user,to_user,payload_json,created_at) VALUES(?,?,?,?,?)')
   .bind(streamId,from,to,encoded,now()).run();
  return json(req,{ok:true});
 }
 if(p==='/api/live/signals'&&m==='POST'){
  const b=await body(req),s=await requireSession(req,env,b),since=Math.max(0,Math.trunc(Number(b.since||0))),streamId=String(b.stream_id||'');
  if(!streamId)throw new HttpError(400,'stream_id_required');
  await gatLiveCleanup(env);
  const r=await env.DB.prepare('SELECT id,stream_id,from_user,to_user,payload_json,created_at FROM live_stream_signals WHERE stream_id=? AND to_user=? AND id>? ORDER BY id ASC LIMIT 100')
   .bind(streamId,clean(s.user),since).all();
  const signals=(r.results||[]).map(x=>{let payload={};try{payload=JSON.parse(x.payload_json||'{}')}catch{}return{id:Number(x.id||0),stream_id:String(x.stream_id||''),from_user:String(x.from_user||''),payload,created_at:String(x.created_at||'')}});
  return json(req,{ok:true,signals});
 }
'''
    worker = worker.replace(session_anchor, routes + session_anchor, 1)

schema_marker = '-- GAT_LIVE_MEDIA_V163'
if schema_marker not in schema:
    schema += r'''

-- GAT_LIVE_MEDIA_V163
CREATE TABLE IF NOT EXISTS live_stream_sessions (
  stream_id TEXT PRIMARY KEY,
  account_user TEXT NOT NULL,
  kind TEXT NOT NULL CHECK(kind IN ('route','radio_mic')),
  active INTEGER NOT NULL DEFAULT 1,
  started_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_live_stream_sessions_active
  ON live_stream_sessions(active,updated_at);

CREATE TABLE IF NOT EXISTS live_stream_signals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  stream_id TEXT NOT NULL,
  from_user TEXT NOT NULL,
  to_user TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_live_stream_signals_target
  ON live_stream_signals(stream_id,to_user,id);
'''

required = [
    MARKER,
    "p==='/api/public/live'",
    "p==='/api/live/start'",
    "p==='/api/live/heartbeat'",
    "p==='/api/live/stop'",
    "p==='/api/live/signal'",
    "p==='/api/live/signals'",
    "kind==='radio_mic'?await requireAdmin",
    'live_stream_sessions',
    'live_stream_signals',
]
for marker in required:
    if marker not in worker and marker not in schema:
        raise SystemExit('Patch live media incompleto: ' + marker)

worker_path.write_text(worker, encoding='utf-8')
schema_path.write_text(schema, encoding='utf-8')
print('GAT Server 1.0.63: sinalizacao WebRTC para Radio ao Vivo e Transmissao de Rota adicionada.')
