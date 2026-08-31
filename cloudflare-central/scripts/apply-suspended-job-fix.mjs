import fs from 'node:fs';

const file = new URL('../worker.js', import.meta.url);
let s = fs.readFileSync(file, 'utf8');

if (!s.includes("type:'mission_suspended'")) {
  const old = " const delivered=bool(raw,'gameplay.jobDelivered','jobDelivered');\n if(!delivered&&!f.on_job&&m.state==='active'){m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}\n if(!delivered)return f.on_job?{type:'mission_in_progress',mission:m,distance_km:planned}:null;";
  const next = " const delivered=bool(raw,'gameplay.jobDelivered','jobDelivered');\n const cancelled=bool(raw,'gameplay.jobCancelled','jobCancelled','gameplay.jobCanceled','jobCanceled','job.cancelled','job.canceled');\n if(!delivered&&cancelled&&(m.state==='active'||m.state==='suspended')){m=await resetAssigned(env,user,m,'job_cancelled');return{type:'mission_cancelled',mission:m}}\n if(!delivered&&!f.on_job&&m.state==='active'){m={...m,state:'suspended',suspended_at:t};await env.DB.prepare('UPDATE profiles SET current_mission_json=?,updated_at=? WHERE user=?').bind(JSON.stringify(m),t,user).run();return{type:'mission_suspended',mission:m}}\n if(!delivered&&!f.on_job&&m.state==='suspended')return{type:'mission_suspended',mission:m};\n if(!delivered)return f.on_job?{type:'mission_in_progress',mission:m,distance_km:planned}:null;";
  if (!s.includes(old)) throw new Error('Bloco de cancelamento esperado nao encontrado');
  s = s.replace(old, next);
  s = s.replace("const VERSION='1.0.41-cloudflare';", "const VERSION='1.0.42-cloudflare';");
  fs.writeFileSync(file, s, 'utf8');
}

console.log('Suspended-job protection ready');
