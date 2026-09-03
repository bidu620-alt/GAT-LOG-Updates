from pathlib import Path

p=Path('.github/workflows/build-server-local.yml')
s=p.read_text(encoding='utf-8')

def rep(old,new,label):
    global s
    if old not in s:
        if new in s:return
        raise SystemExit('Nao encontrei '+label)
    s=s.replace(old,new)

rep('name: Build GAT Server 1.0.48 Local','name: Build GAT Server 1.0.49 Local','nome do workflow')
rep('      - name: Verify startup damage proof, JeanJC repair, Lapeal repair and monthly trip goal\n        shell: bash\n        run: node --test --test-timeout=30000 server-local/go-live-ranking.test.mjs server-local/preflight-rank.test.mjs server-local/lapeal-repair.test.mjs server-local/jeanjc-repair.test.mjs server-local/monthly-trip-goal.test.mjs\n',
'''      - name: Verify startup damage proof, JeanJC repair, Lapeal repair and legacy monthly migration\n        shell: bash\n        run: node --test --test-timeout=30000 server-local/go-live-ranking.test.mjs server-local/preflight-rank.test.mjs server-local/lapeal-repair.test.mjs server-local/jeanjc-repair.test.mjs server-local/monthly-trip-goal.test.mjs\n      - name: Apply GAT Server 1.0.49 career history rules\n        run: python server-local/apply-v149-career-history.py server-local/runtime\n      - name: Verify GAT Server 1.0.49 history and ranking separation\n        shell: bash\n        run: node --test --test-timeout=30000 server-local/career-history-v149.test.mjs\n''','passo de verificacao antes da 1.0.49')
rep('      - name: Prepare panel version 1.0.48','      - name: Prepare panel version 1.0.49','nome prepare panel')
rep('      - name: Compile panel 1.0.48','      - name: Compile panel 1.0.49','nome compile panel')
rep("assert '1.0.48-local' in text and 'const MIN_KM=0;' in text", "assert '1.0.49-local' in text and 'const MIN_KM=0;' in text", 'assert da versao')
rep("assert 'monthly_completed=MIN(monthly_goal,monthly_completed+1)' in text and 'if(false&&workAlreadyCompleted)' in text and \"classification_status:'pending',monthly_increment:1\" in text",
    "assert 'monthly_completed=monthly_completed+1' in text and 'history_recorded:true' in text and 'ranking_reason:rankReason' in text and \"reason:'route_already_used'\" not in text and \"classification_status:'pending',monthly_increment:1\" in text",
    'assert mensal antigo')
rep("assert 'go_live_baseline_2026_09_02' in host and 'DELETE FROM deliveries' in host and 'points=0' in host and 'reconcileMonthlyTripGoal' in host and 'substr(d.delivered_at,1,7)' in host",
    "assert 'go_live_baseline_2026_09_02' in host and 'DELETE FROM deliveries' in host and 'points=0' in host and 'reconcileMonthlyTripCount' in host and 'substr(d.delivered_at,1,7)' in host and 'MIN(monthly_goal' not in host",
    'assert de reconciliacao mensal')
rep("release=pathlib.Path('releases/GAT_SERVER_LOCAL_1.0.48.zip')","release=pathlib.Path('releases/GAT_SERVER_LOCAL_1.0.49.zip')",'zip da release')
rep('      - name: Publish GAT Server 1.0.48 updater','      - name: Publish GAT Server 1.0.49 updater','nome publish')
rep('cp server-local/updater-out/GAT_LOG_SERVER_UPDATE_1.0.48_LOCAL.exe releases/GAT_LOG_SERVER_UPDATE_1.0.48_LOCAL.exe','cp server-local/updater-out/GAT_LOG_SERVER_UPDATE_1.0.49_LOCAL.exe releases/GAT_LOG_SERVER_UPDATE_1.0.49_LOCAL.exe','nome exe publish')
rep("git add releases/GAT_LOG_SERVER_UPDATE_1.0.48_LOCAL.exe releases/GAT_SERVER_LOCAL_1.0.48.zip releases/GAT_SERVER_LOCAL_1.0.48.sha256","git add releases/GAT_LOG_SERVER_UPDATE_1.0.49_LOCAL.exe releases/GAT_SERVER_LOCAL_1.0.49.zip releases/GAT_SERVER_LOCAL_1.0.49.sha256",'git add release')
rep("git commit -m 'Publish GAT Server 1.0.48 JeanJC ranking repair'","git commit -m 'Publish GAT Server 1.0.49 career history and ranking separation'",'commit publish')
rep('name: GAT-Server-1.0.48-local-updater','name: GAT-Server-1.0.49-local-updater','artifact name')
rep('path: server-local/updater-out/GAT_LOG_SERVER_UPDATE_1.0.48_LOCAL.exe','path: server-local/updater-out/GAT_LOG_SERVER_UPDATE_1.0.49_LOCAL.exe','artifact path')

# Release workflow must never accidentally publish an old 1.0.48 package after this branch merges.
for bad in ['Build GAT Server 1.0.48 Local','GAT_SERVER_LOCAL_1.0.48.zip','GAT_LOG_SERVER_UPDATE_1.0.48_LOCAL.exe','reconcileMonthlyTripGoal']:
    if bad in s: raise SystemExit('Workflow 1.0.49 ainda contem referencia perigosa: '+bad)
if 'apply-v148-jeanjc-rank-fix.py' not in s: raise SystemExit('Reparo v1.48 deve continuar na cadeia de build')
if 'apply-v149-career-history.py server-local/runtime' not in s: raise SystemExit('Patch v1.49 nao entrou no build')
p.write_text(s,encoding='utf-8')
print('Workflow de release preparado para GAT Server 1.0.49.')
