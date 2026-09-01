-- Match the predicates/order actually used by frequent API requests.
CREATE INDEX IF NOT EXISTS idx_sessions_expiry ON sessions(expires_at);
CREATE INDEX IF NOT EXISTS idx_auth_attempts_at ON auth_attempts(at);
CREATE INDEX IF NOT EXISTS idx_telemetry_account_updated ON telemetry_live(account_user, updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_deliveries_user_id ON deliveries(user, id DESC);
CREATE INDEX IF NOT EXISTS idx_deliveries_month_user ON deliveries(substr(delivered_at,1,7), user);
CREATE INDEX IF NOT EXISTS idx_client_tokens_device ON client_tokens(device_id);
CREATE INDEX IF NOT EXISTS idx_work_completed_user_month ON work_completed(user, month_key);
