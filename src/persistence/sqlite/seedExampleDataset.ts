import { createExampleDataset } from "../../domain/repository/fixtures/exampleDataset.js";
import type { RepositoryDataset } from "../../domain/repository/types.js";
import type { SQLiteDatabase } from "./createDatabase.js";
import { stringifyJsonObject } from "./json.js";

const DELETE_ORDER = [
  "screen_events",
  "module_instances",
  "screen_instances",
  "data_sources",
  "calls",
  "notifications",
  "messages",
  "conversation_participants",
  "conversations",
  "shots",
  "episodes",
  "render_presets",
  "animation_presets",
  "module_theme_configs",
  "apps",
  "actors",
  "device_states",
  "devices",
  "themes",
  "media_assets",
  "productions",
] as const;

function insertRows(
  database: SQLiteDatabase,
  table: string,
  columns: string[],
  records: object[],
  jsonFields: ReadonlySet<string> = new Set(),
  booleanFields: ReadonlySet<string> = new Set(),
): void {
  if (records.length === 0) {
    return;
  }
  const placeholders = columns.map(() => "?").join(", ");
  const statement = database.prepare(
    `INSERT INTO ${table} (${columns.join(", ")}) VALUES (${placeholders})`,
  );
  for (const record of records) {
    const values = columns.map((column) => {
      const value = (record as Record<string, unknown>)[column];
      if (value === undefined || value === null) {
        return null;
      }
      if (jsonFields.has(column)) {
        return stringifyJsonObject(
          value as Record<string, unknown>,
          `${table}.${column}`,
        );
      }
      if (booleanFields.has(column)) {
        return value ? 1 : 0;
      }
      return value as string | number;
    });
    statement.run(...values);
  }
}

