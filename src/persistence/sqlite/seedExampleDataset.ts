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
  "navigation_bars",
  "component_classes",
  "status_bars",
  "icon_themes",
  "production_fonts",
  "palette_colors",
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

interface SeedDeviceSpec {
  id: string;
  name: string;
  manufacturer: string;
  model: string;
  osFamily: string;
  width: number;
  height: number;
  scale: number;
}

const BASE_DEVICE_SPECS: SeedDeviceSpec[] = [
  {
    id: "device_iphone_15_pro",
    name: "iPhone 15 Pro",
    manufacturer: "Apple",
    model: "iPhone 15 Pro",
    osFamily: "ios",
    width: 1179,
    height: 2556,
    scale: 3,
  },
  {
    id: "device_iphone_generic",
    name: "iPhone 15 Pro Max",
    manufacturer: "Apple",
    model: "iPhone 15 Pro Max",
    osFamily: "ios",
    width: 1290,
    height: 2796,
    scale: 3,
  },
  {
    id: "device_iphone_14_pro",
    name: "iPhone 14 Pro",
    manufacturer: "Apple",
    model: "iPhone 14 Pro",
    osFamily: "ios",
    width: 1179,
    height: 2556,
    scale: 3,
  },
  {
    id: "device_samsung_galaxy_s24",
    name: "Samsung Galaxy S24",
    manufacturer: "Samsung",
    model: "Galaxy S24",
    osFamily: "android",
    width: 1080,
    height: 2340,
    scale: 3,
  },
  {
    id: "device_samsung_galaxy_s24_ultra",
    name: "Samsung Galaxy S24 Ultra",
    manufacturer: "Samsung",
    model: "Galaxy S24 Ultra",
    osFamily: "android",
    width: 1440,
    height: 3120,
    scale: 3,
  },
  {
    id: "device_google_pixel_8_pro",
    name: "Google Pixel 8 Pro",
    manufacturer: "Google",
    model: "Pixel 8 Pro",
    osFamily: "android",
    width: 1344,
    height: 2992,
    scale: 3,
  },
];

function deviceMetrics(width: number, height: number, scale: number) {
  const statusBarHeight = Math.round(height * 0.063);
  return {
    designSpace: {
      width: Math.round(width / scale),
      height: Math.round(height / scale),
      unit: "logical",
    },
    renderSize: { width, height },
    scaleToPixels: scale,
    canvas: { width, height },
    screen: { x: 0, y: 0, width, height },
    viewport: { x: 0, y: 0, width, height },
    safeArea: {
      top: statusBarHeight,
      right: 0,
      bottom: Math.round(height * 0.036),
      left: 0,
    },
    statusBar: {
      x: 0,
      y: 0,
      width,
      height: statusBarHeight,
    },
    cornerRadius: Math.round(width * 0.12),
    pixelRatio: scale,
    defaultScreenScale: 1,
  };
}

function productionDevices(dataset: RepositoryDataset) {
  const baseDevice = dataset.devices[0];
  const productionId = dataset.productions[0]?.id ?? baseDevice?.production_id;
  return BASE_DEVICE_SPECS.map((spec) => ({
    id: spec.id,
    production_id: productionId,
    name: spec.name,
    manufacturer: spec.manufacturer,
    model: spec.model,
    os_family: spec.osFamily,
    metrics_json:
      spec.id === baseDevice?.id
        ? baseDevice.metrics_json
        : deviceMetrics(spec.width, spec.height, spec.scale),
  }));
}

