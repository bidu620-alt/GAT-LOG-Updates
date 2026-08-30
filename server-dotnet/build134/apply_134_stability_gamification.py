from pathlib import Path
import re

agent=Path('/tmp/gat-src/cmd/agent/main.go')
core=Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c=core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.33"' in c:
    c=c.replace('InternalVersion = "1.0.33"','InternalVersion = "1.0.34"',1)
elif 'InternalVersion = "1.0.34"' not in c:
    raise SystemExit('versao 1.0.33 do agente nao encontrada')
core.write_text(c,encoding='utf-8')

s=agent.read_text(encoding='utf-8')

# Campos de bônus de viagem perfeita.
if 'PerfectBonusXP' not in s:
    m=re.search(r'(\tTruckDamagePct\s+float64\s+`json:"truck_damage_delta_pct,omitempty"`\n)',s)
    if not m:
        raise SystemExit('campos de regra da entrega nao encontrados')
    extra='\tPerfectBonusXP   int     `json:"perfect_bonus_xp,omitempty"`\n\tPerfectTrip      bool    `json:"perfect_trip,omitempty"`\n'
    s=s[:m.end()]+extra+s[m.end():]

# Proteções de entrada e regra de viagem perfeita (+5 XP).
old=re.search(r'func gatTripRuleBreakdown\(distance float64, q gatTripCompleteRequest\).*?\n}\n\nfunc gatApplyRulesToDelivery\(d \*gatDelivery, distance float64, q gatTripCompleteRequest\) \{.*?\n}\n\nfunc gatTripRuleResponse\(d \*gatDelivery\) map\[string\]any \{.*?\n}\n',s,re.S)
if not old:
    raise SystemExit('bloco de regras 1.0.32 nao encontrado')
new='''func gatClamp(v, min, max float64) float64 {
    if v < min { return min }
    if v > max { return max }
    return v
}

func gatSanitizeTripRequest(q *gatTripCompleteRequest) {
    if q == nil { return }
    q.WeightKg = gatClamp(q.WeightKg, 0, 200000)
    q.PlannedDistanceKm = gatClamp(q.PlannedDistanceKm, 0, 15000)
    q.FirstObservedRemainingKm = gatClamp(q.FirstObservedRemainingKm, 0, 15000)
    q.CargoDamagePct = gatClamp(gatDamagePct(q.CargoDamagePct), 0, 100)
    if q.TruckDamageStartPct >= 0 { q.TruckDamageStartPct = gatClamp(gatDamagePct(q.TruckDamageStartPct), 0, 100) }
    if q.TruckDamageMaxPct >= 0 { q.TruckDamageMaxPct = gatClamp(gatDamagePct(q.TruckDamageMaxPct), 0, 100) }
    if q.SpeedFines < 0 { q.SpeedFines = 0 }
    if q.SpeedFines > 50 { q.SpeedFines = 50 }
}

func gatTripRuleBreakdown(distance float64, q gatTripCompleteRequest) (base int, cargoPenalty int, truckPenalty int, speedPenalty int, penalty int, perfectBonus int, final int, truckDelta float64) {
    distance = gatClamp(distance, 0, 15000)
    base = gatXPForDistance(distance)
    cargoPenalty = gatCargoDamagePenalty(q.CargoDamagePct)
    start := q.TruckDamageStartPct
    maxd := q.TruckDamageMaxPct
    if start >= 0 && maxd >= 0 && maxd > start { truckDelta = maxd-start }
    truckDelta = gatClamp(truckDelta, 0, 100)
    truckPenalty = gatTruckDamagePenalty(truckDelta)
    fines := q.SpeedFines
    if fines < 0 { fines = 0 }
    if fines > 50 { fines = 50 }
    speedPenalty = fines*3
    penalty = cargoPenalty+truckPenalty+speedPenalty
    if distance >= 250 && fines == 0 && q.CargoDamagePct <= 0.5 && truckDelta <= 0.5 {
        perfectBonus = 5
    }
    final = base-penalty+perfectBonus
    if final < 0 { final = 0 }
    return
}

func gatApplyRulesToDelivery(d *gatDelivery, distance float64, q gatTripCompleteRequest) {
    if d==nil { return }
    base,cargoPenalty,truckPenalty,speedPenalty,penalty,perfectBonus,final,delta:=gatTripRuleBreakdown(distance,q)
    d.BaseXP=base; d.CargoPenaltyXP=cargoPenalty; d.TruckPenaltyXP=truckPenalty; d.SpeedPenaltyXP=speedPenalty
    d.PenaltyXP=penalty; d.PerfectBonusXP=perfectBonus; d.PerfectTrip=perfectBonus>0; d.XPAwarded=final; d.SpeedFines=q.SpeedFines
    d.CargoDamagePct=gatClamp(q.CargoDamagePct,0,100); d.TruckDamagePct=delta
}

func gatTripRuleResponse(d *gatDelivery) map[string]any {
    if d==nil { return map[string]any{} }
    return map[string]any{
        "base_xp":d.BaseXP,"penalty_xp":d.PenaltyXP,"perfect_bonus_xp":d.PerfectBonusXP,"perfect_trip":d.PerfectTrip,"xp_awarded":d.XPAwarded,
        "speed_fines":d.SpeedFines,"speed_penalty_xp":d.SpeedPenaltyXP,
        "cargo_damage_pct":d.CargoDamagePct,"cargo_penalty_xp":d.CargoPenaltyXP,
        "truck_damage_delta_pct":d.TruckDamagePct,"truck_penalty_xp":d.TruckPenaltyXP,
    }
}
'''
s=s[:old.start()]+new+s[old.end():]

