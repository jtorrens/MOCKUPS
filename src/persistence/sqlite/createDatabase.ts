import { mkdirSync, readFileSync } from "node:fs";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import Database from "better-sqlite3";
import {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
} from "../../domain/fonts/productionFontNormalization.js";
import { developmentDatabasePath } from "./paths.js";

const schemaPath = fileURLToPath(new URL("./schema.sql", import.meta.url));

export type SQLiteDatabase = Database.Database;

const SEED_PALETTE_COLORS = [
  ["debug_red", "#FF00FF"],
  ["gray_000", "#000000"],
  ["gray_010", "#1A1A1A"],
  ["gray_020", "#333333"],
  ["gray_030", "#4D4D4D"],
  ["gray_040", "#666666"],
  ["gray_050", "#808080"],
  ["gray_060", "#999999"],
  ["gray_070", "#B3B3B3"],
  ["gray_080", "#CCCCCC"],
  ["gray_090", "#E6E6E6"],
  ["gray_100", "#FFFFFF"],
  ["red", "#FA0000"],
  ["blue", "#007AFF"],
  ["blue_bright", "#0A84FF"],
  ["pastel_coral", "#FF8A80"],
  ["pastel_orange", "#FFB74D"],
  ["pastel_yellow", "#FFF176"],
  ["pastel_mint", "#66D9A3"],
  ["pastel_sky", "#64B5F6"],
  ["pastel_lavender", "#B39DDB"],
  ["purple", "#6750A4"],
  ["purple_tint", "#D0BCFF"],
] as const;

function isNeutralHex(valueHex: string) {
  const { red, green, blue } = hexChannels(valueHex);
  return red === green && green === blue;
}

const THEME_HEX_TO_PALETTE_TOKEN = new Map(
  SEED_PALETTE_COLORS.map(([token, valueHex]) => [
    valueHex.toUpperCase(),
    token,
  ]),
);
function hexChannels(value: string) {
  const normalized = value.replace("#", "").toUpperCase();
  return {
    red: Number.parseInt(normalized.slice(0, 2), 16),
    green: Number.parseInt(normalized.slice(2, 4), 16),
    blue: Number.parseInt(normalized.slice(4, 6), 16),
  };
}

function closestPaletteTokenForHex(value: string) {
  const normalized = value.toUpperCase();
  const exact = THEME_HEX_TO_PALETTE_TOKEN.get(normalized);
  if (exact) return exact;
  const target = hexChannels(normalized);
  let bestToken = "red";
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const [token, valueHex] of SEED_PALETTE_COLORS) {
    const candidate = hexChannels(valueHex);
    const distance =
      (target.red - candidate.red) ** 2 +
      (target.green - candidate.green) ** 2 +
      (target.blue - candidate.blue) ** 2;
    if (distance < bestDistance) {
      bestDistance = distance;
      bestToken = token;
    }
  }
  return bestToken;
}

function themeColorTokenForValue(value: string) {
  return closestPaletteTokenForHex(value);
}

function convertThemeColorsToPaletteTokens(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((entry) => convertThemeColorsToPaletteTokens(entry));
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [
        key,
        convertThemeColorsToPaletteTokens(entry),
      ]),
    );
  }
  if (typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value)) {
    return themeColorTokenForValue(value);
  }
  return value;
}

function componentToHex(value: number) {
  return Math.max(0, Math.min(255, Math.round(value)))
    .toString(16)
    .padStart(2, "0")
    .toUpperCase();
}

function normalizeRgbLikeColorToPaletteToken(value: string) {
  const match =
    /^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*([0-9.]+))?\s*\)$/i.exec(
      value.trim(),
    );
  if (!match) return value;
  const [, r, g, b, alpha] = match;
  const hex = `#${componentToHex(Number(r))}${componentToHex(Number(g))}${componentToHex(Number(b))}`;
  const color = closestPaletteTokenForHex(hex);
  if (alpha === undefined) return color;
  return { color, alpha: Math.max(0, Math.min(1, Number(alpha))) };
}

function normalizePhysicalColorsToPalette(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((entry) => normalizePhysicalColorsToPalette(entry));
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [
        key,
        normalizePhysicalColorsToPalette(entry),
      ]),
    );
  }
  if (typeof value !== "string") return value;
  if (/^#[0-9a-fA-F]{6}$/.test(value)) {
    return closestPaletteTokenForHex(value);
  }
  if (/^rgba?\(/i.test(value.trim())) {
    return normalizeRgbLikeColorToPaletteToken(value);
  }
  return value;
}

function modeForPath(path: string[]) {
  return path.includes("dark") ? "dark" : "light";
}

function cleanupDebugRedDefault(path: string[], value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((entry, index) =>
      cleanupDebugRedDefault([...path, String(index)], entry),
    );
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [
        key,
        cleanupDebugRedDefault([...path, key], entry),
      ]),
    );
  }
  if (value !== "red") return value;

  const joined = path.join(".");
  const mode = modeForPath(path);
  const dark = mode === "dark";

  if (joined.endsWith("wallpaper.color")) return dark ? "gray_000" : "gray_100";
  if (joined.endsWith("notifications.background.color")) {
    return dark ? "gray_020" : "gray_090";
  }
  if (joined.endsWith("header.background")) return "colors.background";
  if (joined.endsWith("header.separatorColor")) return "borders.primary";
  if (joined.endsWith("chatBubbles.outgoingBackground")) {
    return dark ? "blue_bright" : "blue";
  }
  if (joined.endsWith("chatBubbles.incomingBackground")) {
    return dark ? "gray_020" : "gray_100";
  }
  if (joined.endsWith("chatBubbles.status.textColor")) {
    return dark ? "gray_060" : "gray_050";
  }
  if (joined.endsWith("chatBubbles.status.sentColor")) {
    return dark ? "gray_060" : "gray_050";
  }
  if (joined.endsWith("chatBubbles.status.deliveredColor")) {
    return dark ? "gray_060" : "gray_050";
  }
  if (joined.endsWith("chatBubbles.status.readColor")) {
    return dark ? "blue_bright" : "blue";
  }

  return value;
}

const SCREEN_INSTANCE_V2_COLUMNS = {
  module_id: "TEXT",
  module_schema_version: "INTEGER",
  theme_mode: "TEXT",
  module_data_json: "TEXT",
  module_config_json: "TEXT",
  module_tokens_override_json: "TEXT",
} as const;

const COLOR_NORMALIZATION_JSON_COLUMNS = [
  ["productions", "settings_json"],
  ["productions", "metadata_json"],
  ["palette_colors", "metadata_json"],
  ["production_fonts", "files_json"],
  ["production_fonts", "metadata_json"],
  ["icon_themes", "mapping_json"],
  ["icon_themes", "metadata_json"],
  ["status_bars", "config_json"],
  ["status_bars", "metadata_json"],
  ["navigation_bars", "config_json"],
  ["navigation_bars", "metadata_json"],
  ["component_classes", "tokens_json"],
  ["component_classes", "metadata_json"],
  ["themes", "tokens_json"],
  ["module_theme_configs", "tokens_json"],
  ["module_theme_configs", "metadata_json"],
  ["devices", "metrics_json"],
  ["device_states", "state_json"],
  ["actors", "metadata_json"],
  ["apps", "config_json"],
  ["apps", "metadata_json"],
  ["animation_presets", "parameters_json"],
  ["render_presets", "codec_json"],
  ["render_presets", "color_json"],
  ["render_presets", "quality_json"],
  ["render_presets", "export_json"],
  ["shots", "canvas_json"],
  ["shots", "metadata_json"],
  ["conversations", "metadata_json"],
  ["conversation_participants", "metadata_json"],
  ["messages", "style_override_json"],
  ["messages", "animation_override_json"],
  ["messages", "layout_override_json"],
  ["messages", "metadata_json"],
  ["notifications", "payload_json"],
  ["notifications", "style_override_json"],
  ["notifications", "metadata_json"],
  ["calls", "metadata_json"],
  ["data_sources", "config_json"],
  ["data_sources", "metadata_json"],
  ["screen_instances", "data_ref_json"],
  ["screen_instances", "device_state_json"],
  ["screen_instances", "module_data_json"],
  ["screen_instances", "module_config_json"],
  ["screen_instances", "module_tokens_override_json"],
  ["screen_instances", "transform_json"],
  ["screen_instances", "props_json"],
  ["screen_instances", "transition_in_json"],
  ["screen_instances", "transition_out_json"],
  ["module_instances", "content_json"],
  ["module_instances", "behavior_json"],
  ["module_instances", "animation_json"],
  ["module_instances", "metadata_json"],
] as const;

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

