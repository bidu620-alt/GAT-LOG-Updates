from pathlib import Path
import sys

path=Path(sys.argv[1])
s=path.read_text(encoding='utf-8')

# Meta mensal oficial: 30 viagens validas. Classificacao serve para organizar o
# catalogo, mas nao deve atrasar nem duplicar o x/30.
pending_old="UPDATE profiles SET total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?"
pending_new="UPDATE profiles SET monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1,total_km=total_km+?,xp=xp+?,points=points+?,perfect_trips=perfect_trips+?,penalty_xp=penalty_xp+?,speed_fines=speed_fines+?,safety_score=MAX(0,100-((penalty_xp+?)*0.1)),current_mission_json=NULL,updated_at=? WHERE user=?"
if pending_old not in s:
    raise SystemExit('Nao encontrei update de entrega pendente para contar no mes.')
s=s.replace(pending_old,pending_new,1)

# Entregas ja classificadas tambem contam por viagem, limitadas a meta mensal.
normal_old="monthly_completed=monthly_completed+1,total_deliveries=total_deliveries+1"
normal_new="monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1"
if normal_old not in s:
    raise SystemExit('Nao encontrei incremento mensal da entrega classificada.')
s=s.replace(normal_old,normal_new,1)

# Repetir um trabalho continua sendo uma viagem valida. Mantemos a classificacao e
# as tabelas de catalogo, mas nao desviamos a viagem para o antigo modo XP-only.
repeat_old="if(workAlreadyCompleted){\n   await env.DB.prepare('UPDATE profiles SET xp=xp+?,current_mission_json=NULL,updated_at=? WHERE user=?')"
repeat_new="if(false&&workAlreadyCompleted){\n   await env.DB.prepare('UPDATE profiles SET xp=xp+?,current_mission_json=NULL,updated_at=? WHERE user=?')"
if repeat_old not in s:
    raise SystemExit('Nao encontrei antigo branch XP-only de repeticao.')
s=s.replace(repeat_old,repeat_new,1)

# A classificacao manual posterior nao pode somar novamente uma viagem que ja contou.
classify_old="if(counted)await env.DB.prepare('UPDATE profiles SET monthly_completed=monthly_completed+1,updated_at=? WHERE user=?').bind(at,q.user).run();"
classify_new="if(counted)await env.DB.prepare('UPDATE profiles SET updated_at=? WHERE user=?').bind(at,q.user).run();"
if classify_old not in s:
    raise SystemExit('Nao encontrei incremento tardio da classificacao manual.')
s=s.replace(classify_old,classify_new,1)

# O retorno pendente deixa explicito que a viagem ja contou no progresso mensal.
s=s.replace("classification_status:'pending'};","classification_status:'pending',monthly_increment:1};",1)

required=[
    'monthly_completed=MIN(monthly_goal,monthly_completed+1),total_deliveries=total_deliveries+1',
    'if(false&&workAlreadyCompleted)',
    "if(counted)await env.DB.prepare('UPDATE profiles SET updated_at=? WHERE user=?')",
    "classification_status:'pending',monthly_increment:1"
]
for marker in required:
    if marker not in s:
        raise SystemExit('Patch de 30 viagens incompleto: '+marker)

path.write_text(s,encoding='utf-8')
print('Meta mensal local: cada entrega valida conta imediatamente no 30/30; classificacao nao duplica.')
