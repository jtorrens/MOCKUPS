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
        typing: [{ token: "chat_send", order: 10, color: "#007AFF" }],
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
    textColor: "#7A7A7A",
    sentColor: "#7A7A7A",
    deliveredColor: "#7A7A7A",
    readColor: "#34B7F1",
    failedColor: "#D92D20",
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
      ...chatBubbles,
      status: {
        ...defaultChatBubbleStatusConfig(),
        ...status,
      },
    },
    modes: {
      ...modes,
      light: mergeModeStatus("light", {
        textColor: "#7A7A7A",
        sentColor: "#7A7A7A",
        deliveredColor: "#7A7A7A",
        readColor: "#34B7F1",
        failedColor: "#D92D20",
      }),
      dark: mergeModeStatus("dark", {
        textColor: "#A1A1A6",
        sentColor: "#A1A1A6",
        deliveredColor: "#A1A1A6",
        readColor: "#53C7FF",
        failedColor: "#FF6B5F",
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
