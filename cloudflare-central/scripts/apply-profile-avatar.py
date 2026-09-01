from pathlib import Path

p = Path('worker.js')
s = p.read_text(encoding='utf-8')

old_profile = "safety_score:p.safety_score,current_mission:mission,deliveries}"
new_profile = "safety_score:p.safety_score,avatar_url:p.avatar_url||'',current_mission:mission,deliveries}"
if 'avatar_url:p.avatar_url' not in s:
    if old_profile not in s:
        raise SystemExit('profile return anchor not found')
    s = s.replace(old_profile, new_profile, 1)

anchor = " if(p==='/api/site/profile'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);return json(req,{ok:true,profile:await profile(env,s.user)})}\n"
endpoint = " if(p==='/api/site/profile/avatar'&&m==='POST'){const b=await body(req),s=await requireSession(req,env,b);await ensureProfile(env,s.user);const avatar=String(b.avatar_url||'').trim();if(avatar){if(avatar.length>220000)throw new HttpError(413,'avatar_too_large');if(!/^data:image\\/(?:webp|png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(avatar))throw new HttpError(400,'invalid_avatar')}await env.DB.prepare('UPDATE profiles SET avatar_url=?,updated_at=? WHERE user=?').bind(avatar||null,now(),s.user).run();await audit(env,s.user,avatar?'profile_avatar_update':'profile_avatar_remove',s.user,{bytes:avatar.length});return json(req,{ok:true,avatar_url:avatar})}\n"
if "/api/site/profile/avatar" not in s:
    if anchor not in s:
        raise SystemExit('site profile route anchor not found')
    s = s.replace(anchor, anchor + endpoint, 1)

p.write_text(s, encoding='utf-8')
print('profile avatar patch applied')
