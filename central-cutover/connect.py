"""Move only the GAT API hostname after verifying the imported local central."""
import base64,hashlib,json,os,pathlib,time,urllib.request
from cryptography.hazmat.primitives import serialization,hashes
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from preflight import ACCOUNT,ZONE,TUNNEL,HOST,TEST,cf,get
DOMAIN='fcd2a9b94eb2e380a38868079e8a62b485a39200'
DEST=TUNNEL+'.cfargotunnel.com'
def check_local(host):
    health,_=get(host,'/health');assert health.get('agent_version')=='1.0.39-local'
    status,_=get(host,'/api/public/service-status');assert status.get('paused') is False and status.get('storage')=='local-sqlite'
    ranking,headers=get(host,'/api/public/ranking');assert ranking.get('ok') is True and len(ranking.get('ranking',[]))>=8
    headers={k.lower():v for k,v in headers.items()}
    assert headers.get('access-control-allow-origin')=='https://gatlogets2.com.br','Missing site CORS'
    catalog,_=get(host,'/api/public/work/catalog');assert len(catalog.get('catalog',[]))==30
    req=urllib.request.Request('https://'+host+'/api/site/login',method='OPTIONS',headers={'User-Agent':'GAT-Migration-Check/1.0','Origin':'https://gatlogets2.com.br','Access-Control-Request-Method':'POST','Access-Control-Request-Headers':'content-type'})
    with urllib.request.urlopen(req,timeout=30) as r:
        assert r.status==204 and r.headers.get('Access-Control-Allow-Origin')=='https://gatlogets2.com.br'
    return {'host':host,'version':health['agent_version'],'storage':status['storage'],'paused':status['paused'],'ranking_drivers':len(ranking['ranking']),'catalog_items':len(catalog['catalog']),'cors_verified':True}
def seal(checkpoint):
    raw=json.dumps(checkpoint,sort_keys=True).encode()
    public=serialization.load_pem_public_key(pathlib.Path('migration-local/export-public.pem').read_bytes())
    key=AESGCM.generate_key(bit_length=256);nonce=os.urandom(12)
    wrapped=public.encrypt(key,padding.OAEP(mgf=padding.MGF1(hashes.SHA256()),algorithm=hashes.SHA256(),label=None))
    encrypted=AESGCM(key).encrypt(nonce,raw,b'GAT-DOMAIN-CUTOVER-20260901')
    outer=base64.b64encode(json.dumps({k:base64.b64encode(v).decode() for k,v in {'key':wrapped,'nonce':nonce,'ciphertext':encrypted}.items()}).encode()).decode()
    for i in range(0,len(outer),6000):print('GAT_CUTOVER_CHECKPOINT:'+outer[i:i+6000],flush=True)
    print(json.dumps({'checkpoint_sha256':hashlib.sha256(raw).hexdigest()}),flush=True)
def main():
    print(json.dumps({'preflight':check_local(TEST)}),flush=True)
    tunnel=cf('/accounts/'+ACCOUNT+'/cfd_tunnel/'+TUNNEL);assert tunnel.get('status')=='healthy'
    config=cf('/accounts/'+ACCOUNT+'/cfd_tunnel/'+TUNNEL+'/configurations')
    assert any(x.get('hostname')==HOST and x.get('service')=='http://127.0.0.1:5056' for x in config['config']['ingress'])
    domains=[d for d in cf('/accounts/'+ACCOUNT+'/workers/domains') if d.get('hostname')==HOST]
    records=cf('/zones/'+ZONE+'/dns_records?name='+HOST)
    root_before=cf('/zones/'+ZONE+'/dns_records?name=gatlogets2.com.br')
    www_before=cf('/zones/'+ZONE+'/dns_records?name=www.gatlogets2.com.br')
    if not domains and len(records)==1 and records[0].get('content')==DEST:
        print(json.dumps({'already_connected':check_local(HOST)}));return
    if domains:
        assert len(domains)==1 and domains[0]['id']==DOMAIN and domains[0]['service']=='gat-log-api','Unexpected Worker domain owner'
        assert len(records)==1 and records[0].get('type')=='AAAA' and records[0].get('content')=='100::','Unexpected existing API DNS'
        old,_=get(HOST,'/api/public/service-status')
    else:
        # Resume the exact migration if the successful detach returned HTTP 204.
        assert not records or (len(records)==1 and records[0].get('id')=='8c7d8bfe2077401049fbf8c1820c5f7f' and records[0].get('content')=='100::'), 'Unexpected DNS during resume'
        old,_=get('gat-log-api.bidufilmes.workers.dev','/api/public/service-status')
    assert old.get('paused') is True and old.get('reason')=='migration','Cloud database must stay paused'
    seal({'captured_at':time.time(),'domains':domains,'api_dns':records,'root_dns':root_before,'www_dns':www_before,'tunnel':TUNNEL,'old_status':old})
    if domains:cf('/accounts/'+ACCOUNT+'/workers/domains/'+DOMAIN,'DELETE')
    print('API Worker custom domain is detached.',flush=True)
    records=cf('/zones/'+ZONE+'/dns_records?name='+HOST)
    body={'type':'CNAME','name':HOST,'content':DEST,'proxied':True,'ttl':1}
    if records:
        assert len(records)==1 and records[0].get('type')=='AAAA' and records[0].get('content')=='100::','Unexpected DNS after detach'
        cf('/zones/'+ZONE+'/dns_records/'+records[0]['id'],'PUT',body)
    else:cf('/zones/'+ZONE+'/dns_records','POST',body)
    print('API DNS now points to the verified local tunnel.',flush=True)
    assert cf('/zones/'+ZONE+'/dns_records?name=gatlogets2.com.br')==root_before
    assert cf('/zones/'+ZONE+'/dns_records?name=www.gatlogets2.com.br')==www_before
    # Once the local database is reachable it may accept writes: never automatically
    # revert to the old D1 database on a transient verification failure.
    for attempt in range(24):
        try:
            result=check_local(HOST)
            print(json.dumps({'production_verified':result,'site_dns_preserved':True,'d1_kept_paused':True}),flush=True)
            return
        except Exception as e:
            if attempt==23:raise RuntimeError('API DNS changed; external verification pending. Keep D1 paused and inspect local tunnel.') from e
            time.sleep(5)
if __name__=='__main__':main()