function productionRenderPresets(dataset: RepositoryDataset) {
  const productionId = dataset.productions[0]?.id ?? "production_demo";
  function ffmpegArgs(codec: string) {
    if (codec === "exr") return "-compression zip -pix_fmt rgba64le";
    if (codec === "png") return "-compression_level 6 -pix_fmt rgba";
    if (codec === "prores_422_hq") return "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le";
    if (codec === "prores_4444") return "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le";
    if (codec === "h264_low") return "-c:v libx264 -preset medium -crf 28 -pix_fmt yuv420p";
    if (codec === "h264_medium") return "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p";
    if (codec === "h264_high") return "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p";
    return "";
  }
  return [
    {
      id: "render_preset_mov_prores_422_hq",
      production_id: productionId,
      name: "MOV ProRes 422 HQ",
      width: 1,
      height: 1,
      fps: 1,
      format: "mov",
      codec_json: { codec: "prores_422_hq" },
      color_json: { colorSpace: "rec709", alpha: false },
      quality_json: { profile: "prores_422_hq" },
      export_json: { extension: "mov", ffmpegArgs: ffmpegArgs("prores_422_hq") },
    },
    {
      id: "render_preset_mov_prores_4444_alpha",
      production_id: productionId,
      name: "MOV ProRes 4444 Alpha",
      width: 1,
      height: 1,
      fps: 1,
      format: "mov",
      codec_json: { codec: "prores_4444" },
      color_json: { colorSpace: "rec709", alpha: true },
      quality_json: { profile: "prores_4444" },
      export_json: { extension: "mov", ffmpegArgs: ffmpegArgs("prores_4444") },
    },
    {
      id: "render_preset_mov_h264_low",
      production_id: productionId,
      name: "MOV H.264 Low",
      width: 1,
      height: 1,
      fps: 1,
      format: "mov",
      codec_json: { codec: "h264_low" },
      color_json: { colorSpace: "rec709", alpha: false },
      quality_json: { profile: "h264_low" },
      export_json: { extension: "mov", ffmpegArgs: ffmpegArgs("h264_low") },
    },
    {
      id: "render_preset_mov_h264_medium",
      production_id: productionId,
      name: "MOV H.264 Medium",
      width: 1,
      height: 1,
      fps: 1,
      format: "mov",
      codec_json: { codec: "h264_medium" },
      color_json: { colorSpace: "rec709", alpha: false },
      quality_json: { profile: "h264_medium" },
      export_json: { extension: "mov", ffmpegArgs: ffmpegArgs("h264_medium") },
    },
    {
      id: "render_preset_mov_h264_high",
      production_id: productionId,
      name: "MOV H.264 High",
      width: 1,
      height: 1,
      fps: 1,
      format: "mov",
      codec_json: { codec: "h264_high" },
      color_json: { colorSpace: "rec709", alpha: false },
      quality_json: { profile: "h264_high" },
      export_json: { extension: "mov", ffmpegArgs: ffmpegArgs("h264_high") },
    },
    {
      id: "render_preset_image_png",
      production_id: productionId,
      name: "PNG Image Sequence",
      width: 1,
      height: 1,
      fps: 1,
      format: "image",
      codec_json: { codec: "png" },
      color_json: { colorSpace: "srgb", alpha: true },
      quality_json: { profile: "png" },
      export_json: { extension: "png", sequence: true, ffmpegArgs: ffmpegArgs("png") },
    },
    {
      id: "render_preset_image_exr",
      production_id: productionId,
      name: "EXR Image Sequence",
      width: 1,
      height: 1,
      fps: 1,
      format: "image",
      codec_json: { codec: "exr" },
      color_json: { colorSpace: "linear", alpha: true },
      quality_json: { profile: "exr" },
      export_json: { extension: "exr", sequence: true, ffmpegArgs: ffmpegArgs("exr") },
    },
  ];
}

function productionRecords(dataset: RepositoryDataset) {
  return dataset.productions.map((production) => ({
    ...production,
    settings_json: {
      mediaRoot: "",
      ...(production.settings_json ?? {}),
    },
  }));
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
    productionRecords(dataset),
    new Set(["settings_json", "metadata_json"]),
  );
  insertRows(
    database,
    "production_fonts",
    [
      "id",
      "production_id",
      "family",
      "category",
      "files_json",
      "source_path",
      "metadata_json",
    ],
    dataset.productionFonts,
    new Set(["files_json", "metadata_json"]),
  );
  insertRows(
    database,
    "palette_colors",
    [
      "id",
      "production_id",
      "token",
      "value_hex",
      "is_neutral",
      "metadata_json",
    ],
    dataset.paletteColors,
    new Set(["metadata_json"]),
    new Set(["is_neutral"]),
  );
  insertRows(
    database,
    "icon_themes",
    [
      "id",
      "production_id",
      "name",
      "family",
      "asset_root",
      "mapping_json",
      "metadata_json",
    ],
    dataset.iconThemes,
    new Set(["mapping_json", "metadata_json"]),
  );
  insertRows(
    database,
    "status_bars",
    [
      "id",
      "production_id",
      "name",
      "family",
      "config_json",
      "metadata_json",
    ],
    dataset.statusBars,
    new Set(["config_json", "metadata_json"]),
  );
  insertRows(
    database,
    "navigation_bars",
    [
      "id",
      "production_id",
      "name",
      "family",
      "config_json",
      "metadata_json",
    ],
    dataset.navigationBars,
    new Set(["config_json", "metadata_json"]),
  );
  insertRows(
    database,
    "component_classes",
    [
      "id",
      "production_id",
      "component_type",
      "name",
      "tokens_json",
      "metadata_json",
    ],
    dataset.componentClasses,
    new Set(["tokens_json", "metadata_json"]),
  );
  insertRows(
    database,
    "themes",
    [
      "id",
      "production_id",
      "name",
      "family",
      "version",
      "icon_theme_id",
      "status_bar_id",
      "navigation_bar_id",
      "tokens_json",
    ],
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
    ],
    productionDevices(dataset),
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
    "render_presets",
    [
      "id",
      "production_id",
      "name",
      "width",
      "height",
      "fps",
      "format",
      "codec_json",
      "color_json",
      "quality_json",
      "export_json",
    ],
    productionRenderPresets(dataset),
    new Set(["codec_json", "color_json", "quality_json", "export_json"]),
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
      "duration_frames",
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
      "animation_json",
      "metadata_json",
    ],
    dataset.moduleInstances,
    new Set(["content_json", "behavior_json", "animation_json", "metadata_json"]),
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