function seedRecords(
  database: SQLiteDatabase,
  dataset: RepositoryDataset,
): void {
  insertRows(
    database,
    "productions",
    [
      "id",
      "name",
      "slug",
      "default_fps",
      "created_at",
      "updated_at",
      "settings_json",
      "metadata_json",
    ],
    dataset.productions,
    new Set(["settings_json", "metadata_json"]),
  );
  insertRows(
    database,
    "media_assets",
    [
      "id",
      "production_id",
      "name",
      "asset_type",
      "uri",
      "mime_type",
      "checksum",
      "dimensions_json",
      "metadata_json",
    ],
    dataset.mediaAssets,
    new Set(["dimensions_json", "metadata_json"]),
  );
  insertRows(
    database,
    "themes",
    ["id", "production_id", "name", "family", "version", "tokens_json"],
    dataset.themes,
    new Set(["tokens_json"]),
  );
  insertRows(
    database,
    "devices",
    [
      "id",
      "production_id",
      "name",
      "manufacturer",
      "model",
      "os_family",
      "metrics_json",
      "frame_asset_id",
    ],
    dataset.devices,
    new Set(["metrics_json"]),
  );
  insertRows(
    database,
    "device_states",
    ["id", "production_id", "device_id", "name", "state_json"],
    dataset.deviceStates,
    new Set(["state_json"]),
  );
  insertRows(
    database,
    "actors",
    [
      "id",
      "production_id",
      "display_name",
      "short_name",
      "avatar_asset_id",
      "default_device_id",
      "default_theme_id",
      "metadata_json",
    ],
    dataset.actors,
    new Set(["metadata_json"]),
  );
  insertRows(
    database,
    "apps",
    [
      "id",
      "production_id",
      "name",
      "bundle_key",
      "app_type",
      "icon_asset_id",
      "config_json",
      "metadata_json",
    ],
    dataset.apps,
    new Set(["config_json", "metadata_json"]),
  );
  insertRows(
    database,
    "module_theme_configs",
    [
      "id",
      "production_id",
      "theme_id",
      "app_id",
      "module_id",
      "module_schema_version",
      "name",
      "tokens_json",
      "metadata_json",
    ],
    dataset.moduleThemeConfigs,
    new Set(["tokens_json", "metadata_json"]),
  );
  insertRows(
    database,
    "animation_presets",
    [
      "id",
      "production_id",
      "name",
      "animation_type",
      "version",
      "parameters_json",
    ],
    dataset.animationPresets,
    new Set(["parameters_json"]),
  );
  insertRows(
    database,
    "episodes",
    ["id", "production_id", "name", "slug", "sort_order", "metadata_json"],
    dataset.episodes,
    new Set(["metadata_json"]),
  );
  insertRows(
    database,
    "shots",
    [
      "id",
      "production_id",
      "episode_id",
      "owner_actor_id",
      "name",
      "slug",
      "version",
      "sort_order",
      "duration_frames",
      "fps",
      "render_preset_id",
      "canvas_json",
      "metadata_json",
    ],
    dataset.shots,
    new Set(["canvas_json", "metadata_json"]),
  );
  insertRows(
    database,
    "conversations",
    [
      "id",
      "production_id",
      "name",
      "app_id",
      "owner_actor_id",
      "target_actor_id",
      "metadata_json",
    ],
    dataset.conversations,
    new Set(["metadata_json"]),
  );
  insertRows(
    database,
    "conversation_participants",
    ["id", "conversation_id", "actor_id", "role", "sort_order", "metadata_json"],
    dataset.conversationParticipants,
    new Set(["metadata_json"]),
  );
  insertRows(
    database,
    "messages",
    [
      "id",
      "conversation_id",
      "sort_order",
      "sender_actor_id",
      "message_type",
      "text",
      "start_frame",
      "enter_duration_frames",
      "write_on_enabled",
      "write_on_start_frame",
      "write_on_duration_frames",
      "exit_frame",
      "media_asset_id",
      "style_override_json",
      "animation_override_json",
      "layout_override_json",
      "metadata_json",
    ],
    dataset.messages,
    new Set([
      "style_override_json",
      "animation_override_json",
      "layout_override_json",
      "metadata_json",
    ]),
    new Set(["write_on_enabled"]),
  );
  insertRows(
    database,
    "notifications",
    [
      "id",
      "production_id",
      "app_id",
      "owner_actor_id",
      "sender_actor_id",
      "notification_type",
      "title",
      "body",
      "sort_order",
      "payload_json",
      "style_override_json",
      "metadata_json",
    ],
    dataset.notifications,
    new Set(["payload_json", "style_override_json", "metadata_json"]),
  );
  insertRows(
    database,
    "screen_instances",
    [
      "id",
      "shot_id",
      "app_id",
      "screen_type",
      "module_id",
      "module_schema_version",
      "owner_actor_id",
      "device_id",
      "device_state_id",
      "device_state_json",
      "theme_id",
      "theme_mode",
      "start_frame",
      "end_frame",
      "layer_order",
      "data_ref_json",
      "module_data_json",
      "module_config_json",
      "module_tokens_override_json",
      "transform_json",
      "props_json",
      "transition_in_json",
      "transition_out_json",
    ],
    dataset.screenInstances,
    new Set([
      "data_ref_json",
      "module_data_json",
      "module_config_json",
      "module_tokens_override_json",
      "device_state_json",
      "transform_json",
      "props_json",
      "transition_in_json",
      "transition_out_json",
    ]),
  );
  insertRows(
    database,
    "module_instances",
    [
      "id",
      "screen_instance_id",
      "module_id",
      "module_schema_version",
      "sort_order",
      "content_json",
      "behavior_json",
      "metadata_json",
    ],
    dataset.moduleInstances,
    new Set(["content_json", "behavior_json", "metadata_json"]),
  );
  insertRows(
    database,
    "screen_events",
    [
      "id",
      "screen_instance_id",
      "event_type",
      "start_frame",
      "duration_frames",
      "target_id",
      "animation_preset_id",
      "payload_json",
    ],
    dataset.screenEvents,
    new Set(["payload_json"]),
  );
}

export function seedExampleDataset(
  database: SQLiteDatabase,
  dataset = createExampleDataset(),
): void {
  const seedTransaction = database.transaction(() => {
    for (const table of DELETE_ORDER) {
      database.prepare(`DELETE FROM ${table}`).run();
    }
    seedRecords(database, dataset);
  });
  seedTransaction();

  const foreignKeyErrors = database.pragma("foreign_key_check") as unknown[];
  if (foreignKeyErrors.length > 0) {
    throw new Error(
      `SQLite seed produced foreign-key errors: ${JSON.stringify(foreignKeyErrors)}`,
    );
  }
}
