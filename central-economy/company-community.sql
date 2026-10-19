CREATE TABLE IF NOT EXISTS company_settings(
 company_id TEXT PRIMARY KEY REFERENCES eco_companies(id),slogan TEXT NOT NULL DEFAULT '',rules TEXT NOT NULL DEFAULT '',discord_url TEXT NOT NULL DEFAULT '',
 logo_id TEXT,cover_id TEXT,cover_position INTEGER NOT NULL DEFAULT 50,theme_json TEXT NOT NULL DEFAULT '{}',updated_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS company_media(id TEXT PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL,mime TEXT NOT NULL,content TEXT NOT NULL,created_at TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS company_media_company ON company_media(company_id,created_at);
CREATE TABLE IF NOT EXISTS company_posts(id TEXT PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL,text TEXT NOT NULL,image_id TEXT REFERENCES company_media(id),pinned INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL,deleted_at TEXT);
CREATE INDEX IF NOT EXISTS company_posts_feed ON company_posts(company_id,pinned DESC,created_at DESC);
CREATE TABLE IF NOT EXISTS company_comments(id TEXT PRIMARY KEY,post_id TEXT NOT NULL REFERENCES company_posts(id),user TEXT NOT NULL,text TEXT NOT NULL,created_at TEXT NOT NULL,deleted_at TEXT);
CREATE INDEX IF NOT EXISTS company_comments_post ON company_comments(post_id,created_at);
CREATE TABLE IF NOT EXISTS company_likes(post_id TEXT NOT NULL REFERENCES company_posts(id),user TEXT NOT NULL,created_at TEXT NOT NULL,PRIMARY KEY(post_id,user));
CREATE TABLE IF NOT EXISTS company_ops(user TEXT NOT NULL,operation_id TEXT NOT NULL,request_hash TEXT NOT NULL,result_json TEXT NOT NULL,PRIMARY KEY(user,operation_id));
CREATE TABLE IF NOT EXISTS company_tenures(id INTEGER PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL,joined_at TEXT NOT NULL,left_at TEXT);
CREATE INDEX IF NOT EXISTS company_tenures_user ON company_tenures(user,joined_at,left_at);
CREATE UNIQUE INDEX IF NOT EXISTS company_tenures_open ON company_tenures(user) WHERE left_at IS NULL;
INSERT INTO company_tenures(company_id,user,joined_at) SELECT m.company_id,m.user,m.joined_at FROM eco_company_members m WHERE NOT EXISTS(SELECT 1 FROM company_tenures t WHERE t.user=m.user AND t.left_at IS NULL);
CREATE TRIGGER IF NOT EXISTS company_member_join AFTER INSERT ON eco_company_members BEGIN
 INSERT INTO company_tenures(company_id,user,joined_at) VALUES(NEW.company_id,NEW.user,NEW.joined_at);
END;
CREATE TRIGGER IF NOT EXISTS company_member_leave AFTER DELETE ON eco_company_members BEGIN
 UPDATE company_tenures SET left_at=strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE user=OLD.user AND company_id=OLD.company_id AND left_at IS NULL;
END;
CREATE TRIGGER IF NOT EXISTS company_member_move AFTER UPDATE OF company_id,joined_at ON eco_company_members WHEN OLD.company_id<>NEW.company_id OR OLD.joined_at<>NEW.joined_at BEGIN
 UPDATE company_tenures SET left_at=strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE user=OLD.user AND company_id=OLD.company_id AND left_at IS NULL;
 INSERT INTO company_tenures(company_id,user,joined_at) VALUES(NEW.company_id,NEW.user,NEW.joined_at);
END;
CREATE TABLE IF NOT EXISTS eco_company_requests(id TEXT PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL REFERENCES accounts(user),status TEXT NOT NULL,created_at TEXT NOT NULL,decided_at TEXT,decided_by TEXT,UNIQUE(company_id,user));
CREATE INDEX IF NOT EXISTS eco_company_requests_pending ON eco_company_requests(company_id,status,created_at);

CREATE TABLE IF NOT EXISTS company_convoys(id TEXT PRIMARY KEY,company_id TEXT NOT NULL REFERENCES eco_companies(id),user TEXT NOT NULL,title TEXT NOT NULL,description TEXT NOT NULL,image_id TEXT NOT NULL REFERENCES company_media(id),created_at TEXT NOT NULL,deleted_at TEXT);
CREATE INDEX IF NOT EXISTS company_convoys_company ON company_convoys(company_id,created_at DESC);
CREATE TABLE IF NOT EXISTS company_convoy_attendance(convoy_id TEXT NOT NULL REFERENCES company_convoys(id),user TEXT NOT NULL REFERENCES accounts(user),created_at TEXT NOT NULL,PRIMARY KEY(convoy_id,user));

CREATE TABLE IF NOT EXISTS company_reactions(post_id TEXT NOT NULL REFERENCES company_posts(id),user TEXT NOT NULL REFERENCES accounts(user),emoji TEXT NOT NULL,created_at TEXT NOT NULL,PRIMARY KEY(post_id,user,emoji));
