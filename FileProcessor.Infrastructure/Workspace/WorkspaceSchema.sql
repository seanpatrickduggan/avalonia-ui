PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS sessions (
  id INTEGER PRIMARY KEY,
  started_at_ms INTEGER NOT NULL,
  ended_at_ms   INTEGER,
  app_version   TEXT,
  user_name     TEXT,
  host_name     TEXT,
  notes         TEXT
);

CREATE TABLE IF NOT EXISTS operations (
  id            INTEGER PRIMARY KEY,
  session_id    INTEGER REFERENCES sessions(id) ON DELETE SET NULL,
  type          TEXT,
  status        TEXT,
  started_at_ms INTEGER NOT NULL,
  ended_at_ms   INTEGER,
  name          TEXT,
  metadata_json TEXT
);
CREATE INDEX IF NOT EXISTS ix_operations_session ON operations(session_id);
CREATE INDEX IF NOT EXISTS ix_operations_time    ON operations(started_at_ms);

CREATE TABLE IF NOT EXISTS items (
  id               INTEGER PRIMARY KEY,
  operation_id     INTEGER NOT NULL REFERENCES operations(id) ON DELETE CASCADE,
  external_id      TEXT,
  status           TEXT,
  highest_severity INTEGER DEFAULT 2,
  started_at_ms    INTEGER,
  ended_at_ms      INTEGER,
  metrics_json     TEXT
);
CREATE INDEX IF NOT EXISTS ix_items_operation ON items(operation_id);
CREATE INDEX IF NOT EXISTS ix_items_ext ON items(external_id);

-- 0=Trace,1=Debug,2=Info,3=Warning,4=Error,5=Critical
CREATE TABLE IF NOT EXISTS log_entries (
  id          INTEGER PRIMARY KEY,
  ts_ms       INTEGER NOT NULL,
  level       INTEGER NOT NULL,
  category    TEXT,
  subcategory TEXT,
  message     TEXT,
  data_json   TEXT,
  session_id  INTEGER REFERENCES sessions(id) ON DELETE SET NULL,
  operation_id INTEGER REFERENCES operations(id) ON DELETE CASCADE,
  item_id     INTEGER REFERENCES items(id) ON DELETE CASCADE,
  source      TEXT
);
CREATE INDEX IF NOT EXISTS ix_logs_ts          ON log_entries(ts_ms);
CREATE INDEX IF NOT EXISTS ix_logs_level       ON log_entries(level);
CREATE INDEX IF NOT EXISTS ix_logs_cat_sub     ON log_entries(category, subcategory);
CREATE INDEX IF NOT EXISTS ix_logs_operation_ts ON log_entries(operation_id, ts_ms);
CREATE INDEX IF NOT EXISTS ix_logs_item_ts     ON log_entries(item_id, ts_ms);
