PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS accounts (
  user TEXT PRIMARY KEY,
  password_salt TEXT,
  password_hash TEXT,
  role TEXT NOT NULL DEFAULT 'driver',
  disabled INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sessions (
  token_hash TEXT PRIMARY KEY,
  user TEXT NOT NULL,
  created_at TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user);

CREATE TABLE IF NOT EXISTS client_tokens (
  token_hash TEXT PRIMARY KEY,
  driver TEXT NOT NULL,
  account_user TEXT,
  device_id TEXT,
  created_at TEXT NOT NULL,
  last_seen_at TEXT NOT NULL,
  revoked_at TEXT,
  FOREIGN KEY(account_user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_client_tokens_driver ON client_tokens(driver);
CREATE INDEX IF NOT EXISTS idx_client_tokens_account ON client_tokens(account_user);

CREATE TABLE IF NOT EXISTS client_pairings (
  code_hash TEXT PRIMARY KEY,
  code_plain TEXT NOT NULL,
  driver TEXT NOT NULL,
  device_id TEXT NOT NULL,
  created_at TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  claimed_user TEXT,
  UNIQUE(driver, device_id),
  FOREIGN KEY(claimed_user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_client_pairings_expiry ON client_pairings(expires_at);

CREATE TABLE IF NOT EXISTS auth_attempts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  at TEXT NOT NULL,
  attempt_key TEXT NOT NULL,
  succeeded INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_auth_attempts_key_at ON auth_attempts(attempt_key, at);

CREATE TABLE IF NOT EXISTS telemetry_live (
  driver TEXT PRIMARY KEY,
  account_user TEXT,
  device_id TEXT,
  updated_at TEXT NOT NULL,
  telemetry_json TEXT NOT NULL,
  FOREIGN KEY(account_user) REFERENCES accounts(user) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_telemetry_updated ON telemetry_live(updated_at);

CREATE TABLE IF NOT EXISTS profiles (
  user TEXT PRIMARY KEY,
  monthly_completed INTEGER NOT NULL DEFAULT 0,
  monthly_goal INTEGER NOT NULL DEFAULT 30,
  total_deliveries INTEGER NOT NULL DEFAULT 0,
  total_km REAL NOT NULL DEFAULT 0,
  xp INTEGER NOT NULL DEFAULT 0,
  points INTEGER NOT NULL DEFAULT 0,
  perfect_trips INTEGER NOT NULL DEFAULT 0,
  penalty_xp INTEGER NOT NULL DEFAULT 0,
  speed_fines INTEGER NOT NULL DEFAULT 0,
  safety_score REAL NOT NULL DEFAULT 100,
  avatar_url TEXT,
  current_mission_json TEXT,
  updated_at TEXT NOT NULL,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS deliveries (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user TEXT NOT NULL,
  sequence_no INTEGER,
  source TEXT,
  destination TEXT,
  cargo TEXT,
  weight_kg REAL,
  distance_km REAL,
  xp INTEGER NOT NULL DEFAULT 0,
  perfect INTEGER NOT NULL DEFAULT 0,
  penalty_xp INTEGER NOT NULL DEFAULT 0,
  speed_fines INTEGER NOT NULL DEFAULT 0,
  delivered_at TEXT NOT NULL,
  raw_json TEXT,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_deliveries_user_date ON deliveries(user, delivered_at);

CREATE TABLE IF NOT EXISTS work_catalog (
  id TEXT PRIMARY KEY,
  position INTEGER NOT NULL,
  title TEXT NOT NULL,
  category TEXT,
  icon TEXT,
  custom INTEGER NOT NULL DEFAULT 0,
  active INTEGER NOT NULL DEFAULT 1,
  compatible_cargos_json TEXT NOT NULL DEFAULT '[]'
);

CREATE TABLE IF NOT EXISTS cargo_aliases (
  cargo_key TEXT PRIMARY KEY,
  cargo_name TEXT NOT NULL,
  work_id TEXT NOT NULL,
  confidence REAL NOT NULL DEFAULT 1,
  source TEXT NOT NULL DEFAULT 'manual',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  FOREIGN KEY(work_id) REFERENCES work_catalog(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_cargo_aliases_work ON cargo_aliases(work_id);

CREATE TABLE IF NOT EXISTS cargo_classification_queue (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  delivery_id INTEGER NOT NULL UNIQUE,
  user TEXT NOT NULL,
  cargo TEXT NOT NULL,
  cargo_key TEXT NOT NULL,
  source TEXT,
  destination TEXT,
  weight_kg REAL,
  distance_km REAL,
  delivered_at TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending',
  suggested_work_id TEXT,
  suggested_confidence REAL NOT NULL DEFAULT 0,
  classified_work_id TEXT,
  classified_by TEXT,
  classified_at TEXT,
  FOREIGN KEY(delivery_id) REFERENCES deliveries(id) ON DELETE CASCADE,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE,
  FOREIGN KEY(suggested_work_id) REFERENCES work_catalog(id) ON DELETE SET NULL,
  FOREIGN KEY(classified_work_id) REFERENCES work_catalog(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_cargo_queue_status ON cargo_classification_queue(status, delivered_at);
CREATE INDEX IF NOT EXISTS idx_cargo_queue_user ON cargo_classification_queue(user, delivered_at);

CREATE TABLE IF NOT EXISTS work_completed (
  user TEXT NOT NULL,
  work_id TEXT NOT NULL,
  month_key TEXT NOT NULL,
  completed_at TEXT NOT NULL,
  PRIMARY KEY(user, work_id, month_key),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE,
  FOREIGN KEY(work_id) REFERENCES work_catalog(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS audit (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  at TEXT NOT NULL,
  actor TEXT NOT NULL,
  action TEXT NOT NULL,
  target TEXT NOT NULL,
  details TEXT
);

CREATE TABLE IF NOT EXISTS routes_completed (
  user TEXT NOT NULL,
  month_key TEXT NOT NULL,
  route_key TEXT NOT NULL,
  source TEXT NOT NULL,
  destination TEXT NOT NULL,
  completed_at TEXT NOT NULL,
  PRIMARY KEY(user, month_key, route_key),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS mission_completions (
  mission_id TEXT PRIMARY KEY,
  user TEXT NOT NULL,
  completed_at TEXT NOT NULL,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS admin_backups (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  created_at TEXT NOT NULL,
  actor TEXT NOT NULL,
  snapshot_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
INSERT OR IGNORE INTO meta(key,value) VALUES ('operation_mode','official');
INSERT OR IGNORE INTO meta(key,value) VALUES ('season','2026-09');


-- GAT 1.0.51: viagens abertas sobrevivem a app/jogo/PC offline e troca de jogo/mapa.
CREATE TABLE IF NOT EXISTS open_trips (
  trip_id TEXT PRIMARY KEY,
  user TEXT NOT NULL,
  game_name TEXT NOT NULL DEFAULT '',
  context_key TEXT NOT NULL DEFAULT '',
  fingerprint TEXT NOT NULL,
  state TEXT NOT NULL DEFAULT 'suspended',
  mission_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  last_seen_at TEXT,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_open_trips_user_state ON open_trips(user,state,updated_at);
CREATE INDEX IF NOT EXISTS idx_open_trips_match ON open_trips(user,context_key,fingerprint,updated_at);

CREATE TABLE IF NOT EXISTS open_trip_aliases (
  observed_trip_id TEXT PRIMARY KEY,
  trip_id TEXT NOT NULL,
  user TEXT NOT NULL,
  first_seen_at TEXT NOT NULL,
  last_seen_at TEXT NOT NULL,
  FOREIGN KEY(trip_id) REFERENCES open_trips(trip_id) ON DELETE CASCADE,
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_open_trip_alias_user ON open_trip_aliases(user,last_seen_at);


-- GAT 1.0.54: recibos idempotentes, cadeia local e caixa-preta recuperavel.
CREATE TABLE IF NOT EXISTS telemetry_packet_receipts (
  user TEXT NOT NULL,
  packet_id TEXT NOT NULL,
  device_id TEXT NOT NULL DEFAULT '',
  trip_id TEXT NOT NULL DEFAULT '',
  collected_at TEXT,
  received_at TEXT NOT NULL,
  journal_seq INTEGER,
  journal_chain TEXT,
  journal_verified INTEGER NOT NULL DEFAULT 0,
  journal_reason TEXT,
  mission_event_json TEXT,
  PRIMARY KEY(user,packet_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_packet_receipts_trip ON telemetry_packet_receipts(user,trip_id,received_at);

CREATE TABLE IF NOT EXISTS telemetry_journal_state (
  user TEXT NOT NULL,
  device_id TEXT NOT NULL,
  last_seq INTEGER NOT NULL,
  last_chain TEXT NOT NULL,
  last_packet_id TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY(user,device_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS trip_checkpoints (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user TEXT NOT NULL,
  trip_id TEXT NOT NULL,
  packet_id TEXT NOT NULL,
  phase TEXT NOT NULL,
  collected_at TEXT,
  received_at TEXT NOT NULL,
  journal_verified INTEGER NOT NULL DEFAULT 0,
  summary_json TEXT NOT NULL,
  raw_json TEXT NOT NULL,
  UNIQUE(user,packet_id),
  FOREIGN KEY(user) REFERENCES accounts(user) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_trip_checkpoints_trip ON trip_checkpoints(user,trip_id,received_at);


-- GAT_RADIO_V159
CREATE TABLE IF NOT EXISTS radio_state (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  enabled INTEGER NOT NULL DEFAULT 0,
  playlist_url TEXT NOT NULL DEFAULT '',
  playlist_id TEXT NOT NULL DEFAULT '',
  label TEXT NOT NULL DEFAULT 'Radio GAT',
  revision INTEGER NOT NULL DEFAULT 0,
  updated_by TEXT NOT NULL DEFAULT '',
  updated_at TEXT NOT NULL DEFAULT ''
);
INSERT OR IGNORE INTO radio_state(id,enabled,playlist_url,playlist_id,label,revision,updated_by,updated_at)
VALUES(1,0,'','','Radio GAT',0,'',datetime('now'));
