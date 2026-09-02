from pathlib import Path

p=Path('server-local/runtime/worker.js')
s=p.read_text(encoding='utf-8')
old="if(pr?.current_mission_json)throw new HttpError(409,'mission_already_active');"
new="if(pr?.current_mission_json&&clean(s.user)!=='biduzao')throw new HttpError(409,'mission_already_active');"
if old not in s:
    if new in s:
        print('Troca de trabalho do modo admin ja aplicada.')
    else:
        raise SystemExit('Bloqueio de trabalho atual nao encontrado.')
else:
    s=s.replace(old,new,1)
    p.write_text(s,encoding='utf-8')
    print('Modo admin: @biduzao pode substituir o trabalho atual por outro do catalogo.')
