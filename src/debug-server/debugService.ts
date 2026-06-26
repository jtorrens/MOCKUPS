import type { z } from "zod";
import {
  ActorSchema,
  AnimationPresetSchema,
  AppSchema,
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  DeviceSchema,
  DeviceStateSchema,
  EpisodeSchema,
  IconThemeSchema,
  JsonObjectSchema,
  MediaAssetSchema,
  ModuleInstanceSchema,
  ModuleThemeConfigSchema,
  NavigationBarSchema,
  ProductionSchema,
  PaletteColorSchema,
  ProductionFontSchema,
  RenderPresetSchema,
  ScreenInstanceSchema,
  ShotSchema,
  StatusBarSchema,
  ThemeSchema,
  type JsonObject,
} from "../domain/schemas/index.js";
import {
  mergeTokenObjects,
  moduleTypographyDefaultsFromFonts,
  resolveGlobalThemeTokens,
  resolveModuleThemeTokens,
  resolveShot,
} from "../domain/resolvers/index.js";
import { ChatScreenModule } from "../visual/modules/screens/ChatScreenModule.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { SQLiteRepository } from "../persistence/sqlite/SQLiteRepository.js";
import type { SQLiteDatabase } from "../persistence/sqlite/createDatabase.js";
import {
  readNullableJson,
  readOptionalJson,
  readRequiredJson,
  stringifyJsonObject,
} from "../persistence/sqlite/json.js";

type Row = Record<string, unknown>;

