from pathlib import Path

p=Path('worker.js')
s=p.read_text(encoding='utf-8')

old="mass_kg:num(raw,'mass_kg','cargo_mass','cargoMass','job.cargoMass','job.mass_kg')"
new="mass_kg:num(raw,'mass_kg','cargo_mass','cargoMass','cargo_mass_kg','job.cargoMass','Job.CargoMass','job.mass_kg','job.cargo.mass_kg')"
if old not in s:
    raise SystemExit('campo mass_kg esperado nao encontrado')
s=s.replace(old,new,1)

s=s.replace("on_job:bool(raw,'on_job','onJob','gameplay.onJob','job.onJob','job.active')",
            "on_job:bool(raw,'on_job','onJob','gameplay.onJob','job.onJob','Job.OnJob','job.active','Job.Active')",1)
s=s.replace("cargo_name:str(raw,'cargo_name','cargo','job.cargo','job.cargoName','job.cargo.name')",
            "cargo_name:str(raw,'cargo_name','cargo','job.cargo','job.cargoName','Job.CargoName','job.cargo.name','job.name')",1)

# A Telemetria 1.0.23 cria um registro local de viagem (job latch).
# O Worker precisa enxergar esses campos explicitamente para confiar no registro do cliente.
marker="gat_map:str(raw,'gat_map','map_mode','gatMap')||'base'"
replacement="gat_map:str(raw,'gat_map','map_mode','gatMap')||'base',job_latched:bool(raw,'job_latched','jobLatched'),job_latch_key:str(raw,'job_latch_key','jobLatchKey')"
if marker not in s:
    raise SystemExit('fim do flat esperado nao encontrado')
s=s.replace(marker,replacement,1)

p.write_text(s,encoding='utf-8')
print('Aliases ampliados: peso/carga/trabalho + job_latched/job_latch_key reconhecidos pelo Worker.')