# Sanitiza recibos antes de qualquer cálculo/armazenamento.
needle='''\tif decode(r,&q)!=nil { jsonOut(w,400,map[string]any{"ok":false,"error":"bad_request"}); return }
\tq.TripID=strings.TrimSpace(q.TripID);'''
repl='''\tif decode(r,&q)!=nil { jsonOut(w,400,map[string]any{"ok":false,"error":"bad_request"}); return }
\tgatSanitizeTripRequest(&q)
\tq.TripID=strings.TrimSpace(q.TripID);'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'gatSanitizeTripRequest(&q)' not in s:
    raise SystemExit('ponto de sanitizacao do recibo nao encontrado')

# Estatísticas de direção segura e conquistas no perfil público.
support='''
func gatSafetyStats(p *gatDriverProgress) map[string]any {
    total := len(p.Deliveries)
    fines, penalty, perfect, clean := 0, 0, 0, 0
    cargoSum, truckSum, noFineKm := 0.0, 0.0, 0.0
    cargoCount, truckCount := 0, 0
    for _, d := range p.Deliveries {
        f := d.SpeedFines; if f < 0 { f = 0 }
        fines += f
        pen := d.PenaltyXP; if pen < 0 { pen = 0 }
        penalty += pen
        if d.PerfectTrip || d.PerfectBonusXP > 0 { perfect++ }
        if pen == 0 { clean++ }
        if f == 0 { noFineKm += gatClamp(d.DistanceKm,0,15000) }
        if d.CargoDamagePct > 0 { cargoSum += gatClamp(d.CargoDamagePct,0,100); cargoCount++ }
        if d.TruckDamagePct > 0 { truckSum += gatClamp(d.TruckDamagePct,0,100); truckCount++ }
    }
    avgCargo, avgTruck := 0.0, 0.0
    if cargoCount > 0 { avgCargo = cargoSum/float64(cargoCount) }
    if truckCount > 0 { avgTruck = truckSum/float64(truckCount) }
    score := 0.0
    if total > 0 {
        cleanRate := float64(clean)/float64(total)*100
        perfectRate := float64(perfect)/float64(total)*100
        fineRate := float64(fines)/float64(total)
        score = cleanRate*0.70 + perfectRate*0.30 - fineRate*8 - avgCargo*1.5 - avgTruck
        if score < 0 { score = 0 }; if score > 100 { score = 100 }
    }
    return map[string]any{
        "deliveries":total,"speed_fines":fines,"penalty_xp":penalty,"perfect_trips":perfect,"clean_trips":clean,
        "avg_cargo_damage_pct":avgCargo,"avg_truck_damage_pct":avgTruck,"no_fine_km":noFineKm,"score":score,
    }
}

