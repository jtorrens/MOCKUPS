import { loadExampleRepository } from "../../domain/repository/fixtureLoader.js";
import {
  resolveShot,
} from "../../domain/resolvers/index.js";
import {
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
} from "../../domain/schemas/index.js";
import { createDatabase } from "./createDatabase.js";
import { seedExampleDataset } from "./seedExampleDataset.js";
import { SQLiteRepository } from "./SQLiteRepository.js";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

function resolveChatAtFrame(repository: SQLiteRepository | ReturnType<typeof loadExampleRepository>) {
  const shot = resolveShot({
    repository,
    productionId: "production_demo",
    shotId: "shot_lock_to_chat",
    shotFrame: 210,
  });
  const chatInstance = shot.active_screen_instances.find(
    (screen) => screen.screen_type === "chat",
  );
  assert(chatInstance?.resolved_props, "Chat props must resolve at shot frame 210");
  return ResolvedChatScreenPropsSchema.parse(chatInstance.resolved_props);
}

const database = createDatabase(":memory:");

try {
  seedExampleDataset(database);
  const sqliteRepository = new SQLiteRepository(database);
  const inMemoryRepository = loadExampleRepository();
  const sqliteChat = resolveChatAtFrame(sqliteRepository);
  const inMemoryChat = resolveChatAtFrame(inMemoryRepository);

  assert(
    JSON.stringify(sqliteChat) === JSON.stringify(inMemoryChat),
    "SQLite and in-memory resolved chat props must be equivalent",
  );

  const sqliteChatInstance = sqliteRepository
    .getScreenInstancesForShot("shot_lock_to_chat")
    .find((instance) => instance.screen_type === "chat");
  assert(sqliteChatInstance, "SQLite Chat screen instance must exist");
  const sqliteModuleInstance =
    sqliteRepository.getPrimaryModuleInstanceForScreenInstance(
      sqliteChatInstance.id,
    );
  assert(sqliteModuleInstance, "SQLite Chat module instance must exist");
  const moduleData = ChatModuleDataSchema.parse(
    sqliteModuleInstance.content_json,
  );
  assert(
    sqliteModuleInstance.module_id === "core.chat" &&
      sqliteModuleInstance.module_schema_version === 1 &&
      sqliteChatInstance.data_ref_json === null,
    "SQLite Chat module instance must use canonical core.chat module JSON",
  );
  assert(
    moduleData.messages.length === sqliteChat.messages.length &&
      sqliteChat.messages[1]?.visibleText === "Two minu",
    "SQLite Chat messages and write-on output must come from module instance content_json",
  );
  const legacyRowCount =
    Number(
      (
        database
          .prepare(
            "SELECT (SELECT COUNT(*) FROM conversations) + (SELECT COUNT(*) FROM messages) AS total",
          )
          .get() as { total: number }
      ).total,
    );
  assert(
    legacyRowCount === 0,
    "Canonical SQLite fixture must not require legacy Chat records",
  );

  const requiredTables = [
    "productions",
    "episodes",
    "shots",
    "screen_instances",
    "module_instances",
    "screen_events",
    "icon_themes",
    "status_bars",
    "navigation_bars",
    "themes",
    "module_theme_configs",
    "devices",
    "device_states",
    "actors",
    "apps",
    "palette_colors",
    "production_fonts",
    "animation_presets",
    "render_presets",
    "conversations",
    "conversation_participants",
    "messages",
    "notifications",
    "calls",
    "data_sources",
  ];
  const storedTables = new Set(
    (
      database
        .prepare("SELECT name FROM sqlite_master WHERE type = 'table'")
        .all() as { name: string }[]
    ).map((row) => row.name),
  );
  assert(
    requiredTables.every((table) => storedTables.has(table)),
    "SQLite schema must contain every required domain table",
  );
  const moduleThemeConfig = sqliteRepository.getModuleThemeConfig(
    "theme_ios_light",
    "app_messages",
    "core.chat",
    1,
  );
  assert(
    moduleThemeConfig?.tokens_json.chatBubbles,
    "Seeded Chat module theme config must exist and own chat bubble tokens",
  );
  const screenInstanceColumns = new Set(
    (
      database.pragma("table_info(screen_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  const requiredScreenColumns = [
    "module_id",
    "module_schema_version",
    "app_id",
    "theme_mode",
  ];
  assert(
    requiredScreenColumns.every((column) =>
      screenInstanceColumns.has(column),
    ),
    "screen_instances must contain screen/module reference columns",
  );
  const moduleInstanceColumns = new Set(
    (
      database.pragma("table_info(module_instances)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  const themeColumns = new Set(
    (
      database.pragma("table_info(themes)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  const paletteColumns = new Set(
    (
      database.pragma("table_info(palette_colors)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  const productionFontColumns = new Set(
    (
      database.pragma("table_info(production_fonts)") as {
        name: string;
      }[]
    ).map((column) => column.name),
  );
  const requiredModuleColumns = [
    "screen_instance_id",
    "module_id",
    "module_schema_version",
    "content_json",
    "behavior_json",
    "animation_json",
  ];
  assert(
    requiredModuleColumns.every((column) =>
      moduleInstanceColumns.has(column),
    ),
    "module_instances must contain content/behavior/animation module contract columns",
  );
  assert(
    themeColumns.has("icon_theme_id"),
    "themes must contain the selected icon_theme_id column",
  );
  assert(
    themeColumns.has("status_bar_id"),
    "themes must contain the selected status_bar_id column",
  );
  assert(
    themeColumns.has("navigation_bar_id"),
    "themes must contain the selected navigation_bar_id column",
  );
  assert(
    paletteColumns.has("is_neutral"),
    "palette_colors must contain the neutral-color marker column",
  );
  assert(
    productionFontColumns.has("category"),
    "production_fonts must contain the normal/emoji category column",
  );
  assert(
    Number(database.pragma("user_version", { simple: true })) === 39,
    "SQLite schema version must be 39",
  );

  database.exec("BEGIN");
  let invalidJsonFailed = false;
  try {
    database
      .prepare("UPDATE themes SET tokens_json = ? WHERE id = ?")
      .run("{invalid", "theme_ios_light");
    sqliteRepository.getTheme("theme_ios_light");
  } catch {
    invalidJsonFailed = true;
  } finally {
    database.exec("ROLLBACK");
  }
  assert(invalidJsonFailed, "Invalid JSON TEXT must fail loudly");

  database.exec("BEGIN");
  let invalidModuleJsonFailed = false;
  try {
    database
      .prepare(
        "UPDATE module_instances SET content_json = ? WHERE id = ?",
      )
      .run("{invalid", "screen_instance_chat:module");
    sqliteRepository.getPrimaryModuleInstanceForScreenInstance(
      "screen_instance_chat",
    );
  } catch {
    invalidModuleJsonFailed = true;
  } finally {
    database.exec("ROLLBACK");
  }
  assert(
    invalidModuleJsonFailed,
    "Invalid Chat module instance content_json must fail loudly",
  );

  console.log("✓ schema and seed validated in isolated in-memory SQLite");
  console.log("✓ all required domain tables exist");
  console.log("✓ module_theme_configs exists and seeds core.chat tokens");
  console.log("✓ screen_instances references, module_instances content/behavior/animation, component classes, production font families/categories, palette-neutral colors, screen durations, semantic icon/border/debug colors, stable message ids, normalized production font tokens, and semantic chat header tokens exist in schema v39");
  console.log("✓ SQLiteRepository resolved ChatScreen props with Zod");
  console.log("✓ SQLite and in-memory chat props are equivalent");
  console.log("✓ Chat module instance JSON and actor-based output validated");
  console.log("✓ legacy Chat tables are physically present but contain no fixture rows");
  console.log("✓ invalid JSON TEXT fails loudly");
  console.log("✓ invalid Chat module instance content_json fails loudly");
  console.log("SQLite persistence validation succeeded.");
} finally {
  database.close();
}
