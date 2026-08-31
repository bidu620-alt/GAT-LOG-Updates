from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

# Uma entrega nunca pode concluir no instante em que a carga e aceita.
# Basta exigir pelo menos 1 km de progresso real, por distancia restante ou odometro.
anchor="planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);\n"
insert="""planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);\n const odoNow=num(raw,'truck.odometer','truck.odometerKm','truck.odometer_km');\n"""
if 'const odoNow=' not in s:
    if anchor not in s: raise SystemExit('ponto de telemetria da missao nao encontrado')
    s=s.replace(anchor,insert,1)

old_activation="job_latch_key:f.job_latch_key||'',started_at:m.started_at||t};"
new_activation="job_latch_key:f.job_latch_key||'',start_remaining_km:teleKm>0?teleKm:0,start_odometer_km:odoNow>0?odoNow:0,trip_progress_confirmed:false,started_at:m.started_at||t};"
if 'trip_progress_confirmed:false' not in s:
    if old_activation not in s: raise SystemExit('objeto de ativacao da missao nao encontrado')
    s=s.replace(old_activation,new_activation,1)

guard=""" if(m.state==='active'){\n   let guardChanged=false;\n   if(!(Number(m.start_remaining_km)>0)&&teleKm>0){m.start_remaining_km=teleKm;guardChanged=true}\n   if(!(Number(m.start_odometer_km)>0)&&odoNow>0){m.start_odometer_km=odoNow;guardChanged=true}\n   if(m.trip_progress_confirmed!==true){\n     const startRem=Number(m.start_remaining_km)||0,startOdo=Number(m.start_odometer_km)||0;\n     const byRemaining=startRem>0&&teleKm>=0&&(startRem-teleKm)>=1;\n     const byOdometer=startOdo>0&&odoNow>0&&Math.abs(odoNow-startOdo)>=1;\n     if(byRemaining||byOdometer){m.trip_progress_confirmed=true;guardChanged=true}\n   }\n   if(guardChanged)await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();\n }\n if(delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};\n"""
if "reason:'no_trip_progress'" not in s:
    marker=" if(!delivered&&cancelled&&!hasLoadedJob&&(m.state==='active'||m.state==='suspended'))"
    if marker not in s:
        marker=" if(!delivered&&cancelled&&(m.state==='active'||m.state==='suspended'))"
    if marker not in s: raise SystemExit('ponto de guarda da entrega nao encontrado')
    s=s.replace(marker,guard+marker,1)

s=s.replace("const VERSION='1.0.47-cloudflare';","const VERSION='1.0.48-cloudflare';",1)

required=['trip_progress_confirmed:false',"reason:'no_trip_progress'", "Math.abs(odoNow-startOdo)>=1"]
for x in required:
    if x not in s: raise SystemExit('guarda de entrega incompleta: '+x)

p.write_text(s,encoding='utf-8')
print('Guarda de entrega 1.0.48: exige 1 km de progresso real antes de concluir.')