func gatAchievements(p *gatDriverProgress) []map[string]any {
    st := gatSafetyStats(p)
    perfect := st["perfect_trips"].(int)
    noFineKm := st["no_fine_km"].(float64)
    out := []map[string]any{}
    add := func(id,title,desc string, unlocked bool) { out=append(out,map[string]any{"id":id,"title":title,"description":desc,"unlocked":unlocked}) }
    add("first_delivery","Primeira Entrega","Conclua sua primeira entrega GAT.",p.TotalDeliveries>=1)
    add("ten_deliveries","Na Estrada","Conclua 10 entregas GAT.",p.TotalDeliveries>=10)
    add("perfect_ten","Direção de Ouro","Conclua 10 viagens perfeitas.",perfect>=10)
    add("no_fine_5000","Pé Leve","Percorra 5.000 km em entregas sem multa de velocidade.",noFineKm>=5000)
    add("monthly_30","Meta do Mês","Conclua os 30 trabalhos do mês.",p.MonthlyCompleted>=30)
    add("km_50000","Veterano GAT","Ultrapasse 50.000 km acumulados.",p.TotalKm>=50000)
    return out
}

'''
if 'func gatSafetyStats(' not in s:
    pos=s.find('func gatAuthUser(')
    if pos<0: raise SystemExit('gatAuthUser nao encontrado')
    s=s[:pos]+support+s[pos:]

prof_pat=re.search(r'func publicGatProfile\(p \*gatDriverProgress\) map\[string\]any \{.*?\n}\n\nfunc gatSafetyStats',s,re.S)
if not prof_pat:
    raise SystemExit('publicGatProfile nao encontrado para extensao')
prof='''func publicGatProfile(p *gatDriverProgress) map[string]any {
    var mission any=nil
    if p.CurrentMission!=nil { mission=p.CurrentMission }
    history:=p.Deliveries
    if len(history)>100 { history=history[len(history)-100:] }
    return map[string]any{
        "user":p.User,"month":p.Month,"monthly_completed":p.MonthlyCompleted,"monthly_goal":30,
        "total_deliveries":p.TotalDeliveries,"total_km":p.TotalKm,"xp":p.XP,"level":gatLevel(p.XP),"points":p.Points,
        "xp_rule_pending":false,"points_rule_pending":true,"current_mission":mission,"deliveries":history,
        "safety":gatSafetyStats(p),"achievements":gatAchievements(p),
    }
}

func gatSafetyStats'''
s=s[:prof_pat.start()]+prof+s[prof_pat.end():]

# Ranking de direção segura.
if '/api/public/safety-ranking' not in s:
    needle='\tm.HandleFunc("/api/public/ranking", a.publicRanking)\n'
    if needle not in s: raise SystemExit('rota public/ranking nao encontrada')
    s=s.replace(needle,needle+'\tm.HandleFunc("/api/public/safety-ranking", a.publicSafetyRanking)\n',1)

rank_handler='''
func (a *agent) publicSafetyRanking(w http.ResponseWriter, r *http.Request) {
    if r.Method!=http.MethodGet { jsonOut(w,405,map[string]any{"ok":false,"error":"method_not_allowed"}); return }
    gatProgressMu.Lock(); defer gatProgressMu.Unlock()
    all:=loadGatProgress()
    out:=make([]map[string]any,0,len(all))
    for _,p:=range all {
        ensureGatProgress(all,p.User)
        st:=gatSafetyStats(p)
        out=append(out,map[string]any{"user":p.User,"score":st["score"],"perfect_trips":st["perfect_trips"],"clean_trips":st["clean_trips"],"speed_fines":st["speed_fines"],"avg_cargo_damage_pct":st["avg_cargo_damage_pct"],"avg_truck_damage_pct":st["avg_truck_damage_pct"],"total_km":p.TotalKm,"total_deliveries":p.TotalDeliveries})
    }
    sort.Slice(out,func(i,j int)bool{
        si,_:=out[i]["score"].(float64); sj,_:=out[j]["score"].(float64)
        if si!=sj { return si>sj }
        pi,_:=out[i]["perfect_trips"].(int); pj,_:=out[j]["perfect_trips"].(int)
        if pi!=pj { return pi>pj }
        return strings.ToLower(fmt.Sprint(out[i]["user"]))<strings.ToLower(fmt.Sprint(out[j]["user"]))
    })
    _=saveGatProgress(all)
    jsonOut(w,200,map[string]any{"ok":true,"ranking":out})
}