function extractCssUrl(value: unknown) {
  if (typeof value !== "string") return "";
  const trimmed = value.trim();
  const match = /^url\((['"]?)(.*?)\1\)$/i.exec(trimmed);
  return match?.[2] ?? "";
}

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function previewMediaUrl(productionId: string, filePath: string) {
  if (
    !filePath ||
    /^(data:|blob:|https?:|\/api\/media)/i.test(filePath)
  ) {
    return filePath;
  }
  return `/api/media?productionId=${encodeURIComponent(productionId)}&path=${encodeURIComponent(filePath)}`;
}

function previewMediaFrameUrl({
  filePath,
  fps,
  frame,
  productionId,
}: {
  filePath: string;
  fps: number;
  frame: number;
  productionId: string;
}) {
  if (!filePath || /^(data:|blob:|https?:|\/api\/media-frame)/i.test(filePath)) {
    return filePath;
  }
  return `/api/media-frame?productionId=${encodeURIComponent(productionId)}&path=${encodeURIComponent(filePath)}&frame=${encodeURIComponent(String(Math.max(0, Math.floor(frame))))}&fps=${encodeURIComponent(String(fps > 0 ? fps : 30))}`;
}

function rewriteRenderableMediaUrls(
  node: RenderableNode,
  productionId: string,
): RenderableNode {
  const backgroundUrl = extractCssUrl(node.style?.backgroundImage);
  const maskUrl = extractCssUrl(node.style?.maskImage);
  const webkitMaskUrl = extractCssUrl(node.style?.WebkitMaskImage);
  const nextStyle = node.style
    ? {
        ...node.style,
        ...(backgroundUrl
          ? {
              backgroundImage: cssUrl(
                previewMediaUrl(productionId, backgroundUrl),
              ),
            }
          : {}),
        ...(maskUrl
          ? { maskImage: cssUrl(previewMediaUrl(productionId, maskUrl)) }
          : {}),
        ...(webkitMaskUrl
          ? {
              WebkitMaskImage: cssUrl(
                previewMediaUrl(productionId, webkitMaskUrl),
              ),
            }
          : {}),
      }
    : node.style;
  const assetUri =
    node.asset?.type === "image" && typeof node.asset.uri === "string"
      ? previewMediaUrl(productionId, node.asset.uri)
      : undefined;
  const metadataUri =
    typeof node.metadata?.uri === "string"
      ? previewMediaUrl(productionId, node.metadata.uri)
      : undefined;
  const videoPosterUrl =
    node.type === "message_bubble_media_image" &&
    node.metadata?.type === "video" &&
    typeof node.metadata.uri === "string"
      ? previewMediaFrameUrl({
          filePath: node.metadata.uri,
          fps:
            typeof node.metadata.fps === "number" && node.metadata.fps > 0
              ? node.metadata.fps
              : 30,
          frame:
            typeof node.metadata.frame === "number" && node.metadata.frame >= 0
              ? node.metadata.frame
              : typeof node.frame === "number"
                ? node.frame
                : 0,
          productionId,
        })
      : "";
  return {
    ...node,
    ...(nextStyle || videoPosterUrl
      ? {
          style: {
            ...(nextStyle ?? {}),
            ...(videoPosterUrl
              ? {
                  backgroundImage: cssUrl(videoPosterUrl),
                  backgroundRepeat: "no-repeat",
                }
              : {}),
          },
        }
      : {}),
    ...(assetUri ? { asset: { type: "image", uri: assetUri } } : {}),
    ...(metadataUri
      ? { metadata: { ...node.metadata, uri: metadataUri } }
      : {}),
    ...(node.children
      ? {
          children: node.children.map((child) =>
            rewriteRenderableMediaUrls(child, productionId),
          ),
        }
      : {}),
  };
}
type FieldKind = "string" | "number" | "json";

export interface AppFieldDefinition {
  column: string;
  label: string;
  kind: FieldKind;
  nullable?: boolean;
  readonly?: boolean;
}

export interface AppTableDefinition {
  id: string;
  label: string;
  table: string;
  titleColumn: string;
  fields: AppFieldDefinition[];
  jsonFields: string[];
  optionalScalars?: string[];
}

export interface DebugSelection {
  productionId: string;
  shotId: string;
  screenInstanceId: string;
  frame: number;
}

export interface DebugSaveRequest extends DebugSelection {
  fields: {
    moduleData: unknown;
    moduleConfig: unknown;
    themeTokens: unknown;
    deviceMetrics: unknown;
    deviceState: unknown;
  };
}

export function zodMessage(error: z.ZodError): string {
  return error.issues
    .map((issue) => `${issue.path.join(".") || "value"}: ${issue.message}`)
    .join("\n");
}

export function asObject(value: unknown, label: string): JsonObject {
  const result = JsonObjectSchema.safeParse(value);
  if (!result.success) {
    throw new Error(`${label}\n${zodMessage(result.error)}`);
  }
  return result.data;
}

function nullsToUndefined(row: Row, fields: string[] = []): Row {
  const copy = { ...row };
  for (const field of fields) {
    if (copy[field] === null) {
      delete copy[field];
    }
  }
  return copy;
}

function isObject(value: unknown): value is Row {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function parseScreenInstance(row: Row) {
  return ScreenInstanceSchema.parse({
    ...row,
    data_ref_json: readNullableJson(row, "data_ref_json"),
    module_data_json: readOptionalJson(row, "module_data_json"),
    module_config_json: readOptionalJson(row, "module_config_json"),
    module_tokens_override_json: readOptionalJson(
      row,
      "module_tokens_override_json",
    ),
    device_state_json: readOptionalJson(row, "device_state_json"),
    transform_json: readRequiredJson(row, "transform_json"),
    props_json: readRequiredJson(row, "props_json"),
    transition_in_json: readNullableJson(row, "transition_in_json"),
    transition_out_json: readNullableJson(row, "transition_out_json"),
  });
}

export function parseModuleInstance(row: Row) {
  return ModuleInstanceSchema.parse({
    ...row,
    content_json: readRequiredJson(row, "content_json"),
    behavior_json: readRequiredJson(row, "behavior_json"),
    animation_json: readRequiredJson(row, "animation_json"),
    metadata_json: readOptionalJson(row, "metadata_json"),
  });
}

export const APP_TABLES = [
  {
    id: "productions",
    label: "Productions",
    table: "productions",
    titleColumn: "name",
    jsonFields: ["settings_json", "metadata_json"],
    optionalScalars: ["slug", "created_at", "updated_at"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "name", label: "Name", kind: "string" },
      { column: "slug", label: "Slug", kind: "string", nullable: true },
      { column: "default_fps", label: "Default FPS", kind: "number" },
      { column: "settings_json", label: "Production settings", kind: "json" },
      { column: "metadata_json", label: "Production notes", kind: "json" },
    ],
  },
  {
    id: "episodes",
    label: "Episodes",
    table: "episodes",
    titleColumn: "name",
    jsonFields: ["metadata_json"],
    optionalScalars: ["slug", "sort_order"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "slug", label: "Slug", kind: "string", nullable: true },
      { column: "sort_order", label: "Sort order", kind: "number" },
      { column: "metadata_json", label: "Episode notes", kind: "json" },
    ],
  },
  {
    id: "shots",
    label: "Shots",
    table: "shots",
    titleColumn: "name",
    jsonFields: ["canvas_json", "metadata_json"],
    optionalScalars: [
      "episode_id",
      "owner_actor_id",
      "slug",
      "version",
      "sort_order",
      "render_preset_id",
    ],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "episode_id", label: "Episode", kind: "string", nullable: true },
      {
        column: "owner_actor_id",
        label: "Owner actor",
        kind: "string",
        nullable: true,
      },
      { column: "name", label: "Name", kind: "string" },
      { column: "slug", label: "Slug", kind: "string", nullable: true },
      { column: "version", label: "Version", kind: "number" },
      { column: "sort_order", label: "Sort order", kind: "number" },
      { column: "duration_frames", label: "Duration frames", kind: "number" },
      { column: "fps", label: "FPS", kind: "number" },
      {
        column: "render_preset_id",
        label: "Render preset ID",
        kind: "string",
        nullable: true,
      },
      { column: "canvas_json", label: "Canvas setup", kind: "json" },
      { column: "metadata_json", label: "Shot notes", kind: "json" },
    ],
  },
  {
    id: "screen_instances",
    label: "Screen Instances",
    table: "screen_instances",
    titleColumn: "id",
    jsonFields: [
      "data_ref_json",
      "device_state_json",
      "transform_json",
      "props_json",
      "transition_in_json",
      "transition_out_json",
    ],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "shot_id", label: "Shot ID", kind: "string" },
      { column: "app_id", label: "App", kind: "string", readonly: true },
      { column: "screen_type", label: "Screen type", kind: "string" },
      { column: "module_id", label: "Module ID", kind: "string" },
      {
        column: "module_schema_version",
        label: "Module schema version",
        kind: "number",
      },
      {
        column: "device_state_id",
        label: "Device state",
        kind: "string",
        nullable: true,
      },
      { column: "device_state_json", label: "Device state", kind: "json" },
      {
        column: "theme_mode",
        label: "Theme mode",
        kind: "string",
        nullable: true,
      },
      { column: "start_frame", label: "Start frame", kind: "number" },
      { column: "end_frame", label: "End frame", kind: "number" },
      { column: "layer_order", label: "Layer order", kind: "number" },
      { column: "transform_json", label: "Screen transform", kind: "json" },
      { column: "transition_out_json", label: "Transition", kind: "json" },
    ],
  },
  {
    id: "module_instances",
    label: "Module Instances",
    table: "module_instances",
    titleColumn: "id",
    jsonFields: ["content_json", "behavior_json", "animation_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "screen_instance_id",
        label: "Screen instance",
        kind: "string",
      },
      { column: "module_id", label: "Module ID", kind: "string" },
      {
        column: "module_schema_version",
        label: "Module schema version",
        kind: "number",
      },
      { column: "sort_order", label: "Sort order", kind: "number" },
      { column: "content_json", label: "Module content", kind: "json" },
      { column: "behavior_json", label: "Module behavior", kind: "json" },
      { column: "animation_json", label: "Module animation", kind: "json" },
      { column: "metadata_json", label: "Module instance notes", kind: "json" },
    ],
  },
  {
    id: "actors",
    label: "Actors",
    table: "actors",
    titleColumn: "display_name",
    jsonFields: ["metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "display_name", label: "Display name", kind: "string" },
      {
        column: "short_name",
        label: "Short name",
        kind: "string",
        nullable: true,
      },
      {
        column: "avatar_asset_id",
        label: "Avatar asset ID",
        kind: "string",
        nullable: true,
      },
      {
        column: "default_device_id",
        label: "Default device ID",
        kind: "string",
        nullable: true,
      },
      {
        column: "default_theme_id",
        label: "Default theme ID",
        kind: "string",
        nullable: true,
      },
      { column: "metadata_json", label: "Actor notes", kind: "json" },
    ],
  },
  {
    id: "icon_themes",
    label: "Icon Themes",
    table: "icon_themes",
    titleColumn: "name",
    jsonFields: ["mapping_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "production_id",
        label: "Production ID",
        kind: "string",
        readonly: true,
      },
      { column: "name", label: "Name", kind: "string" },
      { column: "family", label: "Family", kind: "string" },
      { column: "asset_root", label: "Asset root", kind: "string" },
      { column: "mapping_json", label: "Icon mapping", kind: "json" },
      { column: "metadata_json", label: "Icon theme notes", kind: "json" },
    ],
  },
  {
    id: "status_bars",
    label: "Status Bars",
    table: "status_bars",
    titleColumn: "name",
    jsonFields: ["config_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "production_id",
        label: "Production ID",
        kind: "string",
        readonly: true,
      },
      { column: "name", label: "Name", kind: "string" },
      { column: "family", label: "Family", kind: "string" },
      { column: "config_json", label: "Status bar config", kind: "json" },
      { column: "metadata_json", label: "Status bar notes", kind: "json" },
    ],
  },
  {
    id: "navigation_bars",
    label: "Navigation Bars",
    table: "navigation_bars",
    titleColumn: "name",
    jsonFields: ["config_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "production_id",
        label: "Production ID",
        kind: "string",
        readonly: true,
      },
      { column: "name", label: "Name", kind: "string" },
      { column: "family", label: "Family", kind: "string" },
      { column: "config_json", label: "Navigation bar config", kind: "json" },
      { column: "metadata_json", label: "Navigation bar notes", kind: "json" },
    ],
  },
  {
    id: "themes",
    label: "Themes",
    table: "themes",
    titleColumn: "name",
    jsonFields: ["tokens_json"],
    optionalScalars: ["icon_theme_id", "status_bar_id", "navigation_bar_id"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "family", label: "Family", kind: "string", readonly: true },
      {
        column: "icon_theme_id",
        label: "Icon theme",
        kind: "string",
        nullable: true,
      },
      {
        column: "status_bar_id",
        label: "Status bar",
        kind: "string",
        nullable: true,
      },
      {
        column: "navigation_bar_id",
        label: "Navigation bar",
        kind: "string",
        nullable: true,
      },
      { column: "version", label: "Version", kind: "string" },
      { column: "tokens_json", label: "Theme tokens", kind: "json" },
    ],
  },
  {
    id: "module_theme_configs",
    label: "Module Theme Configs",
    table: "module_theme_configs",
    titleColumn: "name",
    jsonFields: ["tokens_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "theme_id", label: "Theme ID", kind: "string" },
      { column: "app_id", label: "App", kind: "string" },
      { column: "module_id", label: "Module ID", kind: "string" },
      {
        column: "module_schema_version",
        label: "Module schema version",
        kind: "number",
      },
      { column: "name", label: "Name", kind: "string" },
      { column: "tokens_json", label: "Module design tokens", kind: "json" },
      { column: "metadata_json", label: "Module theme notes", kind: "json" },
    ],
  },
  {
    id: "devices",
    label: "Devices",
    table: "devices",
    titleColumn: "name",
    jsonFields: ["metrics_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "manufacturer", label: "Manufacturer", kind: "string" },
      { column: "model", label: "Model", kind: "string" },
      { column: "os_family", label: "OS family", kind: "string" },
      {
        column: "frame_asset_id",
        label: "Frame asset ID",
        kind: "string",
        nullable: true,
      },
      { column: "metrics_json", label: "Device metrics", kind: "json" },
    ],
  },
  {
    id: "device_states",
    label: "Device States",
    table: "device_states",
    titleColumn: "name",
    jsonFields: ["state_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "device_id", label: "Device ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "state_json", label: "Device state", kind: "json" },
    ],
  },
  {
    id: "media_assets",
    label: "Media Assets",
    table: "media_assets",
    titleColumn: "name",
    jsonFields: ["dimensions_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "asset_type", label: "Type", kind: "string" },
      { column: "uri", label: "URI", kind: "string" },
      { column: "mime_type", label: "MIME type", kind: "string" },
      {
        column: "checksum",
        label: "Checksum",
        kind: "string",
        nullable: true,
      },
      { column: "dimensions_json", label: "Asset dimensions", kind: "json" },
      { column: "metadata_json", label: "Asset notes", kind: "json" },
    ],
  },
  {
    id: "palette_colors",
    label: "Palette Colors",
    table: "palette_colors",
    titleColumn: "token",
    jsonFields: ["metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "production_id",
        label: "Production ID",
        kind: "string",
        readonly: true,
      },
      { column: "token", label: "Token", kind: "string" },
      { column: "value_hex", label: "RGB / HEX", kind: "string" },
      { column: "metadata_json", label: "Palette notes", kind: "json" },
    ],
  },
  {
    id: "production_fonts",
    label: "Production Fonts",
    table: "production_fonts",
    titleColumn: "family",
    jsonFields: ["files_json", "metadata_json"],
    optionalScalars: ["source_path"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      {
        column: "production_id",
        label: "Production ID",
        kind: "string",
        readonly: true,
      },
      { column: "family", label: "Family", kind: "string" },
      { column: "files_json", label: "Font files", kind: "json" },
      {
        column: "source_path",
        label: "Source directory",
        kind: "string",
        nullable: true,
      },
      { column: "metadata_json", label: "Font notes", kind: "json" },
    ],
  },
  {
    id: "render_presets",
    label: "Render Presets",
    table: "render_presets",
    titleColumn: "name",
    jsonFields: ["codec_json", "color_json", "quality_json", "export_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "width", label: "Width", kind: "number" },
      { column: "height", label: "Height", kind: "number" },
      { column: "fps", label: "FPS", kind: "number" },
      { column: "format", label: "Format", kind: "string" },
      { column: "codec_json", label: "Codec settings", kind: "json" },
      { column: "color_json", label: "Color settings", kind: "json" },
      { column: "quality_json", label: "Quality settings", kind: "json" },
      { column: "export_json", label: "Export settings", kind: "json" },
    ],
  },
  {
    id: "apps",
    label: "Apps",
    table: "apps",
    titleColumn: "name",
    jsonFields: ["config_json", "metadata_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "bundle_key", label: "Bundle key", kind: "string" },
      { column: "app_type", label: "App type", kind: "string" },
      {
        column: "icon_asset_id",
        label: "Icon asset ID",
        kind: "string",
        nullable: true,
      },
      { column: "config_json", label: "App settings", kind: "json" },
      { column: "metadata_json", label: "App notes", kind: "json" },
    ],
  },
  {
    id: "animation_presets",
    label: "Animation Presets",
    table: "animation_presets",
    titleColumn: "name",
    jsonFields: ["parameters_json"],
    fields: [
      { column: "id", label: "ID", kind: "string", readonly: true },
      { column: "production_id", label: "Production ID", kind: "string" },
      { column: "name", label: "Name", kind: "string" },
      { column: "animation_type", label: "Animation type", kind: "string" },
      { column: "version", label: "Version", kind: "string" },
      { column: "parameters_json", label: "Animation parameters", kind: "json" },
    ],
  },
] satisfies AppTableDefinition[];