function applyAdditiveV13Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS navigation_bars (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      name TEXT NOT NULL,
      family TEXT NOT NULL,
      config_json TEXT NOT NULL,
      metadata_json TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_navigation_bars_production
      ON navigation_bars(production_id, name);
  `);

  const themeColumns = new Set(
    (
      database.pragma("table_info(themes)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!themeColumns.has("navigation_bar_id")) {
    database.exec(
      "ALTER TABLE themes ADD COLUMN navigation_bar_id TEXT REFERENCES navigation_bars(id) ON DELETE SET NULL",
    );
  }
  database.pragma("user_version = 13");
}

function defaultTextInputBarConfig(): Record<string, unknown> {
  return {
    placeholder: "Mensaje",
    cursorVisible: true,
    iconSets: {
      left: {
        idle: [
          { token: "chat_emoji", order: 10 },
          { token: "chat_attach", order: 20 },
        ],
        typing: [{ token: "chat_emoji", order: 10 }],
      },
      right: {
        idle: [
          { token: "media_camera", order: 10 },
          { token: "media_mic", order: 20 },
        ],
        typing: [{ token: "chat_send", order: 10, color: "icons.accent" }],
      },
    },
  };
}

function defaultKeyboardConfig(): Record<string, unknown> {
  return {
    language: "es",
    bottomItems: [
      {
        id: "app_language",
        label: "app_language",
        kind: "iconToken",
        token: "app_language",
        zone: "left",
        order: 10,
      },
      {
        id: "media_mic",
        label: "media_mic",
        kind: "iconToken",
        token: "media_mic",
        zone: "right",
        order: 10,
      },
    ],
  };
}

function cleanTextInputBarConfig(value: unknown): Record<string, unknown> {
  const config =
    value && typeof value === "object" && !Array.isArray(value)
      ? { ...(value as Record<string, unknown>) }
      : {};
  delete config.text;
  delete config.state;
  return config;
}

function cleanKeyboardConfig(value: unknown): Record<string, unknown> {
  const config =
    value && typeof value === "object" && !Array.isArray(value)
      ? { ...(value as Record<string, unknown>) }
      : {};
  delete config.mode;
  delete config.pressedKey;
  return config;
}

function applyAdditiveV14Migration(database: SQLiteDatabase): void {
  const legacy = database
    .prepare(
      `SELECT behavior_json
       FROM module_instances
       WHERE module_id = 'core.chat'
       ORDER BY id
       LIMIT 1`,
    )
    .get() as { behavior_json?: string } | undefined;
  const legacyBehavior = (() => {
    try {
      return legacy?.behavior_json
        ? (JSON.parse(legacy.behavior_json) as Record<string, unknown>)
        : {};
    } catch {
      return {};
    }
  })();
  const fallbackTextInputBar = {
    ...defaultTextInputBarConfig(),
    ...cleanTextInputBarConfig(legacyBehavior.textInputBar),
  };
  const fallbackKeyboard = {
    ...defaultKeyboardConfig(),
    ...cleanKeyboardConfig(legacyBehavior.keyboard),
  };

  const rows = database
    .prepare(
      `SELECT id, tokens_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string }[];
  const update = database.prepare(
    "UPDATE module_theme_configs SET tokens_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    let tokens: Record<string, unknown>;
    try {
      tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
    } catch {
      tokens = {};
    }
    const nextTokens = {
      ...tokens,
      textInputBar:
        tokens.textInputBar &&
        typeof tokens.textInputBar === "object" &&
        !Array.isArray(tokens.textInputBar)
          ? {
              ...fallbackTextInputBar,
              ...cleanTextInputBarConfig(tokens.textInputBar),
            }
          : fallbackTextInputBar,
      keyboard:
        tokens.keyboard &&
        typeof tokens.keyboard === "object" &&
        !Array.isArray(tokens.keyboard)
          ? {
              ...fallbackKeyboard,
              ...cleanKeyboardConfig(tokens.keyboard),
            }
          : fallbackKeyboard,
    };
    update.run(JSON.stringify(nextTokens), row.id);
  }
  database.pragma("user_version = 14");
}

function applyAdditiveV15Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, content_json
       FROM module_instances
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; content_json: string }[];
  const update = database.prepare(
    "UPDATE module_instances SET content_json = ? WHERE id = ?",
  );

  for (const row of rows) {
    let content: Record<string, unknown>;
    try {
      content = JSON.parse(row.content_json) as Record<string, unknown>;
    } catch {
      continue;
    }
    const participants = Array.isArray(content.participants)
      ? content.participants.filter(
          (item): item is Record<string, unknown> =>
            item !== null && typeof item === "object" && !Array.isArray(item),
        )
      : [];
    if (participants.length === 0) {
      continue;
    }
    const actorByParticipantId = new Map<string, string>();
    for (const participant of participants) {
      if (
        typeof participant.id === "string" &&
        typeof participant.actorId === "string" &&
        participant.actorId.trim()
      ) {
        actorByParticipantId.set(participant.id, participant.actorId);
      }
    }

    const header =
      content.header !== null &&
      typeof content.header === "object" &&
      !Array.isArray(content.header)
        ? { ...(content.header as Record<string, unknown>) }
        : {};
    const headerParticipantId = header.avatarParticipantId;
    if (
      typeof headerParticipantId === "string" &&
      actorByParticipantId.has(headerParticipantId)
    ) {
      header.actorId = actorByParticipantId.get(headerParticipantId);
    }
    if (header.useParticipantColor === true) {
      header.useContactColor = true;
    }
    delete header.avatarParticipantId;
    delete header.useParticipantColor;

    const messages = Array.isArray(content.messages)
      ? content.messages.map((message) => {
          if (
            message === null ||
            typeof message !== "object" ||
            Array.isArray(message)
          ) {
            return message;
          }
          const nextMessage = { ...(message as Record<string, unknown>) };
          const senderParticipantId = nextMessage.senderParticipantId;
          if (
            typeof senderParticipantId === "string" &&
            actorByParticipantId.has(senderParticipantId)
          ) {
            nextMessage.actorId = actorByParticipantId.get(senderParticipantId);
          }
          delete nextMessage.senderParticipantId;
          return nextMessage;
        })
      : content.messages;

    const nextContent: Record<string, unknown> = {
      ...content,
      header,
      messages,
    };
    delete nextContent.participants;
    update.run(JSON.stringify(nextContent), row.id);
  }
  database.pragma("user_version = 15");
}

function defaultChatBubbleStatusConfig(): Record<string, unknown> {
  return {
    showText: true,
    showTicks: true,
    size: 11,
    gap: 3,
    offsetX: -8,
    offsetY: -5,
    tickSingleIconToken: "message_check",
    tickDoubleIconToken: "message_done_all",
    textColor: "gray_050",
    sentColor: "gray_050",
    deliveredColor: "gray_050",
    readColor: "blue",
    failedColor: "red",
  };
}

function defaultThemeSemanticColorTokens() {
  return {
    "debug.red": "debug_red",
    "icons.primary": "gray_000",
    "icons.secondary": "gray_040",
    "icons.accent": "blue",
    "borders.primary": "gray_080",
    "borders.secondary": "gray_070",
    "borders.alternate": "gray_090",
  };
}

