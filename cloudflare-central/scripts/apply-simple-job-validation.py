from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

old=""" const minKm=Math.max(1,Number(m.min_km)||MIN_KM);
 if(f.on_job&&(f.cargo_id||f.cargo_name)){
   const cargoAccepted=await cargoOK(env,m,f.cargo_name,f.cargo_id),distanceAccepted=planned>=minKm;
   if(cargoAccepted&&distanceAccepted){
     const sameCargo=(f.cargo_id&&m.cargo_id)?clean(f.cargo_id)===clean(m.cargo_id):norm(m.cargo)===norm(f.cargo_name);
     if(m.state==='suspended'&&sameCargo){
       m={...m,state:'active',resumed_at:t};delete m.suspended_at;
       await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
     }else if(m.state!=='active'||!sameCargo){
       m={...m,state:'active',min_km:minKm,cargo:f.cargo_name||m.cargo||'Carga',cargo_id:f.cargo_id||m.cargo_id||'',source:f.source_city||m.source||'',source_city_id:f.source_city_id||m.source_city_id||'',destination:f.destination_city||m.destination||'',destination_city_id:f.destination_city_id||m.destination_city_id||'',weight_kg:f.mass_kg,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_planned_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,started_at:m.started_at||t};
       await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
     }
   }else if(!cargoAccepted){return{type:'mission_waiting',reason:'cargo_not_compatible',cargo_id:f.cargo_id||'',cargo:f.cargo_name||'',min_km:minKm}}
   else{return{type:'mission_waiting',reason:'distance_below_minimum',distance_km:planned,min_km:minKm}}
 }
"""
new=""" const minKm=Math.max(1,Number(m.min_km)||MIN_KM);
 const hasLoadedJob=Boolean(f.job_latched)||Boolean(f.cargo_id||f.cargo_name)&&Number(f.mass_kg)>0;
 if(hasLoadedJob){
   if(m.state==='suspended'){
     m={...m,state:'active',resumed_at:t,job_latch_key:f.job_latch_key||m.job_latch_key||''};delete m.suspended_at;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }else if(m.state!=='active'){
     if(planned<minKm)return{type:'mission_waiting',reason:'distance_below_minimum',distance_km:planned,min_km:minKm};
     m={...m,state:'active',min_km:minKm,cargo:f.cargo_name||m.cargo||'Carga',cargo_id:f.cargo_id||m.cargo_id||'',source:f.source_city||m.source||'',source_city_id:f.source_city_id||m.source_city_id||'',destination:f.destination_city||m.destination||'',destination_city_id:f.destination_city_id||m.destination_city_id||'',weight_kg:f.mass_kg||m.weight_kg||0,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,job_latch_key:f.job_latch_key||'',started_at:m.started_at||t};
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }
 }
"""
if old not in s:
    raise SystemExit('bloco de validacao cargoId esperado nao encontrado')
s=s.replace(old,new,1)

old_delivery=""" if(!await cargoOK(env,m,m.cargo||f.cargo_name,m.cargo_id||f.cargo_id)){await resetAssigned(env,user,m,'cargo_not_compatible',{last_cargo:m.cargo||f.cargo_name,last_cargo_id:m.cargo_id||f.cargo_id});return{type:'delivery_rejected',reason:'cargo_not_compatible'}}
"""
if old_delivery in s:
    s=s.replace(old_delivery,'',1)

# O latch local/carga real e a fonte de verdade para manter a viagem ativa.
s=s.replace("if(!delivered&&!f.on_job&&m.state==='active')", "if(!delivered&&!hasLoadedJob&&m.state==='active')",1)
s=s.replace("if(!delivered&&!f.on_job&&m.state==='suspended')", "if(!delivered&&!hasLoadedJob&&m.state==='suspended')",1)
s=s.replace("if(!delivered)return f.on_job?{type:'mission_in_progress',mission:m,distance_km:planned}:null;", "if(!delivered)return hasLoadedJob?{type:'mission_in_progress',mission:m,distance_km:planned}:null;",1)

# Canonico GAT: cliente novo envia gat_job_event=delivered|cancelled.
# Para clientes antigos, os flags do TruckSim so valem quando nao ha carga ativa no mesmo pacote.
s=s.replace(" const delivered=bool(raw,'gameplay.jobDelivered','jobDelivered');\n const cancelled=bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled');",
" const gatJobEvent=clean(str(raw,'gat_job_event','gatJobEvent'));\n const delivered=gatJobEvent==='delivered'||(!hasLoadedJob&&bool(raw,'gameplay.jobDelivered','jobDelivered'));\n const cancelled=gatJobEvent==='cancelled'||(!hasLoadedJob&&bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled'));",1)

# Nunca cancelar a missao se o mesmo pacote prova que existe carga real ativa.
s=s.replace("if(!delivered&&cancelled&&(m.state==='active'||m.state==='suspended'))", "if(!delivered&&cancelled&&!hasLoadedJob&&(m.state==='active'||m.state==='suspended'))",1)

s=s.replace("const VERSION='1.0.44-cloudflare';","const VERSION='1.0.47-cloudflare';",1)

required=["Boolean(f.job_latched)", "job_latch_key:f.job_latch_key", "!hasLoadedJob&&m.state==='active'", "gat_job_event", "gatJobEvent==='delivered'", "cancelled&&!hasLoadedJob", "reason:'distance_below_minimum'"]
for x in required:
    if x not in s: raise SystemExit('patch simples incompleto: '+x)
segment=s[s.find('async function processMission'):]
if "reason:'cargo_not_compatible'" in segment:
    raise SystemExit('ainda existe bloqueio por catalogo dentro de processMission')

p.write_text(s,encoding='utf-8')
print('Validacao aplicada: carga real mantem trabalho ativo e eventos GAT canonicos encerram a viagem.')