'''
if 'func (a *agent) publicSafetyRanking(' not in s:
    pos=s.find('func (a *agent) publicRanking(')
    if pos<0: raise SystemExit('publicRanking nao encontrado')
    s=s[:pos]+rank_handler+s[pos:]

# Backup automático diário, mantendo os últimos 7.
backup='''
func gatBackupRoot() string { return filepath.Join(core.DataDir(),"backups") }

func gatBackupStatus() (string,int) {
    root:=gatBackupRoot(); entries,err:=os.ReadDir(root); if err!=nil { return "",0 }
    names:=[]string{}
    for _,e:=range entries { if e.IsDir() { names=append(names,e.Name()) } }
    sort.Strings(names)
    if len(names)==0 { return "",0 }
    return names[len(names)-1],len(names)
}

func gatRotateBackups() {
    root:=gatBackupRoot(); entries,err:=os.ReadDir(root); if err!=nil { return }
    names:=[]string{}
    for _,e:=range entries { if e.IsDir() { names=append(names,e.Name()) } }
    sort.Strings(names)
    for len(names)>7 { _=os.RemoveAll(filepath.Join(root,names[0])); names=names[1:] }
}

func gatCreateBackup(reason string) (string,error) {
    base:=core.DataDir(); root:=gatBackupRoot(); if err:=os.MkdirAll(root,0755); err!=nil { return "",err }
    stamp:=time.Now().UTC().Format("2006-01-02_150405")
    dest:=filepath.Join(root,stamp); if err:=os.MkdirAll(dest,0755); err!=nil { return "",err }
    err:=filepath.Walk(base,func(path string,info os.FileInfo,err error) error {
        if err!=nil { return nil }
        rel,e:=filepath.Rel(base,path); if e!=nil || rel=="." { return nil }
        if info.IsDir() {
            if rel=="backups" || strings.HasPrefix(rel,"backups"+string(os.PathSeparator)) { return filepath.SkipDir }
            return nil
        }
        if info.Size()>50*1024*1024 { return nil }
        ext:=strings.ToLower(filepath.Ext(info.Name()))
        if ext==".log" || ext==".tmp" || ext==".exe" || ext==".dll" { return nil }
        target:=filepath.Join(dest,rel); if err:=os.MkdirAll(filepath.Dir(target),0755); err!=nil { return nil }
        b,e:=os.ReadFile(path); if e!=nil { return nil }; _=os.WriteFile(target,b,0644); return nil
    })
    if err!=nil { return "",err }
    _=os.WriteFile(filepath.Join(dest,"backup-info.txt"),[]byte("GAT-LOG backup "+stamp+" | "+reason+"\n"),0644)
    gatRotateBackups(); return stamp,nil
}

func gatEnsureDailyBackup() {
    today:=time.Now().UTC().Format("2006-01-02")
    root:=gatBackupRoot(); entries,_:=os.ReadDir(root)
    for _,e:=range entries { if e.IsDir() && strings.HasPrefix(e.Name(),today+"_") { return } }
    if stamp,err:=gatCreateBackup("automatico diario"); err!=nil { core.AppendLog("Backup GAT falhou: %v",err) } else { core.AppendLog("Backup GAT criado: %s",stamp) }
}

func gatBackupLoop() {
    time.Sleep(8*time.Second); gatEnsureDailyBackup()
    ticker:=time.NewTicker(time.Hour); defer ticker.Stop()
    for range ticker.C { gatEnsureDailyBackup() }
}

func gatDataSize() int64 {
    var total int64
    _=filepath.Walk(core.DataDir(),func(path string,info os.FileInfo,err error) error {
        if err!=nil || info==nil { return nil }
        rel,_:=filepath.Rel(core.DataDir(),path)
        if info.IsDir() && (rel=="backups" || strings.HasPrefix(rel,"backups"+string(os.PathSeparator))) { return filepath.SkipDir }
        if !info.IsDir() { total+=info.Size() }
        return nil
    })
    return total
}