function ensureThemeSemanticColorGroups(tokens: Record<string, unknown>) {
  const modes =
    tokens.modes &&
    typeof tokens.modes === "object" &&
    !Array.isArray(tokens.modes)
      ? { ...(tokens.modes as Record<string, unknown>) }
      : {};

  function mergeMode(mode: "light" | "dark") {
    const modeRoot =
      modes[mode] && typeof modes[mode] === "object" && !Array.isArray(modes[mode])
        ? { ...(modes[mode] as Record<string, unknown>) }
        : {};
    const dark = mode === "dark";
    const modeColors =
      modeRoot.colors &&
      typeof modeRoot.colors === "object" &&
      !Array.isArray(modeRoot.colors)
        ? { ...(modeRoot.colors as Record<string, unknown>) }
        : {};
    const legacyIcons =
      modeRoot.icons &&
      typeof modeRoot.icons === "object" &&
      !Array.isArray(modeRoot.icons)
        ? (modeRoot.icons as Record<string, unknown>)
        : {};
    const legacyBorders =
      modeRoot.borders &&
      typeof modeRoot.borders === "object" &&
      !Array.isArray(modeRoot.borders)
        ? (modeRoot.borders as Record<string, unknown>)
        : {};
    delete modeRoot.icons;
    delete modeRoot.borders;
    return {
      ...modeRoot,
      colors: {
        ...defaultThemeSemanticColorTokens(),
        ...Object.fromEntries(
          Object.entries(legacyIcons).map(([key, value]) => [`icons.${key}`, value]),
        ),
        ...Object.fromEntries(
          Object.entries(legacyBorders).map(([key, value]) => [
            `borders.${key}`,
            value,
          ]),
        ),
        ...modeColors,
      },
    };
  }
  const rootColors =
    tokens.colors &&
    typeof tokens.colors === "object" &&
    !Array.isArray(tokens.colors)
      ? { ...(tokens.colors as Record<string, unknown>) }
      : {};
  const legacyRootIcons =
    tokens.icons &&
    typeof tokens.icons === "object" &&
    !Array.isArray(tokens.icons)
      ? (tokens.icons as Record<string, unknown>)
      : {};
  const legacyRootBorders =
    tokens.borders &&
    typeof tokens.borders === "object" &&
    !Array.isArray(tokens.borders)
      ? (tokens.borders as Record<string, unknown>)
      : {};
  const nextTokens = { ...tokens };
  delete nextTokens.icons;
  delete nextTokens.borders;

  return {
    ...nextTokens,
    colors: {
      ...defaultThemeSemanticColorTokens(),
      ...Object.fromEntries(
        Object.entries(legacyRootIcons).map(([key, value]) => [`icons.${key}`, value]),
      ),
      ...Object.fromEntries(
        Object.entries(legacyRootBorders).map(([key, value]) => [
          `borders.${key}`,
          value,
        ]),
      ),
      ...rootColors,
    },
    modes: {
      ...modes,
      light: mergeMode("light"),
      dark: mergeMode("dark"),
    },
  };
}

function normalizeComponentSemanticColorTokens(tokens: Record<string, unknown>) {
  const replacements: Record<string, string> = {
    accent: "icons.accent",
    background: "icons.secondary",
    textSecondary: "icons.primary",
    blue: "icons.accent",
    blue_bright: "icons.accent",
    gray_000: "icons.primary",
    gray_040: "icons.secondary",
    gray_050: "icons.secondary",
    gray_080: "borders.primary",
    gray_100: "icons.secondary",
  };
  function normalizeValue(value: unknown): unknown {
    if (typeof value === "string") {
      return replacements[value] ?? value;
    }
    if (Array.isArray(value)) {
      return value.map((entry) => normalizeValue(entry));
    }
    if (value && typeof value === "object") {
      return Object.fromEntries(
        Object.entries(value as Record<string, unknown>).map(([key, entry]) => [
          key,
          normalizeValue(entry),
        ]),
      );
    }
    return value;
  }
  const next = normalizeValue(tokens) as Record<string, unknown>;
  for (const key of [
    "borderColorToken",
    "labelColorToken",
    "playCircleColorToken",
    "playIconColorToken",
    "statusColorToken",
    "backgroundColorToken",
    "waveformColorToken",
    "waveformPlayedColorToken",
    "textColorToken",
    "idleTextColor",
    "cursorColor",
  ]) {
    const value = next[key];
    if (typeof value === "string" && replacements[value]) {
      next[key] = replacements[value];
    }
  }
  if (next.componentType === "video_message") {
    return {
      statusVisible: true,
      statusIconToken: "media_video",
      statusColorToken: "icons.secondary",
      statusSize: 12,
      statusPaddingX: 8,
      statusPaddingY: 6,
      statusGap: 4,
      ...next,
    };
  }
  if (next.componentType === "avatar") {
    const { borderColor: _legacyBorderColor, ...rest } = next;
    return {
      borderColorToken: "borders.primary",
      ...rest,
    };
  }
  return next;
}

function normalizeCoreChatHeaderSemanticTokens(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return value;
  }
  const root = { ...(value as Record<string, unknown>) };
  const header =
    root.header && typeof root.header === "object" && !Array.isArray(root.header)
      ? { ...(root.header as Record<string, unknown>) }
      : {};
  const modes =
    root.modes && typeof root.modes === "object" && !Array.isArray(root.modes)
      ? { ...(root.modes as Record<string, unknown>) }
      : {};

  root.header = {
    ...header,
    background:
      typeof header.background === "string" && header.background.includes(".")
        ? header.background
        : "colors.background",
    separatorColor:
      typeof header.separatorColor === "string" &&
      header.separatorColor.includes(".")
        ? header.separatorColor
        : "borders.primary",
  };

  for (const mode of ["light", "dark"] as const) {
    const modeRoot =
      modes[mode] && typeof modes[mode] === "object" && !Array.isArray(modes[mode])
        ? { ...(modes[mode] as Record<string, unknown>) }
        : undefined;
    if (!modeRoot) continue;
    if (
      modeRoot.header &&
      typeof modeRoot.header === "object" &&
      !Array.isArray(modeRoot.header)
    ) {
      delete modeRoot.header;
    }
    modes[mode] = modeRoot;
  }
  root.modes = modes;
  return root;
}

function mergeChatBubbleStatusDefaults(tokens: Record<string, unknown>) {
  const chatBubbles =
    tokens.chatBubbles &&
    typeof tokens.chatBubbles === "object" &&
    !Array.isArray(tokens.chatBubbles)
      ? { ...(tokens.chatBubbles as Record<string, unknown>) }
      : {};
  const status =
    chatBubbles.status &&
    typeof chatBubbles.status === "object" &&
    !Array.isArray(chatBubbles.status)
      ? { ...(chatBubbles.status as Record<string, unknown>) }
      : {};
  const modes =
    tokens.modes &&
    typeof tokens.modes === "object" &&
    !Array.isArray(tokens.modes)
      ? { ...(tokens.modes as Record<string, unknown>) }
      : {};
  function mergeModeStatus(
    mode: "light" | "dark",
    defaults: Record<string, unknown>,
  ) {
    const modeRoot =
      modes[mode] && typeof modes[mode] === "object" && !Array.isArray(modes[mode])
        ? { ...(modes[mode] as Record<string, unknown>) }
        : {};
    const modeBubbles =
      modeRoot.chatBubbles &&
      typeof modeRoot.chatBubbles === "object" &&
      !Array.isArray(modeRoot.chatBubbles)
        ? { ...(modeRoot.chatBubbles as Record<string, unknown>) }
        : {};
    const modeStatus =
      modeBubbles.status &&
      typeof modeBubbles.status === "object" &&
      !Array.isArray(modeBubbles.status)
        ? { ...(modeBubbles.status as Record<string, unknown>) }
        : {};
    return {
      ...modeRoot,
      chatBubbles: {
        ...modeBubbles,
        status: {
          ...defaults,
          ...modeStatus,
        },
      },
    };
  }
  return {
    ...tokens,
    chatBubbles: {
      surfaceReliefEnabled: true,
      contentMetaGap: 4,
      messageLabelUseActorColor: true,
      messageLabelOffsetX: 0,
      messageLabelOffsetY: 0,
      ...chatBubbles,
      status: {
        ...defaultChatBubbleStatusConfig(),
        ...status,
      },
    },
    modes: {
      ...modes,
      light: mergeModeStatus("light", {
        textColor: "gray_050",
        sentColor: "gray_050",
        deliveredColor: "gray_050",
        readColor: "blue",
        failedColor: "red",
      }),
      dark: mergeModeStatus("dark", {
        textColor: "gray_060",
        sentColor: "gray_060",
        deliveredColor: "gray_060",
        readColor: "blue_bright",
        failedColor: "red",
      }),
    },
  };
}

