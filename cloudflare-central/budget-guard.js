export function budgetState(env, at = Date.now()) {
  let snapshot;
  try { snapshot = JSON.parse(env.GAT_D1_BUDGET || 'null'); } catch (_) {}
  const date = new Date(at).toISOString().slice(0, 10);
  const reset = Date.parse(date + 'T00:00:00Z') + 86400000;
  const usageKnown = snapshot && snapshot.date_utc === date &&
    typeof snapshot.rows_read === 'number' && snapshot.rows_read >= 0 && Number.isFinite(snapshot.rows_read) &&
    typeof snapshot.rows_written === 'number' && snapshot.rows_written >= 0 && Number.isFinite(snapshot.rows_written);
  // Reserve 20% for delayed analytics, in-flight requests and index writes.
  const nearLimit = usageKnown && (snapshot.rows_read >= 4000000 || snapshot.rows_written >= 80000);
  const checked = Date.parse(snapshot?.checked_at);
  const fresh = usageKnown && Number.isFinite(checked) && checked <= at && at - checked < 20 * 60000;
  if (nearLimit) return {paused:true,reason:'daily_budget',resumes_at:new Date(reset).toISOString(),message:'Central GAT em pausa para preservar o limite gratuito. As viagens durante a pausa não pontuam. O serviço volta após a renovação da cota e a verificação automática do consumo.'};
  if (!fresh) return {paused:true,reason:'budget_check_pending',resumes_at:null,message:'Central GAT aguardando a verificação automática do limite gratuito. As viagens durante esta pausa não pontuam.'};
  return {paused:false,reason:null,resumes_at:null};
}
