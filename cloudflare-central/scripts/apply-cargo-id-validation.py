from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

# Helper compacto para comparar cargoId estavel com nomes oficiais do catalogo.
if 'const compact=' not in s:
    s=s.replace("const norm=v=>String(v||'').normalize('NFD').replace(/[\\u0300-\\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();",
                "const norm=v=>String(v||'').normalize('NFD').replace(/[\\u0300-\\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();\nconst compact=v=>norm(v).replace(/[^a-z0-9]/g,'');",1)

# Expõe IDs estáveis da telemetria SCS/TruckSim sem depender de idioma.
old="cargo_name:str(raw,'cargo_name','cargo','job.cargo','job.cargoName','job.cargo.name'),source_city:str(raw,'source_city','source','job.sourceCity','job.source.cityName'),destination_city:str(raw,'destination_city','destination','job.destinationCity','job.destination.cityName'),mass_kg:"
new="cargo_name:str(raw,'cargo_name','cargo','job.cargo','job.cargoName','job.cargo.name'),cargo_id:str(raw,'cargo_id','cargoId','job.cargoId','job.cargo.id','game.job.cargoId'),source_city:str(raw,'source_city','source','job.sourceCity','job.source.cityName'),source_city_id:str(raw,'source_city_id','sourceCityId','job.sourceCityId','job.source.id','game.job.sourceCityId'),destination_city:str(raw,'destination_city','destination','job.destinationCity','job.destination.cityName'),destination_city_id:str(raw,'destination_city_id','destinationCityId','job.destinationCityId','job.destination.id','game.job.destinationCityId'),mass_kg:"
if old in s:
    s=s.replace(old,new,1)

old_cargo="""async function cargoOK(env,mission,cargo){const actual=norm(cargo);if(!actual)return false;if(mission.catalog_id==='custom'){const expected=norm(mission.custom_cargo);return expected.length>=2&&(actual===expected||actual.includes(expected)||expected.includes(actual))}const r=await env.DB.prepare('SELECT compatible_cargos_json FROM work_catalog WHERE id=?').bind(String(mission.catalog_id||'')).first();let names=[];try{names=JSON.parse(r?.compatible_cargos_json||'[]')}catch{}return names.map(norm).some(n=>n&&(actual===n||actual.includes(n)||n.includes(actual)))}
"""
new_cargo="""async function cargoOK(env,mission,cargo,cargoId){
 if(mission.catalog_id==='custom')return true;
 const r=await env.DB.prepare('SELECT compatible_cargos_json FROM work_catalog WHERE id=?').bind(String(mission.catalog_id||'')).first();let names=[];try{names=JSON.parse(r?.compatible_cargos_json||'[]')}catch{}
 const idRaw=String(cargoId||'').trim();
 if(idRaw){
   const base=idRaw.split('.')[0],id=compact(base);
   if(id&&names.some(n=>{const x=compact(n);return x&&(x===id||x.includes(id)||id.includes(x))}))return true;
 }
 const actual=norm(cargo);if(!actual)return false;
 return names.map(norm).some(n=>n&&(actual===n||actual.includes(n)||n.includes(actual)));
}
"""
if old_cargo in s:
    s=s.replace(old_cargo,new_cargo,1)

# Apos a estabilizacao anterior, troca o bloco de ativacao por ID + KM.
old_active=""" if(f.on_job&&f.cargo_name){
   const sameCargo=norm(m.cargo)===norm(f.cargo_name);
   if(m.state==='suspended'&&sameCargo){
     m={...m,state:'active',resumed_at:t};delete m.suspended_at;
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }else if(m.state!=='active'||!sameCargo){
     m={...m,state:'active',min_km:MIN_KM,cargo:f.cargo_name,source:f.source_city,destination:f.destination_city,weight_kg:f.mass_kg,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_planned_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,started_at:m.started_at||t};
     await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();
   }
 }
"""
new_active=""" const minKm=Math.max(1,Number(m.min_km)||MIN_KM);
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
if old_active in s:
    s=s.replace(old_active,new_active,1)

s=s.replace("if(distance<MIN_KM){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:MIN_KM}}",
            "if(distance<minKm){await resetAssigned(env,user,m,'distance_below_minimum',{last_distance_km:distance});return{type:'delivery_rejected',reason:'distance_below_minimum',distance_km:distance,min_km:minKm}}",1)
s=s.replace("if(!await cargoOK(env,m,m.cargo||f.cargo_name)){await resetAssigned(env,user,m,'cargo_not_compatible',{last_cargo:m.cargo||f.cargo_name});return{type:'delivery_rejected',reason:'cargo_not_compatible'}}",
            "if(!await cargoOK(env,m,m.cargo||f.cargo_name,m.cargo_id||f.cargo_id)){await resetAssigned(env,user,m,'cargo_not_compatible',{last_cargo:m.cargo||f.cargo_name,last_cargo_id:m.cargo_id||f.cargo_id});return{type:'delivery_rejected',reason:'cargo_not_compatible'}}",1)

old_route=""" const source=String(m.source||f.source_city||'').trim(),destination=String(m.destination||f.destination_city||'').trim();if(!source||!destination||norm(source)===norm(destination)){await resetAssigned(env,user,m,'invalid_route');return{type:'delivery_rejected',reason:'invalid_route'}}
 const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||'');if(await env.DB.prepare('SELECT 1 FROM routes_completed WHERE user=? AND month_key=? AND route_key=?').bind(user,mk,routeKey).first()){await resetAssigned(env,user,m,'route_already_used');return{type:'delivery_rejected',reason:'route_already_used'}}if(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first()){await resetAssigned(env,user,m,'work_already_completed');return{type:'delivery_rejected',reason:'work_already_completed'}}
"""
new_route=""" const source=String(m.source||f.source_city||'Origem nao informada').trim()||'Origem nao informada',destination=String(m.destination||f.destination_city||'Destino nao informado').trim()||'Destino nao informado';
 const routeKey=`${norm(source)}>${norm(destination)}`,mk=month(t),workId=String(m.catalog_id||'');if(await env.DB.prepare('SELECT 1 FROM work_completed WHERE user=? AND work_id=? AND month_key=?').bind(user,workId,mk).first()){await resetAssigned(env,user,m,'work_already_completed');return{type:'delivery_rejected',reason:'work_already_completed'}}
"""
if old_route in s:
    s=s.replace(old_route,new_route,1)

s=s.replace("const VERSION='1.0.43-cloudflare';","const VERSION='1.0.44-cloudflare';",1)

required=['cargo_id:str(raw','async function cargoOK(env,mission,cargo,cargoId)','mission_waiting','distance_below_minimum','source_city_id:str(raw']
for x in required:
    if x not in s: raise SystemExit('patch cargo-id incompleto: '+x)
if "reason:'invalid_route'" in s: raise SystemExit('validacao antiga de origem/destino ainda presente')
p.write_text(s,encoding='utf-8')
print('Validacao GAT: ID da carga + km; origem/destino apenas informativos; personalizado aceita carga de mod.')