function applyAdditiveV16Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, tokens_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string }[];
  const update = database.prepare(
    "UPDATE module_theme_configs SET tokens_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    let tokens: Record<string, unknown>;
    try {
      tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
    } catch {
      continue;
    }
    update.run(JSON.stringify(mergeChatBubbleStatusDefaults(tokens)), row.id);
  }
  database.pragma("user_version = 16");
}

function applyAdditiveV17Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS production_fonts (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      family TEXT NOT NULL,
      files_json TEXT NOT NULL,
      source_path TEXT,
      metadata_json TEXT,
      UNIQUE (production_id, family)
    );
    CREATE INDEX IF NOT EXISTS idx_production_fonts_lookup
      ON production_fonts(production_id, family);
  `);
  database.pragma("user_version = 17");
}

function applyAdditiveV18Migration(database: SQLiteDatabase): void {
  const columns = new Set(
    (
      database.pragma("table_info(production_fonts)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (columns.has("files_json") && !columns.has("style")) {
    database.pragma("user_version = 18");
    return;
  }
  const legacyRows = database
    .prepare("SELECT * FROM production_fonts ORDER BY production_id, family, style")
    .all() as {
    id: string;
    production_id: string;
    family: string;
    style?: string | null;
    file_path?: string | null;
    source_path?: string | null;
    postscript_name?: string | null;
    metadata_json?: string | null;
  }[];
  database.exec(`
    DROP INDEX IF EXISTS idx_production_fonts_lookup;
    CREATE TABLE IF NOT EXISTS production_fonts_v18 (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      family TEXT NOT NULL,
      files_json TEXT NOT NULL,
      source_path TEXT,
      metadata_json TEXT,
      UNIQUE (production_id, family)
    );
  `);
  const grouped = new Map<
    string,
    {
      id: string;
      productionId: string;
      family: string;
      sourcePath: string | null;
      files: Record<string, unknown>[];
      metadata: Record<string, unknown>;
    }
  >();
  for (const row of legacyRows) {
    const key = `${row.production_id}:${row.family}`;
    const current =
      grouped.get(key) ??
      {
        id: row.id,
        productionId: row.production_id,
        family: row.family,
        sourcePath: row.source_path ?? null,
        files: [],
        metadata: {},
      };
    if (row.metadata_json) {
      try {
        current.metadata = {
          ...current.metadata,
          ...(JSON.parse(row.metadata_json) as Record<string, unknown>),
        };
      } catch {
        // Keep malformed metadata out of the migrated family record.
      }
    }
    if (row.file_path) {
      current.files.push({
        style: row.style ?? "Regular",
        filePath: row.file_path,
        ...(row.source_path ? { sourcePath: row.source_path } : {}),
        ...(row.postscript_name ? { postscriptName: row.postscript_name } : {}),
      });
    }
    grouped.set(key, current);
  }
  const insert = database.prepare(
    `INSERT INTO production_fonts_v18 (
      id,
      production_id,
      family,
      files_json,
      source_path,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?, ?)`,
  );
  for (const family of grouped.values()) {
    insert.run(
      family.id,
      family.productionId,
      family.family,
      JSON.stringify({ files: family.files }),
      family.sourcePath,
      JSON.stringify(family.metadata),
    );
  }
  database.exec(`
    DROP TABLE production_fonts;
    ALTER TABLE production_fonts_v18 RENAME TO production_fonts;
    CREATE INDEX IF NOT EXISTS idx_production_fonts_lookup
      ON production_fonts(production_id, family);
  `);
  database.pragma("user_version = 18");
}

function applyAdditiveV19Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS palette_colors (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      token TEXT NOT NULL,
      value_hex TEXT NOT NULL CHECK (value_hex GLOB '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
      is_neutral INTEGER NOT NULL DEFAULT 0 CHECK (is_neutral IN (0, 1)),
      metadata_json TEXT,
      UNIQUE (production_id, token)
    );
    CREATE INDEX IF NOT EXISTS idx_palette_colors_lookup
      ON palette_colors(production_id, token);
  `);
  const paletteColumns = new Set(
    (
      database.pragma("table_info(palette_colors)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!paletteColumns.has("is_neutral")) {
    database.exec(`
      ALTER TABLE palette_colors
        ADD COLUMN is_neutral INTEGER NOT NULL DEFAULT 0 CHECK (is_neutral IN (0, 1))
    `);
  }
  const productions = database
    .prepare("SELECT id FROM productions ORDER BY id")
    .all() as { id: string }[];
  const insert = database.prepare(
    `INSERT OR IGNORE INTO palette_colors (
      id,
      production_id,
      token,
      value_hex,
      is_neutral,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?, ?)`,
  );
  for (const production of productions) {
    for (const [token, valueHex] of SEED_PALETTE_COLORS) {
      insert.run(
        `palette_${production.id}_${token}`,
        production.id,
        token,
        valueHex,
        isNeutralHex(valueHex) ? 1 : 0,
        JSON.stringify({
          source: token === "debug_red" ? "debug_sentinel" : "base_seed_palette",
          protected: token === "debug_red",
          hiddenFromPickers: token === "debug_red",
          note:
            token === "debug_red"
              ? "Protected sentinel color for unresolved theme/component color decisions."
              : "Primitive production color seeded from the base design palette.",
        }),
      );
    }
  }
  database.pragma("user_version = 19");
}

function applyAdditiveV20Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  const update = database.prepare(
    "UPDATE themes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      update.run(
        JSON.stringify(convertThemeColorsToPaletteTokens(tokens)),
        row.id,
      );
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 20");
}

function applyAdditiveV21Migration(database: SQLiteDatabase): void {
  const existingColumns = new Map<string, Set<string>>();
  function columnsForTable(tableName: string) {
    const cached = existingColumns.get(tableName);
    if (cached) return cached;
    const columns = new Set(
      (
        database.pragma(`table_info(${tableName})`) as {
          name: string;
        }[]
      ).map((column) => column.name),
    );
    existingColumns.set(tableName, columns);
    return columns;
  }

  for (const [tableName, columnName] of COLOR_NORMALIZATION_JSON_COLUMNS) {
    if (!columnsForTable(tableName).has(columnName)) continue;
    const rows = database
      .prepare(
        `SELECT id, ${columnName} AS json_value FROM ${tableName} WHERE ${columnName} IS NOT NULL`,
      )
      .all() as { id: string; json_value: string }[];
    if (rows.length === 0) continue;
    const update = database.prepare(
      `UPDATE ${tableName} SET ${columnName} = ? WHERE id = ?`,
    );
    for (const row of rows) {
      try {
        const parsed = JSON.parse(row.json_value) as unknown;
        const normalized = normalizePhysicalColorsToPalette(parsed);
        const nextJson = JSON.stringify(normalized);
        if (nextJson !== row.json_value) {
          update.run(nextJson, row.id);
        }
      } catch {
        // Leave malformed JSON untouched; validation paths should surface it.
      }
    }
  }
  database.pragma("user_version = 21");
}

