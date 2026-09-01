"""Prepare a dedicated test tunnel; never modify the production API DNS."""
import hashlib,json,os,pathlib,sys,urllib.request,urllib.error
ACCOUNT=os.environ['CLOUDFLARE_ACCOUNT_ID']
BASE='https://api.cloudflare.com/client/v4'
ZONE='93405963ee00b5d27166808415bca635'
NAME='gat-central-douglas'
HOST='central-teste.gatlogets2.com.br'
def cf(path,method='GET',data=None):
    req=urllib.request.Request(BASE+path,method=method,data=None if data is None else json.dumps(data).encode(),headers={'Authorization':'Bearer '+os.environ['CLOUDFLARE_API_TOKEN'],'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=45) as response:r=json.load(response)
    except urllib.error.HTTPError as e:
        try:codes=[x.get('code') for x in json.loads(e.read()).get('errors',[])]
        except Exception:codes=[]
        operation=path.replace(ACCOUNT,'[account]').replace(ZONE,'[zone]')
        raise RuntimeError('Cloudflare HTTP '+str(e.code)+' during '+method+' '+operation+'; codes='+str(codes)) from None
    if not r.get('success'):raise RuntimeError('Cloudflare rejected operation: '+str([e.get('code') for e in r.get('errors',[])]))
    return r['result']
def main():
    path='/accounts/'+ACCOUNT+'/cfd_tunnel'
    matches=[t for t in cf(path+'?is_deleted=false') if t['name']==NAME]
    if len(matches)>1:raise RuntimeError('Multiple tunnels have the requested name.')
    if matches:
        tunnel=matches[0]
        if tunnel.get('config_src')!='cloudflare':raise RuntimeError('Existing tunnel is not remotely managed; no configuration changed.')
    else:tunnel=cf(path,'POST',{'name':NAME,'config_src':'cloudflare'})
    tid=tunnel['id'];path+='/'+tid
    config={'ingress':[{'hostname':HOST,'service':'http://127.0.0.1:5056'},{'hostname':'api.gatlogets2.com.br','service':'http://127.0.0.1:5056'},{'service':'http_status:404'}]}
    cf(path+'/configurations','PUT',{'config':config})
    records=cf('/zones/'+ZONE+'/dns_records?name='+HOST)
    destination=tid+'.cfargotunnel.com'
    if records:
        if len(records)!=1 or records[0].get('type')!='CNAME' or records[0].get('content')!=destination:raise RuntimeError('The test hostname already belongs to another service. It was not changed.')
    else:cf('/zones/'+ZONE+'/dns_records','POST',{'type':'CNAME','name':HOST,'content':destination,'proxied':True,'ttl':1})
    token=cf(path+'/token')
    if not isinstance(token,str) or not token or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+/=_-' for c in token):raise RuntimeError('Unexpected tunnel credential format.')
    request=urllib.request.Request('https://api.github.com/repos/cloudflare/cloudflared/releases/latest',headers={'User-Agent':'GAT-Setup'})
    with urllib.request.urlopen(request,timeout=30) as response:release=json.load(response)
    asset=next(x for x in release['assets'] if x['name']=='cloudflared-windows-amd64.exe')
    url=asset['browser_download_url']
    if not url.startswith('https://github.com/cloudflare/cloudflared/releases/download/'):raise RuntimeError('Unexpected conector download source.')
    with urllib.request.urlopen(url,timeout=120) as response:data=response.read()
    digest=hashlib.sha256(data).hexdigest()
    if asset.get('digest') and asset['digest']!='sha256:'+digest:raise RuntimeError('Official conector digest mismatch.')
    source=pathlib.Path('tunnel-setup/Program.cs')
    source.write_text(source.read_text().replace('__TUNNEL_TOKEN__',token).replace('__CLOUDFLARED_URL__',url).replace('__CLOUDFLARED_SHA256__',digest))
    print(json.dumps({'tunnel_id':tid,'test_hostname':HOST,'connector_version':release['tag_name'],'production_dns_changed':False}))
if __name__=='__main__':
    try:main()
    except Exception as e:print('Tunnel preparation stopped:',str(e));sys.exit(1)
