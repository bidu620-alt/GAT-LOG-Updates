from pathlib import Path

p = Path('worker.js')
s = p.read_text(encoding='utf-8')

old = """ if(f.on_job&&f.cargo_name){if(m.state!=='active'||m.cargo!==f.cargo_name||m.source!==f.source_city||m.destination!==f.destination_city){m={...m,state:'active',min_km:MIN_KM,cargo:f.cargo_name,source:f.source_city,destination:f.destination_city,weight_kg:f.mass_kg,planned_distance_km:planned,map_mode:isRbr?'rbr':'base',distance_source:isRbr?'gat_telemetry_remaining_km':'ets2_job_planned_distance',rbr_start_remaining_km:isRbr?teleKm:undefined,started_at:m.started_at||t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run()}}
"""

new = """ if(f.on_job&&f.cargo_name){
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

if 'const sameCargo=norm(m.cargo)' not in s:
    if old not in s:
        raise SystemExit('Bloco de trabalho ativo esperado nao encontrado')
    s = s.replace(old, new, 1)

s = s.replace("const VERSION='1.0.42-cloudflare';", "const VERSION='1.0.43-cloudflare';", 1)
p.write_text(s, encoding='utf-8')
print('Rota ativa estabilizada: variacao de traducao da cidade nao reinicia a missao.')