function applyAdditiveV22Migration(database: SQLiteDatabase): void {
  const existingColumns = new Map<string, Set<string>>();
  function columnsForTable(tableName: string) {
    const cached = existingColumns.get(tableName);
    if (cached) return cached;
    const columns = new Set(
      (
        database.pragma(`table_info(${tableName})`) as {
          name: string;
        }[]
      ).map((column) => column.name),
    );
    existingColumns.set(tableName, columns);
    return columns;
  }

  for (const [tableName, columnName] of COLOR_NORMALIZATION_JSON_COLUMNS) {
    if (!columnsForTable(tableName).has(columnName)) continue;
    const rows = database
      .prepare(
        `SELECT id, ${columnName} AS json_value FROM ${tableName} WHERE ${columnName} IS NOT NULL`,
      )
      .all() as { id: string; json_value: string }[];
    if (rows.length === 0) continue;
    const update = database.prepare(
      `UPDATE ${tableName} SET ${columnName} = ? WHERE id = ?`,
    );
    for (const row of rows) {
      try {
        const parsed = JSON.parse(row.json_value) as unknown;
        const cleaned = cleanupDebugRedDefault([columnName], parsed);
        const nextJson = JSON.stringify(cleaned);
        if (nextJson !== row.json_value) {
          update.run(nextJson, row.id);
        }
      } catch {
        // Leave malformed JSON untouched; validation paths should surface it.
      }
    }
  }
  database.pragma("user_version = 22");
}

function applyAdditiveV23Migration(database: SQLiteDatabase): void {
  const screenColumns = new Set(
    (
      database.pragma("table_info(screen_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!screenColumns.has("duration_frames")) {
    database.exec(
      `ALTER TABLE screen_instances
       ADD COLUMN duration_frames INTEGER NOT NULL DEFAULT 1 CHECK (duration_frames > 0)`,
    );
  }
  database.exec(`
    UPDATE screen_instances
    SET duration_frames = MAX(1, end_frame - start_frame)
    WHERE duration_frames IS NULL
      OR duration_frames <= 1
  `);
  database.pragma("user_version = 23");
}

function defaultAvatarComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "avatar",
    cornerRadius: 12,
    borderWidth: 0,
    borderColorToken: "borders.primary",
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: true,
  });
}

function defaultButtonIconComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "button_icon",
    cornerRadius: 0,
    iconPadding: 2,
    borderWidth: 0,
    borderColorToken: "borders.primary",
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: false,
    labelEnabled: false,
    labelPosition: "bottom",
    labelPadding: 2,
    labelSize: 10,
    labelColorToken: "icons.primary",
  });
}

function defaultLabelComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "label",
    sizingMode: "content",
    width: 120,
    height: 28,
    paddingX: 8,
    paddingY: 4,
    cornerRadius: 10,
    borderWidth: 0,
    borderColorToken: "borders.primary",
    backgroundVisible: true,
    backgroundColorToken: "background",
    textColorToken: "textPrimary",
    fontSize: 12,
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: false,
  });
}

function defaultAudioMessageComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "audio_message",
    width: 260,
    height: 58,
    avatarSize: 38,
    avatarPosition: "left",
    avatarGap: 8,
    playCircleSize: 32,
    playCircleColorToken: "icons.accent",
    playIconColorToken: "icons.secondary",
    microphoneBadgeSize: 16,
    microphoneBadgeIconToken: "media_mic",
    waveformBarCount: 28,
    waveformGap: 2,
    waveformMinHeight: 4,
    waveformMaxHeight: 22,
    progressKnobSize: 9,
    waveformColorToken: "icons.primary",
    waveformPlayedColorToken: "icons.accent",
    textSize: 11,
    textColorToken: "icons.secondary",
    cornerRadius: 18,
    borderWidth: 0,
    borderColorToken: "borders.primary",
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: false,
  });
}

function defaultVideoMessageComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "video_message",
    cornerRadius: 18,
    borderWidth: 0,
    borderColorToken: "borders.primary",
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: false,
    playOverlayEnabled: true,
    playCircleSize: 44,
    playCircleColorToken: "icons.accent",
    playCircleAlpha: 0.55,
    playIconColorToken: "icons.secondary",
    statusVisible: true,
    statusIconToken: "media_video",
    statusColorToken: "icons.secondary",
    statusSize: 12,
    statusPaddingX: 8,
    statusPaddingY: 6,
    statusGap: 4,
  });
}

function defaultTextInputBarComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "text_input_bar",
    placeholder: "Mensaje",
    cursorVisible: true,
    idleTextColor: "icons.secondary",
    cursorWidth: 2,
    cursorBlinkFrames: 15,
    cursorColor: "theme.cursor.color",
    fieldRadius: 20,
    fieldShadowEnabled: true,
    iconSets: {
      left: {
        idle: [
          { token: "chat_emoji", order: 10 },
          { token: "chat_attach", order: 20 },
        ],
        typing: [{ token: "chat_emoji", order: 10 }],
      },
      right: {
        idle: [
          { token: "media_camera", order: 10 },
          { token: "media_mic", order: 20 },
        ],
        typing: [{ token: "chat_send", order: 10, color: "icons.accent" }],
      },
    },
  });
}

function defaultKeyboardComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "keyboard",
    language: "es",
    fontFamily: "Oswald",
    fontWeight: 400,
    fontStyle: "normal",
    pushDurationFrames: 8,
    messageGapToTextInput: 10,
    pressedEffect: "popover",
    keyRadius: 7,
    keyPadding: 6,
    keyShadowEnabled: true,
    surfaceReliefEnabled: true,
    bottomItems: [
      {
        id: "app_language",
        label: "app_language",
        kind: "iconToken",
        token: "app_language",
        zone: "left",
        order: 10,
      },
      {
        id: "media_mic",
        label: "media_mic",
        kind: "iconToken",
        token: "media_mic",
        zone: "right",
        order: 10,
      },
    ],
  });
}

function seedDefaultComponentClasses(database: SQLiteDatabase): void {
  const productions = database
    .prepare("SELECT id FROM productions ORDER BY id")
    .all() as { id: string }[];
  const insert = database.prepare(
    `INSERT OR IGNORE INTO component_classes (
      id,
      production_id,
      component_type,
      name,
      tokens_json,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?, ?)`,
  );
  const updateTokens = database.prepare(
    "UPDATE component_classes SET tokens_json = ? WHERE id = ?",
  );
  for (const production of productions) {
    const defaults = [
      {
        id: `${production.id}:avatar_default`,
        type: "avatar",
        name: "Default avatar",
        tokens: defaultAvatarComponentTokens(),
      },
      {
        id: `${production.id}:button_icon_default`,
        type: "button_icon",
        name: "Default icon button",
        tokens: defaultButtonIconComponentTokens(),
      },
      {
        id: `${production.id}:label_default`,
        type: "label",
        name: "Default label",
        tokens: defaultLabelComponentTokens(),
      },
      {
        id: `${production.id}:audio_message_default`,
        type: "audio_message",
        name: "Default audio message",
        tokens: defaultAudioMessageComponentTokens(),
      },
      {
        id: `${production.id}:video_message_default`,
        type: "video_message",
        name: "Default video message",
        tokens: defaultVideoMessageComponentTokens(),
      },
      {
        id: `${production.id}:text_input_bar_default`,
        type: "text_input_bar",
        name: "Default text input bar",
        tokens: defaultTextInputBarComponentTokens(),
      },
      {
        id: `${production.id}:keyboard_default`,
        type: "keyboard",
        name: "Default keyboard",
        tokens: defaultKeyboardComponentTokens(),
      },
    ];
    for (const component of defaults) {
      insert.run(
        component.id,
        production.id,
        component.type,
        component.name,
        component.tokens,
        JSON.stringify({ source: "seed" }),
      );
      const current = database
        .prepare("SELECT tokens_json FROM component_classes WHERE id = ?")
        .get(component.id) as { tokens_json?: string } | undefined;
      if (!current?.tokens_json) continue;
      try {
        const currentTokens = JSON.parse(current.tokens_json) as Record<
          string,
          unknown
        >;
        const defaultTokens = JSON.parse(component.tokens) as Record<
          string,
          unknown
        >;
        const nextTokens = { ...defaultTokens, ...currentTokens };
        if (JSON.stringify(nextTokens) !== JSON.stringify(currentTokens)) {
          updateTokens.run(JSON.stringify(nextTokens), component.id);
        }
      } catch {
        updateTokens.run(component.tokens, component.id);
      }
    }
  }
}