const PARSERS = {
  productions: ProductionSchema,
  episodes: EpisodeSchema,
  shots: ShotSchema,
  screen_instances: ScreenInstanceSchema,
  module_instances: ModuleInstanceSchema,
  actors: ActorSchema,
  icon_themes: IconThemeSchema,
  status_bars: StatusBarSchema,
  navigation_bars: NavigationBarSchema,
  themes: ThemeSchema,
  module_theme_configs: ModuleThemeConfigSchema,
  devices: DeviceSchema,
  device_states: DeviceStateSchema,
  media_assets: MediaAssetSchema,
  palette_colors: PaletteColorSchema,
  production_fonts: ProductionFontSchema,
  render_presets: RenderPresetSchema,
  apps: AppSchema,
  animation_presets: AnimationPresetSchema,
} as const;

function tableDefinition(tableId: string): AppTableDefinition {
  const definition = APP_TABLES.find((table) => table.id === tableId);
  if (!definition) {
    throw new Error(`Unknown table ${tableId}`);
  }
  return definition;
}

function decodeAppRow(row: Row, definition: AppTableDefinition): Row {
  const decoded = { ...row };
  for (const field of definition.jsonFields) {
    decoded[field] =
      definition.id === "screen_instances" &&
      ["data_ref_json", "transition_in_json", "transition_out_json"].includes(
        field,
      )
        ? readNullableJson(row, field)
        : readOptionalJson(row, field) ?? {};
  }
  return nullsToUndefined(decoded, definition.optionalScalars);
}

function encodeValue(
  value: unknown,
  field: AppFieldDefinition,
  definition: AppTableDefinition,
): string | number | null {
  if (field.kind === "json") {
    if (value === undefined || value === null) {
      return null;
    }
    return stringifyJsonObject(
      asObject(value, field.column),
      `${definition.table}.${field.column}`,
    );
  }
  if (field.kind === "number") {
    if (value === "" || value === null || value === undefined) {
      return field.nullable ? null : 0;
    }
    const numberValue = Number(value);
    if (!Number.isFinite(numberValue)) {
      throw new Error(`${field.label} must be a number`);
    }
    return numberValue;
  }
  if (value === "" || value === null || value === undefined) {
    return field.nullable ? null : "";
  }
  return String(value);
}

function validateAppRow(
  tableId: string,
  value: Row,
): Row {
  if (tableId === "screen_instances") {
    return ScreenInstanceSchema.parse(value) as Row;
  }
  const parser = PARSERS[tableId as keyof typeof PARSERS];
  if (!parser) {
    return value;
  }
  return parser.parse(value) as Row;
}

export function listDebugOptions(database: SQLiteDatabase) {
  const productions = database
    .prepare("SELECT id, name, slug, default_fps AS defaultFps FROM productions ORDER BY name, id")
    .all();
  const episodes = database
    .prepare(
      "SELECT id, production_id AS productionId, name, slug, sort_order AS sortOrder FROM episodes ORDER BY production_id, name COLLATE NOCASE, id",
    )
    .all();
  const shots = database
    .prepare(
      "SELECT id, production_id AS productionId, episode_id AS episodeId, owner_actor_id AS ownerActorId, name, slug, version, duration_frames AS durationFrames, fps FROM shots ORDER BY production_id, episode_id, name COLLATE NOCASE, id",
    )
    .all();
  const screenInstances = database
    .prepare(
      "SELECT id, shot_id AS shotId, app_id AS appId, screen_type AS screenType, module_id AS moduleId, start_frame AS startFrame, end_frame AS endFrame, layer_order AS layerOrder FROM screen_instances ORDER BY shot_id, layer_order, id",
    )
    .all();

  return { productions, episodes, shots, screenInstances };
}

