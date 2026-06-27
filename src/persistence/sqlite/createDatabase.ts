import { mkdirSync, readFileSync } from "node:fs";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import Database from "better-sqlite3";
import { developmentDatabasePath } from "./paths.js";

const schemaPath = fileURLToPath(new URL("./schema.sql", import.meta.url));

export type SQLiteDatabase = Database.Database;

const SEED_PALETTE_COLORS = [
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
  if (joined.endsWith("header.background")) return dark ? "gray_020" : "gray_100";
  if (joined.endsWith("header.separatorColor")) return dark ? "gray_060" : "gray_080";
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
  ["media_assets", "dimensions_json"],
  ["media_assets", "metadata_json"],
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
        typing: [{ token: "chat_send", order: 10, color: "blue" }],
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
      metadata_json TEXT,
      UNIQUE (production_id, token)
    );
    CREATE INDEX IF NOT EXISTS idx_palette_colors_lookup
      ON palette_colors(production_id, token);
  `);
  const productions = database
    .prepare("SELECT id FROM productions ORDER BY id")
    .all() as { id: string }[];
  const insert = database.prepare(
    `INSERT OR IGNORE INTO palette_colors (
      id,
      production_id,
      token,
      value_hex,
      metadata_json
    ) VALUES (?, ?, ?, ?, ?)`,
  );
  for (const production of productions) {
    for (const [token, valueHex] of SEED_PALETTE_COLORS) {
      insert.run(
        `palette_${production.id}_${token}`,
        production.id,
        token,
        valueHex,
        JSON.stringify({
          source: "base_seed_palette",
          note: "Primitive production color seeded from the base design palette.",
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
    borderColor: {
      light: "gray_100",
      dark: "gray_020",
    },
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
    borderColorToken: "textSecondary",
    shadowEnabled: false,
    shadowToken: "system",
    surfaceReliefEnabled: false,
    labelEnabled: false,
    labelPosition: "bottom",
    labelPadding: 2,
    labelSize: 10,
    labelColorToken: "textSecondary",
  });
}

function defaultTextInputBarComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "text_input_bar",
    placeholder: "Mensaje",
    cursorVisible: true,
    idleTextColor: "gray_050",
    cursorWidth: 2,
    cursorBlinkFrames: 15,
    cursorColor: "blue",
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
        typing: [{ token: "chat_send", order: 10, color: "blue" }],
      },
    },
  });
}

function defaultKeyboardComponentTokens() {
  return JSON.stringify({
    schemaVersion: 1,
    componentType: "keyboard",
    language: "es",
    keyRadius: 7,
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