function applyAdditiveV24Migration(database: SQLiteDatabase): void {
  database.exec(`
    CREATE TABLE IF NOT EXISTS component_classes (
      id TEXT PRIMARY KEY,
      production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
      component_type TEXT NOT NULL,
      name TEXT NOT NULL,
      tokens_json TEXT NOT NULL,
      metadata_json TEXT,
      UNIQUE (production_id, component_type, name)
    );
    CREATE INDEX IF NOT EXISTS idx_component_classes_lookup
      ON component_classes(production_id, component_type, name);
  `);
  seedDefaultComponentClasses(database);
  database.pragma("user_version = 24");
}

function applyAdditiveV25Migration(database: SQLiteDatabase): void {
  seedDefaultComponentClasses(database);
  database.pragma("user_version = 25");
}

function applyAdditiveV26Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, tokens_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string }[];
  const update = database.prepare(
    "UPDATE module_theme_configs SET tokens_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    let tokens: Record<string, unknown>;
    try {
      tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
    } catch {
      continue;
    }
    update.run(JSON.stringify(mergeChatBubbleStatusDefaults(tokens)), row.id);
  }
  database.pragma("user_version = 26");
}

function applyAdditiveV27Migration(database: SQLiteDatabase): void {
  seedDefaultComponentClasses(database);
  database.pragma("user_version = 27");
}

function applyAdditiveV28Migration(database: SQLiteDatabase): void {
  seedDefaultComponentClasses(database);
  database.pragma("user_version = 28");
}