'''
if 'func gatBackupLoop(' not in s:
    pos=s.find('func gatSafetyStats(')
    if pos<0: raise SystemExit('ponto para backup nao encontrado')
    s=s[:pos]+backup+s[pos:]

if 'go gatBackupLoop()' not in s:
    needle='\tgo a.ensureFunnel()\n'
    if needle not in s: raise SystemExit('startup ensureFunnel nao encontrado')
    s=s.replace(needle,needle+'\tgo gatBackupLoop()\n',1)

# Saúde da Central + backup manual no Admin.
if '/api/site/admin/health' not in s:
    needle='\tm.HandleFunc("/api/site/admin/audit", a.siteAdminAudit)\n'
    if needle not in s: raise SystemExit('rota admin/audit nao encontrada')
    s=s.replace(needle,needle+'\tm.HandleFunc("/api/site/admin/health", a.siteAdminHealth)\n\tm.HandleFunc("/api/site/admin/backup", a.siteAdminBackup)\n',1)

admin_handlers='''
func (a *agent) siteAdminHealth(w http.ResponseWriter, r *http.Request) {
    var q gatAdminRequest
    _,role,ok:=gatSiteAdminAuth(w,r,&q); if !ok { return }
    gatProgressMu.Lock(); all:=loadGatProgress(); online:=0; last:=""
    now:=time.Now().UTC()
    for _,p:=range all {
        if p.LastTelemetryAt!="" && p.LastTelemetryAt>last { last=p.LastTelemetryAt }
        if t,err:=time.Parse(time.RFC3339,p.LastTelemetryAt); err==nil && now.Sub(t)<=25*time.Second { online++ }
    }
    gatProgressMu.Unlock()
    lastBackup,count:=gatBackupStatus()
    jsonOut(w,200,map[string]any{"ok":true,"viewer_role":role,"agent_version":core.InternalVersion,"accounts":len(loadDriverAccounts()),"online_drivers":online,"last_telemetry_at":last,"data_bytes":gatDataSize(),"backup_count":count,"last_backup":lastBackup,"backup_keep":7})
}

func (a *agent) siteAdminBackup(w http.ResponseWriter, r *http.Request) {
    var q gatAdminRequest
    actor,role,ok:=gatSiteAdminAuth(w,r,&q); if !ok { return }
    if role=="moderator" { jsonOut(w,403,map[string]any{"ok":false,"error":"insufficient_role"}); return }
    stamp,err:=gatCreateBackup("manual por "+actor)
    if err!=nil { jsonOut(w,500,map[string]any{"ok":false,"error":"backup_failed"}); return }
    appendGatAdminAudit(actor,"backup","central","backup="+stamp)
    jsonOut(w,200,map[string]any{"ok":true,"backup":stamp})
}

'''
if 'func (a *agent) siteAdminHealth(' not in s:
    pos=s.find('func (a *agent) siteAdminAudit(')
    if pos<0: raise SystemExit('siteAdminAudit nao encontrado')
    s=s[:pos]+admin_handlers+s[pos:]

# Evita velocidade absurda no painel ao vivo (falha de telemetria).
s=s.replace('out["speed_kmh"] = gatTelemetryFloat(tel, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed", "Truck.Speed")',
            'out["speed_kmh"] = gatClamp(gatTelemetryFloat(tel, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed", "Truck.Speed"),0,200)')

checks=['InternalVersion = "1.0.34"','PerfectBonusXP','gatSanitizeTripRequest(&q)','func gatSafetyStats(','/api/public/safety-ranking','func gatBackupLoop(','/api/site/admin/health','go gatBackupLoop()']
for x in checks:
    target=c if x.startswith('InternalVersion') else s
    if x not in target: raise SystemExit('patch incompleto: '+x)

agent.write_text(s,encoding='utf-8')
print('patch 1.0.34 aplicado: estabilidade, backup, viagem perfeita, ranking seguro e saude da Central')
