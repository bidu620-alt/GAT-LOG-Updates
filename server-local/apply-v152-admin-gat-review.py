from pathlib import Path
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('server-local/runtime')
worker_path=root/'worker.js'
worker=worker_path.read_text(encoding='utf-8')

def once(text,old,new,label):
    if old not in text:
        raise SystemExit('Nao encontrei '+label)
    return text.replace(old,new,1)

# 1.0.52 - revisao humana dos Pontos GAT sem alterar XP/historico.
worker=once(worker,"const VERSION='1.0.51-local';","const VERSION='1.0.52-local';",'versao 1.0.51')

review_helpers=r'''
function gatReviewSuggestion(a={}){
  const own=k=>Object.prototype.hasOwnProperty.call(a||{},k)&&Number.isFinite(Number(a?.[k]));
  const available=own('gat_base_points')&&own('gat_speed_penalty_points')&&own('gat_cargo_penalty_points')&&own('gat_truck_penalty_points');
  if(!available)return{available:false,points:null,base:null,speed:null,cargo:null,truck:null};
  const base=Math.max(0,Math.min(100,Math.round(Number(a.gat_base_points)))),speed=Math.max(0,Math.round(Number(a.gat_speed_penalty_points))),cargo=Math.max(0,Math.round(Number(a.gat_cargo_penalty_points))),truck=Math.max(0,Math.round(Number(a.gat_truck_penalty_points)));
  return{available:true,points:Math.max(0,Math.min(100,base-speed-cargo-truck)),base,speed,cargo,truck};
}
function gatReviewView(a={}){
  const suggestion=gatReviewSuggestion(a),manual=a?.gat_manual_review&&typeof a.gat_manual_review==='object'?a.gat_manual_review:null,current=Number.isFinite(Number(a?.gat_points))?Number(a.gat_points):0,reason=String(a?.ranking_reason||a?.automatic_ranking_reason||'');
  return{gat_review_suggested_points:suggestion.points,gat_review_suggestion_available:suggestion.available,gat_reviewable:current===0&&!!reason&&suggestion.available&&!manual,gat_review_status:manual?.status||null,gat_review_actor:manual?.actor||null,gat_review_at:manual?.at||null,gat_review_previous_points:manual?.previous_points??null,gat_review_approved_points:manual?.approved_points??null};
}
'''
worker=once(worker,'async function adminDriver(env,target){',review_helpers+'\nasync function adminDriver(env,target){','helpers antes de adminDriver')

# Somente o painel administrativo precisa dos campos auxiliares de revisao.
admin_tail="}}return{account:{...a,disabled:!!a.disabled,active_sessions:Number(sessions?.total||0)},profile:p,live:liveData}}"
admin_tail_new="}}p.deliveries=(Array.isArray(p?.deliveries)?p.deliveries:[]).map(x=>({...x,...gatReviewView(x)}));return{account:{...a,disabled:!!a.disabled,active_sessions:Number(sessions?.total||0)},profile:p,live:liveData}}"
worker=once(worker,admin_tail,admin_tail_new,'decoracao do historico no adminDriver')

# Moderador continua sem poder alterar conta/XP/progresso, mas pode revisar Pontos GAT.
worker=once(worker,"if(actor.role==='moderator'&&action!=='reset_mission')throw new HttpError(403,'forbidden');","if(actor.role==='moderator'&&!['reset_mission','review_gat_points'].includes(action))throw new HttpError(403,'forbidden');",'permissao do moderador')