function applyAdditiveV29Migration(database: SQLiteDatabase): void {
  const themeRows = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  const updateTheme = database.prepare(
    "UPDATE themes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of themeRows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      updateTheme.run(JSON.stringify(ensureThemeSemanticColorGroups(tokens)), row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }

  seedDefaultComponentClasses(database);
  const componentRows = database
    .prepare("SELECT id, tokens_json FROM component_classes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  const updateComponent = database.prepare(
    "UPDATE component_classes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of componentRows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      updateComponent.run(
        JSON.stringify(normalizeComponentSemanticColorTokens(tokens)),
        row.id,
      );
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 29");
}

function applyAdditiveV30Migration(database: SQLiteDatabase): void {
  const columns = new Set(
    (
      database.pragma("table_info(palette_colors)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  if (!columns.has("is_neutral")) {
    database.exec(`
      ALTER TABLE palette_colors
        ADD COLUMN is_neutral INTEGER NOT NULL DEFAULT 0 CHECK (is_neutral IN (0, 1))
    `);
  }
  database.exec(`
    UPDATE palette_colors
      SET is_neutral = CASE
        WHEN UPPER(SUBSTR(value_hex, 2, 2)) = UPPER(SUBSTR(value_hex, 4, 2))
         AND UPPER(SUBSTR(value_hex, 4, 2)) = UPPER(SUBSTR(value_hex, 6, 2))
        THEN 1
        ELSE is_neutral
      END
  `);
  const themeRows = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  const updateTheme = database.prepare(
    "UPDATE themes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of themeRows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      if (!tokens.neutralTint) {
        updateTheme.run(
          JSON.stringify({
            neutralTint: { hueDeg: 0, saturation: 0 },
            ...tokens,
          }),
          row.id,
        );
      }
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  const appRows = database
    .prepare("SELECT id, config_json FROM apps ORDER BY id")
    .all() as { id: string; config_json: string }[];
  const updateApp = database.prepare(
    "UPDATE apps SET config_json = ? WHERE id = ?",
  );
  for (const row of appRows) {
    try {
      const config = JSON.parse(row.config_json) as Record<string, unknown>;
      const tokens = config.tokens_json;
      if (tokens && typeof tokens === "object" && !Array.isArray(tokens)) {
        const tokenRoot = tokens as Record<string, unknown>;
        if (!tokenRoot.neutralTint) {
          updateApp.run(
            JSON.stringify({
              ...config,
              tokens_json: {
                neutralTint: { hueDeg: 0, saturation: 0 },
                ...tokenRoot,
              },
            }),
            row.id,
          );
        }
      } else if (!config.neutralTint) {
        updateApp.run(
          JSON.stringify({
            neutralTint: { hueDeg: 0, saturation: 0 },
            ...config,
          }),
          row.id,
        );
      }
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 30");
}

function nextStableMessageId(usedIds: Set<string>): string {
  let index = usedIds.size + 1;
  let id = `message_${String(index).padStart(3, "0")}`;
  while (usedIds.has(id)) {
    index += 1;
    id = `message_${String(index).padStart(3, "0")}`;
  }
  usedIds.add(id);
  return id;
}

function ensureMessageIdsInModuleData(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = value as Record<string, unknown>;
  if (!Array.isArray(root.messages)) return value;

  const usedIds = new Set<string>();
  const messages = root.messages.map((message) => {
    if (!message || typeof message !== "object" || Array.isArray(message)) {
      return message;
    }
    const nextMessage = { ...(message as Record<string, unknown>) };
    const currentId =
      typeof nextMessage.id === "string" ? nextMessage.id.trim() : "";
    if (currentId && !usedIds.has(currentId)) {
      usedIds.add(currentId);
      nextMessage.id = currentId;
      return nextMessage;
    }
    nextMessage.id = nextStableMessageId(usedIds);
    return nextMessage;
  });

  return { ...root, messages };
}

function applyAdditiveV31Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare("SELECT id, module_data_json FROM screen_instances ORDER BY id")
    .all() as { id: string; module_data_json: string | null }[];
  const update = database.prepare(
    "UPDATE screen_instances SET module_data_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    if (!row.module_data_json) continue;
    try {
      const data = JSON.parse(row.module_data_json) as unknown;
      const nextData = ensureMessageIdsInModuleData(data);
      if (JSON.stringify(data) !== JSON.stringify(nextData)) {
        update.run(JSON.stringify(nextData), row.id);
      }
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 31");
}

function applyAdditiveV32Migration(database: SQLiteDatabase): void {
  const productions = database
    .prepare("SELECT id FROM productions ORDER BY id")
    .all() as { id: string }[];
  const insertPalette = database.prepare(
    `INSERT OR IGNORE INTO palette_colors (
      id,
      production_id,
      token,
      value_hex,
      is_neutral,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?, ?)`,
  );
  const updatePalette = database.prepare(
    `UPDATE palette_colors
       SET value_hex = ?,
           is_neutral = ?,
           metadata_json = ?
     WHERE production_id = ? AND token = ?`,
  );
  const debugMetadata = JSON.stringify({
    source: "debug_sentinel",
    protected: true,
    hiddenFromPickers: true,
    note: "Protected sentinel color for unresolved theme/component color decisions.",
  });
  for (const production of productions) {
    insertPalette.run(
      `palette_${production.id}_debug_red`,
      production.id,
      "debug_red",
      "#FF00FF",
      0,
      debugMetadata,
    );
    updatePalette.run("#FF00FF", 0, debugMetadata, production.id, "debug_red");
  }

  const themes = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  const updateTheme = database.prepare(
    "UPDATE themes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of themes) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      const nextTokens = ensureThemeSemanticColorGroups(tokens);
      const nextJson = JSON.stringify(nextTokens);
      if (nextJson !== row.tokens_json) {
        updateTheme.run(nextJson, row.id);
      }
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 32");
}

function normalizeProductionFontTokens(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(normalizeProductionFontTokens);
  if (!value || typeof value !== "object") return value;
  const source = value as Record<string, unknown>;
  const next: Record<string, unknown> = {};
  for (const [key, entry] of Object.entries(source)) {
    if (key === "fontWeight" && typeof entry === "string" && entry.trim()) {
      const parsed = Number(entry);
      next[key] = Number.isFinite(parsed)
        ? parsed
        : fontWeightForProductionStyle(entry);
      if (!Object.hasOwn(source, "fontStyle")) {
        next.fontStyle = fontStyleForProductionStyle(entry);
      }
      continue;
    }
    next[key] = normalizeProductionFontTokens(entry);
  }
  return next;
}

function normalizeJsonColumnFontTokens(
  database: SQLiteDatabase,
  table: string,
  idColumn: string,
  jsonColumn: string,
) {
  const rows = database
    .prepare(`SELECT ${idColumn} AS id, ${jsonColumn} AS value FROM ${table}`)
    .all() as { id: string; value: string | null }[];
  const update = database.prepare(
    `UPDATE ${table} SET ${jsonColumn} = ? WHERE ${idColumn} = ?`,
  );
  for (const row of rows) {
    if (!row.value) continue;
    try {
      const parsed = JSON.parse(row.value) as unknown;
      const normalized = normalizeProductionFontTokens(parsed);
      const nextJson = JSON.stringify(normalized);
      if (nextJson !== row.value) update.run(nextJson, row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
}

function applyAdditiveV33Migration(database: SQLiteDatabase): void {
  normalizeJsonColumnFontTokens(database, "themes", "id", "tokens_json");
  normalizeJsonColumnFontTokens(
    database,
    "module_theme_configs",
    "id",
    "tokens_json",
  );
  normalizeJsonColumnFontTokens(
    database,
    "component_classes",
    "id",
    "tokens_json",
  );
  normalizeJsonColumnFontTokens(
    database,
    "module_instances",
    "id",
    "behavior_json",
  );
  database.pragma("user_version = 33");
}

function removeCoreChatModuleFontIdentityFields(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = { ...(value as Record<string, unknown>) };
  const typography = root.typography;
  if (typography && typeof typography === "object" && !Array.isArray(typography)) {
    const nextTypography = { ...(typography as Record<string, unknown>) };
    for (const key of ["message", "headerTitle", "headerSubtitle"]) {
      const entry = nextTypography[key];
      if (!entry || typeof entry !== "object" || Array.isArray(entry)) continue;
      const nextEntry = { ...(entry as Record<string, unknown>) };
      delete nextEntry.fontFamily;
      delete nextEntry.family;
      delete nextEntry.fontStyle;
      delete nextEntry.productionFontId;
      delete nextEntry.source;
      nextTypography[key] = nextEntry;
    }
    root.typography = nextTypography;
  }
  return root;
}

function applyAdditiveV34Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, tokens_json, metadata_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string; metadata_json: string | null }[];
  const update = database.prepare(
    `UPDATE module_theme_configs
     SET tokens_json = ?, metadata_json = ?
     WHERE id = ?`,
  );
  for (const row of rows) {
    try {
      const tokens = removeCoreChatModuleFontIdentityFields(
        JSON.parse(row.tokens_json),
      );
      let metadata = row.metadata_json
        ? (JSON.parse(row.metadata_json) as Record<string, unknown>)
        : {};
      if (
        metadata.default_tokens_json &&
        typeof metadata.default_tokens_json === "object" &&
        !Array.isArray(metadata.default_tokens_json)
      ) {
        metadata = {
          ...metadata,
          default_tokens_json: removeCoreChatModuleFontIdentityFields(
            metadata.default_tokens_json,
          ),
        };
      }
      update.run(JSON.stringify(tokens), JSON.stringify(metadata), row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 34");
}

function removeCoreChatModuleTypography(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = { ...(value as Record<string, unknown>) };
  delete root.typography;
  return root;
}

function removeCoreChatModuleFontGroup(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = { ...(value as Record<string, unknown>) };
  delete root.fonts;
  return root;
}

function removeLabelComponentFontIdentity(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = { ...(value as Record<string, unknown>) };
  delete root.fontFamily;
  delete root.fontWeight;
  delete root.fontStyle;
  delete root.productionFontId;
  delete root.source;
  return root;
}

function removeOrphanAppFontMetadata(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const root = { ...(value as Record<string, unknown>) };
  const tokenRoot =
    root.tokens_json && typeof root.tokens_json === "object" && !Array.isArray(root.tokens_json)
      ? { ...(root.tokens_json as Record<string, unknown>) }
      : root;
  if (tokenRoot.fonts && typeof tokenRoot.fonts === "object" && !Array.isArray(tokenRoot.fonts)) {
    const fonts = { ...(tokenRoot.fonts as Record<string, unknown>) };
    if (typeof fonts.family !== "string" || !fonts.family.trim()) {
      delete fonts.source;
      delete fonts.productionFontId;
      delete fonts.fontWeight;
      delete fonts.fontStyle;
    }
    tokenRoot.fonts = fonts;
  }
  return root.tokens_json && typeof root.tokens_json === "object" && !Array.isArray(root.tokens_json)
    ? { ...root, tokens_json: tokenRoot }
    : tokenRoot;
}

function applyAdditiveV35Migration(database: SQLiteDatabase): void {
  const moduleRows = database
    .prepare(
      `SELECT id, tokens_json, metadata_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string; metadata_json: string | null }[];
  const updateModule = database.prepare(
    `UPDATE module_theme_configs
     SET tokens_json = ?, metadata_json = ?
     WHERE id = ?`,
  );
  for (const row of moduleRows) {
    try {
      const tokens = removeCoreChatModuleTypography(JSON.parse(row.tokens_json));
      let metadata = row.metadata_json
        ? (JSON.parse(row.metadata_json) as Record<string, unknown>)
        : {};
      if (
        metadata.default_tokens_json &&
        typeof metadata.default_tokens_json === "object" &&
        !Array.isArray(metadata.default_tokens_json)
      ) {
        metadata = {
          ...metadata,
          default_tokens_json: removeCoreChatModuleTypography(
            metadata.default_tokens_json,
          ),
        };
      }
      updateModule.run(JSON.stringify(tokens), JSON.stringify(metadata), row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }

  const componentRows = database
    .prepare(
      `SELECT id, tokens_json
       FROM component_classes
       WHERE component_type = 'label'`,
    )
    .all() as { id: string; tokens_json: string }[];
  const updateComponent = database.prepare(
    "UPDATE component_classes SET tokens_json = ? WHERE id = ?",
  );
  for (const row of componentRows) {
    try {
      updateComponent.run(
        JSON.stringify(removeLabelComponentFontIdentity(JSON.parse(row.tokens_json))),
        row.id,
      );
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }

  const appRows = database
    .prepare("SELECT id, config_json FROM apps")
    .all() as { id: string; config_json: string | null }[];
  const updateApp = database.prepare(
    "UPDATE apps SET config_json = ? WHERE id = ?",
  );
  for (const row of appRows) {
    if (!row.config_json) continue;
    try {
      updateApp.run(
        JSON.stringify(removeOrphanAppFontMetadata(JSON.parse(row.config_json))),
        row.id,
      );
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }

  database.pragma("user_version = 35");
}

function applyAdditiveV36Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, tokens_json, metadata_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string; metadata_json: string | null }[];
  const update = database.prepare(
    `UPDATE module_theme_configs
     SET tokens_json = ?, metadata_json = ?
     WHERE id = ?`,
  );
  for (const row of rows) {
    try {
      const tokens = removeCoreChatModuleFontGroup(JSON.parse(row.tokens_json));
      let metadata = row.metadata_json
        ? (JSON.parse(row.metadata_json) as Record<string, unknown>)
        : {};
      if (
        metadata.default_tokens_json &&
        typeof metadata.default_tokens_json === "object" &&
        !Array.isArray(metadata.default_tokens_json)
      ) {
        metadata = {
          ...metadata,
          default_tokens_json: removeCoreChatModuleFontGroup(
            metadata.default_tokens_json,
          ),
        };
      }
      update.run(JSON.stringify(tokens), JSON.stringify(metadata), row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 36");
}

function applyAdditiveV37Migration(database: SQLiteDatabase): void {
  const rows = database
    .prepare(
      `SELECT id, tokens_json, metadata_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string; metadata_json: string | null }[];
  const update = database.prepare(
    `UPDATE module_theme_configs
     SET tokens_json = ?, metadata_json = ?
     WHERE id = ?`,
  );
  for (const row of rows) {
    try {
      const tokens = normalizeCoreChatHeaderSemanticTokens(
        JSON.parse(row.tokens_json),
      );
      let metadata = row.metadata_json
        ? (JSON.parse(row.metadata_json) as Record<string, unknown>)
        : {};
      if (
        metadata.default_tokens_json &&
        typeof metadata.default_tokens_json === "object" &&
        !Array.isArray(metadata.default_tokens_json)
      ) {
        metadata = {
          ...metadata,
          default_tokens_json: normalizeCoreChatHeaderSemanticTokens(
            metadata.default_tokens_json,
          ),
        };
      }
      update.run(JSON.stringify(tokens), JSON.stringify(metadata), row.id);
    } catch {
      // Malformed JSON is handled by validation paths; skip migration here.
    }
  }
  database.pragma("user_version = 37");
}

function tableColumns(database: SQLiteDatabase, tableName: string): Set<string> {
  return new Set(
    (
      database.pragma(`table_info(${tableName})`) as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
}

function tableExists(database: SQLiteDatabase, tableName: string): boolean {
  const row = database
    .prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?")
    .get(tableName) as { name: string } | undefined;
  return Boolean(row);
}

function applyAdditiveV38Migration(database: SQLiteDatabase): void {
  const needsDeviceRebuild =
    tableExists(database, "devices") &&
    tableColumns(database, "devices").has("frame_asset_id");
  const needsActorRebuild =
    tableExists(database, "actors") &&
    tableColumns(database, "actors").has("avatar_asset_id");
  const needsAppRebuild =
    tableExists(database, "apps") &&
    tableColumns(database, "apps").has("icon_asset_id");
  const needsMessageRebuild =
    tableExists(database, "messages") &&
    tableColumns(database, "messages").has("media_asset_id");

  if (
    needsDeviceRebuild ||
    needsActorRebuild ||
    needsAppRebuild ||
    needsMessageRebuild ||
    tableExists(database, "media_assets")
  ) {
    database.pragma("foreign_keys = OFF");

    if (needsDeviceRebuild) {
      database.exec(`
        CREATE TABLE devices_v38 (
          id TEXT PRIMARY KEY,
          production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          manufacturer TEXT NOT NULL,
          model TEXT NOT NULL,
          os_family TEXT NOT NULL,
          metrics_json TEXT NOT NULL
        );
        INSERT INTO devices_v38 (
          id,
          production_id,
          name,
          manufacturer,
          model,
          os_family,
          metrics_json
        )
        SELECT
          id,
          production_id,
          name,
          manufacturer,
          model,
          os_family,
          metrics_json
        FROM devices;
        DROP TABLE devices;
        ALTER TABLE devices_v38 RENAME TO devices;
      `);
    }

    if (needsActorRebuild) {
      database.exec(`
        CREATE TABLE actors_v38 (
          id TEXT PRIMARY KEY,
          production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
          display_name TEXT NOT NULL,
          short_name TEXT,
          default_device_id TEXT REFERENCES devices(id) ON DELETE SET NULL,
          default_theme_id TEXT REFERENCES themes(id) ON DELETE SET NULL,
          metadata_json TEXT
        );
        INSERT INTO actors_v38 (
          id,
          production_id,
          display_name,
          short_name,
          default_device_id,
          default_theme_id,
          metadata_json
        )
        SELECT
          id,
          production_id,
          display_name,
          short_name,
          default_device_id,
          default_theme_id,
          metadata_json
        FROM actors;
        DROP TABLE actors;
        ALTER TABLE actors_v38 RENAME TO actors;
      `);
    }

    if (needsAppRebuild) {
      database.exec(`
        CREATE TABLE apps_v38 (
          id TEXT PRIMARY KEY,
          production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          bundle_key TEXT NOT NULL,
          app_type TEXT NOT NULL,
          config_json TEXT,
          metadata_json TEXT
        );
        INSERT INTO apps_v38 (
          id,
          production_id,
          name,
          bundle_key,
          app_type,
          config_json,
          metadata_json
        )
        SELECT
          id,
          production_id,
          name,
          bundle_key,
          app_type,
          config_json,
          metadata_json
        FROM apps;
        DROP TABLE apps;
        ALTER TABLE apps_v38 RENAME TO apps;
      `);
    }

    if (needsMessageRebuild) {
      database.exec(`
        CREATE TABLE messages_v38 (
          id TEXT PRIMARY KEY,
          conversation_id TEXT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
          sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
          sender_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
          message_type TEXT NOT NULL,
          text TEXT,
          start_frame INTEGER NOT NULL CHECK (start_frame >= 0),
          enter_duration_frames INTEGER NOT NULL CHECK (enter_duration_frames >= 0),
          write_on_enabled INTEGER NOT NULL CHECK (write_on_enabled IN (0, 1)),
          write_on_start_frame INTEGER CHECK (write_on_start_frame >= 0),
          write_on_duration_frames INTEGER CHECK (write_on_duration_frames >= 0),
          exit_frame INTEGER CHECK (exit_frame >= 0),
          style_override_json TEXT NOT NULL,
          animation_override_json TEXT NOT NULL,
          layout_override_json TEXT NOT NULL,
          metadata_json TEXT NOT NULL
        );
        INSERT INTO messages_v38 (
          id,
          conversation_id,
          sort_order,
          sender_actor_id,
          message_type,
          text,
          start_frame,
          enter_duration_frames,
          write_on_enabled,
          write_on_start_frame,
          write_on_duration_frames,
          exit_frame,
          style_override_json,
          animation_override_json,
          layout_override_json,
          metadata_json
        )
        SELECT
          id,
          conversation_id,
          sort_order,
          sender_actor_id,
          message_type,
          text,
          start_frame,
          enter_duration_frames,
          write_on_enabled,
          write_on_start_frame,
          write_on_duration_frames,
          exit_frame,
          style_override_json,
          animation_override_json,
          layout_override_json,
          metadata_json
        FROM messages;
        DROP TABLE messages;
        ALTER TABLE messages_v38 RENAME TO messages;
      `);
    }

    database.exec("DROP TABLE IF EXISTS media_assets;");
    database.pragma("foreign_key_check");
    database.pragma("foreign_keys = ON");
  }
  database.pragma("user_version = 38");
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
  applyAdditiveV13Migration(database);
  applyAdditiveV14Migration(database);
  applyAdditiveV15Migration(database);
  applyAdditiveV16Migration(database);
  applyAdditiveV17Migration(database);
  applyAdditiveV18Migration(database);
  applyAdditiveV19Migration(database);
  applyAdditiveV20Migration(database);
  applyAdditiveV21Migration(database);
  applyAdditiveV22Migration(database);
  applyAdditiveV23Migration(database);
  applyAdditiveV24Migration(database);
  applyAdditiveV25Migration(database);
  applyAdditiveV26Migration(database);
  applyAdditiveV27Migration(database);
  applyAdditiveV28Migration(database);
  applyAdditiveV29Migration(database);
  applyAdditiveV30Migration(database);
  applyAdditiveV31Migration(database);
  applyAdditiveV32Migration(database);
  applyAdditiveV33Migration(database);
  applyAdditiveV34Migration(database);
  applyAdditiveV35Migration(database);
  applyAdditiveV36Migration(database);
  applyAdditiveV37Migration(database);
  applyAdditiveV38Migration(database);
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
