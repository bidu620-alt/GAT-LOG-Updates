/* Gold por entrega: somente créditos comprovados pela carteira autenticada.
   Não interfere em XP, Pontos BiduLog, danos ou no cálculo da Central. */
(() => {
  'use strict';
  const API = 'https://api.bidulog.com.br';
  const SESSION = 'gat_driver_account_v1';
  const reasonLabel = {
    delivery_before_launch: 'Entrega anterior à ativação do Gold',
    delivery_not_eligible: 'Entrega sem elegibilidade',
    insufficient_brand_evidence: 'Caminhão não confirmado durante a viagem',
    brand_not_owned: 'Marca não adquirida',
    brand_not_owned_at_start: 'Marca não adquirida no início',
    brand_not_selected: 'Marca não selecionada',
    journal_not_verified: 'Diário de telemetria não validado',
    truck_changed: 'Troca de marca durante a viagem',
    telemetry_gap_review: 'Interrupção de telemetria',
    model_missing_while_driving: 'Marca não identificada na viagem',
    journey_not_observed_from_start: 'Viagem não acompanhada desde o início'
  };
  const number = value => Number(value);
  const clean = value => String(value || '').replace(/^@/, '').trim().toLowerCase();
  const goldFormat = units => (number(units) / 100).toLocaleString('pt-BR', {
    minimumFractionDigits: 0, maximumFractionDigits: 2
  });
  const deliveryId = value => {
    const n = number(value);
    return Number.isSafeInteger(n) && n > 0 ? String(n) : '';
  };
  function session() {
    try {
      return JSON.parse(localStorage.getItem(SESSION) || sessionStorage.getItem(SESSION) || 'null');
    } catch (_) { return null; }
  }
  function signedInOwner(profile) {
    const s = session();
    return s && s.token && clean(s.user) === clean(profile && profile.user) ? s : null;
  }

  let currentProfile = null;
  let audit = { key: '', ready: false, loading: false, failed: false, rewards: new Map() };
  let requestNumber = 0;
  let lastQueryAt = 0;
  function parseRecords(data) {
    const records = new Map();
    // O extrato é a prova do crédito. A API pode devolver apenas os 100 últimos itens.
    for (const entry of Array.isArray(data.ledger) ? data.ledger : []) {
      if (entry.kind !== 'delivery_reward' || entry.currency !== 'gold') continue;
      const found = /^delivery:(\d+)$/.exec(String(entry.reference || ''));
      const id = found && deliveryId(found[1]);
      const amount = number(entry.delta_units);
      if (id && Number.isSafeInteger(amount) && amount > 0) {
        records.set(id, { gold_units: amount, reason: 'validated', confirmed: true });
      }
    }
    // eco_rewards é o histórico autoritativo: inclui as viagens sem crédito (valor zero).
    if (Array.isArray(data.delivery_rewards)) {
      for (const item of data.delivery_rewards) {
        const id = deliveryId(item.delivery_id);
        const amount = number(item.gold_units);
        if (!id || !Number.isSafeInteger(amount) || amount < 0) continue;
        const credited = records.get(id);
        records.set(id, {
          gold_units: credited ? credited.gold_units : amount,
          reason: String(item.reason || ''),
          confirmed: !!credited || amount > 0,
          hasRewardRecord: true
        });
      }
    }
    return records;
  }

  function status(id) {
    if (!id) return { value: '—', note: 'ID da entrega indisponível', kind: 'unknown' };
    if (audit.loading && !audit.ready) return { value: '—', note: 'Verificando carteira', kind: 'pending' };
    if (audit.failed && !audit.ready) return { value: '—', note: 'Carteira indisponível', kind: 'unknown' };
    if (!audit.ready) return { value: '—', note: 'Aguardando consulta', kind: 'pending' };
    const reward = audit.rewards.get(id);
    if (!reward) {
      return { value: '—', note: 'Sem registro confirmado no histórico consultado', kind: 'unknown' };
    }
    if (reward.gold_units > 0 && reward.confirmed) {
      return { value: '+' + goldFormat(reward.gold_units) + ' Gold', note: 'Creditado na carteira', kind: 'credited' };
    }
    const reason = reasonLabel[reward.reason] || (reward.reason === 'validated'
      ? 'Entrega validada, sem valor para creditar' : 'Entrega sem Gold');
    return { value: '0 Gold', note: 'Não gerou • ' + reason, kind: 'rejected' };
  }

  function draw() {
    const profile = currentProfile;
    if (!profile) return;
    const own = signedInOwner(profile);
    const deliveries = Array.isArray(profile.deliveries) ? [...profile.deliveries].reverse() : [];
    const rows = document.querySelectorAll('#deliveryRows .gat-delivery-row');
    if (rows.length !== deliveries.length) return;
    rows.forEach((row, index) => {
      const detail = row.querySelector('.gat-xp-breakdown');
      if (!detail) return;
      let chip = detail.querySelector('[data-delivery-gold]');
      if (!own) { if (chip) chip.remove(); return; }
      if (!chip) {
        chip = document.createElement('span');
        chip.setAttribute('data-delivery-gold', '');
        detail.appendChild(chip);
      }
      const result = status(deliveryId(deliveries[index] && deliveries[index].id));
      chip.className = 'gat-xp-chip delivery-gold-' + result.kind;
      chip.title = result.note;
      chip.textContent = '';
      const label = document.createElement('span');
      label.textContent = 'GOLD • ' + result.note;
      const value = document.createElement('b');
      value.textContent = result.value;
      chip.append(label, value);
    });
  }

  async function refresh(force = false) {
    const p = currentProfile;
    const s = signedInOwner(p);
    if (!s) {
      requestNumber++;
      audit = { key: '', ready: false, loading: false, failed: false, rewards: new Map() };
      draw();
      return;
    }
    const key = clean(s.user) + '|' + s.token;
    if (key !== audit.key) {
      requestNumber++;
      audit = { key, ready: false, loading: false, failed: false, rewards: new Map() };
      lastQueryAt = 0;
    }
    draw();
    if (audit.loading || (!force && audit.ready && Date.now() - lastQueryAt < 30000)) return;
    const requestId = ++requestNumber;
    audit.loading = true;
    lastQueryAt = Date.now();
    draw();
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await fetch(API + '/api/economy/state', {
        method: 'POST', cache: 'no-store', signal: controller.signal,
        headers: { 'Content-Type': 'text/plain;charset=UTF-8' },
        body: JSON.stringify({ token: s.token })
      });
      const data = await response.json();
      if (!response.ok || !data || !data.ok || data.mode !== 'official') throw new Error('economy_unavailable');
      if (requestId !== requestNumber || !signedInOwner(currentProfile) || audit.key !== key) return;
      audit.rewards = parseRecords(data);
      audit.ready = true;
      audit.failed = false;
    } catch (_) {
      if (requestId !== requestNumber || audit.key !== key) return;
      audit.failed = !audit.ready;
    } finally {
      clearTimeout(timeout);
      if (requestId === requestNumber && audit.key === key) {
        audit.loading = false;
        draw();
      }
    }
  }

  const style = document.createElement('style');
  style.textContent =
    '.gat-xp-chip.delivery-gold-credited{border-color:#8c681d;background:#30230d;color:#f6cf72}' +
    '.gat-xp-chip.delivery-gold-credited b{color:#ffe096}' +
    '.gat-xp-chip.delivery-gold-rejected{border-color:#54343b;background:#23171d;color:#dca1ae}' +
    '.gat-xp-chip.delivery-gold-unknown,.gat-xp-chip.delivery-gold-pending{color:#96a2b1}' +
    '.gat-xp-chip[data-delivery-gold]{flex-wrap:wrap}';
  document.head.appendChild(style);

  window.addEventListener('gat-driver-profile-updated', event => {
    currentProfile = event.detail && event.detail.profile || null;
    refresh();
  });
  window.addEventListener('gat-account-change', () => {
    requestNumber++;
    audit = { key: '', ready: false, loading: false, failed: false, rewards: new Map() };
    lastQueryAt = 0;
    refresh(true);
  });
  if (window.GATCurrentDriverProfile) {
    currentProfile = window.GATCurrentDriverProfile;
    refresh();
  }
})();
