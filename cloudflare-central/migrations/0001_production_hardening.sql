ALTER TABLE client_tokens ADD COLUMN account_user TEXT REFERENCES accounts(user);
ALTER TABLE client_tokens ADD COLUMN revoked_at TEXT;
CREATE INDEX IF NOT EXISTS idx_client_tokens_account ON client_tokens(account_user);
UPDATE client_tokens
SET account_user = driver
WHERE account_user IS NULL
  AND EXISTS (SELECT 1 FROM accounts WHERE accounts.user = client_tokens.driver);

ALTER TABLE work_catalog ADD COLUMN compatible_cargos_json TEXT NOT NULL DEFAULT '[]';

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

UPDATE accounts SET role = 'owner', updated_at = CURRENT_TIMESTAMP WHERE user = 'biduzao';
