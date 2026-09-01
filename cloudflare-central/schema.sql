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
