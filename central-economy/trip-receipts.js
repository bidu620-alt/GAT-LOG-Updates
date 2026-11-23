// The telemetry pipeline remains the sole authority for deliveries/XP/Gold.
// Receipt retries may acknowledge an existing delivery, never award it twice.
export function acknowledgeTripReceipt(db,user,body){
 const trip=String(body.trip_id||'').trim();
 if(!/^[A-Za-z0-9._:-]{8,160}$/.test(trip))return {status:400,body:{ok:false,error:'invalid_trip_id'}};
 const candidates=new Set([trip]);
 if(db.sql.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name='open_trip_aliases'").get()){
  const alias=db.sql.prepare('SELECT trip_id FROM open_trip_aliases WHERE user=? AND observed_trip_id=?').get(user,trip);
  if(alias?.trip_id)candidates.add(String(alias.trip_id));
 }
 for(const candidate of candidates){
  const rows=db.sql.prepare('SELECT id,xp,raw_json FROM deliveries WHERE user=? AND instr(raw_json,?)>0 ORDER BY id DESC').all(user,candidate);
  for(const row of rows){let raw;try{raw=JSON.parse(row.raw_json)}catch{continue}
   const m=raw.mission||{};
   if(![m.trip_id,m.canonical_trip_id,m.job_latch_key,m.id,raw.trip_id,raw.receipt?.trip_id].some(id=>String(id||'')===candidate))continue;
   return {status:200,body:{ok:true,already_counted:true,completed_now:false,delivery_id:Number(row.id),xp_awarded:Number(row.xp)||0,penalty_xp:Number(raw.audit?.speed_penalty_xp||0)+Number(raw.audit?.cargo_penalty_xp||0)+Number(raw.audit?.truck_penalty_xp||0)}};
  }
 }
 // Do not acknowledge an unobserved receipt: keep the client's durable queue.
 return {status:409,body:{ok:false,error:'receipt_not_observed',message:'A entrega ainda nao foi confirmada pela telemetria da Central. Recibo preservado na fila do cliente.'}};
}
