"""Read account-wide usage without issuing queries to the D1 database."""
import datetime
import json
import os
import sys
import urllib.request

day=datetime.datetime.now(datetime.timezone.utc).date().isoformat()
query='''query Usage($accountTag: string!, $day: Date!) {
 viewer { accounts(filter: {accountTag: $accountTag}) {
  d1AnalyticsAdaptiveGroups(limit: 10000, filter: {date_geq: $day, date_leq: $day}) {
   sum { rowsRead rowsWritten }
  }
 } }
}'''
request=urllib.request.Request('https://api.cloudflare.com/client/v4/graphql',
 data=json.dumps({'query':query,'variables':{'accountTag':os.environ['CLOUDFLARE_ACCOUNT_ID'],'day':day}}).encode(),
 headers={'Authorization':'Bearer '+os.environ['CLOUDFLARE_API_TOKEN'],'Content-Type':'application/json'})
with urllib.request.urlopen(request,timeout=30) as response:
 result=json.load(response)
if result.get('errors'):
 # Only print provider error messages, never credentials or request headers.
 raise SystemExit('Cloudflare usage unavailable: '+ '; '.join(str(e.get('message','GraphQL error')) for e in result['errors']))
accounts=result.get('data',{}).get('viewer',{}).get('accounts',[])
if len(accounts)!=1: raise SystemExit('Cloudflare usage unavailable for this account')
groups=accounts[0].get('d1AnalyticsAdaptiveGroups',[])
reads=sum(g.get('sum',{}).get('rowsRead',0) for g in groups)
writes=sum(g.get('sum',{}).get('rowsWritten',0) for g in groups)
print(json.dumps({'date_utc':day,'rows_read':reads,'rows_written':writes,'free_read_limit':5000000,'free_write_limit':100000,'read_percent':round(reads/50000,2),'write_percent':round(writes/1000,2)}))
if '--enforce' in sys.argv:
 snapshot={'date_utc':day,'rows_read':reads,'rows_written':writes,'checked_at':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 endpoint='https://api.cloudflare.com/client/v4/accounts/'+os.environ['CLOUDFLARE_ACCOUNT_ID']+'/workers/scripts/gat-log-api/secrets'
 update=urllib.request.Request(endpoint,method='PUT',data=json.dumps({'name':'GAT_D1_BUDGET','type':'secret_text','text':json.dumps(snapshot)}).encode(),headers={'Authorization':'Bearer '+os.environ['CLOUDFLARE_API_TOKEN'],'Content-Type':'application/json'})
 with urllib.request.urlopen(update,timeout=30) as response:
  saved=json.load(response)
 if not saved.get('success'): raise SystemExit('Could not update free-tier budget protection')
 print('Budget protection refreshed; paused='+str(reads>=4000000 or writes>=80000))
