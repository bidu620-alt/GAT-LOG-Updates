export const MIN_RANK_CLIENT = '1.0.28';
export const MAX_TELEMETRY_GAP_MS = 120000;
export const RANK_STARTUP_GRACE_MS = 30000;
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
  if (reason === 'client_update_required') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: atualize o GAT Telemetria para 1.0.28 ou superior.';
  if (reason === 'damage_data_incomplete') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: faltaram dados de danos.';
  if (reason === 'telemetry_disconnected') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: a telemetria ficou desconectada.';
  if (reason === 'telemetry_gap') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: houve interrupção prolongada da telemetria.';
  if (reason === 'telemetry_not_verified_from_start') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: a telemetria não foi confirmada continuamente desde o início.';
  if (reason === 'trip_progress_unverified') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: o progresso contínuo da viagem não pôde ser confirmado.';
  if (reason === 'mission_not_active') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: o início da viagem não foi validado para pontuação.';
  if (reason === 'distance_below_minimum') return 'Viagem registrada e XP mantido, mas sem Pontos GAT: a distância não atendeu ao requisito de pontuação.';
  return reason ? 'Viagem registrada e XP mantido, mas esta entrega não recebeu Pontos GAT.' : '';
}
// O primeiro pacote de uma carga pode chegar enquanto o TruckSim GPS ainda atualiza
// os campos de dano. Durante uma janela curta de 30 s, a Central nao condena a viagem
// por esse pacote transitorio: exige duas leituras completas e continuas para selar
// o rank_guard. Depois de verified_at, qualquer falta real de dados ou gap fica sticky.
export function advanceRankGuard(guard, readiness, previousAt, at, legacyProgressConfirmed = false) {
  if (!guard && legacyProgressConfirmed && readiness.eligible) {
    return {reason: null, verified_at: at, last_sample_at: at, valid_samples: 2, migrated_after_server_update: true};
  }

  const next = guard ? {...guard} : {reason: 'telemetry_not_verified_from_start', valid_samples: 0, startup_started_at: at};
  const currentTime = Date.parse(at || '');
  const missionPrevious = Date.parse(next.last_sample_at || '');
  const fallbackPrevious = Date.parse(previousAt || '');
  const previousTime = Number.isFinite(missionPrevious) ? missionPrevious : fallbackPrevious;
  const continuous = Number.isFinite(previousTime) && Number.isFinite(currentTime) &&
    currentTime >= previousTime && currentTime - previousTime <= MAX_TELEMETRY_GAP_MS;

  // Migra uma missao iniciada na 1.0.44 que ficou presa com damage_data_incomplete
  // no primeiro pacote. A migracao so abre uma nova janela curta; ainda exige duas
  // amostras validas antes de liberar a viagem.
  if (!next.verified_at && !next.startup_started_at) {
    next.startup_started_at = at;
    next.valid_samples = 0;
    next.reason = 'telemetry_not_verified_from_start';
    next.migrated_startup_guard_v145 = true;
  }

  // A ativacao antiga podia criar {reason:null} sem registrar a amostra inicial.
  if (!next.verified_at && next.reason === null && !Number.isFinite(Number(next.valid_samples))) {
    next.reason = 'telemetry_not_verified_from_start';
    next.valid_samples = 0;
    if (!next.startup_started_at) next.startup_started_at = at;
  }

  if (!next.verified_at) {
    const startupTime = Date.parse(next.startup_started_at || at);
    const startupAge = Number.isFinite(startupTime) && Number.isFinite(currentTime) ? currentTime - startupTime : 0;
    if (startupAge > RANK_STARTUP_GRACE_MS) {
      next.reason = readiness.eligible ? (next.last_invalid_reason || 'telemetry_not_verified_from_start') : readiness.reason;
      next.startup_failed_at = at;
      next.last_sample_at = at;
      return next;
    }

    if (!readiness.eligible) {
      next.reason = 'telemetry_not_verified_from_start';
      next.last_invalid_reason = readiness.reason;
      next.valid_samples = 0;
      next.last_sample_at = at;
      return next;
    }

    const prior = Math.max(0, Number(next.valid_samples) || 0);
    next.valid_samples = prior > 0 && continuous ? prior + 1 : 1;
    next.reason = 'telemetry_not_verified_from_start';
    next.last_sample_at = at;
    if (!next.first_sample_at) next.first_sample_at = at;
    if (next.valid_samples >= 2) {
      next.reason = null;
      next.verified_at = at;
      delete next.last_invalid_reason;
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