export function loadDebugPayload(
  database: SQLiteDatabase,
  selection: DebugSelection,
) {
  const repository = new SQLiteRepository(database);
  const rawScreen = database
    .prepare("SELECT * FROM screen_instances WHERE id = ?")
    .get(selection.screenInstanceId) as Row | undefined;
  if (!rawScreen) {
    throw new Error(`Unknown screen instance ${selection.screenInstanceId}`);
  }
  const screenInstance = parseScreenInstance(rawScreen);
  if (screenInstance.shot_id !== selection.shotId) {
    throw new Error("Selected screen instance does not belong to selected shot");
  }
  const shot = repository.getShot(selection.shotId);
  const ownerActorId = shot?.owner_actor_id ?? screenInstance.owner_actor_id;
  const ownerActor = ownerActorId ? repository.getActor(ownerActorId) : undefined;
  const themeId = screenInstance.theme_id ?? ownerActor?.default_theme_id;
  const deviceId = screenInstance.device_id ?? ownerActor?.default_device_id;
  const theme = themeId ? repository.getTheme(themeId) : undefined;
  const device = deviceId ? repository.getDevice(deviceId) : undefined;
  const deviceState = screenInstance.device_state_json
    ? {
        id: `${screenInstance.id}:device_state`,
        production_id: shot?.production_id ?? selection.productionId,
        device_id: deviceId ?? "",
        name: "Screen instance device state",
        state_json: screenInstance.device_state_json,
      }
    : screenInstance.device_state_id
      ? repository.getDeviceState(screenInstance.device_state_id)
      : undefined;
  const moduleInstance =
    repository.getPrimaryModuleInstanceForScreenInstance(screenInstance.id);
  if (!theme || !device || !deviceState) {
    throw new Error("Selected screen instance requires theme, device and state");
  }
  const themeMode = getThemeMode(theme.tokens_json, screenInstance.theme_mode);

  const warnings: string[] = [];
  let resolvedScreen: unknown = null;
  let renderable: unknown = null;
  try {
    const resolvedShot = resolveShot({
      repository,
      productionId: selection.productionId,
      shotId: selection.shotId,
      shotFrame: selection.frame,
    });
    const selectedResolved = resolvedShot.active_screen_instances.find(
      (screen) => screen.screen_instance_id === selection.screenInstanceId,
    );
    if (!selectedResolved) {
      warnings.push(
        `Screen instance is inactive at frame ${selection.frame} (${screenInstance.start_frame}–${screenInstance.end_frame - 1}).`,
      );
    } else {
      resolvedScreen =
        selectedResolved.resolved_props ?? selectedResolved.resolved_context;
      if (
        selectedResolved.screen_type === "chat" &&
        selectedResolved.resolved_props
      ) {
        const rawRenderable = RenderableNodeSchema.parse(
          ChatScreenModule.render(selectedResolved.resolved_props),
        );
        renderable = RenderableNodeSchema.parse(
          rewriteRenderableMediaUrls(rawRenderable, selection.productionId),
        );
      } else {
        warnings.push(
          `No visual module renderer is implemented for ${selectedResolved.screen_type}.`,
        );
      }
    }
  } catch (error) {
    warnings.push(error instanceof Error ? error.message : String(error));
  }

  return {
    selection,
    previewContext: {
      deviceId: device.id,
      deviceName: device.name,
      themeId: theme.id,
      themeName: theme.name,
      themeMode,
      ownerActorId: ownerActor?.id ?? null,
      ownerActorName: ownerActor?.display_name ?? null,
    },
    screenInstance: {
      id: screenInstance.id,
      screenType: screenInstance.screen_type,
      moduleId: screenInstance.module_id,
      moduleSchemaVersion: screenInstance.module_schema_version,
      startFrame: screenInstance.start_frame,
      endFrame: screenInstance.end_frame,
    },
    editable: {
      moduleData: moduleInstance?.content_json ?? {},
      moduleConfig: moduleInstance?.behavior_json ?? {},
      themeTokens: theme.tokens_json,
      deviceMetrics: device.metrics_json,
      deviceState: deviceState.state_json,
    },
    resolvedScreen,
    renderable,
    warnings,
  };
}

export function saveDebugPayload(
  database: SQLiteDatabase,
  request: DebugSaveRequest,
) {
  const rawScreen = database
    .prepare("SELECT * FROM screen_instances WHERE id = ?")
    .get(request.screenInstanceId) as Row | undefined;
  if (!rawScreen) {
    throw new Error(`Unknown screen instance ${request.screenInstanceId}`);
  }
  const screenInstance = parseScreenInstance(rawScreen);
  const repository = new SQLiteRepository(database);
  const shot = repository.getShot(request.shotId);
  const ownerActorId = shot?.owner_actor_id ?? screenInstance.owner_actor_id;
  const ownerActor = ownerActorId ? repository.getActor(ownerActorId) : undefined;
  const themeId = screenInstance.theme_id ?? ownerActor?.default_theme_id;
  const deviceId = screenInstance.device_id ?? ownerActor?.default_device_id;
  if (!themeId || !deviceId) {
    throw new Error("Selected screen instance cannot resolve theme/device");
  }
  const currentDeviceState = screenInstance.device_state_json ?? {};
  const moduleInstance = repository.getPrimaryModuleInstanceForScreenInstance(
    screenInstance.id,
  );
  if (!moduleInstance) {
    throw new Error(
      `Selected screen instance ${screenInstance.id} has no module instance`,
    );
  }

  const moduleData =
    moduleInstance.module_id === "core.chat"
      ? ChatModuleDataSchema.parse(request.fields.moduleData)
      : asObject(request.fields.moduleData, "content_json");
  const moduleConfig =
    moduleInstance.module_id === "core.chat"
      ? ChatModuleConfigSchema.parse(request.fields.moduleConfig)
      : asObject(request.fields.moduleConfig, "behavior_json");
  const themeTokens = asObject(request.fields.themeTokens, "theme.tokens_json");
  const deviceMetrics = asObject(
    request.fields.deviceMetrics,
    "device.metrics_json",
  );
  const deviceState = asObject(
    request.fields.deviceState,
    "device_state.state_json",
  );

  const currentTheme = database
    .prepare("SELECT * FROM themes WHERE id = ?")
    .get(themeId) as Row;
  ThemeSchema.parse({
    ...nullsToUndefined(currentTheme, ["icon_theme_id", "status_bar_id"]),
    tokens_json: themeTokens,
  });
  const currentDevice = database
    .prepare("SELECT * FROM devices WHERE id = ?")
    .get(deviceId) as Row;
  DeviceSchema.parse({
    ...currentDevice,
    metrics_json: deviceMetrics,
  });
  asObject(currentDeviceState, "screen_instance.device_state_json");

  const save = database.transaction(() => {
    database
      .prepare(
        `UPDATE module_instances
         SET content_json = ?, behavior_json = ?
         WHERE id = ?`,
      )
      .run(
        stringifyJsonObject(moduleData, "content_json"),
        stringifyJsonObject(moduleConfig, "behavior_json"),
        moduleInstance.id,
      );
    database
      .prepare("UPDATE themes SET tokens_json = ? WHERE id = ?")
      .run(
        stringifyJsonObject(themeTokens, "theme.tokens_json"),
        themeId,
      );
    database
      .prepare("UPDATE devices SET metrics_json = ? WHERE id = ?")
      .run(
        stringifyJsonObject(deviceMetrics, "device.metrics_json"),
        deviceId,
      );
    database
      .prepare("UPDATE screen_instances SET device_state_json = ? WHERE id = ?")
      .run(
        stringifyJsonObject(deviceState, "screen_instance.device_state_json"),
        screenInstance.id,
      );
  });
  save();

  return loadDebugPayload(database, request);
}

export function listAppRecords(database: SQLiteDatabase) {
  return Object.fromEntries(
    APP_TABLES.map((definition) => {
      const rows = database
        .prepare(
          `SELECT * FROM ${definition.table} ORDER BY ${definition.titleColumn}, id`,
        )
        .all() as Row[];
      return [
        definition.id,
        rows.map((row) => decodeAppRow(row, definition)),
      ];
    }),
  );
}

function getThemeMode(themeTokens: Row, requestedMode: unknown): "light" | "dark" {
  if (requestedMode === "light" || requestedMode === "dark") {
    return requestedMode;
  }
  const defaultMode = themeTokens.defaultMode;
  return defaultMode === "dark" ? "dark" : "light";
}

