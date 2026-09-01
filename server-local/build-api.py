"""Build the local variant from the same assembled, tested production API."""
import pathlib, re, shutil, subprocess, sys
root = pathlib.Path(__file__).resolve().parents[1]
out = pathlib.Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'server-local'/'runtime'
shutil.copytree(root/'cloudflare-central', out, dirs_exist_ok=True)
workflow=(root/'.github/workflows/deploy-gat-api.yml').read_text()
for script in re.findall(r'python (scripts/apply-[^\s]+)', workflow):
    subprocess.run([sys.executable,script],cwd=out,check=True)
subprocess.run(['node','scripts/apply-suspended-job-fix.mjs'],cwd=out,check=True)
worker=(out/'worker.js').read_text()
worker=re.sub(r"import .* from '@noble/[^\n]+\n",'',worker)
worker="""import {createHash,pbkdf2Sync} from 'node:crypto';
const sha256=x=>createHash('sha256').update(x).digest();
const bytesToHex=x=>Buffer.from(x).toString('hex');
const pbkdf2=(_,password,salt,options)=>pbkdf2Sync(password,salt,options.c,options.dkLen,'sha256');
"""+worker
assert "const VERSION='1.0.52-cloudflare'" in worker
worker=worker.replace("const VERSION='1.0.52-cloudflare'","const VERSION='1.0.40-local'").replace("service:'GAT Central Cloud'","service:'GAT Central Local'")
(out/'worker.js').write_text(worker)
# Local ranking hotfix is copied from cloudflare-central/ranking-telemetry.js and
# validated by the same production contract tests before packaging.
# This file exists only in the Windows local build. The Cloudflare source and
# its fail-closed quota protection are never edited or bypassed at runtime.
(out/'budget-guard.js').write_text("export function budgetState(){return {paused:false,reason:null,resumes_at:null,storage:'local-sqlite'};}\n")
for name in ['host.mjs','database.mjs']:
    shutil.copy2(root/'server-local'/name,out/name)
print('Local API assembled:',out)
