import { mkdirSync, readFileSync } from "node:fs";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import Database from "better-sqlite3";
import { developmentDatabasePath } from "./paths.js";

const schemaPath = fileURLToPath(new URL("./schema.sql", import.meta.url));

export type SQLiteDatabase = Database.Database;

const SCREEN_INSTANCE_V2_COLUMNS = {
  module_id: "TEXT",
  module_schema_version: "INTEGER",
  theme_mode: "TEXT",
  module_data_json: "TEXT",
  module_config_json: "TEXT",
  module_tokens_override_json: "TEXT",
} as const;

function applyAdditiveV2Migration(database: SQLiteDatabase): void {
  const existingColumns = new Set(
    (
      database.pragma("table_info(screen_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );

  for (const [column, definition] of Object.entries(
    SCREEN_INSTANCE_V2_COLUMNS,
  )) {
    if (!existingColumns.has(column)) {
      database.exec(
        `ALTER TABLE screen_instances ADD COLUMN ${column} ${definition}`,
      );
    }
  }
  database.pragma("user_version = 2");
}

function applyAdditiveV3Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS module_theme_configs (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      theme_id TEXT NOT NULL REFERENCES themes(id) ON DELETE CASCADE,
      module_id TEXT NOT NULL,
      module_schema_version INTEGER NOT NULL CHECK (module_schema_version > 0),
      name TEXT NOT NULL,
      tokens_json TEXT NOT NULL,
      metadata_json TEXT,
      UNIQUE (theme_id, module_id, module_schema_version, name)
    );
    CREATE INDEX IF NOT EXISTS idx_module_theme_configs_lookup
      ON module_theme_configs(theme_id, module_id, module_schema_version);
  `);
  database.pragma("user_version = 3");
}

function applyAdditiveV4Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS episodes (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      name TEXT NOT NULL,
      slug TEXT,
      sort_order INTEGER CHECK (sort_order >= 0),
      metadata_json TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_episodes_production
      ON episodes(production_id, sort_order);
  `);

  const existingShotColumns = new Set(
    (
      database.pragma("table_info(shots)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!existingShotColumns.has("episode_id")) {
    database.exec("ALTER TABLE shots ADD COLUMN episode_id TEXT");
  }
  const existingEpisodeColumns = new Set(
    (
      database.pragma("table_info(episodes)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!existingEpisodeColumns.has("slug")) {
    database.exec("ALTER TABLE episodes ADD COLUMN slug TEXT");
    database.exec(`
      UPDATE episodes
      SET slug = lower(replace(trim(name), ' ', '-'))
      WHERE slug IS NULL OR trim(slug) = ''
    `);
  }
  database.exec(`
    CREATE INDEX IF NOT EXISTS idx_shots_episode
      ON shots(episode_id, sort_order);
  `);
  database.pragma("user_version = 4");
}

function applyAdditiveV5Migration(database: SQLiteDatabase): void {
  const existingShotColumns = new Set(
    (
      database.pragma("table_info(shots)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!existingShotColumns.has("owner_actor_id")) {
    database.exec("ALTER TABLE shots ADD COLUMN owner_actor_id TEXT");
  }
  database.exec(`
    CREATE INDEX IF NOT EXISTS idx_shots_owner_actor
      ON shots(owner_actor_id);
  `);
  database.pragma("user_version = 5");
}

function applyBreakingV6Marker(database: SQLiteDatabase): void {
  database.pragma("user_version = 6");
}

function ensureModuleInstanceAnimationColumn(database: SQLiteDatabase): void {
  const moduleInstanceColumns = new Set(
    (
      database.pragma("table_info(module_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!moduleInstanceColumns.has("animation_json")) {
    database.exec(
      `ALTER TABLE module_instances
       ADD COLUMN animation_json TEXT NOT NULL DEFAULT '{"schemaVersion":1,"tracks":[]}'`,
    );
  }
}

function applyAdditiveV7Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS module_instances (
      id TEXT PRIMARY KEY,
      screen_instance_id TEXT NOT NULL REFERENCES screen_instances(id) ON DELETE CASCADE,
      module_id TEXT NOT NULL,
      module_schema_version INTEGER NOT NULL CHECK (module_schema_version > 0),
      sort_order INTEGER CHECK (sort_order >= 0),
      content_json TEXT NOT NULL,
      behavior_json TEXT NOT NULL,
      animation_json TEXT NOT NULL DEFAULT '{"schemaVersion":1,"tracks":[]}',
      metadata_json TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_module_instances_screen
      ON module_instances(screen_instance_id, sort_order, id);
  `);
  ensureModuleInstanceAnimationColumn(database);
  database.exec(`
    INSERT OR IGNORE INTO module_instances (
      id,
      screen_instance_id,
      module_id,
      module_schema_version,
      sort_order,
      content_json,
      behavior_json,
      animation_json,
      metadata_json
    )
    SELECT
      id || ':module',
      id,
      COALESCE(module_id, screen_type),
      COALESCE(module_schema_version, 1),
      0,
      COALESCE(module_data_json, '{}'),
      COALESCE(module_config_json, '{}'),
      '{"schemaVersion":1,"tracks":[]}',
      '{}'
    FROM screen_instances
    WHERE module_id IS NOT NULL
      AND module_schema_version IS NOT NULL
      AND module_data_json IS NOT NULL
      AND module_config_json IS NOT NULL;
  `);
  database.pragma("user_version = 7");
}

function applyAdditiveV8Migration(database: SQLiteDatabase): void {
  const productionColumns = new Set(
    (
      database.pragma("table_info(productions)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!productionColumns.has("default_fps")) {
    database.exec(
      "ALTER TABLE productions ADD COLUMN default_fps INTEGER NOT NULL DEFAULT 30 CHECK (default_fps > 0)",
    );
  }

  const shotColumns = new Set(
    (
      database.pragma("table_info(shots)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!shotColumns.has("slug")) {
    database.exec("ALTER TABLE shots ADD COLUMN slug TEXT");
    database.exec(`
      UPDATE shots
      SET slug = lower(replace(trim(name), ' ', '-'))
      WHERE slug IS NULL OR trim(slug) = ''
    `);
  }
  if (!shotColumns.has("version")) {
    database.exec(
      "ALTER TABLE shots ADD COLUMN version INTEGER NOT NULL DEFAULT 1 CHECK (version >= 0)",
    );
  }
  database.pragma("user_version = 8");
}

function applyAdditiveV9Migration(database: SQLiteDatabase): void {
  const screenInstanceColumns = new Set(
    (
      database.pragma("table_info(screen_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!screenInstanceColumns.has("device_state_json")) {
    database.exec("ALTER TABLE screen_instances ADD COLUMN device_state_json TEXT");
    database.exec(`
      UPDATE screen_instances
      SET device_state_json = (
        SELECT device_states.state_json
        FROM device_states
        WHERE device_states.id = screen_instances.device_state_id
      )
      WHERE device_state_json IS NULL
        AND device_state_id IS NOT NULL
    `);
  }
  database.pragma("user_version = 9");
}

function applyAdditiveV10Migration(database: SQLiteDatabase): void {
  ensureModuleInstanceAnimationColumn(database);
  database.pragma("user_version = 10");
}

function applyAdditiveV11Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS icon_themes (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      name TEXT NOT NULL,
      family TEXT NOT NULL,
      asset_root TEXT NOT NULL,
      mapping_json TEXT NOT NULL,
      metadata_json TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_icon_themes_production
      ON icon_themes(production_id, name);
  `);

  const themeColumns = new Set(
    (
      database.pragma("table_info(themes)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!themeColumns.has("icon_theme_id")) {
    database.exec(
      "ALTER TABLE themes ADD COLUMN icon_theme_id TEXT REFERENCES icon_themes(id) ON DELETE SET NULL",
    );
  }
  database.pragma("user_version = 11");
}

function applyAdditiveV12Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS status_bars (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      name TEXT NOT NULL,
      family TEXT NOT NULL,
      config_json TEXT NOT NULL,
      metadata_json TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_status_bars_production
      ON status_bars(production_id, name);
  `);

  const themeColumns = new Set(
    (
      database.pragma("table_info(themes)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!themeColumns.has("status_bar_id")) {
    database.exec(
      "ALTER TABLE themes ADD COLUMN status_bar_id TEXT REFERENCES status_bars(id) ON DELETE SET NULL",
    );
  }
  database.pragma("user_version = 12");
}

export function applyInitialSchema(database: SQLiteDatabase): void {
  database.exec(readFileSync(schemaPath, "utf8"));
  applyAdditiveV2Migration(database);
  applyAdditiveV3Migration(database);
  applyAdditiveV4Migration(database);
  applyAdditiveV5Migration(database);
  applyBreakingV6Marker(database);
  applyAdditiveV7Migration(database);
  applyAdditiveV8Migration(database);
  applyAdditiveV9Migration(database);
  applyAdditiveV10Migration(database);
  applyAdditiveV11Migration(database);
  applyAdditiveV12Migration(database);
  database.pragma("foreign_keys = ON");
}

export function createDatabase(
  databasePath = developmentDatabasePath,
): SQLiteDatabase {
  if (databasePath !== ":memory:") {
    mkdirSync(dirname(databasePath), { recursive: true });
  }
  const database = new Database(databasePath);
  database.pragma("foreign_keys = ON");
  applyInitialSchema(database);
  return database;
}
