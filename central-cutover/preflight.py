import json,os,urllib.request,urllib.error
ACCOUNT=os.environ['CLOUDFLARE_ACCOUNT_ID']
ZONE='93405963ee00b5d27166808415bca635'
TUNNEL='6daa92f9-60d0-4d18-abc3-34a14e9ebee1'
HOST='api.gatlogets2.com.br'
TEST='central-teste.gatlogets2.com.br'
def cf(path,method='GET',data=None):
    req=urllib.request.Request('https://api.cloudflare.com/client/v4'+path,method=method,data=None if data is None else json.dumps(data).encode(),headers={'Authorization':'Bearer '+os.environ['CLOUDFLARE_API_TOKEN'],'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=40) as r:
            raw=r.read()
            if not raw and r.status in (200,202,204):return None
            body=json.loads(raw)
    except urllib.error.HTTPError as e:
        try:codes=[x.get('code') for x in json.loads(e.read()).get('errors',[])]
        except Exception:codes=[]
        raise RuntimeError('Cloudflare '+method+' '+path.replace(ACCOUNT,'[account]')+' HTTP '+str(e.code)+' codes '+str(codes)) from None
    if not body.get('success'):raise RuntimeError('Cloudflare rejected '+method+' '+path.replace(ACCOUNT,'[account]'))
    return body.get('result')
def get(host,path):
    req=urllib.request.Request('https://'+host+path,headers={'User-Agent':'GAT-Migration-Check/1.0','Cache-Control':'no-cache','Origin':'https://gatlogets2.com.br'})
    with urllib.request.urlopen(req,timeout=30) as r:
        return json.load(r),dict(r.headers)
def main():
    health,_=get(TEST,'/health');assert health.get('agent_version')=='1.0.39-local',health
    status,_=get(TEST,'/api/public/service-status');assert status.get('paused') is False,status
    ranking,headers=get(TEST,'/api/public/ranking');assert ranking.get('ok') is True and len(ranking.get('ranking',[]))>=1
    print(json.dumps({'local_health':health,'local_status':status,'ranking_drivers':len(ranking['ranking']),'cors':headers.get('Access-Control-Allow-Origin')}))
    old,_=get(HOST,'/api/public/service-status');print(json.dumps({'production_status':old}))
    tunnel=cf('/accounts/'+ACCOUNT+'/cfd_tunnel/'+TUNNEL)
    print(json.dumps({'tunnel_status':tunnel.get('status'),'connections':len(tunnel.get('connections',[]))}))
    domains=cf('/accounts/'+ACCOUNT+'/workers/domains')
    print(json.dumps({'api_domains':[d for d in domains if d.get('hostname')==HOST]}))
    records=cf('/zones/'+ZONE+'/dns_records?name='+HOST)
    print(json.dumps({'api_dns':[{k:r.get(k) for k in ('id','type','name','content','proxied')} for r in records]}))
    try:
        routes=cf('/zones/'+ZONE+'/workers/routes')
        print(json.dumps({'worker_routes':routes}))
    except RuntimeError as e:print(str(e))
if __name__=='__main__':main()
