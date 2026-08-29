from pathlib import Path

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.18"' in c:
    c=c.replace('InternalVersion = "1.0.18"','InternalVersion = "1.0.19"',1)
elif 'InternalVersion = "1.0.19"' not in c:
    raise SystemExit('versao 1.0.18 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

old='func gatLevel(xp int) int { if xp < 0 { xp = 0 }; return 1 + xp/1000 }'
new='func gatLevel(xp int) int { if xp < 0 { xp = 0 }; return 1 + xp/2000 }'
if old in s:
    s=s.replace(old,new,1)
elif new not in s:
    raise SystemExit('gatLevel antigo nao encontrado')

# XP is derived from completed GAT deliveries. This makes it rebuildable and avoids
# duplicated/lost XP counters: 1 valid delivery = 100 XP, 20 deliveries = 1 level.
needle='''\tif p.Deliveries == nil { p.Deliveries = []gatDelivery{} }
\tnowMonth := gatMonth()
'''
replacement='''\tif p.Deliveries == nil { p.Deliveries = []gatDelivery{} }
\tif p.TotalDeliveries < 0 { p.TotalDeliveries = 0 }
\tp.XP = p.TotalDeliveries * 100
\tnowMonth := gatMonth()
'''
if 'p.XP = p.TotalDeliveries * 100' not in s:
    if needle not in s: raise SystemExit('ensureGatProgress patch point not found')
    s=s.replace(needle,replacement,1)

s=s.replace('"level": gatLevel(p.XP), "points": p.Points, "xp_rule_pending": true,',
            '"level": gatLevel(p.XP), "points": p.Points, "xp_rule_pending": false,',1)

old_completion='p.TotalDeliveries++; p.TotalKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true'
new_completion='p.TotalDeliveries++; p.XP = p.TotalDeliveries * 100; p.TotalKm += m.StartKm; p.MonthlyCompleted++; p.CurrentMission = nil; completedNow = true'
if old_completion in s:
    s=s.replace(old_completion,new_completion,1)
elif new_completion not in s:
    raise SystemExit('completion XP patch point not found')

old_response='''\tjsonOut(w, 200, map[string]any{"ok": true, "user": p.User, "driver": strings.TrimSpace(q.Driver), "started": started, "completed_now": completedNow, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "mission": p.CurrentMission, "validation": validation, "xp_awarded": 0, "xp_rule_pending": true})
'''
new_response='''\txpAwarded := 0
\tif completedNow { xpAwarded = 100 }
\tjsonOut(w, 200, map[string]any{"ok": true, "user": p.User, "driver": strings.TrimSpace(q.Driver), "started": started, "completed_now": completedNow, "monthly_completed": p.MonthlyCompleted, "monthly_goal": 40, "mission": p.CurrentMission, "validation": validation, "xp_awarded": xpAwarded, "xp_total": p.XP, "level": gatLevel(p.XP), "xp_rule_pending": false})
'''
if old_response in s:
    s=s.replace(old_response,new_response,1)
elif '"xp_total": p.XP' not in s:
    raise SystemExit('accountTelemetry response patch point not found')

agent.write_text(s,encoding='utf-8')
print('GAT-LOG 1.0.19 XP simples aplicado: 100 XP por missao, 2000 XP por nivel')
