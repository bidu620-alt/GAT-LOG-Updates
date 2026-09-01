"""One-time owner-authorized migration export. Never log plaintext or signed URLs."""
import base64,gzip,json,os,pathlib,sys,time,urllib.request,urllib.error
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ACCOUNT=os.environ['CLOUDFLARE_ACCOUNT_ID']
BASE='https://api.cloudflare.com/client/v4/accounts/'+ACCOUNT
SCRIPT=BASE+'/workers/scripts/gat-log-api'
DB=BASE+'/d1/database/609412d2-d3cd-478a-a1d5-f8ba728ed304/export'

def cf(url,method='GET',data=None,raw=False):
    req=urllib.request.Request(url,data=None if data is None else json.dumps(data).encode(),method=method,
      headers={'Authorization':'Bearer '+os.environ['CLOUDFLARE_API_TOKEN'],'Content-Type':'application/json','User-Agent':'GAT-Migration/1.0'})
    try:
        with urllib.request.urlopen(req,timeout=60) as response:payload=response.read()
    except urllib.error.HTTPError as error:
        raise RuntimeError('Cloudflare API HTTP '+str(error.code)) from None
    if raw:return payload
    result=json.loads(payload)
    if not result.get('success'):raise RuntimeError('Cloudflare API rejected operation; codes='+str([e.get('code') for e in result.get('errors',[])]))
    return result['result']

def main():
    public=serialization.load_pem_public_key(pathlib.Path('migration-local/export-public.pem').read_bytes())
    assert public.key_size>=4096
    deployed=cf(SCRIPT,raw=True)
    if b'GAT_MIGRATION_PAUSED' not in deployed:raise RuntimeError('Migration pause is not deployed yet; no export started.')
    cf(SCRIPT+'/secrets','PUT',{'name':'GAT_MIGRATION_PAUSED','type':'secret_text','text':'1'})
    # Verify the actual production handler has applied the pause before export.
    for _ in range(24):
        time.sleep(5)
        try:
            req=urllib.request.Request('https://api.gatlogets2.com.br/api/public/service-status',headers={'User-Agent':'GAT-Deployment-Check/1.0'})
            with urllib.request.urlopen(req,timeout=20) as response:status=json.load(response)
            if status.get('paused') is True and status.get('reason')=='migration':break
        except (urllib.error.URLError,ValueError):pass
    else:raise RuntimeError('Could not verify the public migration pause. Export was not started; pause remains enabled.')
    # Allow requests that started before the pause to finish.
    time.sleep(10)
    print('MIGRATION_PAUSE_VERIFIED')
    request={'output_format':'polling'}
    for _ in range(90):
        result=cf(DB,'POST',request)
        if result.get('status')=='error':raise RuntimeError('D1 export failed; no data published.')
        if result.get('status')=='complete':break
        bookmark=result.get('at_bookmark')
        if not bookmark:raise RuntimeError('D1 export returned no polling bookmark.')
        request={'output_format':'polling','current_bookmark':bookmark};time.sleep(2)
    else:raise RuntimeError('D1 export did not complete in time.')
    url=result.get('result',{}).get('signed_url')
    if not url or not url.startswith('https://'):raise RuntimeError('D1 returned no HTTPS export URL.')
    try:
        with urllib.request.urlopen(url,timeout=90) as response:sql=response.read()
    except Exception:raise RuntimeError('Could not download the D1 export.') from None
    if not sql or b'CREATE TABLE' not in sql:raise RuntimeError('Invalid or empty SQL export.')
    key=AESGCM.generate_key(bit_length=256);nonce=os.urandom(12);aad=b'GAT-D1-MIGRATION-20260901'
    encrypted=AESGCM(key).encrypt(nonce,gzip.compress(sql),aad)
    wrapped=public.encrypt(key,padding.OAEP(mgf=padding.MGF1(algorithm=hashes.SHA256()),algorithm=hashes.SHA256(),label=None))
    envelope=json.dumps({'v':1,'key':base64.b64encode(wrapped).decode(),'nonce':base64.b64encode(nonce).decode(),'ciphertext':base64.b64encode(encrypted).decode()},separators=(',',':')).encode()
    sealed=base64.b64encode(envelope).decode()
    print('ENCRYPTED_EXPORT_BYTES',len(encrypted))
    for i in range(0,len(sealed),6000):print('GAT_SEALED_CHUNK:'+sealed[i:i+6000])
    print('ENCRYPTED_EXPORT_COMPLETE')

if __name__=='__main__':
    try:main()
    except Exception as error:
        # No traceback/HTTP body or signed URL may appear in public Actions logs.
        print('Migration export stopped:',str(error));sys.exit(1)
