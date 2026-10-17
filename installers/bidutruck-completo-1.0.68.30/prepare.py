from pathlib import Path
import shutil,json,hashlib
source=Path('C:/Program Files/RoadLife Telemetria');root=Path('work/full-installer');payload=root/'payload';payload.mkdir(parents=True,exist_ok=True)
allowed_dirs={'RoadLifeDash','RoadLifeDashboard2','TruckSimGPS','voz','runtimes','gatdash-www'}
allowed_files={'GAT_TELEMETRIA.exe','GAT_TELEMETRIA_APP.exe','GAT_TELEMETRIA_APP.exe.config','Newtonsoft.Json.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll','ROADLIFE_TELEMETRIA.ico','gat-home-hero-1.0.44.jpg'}
records=[]
for p in source.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(source)
 if (len(rel.parts)==1 and p.name not in allowed_files) or (len(rel.parts)>1 and rel.parts[0] not in allowed_dirs):continue
 if any(x.lower() in {'logs','dev','data','cache'} or x.lower().startswith('backup-') for x in rel.parts):continue
 if p.suffix.lower() in {'.pdb','.zip','.sec','.dpapi'}:continue
 dst=payload/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
 records.append({'path':rel.as_posix(),'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
assert (payload/'GAT_TELEMETRIA_APP.exe').read_bytes()==Path('work/economy-launch/client/src/bin/Release/net48/GAT_TELEMETRIA.exe').read_bytes()
assert (payload/'RoadLifeDash/RoadLifeDash.xaml').read_bytes()==Path('work/economy-launch/client/installer/payload/RoadLifeDash/RoadLifeDash.xaml').read_bytes()
assert (payload/'TruckSimGPS/TruckSimGPS_Server.exe').exists()
(root/'managed-files.txt').write_text('\n'.join(r['path'].replace('/','\\') for r in records),encoding='utf-8')
out=Path('outputs/bidutruck-completo');out.mkdir(parents=True,exist_ok=True)
(out/'manifesto-arquivos.json').write_text(json.dumps({'version':'1.0.68.30','source':'instalação validada no PC, conferida contra o build publicado','files':records},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'files':len(records),'bytes':sum(x['size'] for x in records)}))
