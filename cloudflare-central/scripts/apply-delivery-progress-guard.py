from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

# Evita que um pulso antigo de jobDelivered conclua um trabalho que acabou de ser iniciado.
# A entrega so e aceita depois que o sinal de entrega ficou falso ao menos uma vez
# e a telemetria confirmou pelo menos 1 km de progresso real da viagem.
anchor="planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);\n"
insert="""planned=isRbr?(teleKm||baseKm):(baseKm||teleKm);\n const deliveredNow=bool(raw,'gameplay.jobDelivered','jobDelivered'),odoNow=num(raw,'truck.odometer','truck.odometerKm','truck.odometer_km');\n"""
if 'const deliveredNow=' not in s:
    if anchor not in s: raise SystemExit('ponto de telemetria da missao nao encontrado')
    s=s.replace(anchor,insert,1)

old_activation="job_latch_key:f.job_latch_key||'',started_at:m.started_at||t};"
new_activation="job_latch_key:f.job_latch_key||'',delivery_armed:!deliveredNow,start_remaining_km:teleKm>0?teleKm:0,start_odometer_km:odoNow>0?odoNow:0,trip_progress_confirmed:false,started_at:m.started_at||t};"
if 'trip_progress_confirmed:false' not in s:
    if old_activation not in s: raise SystemExit('objeto de ativacao da missao nao encontrado')
    s=s.replace(old_activation,new_activation,1)

cancel_line=" const cancelled=bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled');\n"
guard=""" const cancelled=bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled');\n if(m.state==='active'){\n   let guardChanged=false;\n   if(!delivered&&m.delivery_armed!==true){m.delivery_armed=true;guardChanged=true}\n   if(!delivered&&!(Number(m.start_remaining_km)>0)&&teleKm>0){m.start_remaining_km=teleKm;guardChanged=true}\n   if(!delivered&&!(Number(m.start_odometer_km)>0)&&odoNow>0){m.start_odometer_km=odoNow;guardChanged=true}\n   if(m.trip_progress_confirmed!==true){\n     const startRem=Number(m.start_remaining_km)||0,startOdo=Number(m.start_odometer_km)||0;\n     const byRemaining=startRem>0&&teleKm>=0&&(startRem-teleKm)>=1;\n     const byOdometer=startOdo>0&&odoNow>0&&Math.abs(odoNow-startOdo)>=1;\n     if(byRemaining||byOdometer){m.trip_progress_confirmed=true;guardChanged=true}\n   }\n   if(guardChanged)await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();\n }\n if(delivered&&m.state==='active'&&m.delivery_armed!==true)return{type:'delivery_ignored',reason:'delivery_signal_not_armed'};\n if(delivered&&m.state==='active'&&m.trip_progress_confirmed!==true)return{type:'delivery_ignored',reason:'no_trip_progress'};\n"""
if "reason:'no_trip_progress'" not in s:
    if cancel_line not in s: raise SystemExit('ponto de guarda da entrega nao encontrado')
    s=s.replace(cancel_line,guard,1)

s=s.replace("const VERSION='1.0.47-cloudflare';","const VERSION='1.0.48-cloudflare';",1)

required=['delivery_armed:!deliveredNow','trip_progress_confirmed:false',"reason:'delivery_signal_not_armed'", "reason:'no_trip_progress'", "Math.abs(odoNow-startOdo)>=1"]
for x in required:
    if x not in s: raise SystemExit('guarda de entrega incompleta: '+x)

p.write_text(s,encoding='utf-8')
print('Guarda de entrega 1.0.48 aplicado: evento armado + 1 km de progresso real antes de concluir.')