review_action=r'''if(action==='review_gat_points'){
   const id=Math.trunc(Number(b.delivery_id)),decision=String(b.review_decision||'');
   if(!Number.isFinite(id)||id<=0)throw new HttpError(400,'invalid_delivery_id');
   if(!['approve','keep_zero'].includes(decision))throw new HttpError(400,'invalid_review_decision');
   const d=await env.DB.prepare('SELECT id,raw_json FROM deliveries WHERE id=? AND user=?').bind(id,target).first();if(!d)throw new HttpError(404,'delivery_not_found');
   let raw={};try{raw=JSON.parse(d.raw_json||'{}')}catch{}if(!raw||typeof raw!=='object')raw={};
   const aData=raw.audit&&typeof raw.audit==='object'?{...raw.audit}:{},existing=aData.gat_manual_review&&typeof aData.gat_manual_review==='object'?aData.gat_manual_review:null;
   if(existing)throw new HttpError(409,'gat_review_already_done');
   const current=Number.isFinite(Number(aData.gat_points))?Math.max(0,Math.round(Number(aData.gat_points))):0,automaticReason=String(aData.ranking_reason||aData.automatic_ranking_reason||'');
   if(current!==0)throw new HttpError(409,'gat_review_requires_zero_points');
   if(!automaticReason)throw new HttpError(409,'gat_review_requires_validation_error');
   const suggestion=gatReviewSuggestion(aData);if(!suggestion.available)throw new HttpError(409,'gat_review_no_saved_breakdown');
   const approved=decision==='approve'?suggestion.points:0,reviewAt=now(),manual={status:decision==='approve'?'approved':'kept_zero',actor:actor.user,at:reviewAt,previous_points:current,suggested_points:suggestion.points,approved_points:approved,automatic_reason:automaticReason};
   aData.automatic_ranking_reason=aData.automatic_ranking_reason||automaticReason;
   aData.automatic_ranking_message=aData.automatic_ranking_message||String(aData.ranking_message||'');
   aData.gat_manual_review=manual;aData.gat_points_manual=decision==='approve';aData.gat_points=approved;
   if(decision==='approve'){
     aData.ranking_eligible=true;aData.rank_eligible=true;aData.ranking_reason=null;aData.ranking_message='Pontuacao GAT validada manualmente por @'+actor.user+'.';
   }
   raw.audit=aData;
   const delta=approved-current;
   await env.DB.batch([env.DB.prepare('UPDATE deliveries SET raw_json=? WHERE id=? AND user=?').bind(JSON.stringify(raw),id,target),env.DB.prepare('UPDATE profiles SET points=MAX(0,points+?),updated_at=? WHERE user=?').bind(delta,reviewAt,target)]);
   await audit(env,actor.user,'review_gat_points',target,{delivery_id:id,decision,previous_points:current,suggested_points:suggestion.points,approved_points:approved,points_delta:delta,automatic_reason:automaticReason});
   invalidateRead('profile:'+target);invalidateRead('ranking');
   return json(req,{ok:true,action,target,delivery_id:id,decision,previous_points:current,suggested_points:suggestion.points,approved_points:approved,points_delta:delta});
 }
 '''
worker=once(worker,"if(action==='reset_mission')",review_action+"if(action==='reset_mission')",'inicio das acoes administrativas')

required=[
    "const VERSION='1.0.52-local'",
    'function gatReviewSuggestion',
    'gat_review_suggested_points',
    "'review_gat_points'",
    'gat_review_already_done',
    'gat_review_no_saved_breakdown',
    'gat_manual_review',
    'automatic_ranking_reason',
    "points=MAX(0,points+?)",
    "await audit(env,actor.user,'review_gat_points'",
    "invalidateRead('profile:'+target)",
    'p.deliveries=(Array.isArray(p?.deliveries)',
]
for marker in required:
    if marker not in worker:raise SystemExit('Patch v1.52 incompleto: '+marker)
if "if(actor.role==='moderator'&&action!=='reset_mission')" in worker:raise SystemExit('Moderador ainda bloqueado na revisao GAT')
if "aData.rank_verified=true" in worker or "aData.rank_verified = true" in worker:raise SystemExit('Revisao manual nao pode fingir verificacao automatica')

worker_path.write_text(worker,encoding='utf-8')
print('GAT Server 1.0.52: revisao manual de Pontos GAT pronta para owner/admin/moderator.')
