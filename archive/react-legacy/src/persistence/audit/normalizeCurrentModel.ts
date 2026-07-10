import { existsSync } from "node:fs";
import Database from "better-sqlite3";
import { developmentDatabasePath } from "../sqlite/paths.js";

type SQLiteDatabase = Database.Database;

interface Change {
  scope: string;
  message: string;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseJsonObject(value: string, scope: string) {
  const parsed = JSON.parse(value) as unknown;
  if (!isObject(parsed)) {
    throw new Error(`${scope} must be a JSON object`);
  }
  return parsed;
}

function writeJson(value: unknown) {
  return JSON.stringify(value);
}

function writeOnDuration(message: Record<string, unknown>) {
  const reveal = isObject(message.textReveal) ? message.textReveal : {};
  return typeof reveal.durationFrames === "number" &&
    Number.isFinite(reveal.durationFrames) &&
    reveal.durationFrames > 0
    ? reveal.durationFrames
    : 0;
}

function normalizeMessages(content: Record<string, unknown>, scope: string, changes: Change[]) {
  if (!Array.isArray(content.messages)) return false;
  let changed = false;
  let previousWriteOnEndFrame = 0;
  const messages = content.messages.map((entry, index) => {
    if (!isObject(entry)) return entry;
    const message = { ...entry };
    const legacyStartFrame =
      typeof message.startFrame === "number" && Number.isFinite(message.startFrame)
        ? Math.max(0, Math.round(message.startFrame))
        : undefined;
    const existingDelay =
      typeof message.delayAfterPreviousFrames === "number" &&
      Number.isFinite(message.delayAfterPreviousFrames)
        ? Math.max(0, Math.round(message.delayAfterPreviousFrames))
        : undefined;
    const delayAfterPreviousFrames =
      existingDelay ??
      (legacyStartFrame !== undefined
        ? Math.max(0, legacyStartFrame - previousWriteOnEndFrame)
        : 0);
    if (existingDelay === undefined || message.startFrame !== undefined) {
      message.delayAfterPreviousFrames = delayAfterPreviousFrames;
      delete message.startFrame;
      changes.push({
        scope: `${scope}.messages[${index}]`,
        message: `Converted legacy startFrame to delayAfterPreviousFrames=${delayAfterPreviousFrames}.`,
      });
      changed = true;
    }
    const effectiveStartFrame = previousWriteOnEndFrame + delayAfterPreviousFrames;
    previousWriteOnEndFrame = effectiveStartFrame + writeOnDuration(message);
    return message;
  });
  if (changed) {
    content.messages = messages;
  }
  return changed;
}

function normalizeCoreChatModuleThemes(database: SQLiteDatabase, changes: Change[]) {
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
    const tokens = parseJsonObject(
      row.tokens_json,
      `module_theme_configs.${row.id}.tokens_json`,
    );
    const modes = isObject(tokens.modes) ? { ...tokens.modes } : {};
    let changed = false;
    for (const mode of ["light", "dark"] as const) {
      const modeRoot = isObject(modes[mode]) ? { ...modes[mode] } : undefined;
      const chatBubbles = isObject(modeRoot?.chatBubbles)
        ? { ...modeRoot.chatBubbles }
        : undefined;
      if (chatBubbles && "contentMetaGap" in chatBubbles) {
        delete chatBubbles.contentMetaGap;
        modes[mode] = {
          ...modeRoot,
          chatBubbles,
        };
        changes.push({
          scope: `module_theme_configs.${row.id}.modes.${mode}.chatBubbles`,
          message: "Removed mode-level contentMetaGap; it is a root design value.",
        });
        changed = true;
      }
    }
    if (changed) {
      tokens.modes = modes;
      update.run(writeJson(tokens), row.id);
    }
  }
}

function normalizeCoreChatModuleInstances(database: SQLiteDatabase, changes: Change[]) {
  const rows = database
    .prepare(
      `SELECT id, content_json
       FROM module_instances
       WHERE module_id = 'core.chat' AND module_schema_version = 1`,
    )
    .all() as { id: string; content_json: string }[];
  const update = database.prepare(
    "UPDATE module_instances SET content_json = ? WHERE id = ?",
  );
  for (const row of rows) {
    const content = parseJsonObject(
      row.content_json,
      `module_instances.${row.id}.content_json`,
    );
    if (normalizeMessages(content, `module_instances.${row.id}.content_json`, changes)) {
      update.run(writeJson(content), row.id);
    }
  }
}

function normalizeDatabase(database: SQLiteDatabase, write: boolean) {
  const changes: Change[] = [];
  const transaction = database.transaction(() => {
    normalizeCoreChatModuleThemes(database, changes);
    normalizeCoreChatModuleInstances(database, changes);
    if (!write) {
      throw new Error("__DRY_RUN_ROLLBACK__");
    }
  });
  try {
    transaction();
  } catch (error) {
    if (!(error instanceof Error) || error.message !== "__DRY_RUN_ROLLBACK__") {
      throw error;
    }
  }
  return changes;
}

const write = process.argv.includes("--write");

if (!existsSync(developmentDatabasePath)) {
  console.error(`Development database not found at ${developmentDatabasePath}`);
  process.exit(1);
}

const database = new Database(developmentDatabasePath);
try {
  database.pragma("foreign_keys = ON");
  const changes = normalizeDatabase(database, write);
  if (changes.length === 0) {
    console.log("Current-model normalization: no changes needed.");
  } else {
    for (const change of changes) {
      console.log(`${write ? "WRITE" : "DRY"} ${change.scope}: ${change.message}`);
    }
    console.log(
      `Current-model normalization ${write ? "applied" : "dry-run"}: ${changes.length} change(s).`,
    );
    if (!write) {
      console.log("Run with -- --write to apply these changes to the development DB.");
    }
  }
} finally {
  database.close();
}

