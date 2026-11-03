// Recovery candidate verification for GAT Journal V3.
// This module does not modify the DB or award Gold.
// Call only AFTER independently validating each packet's payload hash and HMAC.
export function assessJournalRecovery(previous, current, lastAcceptedSeq) {
  if (!previous?.payloadVerified || !previous?.macVerified ||
      !current?.payloadVerified || !current?.macVerified) {
    return {eligible:false, reason:'unauthenticated_packet'};
  }
  if (!Number.isSafeInteger(previous.seq) || !Number.isSafeInteger(current.seq) ||
      !Number.isSafeInteger(lastAcceptedSeq) ||
      previous.seq <= lastAcceptedSeq || current.seq !== previous.seq + 1) {
    return {eligible:false, reason:'invalid_sequence'};
  }
  if (!previous.packetId || !current.packetId || previous.packetId === current.packetId ||
      !previous.deviceId || previous.deviceId !== current.deviceId ||
      !previous.user || previous.user !== current.user) {
    return {eligible:false, reason:'identity_mismatch'};
  }
  if (!/^[a-f0-9]{64}$/i.test(previous.chain || '') ||
      String(current.prev || '').toLowerCase() !== previous.chain.toLowerCase()) {
    return {eligible:false, reason:'broken_link'};
  }
  const a = Date.parse(previous.collectedAt || '');
  const b = Date.parse(current.collectedAt || '');
  if (!Number.isFinite(a) || !Number.isFinite(b) || b <= a ||
      b - a > 20000 || Math.abs(Date.now() - b) > 30000) {
    return {eligible:false, reason:'stale_or_gapped_samples'};
  }
  return {eligible:true, reason:'candidate_only', seq:current.seq, chain:current.chain};
}
