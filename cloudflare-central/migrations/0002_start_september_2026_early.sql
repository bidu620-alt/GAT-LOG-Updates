-- Inicia antecipadamente a temporada 2026-09.
-- Preserva histórico, entregas, XP, km totais e demais dados acumulados.

UPDATE meta SET value='2026-09' WHERE key='season';
INSERT OR IGNORE INTO meta(key,value) VALUES('season','2026-09');

UPDATE profiles
SET monthly_completed=0,
    current_mission_json=NULL,
    updated_at=strftime('%Y-%m-%dT%H:%M:%fZ','now');
