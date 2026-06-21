import { loadExampleRepository } from "../../domain/repository/fixtureLoader.js";
import {
  resolveShot,
} from "../../domain/resolvers/index.js";
import {
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
} from "../../domain/schemas/index.js";
import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";
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

const database = createDatabase(developmentDatabasePath);

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
  const moduleData = ChatModuleDataSchema.parse(
    sqliteChatInstance.module_data_json,
  );
  assert(
    sqliteChatInstance.module_id === "core.chat" &&
      sqliteChatInstance.module_schema_version === 1 &&
      sqliteChatInstance.data_ref_json === null,
    "SQLite Chat instance must use canonical core.chat module JSON",
  );
  assert(
    moduleData.messages.length === sqliteChat.messages.length &&
      sqliteChat.messages[1]?.visibleText === "Two minu",
    "SQLite Chat messages and write-on output must come from module_data_json",
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
    "screen_templates",
    "screen_instances",
    "screen_events",
    "themes",
    "module_theme_configs",
    "devices",
    "device_states",
    "actors",
    "apps",
    "media_assets",
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
  const requiredModuleColumns = [
    "module_id",
    "module_schema_version",
    "theme_mode",
    "module_data_json",
    "module_config_json",
    "module_tokens_override_json",
  ];
  assert(
    requiredModuleColumns.every((column) =>
      screenInstanceColumns.has(column),
    ),
    "screen_instances must contain all module contract columns",
  );
  assert(
    Number(database.pragma("user_version", { simple: true })) === 5,
    "SQLite schema version must be 5",
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
        "UPDATE screen_instances SET module_data_json = ? WHERE id = ?",
      )
      .run("{invalid", "screen_instance_chat");
    sqliteRepository.getScreenInstancesForShot("shot_lock_to_chat");
  } catch {
    invalidModuleJsonFailed = true;
  } finally {
    database.exec("ROLLBACK");
  }
  assert(
    invalidModuleJsonFailed,
    "Invalid Chat module_data_json must fail loudly",
  );

  console.log(`✓ schema and seed validated at ${developmentDatabasePath}`);
  console.log("✓ all 21 required domain tables exist");
  console.log("✓ module_theme_configs exists and seeds core.chat tokens");
  console.log("✓ screen_instances module contract columns and schema v5 exist");
  console.log("✓ SQLiteRepository resolved ChatScreen props with Zod");
  console.log("✓ SQLite and in-memory chat props are equivalent");
  console.log("✓ Chat module JSON and sender-based output validated");
  console.log("✓ legacy Chat tables are physically present but contain no fixture rows");
  console.log("✓ invalid JSON TEXT fails loudly");
  console.log("✓ invalid Chat module_data_json fails loudly");
  console.log("SQLite persistence validation succeeded.");
} finally {
  database.close();
}