function loadInheritedJson(database: SQLiteDatabase) {
  const inherited: Record<string, Record<string, Record<string, Row>>> = {};
  const setInherited = (
    tableId: string,
    recordId: string,
    field: string,
    value: Row,
  ) => {
    inherited[tableId] ??= {};
    inherited[tableId][recordId] ??= {};
    inherited[tableId][recordId][field] = value;
  };

  const themes = database
    .prepare("SELECT * FROM themes ORDER BY production_id, name, id")
    .all() as Row[];
  const themeById = new Map(
    themes.map((row) => {
      const theme = ThemeSchema.parse({
        ...nullsToUndefined(row, ["icon_theme_id", "status_bar_id"]),
        tokens_json: readRequiredJson(row, "tokens_json"),
      });
      return [theme.id, theme] as const;
    }),
  );
  const actorRows = database.prepare("SELECT * FROM actors").all() as Row[];
  const actorById = new Map(
    actorRows.map((row) => [
      String(row.id),
      ActorSchema.parse({
        ...nullsToUndefined(row, [
          "short_name",
          "avatar_asset_id",
          "default_device_id",
          "default_theme_id",
        ]),
        metadata_json: readOptionalJson(row, "metadata_json"),
      }),
    ]),
  );
  const shotRows = database.prepare("SELECT * FROM shots").all() as Row[];
  const shotById = new Map(
    shotRows.map((row) => [
      String(row.id),
      ShotSchema.parse({
        ...nullsToUndefined(row, [
          "episode_id",
          "owner_actor_id",
          "sort_order",
          "render_preset_id",
        ]),
        canvas_json: readOptionalJson(row, "canvas_json"),
        metadata_json: readOptionalJson(row, "metadata_json"),
      }),
    ]),
  );
  const appRows = database.prepare("SELECT * FROM apps").all() as Row[];
  const appById = new Map(
    appRows.map((row) => [
      String(row.id),
      AppSchema.parse({
        ...nullsToUndefined(row, ["icon_asset_id"]),
        config_json: readOptionalJson(row, "config_json"),
        metadata_json: readOptionalJson(row, "metadata_json"),
      }),
    ]),
  );
  const firstThemeByProductionId = new Map<string, JsonObject>();
  for (const theme of themeById.values()) {
    if (!firstThemeByProductionId.has(theme.production_id)) {
      firstThemeByProductionId.set(
        theme.production_id,
        resolveGlobalThemeTokens(
          theme,
          getThemeMode(theme.tokens_json, undefined),
        ),
      );
    }
  }
  for (const app of appById.values()) {
    const inheritedThemeTokens = firstThemeByProductionId.get(app.production_id);
    if (inheritedThemeTokens) {
      setInherited("apps", app.id, "config_json", inheritedThemeTokens);
    }
  }

  const moduleConfigs = database
    .prepare("SELECT * FROM module_theme_configs")
    .all() as Row[];
  for (const row of moduleConfigs) {
    const config = ModuleThemeConfigSchema.parse({
      ...row,
      tokens_json: readRequiredJson(row, "tokens_json"),
      metadata_json: readOptionalJson(row, "metadata_json") ?? {},
    });
    const theme = themeById.get(config.theme_id);
    const app = appById.get(config.app_id);
    if (!theme) continue;
    const themeMode = getThemeMode(theme.tokens_json, undefined);
    const appConfig = app?.config_json && isObject(app.config_json)
      ? app.config_json
      : {};
    const appTokens = isObject(appConfig.tokens_json)
      ? appConfig.tokens_json
      : appConfig;
    const genericTokens = mergeTokenObjects(
      resolveGlobalThemeTokens(theme, themeMode),
      resolveModuleThemeTokens(appTokens, themeMode),
    );
    const moduleDefaultsFromGenericTokens =
      moduleTypographyDefaultsFromFonts(genericTokens);
    const defaultTokens =
      config.metadata_json && isObject(config.metadata_json.default_tokens_json)
        ? config.metadata_json.default_tokens_json
        : config.tokens_json;
    setInherited(
      "module_theme_configs",
      config.id,
      "tokens_json",
      mergeTokenObjects(
        mergeTokenObjects(genericTokens, moduleDefaultsFromGenericTokens),
        defaultTokens,
      ),
    );
  }

  return inherited;
}

export function loadAppState(database: SQLiteDatabase) {
  return {
    tables: APP_TABLES,
    records: listAppRecords(database),
    options: listDebugOptions(database),
    inheritedJson: loadInheritedJson(database),
  };
}

export interface AppUpdateRequest {
  tableId: string;
  recordId: string;
  patch: Record<string, unknown>;
}

export interface AppCreateRequest {
  tableId:
    | "productions"
    | "episodes"
    | "shots"
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "palette_colors"
    | "production_fonts"
    | "render_presets";
  parent?: {
    productionId?: string;
    episodeId?: string;
  };
  name?: string;
  family?: "ios" | "android";
}

export interface AppRecordActionRequest {
  tableId:
    | "shots"
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "palette_colors"
    | "production_fonts"
    | "render_presets";
  recordId: string;
}

function slugifyIdPart(value: string): string {
  return (
    value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .slice(0, 40) || "new"
  );
}

function defaultThemeTokens(family: "ios" | "android") {
  const isAndroid = family === "android";
  const fontFamily = isAndroid ? "Roboto" : "SF Pro Text";
  const lightBackground = "white";
  const darkBackground = isAndroid ? "red" : "black";
  const lightText = isAndroid ? "red" : "black";
  const darkText = isAndroid ? "red" : "white";
  const lightSecondary = isAndroid ? "red" : "gray_medium";
  const darkSecondary = isAndroid ? "red" : "gray_medium_bright";
  const lightAccent = isAndroid ? "red" : "blue";
  const darkAccent = isAndroid ? "red" : "blue_bright";

  return {
    defaultMode: "light",
    modes: {
      light: {
        colors: {
          background: lightBackground,
          textPrimary: lightText,
          textSecondary: lightSecondary,
          accent: lightAccent,
        },
        statusBar: {
          type: isAndroid ? "android-default" : "ios-default",
          foreground: lightText,
          background: "rgba(255,255,255,0)",
        },
        navigationBar: {
          type: isAndroid ? "android-gesture" : "ios-home-indicator",
          foreground: lightText,
          background: "rgba(255,255,255,0)",
        },
        keyboard: {
          background: isAndroid ? "red" : "keyboard_light_background",
          keyBackground: "white",
          specialKeyBackground: isAndroid ? "red" : "keyboard_light_special",
          pressedKeyBackground: isAndroid ? "red" : "gray",
          popoverBackground: "white",
          text: lightText,
        },
        notifications: {
          background: isAndroid ? "rgba(255,255,255,0.94)" : "rgba(245,245,247,0.92)",
          titleColor: lightText,
          bodyColor: isAndroid ? "red" : "gray_deep",
        },
      },
      dark: {
        colors: {
          background: darkBackground,
          textPrimary: darkText,
          textSecondary: darkSecondary,
          accent: darkAccent,
        },
        statusBar: {
          type: isAndroid ? "android-default" : "ios-default",
          foreground: darkText,
          background: "rgba(0,0,0,0)",
        },
        navigationBar: {
          type: isAndroid ? "android-gesture" : "ios-home-indicator",
          foreground: darkText,
          background: "rgba(0,0,0,0)",
        },
        keyboard: {
          background: isAndroid ? "red" : "keyboard_dark_background",
          keyBackground: isAndroid ? "red" : "keyboard_dark_key",
          specialKeyBackground: isAndroid ? "red" : "gray_deep",
          pressedKeyBackground: isAndroid ? "red" : "gray",
          popoverBackground: isAndroid ? "red" : "keyboard_dark_key",
          text: darkText,
        },
        notifications: {
          background: isAndroid ? "rgba(250,0,0,0.94)" : "rgba(44,44,46,0.92)",
          titleColor: darkText,
          bodyColor: isAndroid ? "red" : "gray_soft",
        },
      },
    },
    fonts: {
      family: fontFamily,
      bodySize: 17,
      bodyLineHeight: 21.25,
      captionSize: 13,
      weight: "Regular",
      source: "installed_system_font",
    },
    colors: {
      background: lightBackground,
      textPrimary: lightText,
      textSecondary: lightSecondary,
      accent: lightAccent,
    },
    statusBar: {
      type: isAndroid ? "android-default" : "ios-default",
      foreground: lightText,
      background: "rgba(255,255,255,0)",
      iconScale: 1,
    },
    navigationBar: {
      type: isAndroid ? "android-gesture" : "ios-home-indicator",
      foreground: lightText,
      background: "rgba(255,255,255,0)",
      iconScale: 1,
    },
    keyboard: {
      background: isAndroid ? "red" : "keyboard_light_background",
      keyBackground: "white",
      specialKeyBackground: isAndroid ? "red" : "keyboard_light_special",
      pressedKeyBackground: isAndroid ? "red" : "gray",
      popoverBackground: "white",
      text: lightText,
    },
    notifications: {
      backdropBlur: isAndroid ? 16 : 24,
    },
    spacing: {
      xs: 4,
      sm: 8,
      md: 16,
      lg: 24,
    },
    radii: {
      notification: isAndroid ? 20 : 16,
      card: isAndroid ? 16 : 12,
    },
    shadows: {
      avatar: {
        color: "rgba(0,0,0,0.18)",
        offsetX: 0,
        offsetY: 4,
        blur: 18,
      },
      elevated: {
        color: "rgba(0,0,0,0.18)",
        offsetX: 0,
        offsetY: 4,
        blur: 18,
      },
    },
  };
}

