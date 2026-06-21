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

export function applyInitialSchema(database: SQLiteDatabase): void {
  database.exec(readFileSync(schemaPath, "utf8"));
  applyAdditiveV2Migration(database);
  applyAdditiveV3Migration(database);
  applyAdditiveV4Migration(database);
  applyAdditiveV5Migration(database);
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
