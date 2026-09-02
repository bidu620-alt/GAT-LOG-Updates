export const MIN_RANK_CLIENT = '1.0.28';
export const MAX_TELEMETRY_GAP_MS = 120000;
export const DAMAGE_FIELDS = {
  cargo: 'cargo_damage_pct', engine: 'truck_engine_damage_pct',
  transmission: 'truck_transmission_damage_pct', cabin: 'truck_cabin_damage_pct',
  chassis: 'truck_chassis_damage_pct', wheels: 'truck_wheels_damage_pct',
  trailer: 'trailer_damage_pct'
};
export function validDamage(value) {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 100;
}
export function versionAtLeast(value, minimum = MIN_RANK_CLIENT) {
  if (typeof value !== 'string' || !/^\d+\.\d+\.\d+(?:\.\d+)?$/.test(value)) return false;
  const a = value.split('.').map(Number), b = minimum.split('.').map(Number);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    if ((a[i] || 0) !== (b[i] || 0)) return (a[i] || 0) > (b[i] || 0);
  }
  return true;
}
export function rankingReadiness(raw = {}) {
  const missing = Object.entries(DAMAGE_FIELDS).filter(([, key]) => !validDamage(raw[key])).map(([name]) => name);
  const versionOK = versionAtLeast(raw.gat_client_version);
  const connected = raw.game?.connected === true;
  const reason = !versionOK ? 'client_update_required' : !connected ? 'telemetry_disconnected' : missing.length ? 'damage_data_incomplete' : null;
  return {eligible: !reason, reason, missing_damage: missing, client_version: String(raw.gat_client_version || ''), minimum_version: MIN_RANK_CLIENT};
}
export function rankingMessage(reason) {
  if (reason === 'client_update_required') return 'Ranking bloqueado: atualize o GAT Telemetria para 1.0.28 ou superior.';
  if (reason === 'damage_data_incomplete') return 'Ranking bloqueado: faltam dados de danos. Atualize o TruckSim GPS com o pacote GAT de danos e reinicie o jogo.';
  if (reason === 'telemetry_disconnected') return 'Ranking bloqueado: mantenha o jogo conectado ao GAT Telemetria.';
  if (reason === 'telemetry_gap') return 'Esta viagem não pontua: houve interrupção prolongada da telemetria. Inicie outra viagem com a telemetria ativa.';
  if (reason === 'telemetry_not_verified_from_start') return 'Esta viagem ainda está aguardando confirmação contínua da telemetria.';
  return reason ? 'Esta viagem não pontua. Corrija a telemetria e inicie outra viagem.' : '';
}
// A viagem e armada com a primeira leitura valida e confirmada pela segunda leitura
// continua. O relogio da propria missao evita depender do pacote idle anterior, que
// podia deixar uma viagem automatica presa em telemetry_not_verified_from_start.
// Depois de verificada, falhas reais de telemetria continuam sticky.
export function advanceRankGuard(guard, readiness, previousAt, at, legacyProgressConfirmed = false) {
  if (!guard && legacyProgressConfirmed && readiness.eligible) {
    return {reason: null, verified_at: at, last_sample_at: at, valid_samples: 2, migrated_after_server_update: true};
  }
  const next = guard ? {...guard} : {reason: 'telemetry_not_verified_from_start', valid_samples: 0};
  const currentTime = Date.parse(at || '');
  const missionPrevious = Date.parse(next.last_sample_at || '');
  const fallbackPrevious = Date.parse(previousAt || '');
  const previousTime = Number.isFinite(missionPrevious) ? missionPrevious : fallbackPrevious;
  const continuous = Number.isFinite(previousTime) && Number.isFinite(currentTime) &&
    currentTime >= previousTime && currentTime - previousTime <= MAX_TELEMETRY_GAP_MS;

  // A ativacao antiga podia criar {reason:null} sem marcar que a primeira amostra
  // ainda precisava de confirmacao. Transforme-a explicitamente na amostra 1.
  if (!next.verified_at && next.reason === null && !Number.isFinite(Number(next.valid_samples))) {
    if (!readiness.eligible) return {...next, reason: readiness.reason, last_sample_at: at, valid_samples: 0};
    return {...next, reason: 'telemetry_not_verified_from_start', first_sample_at: at, last_sample_at: at, valid_samples: 1};
  }

  if (next.reason === 'telemetry_not_verified_from_start') {
    if (!readiness.eligible) {
      next.last_sample_at = at;
      next.valid_samples = 0;
      return next;
    }
    const prior = Math.max(0, Number(next.valid_samples) || 0);
    next.valid_samples = continuous ? Math.max(1, prior) + 1 : 1;
    next.last_sample_at = at;
    if (!next.first_sample_at) next.first_sample_at = at;
    if (next.valid_samples >= 2) {
      next.reason = null;
      next.verified_at = at;
    }
    return next;
  }

  if (!next.reason) {
    if (!readiness.eligible) next.reason = readiness.reason;
    else if (!continuous) next.reason = 'telemetry_gap';
    next.last_sample_at = at;
  }
  return next;
}
// TruckSim can detach the trailer in the delivery packet. Reuse only its immediately
// preceding verified trailer reading, and require the delivery's final cargo damage.
export function restoreDeliveredTrailer(raw, previous, previousAt, at, delivered, loaded) {
  if (!delivered || loaded || !previous || !rankingReadiness(previous).eligible ||
      !Number.isFinite(Date.parse(previousAt)) || Date.parse(at) - Date.parse(previousAt) > 30000) return;
  const details = raw.gameplay?.jobDeliveredDetails || raw.jobDeliveredDetails;
  if (!validDamage(details?.cargoDamage)) return;
  if (!validDamage(raw.cargo_damage_pct)) raw.cargo_damage_pct = details.cargoDamage <= 1.0001 ? details.cargoDamage * 100 : details.cargoDamage;
  if (!validDamage(raw.trailer_damage_pct)) raw.trailer_damage_pct = previous.trailer_damage_pct;
}