function defaultIconThemeMapping() {
  return {
    tokens: {},
    notes: {
      source: "material-rounded-basic",
      usage:
        "Logical icon tokens resolve to SVG filenames in this production icon theme.",
    },
  };
}

function defaultStatusBarConfig() {
  return {
    schemaVersion: 2,
    layout: {
      height: 54,
      itemSize: 18,
      gap: 6,
      sidePadding: 24,
    },
    items: [
      {
        id: "time",
        label: "Time",
        kind: "text",
        value: "9:41",
        zone: "left",
        order: 10,
      },
      {
        id: "carrier",
        label: "Carrier",
        kind: "text",
        value: "",
        zone: "off",
        order: 20,
      },
      {
        id: "signal",
        label: "Signal",
        kind: "generatedSignal",
        value: 4,
        zone: "right",
        order: 10,
      },
      {
        id: "wifi",
        label: "Wi‑Fi",
        kind: "iconToken",
        token: "status_wifi",
        zone: "right",
        order: 20,
      },
      {
        id: "soundOff",
        label: "Sound Off",
        kind: "iconToken",
        token: "media_volume_off",
        zone: "off",
        order: 30,
      },
      {
        id: "bluetooth",
        label: "Bluetooth",
        kind: "iconToken",
        token: "status_bluetooth",
        zone: "off",
        order: 40,
      },
      {
        id: "battery",
        label: "Battery",
        kind: "generatedBattery",
        value: 85,
        charging: false,
        zone: "right",
        order: 50,
      },
    ],
  };
}

function defaultNavigationBarConfig() {
  return {
    schemaVersion: 1,
    layout: {
      height: 34,
      itemSize: 18,
      sidePadding: 40,
      strokeWidth: 2,
      cornerRadius: 3,
      filled: false,
    },
    items: [
      {
        id: "back",
        label: "Back",
        kind: "generatedBack",
        zone: "left",
        order: 10,
      },
      {
        id: "home",
        label: "Home",
        kind: "generatedHome",
        zone: "center",
        order: 10,
      },
      {
        id: "recents",
        label: "Recents",
        kind: "generatedRecents",
        zone: "right",
        order: 10,
      },
    ],
  };
}

function uniqueId(prefix: string, name: string): string {
  return `${prefix}_${slugifyIdPart(name)}_${Date.now().toString(36)}`;
}

function nextSortOrder(
  database: SQLiteDatabase,
  table: string,
  parentColumn: string,
  parentId: string,
): number {
  const row = database
    .prepare(
      `SELECT COALESCE(MAX(sort_order), -1) + 1 AS next_sort_order FROM ${table} WHERE ${parentColumn} = ?`,
    )
    .get(parentId) as { next_sort_order?: number } | undefined;
  return Number(row?.next_sort_order ?? 0);
}

function firstScalar(
  database: SQLiteDatabase,
  query: string,
  ...params: unknown[]
): string | null {
  const row = database.prepare(query).get(...params) as
    | Record<string, unknown>
    | undefined;
  const value = row ? Object.values(row)[0] : undefined;
  return typeof value === "string" ? value : null;
}

