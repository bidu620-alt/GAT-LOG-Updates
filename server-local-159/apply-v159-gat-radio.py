from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/runtime')
worker_path = root / 'worker.js'
schema_path = root / 'schema.sql'
worker = worker_path.read_text(encoding='utf-8')
schema = schema_path.read_text(encoding='utf-8')

MARKER = 'GAT_RADIO_V159'


def once(text, old, new, label):
    if old not in text:
        raise SystemExit('Nao encontrei ' + label)
    return text.replace(old, new, 1)


if MARKER not in worker:
    worker = once(worker, "const VERSION='1.0.58-local';", "const VERSION='1.0.59-local';", 'versao 1.0.58')

    helper = r'''
// GAT_RADIO_V159 - radio controlada pelo site e reproduzida somente no GAT Telemetria.
function gatRadioPlaylistId(value){
 const raw=String(value||'').trim();
 if(!raw)return '';
 if(/^[A-Za-z0-9_-]{10,100}$/.test(raw))return raw;
 try{
  const x=new URL(raw),host=x.hostname.toLowerCase();
  if(!['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(host))return '';
  const id=String(x.searchParams.get('list')||'').trim();
  return /^[A-Za-z0-9_-]{10,100}$/.test(id)?id:'';
 }catch{return ''}
}
function gatRadioPublic(row){
 return{enabled:!!Number(row?.enabled||0),source:'youtube',playlist_url:String(row?.playlist_url||''),playlist_id:String(row?.playlist_id||''),label:String(row?.label||'Radio GAT'),revision:Number(row?.revision||0),updated_by:String(row?.updated_by||''),updated_at:String(row?.updated_at||'')};
}
async function gatRadioState(env){
 const row=await env.DB.prepare('SELECT enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at FROM radio_state WHERE id=1').first();
 return gatRadioPublic(row);
}
'''
    route_anchor = 'async function route(req,env){const u=new URL(req.url),p=u.pathname,m=req.method;'
    worker = once(worker, route_anchor, helper + '\n' + route_anchor, 'inicio do roteador HTTP')

    public_anchor = " if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'cloudflare-workers-d1',mission_min_km:MIN_KM,test_mode:false});"
    if public_anchor not in worker:
        public_anchor = " if(p==='/api/public/version')return json(req,{ok:true,agent_version:VERSION,platform:'local-sqlite',mission_min_km:MIN_KM,test_mode:false});"
    radio_public = public_anchor + "\n if(p==='/api/public/radio'&&m==='GET')return json(req,{ok:true,radio:await gatRadioState(env)});"
    worker = once(worker, public_anchor, radio_public, 'endpoint publico de versao')

    admin_anchor = " if(p==='/api/site/admin/session'&&m==='POST'){const b=await body(req),s=await requireAdmin(req,env,b);return json(req,{ok:true,user:s.user,role:s.role})}"
    radio_admin = r''' if(p==='/api/site/admin/radio'&&m==='POST'){
  const b=await body(req),s=await requireAdmin(req,env,b),current=await gatRadioState(env);
  const rawUrl=String(b.playlist_url!==undefined?b.playlist_url:current.playlist_url||'').trim();
  const playlistId=gatRadioPlaylistId(rawUrl);
  const enabled=b.enabled!==undefined?(b.enabled===true||b.enabled===1||String(b.enabled).toLowerCase()==='true'):current.enabled;
  if(rawUrl&&!playlistId)throw new HttpError(400,'invalid_youtube_playlist');
  if(enabled&&!playlistId)throw new HttpError(400,'playlist_required');
  const playlistUrl=playlistId?`https://www.youtube.com/playlist?list=${playlistId}`:'',label=String(b.label||current.label||'Radio GAT').trim().slice(0,80)||'Radio GAT',t=now();
  await env.DB.prepare('INSERT INTO radio_state(id,enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at) VALUES(1,?,?,?,?,1,?,?) ON CONFLICT(id) DO UPDATE SET enabled=excluded.enabled,playlist_url=excluded.playlist_url,playlist_id=excluded.playlist_id,label=excluded.label,revision=radio_state.revision+1,updated_by=excluded.updated_by,updated_at=excluded.updated_at').bind(enabled?1:0,playlistUrl,playlistId,label,s.user,t).run();
  await audit(env,s.user,'radio_update','radio',{enabled:!!enabled,playlist_id:playlistId,label});
  return json(req,{ok:true,radio:await gatRadioState(env)});
 }
'''
    worker = once(worker, admin_anchor, radio_admin + admin_anchor, 'sessao administrativa')

schema_marker = '-- GAT_RADIO_V159'
if schema_marker not in schema:
    schema += r'''

-- GAT_RADIO_V159
CREATE TABLE IF NOT EXISTS radio_state (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  enabled INTEGER NOT NULL DEFAULT 0,
  playlist_url TEXT NOT NULL DEFAULT '',
  playlist_id TEXT NOT NULL DEFAULT '',
  label TEXT NOT NULL DEFAULT 'Radio GAT',
  revision INTEGER NOT NULL DEFAULT 0,
  updated_by TEXT NOT NULL DEFAULT '',
  updated_at TEXT NOT NULL DEFAULT ''
);
INSERT OR IGNORE INTO radio_state(id,enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at)
VALUES(1,0,'','','Radio GAT',0,'',datetime('now'));
'''

required = [
    MARKER,
    "const VERSION='1.0.59-local'",
    "p==='/api/public/radio'",
    "p==='/api/site/admin/radio'",
    'invalid_youtube_playlist',
    "await requireAdmin(req,env,b)",
    'radio_state',
]
for marker in required:
    if marker not in worker and marker not in schema:
        raise SystemExit('Patch 1.0.59 incompleto: ' + marker)

worker_path.write_text(worker, encoding='utf-8')
schema_path.write_text(schema, encoding='utf-8')
print('GAT Server 1.0.59: Radio GAT adicionada (controle admin/moderador, YouTube playlist, leitura publica para o cliente).')