function defaultDeviceMetrics(width: number, height: number, scale = 3) {
  const designWidth = Math.round(width / scale);
  const designHeight = Math.round(height / scale);
  const statusBarHeight = Math.round(height * 0.063);
  const safeBottom = Math.round(height * 0.036);
  return {
    designSpace: {
      width: designWidth,
      height: designHeight,
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
      bottom: safeBottom,
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

function defaultRenderPresetPayload(format = "mov", codec = "prores_422_hq") {
  const isImage = format === "image";
  const hasAlpha =
    codec === "prores_4444" ||
    codec === "prores_4444_alpha" ||
    codec === "png" ||
    codec === "exr";
  return {
    codec_json: { codec },
    color_json: {
      colorSpace: codec === "exr" ? "linear" : isImage ? "srgb" : "rec709",
      alpha: hasAlpha,
    },
    quality_json: { profile: codec },
    export_json: {
      extension: isImage ? codec : "mov",
      sequence: isImage,
      ffmpegArgs: ffmpegArgsForRenderPreset(format, codec),
    },
  };
}

function ffmpegArgsForRenderPreset(format: string, codec: string) {
  if (format === "image") {
    if (codec === "exr") return "-compression zip -pix_fmt rgba64le";
    return "-compression_level 6 -pix_fmt rgba";
  }
  if (codec === "prores_422_proxy") return "-c:v prores_ks -profile:v 0 -pix_fmt yuv422p10le";
  if (codec === "prores_422_lt") return "-c:v prores_ks -profile:v 1 -pix_fmt yuv422p10le";
  if (codec === "prores_422") return "-c:v prores_ks -profile:v 2 -pix_fmt yuv422p10le";
  if (codec === "prores_422_hq") return "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le";
  if (codec === "prores_4444" || codec === "prores_4444_alpha") {
    return "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le";
  }
  if (codec === "prores_4444_xq") return "-c:v prores_ks -profile:v 5 -pix_fmt yuva444p10le";
  if (codec === "h264_low") return "-c:v libx264 -preset medium -crf 28 -pix_fmt yuv420p";
  if (codec === "h264_medium") return "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p";
  if (codec === "h264_high") return "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p";
  return "";
}

export function createAppRecord(
  database: SQLiteDatabase,
  request: AppCreateRequest,
) {
  if (request.tableId === "productions") {
    const name = request.name?.trim() || "New production";
    const id = uniqueId("production", name);
    database
      .prepare(
        `INSERT INTO productions (id, name, slug, default_fps, settings_json, metadata_json)
         VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(id, name, slugifyIdPart(name), 30, stringifyJsonObject({ mediaRoot: "" }, "productions.settings_json"), "{}");
    const record = decodeAppRow(
      database.prepare("SELECT * FROM productions WHERE id = ?").get(id) as Row,
      tableDefinition("productions"),
    );
    return { tableId: "productions", record, state: loadAppState(database) };
  }

  if (request.tableId === "episodes") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating an episode requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const sortOrder = nextSortOrder(
      database,
      "episodes",
      "production_id",
      productionId,
    );
    const name = request.name?.trim() || `Episode ${sortOrder + 1}`;
    const id = uniqueId("episode", name);
    const slug = slugifyIdPart(name);
    database
      .prepare(
        `INSERT INTO episodes (id, production_id, name, slug, sort_order, metadata_json)
         VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(id, productionId, name, slug, sortOrder, "{}");
    const record = decodeAppRow(
      database.prepare("SELECT * FROM episodes WHERE id = ?").get(id) as Row,
      tableDefinition("episodes"),
    );
    return { tableId: "episodes", record, state: loadAppState(database) };
  }

  if (request.tableId === "themes") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a theme requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const family = request.family === "android" ? "android" : "ios";
    const name = request.name?.trim() || `${family === "ios" ? "iOS" : "Android"} Theme`;
    const id = uniqueId("theme", name);
    database
      .prepare(
        `INSERT INTO themes (id, production_id, name, family, version, tokens_json)
         VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        family,
        "1.0.0",
        stringifyJsonObject(defaultThemeTokens(family), "theme.tokens_json"),
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM themes WHERE id = ?").get(id) as Row,
      tableDefinition("themes"),
    );
    return { tableId: "themes", record, state: loadAppState(database) };
  }

  if (request.tableId === "icon_themes") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating an icon theme requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const name = request.name?.trim() || "Material Rounded Basic";
    const id = uniqueId("icon_theme", name);
    database
      .prepare(
        `INSERT INTO icon_themes (
          id,
          production_id,
          name,
          family,
          asset_root,
          mapping_json,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        "material-rounded",
        "ASSETS/icon-themes/material-rounded-basic",
        stringifyJsonObject(defaultIconThemeMapping(), "icon_themes.mapping_json"),
        "{}",
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM icon_themes WHERE id = ?").get(id) as Row,
      tableDefinition("icon_themes"),
    );
    return { tableId: "icon_themes", record, state: loadAppState(database) };
  }

  if (request.tableId === "status_bars") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a status bar requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const name = request.name?.trim() || "New Status Bar";
    const id = uniqueId("status_bar", name);
    database
      .prepare(
        `INSERT INTO status_bars (
          id,
          production_id,
          name,
          family,
          config_json,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        "ios",
        stringifyJsonObject(defaultStatusBarConfig(), "status_bars.config_json"),
        "{}",
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM status_bars WHERE id = ?").get(id) as Row,
      tableDefinition("status_bars"),
    );
    return { tableId: "status_bars", record, state: loadAppState(database) };
  }

  if (request.tableId === "navigation_bars") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a navigation bar requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const name = request.name?.trim() || "New Navigation Bar";
    const id = uniqueId("navigation_bar", name);
    database
      .prepare(
        `INSERT INTO navigation_bars (
          id,
          production_id,
          name,
          family,
          config_json,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        "ios",
        stringifyJsonObject(
          defaultNavigationBarConfig(),
          "navigation_bars.config_json",
        ),
        "{}",
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM navigation_bars WHERE id = ?").get(id) as Row,
      tableDefinition("navigation_bars"),
    );
    return { tableId: "navigation_bars", record, state: loadAppState(database) };
  }

  if (request.tableId === "devices") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a device requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const name = request.name?.trim() || "New device";
    const id = uniqueId("device", name);
    database
      .prepare(
        `INSERT INTO devices (id, production_id, name, manufacturer, model, os_family, metrics_json, frame_asset_id)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        "Custom",
        name,
        "custom",
        stringifyJsonObject(defaultDeviceMetrics(1080, 2340), "devices.metrics_json"),
        null,
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM devices WHERE id = ?").get(id) as Row,
      tableDefinition("devices"),
    );
    return { tableId: "devices", record, state: loadAppState(database) };
  }

  if (request.tableId === "production_fonts") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a production font requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const family = request.name?.trim() || "New Font";
    const id = uniqueId("font", family);
    database
      .prepare(
        `INSERT INTO production_fonts (
          id,
          production_id,
          family,
          files_json,
          source_path,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(id, productionId, family, '{"files":[]}', null, "{}");
    const record = decodeAppRow(
      database.prepare("SELECT * FROM production_fonts WHERE id = ?").get(id) as Row,
      tableDefinition("production_fonts"),
    );
    return { tableId: "production_fonts", record, state: loadAppState(database) };
  }

  if (request.tableId === "palette_colors") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a palette color requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id FROM productions WHERE id = ?")
      .get(productionId);
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const token = slugifyIdPart(request.name?.trim() || "new_color").replace(
      /-/g,
      "_",
    );
    const id = uniqueId("palette", token);
    database
      .prepare(
        `INSERT INTO palette_colors (
          id,
          production_id,
          token,
          value_hex,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        token,
        "#FFFFFF",
        stringifyJsonObject(
          { note: "Production palette primitive color." },
          "palette_colors.metadata_json",
        ),
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM palette_colors WHERE id = ?").get(id) as Row,
      tableDefinition("palette_colors"),
    );
    return { tableId: "palette_colors", record, state: loadAppState(database) };
  }

  if (request.tableId === "render_presets") {
    const productionId = request.parent?.productionId;
    if (!productionId) {
      throw new Error("Creating a render preset requires a productionId parent.");
    }
    const production = database
      .prepare("SELECT id, default_fps FROM productions WHERE id = ?")
      .get(productionId) as Row | undefined;
    if (!production) {
      throw new Error(`Production ${productionId} not found`);
    }
    const name = request.name?.trim() || "New Render Preset";
    const id = uniqueId("render_preset", name);
    const payload = defaultRenderPresetPayload();
    database
      .prepare(
        `INSERT INTO render_presets (
          id,
          production_id,
          name,
          width,
          height,
          fps,
          format,
          codec_json,
          color_json,
          quality_json,
          export_json
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        name,
        1,
        1,
        1,
        "mov",
        stringifyJsonObject(payload.codec_json, "render_presets.codec_json"),
        stringifyJsonObject(payload.color_json, "render_presets.color_json"),
        stringifyJsonObject(payload.quality_json, "render_presets.quality_json"),
        stringifyJsonObject(payload.export_json, "render_presets.export_json"),
      );
    const record = decodeAppRow(
      database.prepare("SELECT * FROM render_presets WHERE id = ?").get(id) as Row,
      tableDefinition("render_presets"),
    );
    return { tableId: "render_presets", record, state: loadAppState(database) };
  }

  const episodeId = request.parent?.episodeId;
  if (!episodeId) {
    throw new Error("Creating a shot requires an episodeId parent.");
  }
  const episode = database
    .prepare("SELECT id, production_id FROM episodes WHERE id = ?")
    .get(episodeId) as Row | undefined;
  if (!episode) {
    throw new Error(`Episode ${episodeId} not found`);
  }
  const productionId = String(episode.production_id);
  const production = database
    .prepare("SELECT default_fps FROM productions WHERE id = ?")
    .get(productionId) as { default_fps?: number } | undefined;
  const sortOrder = nextSortOrder(database, "shots", "episode_id", episodeId);
  const name = request.name?.trim() || `Shot ${sortOrder + 1}`;
  const id = uniqueId("shot", name);
  const slug = slugifyIdPart(name);
  const renderPresetId = firstScalar(
    database,
    "SELECT id FROM render_presets WHERE production_id = ? ORDER BY name, id LIMIT 1",
    productionId,
  );
  const ownerActorId = firstScalar(
    database,
    "SELECT id FROM actors WHERE production_id = ? ORDER BY display_name, id LIMIT 1",
    productionId,
  );
  const fpsRow = renderPresetId
    ? (database
        .prepare("SELECT fps FROM render_presets WHERE id = ?")
        .get(renderPresetId) as { fps?: number } | undefined)
    : undefined;
  const fps = Number(fpsRow?.fps ?? production?.default_fps ?? 30);
  database
    .prepare(
      `INSERT INTO shots (
        id,
        production_id,
        episode_id,
        owner_actor_id,
        name,
        slug,
        version,
        sort_order,
        duration_frames,
        fps,
        render_preset_id,
        canvas_json,
        metadata_json
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
    )
    .run(
      id,
      productionId,
      episodeId,
      ownerActorId,
      name,
      slug,
      1,
      sortOrder,
      fps * 4,
      fps,
      renderPresetId,
      "{}",
      "{}",
    );
  const record = decodeAppRow(
    database.prepare("SELECT * FROM shots WHERE id = ?").get(id) as Row,
    tableDefinition("shots"),
  );
  return { tableId: "shots", record, state: loadAppState(database) };
}

function copyName(value: unknown) {
  const name = typeof value === "string" && value.trim() ? value.trim() : "Copy";
  return `${name} Copy`;
}

function duplicateSimpleRecord(
  database: SQLiteDatabase,
  tableId:
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "palette_colors"
    | "production_fonts"
    | "render_presets",
  recordId: string,
) {
  const definition = tableDefinition(tableId);
  const existing = database
    .prepare(`SELECT * FROM ${definition.table} WHERE id = ?`)
    .get(recordId) as Row | undefined;
  if (!existing) {
    throw new Error(`Record ${recordId} not found in ${tableId}`);
  }
  const nameSource =
    tableId === "production_fonts"
      ? existing.family
      : tableId === "palette_colors"
        ? existing.token
        : existing.name;
  const name = copyName(nameSource);
  const id = uniqueId(
    tableId === "icon_themes"
      ? "icon_theme"
      : tableId === "status_bars"
        ? "status_bar"
      : tableId === "navigation_bars"
        ? "navigation_bar"
      : tableId === "themes"
      ? "theme"
      : tableId === "devices"
        ? "device"
      : tableId === "palette_colors"
        ? "palette"
      : tableId === "production_fonts"
        ? "font"
        : "render_preset",
    name,
  );
  const columns = Object.keys(existing);
  const next: Row = {
    ...existing,
    id,
    ...(tableId === "production_fonts"
      ? { family: name }
      : tableId === "palette_colors"
        ? { token: slugifyIdPart(name).replace(/-/g, "_") }
        : { name }),
  };
  database
    .prepare(
      `INSERT INTO ${definition.table} (${columns.join(", ")})
       VALUES (${columns.map(() => "?").join(", ")})`,
    )
    .run(...columns.map((column) => next[column]));
  return decodeAppRow(
    database.prepare(`SELECT * FROM ${definition.table} WHERE id = ?`).get(id) as Row,
    definition,
  );
}

function duplicateShotRecord(database: SQLiteDatabase, recordId: string) {
  return database.transaction(() => {
    const existing = database
      .prepare("SELECT * FROM shots WHERE id = ?")
      .get(recordId) as Row | undefined;
    if (!existing) {
      throw new Error(`Record ${recordId} not found in shots`);
    }
    const episodeId = String(existing.episode_id ?? "");
    const productionId = String(existing.production_id);
    const name = copyName(existing.name);
    const id = uniqueId("shot", name);
    const sortOrder = episodeId
      ? nextSortOrder(database, "shots", "episode_id", episodeId)
      : Number(existing.sort_order ?? 0) + 1;
    database
      .prepare(
        `INSERT INTO shots (
          id,
          production_id,
          episode_id,
          owner_actor_id,
          name,
          slug,
          version,
          sort_order,
          duration_frames,
          fps,
          render_preset_id,
          canvas_json,
          metadata_json
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      )
      .run(
        id,
        productionId,
        existing.episode_id ?? null,
        existing.owner_actor_id ?? null,
        name,
        `${slugifyIdPart(String(existing.slug ?? existing.name ?? "shot"))}_copy`,
        existing.version ?? 1,
        sortOrder,
        existing.duration_frames,
        existing.fps,
        existing.render_preset_id ?? null,
        existing.canvas_json ?? "{}",
        existing.metadata_json ?? "{}",
      );

    const screenIdMap = new Map<string, string>();
    const screens = database
      .prepare("SELECT * FROM screen_instances WHERE shot_id = ? ORDER BY layer_order, id")
      .all(recordId) as Row[];
    for (const screen of screens) {
      const oldScreenId = String(screen.id);
      const newScreenId = uniqueId("screen", `${id}_${oldScreenId}`);
      screenIdMap.set(oldScreenId, newScreenId);
      database
        .prepare(
          `INSERT INTO screen_instances (
            id,
            shot_id,
            app_id,
            screen_type,
            module_id,
            module_schema_version,
            owner_actor_id,
            device_id,
            device_state_id,
            device_state_json,
            theme_id,
            theme_mode,
            start_frame,
            end_frame,
            layer_order,
            data_ref_json,
            module_data_json,
            module_config_json,
            module_tokens_override_json,
            transform_json,
            props_json,
            transition_in_json,
            transition_out_json
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        )
        .run(
          newScreenId,
          id,
          screen.app_id,
          screen.screen_type,
          screen.module_id ?? null,
          screen.module_schema_version ?? null,
          screen.owner_actor_id,
          screen.device_id ?? null,
          screen.device_state_id ?? null,
          screen.device_state_json ?? null,
          screen.theme_id ?? null,
          screen.theme_mode ?? null,
          screen.start_frame,
          screen.end_frame,
          screen.layer_order,
          screen.data_ref_json ?? null,
          screen.module_data_json ?? null,
          screen.module_config_json ?? null,
          screen.module_tokens_override_json ?? null,
          screen.transform_json ?? "{}",
          screen.props_json ?? "{}",
          screen.transition_in_json ?? null,
          screen.transition_out_json ?? null,
        );
    }

    for (const [oldScreenId, newScreenId] of screenIdMap) {
      const modules = database
        .prepare("SELECT * FROM module_instances WHERE screen_instance_id = ? ORDER BY sort_order, id")
        .all(oldScreenId) as Row[];
      for (const moduleInstance of modules) {
        database
          .prepare(
            `INSERT INTO module_instances (
              id,
              screen_instance_id,
              module_id,
              module_schema_version,
              sort_order,
              content_json,
              behavior_json,
              animation_json,
              metadata_json
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
          )
          .run(
            uniqueId("module_instance", `${newScreenId}_${moduleInstance.id}`),
            newScreenId,
            moduleInstance.module_id,
            moduleInstance.module_schema_version,
            moduleInstance.sort_order ?? null,
            moduleInstance.content_json,
            moduleInstance.behavior_json,
            moduleInstance.animation_json ?? '{"schemaVersion":1,"tracks":[]}',
            moduleInstance.metadata_json ?? null,
          );
      }

      const events = database
        .prepare("SELECT * FROM screen_events WHERE screen_instance_id = ? ORDER BY start_frame, id")
        .all(oldScreenId) as Row[];
      for (const event of events) {
        database
          .prepare(
            `INSERT INTO screen_events (
              id,
              screen_instance_id,
              event_type,
              start_frame,
              duration_frames,
              target_id,
              animation_preset_id,
              payload_json
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
          )
          .run(
            uniqueId("screen_event", `${newScreenId}_${event.id}`),
            newScreenId,
            event.event_type,
            event.start_frame,
            event.duration_frames,
            event.target_id ?? null,
            event.animation_preset_id ?? null,
            event.payload_json,
          );
      }
    }

    return decodeAppRow(
      database.prepare("SELECT * FROM shots WHERE id = ?").get(id) as Row,
      tableDefinition("shots"),
    );
  })();
}

export function duplicateAppRecord(
  database: SQLiteDatabase,
  request: AppRecordActionRequest,
) {
  const record =
    request.tableId === "shots"
      ? duplicateShotRecord(database, request.recordId)
      : duplicateSimpleRecord(database, request.tableId, request.recordId);
  return { tableId: request.tableId, record, state: loadAppState(database) };
}

export function deleteAppRecord(
  database: SQLiteDatabase,
  request: AppRecordActionRequest,
) {
  const definition = tableDefinition(request.tableId);
  const existing = database
    .prepare(`SELECT id FROM ${definition.table} WHERE id = ?`)
    .get(request.recordId);
  if (!existing) {
    throw new Error(`Record ${request.recordId} not found in ${request.tableId}`);
  }
  database
    .prepare(`DELETE FROM ${definition.table} WHERE id = ?`)
    .run(request.recordId);
  return {
    tableId: request.tableId,
    deletedRecordId: request.recordId,
    state: loadAppState(database),
  };
}

export function updateAppRecord(
  database: SQLiteDatabase,
  request: AppUpdateRequest,
) {
  const definition = tableDefinition(request.tableId);
  const existingRow = database
    .prepare(`SELECT * FROM ${definition.table} WHERE id = ?`)
    .get(request.recordId) as Row | undefined;
  if (!existingRow) {
    throw new Error(`Record ${request.recordId} not found in ${definition.id}`);
  }
  const existing = decodeAppRow(existingRow, definition);
  const allowed = new Map(
    definition.fields
      .filter((field) => !field.readonly)
      .map((field) => [field.column, field]),
  );
  const encoded: Record<string, string | number | null> = {};
  const decodedPatch: Row = {};
  for (const [column, value] of Object.entries(request.patch)) {
    const field = allowed.get(column);
    if (!field) {
      throw new Error(`Field ${column} is not editable on ${definition.id}`);
    }
    encoded[column] = encodeValue(value, field, definition);
    decodedPatch[column] =
      field.kind === "json"
        ? asObject(value, field.column)
        : encoded[column];
  }

  if (Object.keys(encoded).length === 0) {
    return {
      record: existing,
      records: listAppRecords(database)[definition.id],
    };
  }

  const candidate = validateAppRow(definition.id, {
    ...existing,
    ...decodedPatch,
  });
  const assignments = Object.keys(encoded)
    .map((column) => `${column} = ?`)
    .join(", ");
  database
    .prepare(`UPDATE ${definition.table} SET ${assignments} WHERE id = ?`)
    .run(...Object.values(encoded), request.recordId);

  if (
    definition.id === "screen_instances" &&
    (Object.hasOwn(encoded, "start_frame") ||
      Object.hasOwn(encoded, "end_frame"))
  ) {
    const screenRows = database
      .prepare(
        `SELECT id FROM screen_instances
         WHERE shot_id = ?
         ORDER BY start_frame, end_frame, id`,
      )
      .all(String(existing.shot_id)) as { id: string }[];
    const updateLayer = database.prepare(
      "UPDATE screen_instances SET layer_order = ? WHERE id = ?",
    );
    screenRows.forEach((row, index) => updateLayer.run(index, row.id));
  }

  const savedRow = database
    .prepare(`SELECT * FROM ${definition.table} WHERE id = ?`)
    .get(request.recordId) as Row;
  const saved = decodeAppRow(savedRow, definition);
  validateAppRow(definition.id, saved);

  return {
    record: candidate,
    saved,
    records: listAppRecords(database)[definition.id],
  };
}
